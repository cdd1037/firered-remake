namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string FixedSparkTemplate="gSparkElectricitySpriteTemplate";
    private bool ValidateFixedSparkOperand(SourceAnimationCommand c)
    {
        if(c.Op!="createsprite"||c.Args.FirstOrDefault()!=FixedSparkTemplate)return false;
        var a=c.Args;
        if(a.Length!=10||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||Number(a[3]) is <0 or >255||Number(a[5]) is <0 or >255||Number(a[6])<0||Number(a[7]) is <0 or >3||Number(a[8]) is <0 or >1||Number(a[9]) is <0 or >1)Fail(c,"Stationary spark source operands differ.");
        if(!_script.SpriteRecipes.TryGetValue(FixedSparkTemplate,out var r)||r.Callback!="AnimSparkElectricity"||r.Asset!="spark-2"||r.Width!=16||r.Height!=16||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{0})||r.FrameLoops.Any(x=>x.Length!=0)||r.AffineAnimations.Length!=0||_script.SineTable.Length!=320)Fail(c,"Stationary spark source recipe differs.");
        if(_validatingInvokedProgram)ValidateSludgeSparkCapacity(c);
        return true;
    }
    private bool TryCreateFixedSparkSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=FixedSparkTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_SPARK_2"))Fail(c,"Stationary spark graphics have not been loaded.");
        _sprites.Add(new FixedSparkSprite(slot,priority,_script.SpriteRecipes[FixedSparkTemplate],c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private sealed class FixedSparkSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly PixelPoint _position,_offset;private readonly SourceAffineMatrix _matrix;private readonly int _priority;private int _remaining;
        public FixedSparkSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] a,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;int actor=o._cue.Actor.Value,target=o._cue.Target.Value;
            // The original defender-partner selector intentionally checks the
            // attacker's partner visibility. Preserve that source distinction.
            bool partner=o.IsSourceBattlerVisible(actor^2);
            int selected=a[4] switch{0=>actor,2=>partner?actor^2:actor,3=>partner?target^2:target,_=>target};
            var b=o._battlers[selected];_position=a[5]==0?b.Base(o._doubles):b.Center(o._doubles,true);
            _offset=new(unchecked((short)((o._script.SineTable[a[0]]*a[1])>>8)),unchecked((short)((o._script.SineTable[a[0]+64]*a[1])>>8)));
            _priority=(a[6]&1)!=0?(o.BattlerBackgroundPriority(selected)+1)&3:2;
            int sine=o._script.SineTable[a[2]],cosine=o._script.SineTable[a[2]+64];_matrix=new(cosine,sine,-sine,cosine);_remaining=a[3];
        }
        public override void Step(SourceBattleAnimation o){if(_remaining--<=0)Finished=true;}
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,0,true,false,false,256,_recipe.Asset,Subpriority)
        {Width=16,Height=16,Affine=true,MatrixA=_matrix.A,MatrixB=_matrix.B,MatrixC=_matrix.C,MatrixD=_matrix.D,OamPriority=_priority};
    }
}
