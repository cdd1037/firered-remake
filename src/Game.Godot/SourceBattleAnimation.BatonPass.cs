namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string BatonPassBallTemplate="gBatonPassPokeballSpriteTemplate";
    private bool ValidateBatonPassCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="loadspritegfx"&&a.FirstOrDefault()=="ANIM_TAG_POKEBALL")
        {if(a.Length!=1)Fail(c,"Baton Pass graphics operands differ.");return true;}
        if(c.Op=="playsewithpan"&&a.FirstOrDefault()=="SE_M_BATON_PASS")
        {if(a.Length!=2||SoundPan(a[1]) is < -64 or >63||!_script.SoundEffects.ContainsKey(a[0]))Fail(c,"Baton Pass sound operands differ.");return true;}
        if(c.Op!="createsprite"||a.FirstOrDefault()!=BatonPassBallTemplate)return false;
        if(a.Length!=3||Number(a[1])!=0||Number(a[2])!=2)Fail(c,"Baton Pass source selector/priority differs.");
        if(!_script.SpriteRecipes.TryGetValue(BatonPassBallTemplate,out var r)||r.Asset!="pokeball"||r.Callback!="AnimBatonPassPokeball"||
            r.Width!=16||r.Height!=16||r.Blend||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.Subsprites.Length!=0||
            r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||
            !r.LoopStarts.SequenceEqual([0])||r.FrameLoops.Any(x=>x.Length!=0))Fail(c,"Baton Pass source recipe differs.");
        if(_validatingInvokedProgram)
        {
            var commands=OverlayInvocationCommands();
            if(!IsSourceBattlerPresent(_cue.Actor.Value)||commands.Count(x=>x.Op=="createsprite"&&x.Args[0]==BatonPassBallTemplate)!=1||
                commands.Any(x=>x.Op=="createsprite"&&x.Args[0]!=BatonPassBallTemplate||
                    x.Op=="createvisualtask"&&x.Args[0]!="AnimTask_BlendColorCycle"||
                    x.Op is "visible" or "invisible" or "monbg" or "monbg_static" or "clearmonbg_static" or "fadetobg" or "changebg"))
                Fail(c,"Baton Pass requires one present actor, one ball and its reviewed palette companion.");
        }
        return true;
    }
    private bool TryCreateBatonPassSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=BatonPassBallTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_POKEBALL"))Fail(c,"Baton Pass graphics have not been loaded.");
        _sprites.Add(new BatonPassBall(slot,priority,_script.SpriteRecipes[BatonPassBallTemplate],this));return true;
    }
    private sealed class BatonPassBall:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly int _actor;private readonly PixelPoint _position;private PixelPoint _offset;
        private readonly short[] _d=new short[8];
        public BatonPassBall(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_actor=o._cue.Actor.Value;
            // Source coordinates are picture coordinates, not the actor's current
            // raw x/y or x2/y2. The departing body keeps its existing offsets.
            _position=o._battlers[_actor].Center(o._doubles,true);
            o.PrepareBattlerRotScale(_actor,0);_d[1]=_d[2]=256;_d[0]=1;
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=16,Height=16,VFlip=f.VFlip};}}
        public override void AdvanceAnimation(SourceBattleAnimation o)=>_frames.Step();
        public override void Step(SourceBattleAnimation o)
        {
            switch(_d[0])
            {
                case 1:
                    _d[1]+=96;_d[2]-=26;o.SetBattlerRotScale(_actor,_d[1],_d[2],0);
                    if(++_d[3]==5)_d[0]++;
                    // The source intentionally falls through: both scale writes
                    // and both counter increments occur on these first ticks.
                    goto case 2;
                case 2:
                    _d[1]+=96;_d[2]+=48;o.SetBattlerRotScale(_actor,_d[1],_d[2],0);
                    if(++_d[3]==9)
                    {_d[3]=0;o.SetSourceBattlerSpriteInvisible(_actor,true);o.ResetBattlerRotScale(_actor);_d[0]++;}
                    break;
                case 3:
                    _offset=new(0,unchecked((short)(_offset.Y-6)));
                    if(_position.Y+_offset.Y< -32)Finished=true;
                    break;
            }
        }
    }
}
