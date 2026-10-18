namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string RazorTornadoTemplate="gRazorWindTornadoSpriteTemplate";
    private const string SlideAndBackTemplate="gSlideMonToOffsetAndBackSpriteTemplate";
    private bool ValidateChargePairCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_SkullBashPosition")
        {
            if(a.Length!=3||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >1)Fail(c,"Skull Bash position requires a source setup/reset selector.");
            if(_validatingInvokedProgram&&!IsSourceBattlerPresent(_cue.Actor.Value))Fail(c,"Skull Bash position requires a present attacker sprite.");
            return true;
        }
        if(c.Op!="createsprite"||a.FirstOrDefault() is not (RazorTornadoTemplate or SlideAndBackTemplate))return false;
        bool tornado=a[0]==RazorTornadoTemplate;
        if(a.Length!=(tornado?10:9)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue))Fail(c,"Charge sprite source operands differ.");
        if(!tornado)
        {
            if(Number(a[3]) is <0 or >1||Number(a[6]) is <0 or >1||Number(a[7]) is <1 or >32767||Number(a[8]) is <0 or >1)Fail(c,"Slide-and-back requires a participant, positive duration and source direction/reset flags.");
            if(_validatingInvokedProgram&&!IsSourceBattlerPresent(Resolve(a[3]).Value))Fail(c,"Slide-and-back requires a present battler sprite.");
            return true;
        }
        // TranslateSpriteInCircle performs one wrap, not a modulo operation.
        if(Number(a[7]) is <0 or >255||Number(a[8]) is <-255 or >255||Number(a[9]) is <1 or >32767||_script.SineTable.Length!=320)Fail(c,"Tornado circle phase, speed, duration or source sine table differs.");
        if(!_script.SpriteRecipes.TryGetValue(a[0],out var r)){Fail(c,"Razor tornado source recipe is missing.");return true;}
        SourceSpriteAffineCommand[] affine=[new("frame",16,256,0,0,0),new("frame",4,0,0,40,0),new("end",0,0,0,0,0)];
        if(r.Callback!="AnimRazorWindTornado"||r.Asset!="gust"||r.Width!=32||r.Height!=64||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||!r.LoopStarts.SequenceEqual([0])||r.FrameLoops.Any(p=>p.Length!=0)||r.AffineAnimations.Length!=1||!r.AffineAnimations[0].SequenceEqual(affine))Fail(c,"Razor tornado source frame or affine program differs.");
        return true;
    }
    private bool TryCreateChargePairSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]==SlideAndBackTemplate)
        {
            _oamY[slot]=160;_sprites.Add(new SlideAndBackSprite(slot,priority,this));return true;
        }
        if(c.Args[0]!=RazorTornadoTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_GUST"))Fail(c,"Razor tornado graphics have not been loaded.");
        _sprites.Add(new RazorTornadoSprite(slot,priority,_script.SpriteRecipes[RazorTornadoTemplate],this));return true;
    }
    private bool TryCreateChargePairTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_SkullBashPosition")return false;
        _tasks.Add(new SkullBashPositionTask(this,_animationArgs[0]));return true;
    }
    private sealed class RazorTornadoSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;
        private readonly PixelPoint _position;private PixelPoint _offset;private readonly short[] _data=new short[8];private bool _destroy;
        public RazorTornadoSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);var a=o._animationArgs;var p=o._battlers[o._cue.Actor.Value].Base(o._doubles);
            _position=new(unchecked((short)(p.X+o.InitialXDirection()*a[0])),unchecked((short)(p.Y+a[1]+(o._cue.Actor.IsPlayer?16:0))));
            _data[0]=a[4];_data[1]=a[2];_data[2]=a[5];_data[3]=a[6];_data[4]=a[3];Circle(o);
        }
        private void Circle(SourceBattleAnimation o)
        {
            var d=_data;
            if(d[3]!=0)
            {
                _offset=new(unchecked((short)(o._script.SineTable[d[0]]*d[1]>>8)),unchecked((short)(o._script.SineTable[d[0]+64]*d[1]>>8)));
                d[0]=unchecked((short)(d[0]+d[2]));if(d[0]>=256)d[0]-=256;else if(d[0]<0)d[0]+=256;d[3]--;
            }
            else _destroy=true;
        }
        public override void Step(SourceBattleAnimation o){if(_destroy)Finished=true;else Circle(o);}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine.Step();}
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,f.Tile,!Finished,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=32,Height=64,VFlip=f.VFlip,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
    private sealed class SlideAndBackSprite:VisualSprite
    {
        private readonly int _battler;private readonly PixelPoint _position;private readonly bool _reset;private bool _end;
        private readonly short[] _data=new short[8];
        public SlideAndBackSprite(int slot,int priority,SourceBattleAnimation o):base(slot,priority)
        {
            var a=o._animationArgs;var b=a[0]==0?o._cue.Actor:o._cue.Target;_battler=b.Value;_reset=a[5]!=0;
            _position=o._battlers[o._cue.Target.Value].Center(o._doubles,true);
            if(!b.IsPlayer){a[1]=unchecked((short)-a[1]);if(a[3]==1)a[2]=unchecked((short)-a[2]);}
            var p=o.BattlerPosition(_battler);var offset=o._offsets[_battler];var d=_data;
            d[0]=a[4];d[1]=unchecked((short)(p.X+offset.X));d[2]=unchecked((short)(d[1]+a[1]));d[3]=unchecked((short)(p.Y+offset.Y));d[4]=unchecked((short)(d[3]+a[2]));
            short x=unchecked((short)((d[2]-d[1])<<8)),y=unchecked((short)((d[4]-d[3])<<8));d[1]=(short)(x/d[0]);d[2]=(short)(y/d[0]);
            d[3]=unchecked((short)(offset.X<<8));d[4]=unchecked((short)(offset.Y<<8));d[5]=(short)_battler;
        }
        public override void Step(SourceBattleAnimation o)
        {
            var d=_data;
            if(_end){if(_reset)o._offsets[_battler]=default;Finished=true;return;}
            if(d[0]>0){--d[0];d[3]=unchecked((short)(d[3]+d[1]));d[4]=unchecked((short)(d[4]+d[2]));o._offsets[_battler]=new(d[3]>>8,d[4]>>8);}
            else _end=true;
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,SlideAndBackTemplate,_position,default,0,false,false,false,256,null,Subpriority);
    }
    private sealed class SkullBashPositionTask:VisualTask
    {
        private readonly short[] _data=new short[16];private readonly bool _reset;
        public SkullBashPositionTask(SourceBattleAnimation o,int mode)
        {
            var d=_data;d[0]=(short)o._cue.Actor.Value;d[1]=(short)(o._cue.Actor.IsPlayer?0:1);d[3]=8;_reset=mode==1;
            if(_reset){d[4]=(short)(d[1]==0?-0x600:0x600);d[5]=(short)(d[1]==0?-0xC0:0xC0);}
            else d[5]=(short)(d[1]==0?-3:3);
        }
        private void OffsetX(SourceBattleAnimation o,int x){var p=o._offsets[_data[0]];o._offsets[_data[0]]=new(unchecked((short)x),p.Y);}
        private void Rotate(SourceBattleAnimation o)
        {int slot=_data[0];o.SetBattlerRotScale(slot,256,256,_data[4]);var p=o._offsets[slot];o._offsets[slot]=new(p.X,Math.Abs(o._matrixC[slot])>>3);}
        public override void Step(SourceBattleAnimation o)
        {
            var d=_data;
            if(_reset)
            {
                if(d[3]!=0){d[4]-=d[5];Rotate(o);d[3]--;}
                else{o.ResetBattlerRotScale(d[0]);Finished=true;}
                return;
            }
            switch(d[2])
            {
                case 0:
                    if(d[3]!=0){d[4]+=d[5];OffsetX(o,d[4]);d[3]--;}
                    else{d[3]=8;d[4]=0;d[5]=(short)(d[1]==0?-0xC0:0xC0);o.PrepareBattlerRotScale(d[0],0);d[2]++;}break;
                case 1:
                    if(d[3]!=0){d[4]+=d[5];Rotate(o);d[3]--;}
                    else{d[3]=8;d[4]=(short)o._offsets[d[0]].X;d[5]=(short)(d[1]==0?2:-2);d[6]=1;d[2]++;}break;
                case 2:
                    if(d[3]!=0)
                    {if(d[6]!=0)d[6]--;else{OffsetX(o,d[4]+((d[3]&1)!=0?d[5]:-d[5]));d[6]=1;d[3]--;}}
                    else{OffsetX(o,d[4]);d[3]=12;d[2]++;}break;
                case 3:if(d[3]!=0)d[3]--;else{d[3]=3;d[4]=(short)o._offsets[d[0]].X;d[5]=(short)(d[1]==0?8:-8);d[2]++;}break;
                case 4:if(d[3]!=0){d[4]+=d[5];OffsetX(o,d[4]);d[3]--;}else Finished=true;break;
            }
        }
    }
}
