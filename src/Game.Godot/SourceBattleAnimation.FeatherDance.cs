namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string FallingFeatherTemplate="gFallingFeatherSpriteTemplate";
    private bool ValidateFeatherDanceCommand(SourceAnimationCommand c)
    {
        if(c.Op!="createsprite"||c.Args.FirstOrDefault()!=FallingFeatherTemplate)return false;
        var a=c.Args;
        if(a.Length!=11||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >ushort.MaxValue))Fail(c,"Falling feather source operands differ.");
        if(!_script.SpriteRecipes.TryGetValue(FallingFeatherTemplate,out var r)||r.Callback!="AnimFallingFeather"||r.Asset!="white-feather"||r.Width!=32||r.Height!=32||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.AffineAnimations.Length!=0||r.Animations.Length!=2||!r.LoopStarts.SequenceEqual(new[]{-1,-1})||r.FrameLoops.Any(x=>x.Length!=0)||_script.SineTable.Length!=320)Fail(c,"Falling feather source recipe differs.");
        if(!r!.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,0,false,false)})||!r.Animations[1].SequenceEqual(new[]{new SourceSpriteAnimFrame(16,0,true,false)}))Fail(c,"Falling feather frame programs differ.");
        if(!_script.SpriteTags.TryGetValue("ANIM_TAG_WHITE_FEATHER",out int tag)||!_script.SpritePalettes.TryGetValue(tag,out var palette)||palette.Length!=16)Fail(c,"Falling feather indexed material is missing.");
        // Reviewed bounded falling interval; the native accumulator is unsigned16.
        // Other offsets, angle/tilt, direction and amplitude bytes retain native widths.
        if(Number(a[4]) is < -32 or >0||Number(a[7]) is <1 or >4096||Number(a[9]) is <1 or >96)Fail(c,"Falling feather vertical interval is outside the bounded source closure.");
        if(_validatingInvokedProgram)
        {
            var commands=OverlayInvocationCommands();
            if(commands.Any(x=>x.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"||x.Op=="createvisualtask"||x.Op=="createsprite"&&x.Args[0]!=FallingFeatherTemplate))Fail(c,"Falling feather needs its reviewed non-emitting matrix closure.");
            int births=commands.Count(x=>x.Op=="createsprite");
            if(22+births+_spriteScene.Sprites.Sum(x=>Math.Max(1,x.Frame.Subsprites.Length))>64||4+births+_spriteScene.Sprites.Count(x=>x.Frame.Affine||x.Frame.Scale!=256)>32)Fail(c,"Falling feather exceeds source OAM or affine capacity.");
        }
        return true;
    }
    private bool TryCreateFeatherDanceSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=FallingFeatherTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_WHITE_FEATHER"))Fail(c,"Falling feather graphics have not been loaded.");
        _sprites.Add(new FallingFeatherSprite(slot,priority,_script.SpriteRecipes[FallingFeatherTemplate],c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private sealed class FallingFeatherSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private SourceSpriteFrameProgram _frames;
        private PixelPoint _position,_offset;private SourceAffineMatrix _matrix;
        private ushort _angle,_vertical,_fall,_tilt;private readonly short _angular;private readonly byte[] _amplitudes;private readonly int _bottom;
        private int _quadrant,_amplitude,_priority;private bool _pause,_flipPending,_front,_hflip,_restart,_destroyNext;
        public FallingFeatherSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;
            int battler=(args[7]&0x100)!=0?o._cue.Actor.Value:o._cue.Target.Value;
            // The source passes ATTR_HEIGHT/WIDTH (0/1) to GetBattlerSpriteCoord,
            // so these are base X/Y, not dimensions or picture/elevation offsets.
            if((battler&1)==0)o._animationArgs[0]=unchecked((short)-args[0]);
            int x=(battler&1)==0?unchecked((short)-args[0]):unchecked((short)args[0]);
            var p=o._battlers[battler].Base(o._doubles);_position=new(unchecked((short)(p.X+x)),unchecked((short)(p.Y+args[1])));
            _vertical=unchecked((ushort)(_position.Y<<8));_bottom=(p.Y+args[6])&0x7fff;
            _angle=(ushort)(args[2]&255);_tilt=(ushort)((args[2]>>8)&255);_angular=unchecked((short)args[3]);_fall=unchecked((ushort)args[4]);_amplitudes=[(byte)(args[5]&255),(byte)((args[5]>>8)&255)];
            bool back=_angle is >=64 and <=191;_priority=(o.BattlerBackgroundPriority(battler)+(back?1:0))&3;_front=!back;
            _hflip=back?(_angular&0x8000)==0:(_angular&0x8000)!=0;_quadrant=_angle>>6;
            _frames=new(recipe,_hflip?1:0);UpdateMatrix(o);
        }
        private void UpdateMatrix(SourceBattleAnimation o)
        {
            _offset=new((_amplitudes[_amplitude]*o._script.SineTable[_angle])>>8,0);
            int angle=(( -_offset.X>>1)+_tilt)&255,sin=o._script.SineTable[angle],cos=o._script.SineTable[angle+64];_matrix=new(cos,sin,-sin,cos);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_destroyNext){Finished=true;return;}
            // Source pause byte always starts at zero, so each crossing pauses once.
            if(_pause){_pause=false;return;}
            int quadrant=_angle/64;
            if((quadrant==0&&_quadrant==1)||(quadrant==1&&_quadrant==0)||(quadrant==2&&_quadrant==3))
            {_flipPending=true;_pause=true;}
            else if((quadrant==0&&_quadrant==3)||(quadrant==3&&_quadrant==0))
            {_amplitude^=1;_pause=true;}
            else if((quadrant==1&&_quadrant==2)||(quadrant==2&&_quadrant==1))_pause=true;
            else if(quadrant==3&&_quadrant==2)_flipPending=true;
            else if(_flipPending)
            {
                _hflip=!_hflip;_frames=new(_recipe,_hflip?1:0);_restart=true;
                _priority=(_priority+(_front?1:-1))&3;_front=!_front;_flipPending=false;
            }
            _quadrant=quadrant;UpdateMatrix(o);
            _vertical=unchecked((ushort)(_vertical+_fall));_position=new(_position.X,_vertical>>8);
            _angle=(ushort)((_angle+((_angular&0x8000)!=0?-(_angular&0x7fff):_angular))&255);
            if(_position.Y>=_bottom)_destroyNext=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_restart)_restart=false;else _frames.Step();}
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!Finished,_hflip,false,256,_recipe.Asset,Subpriority)
        {Width=32,Height=32,Affine=true,MatrixA=_matrix.A,MatrixB=_matrix.B,MatrixC=_matrix.C,MatrixD=_matrix.D,OamPriority=_priority};
    }
}
