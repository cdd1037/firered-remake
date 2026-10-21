namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string PsychoBoostTemplate="gPsychoBoostOrbSpriteTemplate";
    private static readonly SourceSpriteAffineCommand[][] PsychoBoostAffinePrograms=
    [
        [new("frame",32,32,0,0,0),new("frame",16,16,0,17,0),new("loop",0,0,0,0,0),new("frame",-8,-8,0,10,0),new("frame",8,8,0,10,0),new("loop",0,0,0,0,4),new("loop",0,0,0,0,0),new("frame",-16,-16,0,5,0),new("frame",16,16,0,5,0),new("loop",0,0,0,0,7),new("end",0,0,0,0,0)],
        [new("frame",-20,24,0,15,0),new("end",0,0,0,0,0)]
    ];
    private bool ValidatePsychoBoostCommand(SourceAnimationCommand c)
    {
        var a=c.Args;if(c.Op!="createsprite"||a.FirstOrDefault()!=PsychoBoostTemplate)return false;
        if(a.Length!=3||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Psycho Boost orb source operands differ.");
        if(!_script.SpriteRecipes.TryGetValue(PsychoBoostTemplate,out var r)){Fail(c,"Psycho Boost orb recipe is missing.");return true;}
        if(r.Callback!="AnimPsychoBoost"||r.Asset!="circle-of-light"||r.Width!=64||r.Height!=64||!r.Blend||!r.AffineDoubleSize||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{0})||r.FrameLoops.Any(x=>x.Length!=0)||r.AffineAnimations.Length!=2)Fail(c,"Psycho Boost source frame or affine shape differs.");
        for(int i=0;i<2;i++)if(!r.AffineAnimations[i].SequenceEqual(PsychoBoostAffinePrograms[i]))Fail(c,"Psycho Boost source affine program differs.");
        if(!_script.SpriteTags.TryGetValue("ANIM_TAG_CIRCLE_OF_LIGHT",out int tag)||!_script.SpritePalettes.TryGetValue(tag,out var palette)||palette.Length!=16||!_script.SoundEffects.ContainsKey("SE_M_TELEPORT"))Fail(c,"Psycho Boost indexed material or callback sound dependency is missing.");
        if(_validatingInvokedProgram)
        {
            var commands=OverlayInvocationCommands();
            if(commands.Any(x=>x.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"||x.Op=="createsprite"&&x.Args[0]!=PsychoBoostTemplate||x.Op=="createvisualtask"&&x.Args[0] is not ("AnimTask_FadeScreenToWhite" or "AnimTask_SetPsychicBackground" or "AnimTask_BlendColorCycle" or "AnimTask_ShakeMon" or "AnimTask_ShakeMon2")))Fail(c,"Psycho Boost requires a reviewed non-emitting affine closure.");
            int births=commands.Count(x=>x.Op=="createsprite");
            if(22+births+_spriteScene.Sprites.Sum(x=>Math.Max(1,x.Frame.Subsprites.Length))>64||4+births+_spriteScene.Sprites.Count(x=>x.Frame.Affine||x.Frame.Scale!=256)>32)Fail(c,"Psycho Boost exceeds source OAM or affine capacity.");
        }
        return true;
    }
    private bool TryCreatePsychoBoostSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=PsychoBoostTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_CIRCLE_OF_LIGHT"))Fail(c,"Psycho Boost graphics have not been loaded.");
        _sprites.Add(new PsychoBoostSprite(slot,priority,_script.SpriteRecipes[PsychoBoostTemplate],this));return true;
    }
    private sealed class PsychoBoostSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private SourceSpriteAffineProgram _affine;
        private readonly PixelPoint _position;private PixelPoint _offset;private short _phase=1,_blend=8,_timer,_verticalFraction;private bool _invisible,_restartAffine;
        public PsychoBoostSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);
            // The native initial callback reads BATTLER_COORD_X/Y, not animated
            // x2/y2 or picture/elevation offsets, before BeginAffineAnim.
            _position=owner._battlers[owner._cue.Actor.Value].Base(owner._doubles);owner.SetAlphaBlend(0,63,8,8);
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible,f.HFlip,true,256,_recipe.Asset,Subpriority){Width=64,Height=64,VFlip=f.VFlip,Affine=true,DoubleSize=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_phase)
            {
                case 1:
                    if(_affine.Ended)
                    {
                        owner._audio.Play(new("SE_M_TELEPORT",owner.AdjustPanning(-64),owner._frame));
                        // ChangeSpriteAffineAnim preserves scale and rotation.
                        _affine=new(_recipe.AffineAnimations[1],_affine.XScale,_affine.YScale,_affine.Rotation);_restartAffine=true;_phase=2;
                    }
                    break;
                case 2:
                    if(_timer++>1)
                    {
                        _timer=0;_blend--;owner.SetAlphaBlend(0,63,_blend,16-_blend);
                        if(_blend==0){_phase=3;_invisible=true;}
                    }
                    _verticalFraction=unchecked((short)(_verticalFraction+0x380));_offset=new(_offset.X,unchecked((short)(_offset.Y-(_verticalFraction>>8))));_verticalFraction&=0xFF;
                    break;
                case 3:owner.ClearAlphaBlend();Finished=true;break;
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {_frames.Step();if(_restartAffine)_restartAffine=false;else _affine.Step();}
    }
}
