using Game.Application;
using Game.Domain.World;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldDoorPresentationTests
{
    private static JsonDocument SourceVectors()=>JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(
        "tests/Game.Presentation.Tests/Fixtures/field-door-source-vectors.json",
        "python tests/Reference/generate_field_door_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-door-source-vectors.json")));

    [TestMethod]
    [DataRow(40)]
    [DataRow(73)]
    public void LadderRecordedStepAndPlainArrivalReuseTheSourceControllers(int switchFrame)
    {
        var live=new FieldDoorPresentation.StepDeparture();
        Assert.AreEqual(40,FieldDoorPresentation.DepartureFrames(FieldTransitionCause.PlainWarp,FieldTransitionDeparture.StepOntoWarp));
        for(int frame=0;frame<switchFrame;frame++)
        {
            if(frame>0)live.Advance(frame>=switchFrame-1);
            var recorded=FieldDoorPresentation.SampleRecorded(FieldTransitionCause.PlainWarp,FieldTransitionArrival.Plain,
                switchFrame,frame,departureKind:FieldTransitionDeparture.StepOntoWarp);
            Assert.AreEqual(live.Sample,recorded,$"departure {frame}");
            Assert.AreEqual(Math.Min(frame,16),recorded.WalkPixels);
            Assert.AreEqual(frame>16,recorded.MusicFadeRequested);
        }
        live.Advance(true);Assert.IsTrue(live.Complete);
        var arrival=new FieldWarpPresentation.PlainArrival();
        for(int frame=0;frame<=24;frame++)
        {
            if(frame>0)arrival.Advance();
            Assert.AreEqual(FieldDoorPresentation.FromPlain(arrival.Sample),FieldDoorPresentation.SampleRecorded(
                FieldTransitionCause.PlainWarp,FieldTransitionArrival.Plain,switchFrame,switchFrame+frame,
                departureKind:FieldTransitionDeparture.StepOntoWarp),$"arrival {frame}");
        }
        Assert.IsTrue(arrival.Complete);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldDoorPresentation.SampleRecorded(
            FieldTransitionCause.PlainWarp,FieldTransitionArrival.Plain,39,0,departureKind:FieldTransitionDeparture.StepOntoWarp));
    }

    [TestMethod]
    public void FreshNormalWalkDisplaysEightFootCallbacksThenEightStandingCallbacks()
    {
        // Original sAnim_GoSouth/North: foot,stand,alternate-foot,stand each8.
        // SeekSpriteAnim primes8; AnimateSprites decrements in the start callback.
        // The16th movement callback pauses the animation before its sprite update.
        int[] expected=[1,1,1,1,1,1,1,1,0,0,0,0,0,0,0,0];
        Assert.AreEqual(0,FieldDoorPresentation.NormalWalkPoseFrame(0));
        for(int pixels=1;pixels<=16;pixels++)
            Assert.AreEqual(expected[pixels-1],FieldDoorPresentation.NormalWalkPoseFrame(pixels),$"walk pixel {pixels}");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldDoorPresentation.NormalWalkPoseFrame(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldDoorPresentation.NormalWalkPoseFrame(17));
    }

    [TestMethod]
    public void EveryDoorTaskPaletteWalkingPixelWindowAndSoundMatchesExtractedOriginalCallbacks()
    {
        using var document=SourceVectors();int traces=0,rows=0;
        foreach(var trace in document.RootElement.GetProperty("traces").EnumerateArray())
        {
            traces++;string name=trace.GetProperty("name").GetString()!;
            Assert.IsTrue(name is "doorDepartureReady" or "doorDepartureDelayed" or "animatedArrival"
                or "stepDepartureReady" or "stepDepartureDelayed" or "nonAnimatedArrival");
            var departure=new FieldDoorPresentation.Departure();
            var arrival=new FieldDoorPresentation.AnimatedArrival();
            var stepDeparture=new FieldDoorPresentation.StepDeparture();
            var nonAnimatedArrival=new FieldDoorPresentation.NonAnimatedArrival();
            foreach(var row in trace.GetProperty("frames").EnumerateArray())
            {
                rows++;var v=row.EnumerateArray().Select(value=>value.GetInt32()).ToArray();
                Assert.AreEqual(18,v.Length);int frame=v[0];
                if(frame>0)
                {
                    if(name=="animatedArrival")arrival.Advance();
                    else if(name=="nonAnimatedArrival")nonAnimatedArrival.Advance();
                    else if(name.StartsWith("stepDeparture",StringComparison.Ordinal))stepDeparture.Advance(name=="stepDepartureReady"||frame>=100);
                    else departure.Advance(name=="doorDepartureReady"||frame>=100);
                }
                var expected=new FieldDoorPresentation.PhaseSample(frame,v[1],v[2],v[3],v[4]!=0,v[5]!=0,
                    v[6],v[7]!=0,v[8]!=0,v[9],v[10]!=0,v[11],v[12],v[15]!=0,v[16]!=0,v[17]!=0);
                var actual=name switch
                {
                    "animatedArrival"=>arrival.Sample,"nonAnimatedArrival"=>nonAnimatedArrival.Sample,
                    "stepDepartureReady" or "stepDepartureDelayed"=>stepDeparture.Sample,_=>departure.Sample
                };
                Assert.AreEqual(expected,actual,$"{name} callback {frame}");
                // Host begins with zero saved registers. On the setup callback
                // WIN1 extends to255; child callbacks use240; restore returns0.
                int win0=actual.WipeActive?actual.WipeLeft:0;
                int win1=actual.WipeActive?(actual.WipeRight<<8)|(frame==1?255:240):0;
                Assert.AreEqual(v[13],win0,$"{name} WIN0H callback {frame}");
                Assert.AreEqual(v[14],win1,$"{name} WIN1H callback {frame}");
            }
            Assert.IsTrue(name switch
            {
                "animatedArrival"=>arrival.Complete,"nonAnimatedArrival"=>nonAnimatedArrival.Complete,
                "stepDepartureReady" or "stepDepartureDelayed"=>stepDeparture.Complete,_=>departure.Complete
            });
        }
        Assert.AreEqual(6,traces);Assert.AreEqual(451,rows);
    }

    [TestMethod]
    public void DepartureDelaysMusicUntilAfterOpeningWalkingHidingAndClosing()
    {
        var departure=new FieldDoorPresentation.Departure();
        int soundPulses=0;
        for(int frame=1;frame<=FieldDoorPresentation.MinimumDepartureFrames;frame++)
        {
            departure.Advance(true);var sample=departure.Sample;
            if(sample.OpenDoorSoundRequested){soundPulses++;Assert.AreEqual(1,frame);}
            Assert.AreEqual(Math.Clamp(frame-20,0,16),sample.WalkPixels,$"walk {frame}");
            Assert.AreEqual(frame<37,sample.PlayerVisible,$"visibility {frame}");
            Assert.AreEqual(frame>=58,sample.MusicFadeRequested,$"music request {frame}");
            Assert.AreEqual(frame>=58,sample.TeleportTask,$"teleport task {frame}");
            Assert.AreEqual(frame is >=58 and <79,sample.PaletteActive,$"palette {frame}");
            Assert.AreEqual(frame==81,sample.Complete,$"completion {frame}");
        }
        Assert.AreEqual(1,soundPulses);
        var final=departure.Sample;departure.Advance(false);Assert.AreEqual(final,departure.Sample);
    }

    [TestMethod]
    public void DelayedMusicCannotTransferEarlyOrReopenTheDoor()
    {
        var departure=new FieldDoorPresentation.Departure();
        for(int frame=1;frame<=120;frame++)departure.Advance(false);
        Assert.IsFalse(departure.Complete);Assert.AreEqual(1,departure.Sample.TaskPhase);
        Assert.AreEqual(16,departure.Sample.BackgroundBlend);Assert.AreEqual(16,departure.Sample.ObjectBlend);
        Assert.IsFalse(departure.Sample.DoorAnimationActive);Assert.IsFalse(departure.Sample.PlayerVisible);
        departure.Advance(true);Assert.AreEqual(2,departure.Sample.TaskPhase);Assert.IsFalse(departure.Complete);
        departure.Advance(false);Assert.IsTrue(departure.Complete);
        Assert.AreEqual(122,departure.Frames);
    }

    [TestMethod]
    public void ArrivalUsesFrlgDelayAndOverlapsClosingWithTheLastTwoWalkingPixels()
    {
        var arrival=new FieldDoorPresentation.AnimatedArrival();int soundPulses=0;
        for(int frame=1;frame<=FieldDoorPresentation.AnimatedArrivalFrames;frame++)
        {
            arrival.Advance();var sample=arrival.Sample;
            if(sample.OpenDoorSoundRequested){soundPulses++;Assert.AreEqual(26,frame);}
            Assert.AreEqual(Math.Clamp(frame-45,0,16),sample.WalkPixels,$"walk {frame}");
            Assert.AreEqual(frame>=46,sample.PlayerVisible,$"visibility {frame}");
            Assert.AreEqual(frame<43,sample.WipeActive,$"wipe {frame}");
            Assert.AreEqual(frame<46,sample.PaletteActive,$"palette {frame}");
            Assert.IsFalse(sample.MusicFadeRequested);
            if(frame==1){Assert.AreEqual(6,sample.TaskPhase);Assert.AreEqual(120,sample.WipeLeft);Assert.AreEqual(120,sample.WipeRight);}
            if(frame==60){Assert.AreEqual(9,sample.TaskPhase);Assert.AreEqual(15,sample.WalkPixels);Assert.IsTrue(sample.DoorAnimationActive);}
            if(frame==80){Assert.AreEqual(4,sample.TaskPhase);Assert.IsFalse(sample.Complete);}
            Assert.AreEqual(frame==81,sample.Complete,$"completion {frame}");
        }
        Assert.AreEqual(1,soundPulses);
        var final=arrival.Sample;arrival.Advance();Assert.AreEqual(final,arrival.Sample);
    }

    [TestMethod]
    public void SharedDelayThreePaletteMatchesEveryOriginalArrivalCallback()
    {
        using var document=SourceVectors();
        var trace=document.RootElement.GetProperty("traces").EnumerateArray().Single(t=>t.GetProperty("name").GetString()=="animatedArrival");
        var palette=new FieldWarpPresentation.NormalPalette(true,3);
        foreach(var row in trace.GetProperty("frames").EnumerateArray().Skip(1))
        {
            palette.Advance();
            Assert.AreEqual(row[2].GetInt32(),palette.BackgroundBlend,$"BG {row[0]}");
            Assert.AreEqual(row[3].GetInt32(),palette.ObjectBlend,$"OBJ {row[0]}");
            Assert.AreEqual(row[4].GetInt32()!=0,palette.Active,$"active {row[0]}");
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldWarpPresentation.NormalPalette(true,-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldWarpPresentation.NormalPalette(true,128));
    }

    [TestMethod]
    public void NonAnimatedDepartureFinishesNormalStepBeforeRequestingMusicAndPalette()
    {
        var departure=new FieldDoorPresentation.StepDeparture();
        for(int frame=0;frame<=FieldDoorPresentation.MinimumStepDepartureFrames;frame++)
        {
            if(frame>0)departure.Advance(true);
            var sample=departure.Sample;
            Assert.AreEqual(Math.Min(frame,16),sample.WalkPixels,$"walk {frame}");
            Assert.IsTrue(sample.PlayerVisible);
            Assert.AreEqual(frame>=17,sample.MusicFadeRequested,$"music {frame}");
            Assert.AreEqual(frame>=17,sample.TeleportTask,$"warp task {frame}");
            Assert.AreEqual(frame is >=17 and <38,sample.PaletteActive,$"palette {frame}");
            Assert.AreEqual(frame==40,sample.Complete,$"complete {frame}");
            Assert.AreEqual(-1,sample.DoorFrame);Assert.IsFalse(sample.DoorAnimationActive);
            Assert.IsFalse(sample.WipeActive);Assert.IsFalse(sample.OpenDoorSoundRequested);
        }
        var final=departure.Sample;departure.Advance(false);Assert.AreEqual(final,departure.Sample);
    }

    [TestMethod]
    public void NonAnimatedDepartureKeepsTheCompletedStepWhileWaitingForMusic()
    {
        var departure=new FieldDoorPresentation.StepDeparture();
        for(int frame=1;frame<=120;frame++)departure.Advance(false);
        Assert.IsFalse(departure.Complete);Assert.AreEqual(1,departure.Sample.TaskPhase);
        Assert.AreEqual(16,departure.Sample.WalkPixels);Assert.IsTrue(departure.Sample.PlayerVisible);
        Assert.AreEqual(16,departure.Sample.BackgroundBlend);Assert.AreEqual(16,departure.Sample.ObjectBlend);
        departure.Advance(true);Assert.AreEqual(2,departure.Sample.TaskPhase);Assert.IsFalse(departure.Complete);
        departure.Advance(false);Assert.IsTrue(departure.Complete);Assert.AreEqual(122,departure.Frames);
    }

    [TestMethod]
    public void NonAnimatedArrivalHidesUntilThePaletteWeatherAndTaskGatesAllowTheFullStep()
    {
        var arrival=new FieldDoorPresentation.NonAnimatedArrival();
        for(int frame=0;frame<=FieldDoorPresentation.NonAnimatedArrivalFrames;frame++)
        {
            if(frame>0)arrival.Advance();
            var sample=arrival.Sample;
            Assert.AreEqual(Math.Clamp(frame-23,0,16),sample.WalkPixels,$"walk {frame}");
            Assert.AreEqual(frame==0||frame>=24,sample.PlayerVisible,$"visibility {frame}");
            Assert.AreEqual(frame<22,sample.PaletteActive,$"palette {frame}");
            Assert.AreEqual(frame==41,sample.Complete,$"complete {frame}");
            Assert.AreEqual(frame switch {0=>0,<24=>1,<40=>2,_=>3},sample.TaskPhase,$"phase {frame}");
            Assert.IsFalse(sample.MusicFadeRequested);Assert.IsFalse(sample.TeleportTask);
            Assert.AreEqual(-1,sample.DoorFrame);Assert.IsFalse(sample.DoorAnimationActive);
            Assert.IsFalse(sample.WipeActive);Assert.IsFalse(sample.OpenDoorSoundRequested);
        }
        var final=arrival.Sample;arrival.Advance();Assert.AreEqual(final,arrival.Sample);
    }

    [TestMethod]
    [DataRow(FieldTransitionCause.PlainWarp,FieldTransitionArrival.Plain,0)]
    [DataRow(FieldTransitionCause.PlainWarp,FieldTransitionArrival.Plain,45)]
    [DataRow(FieldTransitionCause.PlainWarp,FieldTransitionArrival.AnimatedDoor,0)]
    [DataRow(FieldTransitionCause.PlainWarp,FieldTransitionArrival.AnimatedDoor,45)]
    [DataRow(FieldTransitionCause.PlainWarp,FieldTransitionArrival.NonAnimatedDoor,0)]
    [DataRow(FieldTransitionCause.PlainWarp,FieldTransitionArrival.NonAnimatedDoor,45)]
    [DataRow(FieldTransitionCause.AnimatedDoor,FieldTransitionArrival.Plain,0)]
    [DataRow(FieldTransitionCause.AnimatedDoor,FieldTransitionArrival.Plain,45)]
    [DataRow(FieldTransitionCause.AnimatedDoor,FieldTransitionArrival.AnimatedDoor,0)]
    [DataRow(FieldTransitionCause.AnimatedDoor,FieldTransitionArrival.AnimatedDoor,45)]
    [DataRow(FieldTransitionCause.AnimatedDoor,FieldTransitionArrival.NonAnimatedDoor,0)]
    [DataRow(FieldTransitionCause.AnimatedDoor,FieldTransitionArrival.NonAnimatedDoor,45)]
    [DataRow(FieldTransitionCause.NonAnimatedDoor,FieldTransitionArrival.Plain,0)]
    [DataRow(FieldTransitionCause.NonAnimatedDoor,FieldTransitionArrival.Plain,45)]
    [DataRow(FieldTransitionCause.NonAnimatedDoor,FieldTransitionArrival.AnimatedDoor,0)]
    [DataRow(FieldTransitionCause.NonAnimatedDoor,FieldTransitionArrival.AnimatedDoor,45)]
    [DataRow(FieldTransitionCause.NonAnimatedDoor,FieldTransitionArrival.NonAnimatedDoor,0)]
    [DataRow(FieldTransitionCause.NonAnimatedDoor,FieldTransitionArrival.NonAnimatedDoor,45)]
    public void RecordedDepartureAndArrivalBranchesShareEveryLiveSample(FieldTransitionCause cause,FieldTransitionArrival kind,int musicDelay)
    {
        int minimum=cause switch
        {
            FieldTransitionCause.AnimatedDoor=>FieldDoorPresentation.MinimumDepartureFrames,
            FieldTransitionCause.NonAnimatedDoor=>FieldDoorPresentation.MinimumStepDepartureFrames,
            _=>FieldWarpPresentation.MinimumDepartureFrames
        };
        int switchFrame=minimum+musicDelay;
        var doorDeparture=new FieldDoorPresentation.Departure();var plainDeparture=new FieldWarpPresentation.Departure();
        var stepDeparture=new FieldDoorPresentation.StepDeparture();
        for(int frame=0;frame<switchFrame;frame++)
        {
            var sample=cause switch
            {
                FieldTransitionCause.AnimatedDoor=>doorDeparture.Sample,FieldTransitionCause.NonAnimatedDoor=>stepDeparture.Sample,
                _=>FieldDoorPresentation.FromPlain(plainDeparture.Sample)
            };
            Assert.AreEqual(sample,FieldDoorPresentation.SampleRecorded(cause,kind,switchFrame,frame),$"departure {frame}");
            bool ready=frame+1>=switchFrame-1;
            if(cause==FieldTransitionCause.AnimatedDoor)doorDeparture.Advance(ready);
            else if(cause==FieldTransitionCause.NonAnimatedDoor)stepDeparture.Advance(ready);
            else plainDeparture.Advance(ready);
        }
        Assert.IsTrue(cause switch
        {
            FieldTransitionCause.AnimatedDoor=>doorDeparture.Complete,FieldTransitionCause.NonAnimatedDoor=>stepDeparture.Complete,
            _=>plainDeparture.Complete
        });
        var doorArrival=new FieldDoorPresentation.AnimatedArrival();var plainArrival=new FieldWarpPresentation.PlainArrival();
        var nonAnimatedArrival=new FieldDoorPresentation.NonAnimatedArrival();
        int end=kind switch
        {
            FieldTransitionArrival.AnimatedDoor=>FieldDoorPresentation.AnimatedArrivalFrames,
            FieldTransitionArrival.NonAnimatedDoor=>FieldDoorPresentation.NonAnimatedArrivalFrames,
            _=>FieldWarpPresentation.PlainArrivalFrames
        };
        for(int frame=0;frame<=end+3;frame++)
        {
            var sample=kind switch
            {
                FieldTransitionArrival.AnimatedDoor=>doorArrival.Sample,FieldTransitionArrival.NonAnimatedDoor=>nonAnimatedArrival.Sample,
                _=>FieldDoorPresentation.FromPlain(plainArrival.Sample)
            };
            Assert.AreEqual(sample,FieldDoorPresentation.SampleRecorded(cause,kind,switchFrame,switchFrame+frame),$"arrival {frame}");
            if(kind==FieldTransitionArrival.AnimatedDoor)doorArrival.Advance();
            else if(kind==FieldTransitionArrival.NonAnimatedDoor)nonAnimatedArrival.Advance();
            else plainArrival.Advance();
        }
        Assert.IsTrue(kind switch
        {
            FieldTransitionArrival.AnimatedDoor=>doorArrival.Complete,FieldTransitionArrival.NonAnimatedDoor=>nonAnimatedArrival.Complete,
            _=>plainArrival.Complete
        });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldDoorPresentation.SampleRecorded(cause,kind,minimum-1,0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldDoorPresentation.SampleRecorded(cause,kind,switchFrame,-1));
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(120)]
    public void HostCadenceCannotChangeDoorCallbackPhases(int hz)
    {
        var departure=new FieldDoorPresentation.Departure();var arrival=new FieldDoorPresentation.AnimatedArrival();
        var stepDeparture=new FieldDoorPresentation.StepDeparture();var nonAnimatedArrival=new FieldDoorPresentation.NonAnimatedArrival();
        double elapsed=0;int processed=0;
        for(int hostFrame=0;hostFrame<hz*2;hostFrame++)
        {
            elapsed+=60d/hz;
            while(processed<(int)Math.Floor(elapsed+1e-7))
            {processed++;departure.Advance(true);arrival.Advance();stepDeparture.Advance(true);nonAnimatedArrival.Advance();}
        }
        Assert.AreEqual(81,departure.Frames);Assert.IsTrue(departure.Complete);
        Assert.AreEqual(81,arrival.Frames);Assert.IsTrue(arrival.Complete);
        Assert.AreEqual(40,stepDeparture.Frames);Assert.IsTrue(stepDeparture.Complete);
        Assert.AreEqual(41,nonAnimatedArrival.Frames);Assert.IsTrue(nonAnimatedArrival.Complete);
    }
}
