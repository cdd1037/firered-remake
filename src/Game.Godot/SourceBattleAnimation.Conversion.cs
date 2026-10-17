namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private bool ValidateConversionCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="createvisualtask"&&a.FirstOrDefault() is "AnimTask_ConversionAlphaBlend" or "AnimTask_Conversion2AlphaBlend")
        {if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Conversion alpha task operands differ.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_FlashAnimTagWithColor")
        {
            if(a.Length!=9||Number(a[1]) is <0 or >255||!_script.SpriteTags.ContainsKey(a[2])||!_script.SpritePalettes.ContainsKey(AnimationArgument(a[2]))||Number(a[3]) is <0 or >255||Number(a[4]) is <0 or >short.MaxValue||AnimationArgument(a[5]) is <0 or >32767||Number(a[6]) is <0 or >16||AnimationArgument(a[7]) is <0 or >32767||Number(a[8]) is <0 or >16)Fail(c,"Sprite palette flash operands or indexed palette dependency differ.");
            return true;
        }
        if(c.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var found)||found.Callback is not ("AnimConversion" or "AnimConversion2"))return false;
        var r=_script.SpriteRecipes[a[0]];bool second=r.Callback=="AnimConversion2";
        if(a.Length!=(second?6:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||second&&Number(a[5])<0||!r.Blend||r.Width!=8||r.Height!=8||!r.AffineDoubleSize||r.AffineAnimations.Length!=1||r.Animations.Length!=1)Fail(c,"Conversion sprite recipe or operands differ.");
        return true;
    }
    private bool TryCreateConversionTask(SourceAnimationCommand c)
    {
        if(c.Args[0] is "AnimTask_ConversionAlphaBlend" or "AnimTask_Conversion2AlphaBlend")
        {var task=new ConversionAlphaTask(c.Args[0]=="AnimTask_Conversion2AlphaBlend");task.Step(this);_tasks.Add(task);return true;}
        if(c.Args[0]!="AnimTask_FlashAnimTagWithColor")return false;
        var a=c.Args.Skip(2).Select(AnimationArgument).ToArray();if(FindSpritePalette(a[0])==255)Fail(c,"Sprite palette flash has no loaded palette.");
        _tasks.Add(new SpritePaletteFlashTask(a,this));return true;
    }
    private bool TryCreateConversionSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(c.Args[0],out var r)||r.Callback is not ("AnimConversion" or "AnimConversion2"))return false;
        if(!_loaded.Contains("ANIM_TAG_CONVERSION"))Fail(c,"Conversion graphics are not loaded.");
        _sprites.Add(new ConversionSprite(slot,priority,r,c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private sealed class ConversionAlphaTask(bool second):VisualTask
    {
        private short _timer,_blend,_state;
        public override void Step(SourceBattleAnimation o)
        {
            if(!second&&_state==1){o._animationArgs[7]=-1;_state++;}
            else if(!second&&_state==2)Finished=true;
            else if(++_timer==4){_timer=0;_blend++;o._alphaA=second?_blend:16-_blend;o._alphaB=second?16-_blend:_blend;if(_blend==16){if(second)Finished=true;else _state++;}}
        }
    }
    private sealed class SpritePaletteFlashTask:VisualTask
    {
        private readonly int _tag,_firstColor,_firstBlend,_secondColor,_secondBlend;
        private short _delay,_toggle,_remaining;private bool _restoring;
        public SpritePaletteFlashTask(int[] a,SourceBattleAnimation o)
        {_tag=a[0];_delay=_toggle=(short)a[1];_remaining=(short)a[2];_firstColor=a[3];_firstBlend=a[4];_secondColor=a[5];_secondBlend=a[6];Begin(o,_firstBlend,_firstColor);}
        private void Begin(SourceBattleAnimation o,int coefficient,int color)
        {if(!o.PaletteFadeActive){int slot=o.FindSpritePalette(_tag);if(slot==255)throw new InvalidDataException("Sprite palette flash lost its loaded tag.");o._fade=new(1u<<(slot+16),coefficient,coefficient,(ushort)color);o._fade.Update(o);}}
        public override void Step(SourceBattleAnimation o)
        {
            if(_restoring){if(!o.PaletteFadeActive){Begin(o,0,0);Finished=true;}return;}
            if(_delay>0){_delay--;return;}if(o.PaletteFadeActive)return;
            if(_remaining==0){_restoring=true;return;}
            Begin(o,(_toggle&256)!=0?_firstBlend:_secondBlend,(_toggle&256)!=0?_firstColor:_secondColor);
            _toggle^=256;_delay=(short)(_toggle&255);_remaining--;
        }
    }
    private sealed class ConversionSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;
        private readonly bool _second;private PixelPoint _position,_offset;private short _delay;private int _phase;private PhysicalLinear? _linear;
        public ConversionSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] a,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);_second=recipe.Callback=="AnimConversion2";
            var p=o._battlers[_second?o._cue.Target.Value:o._cue.Actor.Value].Base(o._doubles);
            _position=new((short)(p.X+(_second?o.InitialXDirection():1)*a[0]),(short)(p.Y+a[1]));
            if(_second)_delay=(short)a[2];else if((ushort)o._animationArgs[7]==65535)Finished=true;
        }
        public override SourceVisualSpriteFrame Frame{get{var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,_frames.Frame.HFlip,true,256,_recipe.Asset,Subpriority){Width=8,Height=8,VFlip=_frames.Frame.VFlip,Affine=true,DoubleSize=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void Step(SourceBattleAnimation o)
        {
            if(!_second){if((ushort)o._animationArgs[7]==65535)Finished=true;}
            else if(_phase==0){if(_delay!=0)_delay--;else _phase=1;}
            else if(_phase==3)Finished=true;
            else
            {
                if(_phase==1){var p=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_linear=new(p.X-_position.X,p.Y-_position.Y,30);_phase=2;}
                if(_linear!.Step(out var offset))_phase=3;else _offset=offset;
            }
            if(Finished)return;
            if(!_second||_phase!=0)_frames.Step();_affine.Step();
        }
    }
}
