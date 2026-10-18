namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string SuperpowerOrb="gSuperpowerOrbSpriteTemplate",SuperpowerRock="gSuperpowerRockSpriteTemplate",SuperpowerFireball="gSuperpowerFireballSpriteTemplate";
    private bool ValidateSuperpowerCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string n=a.FirstOrDefault()??"";
        if(c.Op=="loadspritegfx"&&n is "ANIM_TAG_METEOR" or "ANIM_TAG_FLAT_ROCK")
        {if(a.Length!=1||!_script.SpriteTags.ContainsKey(n))Fail(c,"Superpower graphics tag is missing.");return true;}
        if(c.Op!="createsprite"||n is not (SuperpowerOrb or SuperpowerRock or SuperpowerFireball))return false;
        bool rock=n==SuperpowerRock;
        if(a.Length!=(rock?7:4)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>AnimationArgument(v) is <short.MinValue or >short.MaxValue))Fail(c,"Superpower source sprite operands differ.");
        if(rock?(Number(a[4]) is <1 or >32767||Number(a[5]) is <0 or >4||Number(a[6]) is <0 or >32767):Number(a[3]) is <0 or >1)Fail(c,"Superpower bounded rock trajectory or participant differs.");
        ValidateSuperpowerRecipe(c,n);
        if(_validatingInvokedProgram)
        {
            if(!IsSourceBattlerPresent(_cue.Actor.Value)||!IsSourceBattlerPresent(_cue.Target.Value)||_cue.Actor==_cue.Target)Fail(c,"Superpower needs distinct present source participants.");
            var closure=OverlayInvocationCommands();
            if(closure.Any(x=>x.Op=="createsprite"&&x.Args[0] is not (SuperpowerOrb or SuperpowerRock or SuperpowerFireball or "gShakeMonOrTerrainSpriteTemplate")||x.Op=="createvisualtask"&&x.Args[0] is not ("AnimTask_ShakeMon2" or "SoundTask_PlaySE2WithPanning")))Fail(c,"Superpower requires its reviewed sprite, shake and sound closure.");
            int sprites=MaximumInvocationCost(x=>x.Op=="createsprite"?1:0),matrices=MaximumInvocationCost(x=>x.Op=="createsprite"&&x.Args[0]==SuperpowerOrb?1:0);
            int tasks=1+MaximumInvocationCost(x=>x.Op=="monbg"?(_doubles?2:1):x.Op is "clearmonbg" or "createvisualtask" or "createsoundtask" or "waitplaysewithpan" or "loopsewithpan" or "panse" or "panse_adjustall" or "panse_adjustnone"?1:0);
            if(22+sprites+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>64||4+matrices+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32||tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Superpower invocation exceeds native object, matrix or task capacity.");
        }
        return true;
    }
    private void ValidateSuperpowerRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Superpower source recipe is missing.");return;}
        bool orb=name==SuperpowerOrb,rock=name==SuperpowerRock;string asset=orb?"circle-of-light":rock?"flat-rock":"meteor";string callback=orb?"AnimSuperpowerOrb":rock?"AnimSuperpowerRock":"AnimSuperpowerFireball";int size=rock?16:64;
        if(r.Asset!=asset||r.Callback!=callback||r.Width!=size||r.Height!=size||r.Blend!=orb||r.AffineDoubleSize!=orb||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||!r.LoopStarts.SequenceEqual([0])||r.FrameLoops.Any(x=>x.Length!=0)||r.AffineAnimations.Length!=(orb?1:0))Fail(c,"Superpower source shape or static frame differs.");
        SourceSpriteAffineCommand[] expected=[new("frame",32,32,0,0,0),new("frame",4,4,0,64,0),new("frame",-6,-6,0,8,0),new("frame",6,6,0,8,0),new("jump",0,0,0,0,2)];
        if(orb&&!r.AffineAnimations[0].SequenceEqual(expected))Fail(c,"Superpower orb affine program differs.");
    }
    private bool TryCreateSuperpowerSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (SuperpowerOrb or SuperpowerRock or SuperpowerFireball))return false;
        var recipe=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Superpower source graphics are not loaded.");
        _sprites.Add(new SuperpowerSprite(slot,priority,recipe,Resolve(c.Args[1]),this));return true;
    }
    private sealed class SuperpowerSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;private readonly int _target,_priority,_tile;private readonly bool _flip;
        private PixelPoint _position,_offset;private PhysicalLinear? _linear;private short _remaining,_velocity,_x,_y;private uint _fixedY;private int _phase,_age;
        public SuperpowerSprite(int slot,int priority,SourceSpriteRecipe recipe,AnimationSlot initial,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=o._animationArgs;_position=o._battlers[initial.Value].Center(o._doubles,true);_priority=2;
            if(recipe.Template==SuperpowerRock){_position=new(a[0],120);_remaining=a[3];_velocity=a[1];_tile=a[2]*4;_fixedY=120u<<8;return;}
            bool actor=a[0]==0;int source=(actor?o._cue.Actor:o._cue.Target).Value;_target=(actor?o._cue.Target:o._cue.Actor).Value;
            if(actor)_position=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);
            _priority=o.BattlerBackgroundPriority(source);
            if(recipe.Template==SuperpowerOrb)_affine=new(recipe.AffineAnimations[0]);
            else{_flip=(_target&1)==0;BeginTranslation(o);}
        }
        private void BeginTranslation(SourceBattleAnimation o)
        {var end=o._battlers[_target].Center(o._doubles,true);_linear=new(end.X-_position.X,end.Y-_position.Y,16);_phase=1;}
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==SuperpowerRock)
            {
                if(_phase==0)
                {
                    if(_remaining!=0){_fixedY=unchecked(_fixedY-(uint)_velocity);_position=new(_position.X,unchecked((short)(unchecked((int)_fixedY)>>8)));if(_position.Y< -8)Finished=true;else _remaining--;return;}
                    var from=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);var to=o._battlers[o._cue.Target.Value].Center(o._doubles,true);
                    _velocity=unchecked((short)(to.X-from.X));_remaining=unchecked((short)(to.Y-from.Y));_x=unchecked((short)(_position.X<<4));_y=unchecked((short)(_position.Y<<4));_phase=1;return;
                }
                _x=unchecked((short)(_x+_velocity));_y=unchecked((short)(_y+_remaining));_position=new(_x>>4,_y>>4);
                if(unchecked((ushort)(_position.X+8))>256||_position.Y is < -8 or >120)Finished=true;return;
            }
            if(_phase==0){if(++_age==180){o.ClearAlphaBlend();BeginTranslation(o);}return;}
            if(_phase==2){Finished=true;return;}
            if(_linear!.Step(out var offset))_phase=2;else _offset=offset;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,f.Tile+_tile,true,_flip,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_flip,OamPriority=_priority,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
}
