using Game.Content;
using System.Text.Json;

internal sealed record PowerPlantPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class PowerPlantPresentationAssets
{
    internal const string Base = "content/generated/power-plant-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    internal static PowerPlantPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Power Plant presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Power Plant presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Power Plant presentation directory symlink rejected.");
            byte[] data = File.ReadAllBytes(file.FullName);
            return data;
        }
        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        const string profile = "firered-power-plant-presentation-v1";
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile)
            throw new InvalidDataException("Power Plant presentation identity differs.");
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
                throw new InvalidDataException("Power Plant asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) && !files.ContainsKey(texture.GetString()!))
                throw new InvalidDataException("Missing presentation texture: " + texture.GetString());
        }
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
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Power Plant cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
