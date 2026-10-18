namespace Game.Presentation.BattleAnimations;

internal static partial class SourceAnimationProgramDependencies
{
    // The resolver binds source labels against its one canonical catalog. A tail
    // has no return frame; helpers retain their original one-level call contract.
    internal static SourceAnimationScript BindReferences(SourceAnimationScript root,IReadOnlyDictionary<string,SourceAnimationScript> catalog)
    {
        var active=new HashSet<string>(StringComparer.Ordinal);
        var resolved=new Dictionary<string,SourceAnimationScript>(StringComparer.Ordinal);
        SourceAnimationScript Bind(SourceAnimationScript source)
        {
            if(active.Contains(source.Label))throw new InvalidDataException("Source tail programs must be acyclic.");
            if(resolved.TryGetValue(source.Label,out var known))return known;
            active.Add(source.Label);
            var tails=new Dictionary<string,SourceAnimationScript>(StringComparer.Ordinal);
            foreach(string label in source.ProgramReferences)
            {
                if(string.IsNullOrEmpty(label)||source.Labels.ContainsKey(label)||!catalog.TryGetValue(label,out var target)||target.Label!=label||!tails.TryAdd(label,Bind(target)))
                    throw new InvalidDataException("Missing, duplicate or ambiguous source tail program.");
            }
            active.Remove(source.Label);var result=source with{TailPrograms=tails};resolved.Add(source.Label,result);return result;
        }
        var bound=Bind(root);ProgramClosure(bound);return bound;
    }
    internal static SourceAnimationScript[] ProgramClosure(SourceAnimationScript root)
    {
        var result=new List<SourceAnimationScript>();var active=new HashSet<string>(StringComparer.Ordinal);var visited=new HashSet<string>(StringComparer.Ordinal);
        void Visit(SourceAnimationScript source)
        {
            if(active.Contains(source.Label))throw new InvalidDataException("Source tail programs must be acyclic.");
            if(visited.Contains(source.Label))return;
            if(source.Commands.Length is <1 or >128)throw new InvalidDataException("Source program exceeds its per-program command bound.");
            if(source.ProgramReferences.Distinct(StringComparer.Ordinal).Count()!=source.ProgramReferences.Length||!source.ProgramReferences.ToHashSet(StringComparer.Ordinal).SetEquals(source.TailPrograms.Keys))
                throw new InvalidDataException("Source program references have not been canonically resolved.");
            if(source.TailPrograms.Count!=0)ValidateSelector(source);
            active.Add(source.Label);result.Add(source);
            foreach(var (label,target) in source.TailPrograms)
            {
                if(target.Label!=label||source.Labels.ContainsKey(label))throw new InvalidDataException("Source tail program label differs.");
                Visit(target);
            }
            active.Remove(source.Label);visited.Add(source.Label);
        }
        Visit(root);return result.ToArray();
    }
    private static void ValidateSelector(SourceAnimationScript source)
    {
        // This bounded selector contract deliberately excludes effect-producing
        // prefixes. Such prefixes need their own companion/lifetime proof rather
        // than disappearing from the selected leaf's capacity calculation.
        var commands=source.Commands;
        if(commands.Length<2||source.Labels.Count!=0||source.Subroutines.Count!=0||commands[0].Op!="createvisualtask"||commands[0].Args.Length!=2||commands[0].Args[0]!="AnimTask_GetBattleTerrain"||!int.TryParse(commands[0].Args[1],out int priority)||priority is <0 or >255)
            throw new InvalidDataException("Tail selector must begin with one synchronous terrain predicate.");
        var targets=new HashSet<string>(StringComparer.Ordinal);
        for(int i=1;i<commands.Length;i++)
        {
            var c=commands[i];string target;
            if(i==commands.Length-1)
            {
                if(c.Op!="goto"||c.Args.Length!=1)throw new InvalidDataException("Tail selector needs an unconditional terminal transfer.");
                target=c.Args[0];
            }
            else
            {
                if(c.Op!="jumpargeq"||c.Args.Length!=3||c.Args[0]!="0"||!int.TryParse(c.Args[1],out int value)||value is <0 or >9)
                    throw new InvalidDataException("Tail selector comparison differs from native terrain ABI.");
                target=c.Args[2];
            }
            if(!source.TailPrograms.ContainsKey(target))throw new InvalidDataException("Tail selector references an absent canonical program.");
            targets.Add(target);
        }
        if(!targets.SetEquals(source.ProgramReferences))throw new InvalidDataException("Tail selector has unused program references.");
    }
    internal static SourceAnimationScript SelectedProgram(SourceAnimationScript root,CommittedMoveAnimation cue)
    {
        if(root.TailPrograms.Count==0)return root;
        if(cue.SourceTerrain is not {} terrain||(int)terrain is <0 or >9)
            throw new SourceAnimationException(new(root.MoveId,root.Label,root.Commands[0].Line,root.Commands[0].Op,"Terrain selector requires committed native terrain 0 through 9."));
        var current=root;
        while(current.TailPrograms.Count!=0)
        {
            string target=current.Commands[^1].Args[0];
            foreach(var c in current.Commands.Skip(1))
                if(c.Op=="jumpargeq"&&int.Parse(c.Args[1],System.Globalization.CultureInfo.InvariantCulture)==(int)terrain){target=c.Args[2];break;}
            current=current.TailPrograms[target];
        }
        return current;
    }
}
