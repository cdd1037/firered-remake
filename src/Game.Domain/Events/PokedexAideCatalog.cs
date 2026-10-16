using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Domain.Events;

public sealed record PokedexAideEffectKey(StorySource Source, string Opcode);
public sealed record PokedexAideDefinition(MapId Map, FieldActorDefinition Actor, int RequiredCaught,
    QuestItemDefinition Item, int Quantity, string RewardFlag,
    ImmutableDictionary<PokedexAideEffectKey, StoryInstruction> Effects);

/// <summary>One-time caught-count rewards, bound from the executable story and actor definitions.
/// No map, script-label or numeric reward table participates in admission.</summary>
public sealed class PokedexAideCatalog
{
    public static PokedexAideCatalog Empty { get; } = new(
        ImmutableDictionary<(MapId, string), PokedexAideDefinition>.Empty);
    public ImmutableDictionary<(MapId Map, string Actor), PokedexAideDefinition> ByActor { get; }

    private PokedexAideCatalog(ImmutableDictionary<(MapId, string), PokedexAideDefinition> actors)
        => ByActor = actors;

    public static PokedexAideCatalog Bind(IStoryCatalog scripts, IEnumerable<FieldMapDefinition> maps,
        InventoryCatalog inventory)
    {
        var actors = ImmutableDictionary.CreateBuilder<(MapId, string), PokedexAideDefinition>();
        foreach (var map in maps)
        foreach (var actor in map.Actors.Where(actor => !actor.IsClone))
        {
            var entry = scripts.FindProgram(actor.ScriptId);
            if (entry is null || !entry.Instructions.Any(IsCount)) continue;
            // Other Pokedex services also count Pokemon. The caught threshold plus item
            // reward distinguishes this service without relying on actor or region names.
            var instructions = entry.Instructions;
            if (!instructions.Any(IsCaughtGate) || !instructions.Any(i => i.Opcode == "additem")) continue;
            string Invalid(string reason) => "pokedex_aide_" + reason + ":" + entry.Id;
            int Value(string token) => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value :
                token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token[2..], NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out value) ? value : scripts.TryConstant(token, out value) ? value :
                throw new InvalidDataException(Invalid("constant_missing") + ":" + token);
            int One(Func<StoryInstruction, bool> match)
            {
                var indices = instructions.Select((instruction, index) => (instruction, index))
                    .Where(pair => match(pair.instruction)).Select(pair => pair.index).ToArray();
                return indices.Length == 1 ? indices[0] : throw new InvalidDataException(Invalid("instruction_not_unique"));
            }
            int count = One(IsCount), gate = One(IsCaughtGate), space = One(i => i.Opcode == "checkitemspace"),
                add = One(i => i.Opcode == "additem"), flag = One(i => i.Op == StoryOp.SetFlag);
            var reward = instructions[add]; var flagInstruction = instructions[flag];
            if (reward.Args.Length is < 1 or > 2 || !reward.Args[0].StartsWith("ITEM_", StringComparison.Ordinal) ||
                flagInstruction.Args.Length != 1 || !flagInstruction.Args[0].StartsWith("FLAG_", StringComparison.Ordinal))
                throw new InvalidDataException(Invalid("reward_invalid"));
            string rewardFlag = flagInstruction.Args[0];
            int guard = One(i => i.Op == StoryOp.FlagGoto && i.Opcode == "goto_if_set" &&
                i.Args.Length == 2 && i.Args[0] == rewardFlag);
            int selector = One(i => i.Op == StoryOp.SetVar && i.Args.Length == 2 && i.Args[0] == "VAR_0x8004");
            int required = Value(instructions[gate].Args[1]), quantity = reward.Args.Length == 1 ? 1 : Value(reward.Args[1]);
            var spaceInstruction = instructions[space];
            if (!(guard < selector && selector < count && count < gate && gate < space && space < add && add < flag) ||
                Value(instructions[selector].Args[1]) != 0 || required is < 1 or > ushort.MaxValue ||
                quantity < 1 || quantity > inventory.Limits.MaximumStack || spaceInstruction.Args.Length is < 1 or > 2 ||
                spaceInstruction.Args[0] != reward.Args[0] ||
                (spaceInstruction.Args.Length == 1 ? 1 : Value(spaceInstruction.Args[1])) != quantity ||
                space + 1 >= add || instructions[space + 1] is not { Op: StoryOp.ConditionalGoto, Opcode: "goto_if_eq" } capacity ||
                capacity.Args.Length != 3 || capacity.Args[0] != "VAR_RESULT" || Value(capacity.Args[1]) != 0)
                throw new InvalidDataException(Invalid("reward_flow_invalid"));
            var itemId = new ItemId("pret:" + reward.Args[0]);
            if (!inventory.Items.TryGetValue(itemId, out var item) || item.SourceItemId != Value(reward.Args[0]) ||
                item.SourceItemSymbol != reward.Args[0] || !scripts.TryConstant(rewardFlag, out _) ||
                !scripts.TryConstant(item.Pocket, out _))
                throw new InvalidDataException(Invalid("reward_definition_missing"));
            var graph = ReachablePrograms(scripts, entry.Id).ToArray();
            if (graph.Any(program => program.Instructions.Any(i => i.Op == StoryOp.Unsupported)))
                throw new InvalidDataException(Invalid("unsupported_instruction"));
            var effects = ImmutableDictionary.CreateBuilder<PokedexAideEffectKey, StoryInstruction>();
            foreach (var instruction in graph.SelectMany(program => program.Instructions).Where(IsHandledEffect))
            {
                var key = new PokedexAideEffectKey(instruction.Source, instruction.Opcode);
                if (effects.TryGetValue(key, out var previous) &&
                    (previous.Op != instruction.Op || !previous.Args.SequenceEqual(instruction.Args)))
                    throw new InvalidDataException(Invalid("effect_conflict"));
                effects[key] = instruction;
            }
            if (!effects.Values.Any(i => i.Opcode == "buffernumberstring" && i.Args.Length == 2 &&
                    i.Args[0] == "STR_VAR_1" && Value(i.Args[1]) == required) ||
                !effects.Values.Any(i => i.Opcode == "bufferitemname" && i.Args.SequenceEqual(new[] { "STR_VAR_2", reward.Args[0] })))
                throw new InvalidDataException(Invalid("request_buffers_missing"));
            actors.Add((map.Id, actor.LocalId), new(map.Id, actor, required, item, quantity, rewardFlag, effects.ToImmutable()));
        }
        return new(actors.ToImmutable());
    }

    private static bool IsCount(StoryInstruction instruction) => instruction.Opcode == "specialvar" &&
        instruction.Args.SequenceEqual(new[] { "VAR_RESULT", "GetPokedexCount" });
    private static bool IsCaughtGate(StoryInstruction instruction) => instruction.Op == StoryOp.ConditionalGoto &&
        instruction.Opcode == "goto_if_lt" && instruction.Args.Length == 3 && instruction.Args[0] == "VAR_0x8006";
    private static bool IsHandledEffect(StoryInstruction instruction) => IsCount(instruction) || instruction.Opcode is
        "buffernumberstring" or "checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural";

    private static IEnumerable<StoryProgram> ReachablePrograms(IStoryCatalog scripts, string entry)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(); pending.Push(entry);
        while (pending.TryPop(out var id))
        {
            if (!seen.Add(id)) continue;
            var program = scripts.FindProgram(id) ?? throw new InvalidDataException("pokedex_aide_program_missing:" + id);
            yield return program;
            foreach (var instruction in program.Instructions)
                if (instruction.Op is StoryOp.Call or StoryOp.Goto or StoryOp.ConditionalCall or
                    StoryOp.ConditionalGoto or StoryOp.FlagCall or StoryOp.FlagGoto)
                {
                    if (instruction.Args.IsEmpty) throw new InvalidDataException("pokedex_aide_branch_invalid:" + id);
                    pending.Push(instruction.Args[^1]);
                }
        }
    }
}
