using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

public sealed record SourceTwinsBattleDefinition(string ScriptEntry, string IntroText, string DefeatText,
    string PostBattleText, string NotEnoughMonsText, TrainerSourceRef Source);
public sealed record SourceTwinsTrainerDefinition(
    TrainerDefinition Trainer, ImmutableArray<SourceTwinsBattleDefinition> Interactions,
    string EntryScript, string Callback, int BattleMode, uint BattleTypeFlags,
    string EncounterMusic, int EncounterMusicId, int TrainerFlag, int VictoryMoney);
public sealed record SourceTwinsEncounterResult(SourceBattleFrame? State, RngState Rng,
    ImmutableArray<SourceBattleEvent> Events, ImmutableArray<RandomTrace> Trace, string? Fault)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public ImmutableArray<SourceBattleSlot> EntrySlots { get; init; } = [];
    public bool Succeeded => Fault is null;
}
public sealed record SourceTwinsEnemyChoice(BattlerSlot Actor, BattlerSlot ScoredTarget,
    SourceActionSelection Selection, ImmutableArray<int> Scores,
    ImmutableArray<int> SimulatedDamagePercent, ImmutableArray<ForestTrainerAbilityRead> AbilityReads);
public sealed record SourceTwinsAiResult(SourceBattleFrame State, ImmutableArray<SourceTwinsEnemyChoice> Choices,
    ImmutableArray<RandomTrace> Trace, string? Fault)
{
    public bool Succeeded => Fault is null;
    public ImmutableArray<SourceActionSelection> Selections => Choices.Select(c => c.Selection).ToImmutableArray();
}

/// <summary>Original Route8 shared trainer, with a single four-slot battle and real complete parties.
/// Encounter and CHECK_BAD_MOVE admission are independent of move recipe completion.</summary>
public static class SourceTwinsTrainerEncounter
{
    public const string Version = "firered-route8-twins-encounter-v1";
    public static TrainerId TrainerId { get; } = new("pret:TRAINER_TWINS_ELI_ANNE");
    public const int SharedTrainerFlag = 0x500 + 484;
    public const int VictoryMoney = 528;
    public static string? ValidateDefinition(SourceTwinsTrainerDefinition? definition)
    {
        if (definition is not { Trainer: { } t } || t.Id != TrainerId || !t.DoubleBattle || t.Party.IsDefault || t.Party.Length != 2 || definition.Interactions.IsDefault || definition.Interactions.Length != 2 ||
            definition.EntryScript != "EventScript_TryDoDoubleTrainerBattle" || definition.Callback != "CB2_EndTrainerBattle" ||
            definition.BattleMode != 4 || definition.BattleTypeFlags != 9 || definition.EncounterMusic != "MUS_ENCOUNTER_GIRL" ||
            definition.EncounterMusicId != 284 || definition.TrainerFlag != SharedTrainerFlag || definition.VictoryMoney != VictoryMoney)
            return "source_twins_definition";
        for (int i = 0; i < 2; i++)
        {
            string name = i == 0 ? "Eli" : "Anne"; var b = definition.Interactions[i];
            if (b is null || b.ScriptEntry != $"Route8_EventScript_{name}" || b.IntroText != $"Route8_Text_{name}Intro" ||
                b.DefeatText != $"Route8_Text_{name}Defeat" || b.PostBattleText != $"Route8_Text_{name}PostBattle" ||
                b.NotEnoughMonsText != $"Route8_Text_{name}NotEnoughMons") return "source_twins_roster_or_interaction";
        }
        return null;
    }

