namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string MimicOrbTemplate="gMimicOrbSpriteTemplate";
    private bool ValidateBattlerCopyOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="createsprite"&&name==MimicOrbTemplate)
        {
            if(a.Length!=5||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue)||!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Mimic orb operands or recipe differ.");
            r=_script.SpriteRecipes[name];SourceSpriteAffineCommand[][] expected=[[new("frame",0,0,0,0,0),new("frame",48,48,0,14,0),new("end",0,0,0,0,0)],[new("frame",-16,-16,0,1,0),new("jump",0,0,0,0,0)]];
            if(r.Asset!="orbs"||r.Callback!="AnimMimicOrb"||r.Width!=16||r.Height!=16||r.Blend||!r.AffineDoubleSize||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(8,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{-1})||r.FrameLoops.Any(p=>p.Length!=0)||r.AffineAnimations.Length!=2||!r.AffineAnimations[0].SequenceEqual(expected[0])||!r.AffineAnimations[1].SequenceEqual(expected[1]))Fail(c,"Mimic orb source frame/affine programs differ.");
            return true;
        }
        if(c.Op!="createvisualtask"||name is not ("AnimTask_NightShadeClone" or "AnimTask_DoubleTeam" or "AnimTask_ShrinkTargetCopy"))return false;
        int length=name=="AnimTask_DoubleTeam"?2:name=="AnimTask_NightShadeClone"?3:4;
        if(a.Length!=length||Number(a[1]) is <0 or >255)Fail(c,"Battler copy task operand shape differs.");
        if(name=="AnimTask_NightShadeClone"&&Number(a[2]) is <0 or >32767)Fail(c,"Night Shade hold must be a bounded source word.");
        if(name=="AnimTask_ShrinkTargetCopy"&&(Number(a[2]) is <short.MinValue or >short.MaxValue||Number(a[3]) is <1 or >1023))Fail(c,"Mimic shrink must terminate before signed scale overflow.");
        if(name=="AnimTask_DoubleTeam"&&(!_script.SpriteTags.ContainsKey("ANIM_TAG_BENT_SPOON")||_script.SineTable.Length!=320))Fail(c,"Double Team needs its allocated palette tag and source sine table.");
        if(_validatingInvokedProgram)
        {
            var closure=OverlayInvocationCommands();
            if(name=="AnimTask_ShrinkTargetCopy")
            {
                int at=Array.IndexOf(closure.ToArray(),c);
                if(at<0||!closure.Skip(at+1).TakeWhile(x=>x.Op!="waitforvisualfinish").Any(x=>x.Op=="setarg"&&x.Args.Length==2&&x.Args[0]=="7"&&AnimationArgument(x.Args[1])==65535))Fail(c,"Mimic shrink needs its restore signal before the visual wait.");
            }
            if(name=="AnimTask_DoubleTeam")
            {
                // A source clone copies all battler sprite fields. This root has
                // no affine, visibility, form, translation or palette companions.
                if(closure.Any(x=>x.Op=="createsprite"||x.Op=="loadspritegfx"||x.Op=="createvisualtask"&&x.Args[0]!="AnimTask_DoubleTeam")||closure.Count(x=>x.Op=="createvisualtask"&&x.Args[0]==name)!=1)Fail(c,"Double Team requires one reviewed raw-clone owner.");
                if(FindSpritePalette(65535)==255)Fail(c,"Double Team requires a free source palette bank.");
            }
        }
        return true;
    }
    private bool TryCreateBattlerCopyTask(SourceAnimationCommand c)
    {
        switch(c.Args[0])
        {
            case "AnimTask_NightShadeClone":_tasks.Add(new NightShadeCloneTask(this,_animationArgs[0]));return true;
            case "AnimTask_DoubleTeam":_tasks.Add(new DoubleTeamTask(this));return true;
            case "AnimTask_ShrinkTargetCopy":var task=new ShrinkTargetCopyTask(this,_animationArgs[0],_animationArgs[1]);if(!task.Finished)_tasks.Add(task);return true;
            default:return false;
        }
    }
    private bool TryCreateBattlerCopySprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=MimicOrbTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_ORBS"))Fail(c,"Mimic orb graphics are not loaded.");
        _sprites.Add(new MimicOrbSprite(slot,priority,_script.SpriteRecipes[MimicOrbTemplate],this));return true;
    }
    // The misleading native name does not create a new sprite: it enlarges the
    // existing actor OBJ over the separate monbg copy.
    private sealed class NightShadeCloneTask:VisualTask
    {
        private readonly int _slot;private int _scale=128,_hold,_phase,_counter,_alpha;
        public NightShadeCloneTask(SourceBattleAnimation o,int hold)
        {_slot=o._cue.Actor.Value;_hold=hold;o.SetAlphaBlend(0,63,0,16);o.PrepareBattlerRotScale(_slot,1);o.SetBattlerRotScale(_slot,128,128,0);o.SetSourceBattlerSpriteInvisible(_slot,false);}
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase==0){if(++_counter==3){_counter=0;o._alphaA=++_alpha;o._alphaB=16-_alpha;if(_alpha==9)_phase=1;}return;}
            if(_hold>0){_hold--;return;}
            _scale+=8;if(_scale<=255)o.SetBattlerRotScale(_slot,_scale,_scale,0);else{o.ResetBattlerRotScale(_slot);o.ClearAlphaBlend();Finished=true;}
        }
    }
    private sealed class ShrinkTargetCopyTask:VisualTask
    {
        private readonly int _slot,_speed;private short _sum;private int _remaining,_scale=256,_phase,_restore;
        public ShrinkTargetCopyTask(SourceBattleAnimation o,int speed,int duration)
        {_slot=o._cue.Target.Value;_speed=speed;_remaining=duration;if(o.SourceBattlerSpriteInvisible(_slot)){Finished=true;return;}o.PrepareBattlerRotScale(_slot,1);}
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase==0)
            {
                _sum=unchecked((short)(_sum+_speed));int x=_sum>>8;if((_slot&1)!=0)x=-x;
                _scale+=16;o.SetBattlerRotScale(_slot,_scale,_scale,0);o.SetBattlerYOffsetFromScale(_slot);o._offsets[_slot]=new(x,o._offsets[_slot].Y);
                if(--_remaining==0)_phase=1;return;
            }
            if(unchecked((ushort)o._animationArgs[7])==65535&&_restore==0){o.ResetBattlerRotScale(_slot);o._offsets[_slot]=default;_restore=1;return;}
            if(_restore==0)return;if(++_restore==3)Finished=true;
        }
    }
    private sealed class DoubleTeamTask:VisualTask
    {
        private readonly int _actor,_tag;public int LiveCopies;
        public DoubleTeamTask(SourceBattleAnimation o)
        {
            _actor=o._cue.Actor.Value;_tag=o._script.SpriteTags["ANIM_TAG_BENT_SPOON"];int palette=o.AllocateSpritePalette(_tag);
            if(palette==255)throw new InvalidDataException("Double Team source palette allocation exhausted.");
            // The source copies unfaded palette entries 1..15 then darkens once.
            // RGB555 battler pixels preserve that exact uniform transformation;
            // index0 remains transparent. No source callback reads this bank later.
            for(int i=0;i<2&&o.IsSourceBattlerPresent(_actor);i++)
            {int slot=o.FindFreeSpriteSlot();if(slot==64)break;o._sprites.Add(new DoubleTeamClone(slot,o,this,i<<7,palette));LiveCopies++;}
            o.SetBattlerBackgroundEnabled(_actor,false);
        }
        public override void Step(SourceBattleAnimation o)
        {if(LiveCopies!=0)return;o.SetBattlerBackgroundEnabled(_actor,true);o.FreeSpritePalette(_tag);Finished=true;}
    }
    private sealed class DoubleTeamClone:VisualSprite
    {
        private readonly DoubleTeamTask _task;private readonly AnimationBattler _battler;private readonly PixelPoint _position;private readonly int _palette;private PixelPoint _offset;private int _age,_timer,_angle;
        public DoubleTeamClone(int slot,SourceBattleAnimation o,DoubleTeamTask task,int angle,int palette):base(slot,o._battlers[o._cue.Actor.Value].Subpriority)
        {_task=task;_battler=o._battlers[o._cue.Actor.Value];_position=o.BattlerPosition(_battler.Slot.Value);_offset=o._offsets[_battler.Slot.Value];_angle=angle;_palette=palette;o._oamY[slot]=o._oamY[_battler.Slot.Value];}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o)
        {if(++_timer>1){_timer=0;_age++;}if(_age>64){_task.LiveCopies--;Finished=true;return;}int radius=o._script.SineTable[_age]/6,increment=o._script.SineTable[_age]/13;_angle=(_angle+increment)&255;_offset=new(o._script.SineTable[_angle]*radius>>8,_offset.Y);}
        public override SourceVisualSpriteFrame Frame=>new(Slot,"AnimDoubleTeam",_position,_offset,0,true,false,true,256,_battler.Asset,Subpriority){Width=64,Height=64,BattlerCopySlot=_battler.Slot.Value,PaletteSlot=_palette,BattlerCopyPalette=new(0,11,0)};
    }
    private sealed class MimicOrbSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private SourceSpriteAffineProgram _affine;private SourceFastLinear? _linear;private readonly PixelPoint _position;private PixelPoint _offset;private int _phase;private bool _invisible=true,_restarted;
        public MimicOrbSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);var a=o._animationArgs;var b=o._battlers[o._cue.Target.Value];if(b.Slot.IsPlayer)a[0]=unchecked((short)-a[0]);var p=b.Base(o._doubles);_position=new(unchecked((short)(p.X+a[0])),unchecked((short)(p.Y+a[1])));
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase==3){Finished=true;return;}
            if(_phase==0)
            {
                _invisible=false;if(!_affine.Ended)return;
                _affine=new(_recipe.AffineAnimations[1],_affine.XScale,_affine.YScale,_affine.Rotation);_restarted=true;_phase=1;return;
            }
            if(_phase==1){var end=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_linear=SourceFastLinear.FromDuration(end.X-_position.X,end.Y-_position.Y,25);_phase=2;}
            if(_linear!.Step(out var offset)){_phase=3;return;}_offset=offset;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();if(_restarted)_restarted=false;else _affine.Step();}
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine.Matrix;return new(Slot,MimicOrbTemplate,_position,_offset,f.Tile,!_invisible,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=16,Height=16,Affine=true,DoubleSize=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
}
