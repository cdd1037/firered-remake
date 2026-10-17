using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

[Flags]
public enum SourceTrainerTowerBattleFlags : uint { Double = 1, Trainer = 8, TrainerTower = 1 << 19 }

/// <summary>Explicit Tower encounter and its generated preset roster.</summary>
public sealed record SourceTrainerTowerEncounterContext(TrainerTowerEncounterRoster Roster);

/// <summary>Explicit facility settlement, with ordinary victory Pickup and persistent battle damage.
/// The Application owns the nurse/lobby callback and inventory projection.</summary>
public sealed record SourceTrainerTowerSettlement(WildBattleOutcome Outcome, int Money,
    int Experience, int EffortValues, int TrainerPrize, int PayDay, int WhiteoutPenalty,
    bool PerformPickup, bool HealAndReturnToLobby);

public static class SourceTrainerTowerEncounter
{
    public const string Version = "firered-local-trainer-tower-encounter-v1";

    /// <summary>Enemy creation has no RNG draws. Source SetUpBattleVars follows BuildEnemyParty;
    /// unlike ordinary doubles, Tower already has DOUBLE set and consumes the fifth AI target draw.</summary>
    public static SourceTrainerEncounterEntryResult Begin(GameOpponentPokemonCatalog opponents,
        TrainerPartyCreationResult created, ImmutableArray<WildBattleCalledPartyMember> playerParty,
        ForestTrainerBattleStyle style = ForestTrainerBattleStyle.Shift,
        bool attackBadgeBoost = false, bool defenseBadgeBoost = false, bool speedBadgeBoost = false,
        bool specialBadgeBoost = false, WildBattleTerrain terrain = WildBattleTerrain.Building,
        GamePokemonCatalog? currentOwned = null)
    {
        var initial = created?.Rng ?? new RngState(0);
        SourceTrainerEncounterEntryResult Fail(string fault) => new(null, initial, [], [], fault);
        if (opponents is null || opponents.RulesVersion != GameOpponentPokemonCatalog.TrainerTowerProfile ||
            created?.TowerRoster is not { } proof || !proof.Matches(created))
            return Fail("source_tower_creation_receipt");
        var current = currentOwned ?? opponents.Owned;
        if (!ReferenceEquals(current, opponents.Owned)) return Fail("source_tower_current_owned");
        var catalog = opponents.Mechanics.Catalog;
        if (!Enum.IsDefined(style) || !Enum.IsDefined(terrain) || playerParty.IsDefaultOrEmpty || playerParty.Length > 6)
            return Fail("source_tower_player_party");
        foreach (var member in playerParty)
            if (member?.Pokemon is null || !current.Catalog.Species.ContainsKey(member.Pokemon.Species) ||
                SourcePokemonValidator.ValidatePartyMember(catalog, member, current) is not null ||
                member.KnockedOffItem is not null || member.PersistentHeldItemOverride is not null ||
                !SourceFrameBerryItems.SupportsHeldItem(member.HeldItem, catalog) ||
                member.Status is not (WildBattleStatus.None or WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Freeze or WildBattleStatus.Paralysis) &&
                !WildBattleStatus.IsSleeping(member.Status) && member.Status != 0x80)
                return Fail("source_tower_player_member");
        var active = playerParty.Select((m, i) => (m, i)).Where(x => !x.m.IsEgg && x.m.Pokemon.CurrentHp > 0)
            .Take(proof.IsDouble ? 2 : 1).Select(x => x.i).ToArray();
        if (active.Length != (proof.IsDouble ? 2 : 1)) return Fail(proof.IsDouble ? "source_tower_two_usable_pokemon_required" : "source_tower_usable_pokemon_required");
        int maximum = playerParty.Where(m => !m.IsEgg).Max(m => m.Pokemon.Level);
        if (created.Party.Any(m => m.Pokemon.Level != maximum)) return Fail("source_tower_party_maximum_level");
        try
        {
            SourceTrainerEncounterContext? trainer = null;
            if (!proof.IsDouble)
            {
                var withReceipt = created with { Roster = TrainerEncounterRoster.CreateTower(created, proof) };
                var context = SourceTrainerEncounterPolicy.CreateContext(catalog, withReceipt, style);
                if (!context.Succeeded) return Fail(context.Fault!);
                trainer = context.Context;
            }
            var setup = SourceTrainerEncounterEntry.Prepare(initial, "trainer-tower");
            if (!setup.Succeeded) return Fail(setup.Fault!);
            var rng = setup.Rng; var trace = ImmutableArray.CreateBuilder<RandomTrace>(); trace.AddRange(created.Trace); trace.AddRange(setup.Trace);
            if (proof.IsDouble)
            {
                var flank = Gen3Rng.Next(rng, "trainer-tower.entry.ai-setup.target"); rng = flank.State; trace.Add(flank.Trace);
            }
            var enemy = created.Party.Select(p => new WildBattleCalledPartyMember(p.Pokemon, p.HeldItem)).ToImmutableArray();
            var slots = Enumerable.Range(0, 4).Select(i => {
                if (!proof.IsDouble && i > 1) return new SourceBattleSlot(new(i), null, 0, null);
                var side = (BattleSide)(i & 1); int index = side == BattleSide.Player ? active[i / 2] : i / 2;
                var member = side == BattleSide.Player ? playerParty[index] : enemy[index];
                return new SourceBattleSlot(new(i), new(side, index), 1,
                    new WildBattleCombatant(member.Pokemon, HeldItem: member.HeldItem, Status: member.Status,
                        AttackBadgeBoost: side == BattleSide.Player && attackBadgeBoost) {
                        FirstTurnCounter = 2, DefenseBadgeBoost = side == BattleSide.Player && defenseBadgeBoost,
                        SpeedBadgeBoost = side == BattleSide.Player && speedBadgeBoost,
                        SpecialBadgeBoost = side == BattleSide.Player && specialBadgeBoost });
            }).ToImmutableArray();
            var frame = new SourceBattleFrame {
                SourceMechanicsRulesVersion = SourceBattleKernel.Version,
                SourceCampaignRulesVersion = current.Opponents.RulesVersion, Campaign = current,
                Layout = proof.IsDouble ? SourceBattleLayout.PresetDoubles : SourceBattleLayout.Singles,
                KoReplacementTiming = proof.IsDouble ? KoReplacementTiming.EndOfTurn : KoReplacementTiming.SourceImmediate,
                BattleKind = WildBattleCalledBattleKind.Trainer, TrainerEncounter = trainer,
                TrainerTowerEncounter = new(proof),
                Rng = rng, SourceAbsentMask = (byte)(proof.IsDouble ? 0 : 12), TurnStartAbsentMask = (byte)(proof.IsDouble ? 0 : 12),
                Parties = [new(BattleSide.Player, playerParty), new(BattleSide.Opponent, enemy)], Slots = slots,
                Field = new() { Terrain = terrain }, Rewards = new()
            };
            var started = SourceBattleEventHooks.Publish(frame, (n, t) => new SourceTrainerStartedEvent(n, t, created.Trainer.Id, enemy.Length));
            var start = SourceBattleEventHooks.Publish(started.State, (n, t) => new SourceStartedEvent(n, t));
            var entrySlots = start.State.Slots;
            var entry = SourceFourEntryAbilities.InitializeEntry(catalog, start.State, start.State.Revision);
            if (!entry.Succeeded) return Fail(entry.Fault!);
            var draw = Gen3Rng.Next(entry.State.Rng, "trainer-tower.entry.random-turn-number");
            var ready = SourceBattleEventHooks.Publish(entry.State with { Rng = draw.State, RandomTurnNumber = draw.Value },
                (n, t) => new SourceTurnReadyEvent(n, t, draw.Value));
            return new(ready.State, ready.State.Rng, [started.Event, start.Event, .. entry.Events, ready.Event], [.. trace, .. entry.Trace, draw.Trace], null) { EntrySlots = entrySlots };
        }
        catch (OverflowException) { return Fail("source_tower_counter_exhausted"); }
        catch (ArgumentException e) { return Fail("source_tower_invalid_entry:" + e.Message); }
    }

