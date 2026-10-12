using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Authenticated Slowbro80 projection, with no owned-catalog construction or promotion API.</summary>
public static class PretSaffronGymPokemon
{
    public const string Profile = "firered-saffron-gym-pokemon-v1";
    public const string GeneratedSha256 = "a59beaae004857c7c87959d442a6bbac4527ce54dc46c00a7e277d88b6dc8d14";
    public static SilphPokemonDocument LoadDocument(string root, string? dependencyProjectRoot = null)
    {
        var source = SaffronGymSource.Catalogs(root); var d = source.Parse<SilphPokemonDocument>("pokemon.json");
        SilphSourcePackage.Need(d.SchemaVersion == 1 && d.Profile == Profile && d.SourceCommit == PokemonFactory.SourceCommit && d.Species.IsEmpty &&
            d.OpponentSpecies.Select(s => s.SourceSpeciesId).SequenceEqual([80]) && d.SpeciesNames.Length == 1 && d.EncodedNames.Length == 1 &&
            d.Machines.Length == 1 && d.HeldItems.Length == 1 && d.SourceEvolutions.Length == 1 && !d.Limitations.IsDefaultOrEmpty &&
            d.CanonicalCatalogSha256 == PretCanonicalPokemonCatalog.CatalogSha256, "Saffron Gym enemy-only projection differs.");
        var canonical = PretCanonicalPokemonCatalog.Load(dependencyProjectRoot ?? root, dependencyProjectRoot ?? root);
        var slowbro = d.OpponentSpecies[0]; var meta = canonical.Metadata[slowbro.Id];
        SilphSourcePackage.Need(JsonSerializer.Serialize(slowbro) == JsonSerializer.Serialize(canonical.Catalog.Species[slowbro.Id]) &&
            JsonSerializer.Serialize(d.SourceEvolutions[0].SourceEvolutions) == JsonSerializer.Serialize(meta.SourceEvolutions) &&
            d.HeldItems[0].Common == meta.OriginalFields["itemCommon"] && d.HeldItems[0].Rare == meta.OriginalFields["itemRare"] &&
            d.RequiredMoves.ToHashSet().SetEquals(slowbro.LevelUpMoves.Select(m => m.Move)) && d.RequiredMoves.All(canonical.Catalog.Moves.ContainsKey),
            "Saffron Gym canonical Slowbro differs.");
        source.Reference(slowbro.Source); source.Reference(slowbro.LearnsetSource);
        foreach (var row in slowbro.LevelUpMoves) source.Reference(row.Source);
        foreach (var row in d.SourceEvolutions[0].SourceEvolutions) source.Reference(row.Source);
        source.Reference(d.HeldItems[0].Source); source.Reference(d.SpeciesNames[0].Source); source.Reference(d.Machines[0].Source);
        source.Reference(d.EncodedNames[0].Source); source.Reference(d.EncodedNames[0].EncodingSource);
        SilphSourcePackage.Need(d.EncodedNames[0].Bytes[^1] == 255, "Saffron Gym source name terminator differs.");
        return d;
    }
    public static ImmutableArray<PokemonSpeciesDefinition> LoadOpponents(string root, string? dependencyProjectRoot = null) => LoadDocument(root, dependencyProjectRoot).OpponentSpecies;
}
