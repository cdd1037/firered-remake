namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string SludgeProjectileTemplate="gSludgeProjectileSpriteTemplate",SludgeHitTemplate="gSludgeBombHitParticleSpriteTemplate";
    private bool ValidateSludgeOperand(SourceAnimationCommand c)
    {
        if(c.Op!="createsprite"||c.Args.FirstOrDefault() is not (SludgeProjectileTemplate or SludgeHitTemplate))return false;
        var a=c.Args;bool projectile=a[0]==SludgeProjectileTemplate;
        if(a.Length!=(projectile?7:6)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||Number(a[5]) is <1 or >short.MaxValue||projectile&&Number(a[6]) is <0 or >1)Fail(c,"Sludge source operands differ.");
        if(!_script.SpriteRecipes.TryGetValue(a[0],out var r)||r.Callback!=(projectile?"AnimSludgeProjectile":"AnimSludgeBombHitParticle")||r.Asset!="poison-bubble"||r.Width!=16||r.Height!=16||r.Blend||r.AffineDoubleSize!=projectile||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(projectile?0:8,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{-1})||r.FrameLoops.Any(x=>x.Length!=0)||r.AffineAnimations.Length!=1||_script.SineTable.Length!=320)Fail(c,"Sludge source recipe differs.");
        SourceSpriteAffineCommand[] affine=projectile?[new("frame",352,352,0,0,0),new("frame",-10,-10,0,10,0),new("frame",10,10,0,10,0),new("jump",0,0,0,0,0)]:[new("frame",236,236,0,0,0),new("end",0,0,0,0,0)];
        if(!r!.AffineAnimations[0].SequenceEqual(affine))Fail(c,"Sludge source affine program differs.");
        if(_validatingInvokedProgram)ValidateSludgeSparkCapacity(c);
        return true;
    }
    private void ValidateSludgeSparkCapacity(SourceAnimationCommand c)
    {
        var commands=OverlayInvocationCommands();
        // These closures use counted single-object sprites and non-emitting
        // palette/shake tasks. Count all births without credit for destruction.
        if(commands.Any(x=>x.Op=="createsprite"&&x.Args[0] is not (SludgeProjectileTemplate or SludgeHitTemplate or FixedSparkTemplate or "gPoisonBubbleSpriteTemplate" or "gSparkElectricityFlashingSpriteTemplate" or "gHorizontalLungeSpriteTemplate" or "gBasicHitSplatSpriteTemplate" or "gElectricitySpriteTemplate")||x.Op=="createvisualtask"&&x.Args[0] is not ("AnimTask_ShakeMon" or "AnimTask_ShakeMon2" or "AnimTask_BlendColorCycle")))Fail(c,"Sludge/Spark requires its bounded non-emitting source closure.");
        int births=MaximumInvocationCost(x=>x.Op=="createsprite"?1:0);
        int matrices=MaximumInvocationCost(x=>x.Op=="createsprite"&&(x.Args[0] is FixedSparkTemplate or "gBasicHitSplatSpriteTemplate"||_script.SpriteRecipes.TryGetValue(x.Args[0],out var r)&&r.AffineAnimations.Length>0)?1:0);
        int tasks=1+MaximumInvocationCost(x=>x.Op is "createvisualtask" or "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse"?1:0);
        if(22+births+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>64||4+matrices+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32||tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Sludge/Spark closure exceeds native object, matrix or task capacity.");
    }
    private bool TryCreateSludgeSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (SludgeProjectileTemplate or SludgeHitTemplate))return false;
        if(!_loaded.Contains("ANIM_TAG_POISON_BUBBLE"))Fail(c,"Sludge graphics have not been loaded.");
        _sprites.Add(new SludgeSprite(slot,priority,_script.SpriteRecipes[c.Args[0]],c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private sealed class SludgeSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;
        private readonly PixelPoint _position;private PixelPoint _offset;private readonly HorizontalArc? _arc;
        private readonly short[] _d=new short[8];
        public SludgeSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;
            // Approved safe policy: the source requests animation 2 when args[3]
            // is zero, but this template has only animation 0. Keep that sole
            // legal program; the valid nonzero-mode trajectory is unchanged.
            _frames=new(recipe,0);_affine=new(recipe.AffineAnimations[0]);
            var target=o._battlers[o._cue.Target.Value].Center(o._doubles,true);
            if(recipe.Template==SludgeProjectileTemplate)
            {
                var actor=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);
                _position=new(unchecked((short)(actor.X+o.InitialXDirection()*args[0])),unchecked((short)(actor.Y+args[1])));
                _arc=new(target.X-_position.X,target.Y-_position.Y,args[2],-30);
            }
            else
            {
                _position=target;_d[0]=(short)args[2];
                // InitSpriteDataForLinearTranslation first narrows the 8.8
                // deltas to signed16, then divides with truncation toward zero.
                _d[1]=(short)(unchecked((short)(args[0]<<8))/args[2]);_d[2]=(short)(unchecked((short)(args[1]<<8))/args[2]);
                _d[5]=(short)(_d[1]/args[2]);_d[6]=(short)(_d[2]/args[2]);
            }
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_arc is not null){if(_arc.Step(o._script.SineTable,out var offset))Finished=true;else _offset=offset;return;}
            _d[0]--;_d[3]=unchecked((short)(_d[3]+_d[1]));_d[4]=unchecked((short)(_d[4]+_d[2]));_offset=new(_d[3]>>8,_d[4]>>8);
            _d[1]=unchecked((short)(_d[1]-_d[5]));_d[2]=unchecked((short)(_d[2]-_d[6]));if(_d[0]==0)Finished=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine.Step();}
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,_frames.Frame.HFlip,false,256,_recipe.Asset,Subpriority)
                {VFlip=_frames.Frame.VFlip,Width=16,Height=16,Affine=true,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
    }
}
