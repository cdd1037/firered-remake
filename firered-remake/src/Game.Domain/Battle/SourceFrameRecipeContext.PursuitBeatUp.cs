using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override void PartyHitCriticalMessage(BattlerSlot actor, BattlerSlot target, bool critical, int damage)
    {
        if (critical && damage == 0 && HeldDamageEffectiveZero(target))
            Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
    }
    internal override ImmutableArray<WildBattleCalledPartyMember> BeatUpParty(BattleSide side) => Frame.Party(side).Members;
    internal override void BeatUpBeginHit(BattlerSlot target) => BeginScriptTarget(target);
    internal void RunPursuitIntercept()
    {
        BeginScriptTarget(action.Target);
        ExecutePursuitIntercept(catalog, action.Actor.Slot, action.Target, action.ChosenSlot, move,
            $"source-battle.slot-{action.Actor.Slot.Value}");
        FinishAction();
    }
    internal override void PursuitRecordDamage(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, int lost)
    {
        if (lost == 0) return;
        var cell = Frame.Get(target);
        Frame = Frame with { Slots = Frame.Slots.SetItem(target.Value, cell with { Registers = cell.Registers with {
            Damage = SourceMoveHistoryReducer.RecordDamage(cell.Registers.Damage, Turn, actor, lost, move.Type) } }) };
    }
    internal override void PursuitMoveEnd(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition move, string purpose, int damage, WildBattleCombatant previousActor)
    {
        SourceGenericContact(catalog, actor, target, move, purpose, damage);
        var immunity = SourceFrameImmunityRules.Apply(Frame); Frame = immunity.State; Events.AddRange(immunity.Events);
        // The interrupt did not run obedience; retain the original flag rather than inventing it.
        Set(actor, SourceBattleHeldItems.FinishChoice(Get(actor), move.Id, false, false) with {
            LastUsedMove = Get(actor).Pokemon.CurrentHp > 0 ? previousActor.LastUsedMove : null });
    }
}
