using Game.Application;
using System.Text.Json;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignFieldGrassLandingEffectTests
{
    private static int? Grass(int x,int y)=>2;
    private static bool Exists(string id)=>true;
    private static CampaignFieldGrassLandingEffects Land()
    {
        var owner=new CampaignFieldGrassLandingEffects();
        owner.QueueMovement("player",10,4,10,6,"jump_2_down",32,0);
        owner.AdvanceTo(32,Grass,Exists);
        return owner;
    }

    [TestMethod]
    public void GrassImpactCoverAndDepthMatchOriginalExtractedCallbacks()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile("tests/Reference/field-jump-effect-source-vectors.json",
            "python tests/Reference/generate_field_jump_effect_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Reference/field-jump-effect-source-vectors.json")));
        var impact=source.RootElement.GetProperty("grassImpact").EnumerateArray().ToArray();
        var covers=source.RootElement.GetProperty("grassCover").EnumerateArray().ToArray();
        var priorities=source.RootElement.GetProperty("grassPriority").EnumerateArray().ToArray();
        Assert.AreEqual(35,impact.Length);Assert.AreEqual(160,covers.Length);Assert.AreEqual(7,priorities.Length);
        foreach(var row in impact)
        {
            var cue=new CampaignFieldJumpCue("player","map.route-1",10,4,10,6,"jump_2_down",32,32+row[0].GetInt32());
            Assert.AreEqual(row[1].GetInt32(),CampaignFieldJumpEffects.GrassImpactFrame(cue,2),$"source grass impact {row}");
        }
        for(int scenario=0;scenario<8;scenario++)
        {
            var owner=Land();
            if(scenario is 1 or 2 or 7)owner.QueueMovement("player",10,6,10,7,"walk_down",scenario==1?16:8,32);
            if(scenario==3)owner.QueueMovement("player",10,6,10,8,"jump_2_down",32,32);
            if(scenario==6)owner.QueueMovement("player",10,6,10,6,"face_up",1,32);
            if(scenario==7)owner.QueueMovement("player",10,7,10,6,"walk_up",16,40);
            var rows=covers.Where(r=>r[0].GetInt32()==scenario).ToArray();Assert.AreEqual(20,rows.Length);
            foreach(var row in rows)
            {
                int age=row[1].GetInt32();
                owner.AdvanceTo(32+age,(_,_)=>scenario==5&&age>=2?0:2,_=>scenario!=4||age<2);
                var original=owner.Covers.Where(c=>!c.Rustling).ToArray();
                Assert.AreEqual(row[2].GetInt32(),original.Length==0?-1:0,$"source grass cover {row}");
                if(original.Length>0)Assert.AreEqual(age,original.Single().Age);
            }
        }
        foreach(var row in priorities)
        {
            int x=row[1].GetInt32(),y=row[2].GetInt32();
            int actor=CampaignFieldGrassLandingEffects.Subpriority(3,y,32,0,1);
            Assert.AreEqual(row[3].GetInt32(),actor);
            Assert.AreEqual(row[4].GetInt32(),CampaignFieldGrassLandingEffects.AdjustCoverSubpriority(8,8,145,[new(x,y,32,actor)]),$"source grass depth {row}");
        }
    }

    [TestMethod]
    public void LandingStartsAtFinalCallbackAndStationaryCoverHasNoFixedTail()
    {
        var owner=new CampaignFieldGrassLandingEffects();
        owner.QueueMovement("player",10,4,10,6,"jump_2_down",32,0);
        owner.AdvanceTo(31.99,Grass,Exists);Assert.AreEqual(0,owner.Covers.Length);
        owner.AdvanceTo(32,Grass,Exists);Assert.AreEqual(new CampaignFieldGrassLandingCover("player",10,6,0),owner.Covers.Single());
        owner.AdvanceTo(32.99,Grass,Exists);Assert.AreEqual(0,owner.Covers.Single().Age);
        owner.AdvanceTo(132,Grass,Exists);Assert.AreEqual(100,owner.Covers.Single().Age);
    }

    [TestMethod]
    public void NormalWalkKeepsPreviousTileUntilCompletionAndRemovesOnNextCallback()
    {
        var owner=Land();owner.QueueMovement("player",10,6,10,7,"walk_down",16,32);
        foreach(double frame in new[]{33d,47d,48d,48.99})
        { owner.AdvanceTo(frame,Grass,Exists);Assert.AreEqual(1,owner.Covers.Count(c=>!c.Rustling),$"frame {frame}"); }
        owner.AdvanceTo(49,Grass,Exists);Assert.AreEqual(0,owner.Covers.Count(c=>!c.Rustling));
        Assert.AreEqual(7,owner.Covers.Single().Y);
    }

    [TestMethod]
    public void FastDepartureWaitsForStationaryAnimationEndAndDepartureStaysSticky()
    {
        var owner=Land();owner.QueueMovement("player",10,6,10,7,"player_run_down",8,32);
        owner.AdvanceTo(40,Grass,Exists);Assert.AreEqual(1,owner.Covers.Count(c=>!c.Rustling));
        // Returning before the END callback cannot clear the source data7 bit.
        owner.QueueMovement("player",10,7,10,6,"walk_up",16,40);
        owner.AdvanceTo(42,Grass,Exists);Assert.AreEqual(1,owner.Covers.Count(c=>!c.Rustling));
        owner.AdvanceTo(43,Grass,Exists);Assert.AreEqual(0,owner.Covers.Count(c=>!c.Rustling));
    }

    [TestMethod]
    public void FarJumpLeavesOldCoverAtMidpointThenCreatesDestinationCover()
    {
        var owner=Land();owner.QueueMovement("player",10,6,10,8,"jump_2_down",32,32);
        owner.AdvanceTo(48,Grass,Exists);Assert.AreEqual(6,owner.Covers.Single().Y);
        owner.AdvanceTo(49,Grass,Exists);Assert.AreEqual(0,owner.Covers.Length);
        owner.AdvanceTo(64,Grass,Exists);Assert.AreEqual(new CampaignFieldGrassLandingCover("player",10,8,0),owner.Covers.Single());
    }

    [TestMethod]
    public void InPlaceTurnsAndJumpsPreserveAndDeduplicateExistingCover()
    {
        var owner=Land();
        owner.QueueMovement("player",10,6,10,6,"face_left",1,32);
        owner.QueueMovement("player",10,6,10,6,"walk_in_place_up",16,33);
        owner.QueueMovement("player",10,6,10,6,"jump_in_place_up",16,49);
        owner.AdvanceTo(65,Grass,Exists);
        Assert.AreEqual(new CampaignFieldGrassLandingCover("player",10,6,33),owner.Covers.Single());
    }

    [TestMethod]
    public void FrameSkipsProcessDepartureAndLaterLandingsInSourceOrder()
    {
        var skipped=Land();var stepped=Land();
        foreach(var owner in new[]{skipped,stepped})
        {
            owner.QueueMovement("player",10,6,10,8,"jump_2_down",32,32);
            owner.QueueMovement("npc",2,2,2,3,"jump_down",16,33);
        }
        skipped.AdvanceTo(90,Grass,Exists);
        for(int frame=33;frame<=90;frame++)stepped.AdvanceTo(frame,Grass,Exists);
        CollectionAssert.AreEquivalent(stepped.Covers.ToArray(),skipped.Covers.ToArray());
        Assert.AreEqual(2,skipped.Covers.Length);
    }

    [TestMethod]
    public void MissingActorsCancelPendingLandingsAndTerrainChangesRemoveCovers()
    {
        var owner=Land();owner.QueueMovement("npc",1,1,1,2,"jump_down",16,32);
        owner.AdvanceTo(33,Grass,id=>id!="npc");
        owner.AdvanceTo(48,Grass,Exists);Assert.AreEqual("player",owner.Covers.Single().ActorId);
        owner.AdvanceTo(48,(_,_)=>0,Exists);Assert.AreEqual(0,owner.Covers.Length);
        owner.AdvanceTo(80,Grass,Exists);Assert.AreEqual(0,owner.Covers.Length);
    }

    [TestMethod]
    public void ClearDropsPendingWorkAndCanResumeAtAbsoluteRecapFrame()
    {
        var owner=Land();owner.QueueMovement("player",10,6,10,8,"jump_2_down",32,32);
        owner.Clear();owner.AdvanceTo(500,Grass,Exists);Assert.AreEqual(0,owner.Covers.Length);
        owner.QueueMovement("player",1,1,1,2,"jump_down",16,500);
        owner.AdvanceTo(516,Grass,Exists);Assert.AreEqual(new CampaignFieldGrassLandingCover("player",1,2,0),owner.Covers.Single());
    }

    [TestMethod]
    public void OnlyOrdinaryTallGrassLandingsSpawnAndDifferentActorsDoNotDeduplicate()
    {
        var owner=new CampaignFieldGrassLandingEffects();
        foreach(string id in new[]{"player","npc"})owner.QueueMovement(id,1,1,1,2,"jump_down",16,0);
        owner.QueueMovement("surf",2,2,2,3,"jump_special_down",32,0);
        owner.AdvanceTo(32,(_,_)=>0xd1,Exists);Assert.AreEqual(2,owner.Covers.Length);
        owner.Clear();owner.QueueMovement("player",1,1,1,2,"jump_down",16,0);
        owner.AdvanceTo(16,(_,_)=>0x17,Exists);Assert.AreEqual(0,owner.Covers.Length);
    }

    [TestMethod]
    public void WalkingGrassAnimationDepthAndDepartureMatchOriginalExtractedCallbacks()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile("tests/Reference/field-jump-effect-source-vectors.json",
            "python tests/Reference/generate_field_jump_effect_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Reference/field-jump-effect-source-vectors.json")));
        var rows=source.RootElement.GetProperty("grassWalking").EnumerateArray().ToArray();
        Assert.AreEqual(165,rows.Length);
        for(int scenario=0;scenario<3;scenario++)
        {
            var owner=new CampaignFieldGrassLandingEffects();
            owner.QueueMovement("player",0,-1,0,0,"player_run_down",8,0);
            if(scenario>0)owner.QueueMovement("player",0,0,0,1,"player_run_down",8,8);
            if(scenario==2)owner.QueueMovement("player",0,1,0,0,"player_run_up",8,16);
            var samples=rows.Where(row=>row[0].GetInt32()==scenario).ToArray();Assert.AreEqual(55,samples.Length);
            foreach(var row in samples)
            {
                int age=row[1].GetInt32();owner.AdvanceTo(age+1,Grass,Exists);
                var original=owner.Covers.Where(c=>c.X==0&&c.Y==0&&c.Age==age).ToArray();
                Assert.IsTrue(original.Length<=1,$"source walking grass {row}");
                Assert.AreEqual(row[2].GetInt32(),original.Length==0?-1:original[0].Frame,$"source walking grass {row}");
                if(original.Length>0)
                {
                    int actor=CampaignFieldGrassLandingEffects.Subpriority(3,0,32,0,1);
                    int priority=CampaignFieldGrassLandingEffects.CoverSubpriority(0,0,3,0,[new(8,0,32,actor)],original[0].DepthOffset);
                    Assert.AreEqual(row[5].GetInt32(),priority,$"source walking grass depth {row}");
                }
            }
        }
    }

    [TestMethod]
    public void SpawnCreatesStationaryCoverOnFirstCallbackForEveryPresentActor()
    {
        var owner=new CampaignFieldGrassLandingEffects();
        foreach(string actor in new[]{"player","npc","hidden-npc"})owner.QueueSpawn(actor,10,6,0);
        owner.QueueSpawn("removed-npc",10,6,0);
        owner.AdvanceTo(.99,Grass,id=>id!="removed-npc");Assert.AreEqual(0,owner.Covers.Length);
        owner.AdvanceTo(1,Grass,Exists);Assert.AreEqual(3,owner.Covers.Length);
        foreach(var cover in owner.Covers)
        { Assert.AreEqual(0,cover.Age);Assert.IsFalse(cover.Rustling);Assert.AreEqual(0,cover.Frame);Assert.AreEqual(0,cover.DepthOffset); }
        owner.AdvanceTo(200,Grass,Exists);
        Assert.AreEqual(3,owner.Covers.Length);Assert.IsTrue(owner.Covers.All(c=>c.Age==199&&c.Frame==0));
    }

    [TestMethod]
    public void WalkCreatesDestinationRustleBeforeArrivalAndPreservesSpawnTileUntilCompletion()
    {
        var owner=new CampaignFieldGrassLandingEffects();owner.QueueSpawn("player",0,0,0);
        owner.QueueMovement("player",0,0,0,1,"walk_down",16,0);
        owner.AdvanceTo(0,Grass,Exists);Assert.AreEqual(0,owner.Covers.Length);
        owner.AdvanceTo(1,Grass,Exists);Assert.AreEqual(2,owner.Covers.Length);
        Assert.AreEqual(new CampaignFieldGrassLandingCover("player",0,1,0,true),owner.Covers.Single(c=>c.Rustling));
        owner.AdvanceTo(16,Grass,Exists);Assert.AreEqual(2,owner.Covers.Length);
        owner.AdvanceTo(17,Grass,Exists);Assert.AreEqual(1,owner.Covers.Length);
        Assert.AreEqual(1,owner.Covers.Single().Y);
    }

    [TestMethod]
    public void WalkingSequenceAndDepthKeepCallbackBeforeAnimationBoundary()
    {
        var owner=new CampaignFieldGrassLandingEffects();owner.QueueMovement("npc",0,0,0,1,"walk_down",16,0);
        // field_effect_objects.h: images1,2,3,4,0 each last10 callbacks.
        // field_effect_helpers.c computes depth before sprite.c advances command0.
        int[] ages=[0,9,10,11,19,20,29,30,39,40,50,100];
        int[] images=[1,1,2,2,2,3,3,4,4,0,0,0];
        for(int i=0;i<ages.Length;i++)
        {
            owner.AdvanceTo(1+ages[i],Grass,Exists);var cover=owner.Covers.Single();
            Assert.AreEqual(ages[i],cover.Age);Assert.AreEqual(images[i],cover.Frame,$"age {ages[i]}");
            Assert.AreEqual(ages[i]<=10?4:0,cover.DepthOffset,$"age {ages[i]}");
            Assert.AreEqual(CampaignFieldGrassLandingEffects.Subpriority(3,24,16,0,cover.DepthOffset),
                CampaignFieldGrassLandingEffects.CoverSubpriority(0,1,3,0,[],cover.DepthOffset));
        }
    }

    [TestMethod]
    public void DepartedWalkingRustleFinishesAllFiveCommandsAndRemovesOnCallback51()
    {
        var owner=new CampaignFieldGrassLandingEffects();
        owner.QueueMovement("player",0,0,0,1,"player_run_down",8,0);
        owner.QueueMovement("player",0,1,0,2,"player_run_down",8,8);
        foreach(int frame in new[]{16,17,40,41,50,51})
        {
            owner.AdvanceTo(frame,Grass,Exists);
            Assert.IsTrue(owner.Covers.Any(c=>c.Y==1),$"frame {frame}");
        }
        owner.AdvanceTo(52,Grass,Exists);Assert.IsFalse(owner.Covers.Any(c=>c.Y==1));
        Assert.AreEqual(2,owner.Covers.Single().Y);
    }

    [TestMethod]
    public void StepReturnCreatesNewRustleWithoutResettingOrDeduplicatingOldOwner()
    {
        var owner=new CampaignFieldGrassLandingEffects();
        owner.QueueMovement("player",0,0,0,1,"player_run_down",8,0);
        owner.QueueMovement("player",0,1,0,2,"player_run_down",8,8);
        owner.QueueMovement("player",0,2,0,1,"player_run_up",8,16);
        owner.AdvanceTo(17,Grass,Exists);
        var covers=owner.Covers.Where(c=>c.Y==1).OrderBy(c=>c.Age).ToArray();
        CollectionAssert.AreEqual(new[]{0,16},covers.Select(c=>c.Age).ToArray());
        owner.AdvanceTo(52,Grass,Exists);
        covers=owner.Covers.Where(c=>c.Y==1).ToArray();
        Assert.AreEqual(1,covers.Length);Assert.AreEqual(35,covers[0].Age);
    }

    [TestMethod]
    public void InPlaceJumpLandingDoesNotRestartExistingWalkingAnimation()
    {
        var owner=new CampaignFieldGrassLandingEffects();
        owner.QueueMovement("player",0,0,0,1,"walk_down",16,0);
        owner.QueueMovement("player",0,1,0,1,"jump_in_place_up",16,16);
        owner.AdvanceTo(32,Grass,Exists);
        Assert.AreEqual(new CampaignFieldGrassLandingCover("player",0,1,31,true),owner.Covers.Single());
        Assert.AreEqual(4,owner.Covers.Single().Frame);
    }

    [TestMethod]
    public void OnlyRecognizedDisplacedNormalMovementStartsWalkingRustle()
    {
        string[] admitted=["walk_down","walk_slow_up","walk_slower_left","walk_slowest_right","walk_fast_up","walk_faster_down",
            "player_run_left","player_run_up_slow","ride_water_current_right","slide_down","walk","run","cycle","cycle-stairs","walk-slow","run-slow"];
        string?[] excluded=[null,"teleport_down","face_down","walk_in_place_down","jump_special_down","jump_special_with_effect_up"];
        foreach(string action in admitted)
        {
            var owner=new CampaignFieldGrassLandingEffects();owner.QueueMovement("npc",0,0,0,1,action,16,0);
            owner.AdvanceTo(1,Grass,Exists);Assert.AreEqual(1,owner.Covers.Length,action);
            Assert.IsTrue(owner.Covers.Single().Rustling,action);
        }
        foreach(string? action in excluded)
        {
            var owner=new CampaignFieldGrassLandingEffects();owner.QueueMovement("npc",0,0,0,1,action,32,0);
            owner.AdvanceTo(32,Grass,Exists);Assert.AreEqual(0,owner.Covers.Length,action);
        }
        var inPlace=new CampaignFieldGrassLandingEffects();inPlace.QueueMovement("npc",0,0,0,0,"walk_down",16,0);
        inPlace.AdvanceTo(16,Grass,Exists);Assert.AreEqual(0,inPlace.Covers.Length);
    }

    [TestMethod]
    public void SpawnAndStepUseDestinationTerrainAndReconstructionDropsOldAnimation()
    {
        var owner=new CampaignFieldGrassLandingEffects();owner.QueueSpawn("player",0,0,0);
        owner.QueueMovement("player",0,0,0,1,"walk_down",16,0);
        owner.AdvanceTo(1,(_,y)=>y==1?0xd1:0,Exists);
        Assert.IsTrue(owner.Covers.Single().Rustling);
        owner.Clear();owner.QueueSpawn("player",0,1,100);
        owner.AdvanceTo(100,Grass,Exists);Assert.AreEqual(0,owner.Covers.Length);
        owner.AdvanceTo(101,Grass,Exists);Assert.AreEqual(new CampaignFieldGrassLandingCover("player",0,1,0),owner.Covers.Single());
        owner.AdvanceTo(102,(_,_)=>0x17,Exists);Assert.AreEqual(0,owner.Covers.Length);
    }

    [TestMethod]
    public void FrameSkipsMatchSteppedSpawnWalkingReturnAndJumpDeduplication()
    {
        var skipped=new CampaignFieldGrassLandingEffects();var stepped=new CampaignFieldGrassLandingEffects();
        foreach(var owner in new[]{skipped,stepped})
        {
            owner.QueueSpawn("player",0,0,0);owner.QueueSpawn("npc",0,1,0);
            owner.QueueMovement("player",0,0,0,1,"player_run_down",8,0);
            owner.QueueMovement("player",0,1,0,2,"player_run_down",8,8);
            owner.QueueMovement("player",0,2,0,1,"player_run_up",8,16);
            owner.QueueMovement("player",0,1,0,1,"jump_in_place_up",16,24);
        }
        skipped.AdvanceTo(90,Grass,Exists);
        for(int frame=0;frame<=90;frame++)stepped.AdvanceTo(frame,Grass,Exists);
        CollectionAssert.AreEquivalent(stepped.Covers.ToArray(),skipped.Covers.ToArray());
        Assert.AreEqual(2,skipped.Covers.Length);
    }

    [TestMethod]
    public void SourceCoverDepthIsInFrontAtRestAndBehindOverlappingSouthwardActor()
    {
        int[] priorities=[2,2,2,2,1,2,1,2,1,2,1,2,1,0,0,2];
        for(int i=0;i<priorities.Length;i++)Assert.AreEqual(priorities[i],CampaignFieldGrassLandingEffects.ObjectPriority(i));
        int actor=CampaignFieldGrassLandingEffects.Subpriority(3,0,32,0,1);
        int cover=CampaignFieldGrassLandingEffects.CoverSubpriority(0,0,3,0,[new(8,0,32,actor)]);
        Assert.AreEqual(actor-1,cover);
        Assert.AreEqual(actor+2,CampaignFieldGrassLandingEffects.CoverSubpriority(0,0,3,0,[new(8,1,32,actor)]));
        Assert.AreEqual(cover,CampaignFieldGrassLandingEffects.CoverSubpriority(0,0,3,0,[new(8,-1,32,actor)]));
        Assert.AreEqual(cover,CampaignFieldGrassLandingEffects.CoverSubpriority(0,0,3,0,[new(0,1,32,actor)]));
        Assert.AreEqual(cover,CampaignFieldGrassLandingEffects.CoverSubpriority(0,0,3,0,[new(8,1,32,actor,false)]));
        Assert.AreEqual(cover,CampaignFieldGrassLandingEffects.Subpriority(3,12,8,0,0),"Impact shares resting cover's subpriority.");
    }
}
