namespace Game.Presentation.BattleAnimations;

public sealed record SourceMoveBackgroundFrame(int? Id,int ScrollX,int ScrollY,int Darkening)
{
    public int ScreenSize{get;init;}=1;
    public int Revision{get;init;}
    public ushort[] FadedPalette{get;init;}=[];
}

public sealed partial class SourceBattleAnimation
{
    private int? _moveBackground;
    private int _backgroundFadeState,_backgroundRevision,_hardwareDarkening;
    private HardwarePaletteFade? _hardwareFade;
    private long _nextTaskOrder;
    private readonly short[] _animationArgs=new short[8];
    private readonly ushort[]? _initialBackgroundPalette;
    private ushort[]? _unfadedBackgroundPalette,_fadedBackgroundPalette;
    private SourceMoveBackgroundFrame BackgroundFrame=>new(_moveBackground,0,0,_hardwareDarkening){Revision=_backgroundRevision,FadedPalette=_fadedBackgroundPalette is null?[]:(ushort[])_fadedBackgroundPalette.Clone()};
    private bool ValidateBackgroundCommand(SourceAnimationCommand command)
    {
        if(command.Op=="setarg")
        {
            if(command.Args.Length!=2||Number(command.Args[0]) is <0 or >7||AnimationArgument(command.Args[1]) is < -32768 or >65535)Fail(command,"Shared animation argument exceeds source width.");
            return true;
        }
        if(command.Op=="createvisualtask"&&command.Args.FirstOrDefault()=="AnimTask_SetPsychicBackground")
        {
            if(command.Args.Length!=2||Number(command.Args[1]) is <0 or >255||_script.BackgroundPalettes.Count==0||_initialBackgroundPalette is null)Fail(command,"Psychic palette task lacks source colors or priority.");
            return true;
        }
        if(command.Op=="fadetobg")
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
        if(_script.SoundEffects.TryGetValue(value,out int song))return song;
        if(value.StartsWith("SOUND_PAN_",StringComparison.Ordinal))return SoundPan(value);
        if(value.StartsWith("0x",StringComparison.OrdinalIgnoreCase))return Convert.ToInt32(value[2..],16);
        return PaletteNumber(value);
    }
    private void WriteAnimationArguments(ReadOnlySpan<string> values)
    {
        if(values.Length>8)throw new InvalidDataException("Source animation has only eight shared arguments.");
        for(int i=0;i<values.Length;i++)_animationArgs[i]=unchecked((short)AnimationArgument(values[i]));
    }
    private void LoadBackgroundPalette(int? id)
    {
        int lastBank=id is null?4:2;
        for(int bank=2;bank<=lastBank;bank++){_paletteBlends.Remove(bank);_invertedPalettes.Remove(bank);}
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
    private sealed class PsychicBackgroundTask:VisualTask
    {
        private int _counter;
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation owner)
        {
            // Source rotates first, then checks the shared stop argument.
            if(++_counter==4)
            {
                _counter=0;
                if(owner._fadedBackgroundPalette is {} palette)
                {ushort last=palette[11];for(int i=10;i>0;i--)palette[i+1]=palette[i];palette[1]=last;}
            }
            if(unchecked((ushort)owner._animationArgs[7])==65535)Finished=true;
        }
    }
    private void BeginHardwareFade(int from,int to,bool reset)
    {
        // BeginHardwarePaletteFade replaces the global fade, unlike BeginNormal.
        _fade=null;_hardwareFade=new(from,to,reset);
    }
    private void TransferHardwareFade()
    {
        if(_hardwareFade is not {} fade)return;
        _hardwareDarkening=fade.Y;
        // TransferPlttBuffer/UpdateBlendRegisters runs in VBlank after RunTasks.
        if(fade.Finishing)_hardwareFade=null;
    }
    private sealed class HardwarePaletteFade(int from,int target,bool reset)
    {
        private readonly bool _increasing=from<target;
        public int Y{get;private set;}=from;
        public bool Finishing{get;private set;}
        public void Update()
        {
            if(Finishing)return;
            int next=Y+(_increasing?1:-1);
            if(_increasing?next>target:next<target){Finishing=true;if(reset)Y=0;}
            else Y=next;
        }
    }
    private abstract class SourceTask
    {
        public int Priority{get;set;}
        public long Order{get;set;}
        public bool Finished{get;protected set;}
        public abstract void Step(SourceBattleAnimation owner);
    }
    private void AssignTaskOrder(int visualStart,int soundStart,SourceAnimationCommand command)
    {
        for(int i=visualStart;i<_tasks.Count;i++)
        {_tasks[i].Priority=command.Op=="createvisualtask"?Number(command.Args[1]):5;_tasks[i].Order=_nextTaskOrder++;}
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
                owner._moveBackground=id;owner._backgroundRevision++;owner.LoadBackgroundPalette(id);
                owner.BeginHardwareFade(16,0,true);_state=3;return;
            }
            if(owner.PaletteFadeActive)return;
            if(_state==3){Finished=true;owner._backgroundFadeState=0;}
        }
    }
}
