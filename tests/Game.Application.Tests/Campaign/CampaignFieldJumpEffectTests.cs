using System.Text.Json;
using Game.Application;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignFieldJumpEffectTests
{
    private static CampaignFieldJumpCue Cue(string action,double elapsed,int from=0,int to=2)=>
        new("actor","map.any",from,4,to,4,action,CampaignFieldJumpEffects.Duration(action)??32,elapsed);

    [TestMethod]
    public void TerrainAndDustAgreeWithOriginalExtractedCallbacks()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile("tests/Reference/field-jump-effect-source-vectors.json", "python tests/Reference/generate_field_jump_effect_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Reference/field-jump-effect-source-vectors.json")));
        var pairs=source.RootElement.GetProperty("shadowPairs").EnumerateArray().ToArray();
        var landings=source.RootElement.GetProperty("landings").EnumerateArray().ToArray();
        var dust=source.RootElement.GetProperty("dust").EnumerateArray().ToArray();
        Assert.AreEqual(480,pairs.Length);Assert.AreEqual(240,landings.Length);Assert.AreEqual(27,dust.Length);
        foreach(var row in pairs)
            Assert.AreEqual(row[2].GetInt32()!=0,CampaignFieldJumpEffects.ShadowSurfaceAllowed(row[0].GetInt32(),row[1].GetInt32()),$"source shadow {row}");
        foreach(var row in landings)
            Assert.AreEqual((CampaignFieldJumpLanding)row[1].GetInt32(),CampaignFieldJumpEffects.LandingKind(row[0].GetInt32()),$"source landing {row}");
        foreach(var row in dust)
            Assert.AreEqual(row[1].GetInt32(),CampaignFieldJumpEffects.DustFrame(row[0].GetInt32()),$"source dust {row}");
    }

    [TestMethod]
    public void OrdinaryActionAdmissionAndDurationsAreExact()
    {
        foreach(string direction in new[]{"down","up","left","right"})
        {
            Assert.AreEqual(16,CampaignFieldJumpEffects.Duration("jump_"+direction));
            Assert.AreEqual(32,CampaignFieldJumpEffects.Duration("jump_2_"+direction));
            Assert.AreEqual(16,CampaignFieldJumpEffects.Duration("jump_in_place_"+direction));
            Assert.IsFalse(CampaignFieldJumpEffects.IsOrdinaryJump("jump_special_"+direction));
            Assert.IsFalse(CampaignFieldJumpEffects.IsOrdinaryJump("jump_special_with_effect_"+direction));
            Assert.IsNull(CampaignFieldJumpEffects.Duration("jump_2_"+direction+"_unknown"));
        }
        foreach(string reversal in new[]{"down_up","up_down","left_right","right_left"})
            Assert.AreEqual(16,CampaignFieldJumpEffects.Duration("jump_in_place_"+reversal));
        Assert.IsNull(CampaignFieldJumpEffects.Duration(null));
        Assert.IsFalse(CampaignFieldJumpEffects.IsOrdinaryJump("walk_down"));
    }

    [TestMethod]
    public void ShadowTracksCallbackLifetimeRatherThanArcHeight()
    {
        foreach(string action in new[]{"jump_right","jump_2_right","jump_in_place_right","jump_in_place_right_left"})
        {
            int duration=CampaignFieldJumpEffects.Duration(action)!.Value;
            for(int frame=0;frame<=duration+1;frame++)
                Assert.AreEqual(frame>0&&frame<duration,CampaignFieldJumpEffects.ShadowVisible(Cue(action,frame),(_,_)=>0),$"{action} frame {frame}");
            Assert.AreEqual(0,CampaignFieldMovementTiming.Sample(action,duration,duration-1).DrawYOffset);
            Assert.IsTrue(CampaignFieldJumpEffects.ShadowVisible(Cue(action,duration-1),(_,_)=>0));
            Assert.IsFalse(CampaignFieldJumpEffects.ShadowVisible(Cue(action,.99),(_,_)=>0));
        }
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_special_right",10),(_,_)=>0));
    }

    [TestMethod]
    public void FarJumpUsesSourceLogicalTilesAndNeverRestartsDestroyedShadow()
    {
        // A grass destination is not current until the second logical shift at callback 16.
        int? DestinationGrass(int x,int y)=>x==2?0x02:0;
        Assert.IsTrue(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_2_right",15),DestinationGrass));
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_2_right",16),DestinationGrass));
        // The second half would individually allow previous grass, but the sprite is gone.
        int? IntermediateGrass(int x,int y)=>x==1?0x02:0;
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_2_right",15),IntermediateGrass));
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_2_right",16),IntermediateGrass));
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_2_right",31),IntermediateGrass));
        // Grass at the starting tile alone is permitted, unlike reflective/water starts.
        Assert.IsTrue(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_2_right",1),(x,_)=>x==0?0x02:0));
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_2_right",31),(x,_)=>x==0?0x23:0));
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_2_right",1),(_,_)=>null));
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowVisible(Cue("jump_right",1,0,1),IntermediateGrass));
    }

    [TestMethod]
    public void ShadowAndLandingTerrainSelectorsDoNotConflateGrassWaterAndIce()
    {
        foreach(int behavior in new[]{0x02,0xd1})
        {
            Assert.IsFalse(CampaignFieldJumpEffects.ShadowSurfaceAllowed(0,behavior));
            Assert.IsTrue(CampaignFieldJumpEffects.ShadowSurfaceAllowed(behavior,0));
            Assert.AreEqual(CampaignFieldJumpLanding.TallGrass,CampaignFieldJumpEffects.LandingKind(behavior));
        }
        foreach(int behavior in new[]{0x10,0x11,0x12,0x13,0x15,0x1a,0x1b,0x50,0x51,0x52,0x53})
        {
            Assert.IsFalse(CampaignFieldJumpEffects.ShadowSurfaceAllowed(0,behavior));
            Assert.IsFalse(CampaignFieldJumpEffects.ShadowSurfaceAllowed(behavior,0));
            Assert.AreEqual(CampaignFieldJumpLanding.DeepWater,CampaignFieldJumpEffects.LandingKind(behavior));
        }
        Assert.AreEqual(CampaignFieldJumpLanding.ShallowWater,CampaignFieldJumpEffects.LandingKind(0x16));
        Assert.AreEqual(CampaignFieldJumpLanding.ShallowWater,CampaignFieldJumpEffects.LandingKind(0x17));
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowSurfaceAllowed(0,0x16));
        Assert.IsTrue(CampaignFieldJumpEffects.ShadowSurfaceAllowed(0,0x17));
        Assert.IsFalse(CampaignFieldJumpEffects.ShadowSurfaceAllowed(0,0x23));
        Assert.AreEqual(CampaignFieldJumpLanding.Dust,CampaignFieldJumpEffects.LandingKind(0x23));
        Assert.AreEqual(CampaignFieldJumpLanding.None,CampaignFieldJumpEffects.LandingKind(null));
        Assert.AreEqual(CampaignFieldJumpLanding.Dust,CampaignFieldJumpEffects.LandingKind(0));
    }

    [TestMethod]
    public void DustStartsAtFinalMovementCallbackAndRetainsEndCommandFrame()
    {
        for(int age=-1;age<=26;age++)
        {
            int expected=age<0||age>=25?-1:age<8?0:age<16?1:2;
            Assert.AreEqual(expected,CampaignFieldJumpEffects.DustFrame(age));
            Assert.AreEqual(expected,CampaignFieldJumpEffects.DustFrame(Cue("jump_2_right",32+age),0));
            Assert.AreEqual(expected,CampaignFieldJumpEffects.DustFrame(Cue("jump_right",16+age,0,1),0));
        }
        Assert.AreEqual(2,CampaignFieldJumpEffects.DustFrame(24.99));
        Assert.AreEqual(-1,CampaignFieldJumpEffects.DustFrame(25));
        foreach(int behavior in new[]{0x02,0xd1,0x10,0x16,0x17,0x50})
            Assert.AreEqual(-1,CampaignFieldJumpEffects.DustFrame(Cue("jump_2_right",32),behavior));
        Assert.AreEqual(-1,CampaignFieldJumpEffects.DustFrame(Cue("jump_2_right",32),0,true));
        Assert.AreEqual(-1,CampaignFieldJumpEffects.DustFrame(Cue("jump_2_right",32),null));
        Assert.AreEqual(-1,CampaignFieldJumpEffects.DustFrame(Cue("jump_special_with_effect_right",32),0));
    }

    [TestMethod]
    public void SourceShadowSizesUseGroundAnchorIndependentOfActorHeight()
    {
        Assert.AreEqual(new CampaignFieldShadowGeometry(8,8,8,12),CampaignFieldJumpEffects.ShadowGeometry(0));
        Assert.AreEqual(new CampaignFieldShadowGeometry(16,8,8,12),CampaignFieldJumpEffects.ShadowGeometry(1));
        Assert.AreEqual(new CampaignFieldShadowGeometry(32,8,8,12),CampaignFieldJumpEffects.ShadowGeometry(2));
        Assert.AreEqual(new CampaignFieldShadowGeometry(64,32,8,0),CampaignFieldJumpEffects.ShadowGeometry(3));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>CampaignFieldJumpEffects.ShadowGeometry(4));
    }
}
