namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string AssistPawTemplate="gAssistPawprintSpriteTemplate",SweetPetalTemplate="gSweetScentPetalSpriteTemplate";
    private bool ValidatePetalPawOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op!="createsprite"||name is not (AssistPawTemplate or SweetPetalTemplate))return false;
        bool paw=name==AssistPawTemplate;
        if((paw?a.Length!=8:a.Length is not (5 or 6))||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue))Fail(c,"Petal/paw source operands differ.");
        if(paw&&Number(a[7])<=0||!paw&&(Number(a[4]) is <0 or >2||_script.SineTable.Length!=320))Fail(c,"Petal/paw duration or frame selector differs.");
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Petal/paw source recipe missing.");return true;}
        if(r.Callback!=(paw?"AnimAssistPawprint":"AnimSweetScentPetal")||r.Asset!=(paw?"paw-print":"pink-petal")||r.Width!=(paw?32:8)||r.Height!=r.Width||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.AffineAnimations.Length!=0||r.FrameLoops.Length>r.Animations.Length||r.FrameLoops.Any(p=>p.Length!=0))Fail(c,"Petal/paw source template differs.");
        SourceSpriteAnimFrame F(int tile,bool h=false,bool v=false)=>new(tile,paw?1:8,h,v);
        SourceSpriteAnimFrame[] Petals(bool h)=>[F(0,h),F(1,h),F(2,h),F(3,h),F(3,h,true),F(2,h,true),F(0,h,true),F(1,h,true)];
        SourceSpriteAnimFrame[][] expected=paw?[[F(0)]]:[Petals(false),Petals(true),[F(0)]];
        if(r.Animations.Length!=expected.Length||!r.LoopStarts.SequenceEqual(paw?new[]{0}:new[]{0,0,-1}))Fail(c,"Petal/paw frame program differs.");
        for(int i=0;i<expected.Length;i++)if(!r.Animations[i].SequenceEqual(expected[i]))Fail(c,"Petal/paw frame commands differ.");
        return true;
    }
    private bool TryCreatePetalPawSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (AssistPawTemplate or SweetPetalTemplate))return false;
        var recipe=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Petal/paw graphics are not loaded.");
        _sprites.Add(new PetalPawSprite(slot,priority,recipe,this));return true;
    }
    private sealed class PetalPawSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteFrameProgram _frames;
        private readonly SourceFastLinear? _linear;
        private PixelPoint _position,_offset;
        private short _angle;
        private bool _destroyNext;
        public PetalPawSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;var a=owner._animationArgs;bool paw=recipe.Template==AssistPawTemplate;
            _frames=new(recipe,paw?0:a[1]);
            if(paw)
            {
                _position=new(a[0],a[1]);_linear=SourceFastLinear.FromDuration(a[2]-a[0],a[3]-a[1],a[4]);
                // AnimAssistPawprint only installs InitAndRun: no translation
                // on creation. The next callback initializes and moves once.
            }
            else _position=new(owner._cue.Actor.IsPlayer?0:240,unchecked((short)(a[0]-(owner._cue.Actor.IsPlayer?0:30))));
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_linear is not null)
            {
                if(_destroyNext){Finished=true;return;}
                if(_linear.Step(out var offset))_destroyNext=true;else _offset=offset;
                return;
            }
            _angle=unchecked((short)(_angle+3));bool player=owner._cue.Actor.IsPlayer;
            _position=new(unchecked((short)(_position.X+(player?5:-5))),unchecked((short)(_position.Y+(player?-1:1))));
            if(player?_position.X>240:_position.X<0){Finished=true;return;}
            int index=(_angle&255)+(player?0:64);_offset=new(0,(owner._script.SineTable[index]*16)>>8);
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip};}}
    }
}
