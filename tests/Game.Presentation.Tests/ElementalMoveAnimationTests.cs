using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ElementalMoveAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Cues {get;}=[];
        public List<(int Pan,int Frame)> Pans {get;}=[];
        public bool IsPlaying=>false;
        public void Play(SourceAnimationSound cue)=>Cues.Add(cue);
        public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));
        public void StopAnimationEffects(){}
    }
    private static SourceAnimationScript Script(int move)
    {
        using var hit=SourceHitAnimationCatalog.Read(Path.Combine(TestAssets.Root,"content/generated/battle/animations/hit"));
        return SourceElementalAnimationCatalog.Read(Path.Combine(TestAssets.Root,"content/generated/battle/animations/elemental.json"),hit)[move];
    }
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod]
    [DataRow(7)][DataRow(52)][DataRow(53)]
    public void SourceElementalRootsCompleteForEveryCanonicalActor(int move)
    {
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(Script(move),new("elemental",1,move,new(actor),new(actor^1)),Battlers(doubles),doubles,audio);
            int frames=0;bool visual=false;
            while(runtime.Active&&frames++<1500){var f=runtime.Tick();visual|=f.Sprites.Any(s=>s.Asset=="small-ember");}
            Assert.IsFalse(runtime.Active,$"move {move}, actor {actor}");Assert.IsTrue(visual);Assert.IsTrue(audio.Cues.Count>0);
            var final=runtime.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.AreEqual(0,final.PaletteBlends.Length);
            if(move==53)Assert.IsTrue(audio.Pans.Count>0);
        }
    }
    [TestMethod]
    public void EveryElementalFamilyComposesItsRealGeneratedSpriteSheets()
    {
        string folder=Path.Combine(TestAssets.Root,"content/generated/battle/animations");
        using var hit=SourceHitAnimationCatalog.Read(Path.Combine(folder,"hit"));
        var assets=hit.RootElement.GetProperty("assets").EnumerateArray().Select(a=>a.GetProperty("id").GetString()!).ToDictionary(id=>id,id=>SourceHitAnimationCatalog.Asset(hit,Path.Combine(folder,"hit"),id));
        foreach(var pair in SourceElementalAnimationCatalog.Assets(Path.Combine(folder,"elemental.json")))assets.Add(pair.Key,pair.Value);
        assets.Add("background",new(240,112,new ushort[240*112]));
        foreach(var b in Battlers(true))assets.Add(b.Asset,new(64,64,new ushort[4096]));
        var composer=new SourceBattleFrameComposer(assets,Battlers(true));
        foreach(var script in SourceElementalAnimationCatalog.Read(Path.Combine(folder,"elemental.json"),hit).Values)
        {
            var runtime=new SourceBattleAnimation(script,new("pixels",1,script.MoveId,new(2),new(3)),Battlers(true),true,new Audio());
            int frames=0;while(runtime.Active&&frames++<1500)Assert.AreEqual(240*112*4,composer.ComposeField(runtime.Tick()).Length);
            Assert.IsFalse(runtime.Active,script.Label);
        }
    }
    [TestMethod]
    public void FireSpreadUsesSignedEightBitFractionsAndSourceAnimationLoop()
    {
        var source=Script(7);var sprite=source.Subroutines["FireSpreadEffect"][1];
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_EMBER"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("spread",1,7,new(0),new(1)),Battlers(false),false,new Audio());
        int seen=0;bool wrapped=false;int last=-1;
        while(runtime.Active)
        {
            var f=runtime.Tick();if(f.Sprites.Length==0)continue;var s=f.Sprites.Single();seen++;
            Assert.AreEqual(new PixelPoint((-192*seen)>>8,(240*seen)>>8),s.Offset);
            wrapped|=last>s.Tile;last=s.Tile;
            if(seen==40)break;
        }
        Assert.AreEqual(40,seen);Assert.IsTrue(wrapped);
    }
    [TestMethod]
    public void EmberProjectileUsesSourceDirectionBitsAndCoordinateModes()
    {
        var source=Script(52);var sprite=source.Commands.First(c=>c.Op=="createsprite");
        var script=source with{Commands=[source.Commands[0],sprite,new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        foreach(int actor in new[]{0,1})
        {
            var runtime=new SourceBattleAnimation(script,new("ember",1,52,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            for(int tick=0;tick<3;tick++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            var first=runtime.Tick().Sprites.Single();var start=Battlers(false)[actor].Center(false,true);int sign=actor==0?1:-1;
            Assert.AreEqual(new PixelPoint(start.X+sign*20,start.Y),first.Position);
            Assert.AreEqual(0,first.Tile);Assert.AreEqual("small-ember",first.Asset);
            int count=0;while(runtime.Active&&count++<100)runtime.Tick();Assert.IsFalse(runtime.Active);
        }
    }
    [TestMethod]
    public void ElementalUnsupportedCallbackAndUnloadedGraphicsFailExplicitly()
    {
        var source=Script(52);var commands=source.Commands.ToArray();int index=Array.FindIndex(commands,c=>c.Op=="createsprite");
        commands[index]=commands[index] with{Args=["gUnsupportedElementalSprite",..commands[index].Args.Skip(1)]};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=commands},new("b",1,52,new(0),new(1)),Battlers(false),false,new Audio()));
        var runtime=new SourceBattleAnimation(source with{Commands=source.Commands.Skip(1).ToArray()},new("b",1,52,new(0),new(1)),Battlers(false),false,new Audio());Assert.Throws<SourceAnimationException>(()=>runtime.Tick());
    }
}
