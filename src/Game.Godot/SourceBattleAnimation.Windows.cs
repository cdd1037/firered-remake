namespace Game.Presentation.BattleAnimations;

// Shared WIN0/WIN1 geometry is an on-screen, non-wrapping rectangle. Inverted or
// out-of-display edges and layer-disabling masks need a separately proven PPU
// closure; they are rejected rather than clamped or guessed. Bounds are exclusive.
public sealed record SourceWindow0Frame(bool Enabled,ushort Horizontal,ushort Vertical,int InsideMask,int OutsideMask)
{
    public void Validate()
    {
        if((Horizontal>>8)>(Horizontal&255)||(Horizontal&255)>240||
            (Vertical>>8)>(Vertical&255)||(Vertical&255)>160||
            InsideMask is <0 or >63||OutsideMask is <0 or >63||
            (InsideMask&31)!=31||(OutsideMask&31)!=31)
            throw new InvalidDataException("WIN0 requires bounded non-wrapping edges and unchanged layer visibility.");
    }
    public bool Contains(int x,int y)=>Enabled&&x>=(Horizontal>>8)&&x<(Horizontal&255)&&y>=(Vertical>>8)&&y<(Vertical&255);
    public bool ColorEffectsAt(int x,int y)
    {
        if(x is <0 or >=240||y is <0 or >=160)throw new ArgumentOutOfRangeException(nameof(x));
        bool inside=Contains(x,y);
        return !Enabled||((inside?InsideMask:OutsideMask)&32)!=0;
    }
}
// OBJ-window masks may gate BG1 and color effects; all other layers stay enabled.
public sealed record SourceObjectWindowFrame(bool Enabled,int InsideMask,int OutsideMask)
{
    public void Validate()
    {
        if(InsideMask is <0 or >63||OutsideMask is <0 or >63||
            (InsideMask&29)!=29||(OutsideMask&29)!=29)
            throw new InvalidDataException("OBJ window requires unchanged non-BG1 layers.");
    }
}
public enum SourceBrightnessMode { Lighten=2,Darken=3 }
public sealed record SourceBrightnessFrame(SourceBrightnessMode Mode,int Coefficient)
{
    public void Validate()
    {
        if(Mode is not (SourceBrightnessMode.Lighten or SourceBrightnessMode.Darken)||Coefficient is <0 or >31)
            throw new InvalidDataException("Source BLDCNT brightness mode or BLDY coefficient differs.");
    }
    public ushort Apply(ushort color)
    {
        int y=Math.Min(Coefficient,16);
        if(Mode==SourceBrightnessMode.Darken)return SourceAnimationBackgroundSurface.Darken(color,y);
        int r=color&31,g=(color>>5)&31,b=(color>>10)&31;
        return (ushort)((color&0x8000)|(r+((31-r)*y>>4))|((g+((31-g)*y>>4))<<5)|((b+((31-b)*y>>4))<<10));
    }
}

