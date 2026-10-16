using Game.Content;
using System.Text.Json;

internal sealed record FourIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class FourIslandPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/four-island/";

    internal static FourIslandPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "assets");
        var manifest = package.Manifest;
        var files = package.Files;
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in new[] { 86, 87, 194, 195, 220, 221, 225 } from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            manifest.GetProperty("trainers")[0].GetProperty("trainer").GetString() != "pret:TRAINER_TEAM_ROCKET_GRUNT_45" || !manifest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("item").GetString()).ToHashSet().SetEquals(
                ["pret:ITEM_HM07", "pret:ITEM_NEVER_MELT_ICE"]) || !manifest.GetProperty("tiles").EnumerateArray().Select(t => t.GetProperty("metatileId").GetInt32()).ToHashSet().SetEquals([0x35A, 0x35B]))
            throw new InvalidDataException("Four Island presentation inventory differs.");
        return new(manifest.Clone(), files);
    }
}
