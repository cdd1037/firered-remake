namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private static readonly IReadOnlyDictionary<string,int> NoLabels=new Dictionary<string,int>();
    private IReadOnlyDictionary<string,int> _programLabels=NoLabels;
    private int _validationProgramLength;
    private bool _validatingInvokedProgram=true;
    private SourceAnimationScript _programSource=null!,_invocationProgram=null!;
    private bool _validatingSubroutine;
    private IReadOnlyDictionary<string,int> LabelsFor(string name)=>_programSource.SubroutineLabels.TryGetValue(name,out var labels)?labels:NoLabels;
    private bool ValidProgramLabel(string name)=>_programLabels.TryGetValue(name,out int pc)&&pc>=0&&pc<_validationProgramLength||!_validatingSubroutine&&_programSource.TailPrograms.ContainsKey(name);
    private void TransferProgram(string label)
    {
        if(_programLabels.TryGetValue(label,out int pc)){_pc=pc;return;}
        if(_subroutine is not null||!_programSource.TailPrograms.TryGetValue(label,out var tail))
            throw new InvalidDataException("Missing or helper-scoped source tail program.");
        // Source goto changes only the script pointer. It does not reseed visual
        // RNG, clear argument registers, wait a tick, or create another ACK owner.
        _programSource=tail;_programLabels=tail.Labels;_pc=0;
    }
    private void ValidatePrograms()
    {
        var programs=SourceAnimationProgramDependencies.ProgramClosure(_script);
        _invocationProgram=SourceAnimationProgramDependencies.SelectedProgram(_script,_cue);
        foreach(var source in programs)
        {
            _programSource=source;
            if(source.SubroutineLabels.Keys.Any(name=>!source.Subroutines.ContainsKey(name)))throw new InvalidDataException("Labels refer to an absent source subroutine.");
            _validatingInvokedProgram=ReferenceEquals(source,_invocationProgram)||source.TailPrograms.Count!=0;
            ValidateProgram(source.Commands,source.Labels,false);
            // ABI/resource validation covers every referenced branch and helper;
            // invocation inputs and companion capacity belong to the selected leaf.
            var invoked=SourceAnimationProgramDependencies.RequiredSubroutines(source.Commands,source.Subroutines);
            foreach(var (name,commands) in source.Subroutines)
            {
                _validatingInvokedProgram=ReferenceEquals(source,_invocationProgram)&&invoked.ContainsKey(name);
                ValidateProgram(commands,LabelsFor(name),true);
            }
        }
        _programSource=_script;_validatingInvokedProgram=true;_validatingSubroutine=false;_programLabels=_script.Labels;ValidatePaletteBackupProgram();
    }
    private void ValidateProgram(SourceAnimationCommand[] commands,IReadOnlyDictionary<string,int> labels,bool subroutine)
    {
        if(commands.Length is <1 or >128)throw new InvalidDataException("Source program or label is outside its bounded command range.");
        _programLabels=labels;_validationProgramLength=commands.Length;_validatingSubroutine=subroutine;
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
            foreach(int next in SourceAnimationProgramDependencies.Successors(commands[pc],pc,labels,subroutine?null:_programSource.TailPrograms))Visit(next);
            state[pc]=2;
        }
        // Validate every instruction, including alternate blocks not selected by
        // this invocation; a backward jump to an earlier return is still acyclic.
        for(int pc=0;pc<commands.Length;pc++)Visit(pc);
    }
    private int MaximumInvocationCost(Func<SourceAnimationCommand,int> cost)=>SourceAnimationProgramDependencies.MaximumCost(_invocationProgram,cost);
    private bool ValidateArgumentCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="jumpargeq")
        {
            if(a.Length!=3||(Number(a[0])<0||Number(a[0])>=AnimationRegisterCount)||AnimationArgument(a[1]) is <short.MinValue or >ushort.MaxValue||!ValidProgramLabel(a[2]))Fail(c,"Argument branch needs a source argument, signed16 comparison and scoped label.");
            return true;
        }
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_GetBattleTerrain")
        {
            if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Terrain predicate takes only source task priority.");
            if(_validatingInvokedProgram&&(_cue.SourceTerrain is not {} terrain||(int)terrain is <0 or >9))Fail(c,"Terrain predicate requires committed native terrain 0 through 9.");
            return true;
        }
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_GetTargetIsAttackerPartner")
        {if(a.Length!=3||Number(a[1]) is <0 or >255||a[2]!="ARG_RET_ID")Fail(c,"Partner predicate preserves its authored ignored return-register argument.");return true;}
        if(c.Op!="createvisualtask"||a.FirstOrDefault() is not ("AnimTask_IsContest" or "AnimTask_IsTargetPlayerSide" or "AnimTask_GetAttackerSide" or "AnimTask_GetTargetSide" or "GetIsDoomDesireHitTurn"))return false;
        if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Source argument predicate takes only task priority.");
        if(a[0]=="GetIsDoomDesireHitTurn"&&_validatingInvokedProgram&&_cue.MoveTurn is null)Fail(c,"Delayed-attack predicate requires the committed source move turn.");
        return true;
    }
    private bool TryRunArgumentPredicate(SourceAnimationCommand c)
    {
        if(c.Args[0]=="AnimTask_GetBattleTerrain")
        {
            _animationArgs[0]=(short)_cue.SourceTerrain!.Value;return true;
        }
        if(c.Args[0]=="GetIsDoomDesireHitTurn")
        {
            // The source has two independent ifs, not an else assignment. Turns
            // above two retain ARG_RET_ID and still destroy the task immediately.
            if(_cue.MoveTurn!.Value<2)_animationArgs[7]=0;
            if(_cue.MoveTurn.Value==2)_animationArgs[7]=1;
            return true;
        }
        short? result=c.Args[0] switch
        {
            "AnimTask_GetTargetIsAttackerPartner"=>(short)((_cue.Actor.Value^2)==_cue.Target.Value?1:0),
            "AnimTask_IsContest"=>0,
            "AnimTask_IsTargetPlayerSide"=>(short)(_cue.Target.IsPlayer?1:0),
            "AnimTask_GetTargetSide"=>(short)(_cue.Target.IsPlayer?0:1),
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
internal static partial class SourceAnimationProgramDependencies
{
    internal static IEnumerable<int> Successors(SourceAnimationCommand command,int pc,IReadOnlyDictionary<string,int> labels,IReadOnlyDictionary<string,SourceAnimationScript>? tails=null)
    {
        int Label(int index)
        {
            if(index>=command.Args.Length||!labels.TryGetValue(command.Args[index],out int target))
                throw new InvalidDataException("Source branch label is missing.");
            return target;
        }
        switch(command.Op)
        {
            case "end":case "return":yield break;
            case "goto":if(tails?.ContainsKey(command.Args[0])!=true)yield return Label(0);yield break;
            case "choosetwoturnanim":yield return Label(0);yield return Label(1);yield break;
            case "jumpifmoveturn":yield return Label(1);break;
            case "jumpargeq":if(tails?.ContainsKey(command.Args[2])!=true)yield return Label(2);break;
        }
        yield return pc+1;
    }
    // A conservative whole-path resource bound. Independent branches do not
    // coexist, while each repeated helper call contributes its full cost.
    internal static int MaximumCost(SourceAnimationScript script,Func<SourceAnimationCommand,int> cost)
    {
        ProgramClosure(script); // Reject dangling references/cycles even in unused branches.
        var roots=new Dictionary<string,int>(StringComparer.Ordinal);
        int Root(SourceAnimationScript owner)
        {
            if(roots.TryGetValue(owner.Label,out int found))return found;
            return roots[owner.Label]=Program(owner,owner.Commands,owner.Labels,false);
        }
        int Program(SourceAnimationScript owner,SourceAnimationCommand[] commands,IReadOnlyDictionary<string,int> labels,bool helper)
        {
            if(commands.Length is <1 or >128)throw new InvalidDataException("Source capacity program length differs.");
            var visiting=new bool[commands.Length];var memo=new int?[commands.Length];
            int Visit(int pc)
            {
                if(pc<0||pc>=commands.Length)throw new InvalidDataException("Source capacity path falls outside its program.");
                if(visiting[pc])throw new InvalidDataException("Source capacity path must be acyclic.");
                if(memo[pc] is {} found)return found;
                visiting[pc]=true;var command=commands[pc];int own=cost(command);
                if(own<0)throw new InvalidDataException("Source capacity cost must be nonnegative.");
                if(command.Op=="call")
                {
                    if(helper||command.Args.Length!=1||!owner.Subroutines.TryGetValue(command.Args[0],out var body))
                        throw new InvalidDataException("Source capacity helper scope differs.");
                    own=checked(own+Program(owner,body,owner.SubroutineLabels.GetValueOrDefault(command.Args[0])??new Dictionary<string,int>(),true));
                }
                string? target=command.Op=="goto"?command.Args.ElementAtOrDefault(0):command.Op=="jumpargeq"?command.Args.ElementAtOrDefault(2):null;
                int tail=target is not null&&!helper&&owner.TailPrograms.TryGetValue(target,out var nextProgram)?Root(nextProgram):0;
                foreach(int next in Successors(command,pc,labels,helper?null:owner.TailPrograms))tail=Math.Max(tail,Visit(next));
                visiting[pc]=false;return (memo[pc]=checked(own+tail)).Value;
            }
            return Visit(0);
        }
        return Root(script);
    }
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
