using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class AeroblastAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static string Root=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    [TestMethod]
    public void ReachableSourceTagResolvesIndexedPaletteAndMissingTagFailsClosed()
    {
        using var resources=new SourceAnimationResources(Root);var resolved=resources.Resolve(177);var material=resolved.Assets["air-wave-2"];int tag=resolved.Script.SpriteTags["ANIM_TAG_AIR_WAVE_2"];Assert.AreEqual(tag,material.PaletteTag);CollectionAssert.AreEqual(material.Palette,resolved.Script.SpritePalettes[tag]);
        using var pack=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root,"flying.json")));var row=pack.RootElement.GetProperty("indexedAssets").EnumerateArray().Single(r=>r.GetProperty("id").GetString()=="air-wave-2");Assert.ThrowsExactly<InvalidDataException>(()=>SourceIndexedSpriteAssets.Read(Root,row,new Dictionary<string,int>()));
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void CrescentSourceFrameSeekAndLinearHandoffRetainAllFourFlipPhases(int selected)
    {
        using var resources=new SourceAnimationResources(Root);var source=resources.Resolve(177).Script;
        for(int actor=0;actor<4;actor++)
        {
            var b=Battlers(true);int target=actor^1,sign=(actor&1)==0?1:-1;var start=b[actor].Center(true,true);var end=b[target].Center(true,true);var origin=new PixelPoint(start.X+sign*14,start.Y-sign*12);int dx=end.X-origin.X,dy=end.Y-sign*12-origin.Y;int vx=((Math.Abs(dx)<<8)/15&~1)|(dx<0?1:0),vy=((Math.Abs(dy)<<8)/15&~1)|(dy<0?1:0);
            var script=source with{Commands=[C("loadspritegfx","ANIM_TAG_AIR_WAVE_2"),C("createsprite","gAirWaveCrescentSpriteTemplate","ANIM_ATTACKER","2","14","-12","0","-12","15",selected.ToString(),"0"),C("waitforvisualfinish"),C("end")]};
            var runtime=new SourceBattleAnimation(script,new("air-vector",1,177,new(actor),new(target)),b,true,new Audio());int seen=0,ticks=0;
            while(runtime.Active&&ticks++<64)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;var sprite=frame.Sprites.Single();int updates=Math.Min(seen+1,15),phase=(selected+seen/3)&3;
                Assert.AreEqual(origin,sprite.Position);Assert.AreEqual(new PixelPoint((vx*updates>>8)*(dx<0?-1:1),(vy*updates>>8)*(dy<0?-1:1)),sprite.Offset);Assert.AreEqual((phase&1)!=0,sprite.HFlip);Assert.AreEqual((phase&2)!=0,sprite.VFlip);CollectionAssert.AreEqual(new short[]{(short)(sign*14),(short)(-sign*12),0,(short)(-sign*12),15,(short)selected,0},frame.AnimationArguments.Take(7).ToArray());seen++;
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(16,seen);
        }
    }
    [TestMethod]
    public void CrescentAverageUsesBothCanonicalTargetPositionsEvenWhenPartnerIsHidden()
    {
        using var resources=new SourceAnimationResources(Root);var source=resources.Resolve(177).Script;var b=Battlers(true);b[3]=b[3] with{Visible=false};
        var commands=new[]{C("loadspritegfx","ANIM_TAG_AIR_WAVE_2"),C("createsprite","gAirWaveCrescentSpriteTemplate","ANIM_ATTACKER","2","14","-12","0","-12","15","2","1"),C("waitforvisualfinish"),C("end")};
        var runtime=new SourceBattleAnimation(source with{Commands=commands},new("air-average",1,177,new(0),new(1)),b,true,new Audio());SourceAnimationFrame frame=runtime.Tick();for(int tick=0;frame.Sprites.Length==0&&tick<8;tick++)frame=runtime.Tick();Assert.AreEqual(new PixelPoint(46,84),frame.Sprites.Single().Position);Assert.AreEqual(new PixelPoint(8,-3),frame.Sprites.Single().Offset);while(runtime.Active)runtime.Tick();
        commands[1]=C("createsprite","gAirWaveCrescentSpriteTemplate","ANIM_ATTACKER","2","14","-12","0","-12","15","4","1");Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=commands},new("air-control-index",1,177,new(0),new(1)),b,true,new Audio()));
    }
    [TestMethod]
    public void AeroblastComposesAllActorPositionsAndRestoresSkyBackground()
    {
        using var resources=new SourceAnimationResources(Root);var resolved=resources.Resolve(177);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("air-root",1,177,new(actor),new(actor^1)),b,doubles,new Audio(),Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray());var composer=new SourceBattleFrameComposer(assets,b,resolved.StatMask,resolved.Metallic,surface,resolved.Backgrounds);int ticks=0;bool sky=false,wave=false;
            while(runtime.Active&&ticks++<2048){var frame=runtime.Tick();wave|=frame.Sprites.Any(s=>s.Template=="gAirWaveCrescentSpriteTemplate");sky|=frame.MoveBackground?.Id==18;Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(sky&&wave);var final=runtime.Snapshot();Assert.IsNull(final.MoveBackground?.Id);Assert.AreEqual(0,final.MoveBackground!.ScrollX);Assert.AreEqual(0,final.MoveBackground.ScrollY);Assert.AreEqual(0,final.Sprites.Length);
        }
    }
}
