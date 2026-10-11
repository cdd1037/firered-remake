using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    // gBattleMoveDamage is action-local, but MoveValuesCleanUp does not reset it
    // between spread targets or hits. SpecialStatus receipts remain position-local.
    private int hitItemsCommandDamage;
    private bool hitItemsNoEffect;
    private BattlerSlot? hitItemsSelfTarget;
    private bool delayedHitItemsPending;
    internal void CompleteFutureHitItems(SourceBattleFrame afterFaint)
    {
        Frame = afterFaint;
        if (!delayedHitItemsPending) return;
        // The end-turn owner clears a fainted target before move-end berries/items.
        var berries = SourceFrameBerryItems.ApplyCanonical(Frame, SourceBerryPhase.MoveEnd, catalog: catalog);
        Frame = berries.State; Events.AddRange(berries.Events); Trace.AddRange(berries.Trace);
        ApplyFrameHitItems(delayed: true);
        EndSubstitutePass();
    }
    internal override void HitItemsCommandDamage(int damage, bool noEffect = false)
    {
        hitItemsCommandDamage = damage;
        hitItemsNoEffect = noEffect;
    }
    internal override void HitItemsConfusionDamage(BattlerSlot actor, int damage)
    {
        hitItemsCommandDamage = damage;
        hitItemsSelfTarget = actor;
    }
    internal override bool ContactConfusionSelfDamage => hitItemsSelfTarget is not null;
    private void ApplyFrameHitItems(bool delayed = false)
    {
        // Miss/absorption scripts may skip datahpupdate, retaining command damage
        // from the previous spread target. Their no-effect result still gates items.
        bool noEffect = commandNoEffect || hitItemsNoEffect || Events.Skip(targetEventStart).Any(e =>
            e is SourceMoveMissedEvent || e is SourceAbilityPreventedEvent {
                Ability: "ABILITY_SOUNDPROOF" or "ABILITY_WATER_ABSORB" or "ABILITY_VOLT_ABSORB" or "ABILITY_FLASH_FIRE" });
        ApplyHitItems(invocation.Actor, hitItemsSelfTarget ?? invocation.Target,
            delayed ? new MoveId("pret:MOVE_NONE") : move.Id,
            !delayed && move.Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"), hitItemsCommandDamage, noEffect);
        // CompleteScriptTarget is called once per ordinary multihit and once after
        // its loop; it already runs immunity/berries before this item phase. Do not
        // duplicate the callback in HitItemsPerHitMoveEnd. Unported callers remain gated.
    }
}
