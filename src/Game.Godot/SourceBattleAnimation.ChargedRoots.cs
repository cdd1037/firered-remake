namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string SkyAttackBirdTemplate="gSkyAttackBirdSpriteTemplate";
    private const string SpitUpOrbTemplate="gSpitUpOrbSpriteTemplate";
    private bool ValidateChargedRootCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op!="createsprite"||name is not (SkyAttackBirdTemplate or SpitUpOrbTemplate))return false;
        bool bird=name==SkyAttackBirdTemplate;
        if((bird?a.Length!=3:a.Length is not (4 or 5))||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||
            !bird&&(Number(a[3]) is <0 or >255||a.Length==5&&Number(a[4]) is <0 or >255))Fail(c,"Charged-root sprite operands differ.");
        if(_validatingInvokedProgram&&(!IsSourceBattlerPresent(_cue.Actor.Value)||bird&&_battlers[_cue.Actor.Value].Center(_doubles,true)==_battlers[_cue.Target.Value].Center(_doubles,true)))Fail(c,"Charged-root sprite requires a present actor and nonzero bird velocity.");
        if(!bird&&_script.SineTable.Length!=320)Fail(c,"Spit Up requires the source sine table.");
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Charged-root recipe is missing.");return true;}
        int size=bird?64:8;
        if(r.Callback!=(bird?"AnimSkyAttackBird":"AnimSpitUpOrb")||r.Asset!=(bird?"bird":"red-orb-2")||r.Width!=size||r.Height!=size||r.Blend||!r.AffineDoubleSize||r.Subsprites.Length!=0||
            r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||!r.LoopStarts.SequenceEqual([0])||r.FrameLoops.Any(p=>p.Length!=0)||
            r.AffineAnimations.Length!=(bird?0:1))Fail(c,"Charged-root source template differs.");
        if(!bird&&!r.AffineAnimations[0].SequenceEqual([new("frame",128,128,0,0,0),new("frame",8,8,0,1,0),new("jump",0,0,0,0,1)]))Fail(c,"Spit Up growing affine program differs.");
        return true;
    }
    private bool TryCreateChargedRootSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (SkyAttackBirdTemplate or SpitUpOrbTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Charged-root graphics have not been loaded.");
        _sprites.Add(new ChargedRootSprite(slot,priority,r,this));return true;
    }
    private sealed class ChargedRootSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private readonly short[] _data=new short[8];private readonly SourceAffineMatrix _matrix=new(256,0,0,256);private PixelPoint _position,_offset;
        public ChargedRootSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_position=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);
            if(recipe.Template==SkyAttackBirdTemplate)
            {
                // Cmd_createsprite always seeds target picture coordinates. Its
                // participant operand selects only subpriority, not this endpoint.
                var end=o._battlers[o._cue.Target.Value].Center(o._doubles,true);
                _data[4]=unchecked((short)(_position.X<<4));_data[5]=unchecked((short)(_position.Y<<4));
                _data[6]=unchecked((short)(((end.X-_position.X)<<4)/12));_data[7]=unchecked((short)(((end.Y-_position.Y)<<4)/12));
                int rotation=unchecked((ushort)(SourceBiosAngles.ArcTan2Neg(unchecked((short)(end.X-_position.X)),unchecked((short)(end.Y-_position.Y)))+49152));
                _matrix=SourceAffineMatrix.FromScaleRotation(256,256,rotation);
            }
            else
            {
                var a=o._animationArgs;_data[0]=unchecked((short)((o._script.SineTable[a[0]]*10)>>8));_data[1]=unchecked((short)((o._script.SineTable[a[0]+64]*7)>>8));
                // Stronger branches overwrite only arg0. Source arg1 remains the
                // preceding orb's duration; do not zero or invent that register.
                _data[2]=a[1];_affine=new(recipe.AffineAnimations[0]);
            }
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??_matrix;return new(Slot,_recipe.Template,_position,_offset,f.Tile,!Finished,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=true,DoubleSize=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==SkyAttackBirdTemplate)
            {
                _data[4]=unchecked((short)(_data[4]+_data[6]));_data[5]=unchecked((short)(_data[5]+_data[7]));_position=new(_data[4]>>4,_data[5]>>4);
                if(_position.X is >285 or <-45||_position.Y is >157 or <-45)Finished=true;
            }
            else
            {
                _offset=new(unchecked((short)(_offset.X+_data[0])),unchecked((short)(_offset.Y+_data[1])));short before=_data[3];_data[3]=unchecked((short)(_data[3]+1));if(before>=_data[2])Finished=true;
            }
        }
    }
}
