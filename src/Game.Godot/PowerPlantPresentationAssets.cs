using Game.Content;
using System.Text.Json;

internal sealed record PowerPlantPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class PowerPlantPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/power-plant/";

    internal static PowerPlantPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "assets");
        var manifest = package.Manifest;
        var files = package.Files;
        int[] owned = [125,135,145];
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in owned from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species,side,variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!,s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s=>s.GetInt32()).SequenceEqual(owned) ||
            sprites.Any(s=>s.GetProperty("renderSpeciesId").GetInt32()!=s.GetProperty("sourceSpeciesId").GetInt32() ||
                s.GetProperty("sourceFrameIndex").GetInt32()!=0))
            throw new InvalidDataException("Power Plant source sprite inventory differs.");
        foreach(string property in new[]{"pokemonIcons","footprints"})
            if(!manifest.GetProperty(property).EnumerateArray()
                .Select(r=>r.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned))
                throw new InvalidDataException("Power Plant owned icon/footprint inventory differs.");
        return new(manifest.Clone(), files);
    }
}
