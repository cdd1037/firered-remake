using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Generic preset-double entry and AI admission from the existing private party receipt.
/// The caller retains created for the active battle; the shared lifecycle owns every turn.</summary>
public static class SourcePresetTrainerEncounterEntry
{
    public static SourceTwinsEncounterResult Begin(SourceMechanicsCatalog source,
        SourceTrainerEntryPreparation setup, TrainerPartyCreationResult created,
        ImmutableArray<WildBattleCalledPartyMember> playerParty, GamePokemonCatalog? owned = null,
        bool attackBadgeBoost = false, bool defenseBadgeBoost = false, bool speedBadgeBoost = false,
        bool specialBadgeBoost = false, WildBattleTerrain terrain = WildBattleTerrain.Plain)
    {
        var initial = setup?.InitialRng ?? new RngState(0);
        SourceTwinsEncounterResult Fail(string fault) => new(null, initial, [], [], fault);
        if (setup?.Succeeded != true || CreationFault(source, created, owned) is { }) return Fail("source_preset_trainer_creation");
        if (created.Trace.IsDefaultOrEmpty || created.Trace[0].Before != setup.Rng.Seed ||
            created.Trace[0].Cursor - 1 != setup.Rng.Cursor) return Fail("source_preset_trainer_rng_chain");
        if (!Enum.IsDefined(terrain) || playerParty.IsDefault || playerParty.Length is < 2 or > 6)
            return Fail("source_preset_trainer_player_party");
        var catalog = source.Catalog;
        foreach (var member in playerParty)
            if (member?.Pokemon is null || owned is not null && !owned.Catalog.Species.ContainsKey(member.Pokemon.Species) ||
                SourcePokemonValidator.ValidatePartyMember(catalog, member, owned) is not null ||
                member.KnockedOffItem is not null || member.PersistentHeldItemOverride is not null ||
                !SourceFrameBerryItems.SupportsHeldItem(member.HeldItem, catalog) ||
                member.Status is not (WildBattleStatus.None or WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Freeze or WildBattleStatus.Paralysis) &&
                !WildBattleStatus.IsSleeping(member.Status) && member.Status != 0x80)
                return Fail("source_preset_trainer_player_member");
        var active = playerParty.Select((m, i) => (m, i)).Where(p => !p.m.IsEgg && p.m.Pokemon.CurrentHp > 0)
            .Take(2).Select(p => p.i).ToArray();
        if (active.Length != 2) return Fail("source_preset_trainer_two_usable_pokemon_required");
        try
        {
            var enemies = created.Party.Select(p => new WildBattleCalledPartyMember(p.Pokemon, p.HeldItem)).ToImmutableArray();
            var frame = new SourceBattleFrame {
                Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
                SourceMechanicsRulesVersion = SourceBattleKernel.Version, SourceCampaignRulesVersion = owned?.Opponents.RulesVersion, Campaign = owned,
                BattleKind = WildBattleCalledBattleKind.Trainer, Rng = created.Rng,
                Field = new() { Terrain = terrain },
                Parties = [new(BattleSide.Player, playerParty), new(BattleSide.Opponent, enemies)],
                Slots = Enumerable.Range(0, 4).Select(i => {
                    var side = (BattleSide)(i & 1); int index = side == BattleSide.Player ? active[i / 2] : i / 2;
                    var p = side == BattleSide.Player ? playerParty[index] : enemies[index];
                    return new SourceBattleSlot(new(i), new(side, index), 1, new WildBattleCombatant(p.Pokemon,
                        HeldItem: p.HeldItem, Status: p.Status, AttackBadgeBoost: side == BattleSide.Player && attackBadgeBoost) {
                        FirstTurnCounter = 2, DefenseBadgeBoost = side == BattleSide.Player && defenseBadgeBoost,
                        SpeedBadgeBoost = side == BattleSide.Player && speedBadgeBoost,
                        SpecialBadgeBoost = side == BattleSide.Player && specialBadgeBoost });
                }).ToImmutableArray()
            };
            // TrainerStarted is reserved for the frame-owned singles/Tower contexts.
            // Ordinary doubles retain their authenticated creation receipt in the caller.
            var start = SourceBattleEventHooks.Publish(frame, (n, t) => new SourceStartedEvent(n, t));
            var entry = SourceFourEntryAbilities.InitializeEntry(catalog, start.State, start.State.Revision);
            if (!entry.Succeeded) return Fail(entry.Fault!);
            var draw = Gen3Rng.Next(entry.State.Rng, "preset-trainer.entry.random-turn-number");
            var ready = SourceBattleEventHooks.Publish(entry.State with { Rng = draw.State, RandomTurnNumber = draw.Value },
                (n, t) => new SourceTurnReadyEvent(n, t, draw.Value));
            if (ValidateFrame(source, created, ready.State, owned) is { } invalid) return Fail(invalid);
            return new(ready.State, ready.State.Rng, [start.Event, .. entry.Events, ready.Event],
                [.. setup.Trace, .. created.Trace, .. entry.Trace, draw.Trace], null);
        }
        catch (OverflowException) { return Fail("source_preset_trainer_counter_exhausted"); }
        catch (ArgumentException) { return Fail("source_preset_trainer_invalid_state"); }
        catch (NotSupportedException) { return Fail("source_preset_trainer_unsupported"); }
    }