    public static SourceTrainerActionPreparationResult PrepareActions(SourceBattleItemCatalog items, PokemonCatalog catalog,
        SourceBattleFrame frame, long expectedRevision, GamePokemonCatalog? currentOwned = null)
    {
        var error = Validate(catalog, frame, currentOwned);
        if (error is not null || frame.TrainerTowerEncounter?.Roster is not { } proof)
            return new(frame, null!, [], [], [], error ?? "source_tower_encounter_required");
        if (frame.TrainerEncounter is not null) return SourceTrainerEncounterPolicy.PrepareActions(items, catalog, frame, expectedRevision);
        return SourceFourTrainerAi.PrepareEnemyActions(items, catalog, proof.Trainer, frame, expectedRevision,
            SourceTrainerItemInventory.Create(proof.Trainer), SourceTrainerEncounterPolicy.NoMove);
    }

    public static SourceTrainerTowerSettlement Settle(PokemonCatalog catalog, SourceBattleFrame frame, int money, GamePokemonCatalog? currentOwned = null)
    {
        if (Validate(catalog, frame, currentOwned) is { } error) throw new ArgumentException(error, nameof(frame));
        if (frame.TrainerTowerEncounter is null || frame.FourTurn is { Phase: not SourceFourActionPhase.BattleEnded } ||
            frame.Outcome is not (WildBattleOutcome.Won or WildBattleOutcome.Lost or WildBattleOutcome.Draw) ||
            money is < 0 or > SourceBattleMoneyRules.MaximumMoney) throw new ArgumentException("source_tower_settlement");
        return new(frame.Outcome, money, 0, 0, 0, 0, 0, frame.Outcome == WildBattleOutcome.Won,
            frame.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw);
    }

