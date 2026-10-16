using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

public sealed record OrdinaryMartDefinition(FieldActorDefinition Actor, EarlyShopStock Stock,
    StoryInstruction Instruction);

/// <summary>Ordinary counters are admitted by loaded actors, executable scripts and current stock.
/// Binding happens once at composition; opening a counter needs no region-name table.</summary>
public sealed class OrdinaryMartCatalog
{
    public static OrdinaryMartCatalog Empty { get; } = new(
        ImmutableDictionary<(MapId, string), OrdinaryMartDefinition>.Empty);
    public ImmutableDictionary<(MapId Map, string Actor), OrdinaryMartDefinition> ByActor { get; }

    private OrdinaryMartCatalog(ImmutableDictionary<(MapId, string), OrdinaryMartDefinition> actors)
        => ByActor = actors;

    public static OrdinaryMartCatalog Bind(IStoryCatalog scripts, IEnumerable<FieldMapDefinition> maps,
        EarlyShopCatalog items)
    {
        var stocks = items.Shops.ToImmutableDictionary(stock => stock.Id, StringComparer.Ordinal);
        var actors = ImmutableDictionary.CreateBuilder<(MapId, string), OrdinaryMartDefinition>();
        foreach (var map in maps)
        foreach (var actor in map.Actors.Where(actor => !actor.IsClone))
        {
            var program = scripts.FindProgram(actor.ScriptId);
            if (program is null) continue;
            // Conditional counters and special catalogs retain their own handlers. This
            // binding covers a direct pokemart instruction backed by shared current stock.
            var counters = program.Instructions.Where(instruction => instruction.Opcode == "pokemart" &&
                instruction.Args.Length == 1 && stocks.ContainsKey(instruction.Args[0])).ToArray();
            if (counters.Length == 0) continue;
            if (counters.Length != 1 || counters[0].Op != StoryOp.Special ||
                counters[0].Source.Symbol != actor.ScriptId ||
                program.Instructions.Any(instruction => instruction.Op == StoryOp.Unsupported))
                throw new InvalidDataException("ordinary_mart_program_invalid:" + actor.ScriptId);
            var instruction = counters[0];
            actors.Add((map.Id, actor.LocalId), new(actor, stocks[instruction.Args[0]], instruction));
        }
        return new(actors.ToImmutable());
    }
}
