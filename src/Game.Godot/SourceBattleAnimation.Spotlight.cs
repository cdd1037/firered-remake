namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string EncoreSpotlight="gSpotlightSpriteTemplate",FlatterSpotlight="gFlatterSpotlightSpriteTemplate",ClapHand="gClappingHandSpriteTemplate",ClapMask="gClappingHand2SpriteTemplate",FlatterConfetti="gFlatterConfettiSpriteTemplate";
    private static bool SpotlightTemplate(string name)=>name is EncoreSpotlight or FlatterSpotlight or ClapHand or ClapMask or FlatterConfetti;
    private bool ValidateSpotlightCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";bool sprite=c.Op=="createsprite"&&SpotlightTemplate(name),task=c.Op=="createvisualtask"&&name is "AnimTask_CreateSpotlight" or "AnimTask_RemoveSpotlight";
        if(!sprite&&!task)return false;
        bool mask=name is EncoreSpotlight or FlatterSpotlight,hand=name is ClapHand or ClapMask;
        if(task)
        {if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Spotlight window task has no arguments.");}
        else
        {
            if(a.Length!=(hand?8:name==FlatterConfetti?4:name==FlatterSpotlight?6:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue))Fail(c,"Spotlight sprite source operands differ.");
            if(hand&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1||Number(a[7])<=0)||name==FlatterConfetti&&Number(a[3]) is <0 or >1||name==FlatterSpotlight&&Number(a[5])<=0)Fail(c,"Spotlight selector or positive lifetime differs.");
            if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Spotlight source recipe missing.");return true;}
            if(r.Callback!=(name==EncoreSpotlight?"AnimSpotlight":name==FlatterSpotlight?"AnimFlatterSpotlight":name==ClapHand?"AnimClappingHand":hand?"AnimClappingHand2":"AnimFlatterConfetti")||r.Asset!=(mask?"spotlight":hand?"tag-hand":"confetti")||r.Width!=(mask?64:hand?32:8)||r.Height!=r.Width||r.Blend||r.AffineDoubleSize!=mask||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{0})||r.FrameLoops.Length>1||r.FrameLoops.Any(x=>x.Length!=0)||r.AffineAnimations.Length!=(mask?2:0))Fail(c,"Spotlight source template differs.");
            if(mask)
            {
                SourceSpriteAffineCommand[][] expected=[[new("frame",0,384,0,0,0),new("frame",16,0,0,20,0),new("end",0,0,0,0,0)],[new("frame",320,384,0,0,0),new("frame",-16,0,0,19,0),new("end",0,0,0,0,0)]];
                for(int i=0;i<2;i++)if(!r.AffineAnimations[i].SequenceEqual(expected[i]))Fail(c,"Spotlight source affine program differs.");
            }
            if(name==ClapHand&&!_script.SoundEffects.ContainsKey("SE_M_ENCORE"))Fail(c,"Clapping callback sound dependency is missing.");
            if(name==FlatterConfetti)RequireVisualSeed(c);
        }
        if(_validatingInvokedProgram)ValidateSpotlightCapacity(c);
        return true;
    }
    private void ValidateSpotlightCapacity(SourceAnimationCommand c)
    {
        var commands=OverlayInvocationCommands();
        if(commands.Any(x=>x.Op=="createsprite"&&!SpotlightTemplate(x.Args[0])&&x.Args[0]!="gVerticalDipSpriteTemplate"||x.Op=="createvisualtask"&&x.Args[0] is not ("AnimTask_CreateSpotlight" or "AnimTask_RemoveSpotlight" or "AnimTask_HardwarePaletteFade" or "AnimTask_SwayMon" or "SoundTask_PlaySE1WithPanning" or "SoundTask_PlaySE2WithPanning")||x.Op is "setalpha" or "blendoff" or "fadetobg" or "restorebg" or "changebg" or "monbg" or "monbg_static" or "clearmonbg" or "clearmonbg_static"))Fail(c,"Spotlight requires its bounded non-emitting window/color-register closure.");
        if(MaximumInvocationCost(x=>x.Op=="createsprite"&&x.Args[0] is EncoreSpotlight or FlatterSpotlight?1:0)!=1||MaximumInvocationCost(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_CreateSpotlight"?1:0)!=1||MaximumInvocationCost(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_RemoveSpotlight"?1:0)!=1)Fail(c,"Spotlight requires one mask owner and paired window setup/cleanup.");
        int births=MaximumInvocationCost(x=>x.Op=="createsprite"?1:0),affine=MaximumInvocationCost(x=>x.Op=="createsprite"&&x.Args[0] is EncoreSpotlight or FlatterSpotlight?1:0),tasks=1+MaximumInvocationCost(x=>x.Op is "createvisualtask" or "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse"?1:0);
        if(22+births+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>64||4+affine+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32||tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Spotlight complete path exceeds native object, matrix or task capacity.");
    }
    private bool TryCreateSpotlightTask(SourceAnimationCommand c)
    {
        if(c.Args[0]=="AnimTask_CreateSpotlight"){SetWindow1(true,0x00f0,0x78a0,31,63);return true;}
        if(c.Args[0]=="AnimTask_RemoveSpotlight"){SetWindow1(false,0,0,63,63);return true;}
        return false;
    }
    private bool TryCreateSpotlightSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(!SpotlightTemplate(c.Args[0]))return false;var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Spotlight sprite graphics are not loaded.");
        _sprites.Add(new SpotlightSprite(slot,priority,r,this));return true;
    }
    private sealed class SpotlightSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private SourceSpriteAffineProgram? _affine;
        private PixelPoint _position,_offset;private short _phase,_timer,_fixedX,_remaining,_velocityX,_velocityY,_side;
        private int _tile;private bool _invisible,_hFlip,_restartAffine,_destroyNext;
        private static short S(int value)=>unchecked((short)value);
        private bool Mask=>_recipe.Template is EncoreSpotlight or FlatterSpotlight;
        private bool Hand=>_recipe.Template is ClapHand or ClapMask;
        public SpotlightSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=o._animationArgs;
            if(Mask)
            {
                o.SetObjectWindow(true,31,63);o._window0=new(true,0,0,63,63);var p=o._battlers[o._cue.Target.Value].Base(o._doubles);_position=new(S(p.X+o.InitialXDirection()*a[0]),S(p.Y+a[1]));_remaining=a[2];_invisible=true;_affine=new(recipe.AffineAnimations[0]);
            }
            else if(Hand)
            {
                var p=a[3]==0?o._battlers[o._cue.Actor.Value].Base(o._doubles):o._battlers[o._cue.Target.Value].Center(o._doubles,true);_position=new(S(p.X+a[0]),S(p.Y+a[1]));_tile=16;_hFlip=a[2]==0;_offset=new(_hFlip?-12:12,0);_velocityX=(short)(_hFlip?2:-2);_remaining=a[4];_side=recipe.Template==ClapMask?(short)255:a[2];
            }
            else
            {
                _tile=o.NextVisualRandom()%12;int first=o.NextVisualRandom()&511,second=o.NextVisualRandom()&255;_velocityX=S((first&1)!=0?0x5e0+first:0x5e0-first);_velocityY=S((second&1)!=0?0x480+second:0x480-second);_side=a[0];_position=new(_side==0?-8:248,104);
            }
        }
        private void Close(SourceBattleAnimation o){o.SetObjectWindow(false,63,63);Finished=true;}
        private void Shrink(){_affine=new(_recipe.AffineAnimations[1],_affine!.XScale,_affine.YScale,_affine.Rotation);_restartAffine=true;}
        public override void Step(SourceBattleAnimation o)
        {
            if(Mask)
            {
                if(_destroyNext){Close(o);return;}
                if(_phase==0){_invisible=false;if(_affine!.Ended)_phase++;return;}
                if(_recipe.Template==FlatterSpotlight)
                {
                    if(_phase==1){if(--_remaining==0){Shrink();_phase++;}}
                    else if(_affine!.Ended){_invisible=true;_destroyNext=true;}
                }
                else switch(_phase)
                {
                    case 1:case 3:_fixedX=S(_fixedX+117);_offset=new(_fixedX>>8,0);if(++_timer==21){_timer=0;_phase++;}break;
                    case 2:_fixedX=S(_fixedX-117);_offset=new(_fixedX>>8,0);if(++_timer==41){_timer=0;_phase++;}break;
                    case 4:Shrink();_phase++;break;
                    case 5:if(_affine!.Ended){_invisible=true;_destroyNext=true;}break;
                }
                return;
            }
            if(Hand)
            {
                if(_phase==0)
                {_offset=new(S(_offset.X+_velocityX),0);if(_offset.X==0){_phase++;if(_side==0)o._audio.Play(new("SE_M_ENCORE",o.AdjustPanning(-64),o._frame){PanningPlayer=1});}}
                else
                {_offset=new(S(_offset.X-_velocityX),0);if(Math.Abs(_offset.X)==12){_remaining--;_phase--;}}
                if(_remaining==0)Finished=true;
                return;
            }
            _offset=new(S(_offset.X+(_side==0?1:-1)*(_velocityX>>8)),S(_offset.Y-(_velocityY>>8)));_velocityX=S(_velocityX-22);_velocityY=S(_velocityY-48);if(_velocityX<0)_velocityX=0;if(++_timer==31)Finished=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();if(_restartAffine)_restartAffine=false;else _affine?.Step();}
        public override SourceVisualSpriteFrame Frame
        {get{var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);return new(Slot,_recipe.Template,_position,_offset,_tile,!_invisible,_hFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,Affine=Mask,DoubleSize=Mask,ObjectWindow=Mask||_recipe.Template==ClapMask,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
}
