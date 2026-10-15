using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Encounter metadata only. The canonical frame owns all live parties, RNG and cursors.
/// The deterministic proof key survives same-process JSON continuations. Active battle saves are
/// unsupported; an unavailable factory proof fails explicitly instead of trusting serialized input.</summary>
public sealed record SourceTrainerEncounterContext
{
    public string RosterProof { get; init; }
    public ForestTrainerBattleStyle BattleStyle { get; init; }
    public SourceTrainerItemInventory Inventory { get; init; }
    public MoveId StaleCurrentMove { get; init; }
    public string? KnownPlayerAbility { get; init; }
    public string? KnownPlayerHoldEffect { get; init; }
    public bool PlayerFaintedThisTurn { get; init; }
    private readonly SourceTrainerPartyReceipt? receipt;
    [JsonIgnore] internal SourceTrainerPartyReceipt? Receipt => receipt?.Key == RosterProof ? receipt : null;

    [JsonConstructor]
    public SourceTrainerEncounterContext(string rosterProof, ForestTrainerBattleStyle battleStyle,
        SourceTrainerItemInventory inventory, MoveId staleCurrentMove, string? knownPlayerAbility = null,
        string? knownPlayerHoldEffect = null, bool playerFaintedThisTurn = false)
    {
        RosterProof = rosterProof; BattleStyle = battleStyle; Inventory = inventory;
        StaleCurrentMove = staleCurrentMove; KnownPlayerAbility = knownPlayerAbility;
        KnownPlayerHoldEffect = knownPlayerHoldEffect; PlayerFaintedThisTurn = playerFaintedThisTurn;
        receipt = SourceTrainerPartyReceipt.Resolve(rosterProof);
    }
}

public sealed record SourceTrainerContextResult(SourceTrainerEncounterContext? Context, string? Fault)
{
    public bool Succeeded => Fault is null;
}

/// <summary>Pure encounter/controller policy, with no turn executor or conversion bridge.
/// Regional factories authenticate content before their common generator mints the private receipt.</summary>
public static class SourceTrainerEncounterPolicy
{
    public static MoveId NoMove { get; } = new("pret:MOVE_NONE");
    public static SourceTrainerContextResult CreateContext(PokemonCatalog catalog,
        TrainerPartyCreationResult created, ForestTrainerBattleStyle style = ForestTrainerBattleStyle.Shift)
    {
        if (catalog is null || created?.Receipt is not { } proof || !Enum.IsDefined(style) ||
            !proof.Matches(created)) return new(null, "source_trainer_creation_receipt");
        if (SourceMechanicsCatalog.For(catalog) is null || created.Trainer.DoubleBattle ||
            created.Trainer.AiFlags is not (1 or 7) || created.Party.Any(p =>
                SourcePokemonValidator.ValidateForBattle(catalog, p.Pokemon, created.TowerReceipt) is not null))
            return new(null, "source_trainer_creation_catalog");
        SourceTrainerPartyReceipt.Register(proof);
        return new(new(proof.Key, style, SourceTrainerItemInventory.Create(created.Trainer),
            SourceTrainerEncounterPolicy.NoMove), null);
    }

    internal static bool MatchesTrainer(SourceBattleFrame frame, TrainerDefinition trainer) =>
        frame.TrainerEncounter?.Receipt is { } proof && proof.Trainer == trainer;

