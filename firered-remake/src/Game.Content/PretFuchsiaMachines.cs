using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Projects the already authenticated Fuchsia source rows onto the exact inherited machine catalog.</summary>
public static class PretFuchsiaMachines
{
    public static PewterMachineCatalog Extend(PewterMachineCatalog previous, PretFuchsiaRoutePokemon pokemon)
    {
        EeveeMachineRules.ValidateCatalog(previous);
        ArgumentNullException.ThrowIfNull(pokemon);
        PokemonFactory.ValidateCatalog(pokemon.Catalog);
        if (pokemon.Catalog.Species.Count != 107 || pokemon.Document.Machines.IsDefault || pokemon.Document.Machines.Length != 4)
            throw new InvalidDataException("fuchsia_machine_pokemon_scope");
        ImmutableArray<PewterMachineCompatibility> Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> inherited) =>
            inherited.AddRange(pokemon.Document.Machines.Select(row => new PewterMachineCompatibility(row.Species,
                pokemon.Catalog.Species[row.Species].SourceSpeciesId,
                row.MachineSymbols.Any(symbol => symbol.StartsWith(item.Value[10..] + "_", StringComparison.Ordinal)),
                new(row.Source.Path, row.Source.Symbol, row.Source.StartLine, row.Source.EndLine, row.Source.Sha256))));
        var result = previous with { Profile = FuchsiaMachineRules.Profile,
            Compatibility = Rows(previous.Tm39.Item.Id, previous.Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(machine => machine with {
                Compatibility = Rows(machine.Machine.Item.Id, machine.Compatibility) }).ToImmutableArray() };
        FuchsiaMachineRules.ValidateCatalog(result);
        return result;
    }

    public static string CanonicalSha256(PewterMachineCatalog catalog)
    {
        FuchsiaMachineRules.ValidateCatalog(catalog);
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(catalog, ContentJson.CreateOptions())));
    }
}
