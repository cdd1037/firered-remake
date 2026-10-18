using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Called=FireRed.Tests.Application.WildBattles.CalledLeafAnimationKernelFixture;
using Caller=FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using Legal=FireRed.Tests.Application.WildBattles.ConditionalRecoveryAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

// Legal TM compatibility and actual public-kernel receipts. Controlled stages,
// terrain, HP and bounded RNG searches are integration fixtures, not route replay.
[TestClass,TestCategory("Game")]
public sealed class SecretPowerAnimationKernelTests
{
    private static MoveId Move=>Kernel.Id("SECRET_POWER");
    private static WildBattleCombatant[] Bodies(int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Legal.MachineMon("RATICATE","TM43","SECRET_POWER") with{AccuracyStage=12,SpeedStage=12};return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies,bool single,int terrain=0)
    {var frame=Kernel.Frame(bodies,single);return frame with{Field=frame.Field with{Terrain=(WildBattleTerrain)terrain}};}
    private static bool Qualifying(SourceFourActionResult result)=>result.Trace.Single(t=>t.Purpose.EndsWith(".secondary-effect",StringComparison.Ordinal)).Raw%100<=30;
    private static void Check(SourceFourActionResult result,bool single,SourceBattleEvent? owner)=>Check(result.Events,result,single,owner);
    private static void Check(ImmutableArray<SourceBattleEvent> events,object committed,bool single,SourceBattleEvent? owner)
    {
        string before=JsonSerializer.Serialize(committed);Assert.IsTrue(AnimationDispatchFixture.Supports(290,Move.Value));
        var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Move,used.Move);Assert.AreEqual(Move,attack.CurrentMove);Assert.AreEqual(Move,attack.ChosenMove);
        Assert.AreEqual(Move,pp.CurrentMove);Assert.AreEqual(Move,pp.ChosenMove);Assert.AreEqual(used.Actor,pp.PpOwner);
        Assert.AreEqual(used.Slot,pp.PpSlot);Assert.AreEqual(Math.Max(0,pp.Before-pp.CommandCost),pp.After);
        int count=0;
        for(int i=0;i<events.Length;i++)
        {
            var decision=Kernel.Project(events,i,single);Assert.AreEqual(decision,Kernel.Project(events,i,single));
            Assert.IsNull(Kernel.Project(events,i,single,SourceBattleScene.Off).Dispatch);Assert.IsNull(Called.Project(events,i,single,result:true).Dispatch);
            if(single)
            {
                var legacy=Envelope.Single(events,i) with{SourceEvents=[]};Assert.AreEqual(decision,AnimationDispatchFixture.AtAttackText(legacy,true,SourceBattleScene.On));
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(legacy,false,SourceBattleScene.On).Dispatch);
            }
            if(owner is null||events[i]!=used){Assert.IsNull(decision.Dispatch);continue;}
            var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);count++;
            Assert.AreEqual(290,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);
            Assert.AreEqual(used.Sequence,cue.Sequence);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);
            Assert.AreEqual(owner.Sequence,cue.ResultSequence);Assert.AreEqual(owner is SourceSubstituteDamagedEvent,cue.HitsSubstitute);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
        }
        Assert.AreEqual(owner is null?0:1,count);Assert.AreEqual(before,JsonSerializer.Serialize(committed));
    }

    [TestMethod]
    public void LegalTmHitKeepsSecretPowerIdentityAcrossAllTerrainsActorSlotsAndSelectedAllies()
    {
        var definition=Kernel.Mechanics.Catalog.Moves[Move];Assert.AreEqual(290,definition.SourceMoveId);Assert.AreEqual("EFFECT_SECRET_POWER",definition.Effect);
        Assert.AreEqual(70,definition.Power);Assert.AreEqual(30,definition.SecondaryEffectChance);Assert.AreEqual("MOVE_TARGET_SELECTED",definition.Target);
        foreach(int terrain in Enumerable.Range(0,10))foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2})
        {
            var result=Kernel.Ordered(Frame(Bodies(actor),single,terrain),actor,target);
            Assert.AreEqual((WildBattleTerrain)terrain,result.State.Field.Terrain);Check(result,single,Kernel.Hits(result.Events).Single());
        }
    }

    [TestMethod]
    public void FailedSecondaryRollsAndSilentStatusOrStatPreventionStillOwnThePrimaryHit()
    {
        foreach(bool single in new[]{false,true})foreach(int terrain in Enumerable.Range(0,10))
        {
            var failed=Kernel.Find(Frame(Bodies(),single,terrain),0,1,r=>!Qualifying(r),"Secret Power failed secondary terrain "+terrain);
            Assert.IsFalse(failed.Events.OfType<SourceStatusChangedEvent>().Any());Assert.IsFalse(failed.Events.OfType<SourceStatChangedEvent>().Any());
            Assert.IsFalse(failed.Events.OfType<SourceConfusionChangedEvent>().Any());Assert.IsFalse(failed.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied));
            Check(failed,single,Kernel.Hits(failed.Events).Single());
            var bodies=Bodies();bodies[1]=(terrain switch{
                0=>Kernel.Mon("SNORLAX","DEFENSE_CURL",100),1=>Kernel.Mon("VIGOROTH","SCRATCH",100),
                2=>Kernel.Mon("PIDGEY","GUST",100),3 or 4 or 5=>Kernel.Mon("TENTACOOL","ACID",100),
                6=>Kernel.Mon("LICKITUNG","LICK",100),7=>Kernel.Mon("ZUBAT","ASTONISH",100),
                _=>Kernel.Mon("PERSIAN","SCRATCH",100)}) with{SpeedStage=0};
            var blocked=Kernel.Find(Frame(bodies,single,terrain),0,1,Qualifying,"Secret Power silent secondary prevention terrain "+terrain);
            Assert.IsFalse(blocked.Events.OfType<SourceStatusChangedEvent>().Any());Assert.IsFalse(blocked.Events.OfType<SourceStatChangedEvent>().Any());
            Assert.IsFalse(blocked.Events.OfType<SourceConfusionChangedEvent>().Any());Assert.IsFalse(blocked.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied));
            Assert.IsFalse(blocked.Events.OfType<SourceAbilityPreventedEvent>().Any(),"Ordinary 30% secondary prevention is silent.");
            Check(blocked,single,Kernel.Hits(blocked.Events).Single());
        }
    }

    [TestMethod]
    public void SubstituteAndTrueZeroLossEndureKeepExactlyOneOriginalHitOwner()
    {
        foreach(bool single in new[]{false,true})foreach(int terrain in Enumerable.Range(0,10))foreach(string branch in new[]{"substitute","endure"})
        {
            var bodies=Bodies();bodies[1]=branch=="substitute"?bodies[1] with{CalledVolatile=new(){SubstituteHp=1}}:bodies[1] with{Enduring=true,Pokemon=bodies[1].Pokemon with{CurrentHp=1}};
            var result=Kernel.Ordered(Frame(bodies,single,terrain));var hit=Kernel.Hits(result.Events).Single();
            if(branch=="substitute")Assert.AreEqual(0,result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);
            else{var hp=result.Events.OfType<SourceHpChangedEvent>().Single();Assert.AreEqual(1,hp.HpAfter);Assert.AreEqual(0,hp.Damage);Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured);}
            Check(result,single,hit);
        }
    }

    [TestMethod]
    public void ProtectMissAndRealNormalTypeImmunityNeverBorrowTerrainTailSupport()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"protect","miss","immune"})
        {
            var bodies=Bodies();bodies[1]=branch=="immune"?Kernel.Mon("GASTLY","LICK",100) with{SpeedStage=0}:bodies[1] with{Protected=branch=="protect",EvasionStage=branch=="miss"?12:6};
            if(branch=="miss")bodies[0]=bodies[0] with{AccuracyStage=0};
            var frame=Frame(bodies,single,4);var result=branch=="miss"?Kernel.Find(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"Secret Power real miss"):Kernel.Ordered(frame);
            Assert.IsTrue(result.Events.Any(e=>e is SourceMoveMissedEvent or SourceMoveFailedEvent||e is SourceEffectivenessEvent{Effectiveness:WildBattleEffectiveness.NoEffect}));
            Check(result,single,null);
        }
    }

    [TestMethod]
    public void CalledSecretPowerRetainsAuthenticatedCallerPaymentAndLeafIdentity()
    {
        foreach(bool single in new[]{false,true})foreach(int terrain in new[]{0,4,9})
        {
            var frame=Caller.Frame(118,single);frame=frame with{Rng=new(Caller.MetronomeSeed(290)),Field=frame.Field with{Terrain=(WildBattleTerrain)terrain}};
            var result=Kernel.Direct(frame);Assert.AreEqual(Move,result.Events.OfType<SourceCallerSelectionEvent>().Single().Result);
            var cue=Called.Check(result,single,Kernel.Id("METRONOME"),290);Assert.AreEqual(290,cue.SourceMoveId);
            var selection=result.Events.OfType<SourceCallerSelectionEvent>().Single();var transition=result.Events.OfType<SourceCalledMoveTransitionEvent>().Single();
            Called.Deny(result.Events.Remove(selection),single,290);Called.Deny(result.Events.Remove(transition),single,290);
        }
    }

    [TestMethod]
    public void MissingOrForeignHitPpAndHeaderCannotAuthenticateTheSelector()
    {
        foreach(bool single in new[]{false,true})
        {
            var result=Kernel.Ordered(Frame(Bodies(),single));var events=result.Events;var used=events.OfType<SourceMoveUsedEvent>().Single();
            var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();var hit=events.OfType<SourceHpChangedEvent>().Single();
            foreach(var bad in new[]{events.Remove(attack),events.Remove(pp),events.Remove(hit),events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),events.SetItem(events.IndexOf(hit),hit with{Move=Kernel.Id("NEEDLE_ARM")}),events.SetItem(events.IndexOf(attack),attack with{ChosenMove=Kernel.Id("METRONOME")})})
                Envelope.Deny(bad,single,"Only the original authenticated Secret Power invocation owns this selector.");
            var denied=single?CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events,events.IndexOf(used)),true,SourceBattleScene.On,Kernel.Mechanics.Catalog.Moves,(_,_)=>false)
                :CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events,events.IndexOf(used)),SourceBattleScene.On,Kernel.Mechanics.Catalog.Moves,(_,_)=>false);
            Assert.IsNull(denied.Dispatch);
        }
    }
}
