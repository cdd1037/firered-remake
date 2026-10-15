using System.Collections.Immutable;
using Game.Domain.Items;

namespace Game.Content;

/// <summary>Two exact source rows added to all24 existing machines; no new machines.</summary>
public static class PretSurfMachines
{
    public static PewterMachineCatalog Extend(SafariMachineCatalog previous, PretSurfPokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(previous); ArgumentNullException.ThrowIfNull(pokemon);
        SafariSource.Require(pokemon.Catalog.Species.Count == 112 , "Surf machine predecessor differs.");
        var old = previous.Catalog;
        ImmutableArray<PewterMachineCompatibility> Rows(Game.Domain.Ids.ItemId machine, ImmutableArray<PewterMachineCompatibility> inherited) =>
            inherited.AddRange(pokemon.Document.Machines.Select(row => new PewterMachineCompatibility(row.Species, pokemon.Catalog.Species[row.Species].SourceSpeciesId,
                row.MachineSymbols.Any(s => s.StartsWith(machine.Value[10..] + "_", StringComparison.Ordinal)))));
        return old with { Profile = SurfMachineCatalog.Profile,Compatibility = Rows(old.Tm39.Item.Id, old.Compatibility),AdditionalMachines = old.AdditionalMachines!.Value.Select(m => m with { Compatibility = Rows(m.Machine.Item.Id, m.Compatibility) }).ToImmutableArray() };
    }
}
