using Game.Content;
using System.Text.Json;

internal sealed record MewtwoPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class MewtwoPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/mewtwo/";

    internal static MewtwoPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "assets");
        var manifest = package.Manifest;
        var files = package.Files;
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (150, side, variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            manifest.GetProperty("pokemonIcons")[0].GetProperty("sourceSpeciesId").GetInt32() != 150 ||
            manifest.GetProperty("footprints")[0].GetProperty("sourceSpeciesId").GetInt32() != 150 ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual([150]))
            throw new InvalidDataException("Mewtwo presentation inventory differs.");
        return new(manifest.Clone(), files);
    }
}