    public static string? ValidateFrame(SourceMechanicsCatalog source, TrainerPartyCreationResult created,
        SourceBattleFrame frame, GamePokemonCatalog? owned = null)
    {
        if (CreationFault(source, created, owned) is { } creation) return creation;
        if (frame is null || frame.Layout != SourceBattleLayout.PresetDoubles || frame.BattleKind != WildBattleCalledBattleKind.Trainer ||
            frame.KoReplacementTiming != KoReplacementTiming.EndOfTurn || frame.SourceCampaignRulesVersion != owned?.Opponents.RulesVersion ||
            frame.TrainerEncounter is not null || frame.TrainerTowerEncounter is not null ||
            SourceFourActionTurns.ValidateCapability(source.Catalog, frame) is not null ||
            SourcePokemonValidator.ValidateFrameParties(source.Catalog, frame) is not null)
            return "source_preset_trainer_frame";
        var party = frame.Party(BattleSide.Opponent).Members;
        if (party.Length != created.Party.Length || party.Where((p, i) => p.IsEgg ||
                !created.Roster!.MatchesIdentity(p.Pokemon, i)).Any()) return "source_preset_trainer_roster";
        return null;
    }

    public static SourceTwinsAiResult ChooseEnemyActions(SourceMechanicsCatalog source, TrainerPartyCreationResult created,
        SourceBattleFrame frame, long expectedRevision, GamePokemonCatalog? owned = null, string? knownPlayerAbility = null)
    {
        if (ValidateFrame(source, created, frame, owned) is { } invalid) return new(frame, [], [], invalid);
        var result = SourceFourTrainerAi.ChooseMoves(source.Catalog, created.Trainer, frame, expectedRevision, knownPlayerAbility);
        return new(result.State, result.Choices.Select(c => new SourceTwinsEnemyChoice(c.Actor, c.ScoredTarget,
            c.Selection, c.Scores, c.SimulatedDamagePercent, c.AbilityReads)).ToImmutableArray(), result.Trace, result.Fault);
    }

    /// <summary>Full source controller: switches, shared trainer inventory, then move selection.
    /// The caller persists returned Inventory/SourceCurrentMove alongside its active receipt.</summary>
    public static SourceTrainerActionPreparationResult PrepareEnemyActions(SourceBattleItemCatalog items,
        SourceMechanicsCatalog source, TrainerPartyCreationResult created, SourceBattleFrame frame,
        long expectedRevision, SourceTrainerItemInventory inventory, MoveId staleCurrentMove,
        GamePokemonCatalog? owned = null, string? knownPlayerAbility = null, string? knownPlayerHoldEffect = null)
    {
        if (ValidateFrame(source, created, frame, owned) is { } invalid)
            return new(frame, inventory, [], [], [], invalid) { SourceCurrentMove = staleCurrentMove };
        return SourceFourTrainerAi.PrepareEnemyActions(items, source.Catalog, created.Trainer, frame,
            expectedRevision, inventory, staleCurrentMove, knownPlayerAbility, knownPlayerHoldEffect);
    }

    private static string? CreationFault(SourceMechanicsCatalog? source, TrainerPartyCreationResult? created, GamePokemonCatalog? owned)
    {
        if (source is null || source.Catalog.MechanicsData is null ||
            created?.Roster is not { } proof || !proof.Matches(created) || !created.Trainer.DoubleBattle ||
            created.Party.Length is < 2 or > 6 || created.Trainer.AiFlags is not (1 or 7) ||
            created.Party.Any(p => SourcePokemonValidator.ValidateForBattle(source.Catalog, p.Pokemon) is not null ||
                !SourceFrameBerryItems.SupportsHeldItem(p.HeldItem, source.Catalog))) return "source_preset_trainer_creation";
        if (owned is not null && (!ReferenceEquals(owned.Mechanics.Catalog, source.Catalog) ||
            created.Party.Any(p => !owned.Opponents.Catalog.Species.ContainsKey(p.Pokemon.Species))))
            return "source_preset_trainer_campaign_admission";
        return null;
    }
}
