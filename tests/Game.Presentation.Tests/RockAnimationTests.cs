using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class RockAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    [TestMethod]
    public void FallingRockKeepsTwoEllipsePhasesAndCallbackHandoffFrames()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int average in new[]{0,1})foreach(int tile in new[]{0,1,2})
        {
            int target=actor^1;var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64){Visible=i!=(target^2)}).ToArray();
            var source=resources.Scripts[88];var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_ROCKS"],1),new("createsprite",["gFallingRockSpriteTemplate","ANIM_TARGET","2","19",tile.ToString(),"10",average.ToString()],2),new("waitforvisualfinish",[],3),new("end",[],4)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
            var runtime=new SourceBattleAnimation(script,new("rock-vector",1,88,new(actor),new(target)),battlers,doubles,new Audio());
            var center=battlers[target].Center(doubles,true);
            if(average!=0){center=battlers[target].Base(doubles);if(doubles){var partner=battlers[target^2].Base(true);center=new((center.X+partner.X)/2,(center.Y+partner.Y)/2);}}
            int observed=0,ticks=0;
            while(runtime.Active&&ticks++<128)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;var rock=frame.Sprites.Single();int index=observed++;
                bool bounce=index>=16;int angle=bounce?(192+4*Math.Min(index-16,31))&255:Math.Min(4*(index+1),60);
                Assert.AreEqual(new PixelPoint(center.X+19+(bounce?10:0),center.Y+14),rock.Position);
                Assert.AreEqual(new PixelPoint((source.SineTable[angle]*(bounce?10:0))>>8,(source.SineTable[angle+64]*(bounce?-24:-70))>>8),rock.Offset);
                Assert.AreEqual(32+16*tile,rock.Tile);
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(49,observed);
        }
    }
    [TestMethod]
    public void TerrainShakeUsesRegisterDelayAndRestoresItsCapturedScroll()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var battlers=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
        var source=resources.Scripts[88];var script=source with{Commands=[new("createsprite",["gShakeMonOrTerrainSpriteTemplate","ANIM_TARGET","2","6","1","15","1"],1),new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("terrain-vector",1,88,new(0),new(1)),battlers,false,new Audio());
        for(int tick=1;tick<=15;tick++)
        {var frame=runtime.Tick();Assert.AreEqual((tick/2)%2==0?0:65530,frame.MoveBackground!.ScrollY);Assert.IsTrue(frame.Sprites.All(s=>s.Asset is null&&!s.Visible));Assert.IsTrue(frame.Offsets.All(p=>p==default));}
        Assert.AreEqual(0,runtime.Tick().MoveBackground!.ScrollY);while(runtime.Active)runtime.Tick();Assert.AreEqual(0,runtime.Snapshot().MoveBackground!.ScrollY);
    }
    [TestMethod]
    public void OverlappingTerrainControllersRestoreTheirOwnNonzeroCapturedRegister()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var battlers=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
        var source=resources.Scripts[88];var script=source with{Commands=[new("createsprite",["gShakeMonOrTerrainSpriteTemplate","ANIM_TARGET","2","6","0","5","1"],1),new("delay",["1"],2),new("createsprite",["gShakeMonOrTerrainSpriteTemplate","ANIM_TARGET","2","3","0","1","1"],3),new("waitforvisualfinish",[],4),new("end",[],5)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("overlap-terrain",1,88,new(0),new(1)),battlers,false,new Audio());
        foreach(int expected in new[]{65530,0,65530,65533,65530,0})Assert.AreEqual(expected,runtime.Tick().MoveBackground!.ScrollY);
        while(runtime.Active)runtime.Tick();Assert.AreEqual(0,runtime.Snapshot().MoveBackground!.ScrollY);
    }
    [TestMethod]
    public void ScrolledTerrainRequiresIndexedSurfaceAndUsesItsRetainedMap()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var resolved=resources.Resolve(88);var battlers=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
        var script=resolved.Script with{Commands=[new("createsprite",["gShakeMonOrTerrainSpriteTemplate","ANIM_TARGET","2","6","1","15","1"],1),new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("terrain-pixels",1,88,new(0),new(1)),battlers,false,new Audio());runtime.Tick();var frame=runtime.Tick();Assert.AreEqual(65530,frame.MoveBackground!.ScrollY);
        var surface=resources.CreateBackgroundSurface(0);var original=new ushort[240*112];surface.Render(original,240,112);
        var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,original));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,new ushort[4096]));
        var flattened=new SourceBattleFrameComposer(assets,battlers);Assert.Throws<InvalidDataException>(()=>flattened.ComposeField(frame));
        var indexed=new SourceBattleFrameComposer(assets,battlers,backgroundSurface:surface);var actual=indexed.ComposeField(frame);Assert.AreEqual(240*112*4,actual.Length);
        var expected=new ushort[240*112];surface.Render(expected,240,112,scrollY:65530);
        for(int pixel=0;pixel<expected.Length;pixel++)
        {ushort color=expected[pixel];Assert.AreEqual((byte)((color&31)*255/31),actual[pixel*4]);Assert.AreEqual((byte)(((color>>5)&31)*255/31),actual[pixel*4+1]);Assert.AreEqual((byte)(((color>>10)&31)*255/31),actual[pixel*4+2]);}
    }
}
