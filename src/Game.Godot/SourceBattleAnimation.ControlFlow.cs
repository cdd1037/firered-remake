namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private static readonly IReadOnlyDictionary<string,int> NoLabels=new Dictionary<string,int>();
    private IReadOnlyDictionary<string,int> _programLabels=NoLabels;
    private int _validationProgramLength;
    private bool _validatingInvokedProgram=true;
    private IReadOnlyDictionary<string,int> LabelsFor(string name)=>_script.SubroutineLabels.TryGetValue(name,out var labels)?labels:NoLabels;
    private bool ValidProgramLabel(string name)=>_programLabels.TryGetValue(name,out int pc)&&pc>=0&&pc<_validationProgramLength;
    private void ValidatePrograms()
    {
        if(_script.SubroutineLabels.Keys.Any(name=>!_script.Subroutines.ContainsKey(name)))throw new InvalidDataException("Labels refer to an absent source subroutine.");
        ValidateProgram(_script.Commands,_script.Labels,false);
        // Catalogs share helper dictionaries across roots. Structural validation still
        // covers every helper, while transient invocation inputs belong only to helpers
        // referenced by this root. Nested calls are rejected by ValidateProgram.
        var invoked=SourceAnimationProgramDependencies.RequiredSubroutines(_script.Commands,_script.Subroutines);
        foreach(var (name,commands) in _script.Subroutines)
        {
            _validatingInvokedProgram=invoked.ContainsKey(name);
            ValidateProgram(commands,LabelsFor(name),true);
        }
        _validatingInvokedProgram=true;_programLabels=_script.Labels;
    }
    private void ValidateProgram(SourceAnimationCommand[] commands,IReadOnlyDictionary<string,int> labels,bool subroutine)
    {
        if(commands.Length is <1 or >128)throw new InvalidDataException("Source program or label is outside its bounded command range.");
        _programLabels=labels;_validationProgramLength=commands.Length;
        foreach(var command in commands)
        {
            if(subroutine&&command.Op is "call" or "end"||!subroutine&&command.Op=="return")throw new InvalidDataException("Source call/return scope differs.");
            Validate(command);
        }
        if(labels.Values.Any(pc=>pc<0||pc>=commands.Length))throw new InvalidDataException("Unused source label is outside its program.");
        var state=new byte[commands.Length];
        void Visit(int pc)
        {
            if(pc<0||pc>=commands.Length)throw new InvalidDataException("Source control flow falls outside its program.");
            if(state[pc]==1)throw new InvalidDataException("Source control flow must be acyclic.");
            if(state[pc]==2)return;state[pc]=1;
            var c=commands[pc];var a=c.Args;
            switch(c.Op)
            {
                case "end":case "return":break;
                case "goto":Visit(labels[a[0]]);break;
                case "choosetwoturnanim":Visit(labels[a[0]]);Visit(labels[a[1]]);break;
                case "jumpifmoveturn":Visit(labels[a[1]]);Visit(pc+1);break;
                case "jumpargeq":Visit(labels[a[2]]);Visit(pc+1);break;
                default:Visit(pc+1);break;
            }
            state[pc]=2;
        }
        // Validate every instruction, including alternate blocks not selected by
        // this invocation; a backward jump to an earlier return is still acyclic.
        for(int pc=0;pc<commands.Length;pc++)Visit(pc);
    }
    private bool ValidateArgumentCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="jumpargeq")
        {
            if(a.Length!=3||Number(a[0]) is <0 or >7||AnimationArgument(a[1]) is <short.MinValue or >ushort.MaxValue||!ValidProgramLabel(a[2]))Fail(c,"Argument branch needs a source argument, signed16 comparison and scoped label.");
            return true;
        }
        if(c.Op!="createvisualtask"||a.FirstOrDefault() is not ("AnimTask_IsContest" or "AnimTask_IsTargetPlayerSide" or "AnimTask_GetAttackerSide"))return false;
        if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Source side/engine predicate takes only task priority.");
        return true;
    }
    private bool TryRunArgumentPredicate(SourceAnimationCommand c)
    {
        short? result=c.Args[0] switch
        {
            "AnimTask_IsContest"=>0,
            "AnimTask_IsTargetPlayerSide"=>(short)(_cue.Target.IsPlayer?1:0),
            "AnimTask_GetAttackerSide"=>(short)(_cue.Actor.IsPlayer?0:1),
            _=>null
        };
        if(result is not {} value)return false;
        // Pinned FireRed has no contest mode. These source initializers write the
        // return argument and immediately destroy their counted visual task.
        _animationArgs[7]=value;return true;
    }
}

// Shared by resource dependency resolution and invocation-context validation.
internal static class SourceAnimationProgramDependencies
{
    public static Dictionary<string,SourceAnimationCommand[]> RequiredSubroutines(
        IEnumerable<SourceAnimationCommand> commands,IReadOnlyDictionary<string,SourceAnimationCommand[]> subroutines)
    {
        var required=new Dictionary<string,SourceAnimationCommand[]>(StringComparer.Ordinal);
        void Add(IEnumerable<SourceAnimationCommand> program)
        {
            foreach(var command in program.Where(c=>c.Op=="call"))
            {
                if(command.Args.Length!=1)throw new InvalidDataException("Source call requires one helper name.");
                string name=command.Args[0];if(required.ContainsKey(name))continue;
                if(!subroutines.TryGetValue(name,out var body))throw new InvalidDataException($"Missing source subroutine {name}.");
                required.Add(name,body);Add(body);
            }
        }
        Add(commands);return required;
    }
}
