namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string SandCrescentTemplate="gFlyingSandCrescentSpriteTemplate",RainDropTemplate="gRainDropSpriteTemplate",SunlightTemplate="gSunlightRaySpriteTemplate";
    private bool ValidateWeatherOperand(SourceAnimationCommand command)
    {
        var a=command.Args;string? name=a.FirstOrDefault();
        bool sand=command.Op=="createvisualtask"&&name=="AnimTask_LoadSandstormBackground";
        bool blend=command.Op=="createvisualtask"&&name=="AnimTask_BlendBackground";
        bool heat=command.Op=="createvisualtask"&&name=="AnimTask_MoveHeatWaveTargets";
        bool rain=command.Op=="createvisualtask"&&name=="AnimTask_CreateRaindrops";
        bool crescent=command.Op=="createsprite"&&name==SandCrescentTemplate;
        bool sun=command.Op=="createsprite"&&name==SunlightTemplate;
        if(!sand&&!blend&&!heat&&!rain&&!crescent&&!sun)return false;
        if(sand||blend||heat||rain)
        {
            if(a.Length!=(sand?3:blend?4:rain?5:2)||Number(a[1]) is <0 or >255)Fail(command,"Weather task source operand count or priority differs.");
            if(sand&&AnimationArgument(a[2]) is <short.MinValue or >short.MaxValue||blend&&(Number(a[2]) is <0 or >16||PaletteNumber(a[3]) is <0 or >65535)||rain&&(Number(a[2]) is <short.MinValue or >short.MaxValue||Number(a[3]) is <1 or >short.MaxValue||Number(a[4]) is <1 or >short.MaxValue))
                Fail(command,"Weather task source operands differ.");
            if(rain)RequireVisualSeed(command);
        }
        else if(a.Length!=(crescent?7:3)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue)||crescent&&Number(a[4]) is <256 or >4096)
            Fail(command,"Weather sprite source operands differ.");
        if(_validatingInvokedProgram)
        {
            if(sand||blend)
            {
                if(!_script.OverlayDependencies.Contains("sandstorm")||!_script.OverlayPalettes.TryGetValue("sandstorm",out var palette)||palette.Length!=16)
                    Fail(command,"Sandstorm overlay material or palette dependency is missing.");
                var closure=OverlayInvocationCommands();
                if(closure.Any(c=>c.Op=="monbg")||closure.Count(IsOverlayCommand)!=1||!closure.Any(c=>c.Op=="createvisualtask"&&c.Args.FirstOrDefault()=="AnimTask_LoadSandstormBackground"))
                    Fail(command,"Sandstorm requires one BG1 owner without monbg in its bounded source closure.");
                if(blend&&Array.FindIndex(closure,c=>ReferenceEquals(c,command))<Array.FindIndex(closure,c=>c.Op=="createvisualtask"&&c.Args.FirstOrDefault()=="AnimTask_LoadSandstormBackground"))
                    Fail(command,"Background palette write precedes its source overlay load.");
            }
            if(sand||blend||crescent)
            {
                // Authored schedules peak at four logical objects (eight fragments).
                // Validate a stronger all-allocations-live bound, including any
                // prior controller's scene objects, against source gOamLimit=64.
                var closure=OverlayInvocationCommands();int allocations=closure.Count(c=>c.Op=="createsprite");
                int retained=_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length));
                if(allocations>7||22+2*allocations+retained>64||closure.Any(c=>c.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"||c.Op=="createsprite"&&c.Args.FirstOrDefault()!=SandCrescentTemplate||c.Op=="createvisualtask"&&c.Args.FirstOrDefault() is not ("AnimTask_LoadSandstormBackground" or "AnimTask_BlendParticle" or "AnimTask_BlendBackground" or "AnimTask_MoveHeatWaveTargets")))
                    Fail(command,"Sand crescent subsprite capacity exceeds its reviewed closure.");
            }
            if(crescent||rain||sun)RequireOverlaySpritePalette(command,crescent?"ANIM_TAG_FLYING_DIRT":rain?"ANIM_TAG_RAIN_DROPS":"ANIM_TAG_SUNLIGHT");
            if(rain&&!_script.SpriteDependencies.Contains(RainDropTemplate))Fail(command,"Rain raw child dependency metadata is missing.");
        }
        if(sand||blend||heat)return true;
        string template=crescent?SandCrescentTemplate:rain?RainDropTemplate:SunlightTemplate;
        if(!_script.SpriteRecipes.TryGetValue(template,out var recipe)){Fail(command,"Weather sprite recipe is missing.");return false;}
        if(recipe.Callback!=(crescent?"AnimFlyingSandCrescent":rain?"AnimRainDrop":"AnimSunlight")||recipe.Asset!=(crescent?"flying-dirt":rain?"rain-drops":"sunlight")||recipe.Width!=(rain?16:32)||recipe.Height!=(crescent?16:32)||recipe.Blend!=sun||recipe.AffineDoubleSize||recipe.FrameLoops.Any(p=>p.Length!=0)||recipe.Animations.Length!=1)
            Fail(command,"Weather sprite source shape differs.");
        SourceSpriteAnimFrame[] expected=rain?[new(0,2,false,false),new(8,2,false,false),new(16,2,false,false),new(24,6,false,false),new(32,2,false,false),new(40,2,false,false),new(48,2,false,false)]:[new(0,1,false,false)];
        if(!recipe.Animations[0].SequenceEqual(expected)||!recipe.LoopStarts.SequenceEqual([rain?-1:0]))Fail(command,"Weather sprite frame program differs.");
        SourceSpriteSubsprite[] pieces=[new(-16,0,32,16,0,1),new(16,0,32,16,8,1)];
        if(!recipe.Subsprites.SequenceEqual(crescent?pieces:[]))Fail(command,"Weather subsprite source table differs.");
        if(sun)
        {
            SourceSpriteAffineCommand[] affine=[new("frame",80,80,0,0,0),new("frame",2,2,10,1,0),new("jump",0,0,0,0,1)];
            if(recipe.AffineAnimations.Length!=1||!recipe.AffineAnimations[0].SequenceEqual(affine))Fail(command,"Sunlight affine source program differs.");
        }
        else if(recipe.AffineAnimations.Length!=0)Fail(command,"Nonaffine weather sprite has an affine program.");
        return true;
    }
    private bool TryCreateWeatherTask(SourceAnimationCommand command)
    {
        switch(command.Args[0])
        {
            case "AnimTask_LoadSandstormBackground":_tasks.Add(new SandstormBackgroundTask(this));return true;
            case "AnimTask_BlendBackground":
                BlendPalettes(1u<<8,_animationArgs[0],unchecked((ushort)_animationArgs[1]));return true;
            case "AnimTask_MoveHeatWaveTargets":_tasks.Add(new HeatWaveTargetsTask(this));return true;
            case "AnimTask_CreateRaindrops":
                if(!_loaded.Contains("ANIM_TAG_RAIN_DROPS"))Fail(command,"Rain child graphics have not been loaded.");
                var rain=new RainEmitterTask(_animationArgs[1],_animationArgs[2]);rain.Step(this);if(!rain.Finished)_tasks.Add(rain);return true;
            default:return false;
        }
    }
    private bool TryCreateWeatherSprite(SourceAnimationCommand command,int slot,int priority)
    {
        string name=command.Args[0];if(name is not (SandCrescentTemplate or SunlightTemplate))return false;
        if(!_loaded.Contains(name==SandCrescentTemplate?"ANIM_TAG_FLYING_DIRT":"ANIM_TAG_SUNLIGHT"))Fail(command,"Weather sprite graphics have not been loaded.");
        var recipe=_script.SpriteRecipes[name];
        _sprites.Add(name==SandCrescentTemplate?new SandCrescentSprite(slot,priority,recipe,this):new SunlightSprite(slot,priority,recipe));return true;
    }
    private sealed class SandstormBackgroundTask:VisualTask
    {
        private readonly int _direction;private ushort _x,_y;private int _state,_timer,_alpha;
        public SandstormBackgroundTask(SourceBattleAnimation owner)
        {
            _direction=owner._animationArgs[0]!=0&&!owner._cue.Actor.IsPlayer?6:-6;
            owner.BeginOverlay("sandstorm");owner.SetBackgroundPriority(1,1);owner.SetAlphaBlend(2,63,0,16);
        }
        public override void Step(SourceBattleAnimation owner)
        {
            _x=unchecked((ushort)(_x+_direction));_y=unchecked((ushort)(_y-1));
            if(_state<4)owner.SetOverlayScroll(_x,_y);
            switch(_state)
            {
                case 0:
                    if(++_timer==4){_timer=0;_alpha++;owner.SetAlphaBlend(2,63,_alpha,16-_alpha);if(_alpha==7){_state++;_alpha=0;}}break;
                case 1:if(++_alpha==101){_alpha=7;_state++;}break;
                case 2:
                    if(++_timer==4){_timer=0;_alpha--;owner.SetAlphaBlend(2,63,_alpha,16-_alpha);if(_alpha==0)_state++;}break;
                // Source BG1 clear and register cleanup occupy distinct callbacks.
                case 3:owner.EndOverlay(false);_state++;break;
                case 4:owner.ClearAlphaBlend();owner.SetBackgroundPriority(1,1);Finished=true;break;
            }
        }
    }
    private sealed class HeatWaveTargetsTask:VisualTask
    {
        private readonly int[] _slots;private readonly int _direction;private int _state,_counter,_parity,_age,_translation,_jitter;
        public HeatWaveTargetsTask(SourceBattleAnimation owner)
        {
            _direction=owner._cue.Actor.IsPlayer?1:-1;int target=owner._cue.Target.Value,partner=target^2;
            _slots=owner.IsSourceBattlerVisible(partner)?[target,partner]:[target];
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state==3){foreach(int slot in _slots)owner._offsets[slot]=new(0,owner._offsets[slot].Y);Finished=true;return;}
            if(_state!=1)_translation+=_direction*(_state==0?2:-2);
            if(++_counter>=(_state==1?5:2)){_counter=0;_jitter=(++_parity&1)!=0?2:-2;}
            foreach(int slot in _slots)owner._offsets[slot]=new(_translation+_jitter,owner._offsets[slot].Y);
            if(++_age==(_state==1?96:16)){_age=0;_state++;}
        }
    }
    private sealed class SandCrescentSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly PixelPoint _position;private readonly int _speed,_drop;private readonly bool _reverse;
        private PixelPoint _offset;private short _xFraction,_yFraction;private bool _destroyNext;
        public SandCrescentSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;var a=owner._animationArgs;_reverse=a[3]!=0&&!owner._cue.Actor.IsPlayer;
            if(_reverse)a[1]=unchecked((short)-a[1]);_speed=a[1];_drop=a[2];_position=new(_reverse?304:-64,a[0]);
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,0,true,_reverse,false,256,_recipe.Asset,Subpriority)
        {Width=32,Height=16,Subsprites=_recipe.Subsprites};
        public override void Step(SourceBattleAnimation owner)
        {
            if(_destroyNext){Finished=true;return;}
            _xFraction=unchecked((short)(_xFraction+_speed));_yFraction=unchecked((short)(_yFraction+_drop));
            _offset=new(unchecked((short)(_offset.X+(_xFraction>>8))),unchecked((short)(_offset.Y+(_yFraction>>8))));
            _xFraction&=255;_yFraction&=255;
            if(_reverse?_position.X+_offset.X< -32:_position.X+_offset.X>272)_destroyNext=true;
        }
    }
    private sealed class RainEmitterTask(int interval,int duration):VisualTask
    {
        private int _age;
        public override void Step(SourceBattleAnimation owner)
        {
            if(++_age%interval==1)
            {
                // Original Random calls precede allocation, even when it fails.
                var position=new PixelPoint(owner.NextVisualRandom()%240,owner.NextVisualRandom()%80);int slot=owner.FindFreeSpriteSlot();
                if(slot!=SourceSpriteRegistry<VisualSprite>.Capacity)owner._sprites.Add(new RainDropSprite(slot,owner._script.SpriteRecipes[RainDropTemplate],position));
            }
            if(_age==duration)Finished=true;
        }
    }
    private sealed class RainDropSprite(int slot,SourceSpriteRecipe recipe,PixelPoint position):VisualSprite(slot,4)
    {
        private bool _initialized;private int _age;private PixelPoint _offset;private SourceSpriteFrameProgram? _frames;
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame
        {
            get{var f=_frames?.Frame??recipe.Animations[0][0];return new(Slot,recipe.Template,position,_offset,f.Tile,true,f.HFlip,false,256,recipe.Asset,Subpriority){Width=16,Height=32,VFlip=f.VFlip};}
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner){if(_frames is null)_frames=new(recipe);else _frames.Step();}
        public override void Step(SourceBattleAnimation owner)
        {
            if(!_initialized){_initialized=true;return;}
            if(++_age<14)_offset=new(_offset.X+1,_offset.Y+4);
            if(_frames?.Ended==true)Finished=true;
        }
    }
    private sealed class SunlightSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteAffineProgram _affine;private readonly PhysicalLinear _linear=new(140,80,60);
        private PixelPoint _offset;private bool _destroyNext;
        public SunlightSprite(int slot,int priority,SourceSpriteRecipe recipe):base(slot,priority){_recipe=recipe;_affine=new(recipe.AffineAnimations[0]);}
        public override SourceVisualSpriteFrame Frame
        {
            get{var m=_affine.Matrix;return new(Slot,_recipe.Template,default,_offset,0,true,false,true,256,_recipe.Asset,Subpriority){Width=32,Height=32,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_destroyNext){Finished=true;return;}
            if(_linear.Step(out var offset))_destroyNext=true;else _offset=offset;
            _affine.Step();
        }
    }
}
