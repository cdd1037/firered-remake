namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string PresentBag="gPresentSpriteTemplate",PresentParticle="gPresentHealParticleSpriteTemplate",PresentPredicate="AnimTask_IsHealingMove";
    private bool ValidatePresentCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        bool task=c.Op=="createvisualtask"&&name==PresentPredicate,sprite=c.Op=="createsprite"&&name is PresentBag or PresentParticle;
        if(!task&&!sprite)return false;
        bool bag=name==PresentBag;
        if(a.Length!=(task?2:bag?8:7)||Number(a[task?1:2]) is <0 or >255||sprite&&(Number(a[1]) is <0 or >1||Number(a[2])>127)||a.Skip(task?2:3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Present source operand width differs.");
        if(sprite)
        {
            if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Present source sprite recipe is missing.");return true;}
            var expectedFrames=bag?new[]{new SourceSpriteAnimFrame(0,30,false,false)}:Enumerable.Range(0,4).Select(i=>new SourceSpriteAnimFrame(i*4,4,false,false)).ToArray();
            if(r.Asset!=(bag?"item-bag":"green-sparkle")||r.Callback!=(bag?"AnimPresent":"AnimPresentHealParticle")||r.Width!=(bag?32:16)||r.Height!=r.Width||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(expectedFrames)||!r.LoopStarts.SequenceEqual([-1])||r.FrameLoops.Any(x=>x.Length!=0)||r.AffineAnimations.Length!=(bag?2:0))Fail(c,"Present finite source sprite program differs.");
            if(bag)
            {
                SourceSpriteAffineCommand F(int rotation,int duration)=>new("frame",0,0,rotation&255,duration,0);
                SourceSpriteAffineCommand E()=>new("end",0,0,0,0,0);
                SourceSpriteAffineCommand[][] affine=[[F(-4,10),F(4,20),F(-4,10),E()],[F(-1,2),F(1,4),F(-1,4),F(1,4),F(-1,4),F(1,2),E()]];
                for(int i=0;i<2;i++)if(!r.AffineAnimations[i].SequenceEqual(affine[i]))Fail(c,"Present bag affine program differs.");
            }
        }
        if(!_validatingInvokedProgram)return true;
        if(task&&_cue.IsHealingMove is null)Fail(c,"Present predicate requires its committed source damage-sign input.");
        if(!IsSourceBattlerPresent(_cue.Actor.Value)||!IsSourceBattlerPresent(_cue.Target.Value)||_script.SineTable.Length!=320)Fail(c,"Present requires present participants and its source sine table.");
        ValidateReactionGeometry(c,_battlers[_cue.Actor.Value]);ValidateReactionGeometry(c,_battlers[_cue.Target.Value]);
        var commands=OverlayInvocationCommands();
        if(commands.Any(x=>x.Op=="createsprite"&&x.Args[0] is not(PresentBag or PresentParticle or "gExplosionSpriteTemplate" or "gHealingBlueStarSpriteTemplate")||x.Op=="createvisualtask"&&x.Args[0]!=PresentPredicate))Fail(c,"Present companions exceed the reviewed non-emitting source closure.");
        int births=MaximumInvocationCost(x=>x.Op=="createsprite"?1:0),matrices=MaximumInvocationCost(x=>x.Op=="createsprite"&&x.Args[0]==PresentBag?1:0),tasks=1+MaximumInvocationCost(x=>x.Op is "createvisualtask" or "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "panse_adjustall" or "panse_adjustnone"?1:0);
        if(22+births+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>64||4+matrices+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32||tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Present complete branch exceeds native object, matrix or task capacity.");
        return true;
    }
    private bool TryCreatePresentTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!=PresentPredicate)return false;
        _animationArgs[7]=(short)(_cue.IsHealingMove!.Value?1:0);return true;
    }
    private bool TryCreatePresentSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not(PresentBag or PresentParticle))return false;
        if(!_loaded.Contains(c.Args[0]==PresentBag?"ANIM_TAG_ITEM_BAG":"ANIM_TAG_GREEN_SPARKLE"))Fail(c,"Present graphics are not loaded.");
        _sprites.Add(new PresentSprite(slot,priority,_script.SpriteRecipes[c.Args[0]],this));return true;
    }
    private sealed class PresentSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private SourceSpriteAffineProgram? _affine;
        private readonly short[] _d=new short[8];private PixelPoint _position,_offset;private int _phase;private bool _restart;
        private static short S(int value)=>unchecked((short)value);
        public PresentSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r);var a=o._animationArgs;bool bag=r.Template==PresentBag;
            var b=o._battlers[(bag?o._cue.Actor:o._cue.Target).Value];var p=b.Base(o._doubles);
            // FALSE selects raw BATTLER_COORD_Y, independent of picture offset,
            // elevation and the target's currently displayed substitute image.
            _position=new(S(p.X+o.InitialXDirection()*a[0]),S(p.Y+a[1]));
            if(!bag){_d[1]=a[2];Step(o);return;}
            _affine=new(r.AffineAnimations[0]);var target=o._battlers[o._cue.Target.Value].Base(o._doubles);
            _d[5]=S((_position.X<<8)|_position.Y);_d[6]=S((target.X<<8)|(target.Y+10));_d[7]=S(60<<8);
            _d[3]=(short)(o._cue.Actor.Partner==o._cue.Target?1:3);_d[4]=60;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o)
        {_frames.Step();if(_restart)_restart=false;else _affine?.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==PresentParticle)
            {_d[0]=S(_d[0]+1);_offset=new(0,S(_d[1]*_d[0]));if(_frames.Ended)Finished=true;return;}
            if(_phase==0)
            {
                _d[0]=S(_d[0]+_d[3]*128/_d[4]);if(_d[0]>=128){_d[1]=S(_d[1]+1);_d[0]=0;}
                _offset=new(0,S((o._script.SineTable[_d[0]+128]*(30-_d[1]*8))>>8));
                if(AlongPath()){_offset=default;_d[0]=0;_phase=1;}return;
            }
            if(_d[0]==10){_affine=new(_recipe.AffineAnimations[1]);_restart=true;}
            _d[0]=S(_d[0]+1);if(_d[0]>50)Finished=true;
        }
        private bool AlongPath()
        {
            int startX=(byte)(_d[5]>>8),startY=(byte)_d[5],endX=(byte)(_d[6]>>8),endY=(byte)_d[6];
            int total=_d[7]>>8,current=_d[7]&255;if(endX==0)endX=-32;else if(endX==255)endX=272;
            _position=new(S((short)(endX-startX)*current/total+startX),S((short)(endY-startY)*current/total+startY));
            // Native returns before saving currentTime at the final step. It
            // intentionally leaves position one interpolation step short.
            if(++current==total)return true;_d[7]=S((total<<8)|current);return false;
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_affine is not null,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
}