    public static SourceTwinsEncounterResult Begin(PokemonCatalog catalog, SourceTwinsTrainerDefinition definition,
        ImmutableArray<WildBattleCalledPartyMember> playerParty, RngState rng,
        bool attackBadgeBoost = false, bool defenseBadgeBoost = false, bool speedBadgeBoost = false, GamePokemonCatalog? successorOwned = null)
    {
        SourceTwinsEncounterResult Fail(string code) => new(null, rng, [], [], code);
        if (ValidateDefinition(definition) is { } error) return Fail(error);
        if (catalog?.MechanicsData is null) return Fail("source_twins_catalog_proof");
        if (successorOwned is not null && !ReferenceEquals(successorOwned.Mechanics.Catalog, catalog))
            return Fail("source_twins_successor_owned");
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fail("source_twins_rng");
        if (playerParty.IsDefault || playerParty.Length is < 2 or > 6) return Fail("source_twins_player_party");
        foreach (var member in playerParty)
            if (member?.Pokemon is null || successorOwned is not null && !successorOwned.Catalog.Species.ContainsKey(member.Pokemon.Species) || SourcePokemonValidator.ValidatePartyMember(catalog, member, successorOwned) is not null ||
                member.KnockedOffItem is not null || member.PersistentHeldItemOverride is not null ||
                !SourceFrameBerryItems.SupportsHeldItem(member.HeldItem, catalog) ||
                member.Status is not (WildBattleStatus.None or WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Freeze or WildBattleStatus.Paralysis) &&
                !WildBattleStatus.IsSleeping(member.Status) && !WildBattleSourceFieldEffects.IsValidToxic(member.Status))
                return Fail("source_twins_player_member");
        var active = playerParty.Select((m, i) => (m, i)).Where(x => !x.m.IsEgg && x.m.Pokemon.CurrentHp > 0).Take(2).Select(x => x.i).ToArray();
        if (active.Length != 2) return Fail("source_twins_two_usable_pokemon_required");
        try
        {
            // SetUpBattleVars runs while flags are TRAINER only: four setup draws.
            // CreateNPCTrainerParty adds DOUBLE after generation, so there is no initial flank draw.
            var setupTrace = ImmutableArray.CreateBuilder<RandomTrace>(); var generationRng = rng;
            for (int slot = 0; slot < 4; slot++) {
                var draw = Gen3Rng.Next(generationRng, $"twins.entry.ai-setup.slot-{slot}", 16);
                generationRng = draw.State; setupTrace.Add(draw.Trace);
            }
            var generated = TrainerPartyFactory.CreateValidated(definition.Trainer, catalog, generationRng, SourceMechanicsCatalog.FromDefinitions(catalog));
            var enemy = generated.Party.Select(m => new WildBattleCalledPartyMember(m.Pokemon, m.HeldItem)).ToImmutableArray();
            var frame = new SourceBattleFrame {
                Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
                SourceMechanicsRulesVersion = SourceBattleKernel.Version, SourceCampaignRulesVersion = successorOwned?.Opponents.RulesVersion, Campaign = successorOwned, BattleKind = WildBattleCalledBattleKind.Trainer,
                Rng = generated.Rng, Parties = [new(BattleSide.Player, playerParty), new(BattleSide.Opponent, enemy)],
                Slots = Enumerable.Range(0, 4).Select(i => {
                    var side = (BattleSide)(i & 1); int index = side == BattleSide.Player ? active[i / 2] : i / 2;
                    var p = side == BattleSide.Player ? playerParty[index] : enemy[index];
                    return new SourceBattleSlot(new(i), new(side, index), 1, new WildBattleCombatant(p.Pokemon,
                        HeldItem: p.HeldItem, Status: p.Status, AttackBadgeBoost: side == BattleSide.Player && attackBadgeBoost) {
                        FirstTurnCounter = 2, DefenseBadgeBoost = side == BattleSide.Player && defenseBadgeBoost,
                        SpeedBadgeBoost = side == BattleSide.Player && speedBadgeBoost });
                }).ToImmutableArray()
            };
            var entrySlots = frame.Slots;
            var entry = SourceFourEntryAbilities.InitializeEntry(catalog, frame, frame.Revision);
            if (!entry.Succeeded) return Fail(entry.Fault!);
            var turnNumber = Gen3Rng.Next(entry.State.Rng, "twins.entry.random-turn-number");
            var ready = SourceBattleEventHooks.Publish(entry.State with { Rng = turnNumber.State, RandomTurnNumber = turnNumber.Value },
                (n, t) => new SourceTurnReadyEvent(n, t, turnNumber.Value));
            return new(ready.State, ready.State.Rng, [.. entry.Events, ready.Event],
                [.. setupTrace, .. generated.Trace, .. entry.Trace, turnNumber.Trace], null) { EntrySlots = entrySlots };
        }
        catch (OverflowException) { return Fail("source_twins_rng_or_counter_exhausted"); }
        catch (ArgumentException) { return Fail("source_twins_invalid_generation"); }
        catch (NotSupportedException) { return Fail("source_twins_generation_not_supported"); }
    }

    public static SourceTwinsAiResult ChooseEnemyActions(PokemonCatalog catalog, SourceTwinsTrainerDefinition definition, SourceBattleFrame frame,
        long expectedRevision, string? knownPlayerAbility = null)
    {
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } capability)
            return new(frame, [], [], capability);
        if (ValidateDefinition(definition) is not null || !OriginalEnemyParty(catalog, definition.Trainer, frame)) return new(frame, [], [], "source_twins_ai_original_roster");
        var result = SourceFourTrainerAi.ChooseCore(catalog, frame, expectedRevision, definition.Trainer.AiFlags, knownPlayerAbility, "twins");
        return new(result.State, result.Choices.Select(c => new SourceTwinsEnemyChoice(c.Actor, c.ScoredTarget,
            c.Selection, c.Scores, c.SimulatedDamagePercent, c.AbilityReads)).ToImmutableArray(), result.Trace, result.Fault);
    }

    private static bool OriginalEnemyParty(PokemonCatalog catalog, TrainerDefinition trainer, SourceBattleFrame frame)
    {
        var party = frame.Party(BattleSide.Opponent).Members;
        if (party.Length != trainer.Party.Length) return false;
        uint nameHash = 0;
        for (int i = 0; i < party.Length; i++)
        {
            var expected = trainer.Party[i];
            foreach (int c in trainer.EncodedTrainerName.Concat(expected.EncodedSpeciesName)) nameHash = unchecked(nameHash + (uint)c);
            var member = party[i]; var pokemon = member.Pokemon;
            var moves = (trainer.PartyFlags & 1) != 0
                ? expected.CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                : PokemonMoveRules.InitialMovesCore(catalog, catalog.Species[expected.Species], expected.Level).Select(m => m.Move);
            int iv = expected.SourceIv * 31 / 255;
            if (member.IsEgg || pokemon.Species != expected.Species || pokemon.SourceSpeciesId != expected.SourceSpeciesId ||
                pokemon.Level != expected.Level || pokemon.Personality != unchecked(0x80u + (nameHash << 8)) ||
                pokemon.Ivs != new SixStats(iv, iv, iv, iv, iv, iv) || pokemon.Evs != SixStats.Zero ||
                pokemon.Moves.Any(m => m.PpUps != 0) || !pokemon.Moves.Select(m => m.Move).SequenceEqual(moves)) return false;
        }
        return true;
    }
}
