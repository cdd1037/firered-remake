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

/// <summary>The proof is minted only by the authenticated local-course factory. Serialized keys
/// resolve only while that proof is alive; player saves never contain an active battle.</summary>
public sealed record SourceTrainerTowerEncounterContext
{
    public string RosterProof { get; init; }
    public string Course { get; init; }
    public int FloorIndex { get; init; }
    public int TrainerIndex { get; init; }
    public SourceTrainerTowerBattleFlags BattleFlags { get; init; }
    private readonly SourceTrainerTowerPartyReceipt? receipt;
    [JsonIgnore] internal SourceTrainerTowerPartyReceipt? Receipt => receipt?.Key == RosterProof ? receipt : null;
    [JsonConstructor]
    public SourceTrainerTowerEncounterContext(string rosterProof, string course, int floorIndex, int trainerIndex,
        SourceTrainerTowerBattleFlags battleFlags)
    {
        RosterProof = rosterProof; Course = course; FloorIndex = floorIndex; TrainerIndex = trainerIndex;
        BattleFlags = battleFlags; receipt = SourceTrainerTowerPartyReceipt.Resolve(rosterProof);
    }
}

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
    public static SourceTrainerEncounterEntryResult Begin(TrainerTowerOpponentPokemonCatalog opponents,
        TrainerPartyCreationResult created, ImmutableArray<WildBattleCalledPartyMember> playerParty,
        ForestTrainerBattleStyle style = ForestTrainerBattleStyle.Shift,
        bool attackBadgeBoost = false, bool defenseBadgeBoost = false, bool speedBadgeBoost = false,
        bool specialBadgeBoost = false, WildBattleTerrain terrain = WildBattleTerrain.Building,
        OwnedBattlePokemonCatalog? currentOwned = null)
    {
        var initial = created?.Rng ?? new RngState(0);
        SourceTrainerEncounterEntryResult Fail(string fault) => new(null, initial, [], [], fault);
        if (opponents is null || !ReferenceEquals(TrainerTowerOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            created?.TowerReceipt is not { } proof || !proof.Matches(created) || !ReferenceEquals(proof.Mechanics, opponents.Mechanics))
            return Fail("source_tower_creation_receipt");
        OwnedBattlePokemonCatalog current;
        try { current = OwnedBattlePokemonCatalog.ResolveCurrent(opponents.Owned.SharedBattle, currentOwned); }
        catch (ArgumentException) { return Fail("source_tower_current_owned"); }
        var catalog = proof.Mechanics.Catalog;
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
            SourceTrainerTowerPartyReceipt.Register(proof);
            SourceTrainerEncounterContext? trainer = null;
            if (!proof.IsDouble)
            {
                var withReceipt = created with { Receipt = SourceTrainerPartyReceipt.CreateTower(created, proof) };
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
                SourceCampaignRulesVersion = current.TrainerOpponentProfile,
                Layout = proof.IsDouble ? SourceBattleLayout.PresetDoubles : SourceBattleLayout.Singles,
                KoReplacementTiming = proof.IsDouble ? KoReplacementTiming.EndOfTurn : KoReplacementTiming.SourceImmediate,
                BattleKind = WildBattleCalledBattleKind.Trainer, TrainerEncounter = trainer,
                TrainerTowerEncounter = new(proof.Key, proof.Course, proof.FloorIndex, proof.TrainerIndex, proof.Flags),
                Rng = rng, SourceAbsentMask = (byte)(proof.IsDouble ? 0 : 12), TurnStartAbsentMask = (byte)(proof.IsDouble ? 0 : 12),
                Parties = [new(BattleSide.Player, playerParty), new(BattleSide.Opponent, enemy)], Slots = slots,
                Field = new() { Terrain = terrain }, Rewards = new()
            };
            var started = SourceBattleEventHooks.Publish(frame, (n, t) => new SourceTrainerStartedEvent(n, t, created.Trainer.Id, enemy.Length));
            var start = SourceBattleEventHooks.Publish(started.State, (n, t) => new SourceStartedEvent(n, t));
            var entry = SourceFourEntryAbilities.InitializeEntry(catalog, start.State, start.State.Revision);
            if (!entry.Succeeded) return Fail(entry.Fault!);
            var draw = Gen3Rng.Next(entry.State.Rng, "trainer-tower.entry.random-turn-number");
            var ready = SourceBattleEventHooks.Publish(entry.State with { Rng = draw.State, RandomTurnNumber = draw.Value },
                (n, t) => new SourceTurnReadyEvent(n, t, draw.Value));
            return new(ready.State, ready.State.Rng, [started.Event, start.Event, .. entry.Events, ready.Event], [.. trace, .. entry.Trace, draw.Trace], null);
        }
        catch (OverflowException) { return Fail("source_tower_counter_exhausted"); }
        catch (ArgumentException e) { return Fail("source_tower_invalid_entry:" + e.Message); }
    }

    public static SourceTrainerActionPreparationResult PrepareActions(SourceBattleItemCatalog items, PokemonCatalog catalog,
        SourceBattleFrame frame, long expectedRevision, OwnedBattlePokemonCatalog? currentOwned = null)
    {
        var error = Validate(catalog, frame, currentOwned);
        if (error is not null || frame.TrainerTowerEncounter?.Receipt is not { } proof)
            return new(frame, null!, [], [], [], error ?? "source_tower_encounter_required");
        if (frame.TrainerEncounter is not null) return SourceTrainerEncounterPolicy.PrepareActions(items, catalog, frame, expectedRevision);
        return SourceFourTrainerAi.PrepareEnemyActions(items, catalog, proof.Trainer, frame, expectedRevision,
            SourceTrainerItemInventory.Create(proof.Trainer), SourceTrainerEncounterPolicy.NoMove);
    }

    public static SourceTrainerTowerSettlement Settle(PokemonCatalog catalog, SourceBattleFrame frame, int money, OwnedBattlePokemonCatalog? currentOwned = null)
    {
        if (Validate(catalog, frame, currentOwned) is { } error) throw new ArgumentException(error, nameof(frame));
        if (frame.TrainerTowerEncounter is null || frame.FourTurn is { Phase: not SourceFourActionPhase.BattleEnded } ||
            frame.Outcome is not (WildBattleOutcome.Won or WildBattleOutcome.Lost or WildBattleOutcome.Draw) ||
            money is < 0 or > SourceBattleMoneyRules.MaximumMoney) throw new ArgumentException("source_tower_settlement");
        return new(frame.Outcome, money, 0, 0, 0, 0, 0, frame.Outcome == WildBattleOutcome.Won,
            frame.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw);
    }

    public static string? Validate(PokemonCatalog catalog, SourceBattleFrame frame, OwnedBattlePokemonCatalog? currentOwned = null)
    {
        if (frame is null || catalog is null) return "source_tower_missing_input";
        if (frame.TrainerTowerEncounter is not { } tower) return null;
        if (SourceBattleIdentityRules.Validate(frame) is { } error) return error;
        var proof = tower.Receipt!;
        if (!ReferenceEquals(proof.Mechanics.Catalog, catalog)) return "source_tower_catalog";
        var frameOwner = OwnedBattlePokemonCatalog.ForMechanics(catalog, frame.SourceCampaignRulesVersion);
        var predecessor = TrainerTowerOpponentPokemonCatalog.ForMechanics(catalog)?.Owned.SharedBattle;
        if (frameOwner is null || predecessor is null || currentOwned is not null && !ReferenceEquals(currentOwned, frameOwner))
            return "source_tower_current_owned";
        try { _ = OwnedBattlePokemonCatalog.ResolveCurrent(predecessor, frameOwner); }
        catch (ArgumentException) { return "source_tower_current_owned"; }
        if (frame.Party(BattleSide.Player).Members.Any(m => !frameOwner.Catalog.Species.ContainsKey(m.Pokemon.Species)))
            return "source_tower_player_member";
        if (SourcePokemonValidator.ValidateFrameParties(catalog, frame) is not null) return "source_tower_party_pokemon";
        return null;
    }

    internal static string? ValidateIdentity(SourceBattleFrame frame)
    {
        if (frame.TrainerTowerEncounter is not { } tower) return null;
        if (tower.Receipt is not { } proof || tower.Course != proof.Course || tower.FloorIndex != proof.FloorIndex ||
            tower.TrainerIndex != proof.TrainerIndex || tower.BattleFlags != proof.Flags || frame.BattleKind != WildBattleCalledBattleKind.Trainer ||
            frame.SourceMechanicsRulesVersion != SourceBattleKernel.Version || frame.WildEncounter is not null ||
            (frame.Layout == SourceBattleLayout.PresetDoubles) != proof.IsDouble ||
            (proof.IsDouble ? frame.TrainerEncounter is not null : frame.TrainerEncounter?.Receipt?.Tower != proof))
            return "source_tower_frame_proof";
        var party = frame.Party(BattleSide.Opponent).Members;
        if (party.Length != proof.Party.Length || party.Where((m, i) => m.IsEgg || !proof.MatchesIdentity(m.Pokemon, i)).Any())
            return "source_tower_frame_roster";
        if (frame.Rewards is not { Context: null, Pending: null, CompletedOpponentMask: 0 } rewards || !rewards.Metadata.IsEmpty)
            return "source_tower_reward_policy";
        return null;
    }
}

