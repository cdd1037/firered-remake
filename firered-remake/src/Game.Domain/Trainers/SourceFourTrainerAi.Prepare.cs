using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

public sealed record SourceTrainerPreparedAction(BattlerSlot Actor, SourceActionSelection Selection,
    SourceTrainerItemChoice? ItemChoice = null, SourceTrainerEnemyChoice? MoveChoice = null);
public sealed record SourceTrainerActionPreparationResult(SourceBattleFrame State,
    SourceTrainerItemInventory Inventory, ImmutableArray<SourceTrainerPreparedAction> Actions,
    ImmutableArray<SourceBattleEvent> Events, ImmutableArray<RandomTrace> Trace, string? Fault)
{
    public bool Succeeded => Fault is null;
    public MoveId SourceCurrentMove { get; init; }
    public ImmutableArray<SourceActionSelection> Selections => Actions.Select(a => a.Selection).ToImmutableArray();
}

public static partial class SourceFourTrainerAi
{
    /// <summary>Prepare actual enemy-left then enemy-right controller responses. Each actor resolves
    /// source switch, shared trainer inventory, then move choice before the next actor starts.
    /// Deferred item receipts execute in the existing four-action owner; switches/moves keep its queue.</summary>
    public static SourceTrainerActionPreparationResult PrepareEnemyActions(SourceBattleItemCatalog items,
        PokemonCatalog catalog, TrainerDefinition trainer, SourceBattleFrame frame, long expectedRevision,
        SourceTrainerItemInventory inventory, MoveId staleCurrentMove,
        string? knownPlayerAbility = null, string? knownPlayerHoldEffect = null)
    {
        SourceTrainerActionPreparationResult Fail(string code) => new(frame, inventory!, [], [], [], code) { SourceCurrentMove = staleCurrentMove };
        if (ValidateMetadata(trainer, frame) is { } metadata) return Fail(metadata);
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } capability) return Fail(capability);
        if (items is null || inventory is null) return Fail("source_four_trainer_preparation_inventory");
        if (inventory.Validate(items, trainer) is { } inventoryError) return Fail(inventoryError);
        if (frame.Revision != expectedRevision || frame.Outcome != WildBattleOutcome.Ongoing ||
            frame.FourTurn is not null || !frame.Queue.IsEmpty || !frame.PendingSwitches.IsEmpty ||
            !frame.Reservations.IsEmpty || frame.DeferredKoVacancyMask != 0 ||
            (frame.PreparedMedicines ?? []).Any(p => p.Actor.Slot.Side == BattleSide.Opponent))
            return Fail("source_four_trainer_preparation_phase");
        var proof = SourceMechanicsCatalog.FromDefinitions(catalog);
        try
        {
            var current = frame; var remaining = inventory; var currentMove = staleCurrentMove;
            var actions = ImmutableArray.CreateBuilder<SourceTrainerPreparedAction>();
            var trace = ImmutableArray.CreateBuilder<RandomTrace>();
            var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
            var switches = ImmutableArray.CreateBuilder<SourceActionSelection>();
            foreach (var actor in frame.Slots.Take(frame.BattlerCount).Where(c => c.Slot.Side == BattleSide.Opponent).Select(c => c.Slot))
            {
                if (!SourceFourMedicineRules.CanSelect(current, actor)) continue;
                var cell = current.Get(actor);
                if (cell.Registers.Turn?.Lock is null &&
                    (frame.Layout != SourceBattleLayout.Singles || WildBattleRules.ForcedMoveSlot(cell.Body!) is null))
                {
                    var switching = DecideSwitch(catalog, current, actor, currentMove, switches.ToImmutable());
                    if (!switching.Succeeded) return Fail(switching.Fault!);
                    current = current with { Rng = switching.Rng }; trace.AddRange(switching.Trace);
                    if (switching.PartyIndex is { } index)
                    {
                        var incoming = new PartyMemberKey(BattleSide.Opponent, index);
                        if (current.Slots.Any(c => c.Member == incoming) || switches.Any(s => s.Incoming == incoming))
                            return Fail("source_four_trainer_selected_reserve_illegal");
                        var selection = new SourceActionSelection(cell.Occupant!.Value, SourceActionKind.Switch, Incoming: incoming);
                        switches.Add(selection); actions.Add(new(actor, selection));
                        current = current with { Revision = checked(current.Revision + 1) };
                        continue;
                    }
                    var item = SourceTrainerItemPolicy.Decide(items, trainer, current, actor, remaining);
                    if (!item.Succeeded) return Fail(item.Fault!);
                    if (item.Choice is { } choice)
                    {
                        var prepared = SourceFourMedicineRules.PrepareTrainerItem(items, proof, current,
                            current.Revision, cell.Occupant!.Value, choice.Item);
                        if (!prepared.Battle.Succeeded || prepared.Selection is null)
                            return Fail(prepared.Battle.Fault ?? "source_four_trainer_item_preparation");
                        current = prepared.Battle.State; trace.AddRange(prepared.Battle.Trace); events.AddRange(prepared.Battle.Events);
                        remaining = remaining with { ConsumedMask = remaining.ConsumedMask | (1u << choice.InventorySlot) };
                        actions.Add(new(actor, prepared.Selection, choice));
                        continue;
                    }
                }
                // Locks bypass CHOOSEACTION entirely. Encore reaches this point only after
                // switch/item choice and keeps its zero-scoring-RNG forced move behavior.
                var move = ChooseMove(catalog, trainer, current, current.Revision, actor,
                    knownPlayerAbility, knownPlayerHoldEffect, currentMove);
                if (!move.Succeeded) return Fail(move.Fault!);
                current = move.State; trace.AddRange(move.Trace); currentMove = move.SourceCurrentMove ?? currentMove;
                actions.Add(new(actor, move.Choices[0].Selection, MoveChoice: move.Choices[0]));
            }
            if (actions.Count == 0) return Fail("source_four_trainer_preparation_no_actor");
            if (SourceFourActionTurns.ValidateCapability(catalog, current) is { } invalid) return Fail(invalid);
            return new(current, remaining, actions.ToImmutable(), events.ToImmutable(), trace.ToImmutable(), null) { SourceCurrentMove = currentMove };
        }
        catch (OverflowException) { return Fail("source_four_trainer_preparation_counter_exhausted"); }
    }
}