    public static string? Validate(PokemonCatalog catalog, SourceBattleFrame frame, GamePokemonCatalog? currentOwned = null)
    {
        if (frame is null || catalog is null) return "source_tower_missing_input";
        if (frame.TrainerTowerEncounter is not { } tower) return null;
        if (SourceBattleIdentityRules.Validate(frame) is { } error) return error;
        var proof = tower.Roster!;
        var frameOwner = frame.Campaign;
        if (frameOwner is null || !ReferenceEquals(frameOwner.Mechanics.Catalog, catalog) ||
            currentOwned is not null && !ReferenceEquals(currentOwned, frameOwner))
            return "source_tower_current_owned";
        if (frame.Party(BattleSide.Player).Members.Any(m => !frameOwner.Catalog.Species.ContainsKey(m.Pokemon.Species)))
            return "source_tower_player_member";
        if (SourcePokemonValidator.ValidateFrameParties(catalog, frame) is not null) return "source_tower_party_pokemon";
        return null;
    }

    internal static string? ValidateIdentity(SourceBattleFrame frame)
    {
        if (frame.TrainerTowerEncounter is not { } tower) return null;
        if (tower.Roster is not { } proof || proof.Trainer is null || proof.Party.IsDefaultOrEmpty ||
            proof.Party.Any(p => p?.Pokemon is null || p.Pokemon.Moves.IsDefault) || frame.BattleKind != WildBattleCalledBattleKind.Trainer ||
            frame.SourceMechanicsRulesVersion != SourceBattleKernel.Version || frame.WildEncounter is not null ||
            (frame.Layout == SourceBattleLayout.PresetDoubles) != proof.IsDouble ||
            (proof.IsDouble ? frame.TrainerEncounter is not null : frame.TrainerEncounter?.Roster?.Tower?.SameEncounter(proof) != true))
            return "source_tower_frame_proof";
        var party = frame.Party(BattleSide.Opponent).Members;
        if (party.Length != proof.Party.Length || party.Where((m, i) => m.IsEgg || !proof.MatchesIdentity(m.Pokemon, i)).Any())
            return "source_tower_frame_roster";
        if (frame.Rewards is not { Context: null, Pending: null, CompletedOpponentMask: 0 } rewards || !rewards.Metadata.IsEmpty)
            return "source_tower_reward_policy";
        return null;
    }
}

