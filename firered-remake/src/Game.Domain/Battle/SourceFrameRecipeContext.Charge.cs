using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
internal sealed partial class FrameRecipeContext
{
    internal override bool ChargeLocked(BattlerSlot actor)=>Frame.Get(actor).Registers.Turn?.Lock?.Kind==WildBattleCalledLockKind.Charge;
    internal override WildBattleSemiInvulnerable SemiInvulnerable(BattlerSlot target)=>SourceChargeRules.SemiInvulnerable(
        Frame.Get(target).Registers.Turn?.Lock is {Kind:WildBattleCalledLockKind.Charge} charge?charge.Move:null);
    internal override void ChangeCharge(BattlerSlot actor,PokemonMoveDefinition move,bool locked,string reason)
    {
        var cell=Frame.Get(actor);var turn=cell.Registers.Turn??new(WildBattleRules.CalledMoveKernelRulesVersion,null,0,null);
        var previous=turn.Lock;
        var retained=Frame.Queue.FirstOrDefault(a=>a.Token?.Owner==action.Actor)?.Target??action.Target;
        var next=locked?new SourceTurnLock(WildBattleCalledLockKind.Charge,move.Id,invocation.PpSlot,
            Get(actor).Pokemon.Moves[invocation.PpSlot].Move,retained,1,0,null,false):null;
        if(previous==next)return;
        Frame=Frame with{Slots=Frame.Slots.SetItem(actor.Value,cell with{Registers=cell.Registers with{
            Turn=next is null&&turn.SureHitTurnsRemaining==0?null:turn with{Lock=next}}})};
        Add(n=>new SourceMtMoonVolatileEvent(n,Turn,actor,"called-charge",previous?.TurnsRemaining??0,next?.TurnsRemaining??0,reason));
    }
    internal override void ChargePp(BattlerSlot actor,BattlerSlot target,PokemonMoveDefinition move,bool attackString)=>SpendFramePp(actor,target,move,attackString);
    internal override void ChargeNoPpDeduct()=>invocation=invocation with{Flags=invocation.Flags|WildBattleInvocationFlags.NoPpDeduct};
    internal override void CancellerParalysisBlocked(BattlerSlot actor)
    {
        if(RampageLocked(actor))ChangeRechargeRampageLock(actor,null,"paralysis");
        if(ChargeLocked(actor))ChangeCharge(actor,move,false,"paralysis");
    }
}
