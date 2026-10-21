using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Encounter definition and generated roster, stored explicitly with the live battle.</summary>
public sealed record SourceTrainerEncounterContext(
    TrainerEncounterRoster Roster,
    ForestTrainerBattleStyle BattleStyle,
    SourceTrainerItemInventory Inventory,
    MoveId StaleCurrentMove,
    string? KnownPlayerAbility = null,
    string? KnownPlayerHoldEffect = null,
    bool PlayerFaintedThisTurn = false);

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
        if (catalog is null || created?.Roster is not { } proof || !Enum.IsDefined(style) ||
            !proof.Matches(created)) return new(null, "source_trainer_creation_receipt");
        if (catalog?.MechanicsData is null || created.Trainer.DoubleBattle ||
            created.Trainer.AiFlags is not (1 or 7) || created.Party.Any(p =>
                SourcePokemonValidator.ValidateForBattle(catalog, p.Pokemon, created.TowerRoster) is not null))
            return new(null, "source_trainer_creation_catalog");
        return new(new(proof, style, SourceTrainerItemInventory.Create(created.Trainer),
            SourceTrainerEncounterPolicy.NoMove), null);
    }

    internal static bool MatchesTrainer(SourceBattleFrame frame, TrainerDefinition trainer) =>
        frame.TrainerEncounter?.Roster is { } proof && TrainerEncounterRoster.SameTrainer(proof.Trainer, trainer);

    public static string? Validate(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        if (catalog is null || frame is null) return "source_trainer_encounter_missing_input";
        if (SourceBattleIdentityRules.Validate(frame) is { } shape) return shape;
        if (frame.TrainerEncounter is not { Roster: { } proof } encounter)
            return "source_trainer_encounter_roster_missing";
        if (proof.Trainer is null || proof.Party.IsDefaultOrEmpty ||
            proof.Party.Any(p => p?.Pokemon is null || p.Pokemon.Moves.IsDefault))
            return "source_trainer_encounter_roster";
        if (frame.Layout != SourceBattleLayout.Singles || frame.BattleKind != WildBattleCalledBattleKind.Trainer ||
            frame.KoReplacementTiming != KoReplacementTiming.SourceImmediate || proof.Trainer.DoubleBattle ||
            !Enum.IsDefined(encounter.BattleStyle) || encounter.Inventory is null ||
            encounter.Inventory.Trainer != proof.Trainer.Id || encounter.StaleCurrentMove.Value is null)
            return "source_trainer_encounter_context";
        if (encounter.StaleCurrentMove != SourceTrainerEncounterPolicy.NoMove && encounter.StaleCurrentMove != WildBattleRules.StruggleMove &&
            (!catalog.Moves.TryGetValue(encounter.StaleCurrentMove, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
            return "source_trainer_encounter_stale_move";
        if (encounter.KnownPlayerAbility is { } ability &&
            catalog?.MechanicsData?.Abilities.ContainsKey(ability) != true)
            return "source_trainer_encounter_ability_history";
        if (encounter.KnownPlayerHoldEffect is { } effect && !SourceTrainerAiScript.SupportsKnownHoldEffect(effect))
            return "source_trainer_encounter_hold_effect_history";
        var inventory = encounter.Inventory;
        var expected = SourceTrainerItemInventory.Create(proof.Trainer);
        if (inventory.ItemsNo != expected.ItemsNo || inventory.InitialItems.IsDefault ||
            !inventory.InitialItems.SequenceEqual(expected.InitialItems) ||
            (inventory.ConsumedMask & ~((1u << inventory.ItemsNo) - 1)) != 0)
            return "source_trainer_encounter_inventory";
        if ((proof.Tower is null) != (frame.TrainerTowerEncounter?.Roster is null) ||
            proof.Tower is { } tower && !tower.SameEncounter(frame.TrainerTowerEncounter!.Roster)) return "source_trainer_tower_receipt";
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
        var result = SourceFourTrainerAi.PrepareEnemyActions(items, catalog, context!.Roster!.Trainer,
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

/// <summary>The immutable generated trainer party at entry; mutable battle state remains in frame.Parties.</summary>
public sealed record TrainerEncounterRoster(TrainerDefinition Trainer,
    ImmutableArray<TrainerPartyPokemon> Party, Game.Domain.Randomness.RngState Rng,
    ImmutableArray<Game.Domain.Randomness.RandomTrace> Trace, TrainerTowerEncounterRoster? Tower = null)
{
    public static bool SameTrainer(TrainerDefinition? a, TrainerDefinition? b)
    {
        if (a is null || b is null || a.EncodedTrainerName.IsDefault || b.EncodedTrainerName.IsDefault ||
            a.Items.IsDefault || b.Items.IsDefault || a.AiFlagSymbols.IsDefault || b.AiFlagSymbols.IsDefault ||
            a.Party.IsDefault || b.Party.IsDefault || a.Party.Length != b.Party.Length) return false;
        return a with { EncodedTrainerName = b.EncodedTrainerName, Items = b.Items,
                AiFlagSymbols = b.AiFlagSymbols, Party = b.Party } == b &&
            a.EncodedTrainerName.SequenceEqual(b.EncodedTrainerName) && a.Items.SequenceEqual(b.Items) &&
            a.AiFlagSymbols.SequenceEqual(b.AiFlagSymbols) && a.Party.Zip(b.Party).All(pair =>
                pair.First is { } x && pair.Second is { } y && !x.EncodedSpeciesName.IsDefault &&
                !y.EncodedSpeciesName.IsDefault && !x.CustomMoves.IsDefault && !y.CustomMoves.IsDefault &&
                x with { EncodedSpeciesName = y.EncodedSpeciesName, CustomMoves = y.CustomMoves } == y &&
                x.EncodedSpeciesName.SequenceEqual(y.EncodedSpeciesName) && x.CustomMoves.SequenceEqual(y.CustomMoves));
    }

    internal static TrainerEncounterRoster CreateTower(TrainerPartyCreationResult expected, TrainerTowerEncounterRoster tower) =>
        new(expected.Trainer, expected.Party, expected.Rng, expected.Trace, tower);

    internal static TrainerEncounterRoster Create(PokemonCatalog catalog, TrainerPartyCreationResult created) =>
        new(created.Trainer, created.Party, created.Rng, created.Trace);
    internal bool Matches(TrainerPartyCreationResult actual) =>
        SameTrainer(actual.Trainer, Trainer) && actual.Rng == Rng && !actual.Trace.IsDefault && actual.Trace.SequenceEqual(Trace) &&
        !actual.Party.IsDefault && actual.Party.Length == Party.Length && actual.Party.Where((p, i) =>
            p?.Pokemon is null || p.Pokemon.Moves.IsDefault || p.HeldItem != Party[i].HeldItem ||
            p.Pokemon with { Moves = Party[i].Pokemon.Moves } != Party[i].Pokemon ||
            !p.Pokemon.Moves.SequenceEqual(Party[i].Pokemon.Moves)).Any() == false;
    internal bool MatchesIdentity(PokemonInstance actual, int index) =>
        index >= 0 && index < Party.Length && actual with { CurrentHp = Party[index].Pokemon.CurrentHp,
            Moves = Party[index].Pokemon.Moves, Friendship = Party[index].Pokemon.Friendship } == Party[index].Pokemon;
}
