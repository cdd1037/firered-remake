using Game.Content;
using System.Text.Json;

internal sealed record LeagueRematchPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class LeagueRematchPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/league-rematch/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    internal static LeagueRematchPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe league rematch presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("League rematch presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("League rematch presentation directory symlink rejected.");
            byte[] data = File.ReadAllBytes(file.FullName);
            return data;
        }
        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("League rematch presentation identity differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var asset in manifest.GetProperty("assets").EnumerateArray())
        {
            string path = asset.GetProperty("texturePath").GetString()!;
            if (!files.ContainsKey(path)) files.Add(path, Read(path));
        }
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            if (!files.ContainsKey(path)) files.Add(path, Read(path));
        }
        foreach (var asset in manifest.GetProperty("assets").EnumerateArray())
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("texturePath").GetString()!;
            if (width is <= 0 or > 256 || height is <= 0 or > 256 || !files.TryGetValue(path, out var rgba) ||
                !NativeImages.HasSize(rgba, width, height))
                throw new InvalidDataException("League rematch asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) && !files.ContainsKey(texture.GetString()!))
                throw new InvalidDataException("Missing presentation texture: " + texture.GetString());
        }
        int[] admitted = [169, 200, 214, 230, 248];
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in admitted from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual(admitted))
            throw new InvalidDataException("League rematch presentation inventory differs.");
        var inheritedSprites = manifest.GetProperty("inheritedSprites").EnumerateArray().ToArray();
        var expectedInheritedSprites = (from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (208, side, variant)).ToHashSet();
        if (!inheritedSprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedInheritedSprites))
            throw new InvalidDataException("Inherited Steelix sprite inventory differs.");
        var trainers = manifest.GetProperty("trainers").EnumerateArray().ToArray();
        if (!trainers.Select(t => t.GetProperty("sourceTrainerId").GetInt32()).Order().SequenceEqual(Enumerable.Range(735, 7)))
            throw new InvalidDataException("League rematch trainer inventory differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("League rematch cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
