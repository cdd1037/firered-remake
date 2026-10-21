namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string PainSplitProjectile="gPainSplitProjectileSpriteTemplate",PainSplitMovement="AnimTask_PainSplitMovement";
    private bool ValidatePainSplitCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        bool sprite=c.Op=="createsprite"&&name==PainSplitProjectile,task=c.Op=="createvisualtask"&&name==PainSplitMovement;
        if(!sprite&&!task)return false;
        if(a.Length!=(sprite?6:4)||Number(a[sprite?2:1]) is <0 or >255||sprite&&(Number(a[1]) is <0 or >1||Number(a[2])>127)||a.Skip(sprite?3:2).Any(x=>AnimationArgument(x) is <short.MinValue or >short.MaxValue))Fail(c,"Pain Split source command width differs.");
        if(sprite&&Number(a[5]) is <0 or >1||task&&(Number(a[2]) is <0 or >1||Number(a[3]) is <0 or >2))Fail(c,"Pain Split source participant or mode differs.");
        if(!_script.SpriteRecipes.TryGetValue(PainSplitProjectile,out var r)){Fail(c,"Pain Split projectile recipe is missing.");return false;}
        if(r.Asset!="pain-split"||r.Callback!="AnimPainSplitProjectile"||r.Width!=16||r.Height!=16||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.AffineAnimations.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,5,false,false),new(4,9,false,false),new(8,5,false,false)])||!r.LoopStarts.SequenceEqual([-1])||r.FrameLoops.Length>1||r.FrameLoops.Any(x=>x.Length!=0))Fail(c,"Pain Split finite source frame program differs.");
        if(!_validatingInvokedProgram)return true;
        if(!IsSourceBattlerPresent(_cue.Actor.Value)||!IsSourceBattlerPresent(_cue.Target.Value))Fail(c,"Pain Split requires present participants.");
        ValidateReactionGeometry(c,_battlers[_cue.Actor.Value]);ValidateReactionGeometry(c,_battlers[_cue.Target.Value]);
        var commands=OverlayInvocationCommands();
        if(commands.Any(x=>x.Op=="createsprite"&&x.Args[0]!=PainSplitProjectile||x.Op=="createvisualtask"&&x.Args[0]!=PainSplitMovement||x.Op is not ("loadspritegfx" or "unloadspritegfx" or "createsprite" or "createvisualtask" or "delay" or "playsewithpan" or "playse" or "waitforvisualfinish" or "end" or "call" or "return" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "panse_adjustall" or "panse_adjustnone" or "createsoundtask" or "waitsound" or "setpan")))Fail(c,"Pain Split requires its reviewed non-emitting projectile and battler closure.");
        var changing=new HashSet<int>();
        foreach(var command in commands)
        {
            if(command.Op=="waitforvisualfinish")changing.Clear();
            if(command.Op=="createvisualtask"&&command.Args[0]==PainSplitMovement&&!changing.Add((Number(command.Args[2])==0?_cue.Actor:_cue.Target).Value))Fail(c,"Pain Split battler transformations must drain before another owner starts.");
        }
        int births=MaximumInvocationCost(x=>x.Op=="createsprite"?1:0),tasks=1+MaximumInvocationCost(x=>x.Op is "createvisualtask" or "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "panse_adjustall" or "panse_adjustnone"?1:0);
        if(22+births+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>64||4+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32||tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Pain Split complete path exceeds native object, matrix or task capacity.");
        return true;
    }
    private bool TryCreatePainSplitSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=PainSplitProjectile)return false;
        if(!_loaded.Contains("ANIM_TAG_PAIN_SPLIT"))Fail(c,"Pain Split graphics are not loaded.");
        _sprites.Add(new PainSplitSprite(slot,priority,_script.SpriteRecipes[PainSplitProjectile],this));return true;
    }
    private bool TryCreatePainSplitTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!=PainSplitMovement)return false;
        _tasks.Add(new PainSplitTask(this));return true;
    }
    private sealed class PainSplitSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly PixelPoint _position;private PixelPoint _offset;private short _fixedX=0x80,_velocityY=0x300,_initialY;private bool _bounced;
        public PainSplitSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=owner._animationArgs;var b=owner._battlers[(a[2]==0?owner._cue.Actor:owner._cue.Target).Value];
            // Both paths use source BATTLER_COORD_Y_PIC_OFFSET; the actor path
            // replaces the automatic target location, not its picture-offset rule.
            var p=b.Center(owner._doubles,true);_position=new(unchecked((short)(p.X+a[0])),unchecked((short)(p.Y+a[1])));_initialY=a[1];
        }
        public override void Step(SourceBattleAnimation owner)
        {
            _offset=new(_fixedX>>8,unchecked((short)(_offset.Y+(_velocityY>>8))));
            if(!_bounced&&_offset.Y>-_initialY){_bounced=true;_velocityY=unchecked((short)((-_velocityY/3)*2));}
            _fixedX=unchecked((short)(_fixedX+192));_velocityY=unchecked((short)(_velocityY+128));
            if(_frames.Ended)Finished=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=16,Height=16,VFlip=f.VFlip};}}
    }
    private sealed class PainSplitTask:VisualTask
    {
        private readonly int _slot;private int _ticks,_shake;
        public PainSplitTask(SourceBattleAnimation owner)
        {
            var a=owner._animationArgs;_slot=(a[0]==0?owner._cue.Actor:owner._cue.Target).Value;owner.PrepareBattlerRotScale(_slot,0);
            owner.SetBattlerRotScale(_slot,a[1]==0?224:208,a[1]==0?320:304,a[1]==0?0:a[1]==1?0xf00:0xf100);owner.SetBattlerYOffsetFromScale(_slot);
            var p=owner._offsets[_slot];owner._offsets[_slot]=new(2,p.Y+(a[1]!=0&&(_slot&1)==0?16:0));
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(++_shake==3){_shake=0;var p=owner._offsets[_slot];owner._offsets[_slot]=new(-p.X,p.Y);}
            if(++_ticks!=13)return;
            owner.ResetBattlerRotScale(_slot);owner._offsets[_slot]=default;Finished=true;
        }
    }
}
