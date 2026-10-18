namespace Game.Presentation.BattleAnimations;

public sealed record SourceMoveBackgroundFrame(int? Id,int ScrollX,int ScrollY,int Darkening)
{
    public int ScreenSize{get;init;}=1;
    public int Revision{get;init;}
    public ushort[] FadedPalette{get;init;}=[];
}

public sealed partial class SourceBattleAnimation
{
    private SourceAnimationOverlayFrame? _overlay;
    private int _blendFirstTargets,_blendSecondTargets=63;
    private void BeginOverlayMaterial(string materialId)
    {
        if(!_script.OverlayDependencies.Contains(materialId)||!_script.OverlayPalettes.TryGetValue(materialId,out var palette)||palette.Length!=16)
            throw new InvalidDataException("Source BG1 overlay dependency is missing.");
        _overlay=new(materialId,0,0);SetScanlineBackgroundOffset(1,default);
    }
    private void BeginOverlay(string materialId)
    {
        BeginOverlayMaterial(materialId);
        // LoadPalette replaces the faded and unfaded source bank; it does not
        // retain a blend or inversion from the previous owner of bank eight.
        ClearBackgroundPaletteLoads(8);ResetPaletteTransform(8);
    }
    private void SetOverlayScroll(int x,int y)
    {
        if(_overlay is null)throw new InvalidOperationException("Source overlay is not loaded.");
        _overlay=_overlay with{ScrollX=unchecked((ushort)x),ScrollY=unchecked((ushort)y)};SetScanlineBackgroundOffset(1,new(x,y));
    }
    // InitBattleAnimBg clears VRAM without resetting the separate scroll globals.
    private void ClearBackgroundTilemaps()
    {_overlay=null;_backgrounds.Clear();_staticBackgroundCopies.Clear();}
    private void EndOverlay(bool clearBg1AndBg2)
    {
        _overlay=null;
        if(clearBg1AndBg2)ClearBattlerBackgrounds();
    }
    private void SetAlphaBlend(int firstTargets,int secondTargets,int a,int b)
    {
        if((firstTargets&~63)!=0||(secondTargets&~63)!=0||a is <0 or >31||b is <0 or >31)
            throw new InvalidDataException("Source alpha blend registers differ.");
        _brightness=null;_blendFirstTargets=firstTargets;_blendSecondTargets=secondTargets;_alphaA=a;_alphaB=b;_queuedAlphaRegister=true;_blendEnabled=true;
    }
    private void ClearAlphaBlend()
    {_brightness=null;_blendEnabled=false;_blendFirstTargets=0;_blendSecondTargets=63;_alphaA=_alphaB=0;_queuedAlphaRegister=true;}

