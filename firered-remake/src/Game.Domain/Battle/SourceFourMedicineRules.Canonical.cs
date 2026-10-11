using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public sealed record SourceTrainerItemPreparation(SourceFourActionResult Battle, SourceActionSelection? Selection)
{
    public bool Succeeded => Battle.Succeeded;
    public string? Fault => Battle.Fault;
}

public static partial class SourceFourMedicineRules
{
    public static SourceFourMedicineResult Select(SourceBattleItemCatalog items, SourceMechanicsCatalog mechanics,
        SourceBattleFrame frame, long expectedRevision, OccupantToken actor, InventoryState inventory,
        ImmutableArray<PartyPokemon> party, SourceBattleItemSelection? selection, string? currentMapSection = null)
    {
        SourceFourMedicineResult Fail(string error) => new(new(frame,[],[],error),inventory,party,null);
        if (SourceFourActionTurns.ValidateCapability(mechanics.Catalog,frame) is { } invalid) return Fail(invalid);
        if (ValidateSelectionPhase(frame,expectedRevision,actor,BattleSide.Player) is { } error) return Fail(error);
        try
        {
            if (SourceBattleItemRules.Prepare(items,mechanics,frame,actor,inventory,party,selection,currentMapSection,out var preparation) is { } fault)
                return Fail(fault);
            var p = preparation!;
            var applied = p.Item.IsPokeFlute ? new SourceFourActionResult(p.Frame,[],[],null) :
                SourceBattleItemEffects.PublishChanges(frame,p.Frame,actor,p.Target,p.Item.Id,p.TargetInstanceId);
            var next = applied.State with { Revision = checked(frame.Revision+1),
                PreparedMedicines = (frame.PreparedMedicines ?? []).Add(new(actor,p.Item.Id,p.Target) { SourceAbsentMaskBeforeSelection = SelectionAbsentMask(frame) }) };
            if (SourceFourActionTurns.ValidateCapability(mechanics.Catalog,next) is { } finalError) return Fail(finalError);
            return new(new(next,applied.Events,[],null),p.Inventory,p.Party,new(actor,SourceActionKind.Item,Item:p.Item.Id));
        }
        catch (OverflowException) { return Fail("source_four_action_counter_exhausted"); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { return Fail("source_medicine_content_unsupported"); }
    }

    /// <summary>The AI inventory owner pays on selection; this source receipt defers effect-table execution.
    /// It carries no inventory, synthetic party, result snapshot or caller-provided trust flag.</summary>
    public static SourceTrainerItemPreparation PrepareTrainerItem(SourceBattleItemCatalog items, SourceMechanicsCatalog mechanics,
        SourceBattleFrame frame, long expectedRevision, OccupantToken actor, ItemId item)
    {
        SourceTrainerItemPreparation Fail(string error) => new(new(frame,[],[],error),null);
        if (SourceFourActionTurns.ValidateCapability(mechanics.Catalog,frame) is { } invalid) return Fail(invalid);
        if (ValidateSelectionPhase(frame,expectedRevision,actor,BattleSide.Opponent) is { } error) return Fail(error);
        if (items.Find(item) is not { IsFixedEffect: true } definition || !SourceBattleItemDefinitionProof.IsAuthentic(definition))
            return Fail("source_item_trainer_definition");
        // Source trainer item categories never select PP, revival, infatuation or reusable instruments.
        if (definition.Reusable || (definition.EffectBytes[4] & 0x58) != 0 || (definition.EffectBytes[0] & 0x80) != 0)
            return Fail("source_item_trainer_category");
        try
        {
            var next = frame with { Revision = checked(frame.Revision+1), PreparedMedicines = (frame.PreparedMedicines ?? []).Add(
                new(actor,item,actor.Member) { DeferredEffect = definition, SourceAbsentMaskBeforeSelection = SelectionAbsentMask(frame) }) };
            if (SourceFourActionTurns.ValidateCapability(mechanics.Catalog,next) is { } finalError) return Fail(finalError);
            return new(new(next,[],[],null),new(actor,SourceActionKind.Item,Item:item));
        }
        catch (OverflowException) { return Fail("source_four_action_counter_exhausted"); }
    }

    /// <summary>The source's pre-menu absent snapshot is distinct from live presence after a Revive.
    /// A newly present battler is targetable but never gains an action in the already selecting turn.</summary>
    public static bool CanSelect(SourceBattleFrame frame, BattlerSlot slot) => frame.CanAct(slot) &&
        (SelectionAbsentMask(frame) & slot.Mask) == 0;
    internal static byte SelectionAbsentMask(SourceBattleFrame frame) => (byte)((frame.PreparedMedicines ?? [])
        .Aggregate((int)frame.SourceAbsentMask,(mask,receipt)=>mask|receipt.SourceAbsentMaskBeforeSelection));
    internal static SourceBattleFrame ReconcileRevivedReserves(SourceBattleFrame frame)
    {
        byte vacated=frame.DeferredKoVacancyMask;
        foreach(var cell in frame.Slots)
            if((frame.TurnStartAbsentMask & cell.Slot.Mask)!=0 && !frame.IsSourceAbsent(cell.Slot) &&
                cell.Body?.Pokemon.CurrentHp==0 && !SourceBattleIdentityRules.LegalReserveIndices(frame,cell.Slot.Side).IsEmpty)
                vacated |= cell.Slot.Mask;
        return frame with { DeferredKoVacancyMask=vacated };
    }

    private static string? ValidateSelectionPhase(SourceBattleFrame frame,long revision,OccupantToken actor,BattleSide side)
    {
        if (frame.Revision != revision) return "source_medicine_stale_revision";
        if (frame.FourTurn is not null || !frame.Queue.IsEmpty || !frame.PendingSwitches.IsEmpty ||
            frame.Outcome != WildBattleOutcome.Ongoing || frame.DeferredKoVacancyMask != 0) return "source_medicine_selection_phase";
        if (!frame.IsCurrent(actor) || actor.Slot.Side != side || !CanSelect(frame,actor.Slot) ||
            frame.Get(actor.Slot).Registers.Turn?.Lock is not null) return "source_medicine_actor";
        if ((frame.PreparedMedicines ?? []).Any(p => p.Actor.Slot == actor.Slot)) return "source_medicine_already_selected";
        return null;
    }
}
