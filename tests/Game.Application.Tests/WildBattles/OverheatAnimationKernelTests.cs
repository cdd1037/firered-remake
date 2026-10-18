using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using F = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class OverheatAnimationKernelTests
{
    [TestMethod]
    public void LegalTm50UserOwnsHitAndLaterSelfDropAcrossEveryPosition()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = F.Ordered(F.Initial(315, single, actor), actor, actor ^ 1); var used = result.Events.OfType<SourceMoveUsedEvent>().Single(); var hit = Envelope.Hits(result.Events).Single();
            Assert.AreEqual("EFFECT_OVERHEAT", Kernel.Mechanics.Catalog.Moves[used.Move].Effect); F.Check(result.Events,315,single,used,hit);
            var change = result.Events.OfType<SourceStatChangedEvent>().Single(); Assert.AreEqual(WildBattleStat.SpecialAttack,change.Stat); Assert.AreEqual(used.Actor,change.Actor); Assert.AreEqual(used.Actor,change.Target); Assert.AreEqual(6,change.Before); Assert.AreEqual(4,change.After); Assert.IsTrue(change.Sequence>hit.Sequence);
            for(int i=0;i<result.Events.Length;i++)
            {var wave=F.Wave(result.Events,i,single);if(result.Events[i]!=change){Assert.IsNull(wave);continue;}Assert.IsNotNull(wave);Assert.AreEqual(315,wave.SourceMoveId);Assert.AreEqual(49,wave.AnimationArgument);Assert.AreEqual(used.Actor,wave.Affected);}
        }
    }

    [TestMethod, DataRow(6,false), DataRow(1,false), DataRow(0,false), DataRow(6,true)]
    public void SourceTwoStageDropCapAndWhiteHerbNeverReconstructOrReplayTheHit(int stage,bool whiteHerb)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=F.Body(F.Initial(315,single),0,b=>b with{SpecialAttackStageDelta=stage-6,HeldItem=whiteHerb?"ITEM_WHITE_HERB":"ITEM_NONE"});var result=F.Ordered(frame,0,1);F.Check(result.Events,315,single,result.Events.OfType<SourceMoveUsedEvent>().Single(),Envelope.Hits(result.Events).Single());
            var change=result.Events.OfType<SourceStatChangedEvent>().FirstOrDefault(e=>e.Stat==WildBattleStat.SpecialAttack&&e.After<e.Before);
            if(stage==0){Assert.IsNull(change);Assert.AreEqual(1,result.Events.OfType<SourceStatAtMinimumEvent>().Count());}else{Assert.IsNotNull(change);Assert.AreEqual(stage,change.Before);Assert.AreEqual(Math.Max(0,stage-2),change.After);}
            for(int i=0;i<result.Events.Length;i++){var wave=F.Wave(result.Events,i,single);if(result.Events[i]!=change){Assert.IsNull(wave);continue;}Assert.IsNotNull(wave);Assert.AreEqual(49,wave.AnimationArgument);Assert.AreEqual(new BattlerSlot(0),wave.Affected);}
            if(whiteHerb){Assert.IsTrue(result.Events.OfType<SourceHeldItemConsumedEvent>().Any(e=>e.Item=="ITEM_WHITE_HERB"));Assert.AreEqual(0,result.State.Slots[0].Body!.SpecialAttackStageDelta);}
        }
    }

    [TestMethod, DataRow("substitute"), DataRow("ko"), DataRow("protected"), DataRow("miss"), DataRow("flash-fire")]
    public void TargetSubstituteAndKoKeepSelfDropWhileFailedHitsOwnNeitherRootNorWave(string branch)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=F.Initial(315,single);
            if(branch=="substitute")frame=F.Body(frame,1,b=>b with{CalledVolatile=new(){SubstituteHp=100}});
            if(branch=="ko")frame=F.Body(frame,1,b=>b with{Pokemon=b.Pokemon with{CurrentHp=1}});
            if(branch=="protected")frame=F.Body(frame,1,b=>b with{Protected=true});
            if(branch=="miss"){frame=F.Body(frame,0,b=>b with{AccuracyStage=0});frame=F.Body(frame,1,b=>b with{EvasionStage=12});}
            if(branch=="flash-fire"){frame=F.Body(frame,1,_=>Kernel.Mon("VULPIX","EMBER",100) with{SpeedStage=0});Assert.AreEqual("ABILITY_FLASH_FIRE",WildBattleEffective.Ability(frame.Slots[1].Body!));}
            var result=branch=="miss"?F.Find(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"overheat-miss"):F.Ordered(frame,0,1);
            if(branch is "substitute" or "ko")
            {
                var hit=Envelope.Hits(result.Events).Single();F.Check(result.Events,315,single,result.Events.OfType<SourceMoveUsedEvent>().Single(),hit);
                if(branch=="substitute")Assert.IsInstanceOfType<SourceSubstituteDamagedEvent>(hit);else Assert.AreEqual(0,((SourceHpChangedEvent)hit).HpAfter);
                var stat=result.Events.OfType<SourceStatChangedEvent>().Single(e=>e.Stat==WildBattleStat.SpecialAttack&&e.After<e.Before);Assert.IsNotNull(F.Wave(result.Events,result.Events.IndexOf(stat),single));
            }
            else{F.Deny(result.Events,single);Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any(e=>e.Stat==WildBattleStat.SpecialAttack));for(int i=0;i<result.Events.Length;i++)Assert.IsNull(F.Wave(result.Events,i,single));}
        }
    }

    [TestMethod, DataRow("pp-owner"), DataRow("chosen-move"), DataRow("hit-owner")]
    public void ForeignOwnershipCannotBorrowOverheatHitOrSelfDrop(string mutation)
    {
        foreach(bool single in new[]{false,true})
        {
            var result=F.Ordered(F.Initial(315,single),0,1);var events=result.Events;var pp=events.OfType<SourcePpCommandEvent>().Single();var hit=events.OfType<SourceHpChangedEvent>().Single();
            events=mutation switch{"pp-owner"=>events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),"chosen-move"=>events.SetItem(events.IndexOf(pp),pp with{ChosenMove=Kernel.Id("EMBER")}),"hit-owner"=>events.SetItem(events.IndexOf(hit),hit with{Actor=new(1)}),_=>throw new InvalidOperationException()};
            F.Deny(events,single);for(int i=0;i<events.Length;i++)Assert.IsNull(F.Wave(events,i,single));
        }
    }
}
