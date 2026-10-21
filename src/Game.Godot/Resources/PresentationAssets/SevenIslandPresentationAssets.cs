using Game.Content;
using System.Text.Json;

internal sealed record SevenIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class SevenIslandPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/seven-island/";

    internal static SevenIslandPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "assets");
        var manifest = package.Manifest;
        var files = package.Files;
        int[] owned = [231,232,246,247,248,227,201];
        int[] opponents = [203,237,241,204,235,217];
        int[] rendered = [231,232,246,247,227,201,203,237,241,204,235,217, ..Enumerable.Range(413,27)];
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in rendered from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species,side,variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("renderSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!,s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            sprites.Any(s => s.GetProperty("sourceSpeciesId").GetInt32() !=
                (s.GetProperty("renderSpeciesId").GetInt32() >= 413 ? 201 : s.GetProperty("renderSpeciesId").GetInt32())) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual(owned) ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual(opponents))
            throw new InvalidDataException("Seven Island canonical/form sprite inventory differs.");
        var icons = manifest.GetProperty("pokemonIcons").EnumerateArray().ToArray();
        if (!icons.Select(i => i.GetProperty("renderSpeciesId").GetInt32()).ToHashSet()
                .SetEquals(owned.Concat(Enumerable.Range(413,27))) ||
            icons.Any(i => i.GetProperty("sourceFrameIndex").GetInt32()!=0 || i.GetProperty("sourceFrameCount").GetInt32()!=2) ||
            !manifest.GetProperty("footprints").EnumerateArray().Select(s=>s.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned))
            throw new InvalidDataException("Seven Island owned icon/footprint inventory differs.");
        var inheritedSprites = manifest.GetProperty("inheritedSprites").EnumerateArray().ToArray();
        if (!inheritedSprites.Select(s=>(s.GetProperty("renderSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!,s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(
                from side in new[]{"front","back"} from variant in new[]{"normal","shiny"} select (248,side,variant)))
            throw new InvalidDataException("Inherited Tyranitar sprites differ.");
        var trainers=manifest.GetProperty("trainers").EnumerateArray().ToArray();
        var portraits=manifest.GetProperty("portraits").EnumerateArray().ToArray();
        if (trainers.Any(t=>!portraits.Any(p=>p.GetProperty("id").GetString()==t.GetProperty("portrait").GetString())))
            throw new InvalidDataException("Seven Island trainer inventory differs.");
        return new(manifest.Clone(), files);
    }
}
