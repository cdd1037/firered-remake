using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SnowAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static string Root=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    [TestMethod][DataRow(false)][DataRow(true)]
    public void ExtractedSourceSnowVectorsCoverInitialReverseOrbitAndExitAndExistingWiggle(bool beyond)
    {
        using var resources=new SourceAnimationResources(Root);var source=resources.Resolve(59).Script;
        using var fixture=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/swirling-snow-source-vectors.json")));
        foreach(var row in fixture.RootElement.GetProperty("cases").EnumerateArray().Where(r=>r.GetProperty("beyond").GetBoolean()==beyond))
        {
            int actor=row.GetProperty("actor").GetInt32(),grouped=row.GetProperty("grouped").GetInt32(),y=row.GetProperty("yOffset").GetInt32();var b=Battlers(true);b[(actor^1)^2]=b[(actor^1)^2] with{Visible=false};
            var args=new List<string>{beyond?"gBlizzardIceCrystalSpriteTemplate":"gSwirlingSnowballSpriteTemplate","ANIM_TARGET","40","0",y.ToString(),"0",y.ToString(),"72"};if(beyond)args.AddRange(["6","5",grouped.ToString()]);else args.Add(grouped.ToString());
            var script=source with{Commands=[C("loadspritegfx","ANIM_TAG_ICE_CRYSTALS"),C("createsprite",args.ToArray()),C("waitforvisualfinish"),C("end")],Labels=new Dictionary<string,int>()};
            var runtime=new SourceBattleAnimation(script,new("snow-vector",1,59,new(actor),new(actor^1)),b,true,new Audio());var expected=row.GetProperty("frames").EnumerateArray().Skip(1).Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).Where(r=>r[12]==0).ToArray();int seen=0,ticks=0;
            while(runtime.Active&&ticks++<512)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;Assert.IsTrue(seen<expected.Length);var e=expected[seen++];var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(e[2],e[3]),sprite.Position);Assert.AreEqual(new PixelPoint(e[4],e[5]),sprite.Offset);
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(expected.Length,seen);
        }
    }
    [TestMethod][DataRow(0)][DataRow(1)]
    public void IceSpikeUsesBaseCoordinatesAndSourceSharedXMutation(int grouping)
    {
        using var resources=new SourceAnimationResources(Root);var source=resources.Resolve(196).Script;
        for(int actor=0;actor<4;actor++)
        {
            var b=Battlers(true);int target=actor^1;b[target^2]=b[target^2] with{Visible=false};var script=source with{Commands=[C("loadspritegfx","ANIM_TAG_ICE_SPIKES"),C("createsprite","gIceGroundSpikeSpriteTemplate","ANIM_TARGET","2","12","24",grouping.ToString()),C("waitforvisualfinish"),C("end")]};
            var runtime=new SourceBattleAnimation(script,new("ice-spike",1,196,new(actor),new(target)),b,true,new Audio());SourceAnimationFrame frame=runtime.Tick();for(int tick=0;frame.Sprites.Length==0&&tick<8;tick++)frame=runtime.Tick();
            var p=b[target].Base(true);if(grouping!=0){var partner=b[target^2].Base(true);p=new((p.X+partner.X)/2,(p.Y+partner.Y)/2);}int sign=(actor&1)==0?1:-1;
            Assert.AreEqual(new PixelPoint(p.X+sign*12,p.Y+24),frame.Sprites.Single().Position);Assert.AreEqual(grouping!=0?(short)(sign*12):(short)12,frame.AnimationArguments[0]);
            int ticks=0;while(runtime.Active&&ticks++<64)runtime.Tick();Assert.IsFalse(runtime.Active);
        }
    }
    [TestMethod][DataRow("FALSE")][DataRow("TRUE")][DataRow("-1")][DataRow("2")]
    public void GroupingFlagsUseSourceBooleanOperandsAndRejectOtherValues(string flag)
    {
        using var resources=new SourceAnimationResources(Root);var source=resources.Resolve(59).Script;
        var script=source with{Commands=[C("loadspritegfx","ANIM_TAG_ICE_CRYSTALS"),C("createsprite","gSwirlingSnowballSpriteTemplate","ANIM_TARGET","40","0","0","0","0","72",flag),C("waitforvisualfinish"),C("end")],Labels=new Dictionary<string,int>()};
        if(flag is "-1" or "2"){Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,new("snow-flag",1,59,new(0),new(1)),Battlers(true),true,new Audio()));return;}
        var runtime=new SourceBattleAnimation(script,new("snow-flag",1,59,new(0),new(1)),Battlers(true),true,new Audio());int ticks=0;while(runtime.Active&&ticks++<256)runtime.Tick();Assert.IsFalse(runtime.Active);
    }
    [TestMethod][DataRow(59)][DataRow(196)]
    public void CompleteIceRootsComposeAllPositionsAndRestoreBackground(int move)
    {
        using var resources=new SourceAnimationResources(Root);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("ice-root",1,move,new(actor),new(actor^1)),b,doubles,new Audio(),Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b,surface);int ticks=0;bool background=false,snow=false;
            while(runtime.Active&&ticks++<2048){var frame=runtime.Tick();snow|=frame.Sprites.Any(s=>s.Template=="gSwirlingSnowballSpriteTemplate");background|=frame.MoveBackground?.Id==(move==196?15:(actor&1)==0?9:10);Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(background&&snow);var final=runtime.Snapshot();Assert.IsNull(final.MoveBackground?.Id);Assert.AreEqual(0,final.MoveBackground!.ScrollX);Assert.AreEqual(0,final.MoveBackground.ScrollY);Assert.AreEqual(0,final.Sprites.Length);
        }
    }
}
