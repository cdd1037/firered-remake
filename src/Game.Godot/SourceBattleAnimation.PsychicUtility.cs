namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string BentSpoonTemplate="gBentSpoonSpriteTemplate",KinesisZapTemplate="gKinesisZapEnergySpriteTemplate",
        PsychUpSpiralTemplate="gPsychUpSpiralSpriteTemplate",LusterPurgeCircleTemplate="gLusterPurgeCircleSpriteTemplate";
    private bool ValidatePsychicUtilityOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="createvisualtask"&&name=="AnimTask_BlendColorCycleExclude")
        {
            if(a.Length!=8||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >1||Number(a[3]) is < -13 or >63||
                Number(a[4]) is <1 or >short.MaxValue||Number(a[5]) is <0 or >16||Number(a[6]) is <0 or >16||PaletteNumber(a[7]) is <0 or >ushort.MaxValue)
                Fail(c,"Excluded palette cycle source operands differ.");
            return true;
        }
        if(c.Op!="createsprite"||name is not (BentSpoonTemplate or KinesisZapTemplate or PsychUpSpiralTemplate or LusterPurgeCircleTemplate))return false;
        int count=name==BentSpoonTemplate?3:name==KinesisZapTemplate?6:7;
        if(a.Length!=count||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Psychic material source operands differ.");
        if(name==KinesisZapTemplate&&Number(a[5]) is <0 or >1||name is PsychUpSpiralTemplate or LusterPurgeCircleTemplate&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1))Fail(c,"Psychic coordinate/flip mode differs.");
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Psychic material recipe is missing.");return true;}
        bool spoon=name==BentSpoonTemplate,zap=name==KinesisZapTemplate,spiral=name==PsychUpSpiralTemplate;
        string asset=spoon?"bent-spoon":zap?"alert":spiral?"spiral":"white-circle-of-light";
        if(r.Asset!=asset||r.Callback!=(spoon?"AnimBentSpoon":zap?"AnimKinesisZapEnergy":"AnimSpriteOnMonPos")||r.Width!=(spoon?16:zap?32:64)||r.Height!=(spoon?32:zap?16:64)||r.Blend!=(spiral||name==LusterPurgeCircleTemplate)||r.AffineDoubleSize!=(name==LusterPurgeCircleTemplate)||r.Subsprites.Length!=0)Fail(c,"Psychic source shape differs.");
        if(spoon||zap)
        {
            int[] tiles=spoon?[8,16,8,0,8,16,8,0,8,8,24,32,40]:[0,8,16,24,32,40,48];
            int[] durations=spoon?[60,5,5,5,22,5,5,5,5,22,3,3,22]:[3,3,3,3,3,3,3];
            int programs=spoon?2:1;
            if(r.Animations.Length!=programs||r.FrameLoops.Length!=programs||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(-1,programs))||r.AffineAnimations.Length!=0)Fail(c,"Kinesis frame program shape differs.");
            for(int i=0;i<programs;i++)
            {
                var frames=tiles.Select((tile,index)=>new SourceSpriteAnimFrame(tile,durations[index],i==0,false));
                if(!r.Animations[i].SequenceEqual(frames)||!r.FrameLoops[i].SequenceEqual(new[]{spoon?new SourceSpriteFrameLoop(9,5,1):new SourceSpriteFrameLoop(7,0,1)}))Fail(c,"Kinesis frame/finite-loop commands differ.");
            }
        }
        else
        {
            SourceSpriteAffineCommand[] expected=spiral?[new("frame",256,256,0,0,0),new("frame",-2,-2,246,120,0),new("end",0,0,0,0,0)]:[new("frame",32,32,0,0,0),new("frame",4,4,0,120,0),new("end",0,0,0,0,0)];
            if(r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{0})||r.FrameLoops.Any(x=>x.Length!=0)||r.AffineAnimations.Length!=1||!r.AffineAnimations[0].SequenceEqual(expected))Fail(c,"Psychic source affine program differs.");
            if(_validatingInvokedProgram)
            {
                var commands=OverlayInvocationCommands();int births=commands.Count(x=>x.Op=="createsprite");
                if(22+births+_spriteScene.Sprites.Sum(x=>Math.Max(1,x.Frame.Subsprites.Length))>64||4+births+_spriteScene.Sprites.Count(x=>x.Frame.Affine||x.Frame.Scale!=256)>32)Fail(c,"Psychic source closure exceeds OAM or affine capacity.");
            }
        }
        RequireOverlaySpritePalette(c,"ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_'));
        return true;
    }
    private bool TryCreatePsychicUtilitySprite(SourceAnimationCommand c,int slot,int priority)
    {
        string name=c.Args[0];if(name is not (BentSpoonTemplate or KinesisZapTemplate or PsychUpSpiralTemplate or LusterPurgeCircleTemplate))return false;
        var recipe=_script.SpriteRecipes[name];if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Psychic graphics have not been loaded.");
        if(name is BentSpoonTemplate or KinesisZapTemplate)_sprites.Add(new KinesisFrameSprite(slot,priority,recipe,this));
        else _sprites.Add(new ElementalSprite(slot,priority,recipe,Resolve(c.Args[1]),c.Args.Skip(3).Select(Number).ToArray(),this));
        return true;
    }
    private bool TryCreatePsychicUtilityTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_BlendColorCycleExclude")return false;
        var a=c.Args.Skip(2).Select(PaletteNumber).ToArray();uint mask=a[0]==1?0xEu:0;
        // Native gBattlersCount includes hidden/absent battler slots. This task
        // deliberately differs from the visibility-filtered palette selectors.
        for(int i=0;i<_battlerCount;i++)if(i!=_cue.Actor.Value&&i!=_cue.Target.Value)mask|=1u<<(16+i);
        _tasks.Add(new BlendCycleTask(this,a,mask));return true;
    }
    private sealed class KinesisFrameSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly PixelPoint _position;private readonly bool _hFlip,_vFlip;private bool _destroyReady;
        public KinesisFrameSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;bool player=owner._cue.Actor.IsPlayer,spoon=recipe.Template==BentSpoonTemplate;
            _frames=new(recipe,spoon&&!player?1:0);var center=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);var a=owner._animationArgs;
            _position=spoon?new(center.X+(player?40:-40),center.Y+(player?-10:10)):
                new(unchecked((short)(center.X+(player?a[0]:-a[0]))),unchecked((short)(center.Y+a[1])));
            _hFlip=!spoon&&!player;_vFlip=!spoon&&a[2]!=0;
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;return new(Slot,_recipe.Template,_position,default,f.Tile,true,f.HFlip^_hFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip^_vFlip};}}
        public override void Step(SourceBattleAnimation owner)
        {
            // RunStoredCallbackWhenAnimEnds installs the destructor; it does not
            // execute it until the next AnimateSprites pass.
            if(_destroyReady)Finished=true;else if(_frames.Ended)_destroyReady=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
    }
}
