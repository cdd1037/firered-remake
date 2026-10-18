using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
using Family = FireRed.Tests.Application.WildBattles.ControlPowerAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class ProtectAnimationKernelTests
{
    [TestMethod]
    public void ActualRepeatFailureAndLastActionNeverAuthorizeProtect()
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Family.Bodies(182);bodies[0]=bodies[0] with{LastUsedMove=Family.Move(182),ProtectConsecutiveUses=1};
            var repeat=Kernel.Find(Kernel.Frame(bodies,single),0,0,r=>r.Events.OfType<SourceProtectAttemptEvent>().Any(e=>!e.Succeeded),"Protect repeat failure");
            var attempt=repeat.Events.OfType<SourceProtectAttemptEvent>().Single();Assert.IsTrue(attempt.NotLastAction);Assert.IsTrue(attempt.Roll>attempt.Threshold);Shared.Check(repeat,182,single,null);
            bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("HITMONCHAN","DETECT") with{SpeedStage=12}).ToArray();bodies[0]=Family.Bodies(182)[0] with{SpeedStage=0};
            var frame=Kernel.Frame(bodies,single);var choices=frame.Slots.Take(frame.BattlerCount).Select(s=>new SourceActionSelection(s.Occupant!.Value,SourceActionKind.Move,0,s.Slot)).ToImmutableArray();
            var last=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);Assert.IsTrue(last.Succeeded,last.Fault);Assert.AreEqual(0,last.State.Queue[^1].Slot.Value);
            for(int step=0;!last.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==0);step++)
            {Assert.IsTrue(step<16);Assert.IsNotNull(last.Continuation);last=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,last.State,last.Continuation.Value);Assert.IsTrue(last.Succeeded,last.Fault);}
            attempt=last.Events.OfType<SourceProtectAttemptEvent>().Single();Assert.IsFalse(attempt.NotLastAction);Assert.IsFalse(attempt.Succeeded);Shared.Check(last,182,single,null);
        }
    }
    [TestMethod]
    public void ProtectNeedsTheSuccessfulOwnedAttemptAndItsAdjacentMarker()
    {
        foreach(bool single in new[]{false,true})
        {
            var result=Kernel.Ordered(Kernel.Frame(Family.Bodies(182),single),0,0);var events=result.Events;var attempt=events.OfType<SourceProtectAttemptEvent>().Single();var marker=events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="protect");var pp=events.OfType<SourcePpCommandEvent>().Single();
            Shared.Check(result,182,single,attempt);int ai=events.IndexOf(attempt),mi=events.IndexOf(marker),pi=events.IndexOf(pp);Assert.AreEqual(pi+1,ai);Assert.AreEqual(ai+1,mi);
            void Deny(ImmutableArray<SourceBattleEvent> changed)=>Envelope.Deny(changed,single,"Protect requires successful owned attempt and adjacent protect marker.");
            Deny(events.RemoveAt(ai));Deny(events.RemoveAt(mi));Deny(events.RemoveAt(pi));Deny(Shared.Number(events.Insert(mi+1,marker)));Deny(Shared.Number(events.Insert(ai+1,attempt)));
            Deny(events.SetItem(pi,pp with{PpOwner=new(1)}));Deny(events.SetItem(ai,marker with{Sequence=attempt.Sequence}).SetItem(mi,attempt with{Sequence=marker.Sequence}));
            foreach(var bad in new[]{attempt with{Side=new(1)},attempt with{Move=Kernel.Id("DETECT")},attempt with{Succeeded=false},attempt with{NotLastAction=false},attempt with{After=0},attempt with{Threshold=0,Roll=1},attempt with{Roll=-1}})Deny(events.SetItem(ai,bad));
            foreach(var bad in new[]{marker with{Side=new(1)},marker with{Volatile="endure"},marker with{Before=1},marker with{After=0},marker with{Reason="countdown"}})Deny(events.SetItem(mi,bad));
        }
    }
}
