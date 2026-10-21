namespace Game.Presentation.BattleAnimations;

// These source predicates consume gAnimFriendship directly, not calculated power.
internal static class SourceFriendshipAnimationRules
{
    public static short ReturnLevel(byte friendship)=>friendship>200?(short)3:friendship>91?(short)2:friendship>60?(short)1:(short)0;
    public static short FrustrationLevel(byte friendship)=>friendship<=30?(short)0:friendship<=100?(short)1:friendship<=200?(short)2:(short)3;
}

public sealed partial class SourceBattleAnimation
{
    private const string MovementWavesTemplate="gMovementWavesSpriteTemplate",LickTemplate="gLickSpriteTemplate",WeakAngerTemplate="gWeakFrustrationAngerMarkSpriteTemplate";
    private static readonly SourceTaskAffineCommand[] StrongFrustrationProgram=[new("frame",0,-15,0,7),new("frame",0,15,0,7),new("loop",Count:2),new("end")];
    private bool ValidateFriendshipContactCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="createvisualtask"&&name is "AnimTask_GetReturnPowerLevel" or "AnimTask_GetFrustrationPowerLevel" or "AnimTask_StrongFrustrationGrowAndShrink")
        {
            if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Friendship callback takes only source task priority.");
            if(name=="AnimTask_StrongFrustrationGrowAndShrink")
            {if(!_script.TaskAffinePrograms.TryGetValue("sStrongFrustrationAffineAnimCmds",out var program)||!program.SequenceEqual(StrongFrustrationProgram))Fail(c,"Strong Frustration source affine program differs.");}
            else if(_validatingInvokedProgram&&_cue.Friendship is null)Fail(c,"Friendship predicate requires the committed actor friendship.");
            return true;
        }
        if(c.Op!="createsprite"||name is not (MovementWavesTemplate or LickTemplate or WeakAngerTemplate))return false;
        if(a.Length!=(name==MovementWavesTemplate?6:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Contact sprite argument count or priority differs.");
        if(name==MovementWavesTemplate)
        {if(Number(a[3]) is <0 or >1||Number(a[4]) is <0 or >1||Number(a[5]) is <0 or >32767)Fail(c,"Movement waves require a participant, direction and bounded repeat count.");}
        else if(a.Skip(3).Any(v=>Number(v) is < -32768 or >32767))Fail(c,"Contact sprite coordinate width differs.");
        ValidateFriendshipContactRecipe(c,name);return true;
    }
    private void ValidateFriendshipContactRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Contact sprite recipe is missing.");r=_script.SpriteRecipes[name];
        var expected=name switch{MovementWavesTemplate=>("movement-waves","AnimMovementWaves",32,32),LickTemplate=>("lick","AnimLick",16,32),_=>("anger","AnimWeakFrustrationAngerMark",16,16)};
        if(r.Asset!=expected.Item1||r.Callback!=expected.Item2||r.Width!=expected.Item3||r.Height!=expected.Item4||r.Blend||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.FrameLoops.Any(x=>x.Length!=0))Fail(c,"Contact sprite source shape or callback differs.");
        SourceSpriteAnimFrame[][] frames=name switch
        {
            MovementWavesTemplate=>[[new(0,8,false,false),new(16,8,false,false),new(32,8,false,false),new(16,8,false,false)],[new(16,8,true,false),new(32,8,true,false),new(16,8,true,false),new(0,8,true,false)]],
            LickTemplate=>[[new(0,2,false,false),new(8,2,false,false),new(16,2,false,false),new(24,2,false,false),new(32,2,false,false)]],
            _=>[[new(0,1,false,false)]]
        };
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(name==WeakAngerTemplate?0:-1,frames.Length)))Fail(c,"Contact sprite source frame-table shape differs.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Contact sprite source frames differ.");
        string tag="ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_');
        if(!_script.SpriteTags.TryGetValue(tag,out int paletteTag)||!_script.SpritePalettes.TryGetValue(paletteTag,out var palette)||palette.Length!=16)Fail(c,"Contact sprite indexed palette dependency is missing.");
    }
    private bool TryCreateFriendshipContactTask(SourceAnimationCommand c)
    {
        switch(c.Args[0])
        {
            case "AnimTask_GetReturnPowerLevel":_animationArgs[7]=SourceFriendshipAnimationRules.ReturnLevel(_cue.Friendship!.Value);return true;
            case "AnimTask_GetFrustrationPowerLevel":_animationArgs[7]=SourceFriendshipAnimationRules.FrustrationLevel(_cue.Friendship!.Value);return true;
            case "AnimTask_StrongFrustrationGrowAndShrink":_tasks.Add(new TaskAffinePlayback(this,_cue.Actor,_script.TaskAffinePrograms["sStrongFrustrationAffineAnimCmds"]));return true;
            default:return false;
        }
    }
    private bool TryCreateFriendshipContactSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (MovementWavesTemplate or LickTemplate or WeakAngerTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Contact sprite graphics have not been loaded.");
        var sprite=new FriendshipContactSprite(slot,priority,r,this);if(!sprite.Finished)_sprites.Add(sprite);return true;
    }
    private sealed class FriendshipContactSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;private PixelPoint _offset;
        private short _state,_timer,_cycles,_xFraction,_yFraction;private readonly int _animation,_direction;private bool _invisible,_restart;
        public FriendshipContactSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;var a=owner._animationArgs;_direction=owner._cue.Actor.IsPlayer?1:-1;
            if(recipe.Template==MovementWavesTemplate)
            {
                var center=owner._battlers[(a[0]==0?owner._cue.Actor:owner._cue.Target).Value].Center(owner._doubles,true);
                _position=new(center.X+(a[1]==0?32:-32),center.Y);_animation=a[1];_cycles=a[2];Finished=_cycles==0;
            }
            else
            {
                var center=recipe.Template==LickTemplate?owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true):owner._battlers[owner._cue.Actor.Value].Base(owner._doubles);
                _position=new(unchecked((short)(center.X+owner.InitialXDirection()*a[0])),unchecked((short)(center.Y+a[1])));
                if(recipe.Template==WeakAngerTemplate)_state=1; // Initial callback executes during CreateSpriteAndAnimate.
            }
            _frames=new(recipe,_animation);
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!_invisible,_frames.Frame.HFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_frames.Frame.VFlip};
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {if(_restart)_restart=false;else _frames.Step();}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_recipe.Template==MovementWavesTemplate)
            {
                if(_frames.Ended){if(--_cycles==0)Finished=true;else{_frames=new(_recipe,_animation);_restart=true;}}return;
            }
            if(_recipe.Template==WeakAngerTemplate)
            {
                if(_state++>20)
                {
                    _xFraction=unchecked((short)(_xFraction+160));_yFraction=unchecked((short)(_yFraction+128));
                    _offset=new(_direction*(_xFraction>>8),unchecked((short)(_offset.Y+(_yFraction>>8))));if(_offset.Y>64)Finished=true;
                }
                return;
            }
            if(!_frames.Ended)return;
            // Source sets invisible first on every ended-frame callback; its XOR
            // therefore gives one-frame pulses, rather than a persistent toggle.
            _invisible=true;
            if(_state>=2){Finished=true;return;}
            if(_timer==(_state==0?2:4)){_invisible=!_invisible;_timer=0;if(++_cycles==5){_cycles=0;_state++;}}
            else _timer++;
        }
    }
}
