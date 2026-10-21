namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string PunchTraceTaskName="AnimTask_AttackerPunchWithTrace";
    private bool ValidatePunchTraceCommand(SourceAnimationCommand c)
    {
        if(c.Op!="createvisualtask"||c.Args.FirstOrDefault()!=PunchTraceTaskName)return false;
        var a=c.Args;
        if(a.Length!=4||Number(a[1]) is <0 or >255||AnimationArgument(a[2]) is <0 or >32767||Number(a[3]) is <0 or >16||!_script.SpriteTags.ContainsKey("ANIM_TAG_BENT_SPOON"))
            Fail(c,"Punch trace requires its source priority, RGB555 tint, coefficient and owned palette tag.");
        if(!_validatingInvokedProgram)return true;
        var commands=OverlayInvocationCommands();
        if(_spriteMaterials is null||!_spriteMaterials.TryGetValue(_battlers[_cue.Actor.Value].Asset,out var actorMaterial)||actorMaterial is not {Width:64,Height:64,Pixels.Length:4096})
            Fail(c,"Punch trace requires the current actor-owned 64x64 body material.");
        if(!IsSourceBattlerPresent(_cue.Actor.Value)||commands.Count(x=>x.Op=="createvisualtask"&&x.Args[0]==PunchTraceTaskName)!=1||
            commands.Any(x=>x.Op is "visible" or "invisible" or "monbg_static" or "clearmonbg_static"||
                x.Op=="loadspritegfx"&&x.Args[0] is not("ANIM_TAG_IMPACT" or "ANIM_TAG_HANDS_AND_FEET")||
                x.Op=="createvisualtask"&&x.Args[0] is not(PunchTraceTaskName or "AnimTask_GetAttackerSide" or "AnimTask_StartSlidingBg" or "AnimTask_ShakeMon")||
                x.Op=="createsprite"&&x.Args[0] is not("gBasicHitSplatSpriteTemplate" or "gFistFootSpriteTemplate")))
            Fail(c,"Punch trace requires one present actor and its reviewed movement, hit and background companions.");
        // The authored target shake must not change the cloned actor's offset.
        if(commands.Any(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_ShakeMon"&&(x.Args.Length!=7||Number(x.Args[2])!=1||_cue.Actor==_cue.Target)))
            Fail(c,"Punch trace cannot overlap another actor translation owner.");
        if(commands.Where(x=>x.Op=="loadspritegfx").Select(x=>x.Args[0]).Distinct().Count()+1>12)
            Fail(c,"Punch trace palette ownership exceeds the animation pool.");
        return true;
    }
    private bool TryCreatePunchTraceTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!=PunchTraceTaskName)return false;
        _tasks.Add(new PunchTraceTask(this));return true;
    }
    private sealed class PunchTraceTask:VisualTask
    {
        private readonly int _actor,_speed,_tag,_palette,_priority;
        private readonly SourcePaletteBlend _tint;
        private int _phase,_counter;
        public int LiveCopies;
        public PunchTraceTask(SourceBattleAnimation o)
        {
            _actor=o._cue.Actor.Value;_speed=o._cue.Actor.IsPlayer?8:-8;
            _tag=o._script.SpriteTags["ANIM_TAG_BENT_SPOON"];_palette=o.AllocateSpritePalette(_tag);
            if(_palette==255)throw new InvalidOperationException("Punch trace palette capacity exhausted.");
            _priority=o._battlers[_actor].Subpriority is 20 or 40?2:3;
            // Approved safe initialization: preserve actual current x2; never
            // subtract the allocator's numeric sprite ID. Initialize the copy's
            // normal color authority from the actor before applying its tint.
            // The reviewed closure performs no later clone-palette writes, so
            // this immutable RGB555 transform is equivalent for every opaque
            // body pixel and does not manufacture unavailable native indices.
            _tint=new(16+_palette,o._animationArgs[1],unchecked((ushort)o._animationArgs[0]))
                {Unfaded=o._unfadedPaletteStates.GetValueOrDefault(16+_actor)};
            o.ResetPaletteTransform(16+_palette);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase<2)
            {
                int slot=o.FindFreeSpriteSlot();
                if(slot<SourceSpriteRegistry<VisualSprite>.Capacity&&o.IsSourceBattlerPresent(_actor))
                {o._sprites.Add(new PunchTraceClone(slot,o,_actor,_palette,_priority,_tint,this));LiveCopies++;}
                var p=o._offsets[_actor];o._offsets[_actor]=new(unchecked((short)(p.X+(_phase==0?_speed:-_speed))),p.Y);
                if(_phase==0){if(++_counter==5){_counter--;_phase++;}}
                else if(--_counter==0){o._offsets[_actor]=new(0,p.Y);_phase++;}
                return;
            }
            if(LiveCopies!=0)return;
            o.FreeSpritePalette(_tag);Finished=true;
        }
    }
    private sealed class PunchTraceClone:VisualSprite
    {
        private readonly SourceVisualSpriteFrame _frame;private readonly PunchTraceTask _task;
        private int _remaining=8;
        public PunchTraceClone(int slot,SourceBattleAnimation o,int actor,int palette,int priority,SourcePaletteBlend tint,PunchTraceTask task):base(slot,o._battlers[actor].Subpriority)
        {
            _task=task;
            _frame=o.CaptureBattlerCopyFrame(slot,actor,"AnimBattlerTrace") with
                {PaletteSlot=palette,OamPriority=priority,BattlerCopyPalette=tint,Visible=true,Blend=true};
        }
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>_frame;
        public override void Step(SourceBattleAnimation o)
        {if(--_remaining!=0)return;_task.LiveCopies--;Finished=true;}
    }
}