/// <summary>Actual preset values generated for a Tower opponent, independent of any catalog identity.</summary>
public sealed record TrainerTowerEncounterRoster(TrainerDefinition Trainer,
    ImmutableArray<TrainerPartyPokemon> Party, RngState Rng, ImmutableArray<RandomTrace> Trace,
    string Course, int FloorIndex, int TrainerIndex, bool IsDouble)
{
    [JsonIgnore] public SourceTrainerTowerBattleFlags Flags => SourceTrainerTowerBattleFlags.Trainer | SourceTrainerTowerBattleFlags.TrainerTower |
        (IsDouble ? SourceTrainerTowerBattleFlags.Double : 0);
    internal static TrainerTowerEncounterRoster Create(PokemonCatalog catalog, TrainerPartyCreationResult expected,
        string course, int floor, int trainerIndex, bool isDouble)
    {
        if (catalog.MechanicsData is null || expected.Trainer.AiFlags != 7 || expected.Trainer.DoubleBattle != isDouble ||
            expected.Trainer.Items.Any(i => i != "ITEM_NONE") || expected.Party.IsDefaultOrEmpty || expected.Trace.IsDefault ||
            floor is < 0 or > 7 || trainerIndex is < 0 or > 2)
            throw new ArgumentException("source_tower_roster_input");
        var roster = new TrainerTowerEncounterRoster(expected.Trainer, expected.Party, expected.Rng,
            expected.Trace, course, floor, trainerIndex, isDouble);
        if (expected.Party.Any(m => PokemonStateValidator.ValidateTrainerTowerInstance(catalog, m.Pokemon, roster) is not null))
            throw new ArgumentException("source_tower_roster_pokemon");
        return roster;
    }
    internal bool Matches(TrainerPartyCreationResult actual) => TrainerEncounterRoster.SameTrainer(actual.Trainer, Trainer) && actual.Rng == Rng &&
        !actual.Trace.IsDefault && actual.Trace.SequenceEqual(Trace) && !actual.Party.IsDefault && actual.Party.Length == Party.Length &&
        actual.Party.Where((p, i) => p?.Pokemon is null || p.HeldItem != Party[i].HeldItem || p.Pokemon.Moves.IsDefault ||
            p.Pokemon with { Moves = Party[i].Pokemon.Moves } != Party[i].Pokemon || !p.Pokemon.Moves.SequenceEqual(Party[i].Pokemon.Moves)).Any() == false;
    internal bool MatchesIdentity(PokemonInstance? pokemon) => pokemon is not null && !Party.IsDefault && Enumerable.Range(0, Party.Length).Any(i => MatchesIdentity(pokemon, i));
    internal bool MatchesIdentity(PokemonInstance? pokemon, int index) => pokemon is not null && !Party.IsDefault && index >= 0 && index < Party.Length &&
        pokemon with { CurrentHp = Party[index].Pokemon.CurrentHp, Moves = Party[index].Pokemon.Moves,
            Friendship = Party[index].Pokemon.Friendship } == Party[index].Pokemon;
    internal bool SameEncounter(TrainerTowerEncounterRoster other) => Course == other.Course && FloorIndex == other.FloorIndex &&
        TrainerIndex == other.TrainerIndex && IsDouble == other.IsDouble && TrainerEncounterRoster.SameTrainer(Trainer, other.Trainer) &&
        !Party.IsDefault && !other.Party.IsDefault && Party.Length == other.Party.Length &&
        other.Party.Where((p, i) => !MatchesIdentity(p.Pokemon, i) || p.HeldItem != Party[i].HeldItem).Any() == false;
}
