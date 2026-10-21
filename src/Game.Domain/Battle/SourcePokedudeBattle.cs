using System.Collections.Immutable;
using Game.Domain.Capture;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public sealed record SourcePokedudePartyDefinition(BattleSide Side, int Level, SpeciesId Species,
    ImmutableArray<MoveId> Moves, Nature Nature, PokemonGender Gender, int FixedIv = 0);
public sealed record SourcePokedudeBadges(bool Attack = false, bool Defense = false, bool Speed = false, bool Special = false);
public sealed record SourcePokedudeContext(int LessonId)
{
    public const string Version = "firered-pokedude-battle-v1";
    public string RulesVersion { get; init; } = Version;
}

/// <summary>Temporary Teachy TV parties and source flag policy over the existing single-battle
/// owner. The caller keeps campaign state outside this frame and restores it on every exit.</summary>
public static class SourcePokedudeBattle
{
    public static SourceWildEntryResult Start(SourceMechanicsCatalog mechanics, int lessonId,
        ImmutableArray<SourcePokedudePartyDefinition> party, uint playerTrainerId, string playerName,
        string currentRegion, RngState rng, SourcePokedudeBadges? badges = null)
    {
        SourceWildEntryResult Fail(string why) => new(null, [], [], why);
        if (mechanics is null || lessonId is < 0 or > 3 || party.IsDefaultOrEmpty || party.Length is < 2 or > 3 ||
            party.Any(p => p is null || !Enum.IsDefined(p.Side) || !Enum.IsDefined(p.Nature) || !Enum.IsDefined(p.Gender) ||
                p.FixedIv != 0 || p.Level is < 1 or > 100 || !mechanics.Catalog.Species.ContainsKey(p.Species) ||
                p.Moves.IsDefaultOrEmpty || p.Moves.Length > 4 || p.Moves.Any(m => !mechanics.Catalog.Moves.ContainsKey(m))) )
            return Fail("pokedude_party_definition");
        if (party.Count(p => p.Side == BattleSide.Opponent) != 1 || party.Count(p => p.Side == BattleSide.Player) is < 1 or > 2)
            return Fail("pokedude_party_sides");
        if (!NameTextRules.IsValidTrainerName(playerName) ||
            string.IsNullOrEmpty(currentRegion) || !currentRegion.StartsWith("MAPSEC_", StringComparison.Ordinal))
            return Fail("pokedude_player_metadata");
        if (party.Any(p => mechanics.Catalog.Species[p.Species].GenderRatio switch {
            0 => p.Gender != PokemonGender.Male, 254 => p.Gender != PokemonGender.Female,
            255 => p.Gender != PokemonGender.Genderless, _ => p.Gender == PokemonGender.Genderless }))
            return Fail("pokedude_impossible_gender");
        badges ??= new();
        try
        {
            var trace = ImmutableArray.CreateBuilder<RandomTrace>();
            var players = ImmutableArray.CreateBuilder<WildBattleCombatant>();
            WildBattleCombatant? opponent = null;
            foreach (var definition in party)
            {
                var species = mechanics.Catalog.Species[definition.Species];
                uint personality;
                // CreateMonWithGenderNatureLetter(..., fixedIV=0, letter=0): no IV draws,
                // no seed reset, no fabricated personality. Both sides use the player's OT.
                do
                {
                    var low = Gen3Rng.Next(rng, "pokedude.personality.low16");
                    var high = Gen3Rng.Next(low.State, "pokedude.personality.high16");
                    trace.Add(low.Trace); trace.Add(high.Trace); rng = high.State;
                    personality = (uint)low.Value | ((uint)high.Value << 16);
                } while (personality % 25 != (uint)definition.Nature ||
                    PokemonFactory.GenderFromPersonality(species.GenderRatio, personality) != definition.Gender);
                var made = SourcePokemonFactory.Create(mechanics,
                    new(definition.Species, definition.Level, playerTrainerId, IvPolicy.Fixed(0), PersonalityPolicy.Fixed(personality)), rng);
                rng = made.Rng; trace.AddRange(made.Trace);
                var pokemon = made.Pokemon with { Moves = definition.Moves.Select(m =>
                    new PokemonMoveSlot(m, mechanics.Catalog.Moves[m].Pp, mechanics.Catalog.Moves[m].Pp)).ToImmutableArray() };
                bool player = definition.Side == BattleSide.Player;
                var body = new WildBattleCombatant(pokemon, AttackBadgeBoost: player && badges.Attack) {
                    DefenseBadgeBoost = player && badges.Defense, SpeedBadgeBoost = player && badges.Speed,
                    SpecialBadgeBoost = player && badges.Special };
                if (player) players.Add(body); else opponent = body;
            }
            // The mechanics-only entry consumes the original four AI-setup draws and turn
            // number; no wild held-item draw, table encounter roll, campaign/dex admission.
            var entry = SourceWildBattlePolicy.Start(mechanics, players.ToImmutable(), 0, opponent!, rng);
            if (!entry.Succeeded) return entry;
            var frame = entry.State! with { Pokedude = new(lessonId), Rewards = new() {
                Context = new(SourceRewardBattleKind.OrdinaryWild, playerTrainerId, playerName, currentRegion),
                Metadata = players.Select((_, i) => new SourceRewardMetadata(new(BattleSide.Player, i),
                    playerName, currentRegion, "ITEM_POKE_BALL", SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray() } };
            if (SourceFourActionTurns.ValidateCapability(mechanics.Catalog, frame) is { } invalid) return Fail(invalid);
            return new(frame, entry.Events, trace.ToImmutable().AddRange(entry.Trace), null) { EntrySlots = entry.EntrySlots };
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or NotSupportedException)
        { return Fail(ex.Message); }
    }

    internal static string? ValidateFrame(SourceBattleFrame frame)
    {
        if (frame.Pokedude is not { } context) return null;
        return context.RulesVersion != SourcePokedudeContext.Version || context.LessonId is < 0 or > 3 ||
            frame.Layout != SourceBattleLayout.Singles || frame.SourceMechanicsRulesVersion != SourceBattleKernel.Version ||
            frame.SourceCampaignRulesVersion is not null || frame.TrainerEncounter is not null || frame.TrainerTowerEncounter is not null ||
            frame.BattleKind != WildBattleCalledBattleKind.Wild || frame.WildEncounter is not { } encounter ||
            // Temporary battles have no encounter provenance, but retain the shared
            // terminal outcome. SourceWildBattlePolicy validates its value below.
            (encounter with { SourceOutcome = null }) != new SourceWildEncounterContext() ||
            frame.Outcome != WildBattleOutcome.Ongoing && encounter.SourceOutcome is null ||
            frame.Obedience is not null || frame.Parties.Any(p => p.Members.Any(m => m.IsEgg || m.HeldItem != "ITEM_NONE"))
            ? "pokedude_frame_context" : null;
    }

    /// <summary>Antidote is applied in the temporary party menu, before the item turn marker,
    /// exactly as ordinary player medicine. No real bag is inspected, spent or overwritten.</summary>
    public static SourceTrainerItemPreparation PrepareAntidote(SourceBattleItemCatalog items, PokemonCatalog catalog,
        SourceBattleFrame frame, long expectedRevision)
    {
        SourceTrainerItemPreparation Fail(string why) => new(new(frame, [], [], why), null);
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } invalid) return Fail(invalid);
        if (frame.Pokedude?.LessonId != 1 || frame.Revision != expectedRevision || frame.FourTurn is not null ||
            frame.Outcome != WildBattleOutcome.Ongoing || !frame.Queue.IsEmpty || frame.PreparedMedicines is not null)
            return Fail("pokedude_antidote_phase");
        var actor = frame.Get(new(0)).Occupant!.Value;
        var item = new ItemId("pret:ITEM_ANTIDOTE");
        var applied = SourceBattleItemEffects.Apply(items, frame, actor, new(BattleSide.Player, 0), item);
        if (!applied.Succeeded) return Fail(applied.Fault!);
        var published = SourceBattleItemEffects.PublishChanges(frame, applied.State, actor, actor.Member, item, 0);
        var next = published.State with { Revision = checked(frame.Revision + 1),
            PreparedMedicines = [new(actor, item, actor.Member) { SourceAbsentMaskBeforeSelection = frame.SourceAbsentMask }] };
        return new(new(next, published.Events, [], null), new(actor, SourceActionKind.Item, Item: item));
    }