internal sealed class SourceTrainerTowerPartyReceipt
{
    private static readonly Dictionary<string, WeakReference<SourceTrainerTowerPartyReceipt>> Registry = [];
    internal SourceMechanicsCatalog Mechanics { get; }
    internal TrainerDefinition Trainer => expected.Trainer;
    internal ImmutableArray<TrainerPartyPokemon> Party => expected.Party;
    internal string Course { get; }
    internal int FloorIndex { get; }
    internal int TrainerIndex { get; }
    internal bool IsDouble { get; }
    internal string Key { get; }
    internal SourceTrainerTowerBattleFlags Flags => SourceTrainerTowerBattleFlags.Trainer | SourceTrainerTowerBattleFlags.TrainerTower |
        (IsDouble ? SourceTrainerTowerBattleFlags.Double : 0);
    private readonly TrainerPartyCreationResult expected;
    private SourceTrainerTowerPartyReceipt(SourceMechanicsCatalog mechanics, TrainerPartyCreationResult expected,
        string course, int floor, int trainerIndex, bool isDouble)
    {
        Mechanics = mechanics; this.expected = expected; Course = course; FloorIndex = floor; TrainerIndex = trainerIndex; IsDouble = isDouble;
        Key = CampaignSourceFingerprint.Compute(new { course, floor, trainerIndex, isDouble, expected });
    }
    internal static SourceTrainerTowerPartyReceipt Create(PokemonCatalog catalog, TrainerPartyCreationResult expected,
        string course, int floor, int trainerIndex, bool isDouble)
    {
        if (SourceMechanicsCatalog.For(catalog) is not { } mechanics || expected.Trainer.AiFlags != 7 || expected.Trainer.DoubleBattle != isDouble ||
            expected.Trainer.Items.Any(i => i != "ITEM_NONE") || expected.Party.IsDefaultOrEmpty || expected.Trace.IsDefault ||
            floor is < 0 or > 7 || trainerIndex is < 0 or > 2)
            throw new ArgumentException("source_tower_receipt_input");
        var receipt = new SourceTrainerTowerPartyReceipt(mechanics, expected, course, floor, trainerIndex, isDouble);
        if (expected.Party.Any(m => PokemonStateValidator.ValidateTrainerTowerInstance(catalog, m.Pokemon, receipt) is not null))
            throw new ArgumentException("source_tower_receipt_pokemon");
        return receipt;
    }
    internal bool Matches(TrainerPartyCreationResult actual) => actual.Trainer == Trainer && actual.Rng == expected.Rng &&
        !actual.Trace.IsDefault && actual.Trace.SequenceEqual(expected.Trace) && !actual.Party.IsDefault && actual.Party.Length == Party.Length &&
        actual.Party.Where((p, i) => p?.Pokemon is null || p.HeldItem != Party[i].HeldItem || p.Pokemon.Moves.IsDefault ||
            p.Pokemon with { Moves = Party[i].Pokemon.Moves } != Party[i].Pokemon || !p.Pokemon.Moves.SequenceEqual(Party[i].Pokemon.Moves)).Any() == false;
    internal bool MatchesIdentity(PokemonInstance? pokemon) => pokemon is not null && Enumerable.Range(0, Party.Length).Any(i => MatchesIdentity(pokemon, i));
    internal bool MatchesIdentity(PokemonInstance? pokemon, int index) => pokemon is not null && index >= 0 && index < Party.Length &&
        pokemon with { CurrentHp = Party[index].Pokemon.CurrentHp, Moves = Party[index].Pokemon.Moves,
            Friendship = Party[index].Pokemon.Friendship } == Party[index].Pokemon;
    internal static void Register(SourceTrainerTowerPartyReceipt proof)
    {
        lock (Registry) { foreach (var key in Registry.Where(p => !p.Value.TryGetTarget(out _)).Select(p => p.Key).ToArray()) Registry.Remove(key); Registry[proof.Key] = new(proof); }
    }
    internal static SourceTrainerTowerPartyReceipt? Resolve(string key)
    { lock (Registry) return key is not null && Registry.TryGetValue(key, out var reference) && reference.TryGetTarget(out var proof) ? proof : null; }
}
