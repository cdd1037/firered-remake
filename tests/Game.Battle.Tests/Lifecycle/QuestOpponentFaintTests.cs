using System.Reflection;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using FireRed.Tests.Battle.FieldEffects;

namespace FireRed.Tests.Battle.Lifecycle;

[TestClass]
public sealed class QuestOpponentFaintTests:FieldEffectFixture
{
    [TestMethod]
    public void DirectKoCapturesActualRightAttackerAndOriginalOpponentSpecies()
    {
        var frame=Frame();frame=Rebind(frame,2,Mon("RATICATE","TACKLE"));
        var foe=frame.Get(new(1)).Body!;frame=Rebind(frame,1,foe with{Pokemon=foe.Pokemon with{CurrentHp=1}});
        var result=SourceBattleKernel.ResolveAction(catalog,frame,new(frame.Get(new(2)).Occupant!.Value,frame.Revision,0,new(1)));
        Assert.IsTrue(result.Succeeded,result.Fault);
        Assert.AreEqual(foe.Pokemon.SourceSpeciesId,result.State.LastOpponentFaint!.Species);
        Assert.AreEqual(new BattlerSlot(2),result.State.LastOpponentFaint.ScriptAttacker);
    }

    [TestMethod]
    public void SelfDestructKeepsOpponentScriptAttackerRatherThanInventingPlayerCredit()
    {
        var frame=Rebind(Frame(),1,Mon("KOFFING","SELF_DESTRUCT"));
        var result=SourceBattleKernel.ResolveAction(catalog,frame,new(frame.Get(new(1)).Occupant!.Value,frame.Revision,0,new(0)));
        Assert.IsTrue(result.Succeeded,result.Fault);
        Assert.AreEqual(new BattlerSlot(1),result.State.LastOpponentFaint!.ScriptAttacker);
        Assert.AreEqual(0,result.State.Get(new(1)).Body!.Pokemon.CurrentHp);
    }

    [TestMethod]
    public void ResidualOpponentFaintUsesAfflictedOpponentAndOverwritesPriorReceipt()
    {
        var frame=Actions(Frame()).Result.State;var foe=frame.Get(new(1)).Body!;
        frame=Rebind(frame,1,foe with{Status=WildBattleStatus.Poison,Pokemon=foe.Pokemon with{CurrentHp=1}}) with{
            LastOpponentFaint=new(1,new(2))};
        var (type,context)=Residual(frame);
        type.GetMethod("StatusDamage",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(context,[new BattlerSlot(1),WildBattleStatus.Poison]);
        var after=Result(type,context);
        Assert.AreEqual(foe.Pokemon.SourceSpeciesId,after.LastOpponentFaint!.Species);
        Assert.AreEqual(new BattlerSlot(1),after.LastOpponentFaint.ScriptAttacker);
        Assert.AreEqual(frame.Rng,after.Rng);
    }

    [TestMethod]
    public void LeechSeedLiquidOozeReceiverUsesTheDonorScriptAttacker()
    {
        var frame=Actions(Frame()).Result.State;var donor=Mon("TENTACOOL","WRAP");
        donor=donor with{CalledOverlay=new(){Ability="ABILITY_LIQUID_OOZE"}};
        frame=Rebind(frame,0,donor);var receiver=frame.Get(new(1)).Body!;
        frame=Rebind(frame,1,receiver with{Pokemon=receiver.Pokemon with{CurrentHp=1}});
        var slot=frame.Get(new(0));frame=frame with{
            Slots=frame.Slots.SetItem(0,slot with{Registers=slot.Registers with{LeechSeedSource=new(1)}})};
        var (type,context)=Residual(frame);
        type.GetMethod("LeechSeedResidual",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(context,[new BattlerSlot(0)]);
        var after=Result(type,context);
        Assert.AreEqual(receiver.Pokemon.SourceSpeciesId,after.LastOpponentFaint!.Species);
        Assert.AreEqual(new BattlerSlot(0),after.LastOpponentFaint.ScriptAttacker);
    }

    [TestMethod]
    public void UnknownReplacementHazardClearsStaleAttackerWhileRetainingSpecies()
    {
        var frame=Actions(Frame()).Result.State;var foe=frame.Get(new(3)).Body!;
        frame=Rebind(frame,3,foe with{Pokemon=foe.Pokemon with{CurrentHp=0}}) with{
            LastOpponentFaint=new(1,new(0))};
        var (type,context)=Residual(frame);
        type.GetMethod("FaintAs",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(context,[new BattlerSlot(3),null]);
        var after=Result(type,context);
        Assert.AreEqual(foe.Pokemon.SourceSpeciesId,after.LastOpponentFaint!.Species);
        Assert.IsNull(after.LastOpponentFaint.ScriptAttacker);
    }

    private (Type,object) Residual(SourceBattleFrame frame)
    {
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame),"fixture enters the actual completed-action boundary");
        var type=typeof(SourceFourActionTurns).Assembly.GetType("Game.Domain.Battle.FrameEndTurnContext")!;
        return(type,Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[frame,catalog],null)!);
    }
    private static SourceBattleFrame Result(Type type,object context)=>(SourceBattleFrame)type.GetProperty("Frame",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(context)!;
}
