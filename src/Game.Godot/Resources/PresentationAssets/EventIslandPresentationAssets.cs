using Game.Content;
using System.Text.Json;

internal sealed record EventIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class EventIslandPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/event-island/";

    internal static EventIslandPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "assets");
        var manifest = package.Manifest;
        var files = package.Files;
        int[] owned = [249,250,410];
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in owned from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species,side,variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!,s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s=>s.GetInt32()).SequenceEqual(owned) ||
            sprites.Any(s=>s.GetProperty("renderSpeciesId").GetInt32()!=s.GetProperty("sourceSpeciesId").GetInt32() ||
                s.GetProperty("sourceFrameIndex").GetInt32()!=(s.GetProperty("sourceSpeciesId").GetInt32()==410?1:0)))
            throw new InvalidDataException("Event Island source sprite inventory differs.");
        foreach(string property in new[]{"pokemonIcons","footprints"})
            if(!manifest.GetProperty(property).EnumerateArray()
                .Select(r=>r.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned))
                throw new InvalidDataException("Event Island owned icon/footprint inventory differs.");
        var triangle=manifest.GetProperty("triangle").EnumerateArray().ToArray();
        if(!triangle.Select(r=>r.GetProperty("interactionNumber").GetInt32()).SequenceEqual(Enumerable.Range(0,11)) ||
            triangle.Any(r=>r.GetProperty("objectPaletteSlot").GetInt32()!=10 || r.GetProperty("width").GetInt32()!=32 || r.GetProperty("height").GetInt32()!=32))
            throw new InvalidDataException("Event Island triangle effect inventory differs.");
        return new(manifest.Clone(), files);
    }
}
