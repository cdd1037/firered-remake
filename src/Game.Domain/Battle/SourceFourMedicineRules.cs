using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public sealed record SourceFourMedicineResult(SourceFourActionResult Battle, InventoryState Inventory,
    ImmutableArray<PartyPokemon> Party, SourceActionSelection? Selection)
{
    public bool Succeeded => Battle.Succeeded;
    public string? Fault => Battle.Fault;
}

/// <summary>FR party-menu medicine timing: apply and pay during selection, then execute a
/// no-effect item marker ahead of moves. No battle-only duplicate healing/inventory engine.</summary>
public static partial class SourceFourMedicineRules
{
    internal static string? Validate(SourceBattleFrame frame)
    {
        if (frame.PreparedMedicines is not { } prepared) return null;
        if (frame.SourceMechanicsRulesVersion != SourceBattleKernel.Version || prepared.IsDefaultOrEmpty || prepared.Length > frame.BattlerCount ||
            prepared.Any(p => p is null) ||
            prepared.Select(p => p.Actor.Slot).Distinct().Count() != prepared.Length) return "source_medicine_receipt_shape";
        foreach (var p in prepared)
        {
            if (p is null || !frame.IsCurrent(p.Actor) ||
                p.Target.Side != p.Actor.Slot.Side || p.Target.Index >= frame.Party(p.Target.Side).Members.Length ||
                string.IsNullOrWhiteSpace(p.Item.Value)) return "source_medicine_receipt_identity";
            if ((p.SourceAbsentMaskBeforeSelection & ~15) != 0 || (p.SourceAbsentMaskBeforeSelection & p.Actor.Slot.Mask) != 0)
                return "source_medicine_selection_absent_mask";
            if (p.DeferredEffect is { } deferred && (p.Actor.Slot.Side != BattleSide.Opponent || p.Target != p.Actor.Member ||
                p.Item != deferred.Id || !SourceBattleItemCatalog.SupportsFixedEffect(deferred))) return "source_medicine_deferred_definition";
            if (p.Actor.Slot.Side == BattleSide.Opponent && p.DeferredEffect is null) return "source_medicine_deferred_required";
            if (p.EscapeOnUse && (p.Actor.Slot.Side != BattleSide.Player || p.Target != p.Actor.Member || p.DeferredEffect is not null ||
                p.Item.Value is not ("pret:ITEM_POKE_DOLL" or "pret:ITEM_FLUFFY_TAIL") ||
                frame.WildEncounter is not { ScriptedSnorlax: false, TowerBattleKind: null or PokemonTowerBattleKind.OrdinaryWild })) return "source_medicine_escape_receipt";
            if (!frame.Queue.IsEmpty && !frame.Queue.Skip(frame.NextAction).Any(a =>
                a.Kind == SourceActionKind.Item && a.Token?.Owner == p.Actor && a.Item == p.Item))
                return "source_medicine_receipt_queue";
        }
        return null;
    }

    internal static bool IsPrepared(SourceBattleFrame frame, SourceQueuedAction action) =>
        action.Token is { } token && (frame.PreparedMedicines ?? []).Any(p => p.Actor == token.Owner && p.Item == action.Item);

    internal static SourceFourActionResult Execute(SourceBattleFrame frame, SourceQueuedAction action)
    {
        if (!IsPrepared(frame, action)) throw new ArgumentException("source_medicine_not_prepared");
        var receipt = frame.PreparedMedicines!.Value.Single(p => p.Actor == action.Token!.Value.Owner && p.Item == action.Item);
        var original = frame;
        var appliedEvents = ImmutableArray<SourceBattleEvent>.Empty;
        if (receipt.DeferredEffect is { } deferred)
        {
            var applied = SourceBattleItemEffects.ApplyDefinition(frame, receipt.Actor, receipt.Target, deferred);
            if (!applied.Succeeded && applied.Fault != "source_item_no_effect") return new(frame, [], [], applied.Fault);
            if (applied.Succeeded)
            {
                var published = SourceBattleItemEffects.PublishChanges(frame, applied.State, receipt.Actor, receipt.Target, receipt.Item, 0);
                frame = published.State; appliedEvents = published.Events;
            }
        }
        else if (receipt.Item.Value == "pret:ITEM_POKE_FLUTE")
        {
            var flute = SourceBattleItemEffects.ApplyPokeFlute(frame); frame = flute.State; appliedEvents = flute.Events;
        }
        else if (receipt.EscapeOnUse)
        {
            frame = SourceBattleOutcomeRules.SetOutcome(frame, WildBattleOutcome.Escaped);
            var ended = SourceBattleEventHooks.Publish(frame, (n, t) => new SourceEndedEvent(n, t, WildBattleOutcome.Escaped));
            frame = ended.State; appliedEvents = [ended.Event];
        }
        var cell = frame.Get(action.Slot);
        var body = cell.Body!;
        var volatileState = body.CalledVolatile is { } old ? old with { Grudge = false } : null;
        body = body with { FuryCutterCounter = 0, DestinyBond = false,
            CalledVolatile = volatileState == new WildBattleCalledVolatileState() ? null : volatileState };
        var remaining = frame.PreparedMedicines!.Value.Where(p => p.Actor != action.Token!.Value.Owner).ToImmutableArray();
        // BattleScript_PlayerUseItem only runs moveendcase 15 (Mirror Move) and end;
        // no new attack string/PP/history/RNG and no second healing or inventory mutation.
        return new(frame with { Revision = checked(original.Revision + 1),
            Slots = frame.Slots.SetItem(action.Slot.Value, cell with { Body = body }),
            PreparedMedicines = remaining.IsEmpty ? null : remaining }, appliedEvents, [], null);
    }
}
