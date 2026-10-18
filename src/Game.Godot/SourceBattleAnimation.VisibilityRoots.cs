namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string ShrinkingThinRingTemplate="gThinRingShrinkingSpriteTemplate";
    private bool ValidateVisibilityRootOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op is "visible" or "invisible")
        {if(a.Length!=1||Number(a[0]) is <0 or >3)Fail(c,"Source visibility command requires a native animation participant.");return true;}
        if(c.Op=="createsprite"&&name==ShrinkingThinRingTemplate)
        {
            if(a.Length!=7||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Take(2).Any(x=>Number(x) is <short.MinValue or >short.MaxValue)||Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1||!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Shrinking ring source operands or recipe differ.");
            r=_script.SpriteRecipes[name];SourceSpriteAffineCommand[] affine=[new("frame",512,512,0,0,0),new("frame",-16,-16,0,30,0),new("end",0,0,0,0,0)];
            if(r.Asset!="thin-ring"||r.Callback!="AnimSpriteOnMonPos"||!r.Blend||!r.AffineDoubleSize||r.Width!=64||r.Height!=64||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{0})||r.FrameLoops.Any(x=>x.Length!=0)||r.AffineAnimations.Length!=1||!r.AffineAnimations[0].SequenceEqual(affine))Fail(c,"Shrinking ring source frame/affine program differs.");
            return true;
        }
        if(c.Op!="createvisualtask")return false;
        if(name is not ("AnimTask_AttackerFadeToInvisible" or "AnimTask_AttackerFadeFromInvisible" or "AnimTask_InitAttackerFadeFromInvisible" or "AnimTask_SetAttackerInvisibleWaitForSignal" or "AnimTask_SetAllNonAttackersInvisiblity" or "AnimTask_SetCamouflageBlend" or "AnimTask_BlendBattleAnimPalExclude" or "AnimTask_OdorSleuthMovement"))return false;
        if(a.Length<2||Number(a[1]) is <0 or >255)Fail(c,"Visibility task priority differs.");
        if(name is "AnimTask_AttackerFadeToInvisible" or "AnimTask_AttackerFadeFromInvisible")
        {if(a.Length!=3||AnimationArgument(a[2]) is <short.MinValue or >short.MaxValue)Fail(c,"Attacker fade uses a signed source word, narrowed to u8 delay.");}
        else if(name=="AnimTask_SetAllNonAttackersInvisiblity")
        {if(a.Length!=3||AnimationArgument(a[2]) is <0 or >1)Fail(c,"Non-attacker invisibility must be a source boolean.");}
        else if(name is "AnimTask_SetCamouflageBlend" or "AnimTask_BlendBattleAnimPalExclude")
        {
            bool camouflage=name=="AnimTask_SetCamouflageBlend";
            if(a.Length!=(camouflage?6:7)||AnimationArgument(a[2]) is <0||AnimationArgument(a[2])>(camouflage?127:7)||a.Skip(3).Take(3).Select(AnimationArgument).Where((v,i)=>i==0?v is <0 or >32767:v is <0 or >16).Any()||!camouflage&&AnimationArgument(a[6]) is <0 or >65535)Fail(c,"Visibility palette task source arguments differ.");
            if(camouflage&&_validatingInvokedProgram&&(_cue.SourceTerrain is not {} terrain||(int)terrain is <0 or >9))Fail(c,"Camouflage requires the committed native terrain enum 0 through 9.");
        }
        else if(a.Length!=2)Fail(c,"Visibility task has no authored arguments.");
        if(name=="AnimTask_SetAttackerInvisibleWaitForSignal"&&_validatingInvokedProgram)
        {
            var closure=OverlayInvocationCommands();int at=Array.IndexOf(closure.ToArray(),c);
            if(at<0||!closure.Skip(at+1).Any(x=>x.Op=="setarg"&&x.Args.Length==2&&x.Args[0]=="7"&&AnimationArgument(x.Args[1])==0x1000))Fail(c,"Uncounted logical-invisibility task needs its authored restore signal.");
        }
        if(name=="AnimTask_OdorSleuthMovement"&&_validatingInvokedProgram)
        {
            // The raw clone copies a normal battler sprite. Its authored closure has
            // only palette blending and horizontal lunge, no affine/form mutation.
            foreach(var command in OverlayInvocationCommands())
                if(command.Op=="createvisualtask"&&command.Args[0]!="AnimTask_OdorSleuthMovement"||command.Op=="createsprite"&&command.Args[0] is not ("gHorizontalLungeSpriteTemplate" or "gComplexPaletteBlendSpriteTemplate"))Fail(c,"Odor Sleuth raw clone requires its reviewed normal-battler closure.");
            if(_script.SineTable.Length!=320)Fail(c,"Odor Sleuth requires the exact source sine table.");
        }
        return true;
    }
    private bool TryCreateVisibilityRootSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=ShrinkingThinRingTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_THIN_RING"))Fail(c,"Shrinking ring source graphics are not loaded.");
        _sprites.Add(new ElementalSprite(slot,priority,_script.SpriteRecipes[c.Args[0]],Resolve(c.Args[1]),c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private bool TryCreateVisibilityRootTask(SourceAnimationCommand c)
    {
        var a=_animationArgs;
        switch(c.Args[0])
        {
            case "AnimTask_AttackerFadeToInvisible":_tasks.Add(new AttackerVisibilityFade(this,a[0],false));return true;
            case "AnimTask_AttackerFadeFromInvisible":_tasks.Add(new AttackerVisibilityFade(this,a[0],true));return true;
            case "AnimTask_InitAttackerFadeFromInvisible":SetAttackerVisibilityBlend(0,16);return true;
            case "AnimTask_SetAttackerInvisibleWaitForSignal":_tasks.Add(new AttackerLogicalVisibilityWait(this));return true;
            case "AnimTask_SetAllNonAttackersInvisiblity":
                for(int i=0;i<4;i++)if(i!=_cue.Actor.Value&&IsSourceBattlerVisible(i))SetSourceBattlerSpriteInvisible(i,a[0]!=0);return true;
            case "AnimTask_SetCamouflageBlend":
                int[] colors=[12|(24<<5)|(2<<10),(15<<5)|(2<<10),30|(24<<5)|(11<<10),18<<10,11|(22<<5)|(31<<10),11|(22<<5)|(31<<10),22|(16<<5)|(10<<10),14|(9<<5)|(3<<10),32767,32767];
                a[4]=unchecked((short)colors[(int)_cue.SourceTerrain!.Value]);StartVisibilityPaletteBlend(SelectPalettes(a[0]));return true;
            case "AnimTask_BlendBattleAnimPalExclude":
                int first=a[0] switch{0 or 2=>_cue.Actor.Value,1 or 3=>_cue.Target.Value,4=>_cue.Actor.Value,6=>_cue.Actor.Value^2,7=>_cue.Target.Value^2,_=>255};
                int second=a[0]==4?_cue.Target.Value:255;uint mask=a[0] is 2 or 3 or 6 or 7?0u:SelectPalettes(1);
                for(int i=0;i<4;i++)if(i!=first&&i!=second&&IsSourceBattlerVisible(i))mask|=1u<<(16+i);
                StartVisibilityPaletteBlend(mask);return true;
            case "AnimTask_OdorSleuthMovement":var task=new OdorSleuthMovement(this);if(!task.Finished)_tasks.Add(task);return true;
            default:return false;
        }
    }
    private void StartVisibilityPaletteBlend(uint mask)
    {var a=_animationArgs;var task=new BlendPaletteTask(mask,a[1],a[2],a[3],unchecked((ushort)a[4]));task.Step(this);if(!task.Finished)_tasks.Add(task);}
    private void SetAttackerVisibilityBlend(int a,int b)=>SetAlphaBlend(_battlers[_cue.Actor.Value].BackgroundRank==1?2:4,63,a,b);
    private sealed class AttackerVisibilityFade:VisualTask
    {
        private readonly int _delay;private readonly bool _from;private int _counter,_first,_second;
        public AttackerVisibilityFade(SourceBattleAnimation o,int delay,bool from)
        {_delay=unchecked((byte)delay);_from=from;_first=from?0:16;_second=from?16:0;if(from){o._alphaA=_first;o._alphaB=_second;}else o.SetAttackerVisibilityBlend(_first,_second);}
        public override void Step(SourceBattleAnimation o)
        {
            if(_counter++!=_delay)return;_counter=0;_first+=_from?1:-1;_second+=_from?-1:1;o._alphaA=_first;o._alphaB=_second;
            if(_second!=(_from?0:16))return;
            if(_from)o.ClearAlphaBlend();else o.SetSourceBattlerSpriteInvisible(o._cue.Actor.Value,true);Finished=true;
        }
    }
    private sealed class AttackerLogicalVisibilityWait:VisualTask
    {
        private readonly bool _initial;
        public AttackerLogicalVisibilityWait(SourceBattleAnimation o){_initial=o.SourceBattlerLogicalInvisible(o._cue.Actor.Value);o.SetSourceBattlerLogicalInvisible(o._cue.Actor.Value,true);}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o){if(o._animationArgs[7]!=0x1000)return;o.SetSourceBattlerLogicalInvisible(o._cue.Actor.Value,_initial);Finished=true;}
    }
    private sealed class OdorSleuthMovement:VisualTask
    {
        public int LiveCopies;
        public OdorSleuthMovement(SourceBattleAnimation o)
        {
            if(!o.IsSourceBattlerPresent(o._cue.Target.Value)){Finished=true;return;}
            int slot=o.FindFreeSpriteSlot();if(slot==64){Finished=true;return;}
            var first=new OdorSleuthClone(slot,o,this,false);o._sprites.Add(first);
            slot=o.FindFreeSpriteSlot();if(slot==64){o._sprites.Remove(first);Finished=true;return;}
            o._sprites.Add(new OdorSleuthClone(slot,o,this,true));LiveCopies=2;
        }
        public override void Step(SourceBattleAnimation o){if(LiveCopies==0)Finished=true;}
    }
    private sealed class OdorSleuthClone:VisualSprite
    {
        private readonly OdorSleuthMovement _task;private readonly AnimationBattler _battler;private readonly PixelPoint _position;private PixelPoint _offset;private readonly int _increment,_oamPriority;private int _phase,_toggle,_timer,_angle,_radius=24;private bool _invisible;
        private static short S(int value)=>unchecked((short)value);
        public OdorSleuthClone(int slot,SourceBattleAnimation o,OdorSleuthMovement task,bool second):base(slot,o._battlers[o._cue.Target.Value].Subpriority)
        {_task=task;_battler=o._battlers[o._cue.Target.Value];_position=o.BattlerPosition(_battler.Slot.Value);var offset=o._offsets[_battler.Slot.Value];_offset=new(S(offset.X+(second?24:-24)),offset.Y);_angle=second?0:128;_increment=second?16:-16;_invisible=!second||o.SourceBattlerLogicalInvisible(_battler.Slot.Value);_oamPriority=2;o._oamY[slot]=o._oamY[_battler.Slot.Value];}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o)
        {
            if(++_toggle>1){_toggle=0;if(!o.SourceBattlerLogicalInvisible(o._cue.Target.Value))_invisible=!_invisible;}
            _angle=(_angle+_increment)&255;_offset=new(S(o._script.SineTable[_angle+64]*_radius>>8),_offset.Y);
            if(_phase==0){if(++_timer==60){_timer=0;_phase++;}}
            else if(++_timer>0){_timer=0;_radius-=2;if(_radius<0){_task.LiveCopies--;Finished=true;}}
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,"MoveOdorSleuthClone",_position,_offset,0,!_invisible,false,false,256,_battler.Asset,Subpriority){Width=64,Height=64,BattlerCopySlot=_battler.Slot.Value,OamPriority=_oamPriority};
    }
}
