using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Source-natural Sneasel and committed public-kernel results. Gapped party slots,
// HP, statuses and speeds are controlled integration fixtures, not route evidence.
internal static class BeatUpAnimationKernelFixture
{
    internal static MoveId Move=>Kernel.Id("BEAT_UP");
    internal static SourceBattleFrame Frame(bool single=false,int actor=0,bool gapped=true)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon("SNEASEL","BEAT_UP",57) with{AccuracyStage=12,SpeedStage=12};
        var frame=Kernel.Frame(bodies,single);var side=(BattleSide)(actor&1);
        static WildBattleCalledPartyMember Member(WildBattleCombatant body)=>new(body.Pokemon,body.HeldItem,WildBattleTransformationRules.PersistentStatus(body));
        var reserve=Member(Kernel.Mon("WAILORD","SPLASH"));
        var members=Enumerable.Repeat(reserve,6).ToImmutableArray().SetItem(0,Member(bodies[actor]));
        members=members.SetItem(3,Member(single?Kernel.Mon("WAILORD","SPLASH"):bodies[actor^2]));
        // Mechanics-only opponent parties cannot admit eggs; the owned-egg test
        // supplies a genuine source gift egg and its exact campaign catalog.
        if(gapped)members=members.SetItem(1,reserve with{Pokemon=reserve.Pokemon with{CurrentHp=0}})
            .SetItem(2,reserve with{Status=WildBattleStatus.Poison}).SetItem(4,reserve with{Pokemon=reserve.Pokemon with{CurrentHp=0}});
        var slots=frame.Slots.SetItem(actor,frame.Slots[actor] with{Member=new(side,0)});
        if(!single)slots=slots.SetItem(actor^2,slots[actor^2] with{Member=new(side,3)});
        frame=frame with{Parties=frame.Parties.SetItem((int)side,new(side,members)),Slots=slots,
            EntryAbilities=frame.EntryAbilities! with{AppliedEntries=slots.Select(s=>s.Occupant).ToImmutableArray()}};
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame));return frame;
    }
    internal static SourceBattleFrame OrderedFrame(bool single=false,int actor=0)
    {
        var frame=Frame(single,actor);
        // The public wild lifecycle has one opponent. Larger enemy party arrays
        // below exercise the direct recipe/export seam, not a fabricated trainer.
        if(single&&actor==1)frame=frame with{Parties=frame.Parties.SetItem(1,
            frame.Parties[1] with{Members=[frame.Parties[1].Members[0]]})};
        return frame;
    }
    internal static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events,int index)=>Envelope.Single(events,index) with{SourceEvents=[]};
    internal static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events,int index,bool single,bool result=true,SourceBattleScene scene=SourceBattleScene.On)=>single
        ?result?AnimationDispatchFixture.AtResult(Single(events,index),true,scene):AnimationDispatchFixture.AtAttackText(Single(events,index),true,scene)
        :result?AnimationDispatchFixture.AtResult(Envelope.Double(events,index),scene):AnimationDispatchFixture.AtAttackText(Envelope.Double(events,index),scene);
    internal static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events)=>events.Select((e,i)=>e with{Sequence=i+1}).ToImmutableArray();
    internal static void Deny(ImmutableArray<SourceBattleEvent> events,bool single,string reason)
    {for(int i=0;i<events.Length;i++)foreach(bool result in new[]{false,true})Assert.IsNull(Project(events,i,single,result).Dispatch,$"{reason}: {i}/{events[i].GetType().Name}");}
    internal static void Check(SourceKernelResult result,bool single)=>Check(result.Events,result,single);
    internal static void Check(SourceFourActionResult result,bool single)=>Check(result.Events,result,single);
    internal static void Check(ImmutableArray<SourceBattleEvent> events,object committed,bool single)
    {
        string before=JsonSerializer.Serialize(committed);var used=events.OfType<SourceMoveUsedEvent>().Single(e=>e.Move==Move);
        var attack=events.OfType<SourceAttackStringEvent>().Single(e=>e.CurrentMove==Move);var pp=events.OfType<SourcePpCommandEvent>().Single(e=>e.CurrentMove==Move);
        var contributors=events.OfType<SourceCalledBeatUpMemberEvent>().ToArray();var hits=Envelope.Hits(events);
        Assert.IsTrue(contributors.Length is >=1 and <=6);Assert.AreEqual(contributors.Length,hits.Length);Assert.IsFalse(events.OfType<SourceMultiHitEvent>().Any());
        Assert.AreEqual(used.Actor,pp.PpOwner);Assert.AreEqual(Move,pp.ChosenMove);Assert.AreEqual(Move,attack.ChosenMove);
        Assert.AreEqual(Math.Max(0,pp.Before-pp.CommandCost),pp.After);int count=0;
        for(int i=0;i<events.Length;i++)
        {
            var result=Project(events,i,single);Assert.AreEqual(result,Project(events,i,single));Assert.IsNull(Project(events,i,single,false).Dispatch);
            Assert.IsNull(Project(events,i,single,scene:SourceBattleScene.Off).Dispatch);
            if(single)Assert.IsNull(AnimationDispatchFixture.AtResult(Single(events,i),false,SourceBattleScene.On).Dispatch);
            int ordinal=Array.IndexOf(hits,events[i]);
            if(ordinal<0){Assert.IsNull(result.Dispatch);continue;}
            var cue=result.Dispatch;Assert.IsNotNull(cue,result.Reason);count++;
            Assert.AreEqual(251,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);
            Assert.AreEqual(events[i].Sequence,cue.Sequence);Assert.AreEqual(events[i].Sequence,cue.ResultSequence);
            Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);
            Assert.AreEqual((byte)ordinal,cue.MoveTurn);Assert.AreEqual(CampaignMoveAnimationBoundary.BeforeHitResult,cue.Boundary);
            Assert.IsFalse(cue.AcknowledgesCurrentReceipt);Assert.AreEqual(events[i] is SourceSubstituteDamagedEvent,cue.HitsSubstitute);
            Assert.IsTrue(contributors[ordinal].Sequence<events[i].Sequence);
            if(ordinal+1<contributors.Length)Assert.IsTrue(events[i].Sequence<contributors[ordinal+1].Sequence);
        }
        Assert.AreEqual(hits.Length,count);Assert.AreEqual(before,JsonSerializer.Serialize(committed),"Projection preserves state, RNG, events and trace.");
    }
}
