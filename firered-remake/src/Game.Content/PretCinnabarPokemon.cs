using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Two candidate owned and four enemy-only exact canonical projections; no ownership grant.</summary>
public static class PretCinnabarPokemon
{
    public const string Profile = "firered-cinnabar-pokemon-v1";
    public static SilphPokemonDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root, string? dependencyProjectRoot = null)
    {
        var source = CinnabarSource.Catalogs(root); var d = source.Parse<SilphPokemonDocument>("pokemon.json", canonical);
        SilphSourcePackage.Need(d.SchemaVersion == 1 &&
            d.Species.Select(s => s.SourceSpeciesId).SequenceEqual([98, 99]) &&
            d.OpponentSpecies.Select(s => s.SourceSpeciesId).SequenceEqual([38, 59, 78, 91]) && d.Machines.Length == 6 &&
            d.HeldItems.Length == 6 && d.SourceEvolutions.Length == 6 , "Cinnabar Pokemon projection differs.");
        foreach (var s in d.Species.Concat(d.OpponentSpecies))
        {
            SilphSourcePackage.Need(JsonSerializer.Serialize(s) == JsonSerializer.Serialize(canonical.Catalog.Species[s.Id]), "Cinnabar species differs from canonical source.");
            var meta = canonical.Metadata[s.Id]; var original = d.SourceEvolutions.Single(e => e.Species == s.Id);
            SilphSourcePackage.Need(JsonSerializer.Serialize(original.SourceEvolutions) == JsonSerializer.Serialize(meta.SourceEvolutions), "Cinnabar evolution source differs.");
            var h = d.HeldItems.Single(h => h.Species == s.Id);             SilphSourcePackage.Need(h.Common == meta.ItemCommon && h.Rare == meta.ItemRare, "Cinnabar held-item source differs.");
        }
        SilphSourcePackage.Need(d.RequiredMoves.ToHashSet().SetEquals(d.Species.Concat(d.OpponentSpecies).SelectMany(s => s.LevelUpMoves).Select(m => m.Move)) &&
            d.RequiredMoves.All(canonical.Catalog.Moves.ContainsKey), "Cinnabar natural move closure differs.");
        foreach (var n in d.EncodedNames) { SilphSourcePackage.Need(n.Bytes[^1] == 255, "Cinnabar name terminator differs."); }
        return d;
    }
}