    public static SourceActionSelection CatchSelection(SourceBattleFrame frame)
    {
        if (frame.Pokedude?.LessonId != 3) throw new ArgumentException("pokedude_catch_lesson");
        return new(frame.Get(new(0)).Occupant!.Value, SourceActionKind.Capture) {
            Capture = new(CaptureBall.Poke, new(false, ImmutableHashSet<SpeciesId>.Empty, CaptureEncounterKind.OrdinaryWild)) };
    }

    public static SourceFourActionResult Abort(PokemonCatalog catalog, SourceBattleFrame frame, long expectedRevision)
    {
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } invalid) return new(frame, [], [], invalid);
        if (frame.Pokedude is null || frame.Revision != expectedRevision || frame.Outcome != WildBattleOutcome.Ongoing)
            return new(frame, [], [], "pokedude_abort_phase");
        var next = SourceBattleOutcomeRules.SetOutcome(frame with { Revision = checked(frame.Revision + 1),
            Queue = [], NextAction = 0, FourTurn = null, PendingSwitches = [], Reservations = [], PreparedMedicines = null }, WildBattleOutcome.Draw);
        var ended = SourceBattleEventHooks.Publish(next, (n, t) => new SourceEndedEvent(n, t, WildBattleOutcome.Draw));
        return new(ended.State, [ended.Event], [], null);
    }
}
