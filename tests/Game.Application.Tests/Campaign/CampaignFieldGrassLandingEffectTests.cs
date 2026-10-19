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
                Assert.AreEqual(row[2].GetInt32(),owner.Covers.Length==0?-1:0,$"source grass cover {row}");
                if(owner.Covers.Length>0)Assert.AreEqual(age,owner.Covers.Single().Age);
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
        { owner.AdvanceTo(frame,Grass,Exists);Assert.AreEqual(1,owner.Covers.Length,$"frame {frame}"); }
        owner.AdvanceTo(49,Grass,Exists);Assert.AreEqual(0,owner.Covers.Length);
    }

    [TestMethod]
    public void FastDepartureWaitsForStationaryAnimationEndAndDepartureStaysSticky()
    {
        var owner=Land();owner.QueueMovement("player",10,6,10,7,"player_run_down",8,32);
        owner.AdvanceTo(40,Grass,Exists);Assert.AreEqual(1,owner.Covers.Length);
        // Returning before the END callback cannot clear the source data7 bit.
        owner.QueueMovement("player",10,7,10,6,"walk_up",16,40);
        owner.AdvanceTo(42,Grass,Exists);Assert.AreEqual(1,owner.Covers.Length);
        owner.AdvanceTo(43,Grass,Exists);Assert.AreEqual(0,owner.Covers.Length);
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
