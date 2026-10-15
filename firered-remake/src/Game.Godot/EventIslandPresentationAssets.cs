using Game.Content;
using System.Text.Json;

internal sealed record EventIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class EventIslandPresentationAssets
{
    internal const string Base = "content/generated/event-island-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    internal static EventIslandPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Event Island presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Event Island presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Event Island presentation directory symlink rejected.");
            byte[] data = File.ReadAllBytes(file.FullName);
            return data;
        }
        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        const string profile = "firered-event-island-presentation-v1";
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile)
            throw new InvalidDataException("Event Island presentation identity differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.GetProperty("inheritedInputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            if (!path.EndsWith(".png", StringComparison.Ordinal)) continue;
            files.Add(path, Read(path));
        }
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
        }
        foreach (var asset in manifest.GetProperty("assets").EnumerateArray())
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("texturePath").GetString()!;
            if (width is <= 0 or > 256 || height is <= 0 or > 256 || !files.TryGetValue(path, out var rgba) ||
                PngRgba8.Decode(rgba, width, height).Length != width * height * 4)
                throw new InvalidDataException("Event Island asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) && !files.ContainsKey(texture.GetString()!))
                throw new InvalidDataException("Missing presentation texture: " + texture.GetString());
        }
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
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Event Island cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