public sealed partial class SourceBattleAnimation
{
    private SourceWindow0Frame? _window0,_window1;
    private void SetWindow1(bool enabled,ushort horizontal,ushort vertical,int insideMask,int outsideMask)
    {var frame=new SourceWindow0Frame(enabled,horizontal,vertical,insideMask,outsideMask);frame.Validate();_window1=enabled?frame:null;}
    private SourceObjectWindowFrame? _objectWindow;
    private void SetObjectWindow(bool enabled,int insideMask=63,int outsideMask=61)
    {
        var frame=new SourceObjectWindowFrame(enabled,insideMask,outsideMask);frame.Validate();
        _objectWindow=enabled?frame:null;
    }
    private SourceBrightnessFrame? _brightness;
    private void SetBrightness(int firstTargets,SourceBrightnessMode mode,int coefficient)
    {
        if((firstTargets&~63)!=0)throw new InvalidDataException("Source brightness targets exceed BLDCNT.");
        var brightness=new SourceBrightnessFrame(mode,coefficient);brightness.Validate();
        // BLDCNT is one register: brightness replaces alpha mode and its target
        // masks, while BLDALPHA is a separate register and keeps its old values.
        _blendEnabled=false;_blendFirstTargets=firstTargets;_blendSecondTargets=0;_brightness=brightness;
    }
    private bool ValidateHardwareFadeCommand(SourceAnimationCommand c)
    {
        if(c.Op!="createvisualtask"||c.Args.FirstOrDefault()!="AnimTask_HardwarePaletteFade")return false;
        var a=c.Args;
        if(a.Length!=7||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >255||
            ((Number(a[2])>>6)&3) is not (2 or 3)||Number(a[3]) is <0 or >255||
            Number(a[4]) is <0 or >16||Number(a[5]) is <0 or >16||Number(a[6]) is <0 or >1)
            Fail(c,"Hardware fade requires brightness BLDCNT, byte delay, bounded 0..16 endpoints and reset flag.");
        return true;
    }
    private bool TryCreateHardwareFadeTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_HardwarePaletteFade")return false;
        BeginHardwareFade(_animationArgs[2],_animationArgs[3],_animationArgs[4]!=0,_animationArgs[1],_animationArgs[0]);
        _tasks.Add(new HardwareFadeWaitTask());return true;
    }
    private sealed class HardwareFadeWaitTask:VisualTask
    {public override void Step(SourceBattleAnimation owner){if(!owner.PaletteFadeActive)Finished=true;}}
    private bool ValidateWindowTaskOperand(SourceAnimationCommand command)
    {
        if(command.Op!="createvisualtask"||command.Args.FirstOrDefault()!="AnimTask_FakeOut")return false;
        if(command.Args.Length!=2||Number(command.Args[1])!=5)Fail(command,"Fake Out window task operands differ.");
        if(_validatingInvokedProgram)
        {
            // Only the actual palette/shake/stretch closure has register ownership
            // proof. This is callback-based admission, not a move-ID selector.
            var commands=OverlayInvocationCommands();
            if(commands.Count(c=>c.Op=="createvisualtask"&&c.Args.FirstOrDefault()=="AnimTask_FakeOut")!=1||
                commands.Any(c=>c.Op is "setalpha" or "blendoff" or "fadetobg" or "restorebg" or "changebg" or "monbg" or "monbg_static" or "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"||
                    c.Op=="createvisualtask"&&c.Args.FirstOrDefault() is not ("AnimTask_FakeOut" or "AnimTask_ShakeMon2" or "AnimTask_StretchTargetUp")||
                    c.Op=="createsprite"&&c.Args.FirstOrDefault()!="gSimplePaletteBlendSpriteTemplate"))
                Fail(command,"Concurrent window or color-register ownership is outside the bounded Fake Out closure.");
        }
        return true;
    }
    private bool TryCreateWindowTask(SourceAnimationCommand command)
    {
        if(command.Args[0]!="AnimTask_FakeOut")return false;
        _tasks.Add(new FakeOutTask(this));return true;
    }
    private sealed class FakeOutTask:VisualTask
    {
        private int _left,_right=240,_timer;
        private bool _closed;
        public FakeOutTask(SourceBattleAnimation owner)
        {
            // InitBattleBgsVideo enables WIN0 before any move. The source task
            // writes full-height bounds synchronously; RunTasks closes once in
            // that same frame, and VBlank latches the post-task globals.
            owner._window0=new(true,240,160,31,63);
            owner.SetBrightness(8,SourceBrightnessMode.Darken,16);
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(!_closed)
            {
                _left+=13;_right-=13;_closed=_left>=_right;
                owner._window0=owner._window0! with{Horizontal=_closed?(ushort)0:(ushort)((_left<<8)|_right)};
                return;
            }
            if(++_timer==5)
            {
                owner.SetBrightness(8,SourceBrightnessMode.Lighten,16);
                owner.BlendPalettes(owner.SelectPalettes(1),16,0x7fff);
            }
            else if(_timer>4)
            {
                owner._window0=new(true,0,0,63,63);
                owner.ClearAlphaBlend();owner._blendSecondTargets=0;Finished=true;
            }
        }
    }
}