    public static string? Validate(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        if (catalog is null || frame is null) return "source_trainer_encounter_missing_input";
        if (SourceBattleIdentityRules.Validate(frame) is { } shape) return shape;
        if (frame.TrainerEncounter is not { Receipt: { } proof } encounter)
            return "source_trainer_encounter_proof_unavailable";
        if (frame.Layout != SourceBattleLayout.Singles || frame.BattleKind != WildBattleCalledBattleKind.Trainer ||
            frame.KoReplacementTiming != KoReplacementTiming.SourceImmediate || proof.Trainer.DoubleBattle ||
            !Enum.IsDefined(encounter.BattleStyle) || encounter.Inventory is null ||
            encounter.Inventory.Trainer != proof.Trainer.Id || encounter.StaleCurrentMove.Value is null)
            return "source_trainer_encounter_context";
        if (encounter.StaleCurrentMove != SourceTrainerEncounterPolicy.NoMove && encounter.StaleCurrentMove != WildBattleRules.StruggleMove &&
            (!catalog.Moves.TryGetValue(encounter.StaleCurrentMove, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
            return "source_trainer_encounter_stale_move";
        if (encounter.KnownPlayerAbility is { } ability &&
            SourceMechanicsCatalog.For(catalog)?.Abilities.ContainsKey(ability) != true)
            return "source_trainer_encounter_ability_history";
        if (encounter.KnownPlayerHoldEffect is { } effect && !SourceTrainerAiScript.SupportsKnownHoldEffect(effect))
            return "source_trainer_encounter_hold_effect_history";
        var inventory = encounter.Inventory;
        var expected = SourceTrainerItemInventory.Create(proof.Trainer);
        if (inventory.ItemsNo != expected.ItemsNo || inventory.InitialItems.IsDefault ||
            !inventory.InitialItems.SequenceEqual(expected.InitialItems) ||
            (inventory.ConsumedMask & ~((1u << inventory.ItemsNo) - 1)) != 0)
            return "source_trainer_encounter_inventory";
        if (proof.Tower != frame.TrainerTowerEncounter?.Receipt) return "source_trainer_tower_receipt";
        var party = frame.Party(BattleSide.Opponent).Members;
        if (party.Length != proof.Party.Length || party.Where((member, i) => member.IsEgg ||
                !proof.MatchesIdentity(member.Pokemon, i)).Any()) return "source_trainer_encounter_roster";
        return null;
    }

    public static SourceTrainerActionPreparationResult PrepareActions(SourceBattleItemCatalog items,
        PokemonCatalog catalog, SourceBattleFrame frame, long expectedRevision)
    {
        var context = frame.TrainerEncounter;
        if (Validate(catalog, frame) is { } error)
            return new(frame, context?.Inventory!, [], [], [], error);
        var result = SourceFourTrainerAi.PrepareEnemyActions(items, catalog, context!.Receipt!.Trainer,
            frame, expectedRevision, context.Inventory, context.StaleCurrentMove,
            context.KnownPlayerAbility, context.KnownPlayerHoldEffect);
        return !result.Succeeded ? result : result with { SourceCurrentMove = context.StaleCurrentMove, State = result.State with {
            TrainerEncounter = context with { Inventory = result.Inventory } } };
    }

    public static SourceTrainerSwitchResult ChooseReplacement(PokemonCatalog catalog, SourceBattleFrame frame,
        BattlerSlot slot)
    {
        if (Validate(catalog, frame) is { } error) return new(null, frame.Rng, [], [], error);
        return SourceFourTrainerAi.ChooseReplacement(catalog, frame, slot, frame.TrainerEncounter!.StaleCurrentMove);
    }

    public static bool CanOfferShift(SourceBattleFrame frame) =>
        frame.Layout == SourceBattleLayout.Singles && frame.TrainerEncounter is {
            BattleStyle: ForestTrainerBattleStyle.Shift, PlayerFaintedThisTurn: false } &&
        frame.CanAct(new(0)) && !SourceBattleIdentityRules.LegalReserveIndices(frame, BattleSide.Player).IsEmpty;

    /// <summary>Called by the lifecycle owner for its committed events; no action or RNG is executed.</summary>
    public static SourceBattleFrame Observe(SourceBattleFrame frame, ImmutableArray<SourceBattleEvent> events)
    {
        if (frame.TrainerEncounter is not { } context) return frame;
        foreach (var e in events)
            context = e switch {
                SourceFaintedEvent { Side.Side: BattleSide.Player } => context with { PlayerFaintedThisTurn = true },
                SourceCalledPartySwitchedEvent => context with { StaleCurrentMove = SourceTrainerEncounterPolicy.NoMove },
                _ => context
            };
        return frame with { TrainerEncounter = context };
    }
}

/// <summary>The receipt is internal and immutable, not a user-supplied flag. Copies made by the
/// regional custom-move factories retain it but are accepted only when every expected field matches.</summary>
internal sealed class SourceTrainerPartyReceipt
{
    private static readonly Dictionary<string, WeakReference<SourceTrainerPartyReceipt>> registry = [];
    internal SourceTrainerTowerPartyReceipt? Tower { get; }
    internal TrainerDefinition Trainer { get; }
    internal ImmutableArray<TrainerPartyPokemon> Party { get; }
    private readonly Game.Domain.Randomness.RngState rng;
    private readonly ImmutableArray<Game.Domain.Randomness.RandomTrace> trace;
    internal string Key { get; }
    internal static SourceTrainerPartyReceipt CreateTower(TrainerPartyCreationResult expected, SourceTrainerTowerPartyReceipt proof)
    {
        if (!proof.Matches(expected)) throw new ArgumentException("source_tower_trainer_receipt");
        return new(expected, proof);
    }
    private SourceTrainerPartyReceipt(TrainerPartyCreationResult expected, SourceTrainerTowerPartyReceipt? tower = null)
    {
        Tower = tower; Trainer = expected.Trainer; Party = expected.Party; rng = expected.Rng; trace = expected.Trace;
        Key = CampaignSourceFingerprint.Compute(expected);
    }
    internal static SourceTrainerPartyReceipt Create(PokemonCatalog catalog, TrainerPartyCreationResult natural)
    {
        var expected = natural;
        if ((natural.Trainer.PartyFlags & 1) != 0)
            expected = natural with { Party = natural.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
                Moves = natural.Trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                    .Select(m => new PokemonMoveSlot(m, catalog.Moves[m].Pp, catalog.Moves[m].Pp)).ToImmutableArray()
            } }).ToImmutableArray() };
        return new(expected);
    }
    internal bool Matches(TrainerPartyCreationResult actual) =>
        actual.Trainer == Trainer && actual.Rng == rng && !actual.Trace.IsDefault && actual.Trace.SequenceEqual(trace) &&
        !actual.Party.IsDefault && actual.Party.Length == Party.Length && actual.Party.Where((p, i) =>
            p?.Pokemon is null || p.Pokemon.Moves.IsDefault || p.HeldItem != Party[i].HeldItem ||
            p.Pokemon with { Moves = Party[i].Pokemon.Moves } != Party[i].Pokemon ||
            !p.Pokemon.Moves.SequenceEqual(Party[i].Pokemon.Moves)).Any() == false;
    internal bool MatchesIdentity(PokemonInstance actual, int index) =>
        actual with { CurrentHp = Party[index].Pokemon.CurrentHp, Moves = Party[index].Pokemon.Moves,
            Friendship = Party[index].Pokemon.Friendship } == Party[index].Pokemon;
    internal static void Register(SourceTrainerPartyReceipt proof)
    {
        lock (registry)
        {
            foreach (var key in registry.Where(p => !p.Value.TryGetTarget(out _)).Select(p => p.Key).ToArray()) registry.Remove(key);
            registry[proof.Key] = new(proof);
        }
    }
    internal static SourceTrainerPartyReceipt? Resolve(string key)
    {
        if (key is null) return null;
        lock (registry) return registry.TryGetValue(key, out var reference) && reference.TryGetTarget(out var proof) ? proof : null;
    }
}
