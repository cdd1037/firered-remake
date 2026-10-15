using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Two candidate owned and four enemy-only exact canonical projections; no ownership grant.</summary>
public static class PretCinnabarPokemon
{
    public const string Profile = "firered-cinnabar-pokemon-v1";
    public static SilphPokemonDocument LoadDocument(string root, string? dependencyProjectRoot = null)
    {
        var source = CinnabarSource.Catalogs(root); var d = source.Parse<SilphPokemonDocument>("pokemon.json");
        SilphSourcePackage.Need(d.SchemaVersion == 1 &&
            d.Species.Select(s => s.SourceSpeciesId).SequenceEqual([98, 99]) &&
            d.OpponentSpecies.Select(s => s.SourceSpeciesId).SequenceEqual([38, 59, 78, 91]) && d.Machines.Length == 6 &&
            d.HeldItems.Length == 6 && d.SourceEvolutions.Length == 6 && !d.Limitations.IsDefaultOrEmpty , "Cinnabar Pokemon projection differs.");
        var canonical = PretCanonicalPokemonCatalog.Load(dependencyProjectRoot ?? root, dependencyProjectRoot ?? root);
        foreach (var s in d.Species.Concat(d.OpponentSpecies))
        {
            SilphSourcePackage.Need(JsonSerializer.Serialize(s) == JsonSerializer.Serialize(canonical.Catalog.Species[s.Id]), "Cinnabar species differs from canonical source.");
            var meta = canonical.Metadata[s.Id]; var original = d.SourceEvolutions.Single(e => e.Species == s.Id);
            SilphSourcePackage.Need(JsonSerializer.Serialize(original.SourceEvolutions) == JsonSerializer.Serialize(meta.SourceEvolutions), "Cinnabar evolution source differs.");
            var h = d.HeldItems.Single(h => h.Species == s.Id);             SilphSourcePackage.Need(h.Common == meta.OriginalFields["itemCommon"] && h.Rare == meta.OriginalFields["itemRare"], "Cinnabar held-item source differs.");
        }
        SilphSourcePackage.Need(d.RequiredMoves.ToHashSet().SetEquals(d.Species.Concat(d.OpponentSpecies).SelectMany(s => s.LevelUpMoves).Select(m => m.Move)) &&
            d.RequiredMoves.All(canonical.Catalog.Moves.ContainsKey), "Cinnabar natural move closure differs.");
        foreach (var n in d.EncodedNames) { SilphSourcePackage.Need(n.Bytes[^1] == 255, "Cinnabar name terminator differs."); }
        return d;
    }
    public static ImmutableArray<PokemonSpeciesDefinition> LoadOpponents(string root, string? dependencyProjectRoot = null) => LoadDocument(root, dependencyProjectRoot).OpponentSpecies;
    /// <summary>Constructs the115-species candidate; caller must authenticate CinnabarOwnedPokemonCatalog.</summary>
    public static PokemonCatalog Extend(PokemonCatalog previous, SilphPokemonDocument document)
    {
        SilphSourcePackage.Need(SilphOwnedPokemonCatalog.For(previous) is not null && previous.Species.Count == 113 && document.Species.Select(s => s.SourceSpeciesId).SequenceEqual([98, 99]), "Cinnabar ownership predecessor differs.");
        var krabby = new SpeciesId("pret:SPECIES_KRABBY"); var kingler = new SpeciesId("pret:SPECIES_KINGLER");
        var edge = document.SourceEvolutions.Single(e => e.Species == krabby).SourceEvolutions.Single();
        SilphSourcePackage.Need(edge.Method == "EVO_LEVEL" && edge.SourceMethodId == 4 && edge.ParameterValue == 28 && edge.Target == kingler &&
            document.Species.All(s => s.Evolutions.IsEmpty), "Krabby evolution source differs.");
        var species = previous.Species.Add(krabby, document.Species[0] with { Evolutions = [new(28, kingler, edge.Source)] }).Add(kingler, document.Species[1]);
        return previous with { Species = species };
    }
}