    private int? _moveBackground;
    private ushort BackgroundScrollX {get;set;}
    private ushort BackgroundScrollY {get;set;}
    private int BackgroundScreenSize {get;set;}=1;
    private int _backgroundFadeState,_backgroundRevision,_hardwareDarkening;
    private HardwarePaletteFade? _hardwareFade;
    private long _nextTaskOrder;
    // The source declares eight callback arguments, but Magnitude writes and
    // branches on register15. The remake gives authored script registers a
    // bounded backing instead of aliasing adjacent native globals.
    private const int AnimationRegisterCount=16, AnimationOperandCount=8;
    private readonly short[] _animationArgs=new short[AnimationRegisterCount];
    private readonly ushort[]? _initialBackgroundPalette;
    private ushort[]? _unfadedBackgroundPalette,_fadedBackgroundPalette;
    private SourceMoveBackgroundFrame BackgroundFrame=>new(_moveBackground,BackgroundScrollX,BackgroundScrollY,_hardwareDarkening){ScreenSize=BackgroundScreenSize,Revision=_backgroundRevision,FadedPalette=_fadedBackgroundPalette is null?[]:(ushort[])_fadedBackgroundPalette.Clone()};
    private bool ValidateBackgroundCommand(SourceAnimationCommand command)
    {
        if(command.Op=="setarg")
        {
            if(command.Args.Length!=2||(Number(command.Args[0])<0||Number(command.Args[0])>=AnimationRegisterCount)||AnimationArgument(command.Args[1]) is < -32768 or >65535)Fail(command,"Shared animation argument exceeds source width.");
            return true;
        }
        if(command.Op=="createvisualtask"&&command.Args.FirstOrDefault() is "AnimTask_SetPsychicBackground" or "AnimTask_FadeScreenToWhite")
        {
            if(command.Args.Length!=2||Number(command.Args[1]) is <0 or >255||_script.BackgroundPalettes.Count==0||_initialBackgroundPalette is null)Fail(command,"Psychic palette task lacks source colors or priority.");
            return true;
        }
        if(command.Op=="fadetobgfromset")
        {
            if(command.Args.Length!=3||command.Args.Any(a=>!_script.BackgroundSymbols.TryGetValue(a,out int id)||id is <0 or >255))Fail(command,"Background set requires three source byte identities.");
            return true;
        }
        if(command.Op is "fadetobg" or "changebg")
        {
            if(command.Args.Length!=1||!_script.BackgroundSymbols.ContainsKey(command.Args[0]))Fail(command,"Missing generated source background definition.");
            return true;
        }
        if(command.Op is "restorebg" or "waitbgfadein" or "waitbgfadeout")
        {if(command.Args.Length!=0)Fail(command,"Background command takes no operands.");return true;}
        return false;
    }
    private int AnimationArgument(string value)
    {
        if(value=="ARG_RET_ID")return 7;
        if(value=="TRUE")return 1;if(value=="FALSE")return 0;
        if(value=="MAX_BATTLERS_COUNT")return 4;
        if(value=="(MAX_BATTLERS_COUNT + 1)")return 5;
        if(value=="DOUBLE_CRY_ROAR")return 2; // include/constants/sound.h
        if(value=="DOUBLE_CRY_GROWL")return 255; // include/constants/sound.h
        if(_script.SpriteTags.TryGetValue(value,out int tag))return tag;
        if(_script.SoundEffects.TryGetValue(value,out int song))return song;
        if(value.StartsWith("SOUND_PAN_",StringComparison.Ordinal))return SoundPan(value);
        if(value.StartsWith("0x",StringComparison.OrdinalIgnoreCase))return Convert.ToInt32(value[2..],16);
        return PaletteNumber(value);
    }
    private void WriteAnimationArguments(ReadOnlySpan<string> values)
    {
        if(values.Length>AnimationOperandCount)throw new InvalidDataException("Source animation has only eight shared arguments.");
        for(int i=0;i<values.Length;i++)_animationArgs[i]=unchecked((short)AnimationArgument(values[i]));
    }
    private void SetMoveBackground(int? id)
    {_moveBackground=id;_backgroundRevision++;LoadBackgroundPalette(id);}
    private void LoadBackgroundPalette(int? id)
    {
        int lastBank=id is null?4:2;
        for(int bank=2;bank<=lastBank;bank++){ClearBackgroundPaletteLoads(bank);ResetPaletteTransform(bank);}
        if(id is {} selected&&_script.BackgroundPalettes.TryGetValue(selected,out var colors))
        {
            if(colors.Length!=16)throw new InvalidDataException("Move background needs one source palette bank.");
            _unfadedBackgroundPalette=(ushort[])colors.Clone();_fadedBackgroundPalette=(ushort[])colors.Clone();
        }
        else if(id is null&&_initialBackgroundPalette is {} original)
        {_unfadedBackgroundPalette=(ushort[])original.Clone();_fadedBackgroundPalette=(ushort[])original.Clone();}
        else _unfadedBackgroundPalette=_fadedBackgroundPalette=null;
    }
    private void BlendBackgroundPalette(int coefficient,ushort color)
    {
        if(_unfadedBackgroundPalette is null)return;
        for(int i=0;i<16;i++)_fadedBackgroundPalette![i]=SourcePaletteMath.Blend(_unfadedBackgroundPalette[i],coefficient,color);
    }
    private sealed class PsychicBackgroundTask(bool rotateUnfaded=false):VisualTask
    {
        private int _counter;
        private static void Rotate(ushort[] palette)
        {ushort last=palette[11];for(int i=10;i>0;i--)palette[i+1]=palette[i];palette[1]=last;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation owner)
        {
            // Source rotates first, then checks the shared stop argument.
            if(++_counter==4)
            {
                _counter=0;
                if(owner._fadedBackgroundPalette is {} palette)
                {Rotate(palette);owner.ClearBackgroundPaletteEntries(2);}
                if(rotateUnfaded&&owner._unfadedBackgroundPalette is {} unfaded)Rotate(unfaded);
            }
            if(unchecked((ushort)owner._animationArgs[7])==65535)Finished=true;
        }
    }
    private void BeginHardwareFade(int from,int to,bool reset,int delay=0,int? blendControl=null)
    {
        // BeginHardwarePaletteFade replaces the global fade, unlike BeginNormal.
        // Source delay and delayCounter are six-bit fields after the u8 call.
        _fade=null;_hardwareFade=new(from,to,reset,delay&63,blendControl);
    }
    private void TransferHardwareFade()
    {
        if(_hardwareFade is not {} fade)return;
        if(fade.BlendControl is {} control)
        {
            _hardwareDarkening=0;
            if(fade.Finishing&&fade.Reset){_brightness=null;_blendEnabled=false;_blendFirstTargets=_blendSecondTargets=0;}
            else SetBrightness(control&63,(SourceBrightnessMode)((control>>6)&3),fade.Y);
        }
        else _hardwareDarkening=fade.Y;
        // TransferPlttBuffer/UpdateBlendRegisters runs in VBlank after RunTasks.
        if(fade.Finishing)_hardwareFade=null;
    }
    private sealed class HardwarePaletteFade(int from,int target,bool reset,int delay,int? blendControl)
    {
        private readonly bool _increasing=from<target;
        private int _delayCounter=delay;
        private readonly int _delay=delay;
        public int? BlendControl=>blendControl;
        public bool Reset=>reset;
        public int Y{get;private set;}=from;
        public bool Finishing{get;private set;}
        public void Update()
        {
            if(Finishing)return;
            if(_delayCounter<_delay){_delayCounter++;return;}_delayCounter=0;
            int next=Y+(_increasing?1:-1);
            if(_increasing?next>target:next<target){Finishing=true;if(reset)Y=0;}
            else Y=next;
        }
    }
    private abstract class SourceTask
    {
        public int Priority{get;set;}=-1;
        public long Order{get;set;}
        public bool Finished{get;protected set;}
        public abstract void Step(SourceBattleAnimation owner);
    }
    private void AssignTaskOrder(int visualStart,int soundStart,SourceAnimationCommand command)
    {
        for(int i=visualStart;i<_tasks.Count;i++)
        {if(_tasks[i].Priority<0)_tasks[i].Priority=command.Op=="createvisualtask"?Number(command.Args[1]):5;_tasks[i].Order=_nextTaskOrder++;}
        for(int i=soundStart;i<_soundTasks.Count;i++)
        {_soundTasks[i].Priority=1;_soundTasks[i].Order=_nextTaskOrder++;}
    }
    private void RunSourceTasks()
    {
        foreach(var task in _tasks.Cast<SourceTask>().Concat(_soundTasks).OrderBy(t=>t.Priority).ThenBy(t=>t.Order).ToArray())
        {
            task.Step(this);
            if(!task.Finished)continue;
            if(task is VisualTask visual)_tasks.Remove(visual);else _soundTasks.Remove((SoundTask)task);
        }
    }
    private sealed class FadeToBackgroundTask(int? id):VisualTask
    {
        private int _state;
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state==0){owner.BeginHardwareFade(0,16,false);_state=1;return;}
            if(owner.PaletteFadeActive)return;
            if(_state==1){_state=2;owner._backgroundFadeState=2;}
            else if(_state==2)
            {
                owner.SetMoveBackground(id);
                owner.BeginHardwareFade(16,0,true);_state=3;return;
            }
            if(owner.PaletteFadeActive)return;
            if(_state==3){Finished=true;owner._backgroundFadeState=0;}
        }
    }
}
