namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string AncientRockTemplate="gAncientPowerRockSpriteTemplate",TombRockTemplate="gRockTombRockSpriteTemplate",RedXTemplate="gRedXSpriteTemplate",SpinningSparkleTemplate="gSpinningSparkleSpriteTemplate";
    private bool ValidateRockStatusCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="loadspritegfx"&&name is "ANIM_TAG_X_SIGN" or "ANIM_TAG_SPARKLE_4")
        {if(a.Length!=1)Fail(c,"Rock/status graphics operands differ.");return true;}
        if(c.Op=="createvisualtask"&&name=="AnimTask_GrowAndGrayscale")
        {
            if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Grow/grayscale takes only source task priority.");
            if(_validatingInvokedProgram&&!IsSourceBattlerVisible(_cue.Target.Value))Fail(c,"Grow/grayscale requires a known visible source participant.");
            return true;
        }
        if(c.Op!="createsprite"||name is not (AncientRockTemplate or TombRockTemplate or RedXTemplate or SpinningSparkleTemplate))return false;
        bool rock=name is AncientRockTemplate or TombRockTemplate;
        if(a.Length!=(rock?8:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is < -32768 or >32767))Fail(c,"Rock/status sprite source argument widths differ.");
        if(rock&&(Number(a[6]) is <1 or >32767||Number(a[7]) is <0 or >5))Fail(c,"Rock source lifetime or animation index differs.");
        if(name==TombRockTemplate&&(Number(a[5]) is <0 or >32767))Fail(c,"Falling rock requires a nonnegative source drop.");
        if(name==RedXTemplate&&(Number(a[3]) is <0 or >1||Number(a[4]) is <0 or >32767))Fail(c,"Red X participant or lifetime differs.");
        ValidateRockStatusRecipe(c,name);return true;
    }
    private void ValidateRockStatusRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Rock/status sprite recipe is missing.");r=_script.SpriteRecipes[name];
        var expected=name switch{AncientRockTemplate=>("rocks","AnimRaiseSprite",32),TombRockTemplate=>("rocks","AnimRockTomb",32),RedXTemplate=>("x-sign","AnimRedX",64),_=>("sparkle-4","AnimSpinningSparkle",32)};
        if(r.Asset!=expected.Item1||r.Callback!=expected.Item2||r.Width!=expected.Item3||r.Height!=expected.Item3||r.Blend||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.FrameLoops.Any(x=>x.Length!=0))Fail(c,"Rock/status source shape or callback differs.");
        SourceSpriteAnimFrame[][] frames=name switch
        {
            AncientRockTemplate or TombRockTemplate=>Enumerable.Range(0,6).Select(i=>new[]{new SourceSpriteAnimFrame(i*16,1,false,false)}).ToArray(),
            SpinningSparkleTemplate=>[[new(0,3,false,false),new(16,3,false,false),new(32,3,false,false),new(48,3,false,false),new(64,3,false,false)]],
            _=>[[new(0,1,false,false)]]
        };
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(name==RedXTemplate?0:-1,frames.Length)))Fail(c,"Rock/status source frame-table shape differs.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Rock/status source frames differ.");
        if(name==RedXTemplate)
        {
            string tag="ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_');
            if(!_script.SpriteTags.TryGetValue(tag,out int paletteTag)||!_script.SpritePalettes.TryGetValue(paletteTag,out var palette)||palette.Length!=16)Fail(c,"Rock/status indexed palette dependency is missing.");
        }
    }
    private bool TryCreateRockStatusTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_GrowAndGrayscale")return false;
        _tasks.Add(new GrowAndGrayscaleTask(this));return true;
    }
    private sealed class GrowAndGrayscaleTask:VisualTask
    {
        private readonly int _slot;private short _timer=80;
        public GrowAndGrayscaleTask(SourceBattleAnimation owner)
        {_slot=owner._cue.Target.Value;owner.PrepareBattlerRotScale(_slot,1);owner.SetBattlerRotScale(_slot,0xd0,0xd0,0);owner.SetBattlerGreyscaleOrOriginal(_slot,false);}
        public override void Step(SourceBattleAnimation owner)
        {if(--_timer==-1){owner.ResetBattlerRotScale(_slot);owner.SetBattlerGreyscaleOrOriginal(_slot,true);Finished=true;}}
    }
    private bool TryCreateRockStatusSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (AncientRockTemplate or TombRockTemplate or RedXTemplate or SpinningSparkleTemplate))return false;
        var recipe=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Rock/status sprite graphics have not been loaded.");
        _sprites.Add(new RockStatusSprite(slot,priority,recipe,Number(c.Args[1]),this));return true;
    }
    private sealed class RockStatusSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;
        private readonly PhysicalLinear? _linear;private PixelPoint _offset;private short _speed,_timer,_yOffset,_drop,_ticks;private bool _invisible,_destroyNext;
        public RockStatusSprite(int slot,int priority,SourceSpriteRecipe recipe,int participant,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;var a=owner._animationArgs;_frames=new(recipe,recipe.Template is AncientRockTemplate or TombRockTemplate?a[4]:0);
            var actor=owner._battlers[owner._cue.Actor.Value];
            _position=owner._battlers[(participant==0?owner._cue.Actor:owner._cue.Target).Value].Center(owner._doubles,true);
            switch(recipe.Template)
            {
                case AncientRockTemplate:
                    var start=actor.Base(owner._doubles);_position=new(unchecked((short)(start.X+owner.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
                    _linear=new(0,unchecked((short)(_position.Y+a[2]))-_position.Y,a[3]);break;
                case TombRockTemplate:
                    _offset=new(a[0],0);_yOffset=a[1];_drop=unchecked((short)-a[2]);_speed=3;_timer=a[3];_invisible=true;break;
                case RedXTemplate:
                    if(a[0]==0)_position=actor.Center(owner._doubles,true);_timer=a[1];break;
                case SpinningSparkleTemplate:
                    var center=actor.Center(owner._doubles,true);_position=new(unchecked((short)(center.X+(owner._cue.Actor.IsPlayer?1:-1)*a[0])),unchecked((short)(center.Y+a[1])));break;
            }
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!_invisible,_frames.Frame.HFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_frames.Frame.VFlip};
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_recipe.Template)
            {
                case AncientRockTemplate:
                    if(_destroyNext){Finished=true;return;}
                    // Completion installs the stored destroy callback; it runs next tick.
                    if(_linear!.Step(out var offset))_destroyNext=true;else _offset=offset;break;
                case TombRockTemplate:
                    _invisible=false;
                    if(_drop!=0){_offset=new(_offset.X,unchecked((short)(_yOffset+_drop)));_drop=unchecked((short)(_drop+_speed));_speed++;if(_drop>0)_drop=0;}
                    else if(--_timer==0)Finished=true;break;
                case RedXTemplate:
                    if(_ticks>_timer-10)_invisible=(_ticks&1)!=0;if(_ticks==_timer)Finished=true;_ticks++;break;
                case SpinningSparkleTemplate:
                    if(_destroyNext)Finished=true;else if(_frames.Ended)_destroyNext=true;break;
            }
        }
    }
}
