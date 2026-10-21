namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string ReversalOrbTemplate="gReversalOrbSpriteTemplate",ProtectTemplate="gProtectSpriteTemplate",ShadowBallTemplate="gShadowBallSpriteTemplate",RandomImpactTemplate="gRandomPosHitSplatSpriteTemplate";
    private bool ValidatePowerGuardCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="loadspritegfx"&&name is "ANIM_TAG_BLUE_ORB" or "ANIM_TAG_PROTECT" or "ANIM_TAG_SHADOW_BALL")
        {if(a.Length!=1)Fail(c,"Power/guard graphics operands differ.");return true;}
        if(c.Op=="createvisualtask"&&name=="AnimTask_FlailMovement")
        {
            if(a.Length!=3||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >1)Fail(c,"Flail movement requires a bounded source participant.");
            return true;
        }
        if(c.Op!="createsprite"||name is not (ReversalOrbTemplate or ProtectTemplate or ShadowBallTemplate or RandomImpactTemplate))return false;
        int count=name is ProtectTemplate or ShadowBallTemplate?6:5;
        if(a.Length!=count||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Power/guard sprite ABI differs.");
        if(name==ReversalOrbTemplate&&(Number(a[3]) is <1 or >255||Number(a[4]) is <0 or >255||_script.SineTable.Length!=320))Fail(c,"Reversal orb requires a bounded duration, phase and source sine table.");
        if(name==ProtectTemplate&&Number(a[5])<2)Fail(c,"Protect requires at least two hold ticks so the source alpha counter can return to sixteen.");
        if(name==ShadowBallTemplate&&a.Skip(3).Any(v=>Number(v)<1))Fail(c,"Shadow Ball requires positive source phases and nonzero divisors.");
        if(name==RandomImpactTemplate)
        {if(Number(a[3]) is <0 or >1||Number(a[4]) is < -1 or >3)Fail(c,"Random impact participant or affine selector differs.");RequireVisualSeed(c);}
        ValidatePowerGuardRecipe(c,name);return true;
    }
    private void ValidatePowerGuardRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Power/guard source recipe is missing.");r=_script.SpriteRecipes[name];
        var expected=name switch{ReversalOrbTemplate=>("blue-orb","AnimReversalOrb",8,false),ProtectTemplate=>("protect","AnimProtect",64,true),ShadowBallTemplate=>("shadow-ball","AnimShadowBall",32,false),_=>("impact","AnimHitSplatRandom",32,true)};
        if(r.Asset!=expected.Item1||r.Callback!=expected.Item2||r.Width!=expected.Item3||r.Height!=expected.Item3||r.Blend!=expected.Item4||r.AffineDoubleSize||r.FrameLoops.Any(x=>x.Length!=0)||r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||!r.LoopStarts.SequenceEqual([0]))Fail(c,"Power/guard source template or frame program differs.");
        SourceSpriteAffineCommand[][] affine=[];
        if(name==ShadowBallTemplate)affine=[[new("frame",0,0,10,1,0),new("jump",0,0,0,0,0)]];
        if(name==RandomImpactTemplate)
        {SourceSpriteAffineCommand[] tail=[new("frame",0,0,0,8,0),new("end",0,0,0,0,0)];affine=[tail,..new[]{216,176,128}.Select(s=>(SourceSpriteAffineCommand[])[new("frame",s,s,0,0,0),..tail])];}
        if(r.AffineAnimations.Length!=affine.Length)Fail(c,"Power/guard affine table shape differs.");
        for(int i=0;i<affine.Length;i++)if(!r.AffineAnimations[i].SequenceEqual(affine[i]))Fail(c,"Power/guard affine program differs.");
        if(name==ProtectTemplate)RequireOverlaySpritePalette(c,"ANIM_TAG_PROTECT");
    }
    private bool TryCreatePowerGuardTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_FlailMovement")return false;
        _tasks.Add(new FlailMovementTask(Resolve(c.Args[2]).Value,this));return true;
    }
    private sealed class FlailMovementTask:VisualTask
    {
        private readonly int _slot;private short _state,_ticks,_rotation,_remaining=32,_limit=0x800;
        public FlailMovementTask(int slot,SourceBattleAnimation o){_slot=slot;o.PrepareBattlerRotScale(slot,0);}
        public override void Step(SourceBattleAnimation o)
        {
            if(_state==2){o.ResetBattlerRotScale(_slot);Finished=true;return;}
            if(_state==0)
            {
                _rotation+=0x200;
                if(_rotation>=_limit){short diff=(short)(_limit-_rotation),div=(short)(diff/(_limit*2)),mod=(short)(diff%(_limit*2));if((div&1)==0){_rotation=(short)(_limit-mod);_state=1;}else _rotation=(short)(mod-_limit);}
            }
            else
            {
                _rotation-=0x200;
                if(_rotation<=-_limit){short diff=(short)(_limit-_rotation),div=(short)(diff/(_limit*2)),mod=(short)(diff%(_limit*2));if((div&1)==0){_rotation=(short)(mod-_limit);_state=0;}else _rotation=(short)(_limit-mod);}
            }
            o.SetBattlerRotScale(_slot,256,256,_rotation);
            o._offsets[_slot]=new(-(_rotation/64),Math.Abs(o._matrixC[_slot])>>3);
            if(++_ticks>8){if(_remaining!=0){_remaining--;_limit=(short)Math.Max(16,_limit-64);}else _state=2;}
        }
    }
    private bool TryCreatePowerGuardSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (ReversalOrbTemplate or ProtectTemplate or ShadowBallTemplate or RandomImpactTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Power/guard sprite graphics have not been loaded.");
        if(r.Template==ProtectTemplate&&FindSpritePalette(_script.SpriteTags["ANIM_TAG_PROTECT"])==255)Fail(c,"Protect requires an allocated source palette bank.");
        _sprites.Add(new PowerGuardSprite(slot,priority,r,Number(c.Args[1]),this));return true;
    }
    private sealed class PowerGuardSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private readonly short[] _data=new short[8];private PixelPoint _position,_offset;private int _priority,_oamPriority=2,_paletteSlot;private bool _invisible,_destroyNext;
        public PowerGuardSprite(int slot,int priority,SourceSpriteRecipe r,int participant,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_priority=priority;_frames=new(r,0);var a=o._animationArgs;var actor=o._battlers[o._cue.Actor.Value];
            _position=o._battlers[(participant==0?o._cue.Actor:o._cue.Target).Value].Center(o._doubles,true);
            switch(r.Template)
            {
                case ReversalOrbTemplate:_position=actor.Center(o._doubles,true);_data[0]=a[0];_data[1]=a[1];Step(o);break;
                case ProtectTemplate:
                    var p=actor.Base(o._doubles);_position=new(unchecked((short)(p.X+a[0])),unchecked((short)(p.Y+a[1])));_oamPriority=(o.BattlerBackgroundPriority(actor.Slot.Value)+(actor.Slot.IsPlayer?1:0))&3;
                    _data[0]=a[2];_data[7]=16;_paletteSlot=o.FindSpritePalette(o._script.SpriteTags["ANIM_TAG_PROTECT"]);_data[2]=(short)((_paletteSlot+16)*16);o.SetAlphaBlend(0,63,0,16);break;
                case ShadowBallTemplate:
                    var old=_position;_position=actor.Center(o._doubles,true);_data[1]=a[0];_data[2]=a[1];_data[3]=a[2];_data[4]=unchecked((short)(_position.X<<4));_data[5]=unchecked((short)(_position.Y<<4));
                    _data[6]=unchecked((short)(((old.X-_position.X)<<4)/(a[0]<<1)));_data[7]=unchecked((short)(((old.Y-_position.Y)<<4)/(a[0]<<1)));_affine=new(r.AffineAnimations[0]);break;
                case RandomImpactTemplate:
                    if(a[1]==-1)a[1]=(short)(o.NextVisualRandom()&3);_affine=new(r.AffineAnimations[a[1]]);
                    var b=o._battlers[(a[0]==0?o._cue.Actor:o._cue.Target).Value].Base(o._doubles);_position=new(unchecked((short)(b.X+o.InitialXDirection()*a[0])),unchecked((short)(b.Y+a[1])));
                    _offset=new(o.NextVisualRandom()%48-24,o.NextVisualRandom()%24-12);break;
            }
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible,f.HFlip,_recipe.Blend,256,_recipe.Asset,_priority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_affine is not null,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D,OamPriority=_oamPriority};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            var d=_data;
            switch(_recipe.Template)
            {
                case ReversalOrbTemplate:
                    _offset=new(o._script.SineTable[d[1]]*(d[2]>>8)>>8,o._script.SineTable[d[1]+64]*(d[3]>>8)>>8);d[1]=(short)((d[1]+9)&255);
                    _priority=unchecked((byte)(o._battlers[o._cue.Actor.Value].Subpriority+((ushort)d[1]<64||d[1]>195?-1:1)));
                    if(d[5]==0){d[2]=unchecked((short)(d[2]+0x400));d[3]=unchecked((short)(d[3]+0x100));if(++d[4]==d[0]){d[4]=0;d[5]=1;}}
                    else if(d[5]==1){d[2]=unchecked((short)(d[2]-0x400));d[3]=unchecked((short)(d[3]-0x100));if(++d[4]==d[0])Finished=true;}break;
                case ProtectTemplate:
                    if(_destroyNext){o.ClearAlphaBlend();Finished=true;return;}
                    d[5]=unchecked((short)(d[5]+96));_offset=new(-(d[5]>>8),0);
                    if(++d[1]>1){d[1]=0;var palette=o._spritePaletteBanks[_paletteSlot];ushort first=palette[1];Array.Copy(palette,2,palette,1,6);palette[7]=first;}
                    if(d[7]>6&&d[0]>0&&++d[6]>1){d[6]=0;d[7]--;o._alphaA=16-d[7];o._alphaB=d[7];}
                    if(d[0]>0)d[0]--;else if(++d[6]>1){d[6]=0;d[7]++;o._alphaA=16-d[7];o._alphaB=d[7];if(d[7]==16){_invisible=true;_destroyNext=true;}}break;
                case ShadowBallTemplate:
                    if(d[0]==0){d[4]=unchecked((short)(d[4]+d[6]));d[5]=unchecked((short)(d[5]+d[7]));_position=new(d[4]>>4,d[5]>>4);if(--d[1]<=0)d[0]++;}
                    else if(d[0]==1)
                    {
                        if(--d[2]>0)break;var target=o._battlers[o._cue.Target.Value].Center(o._doubles,true);d[1]=(short)target.X;d[2]=(short)target.Y;d[4]=unchecked((short)(_position.X<<4));d[5]=unchecked((short)(_position.Y<<4));
                        d[6]=unchecked((short)(((d[1]-_position.X)<<4)/d[3]));d[7]=unchecked((short)(((d[2]-_position.Y)<<4)/d[3]));d[0]++;
                    }
                    else if(d[0]==2){d[4]=unchecked((short)(d[4]+d[6]));d[5]=unchecked((short)(d[5]+d[7]));_position=new(d[4]>>4,d[5]>>4);if(--d[3]<=0){_position=o._battlers[o._cue.Target.Value].Center(o._doubles,true);d[0]++;}}
                    else Finished=true;break;
                case RandomImpactTemplate:if(_destroyNext)Finished=true;else if(_affine!.Ended)_destroyNext=true;break;
            }
        }
    }
}
