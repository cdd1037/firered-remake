namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string LeerTemplate="gLeerSpriteTemplate",TailGlowTemplate="gTailGlowOrbSpriteTemplate",TearDropTemplate="gTearDropSpriteTemplate";
    private bool ValidatePrimaryStatOperand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_BlendParticle")
        {
            if(a.Length!=7||Number(a[1]) is <0 or >255||!_script.SpriteTags.TryGetValue(a[2],out int tag)||!_script.SpritePalettes.ContainsKey(tag)||Number(a[3]) is <0 or >255||Number(a[4]) is <0 or >16||Number(a[5]) is <0 or >16||PaletteNumber(a[6]) is <0 or >65535)Fail(c,"Particle palette task requires a source tag, indexed palette and bounded blend operands.");
            return true;
        }
        if(c.Op!="createsprite"||a.FirstOrDefault() is not(LeerTemplate or TailGlowTemplate or TearDropTemplate))return false;
        bool leer=a[0]==LeerTemplate,tail=a[0]==TailGlowTemplate;
        if(a.Length!=(tail?4:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||!_script.SpriteRecipes.ContainsKey(a[0]))Fail(c,"Primary stat sprite operands or recipe differ.");
        if(leer){if(a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Leer coordinates exceed source signed words.");}
        else if(Number(a[3]) is <0 or >1||!tail&&Number(a[4]) is <0 or >3)Fail(c,"Primary stat sprite participant or tear variant differs.");
        var r=_script.SpriteRecipes[a[0]];string asset=leer?"leer":tail?"circle-of-light":"small-bubbles";
        if(r.Callback!=(leer?"AnimLeer":tail?"AnimTailGlowOrb":"AnimTearDrop")||r.Asset!=asset||r.Width!=(leer?32:tail?64:16)||r.Height!=r.Width||r.Blend!=tail||r.AffineDoubleSize||r.Animations.Length!=1||r.FrameLoops.Any(p=>p.Length!=0)||!r.LoopStarts.SequenceEqual([leer?-1:0]))Fail(c,"Primary stat source recipe shape differs.");
        SourceSpriteAnimFrame[] frames=leer?[new(0,3,false,false),new(16,3,false,false),new(32,3,false,false),new(48,3,false,false),new(64,3,false,false)]:[new(0,1,false,false)];
        if(!r.Animations[0].SequenceEqual(frames)||r.AffineAnimations.Length!=(leer?0:tail?1:2))Fail(c,"Primary stat source frame or affine program count differs.");
        if(tail)
        {
            SourceSpriteAffineCommand[] program=[new("frame",16,16,0,0,0),new("frame",8,8,0,18,0),new("loop",0,0,0,0,0),new("frame",-5,-5,0,8,0),new("frame",5,5,0,8,0),new("loop",0,0,0,0,5),new("end",0,0,0,0,0)];
            if(!r.AffineAnimations[0].SequenceEqual(program))Fail(c,"Tail Glow source affine loop differs.");
        }
        else if(!leer)
        {
            for(int i=0;i<2;i++)
            {
                SourceSpriteAffineCommand[] program=[new("frame",192,192,i==0?80:176,0,0),new("frame",0,0,i==0?254:2,8,0),new("end",0,0,0,0,0)];
                if(!r.AffineAnimations[i].SequenceEqual(program))Fail(c,"Tear drop source affine program differs.");
            }
            if(_script.SineTable.Length!=320)Fail(c,"Tear drop source sine table is missing.");
        }
        if(!_script.SpriteTags.TryGetValue("ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_'),out int palette)||!_script.SpritePalettes.ContainsKey(palette))Fail(c,"Primary stat indexed source palette is missing.");
        return true;
    }
    private bool TryCreatePrimaryStatTask(SourceAnimationCommand c)
    {
        var a=c.Args;if(a[0]!="AnimTask_BlendParticle")return false;
        int slot=FindSpritePalette(_script.SpriteTags[a[2]]);
        if(!_loaded.Contains(a[2])||slot==255)Fail(c,"Particle palette tag has no loaded source bank.");
        var task=new BlendPaletteTask(1u<<(slot+16),Number(a[3]),Number(a[4]),Number(a[5]),(ushort)PaletteNumber(a[6]));
        task.Step(this);if(!task.Finished)_tasks.Add(task);return true;
    }
    private bool TryCreatePrimaryStatSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not(LeerTemplate or TailGlowTemplate or TearDropTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Primary stat source graphics have not been loaded.");
        _sprites.Add(new PrimaryStatSprite(slot,priority,r,c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    // These callbacks only select initial position/program and retain shared
    // frame, affine, horizontal arc, and stored-callback lifetime ownership.
    private sealed class PrimaryStatSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly SourceSpriteAffineProgram? _affine;private readonly HorizontalArc? _arc;
        private readonly PixelPoint _position;private PixelPoint _offset;private bool _destroyNext;
        public PrimaryStatSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);bool leer=recipe.Template==LeerTemplate,tail=recipe.Template==TailGlowTemplate;
            var battler=owner._battlers[(leer||args[0]==0?owner._cue.Actor:owner._cue.Target).Value];var center=battler.Center(owner._doubles,true);
            if(leer)_position=new(unchecked((short)(center.X+owner.InitialXDirection()*args[0])),unchecked((short)(center.Y+args[1])));
            else if(tail){_position=new(center.X,unchecked((short)(center.Y+18)));_affine=new(recipe.AffineAnimations[0]);}
            else
            {
                bool right=args[1]<2,upper=(args[1]&1)==0;
                _position=new(unchecked((short)(center.X+(right?battler.SourceWidth/2-(upper?8:14):-battler.SourceWidth/2+(upper?8:14)))),unchecked((short)(center.Y-battler.SourceHeight/2+(upper?8:16))));
                _affine=new(recipe.AffineAnimations[right?0:1]);_arc=new(right?20:-20,12,32,-12);
            }
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=new SourceVisualSpriteFrame(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile+(_arc is null?0:4),true,false,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height};
                if(_affine is null)return f;var m=_affine.Matrix;return f with{Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_arc is not null){if(_arc.Step(owner._script.SineTable,out var offset))Finished=true;else _offset=offset;return;}
            if(_destroyNext){Finished=true;return;}
            if(_affine?.Ended??_frames.Ended)_destroyNext=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner){_frames.Step();_affine?.Step();}
    }
}
