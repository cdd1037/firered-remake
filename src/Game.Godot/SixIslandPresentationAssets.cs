using Game.Content;
using System.Text.Json;

internal sealed record SixIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class SixIslandPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/six-island/";

    internal static SixIslandPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "assets");
        var manifest = package.Manifest;
        var files = package.Files;
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        int[] owned = [161,162,177,178,193,202,182], opponentOnly = [191,192,170,171,185];
        var expectedSprites = (from species in owned.Concat(opponentOnly) from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            !manifest.GetProperty("footprints").EnumerateArray().Select(s=>s.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s=>s.GetInt32()).ToHashSet().SetEquals(owned) ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s=>s.GetInt32()).ToHashSet().SetEquals(opponentOnly) || !manifest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("item").GetString()).ToHashSet().SetEquals(
                ["pret:ITEM_SUN_STONE", "pret:ITEM_DRAGON_SCALE", "pret:ITEM_DREAM_MAIL"]) || !manifest.GetProperty("tiles").EnumerateArray().Select(t => t.GetProperty("metatileId").GetInt32()).ToHashSet().SetEquals([0x357, 0x358]))
            throw new InvalidDataException("Six Island presentation inventory differs.");
        return new(manifest.Clone(), files);
    }
}
