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
    public static SourceFourMedicineResult Select(EarlyShopCatalog shop, OpeningItemsCatalog opening,
        CampaignPokemonContent content, PokemonCatalog battleCatalog, SourceBattleFrame frame,
        long expectedRevision, OccupantToken actor, InventoryState inventory,
        ImmutableArray<PartyPokemon> party, BattleMedicineSelection? selection)
    {
        SourceFourMedicineResult Fail(string error) => new(new(frame, [], [], error), inventory, party, null);
        if (SourceFourActionTurns.ValidateCapability(battleCatalog, frame) is { } error) return Fail(error);
        if (frame.Revision != expectedRevision) return Fail("source_medicine_stale_revision");
        if (frame.FourTurn is not null || !frame.Queue.IsEmpty || !frame.PendingSwitches.IsEmpty ||
            frame.Outcome != WildBattleOutcome.Ongoing || frame.DeferredKoVacancyMask != 0)
            return Fail("source_medicine_selection_phase");
        if (!frame.IsCurrent(actor) || actor.Slot.Side != BattleSide.Player || !CanSelect(frame,actor.Slot) ||
            frame.Get(actor.Slot).Registers.Turn?.Lock is not null) return Fail("source_medicine_actor");
        if ((frame.PreparedMedicines ?? []).Any(p => p.Actor.Slot == actor.Slot)) return Fail("source_medicine_already_selected");
        if (selection is null) return Fail("battle_medicine_cancelled");
        var actual = frame.Party(BattleSide.Player);
        if (party.IsDefaultOrEmpty || party.Length != actual.Members.Length) return Fail("source_item_party_context");
        var identities = new HashSet<long>();
        for (int i = 0; i < party.Length; i++)
        {
            var owned = party[i]; var member = actual.Members[i];
            if (owned?.Pokemon is null || !WildBattleSourcePartyEffects.SamePokemon(owned.Pokemon, member.Pokemon) ||
                owned.Status != member.Status || owned.IsEgg != member.IsEgg ||
                owned.HeldItem != WildBattleSourcePartyEffects.PersistentHeldItem(member)) return Fail("source_item_party_projection");
            if (PokemonStateValidator.ValidateParty(content, owned) is { } invalid) return Fail(invalid);
            if (!identities.Add(owned.InstanceId)) return Fail("source_item_duplicate_identity");
        }
        int index = Enumerable.Range(0, party.Length).FirstOrDefault(i => party[i].InstanceId == selection.TargetInstanceId, -1);
        if (index < 0) return Fail("battle_medicine_target_identity");
        try
        {
            var before = party[index];
            var medicine = EarlyShopMedicineRules.Use(shop, opening, content, inventory, before, selection.Item, PotionUseContext.Battle);
            if (!medicine.Succeeded) return Fail(medicine.Error ?? "battle_medicine_no_effect");
            var after = medicine.Pokemon;
            var target = new PartyMemberKey(BattleSide.Player, index);
            var cell = frame.Slots.FirstOrDefault(c => c.Member == target);
            var next = frame with { Revision = checked(frame.Revision + 1),
                Parties = frame.Parties.SetItem(0, actual with { Members = actual.Members.SetItem(index,
                    actual.Members[index] with { Pokemon = after.Pokemon, Status = after.Status }) }),
                PreparedMedicines = (frame.PreparedMedicines ?? []).Add(new(actor, selection.Item, target)) };
            if (cell is not null)
            {
                bool cured = before.Status != after.Status;
                var active = WildBattleTransformationRules.ApplyPersistentPokemon(cell.Body!, after.Pokemon);
                active = active with { Status = cured ? after.Status : cell.Body!.Status,
                    Nightmare = cured && !WildBattleStatus.IsSleeping(after.Status) ? false : cell.Body!.Nightmare,
                    SourcePartyStatusOverride = cured ? null : cell.Body!.SourcePartyStatusOverride,
                    Transform = active.Transform is { } form ? form with {
                        PartyStatusOverride = cured ? null : cell.Body!.Transform?.PartyStatusOverride } : null };
                next = next with { Slots = next.Slots.SetItem(cell.Slot.Value, cell with { Body = active }) };
            }
            var published = SourceBattleEventHooks.Publish(next, (n, t) => new SourceMedicineAppliedEvent(n, t,
                selection.Item, before.InstanceId, index, cell is not null,
                before.Pokemon.CurrentHp, after.Pokemon.CurrentHp, before.Status, after.Status));
            if (SourceFourActionTurns.ValidateCapability(battleCatalog, published.State) is { } invalid) return Fail(invalid);
            return new(new(published.State, [published.Event], [], null), medicine.Inventory,
                party.SetItem(index, after), new(actor, SourceActionKind.Item, Item: selection.Item));
        }
        catch (OverflowException) { return Fail("source_four_action_counter_exhausted"); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { return Fail("source_medicine_content_unsupported"); }
    }

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
                p.Item != deferred.Id || !SourceBattleItemDefinitionProof.IsAuthentic(deferred))) return "source_medicine_deferred_definition";
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
