namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string PetalBig="gPetalDanceBigFlowerSpriteTemplate",PetalSmall="gPetalDanceSmallFlowerSpriteTemplate",
        IceChunk="gIceBallChunkSpriteTemplate",IceShard="gIceBallImpactShardSpriteTemplate";
    private bool ValidatePetalIceOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="createvisualtask"&&name=="AnimTask_GetRolloutCounter")
        {
            if(a.Length!=3||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >7)Fail(c,"Rollout counter task register differs.");
            if(_validatingInvokedProgram&&_cue.RolloutCounter is not (>=1 and <=5))Fail(c,"Ice Ball requires the owned postdecrement Rollout counter.");
            return true;
        }
        if(c.Op!="createsprite"||name is not (PetalBig or PetalSmall or IceChunk or IceShard))return false;
        bool flower=name is PetalBig or PetalSmall,big=name==PetalBig,chunk=name==IceChunk;
        if(a.Length!=(flower?7:chunk?9:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Petal/Ice source operand widths differ.");
        if((flower&&Number(a[6])<=0)||(chunk&&Number(a[7])<=0)||_script.SineTable.Length!=320)Fail(c,"Petal/Ice linear duration or sine table differs.");
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Petal/Ice source recipe missing.");return true;}
        if(r.Asset!=(flower?"flower":chunk?"ice-chunk":"ice-crystals")||r.Callback!=(big?"AnimPetalDanceBigFlower":flower?"AnimPetalDanceSmallFlower":chunk?"InitIceBallAnim":"InitIceBallParticle")||r.Width!=(big?16:chunk?32:8)||r.Height!=r.Width||r.Blend||r.AffineDoubleSize!=chunk||r.Subsprites.Length!=0||r.FrameLoops.Any(p=>p.Length!=0))Fail(c,"Petal/Ice source shape differs.");
        SourceSpriteAnimFrame F(int tile,int duration)=>new(tile,duration,false,false);
        SourceSpriteAnimFrame[][] frames=chunk?[[F(0,1)],[F(16,4),F(32,4),F(48,4),F(64,4)]]:[[F(big?0:flower?4:6,1)]];
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(-1,frames.Length))||r.AffineAnimations.Length!=(chunk?5:0))Fail(c,"Petal/Ice frame or affine table differs.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Petal/Ice frame commands differ.");
        if(chunk)
        {
            int[] scales=[224,280,336,384,448];
            for(int i=0;i<5;i++)if(!r.AffineAnimations[i].SequenceEqual(new SourceSpriteAffineCommand[]{new("frame",scales[i],scales[i],0,0,0),new("end",0,0,0,0,0)}))Fail(c,"Ice chunk source scale differs.");
            if(_validatingInvokedProgram&&_cue.RolloutCounter is not (>=1 and <=5))Fail(c,"Ice chunk requires the owned postdecrement Rollout counter.");
        }
        if(name==IceShard)RequireVisualSeed(c);
        return true;
    }
    private bool TryCreatePetalIceSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (PetalBig or PetalSmall or IceChunk or IceShard))return false;
        var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Petal/Ice source graphics are not loaded.");
        _sprites.Add(new PetalIceSprite(slot,priority,r,this));return true;
    }
    private bool TryRunPetalIceTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_GetRolloutCounter")return false;
        int register=_animationArgs[0];_animationArgs[register]=(short)(_cue.RolloutCounter!.Value-1);return true;
    }
    private sealed class PetalIceSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private SourceSpriteFrameProgram _frames;
        private readonly SourceSpriteAffineProgram? _affine;private readonly PhysicalLinear? _linear;private readonly HorizontalArc? _arc;
        private readonly PixelPoint _position;private PixelPoint _offset;
        private short _vx,_vy,_x,_y,_ticks;private int _angle=64,_phase,_renderPriority;private bool _hFlip,_beginFrames;
        public PetalIceSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_renderPriority=priority;var a=owner._animationArgs;
            var actor=owner._battlers[owner._cue.Actor.Value];var target=owner._battlers[owner._cue.Target.Value];
            bool flower=recipe.Template is PetalBig or PetalSmall,chunk=recipe.Template==IceChunk;
            var p=flower&&recipe.Template==PetalBig?actor.Base(owner._doubles):chunk||flower?actor.Center(owner._doubles,true):target.Center(owner._doubles,true);
            _position=new(unchecked((short)(p.X+owner.InitialXDirection()*a[0])),unchecked((short)(p.Y+a[1])));
            if(flower)
            {
                int y=unchecked((short)(actor.Center(owner._doubles,true).Y+a[2]));_linear=new(0,y-_position.Y,a[3]);FloatFlower(owner);
            }
            else if(chunk)
            {
                if(!owner._cue.Actor.IsPlayer)a[2]=unchecked((short)-a[2]);
                var end=target.Center(owner._doubles,true);int x=unchecked((short)(end.X+a[2])),y=unchecked((short)(end.Y+a[3]));
                _arc=new(x-_position.X,y-_position.Y,a[4],a[5]);_affine=new(recipe.AffineAnimations[owner._cue.RolloutCounter!.Value-1]);
            }
            else
            {
                _vx=(short)((owner.NextVisualRandom()&255)+256);int vertical=owner.NextVisualRandom()&511;_vy=(short)(vertical>255?256-vertical:vertical);
                // Native tileNum += 8 precedes BeginAnim, which replaces it with
                // sheetTileStart + the source small-crystal frame (tile 6).
            }
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=_frames.Frame;var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);
                return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,_hFlip^f.HFlip,false,256,_recipe.Asset,_renderPriority)
                {Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {if(_beginFrames)_beginFrames=false;else _frames.Step();_affine?.Step();}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_linear is not null){FloatFlower(owner);return;}
            if(_arc is null)
            {
                _x=unchecked((short)(_x+_vx));_y=unchecked((short)(_y+_vy));_offset=new((_vx&1)!=0?-(_x>>8):_x>>8,_y>>8);
                if(++_ticks==21)Finished=true;return;
            }
            switch(_phase)
            {
                case 0:
                    if(_arc.Step(owner._script.SineTable,out var offset)){_frames=new(_recipe,1);_beginFrames=true;_phase=1;}else _offset=offset;
                    break;
                case 1:if(_frames.Ended)_phase=2;break;
                case 2:Finished=true;break;
            }
        }
        private void FloatFlower(SourceBattleAnimation owner)
        {
            if(_linear!.Step(out var offset)){Finished=true;return;}
            bool big=_recipe.Template==PetalBig;var sine=owner._script.SineTable;
            _offset=new(unchecked((short)(offset.X+(sine[_angle]*(big?32:8)>>8))),unchecked((short)(offset.Y+(big?sine[_angle+64]*-5>>8:0))));
            if(big)_renderPriority=owner._battlers[owner._cue.Actor.Value].Subpriority+(unchecked((ushort)(_angle-64))<128?-1:1);
            else if(unchecked((ushort)(_angle-59))<5||unchecked((ushort)(_angle-187))<5)_hFlip=!_hFlip;
            _angle=(_angle+5)&255;
        }
    }
}
