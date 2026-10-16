using Game.Content;
using System.Text.Json;

internal sealed record SevenIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class SevenIslandPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/seven-island/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    internal static SevenIslandPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Seven Island presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Seven Island presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Seven Island presentation directory symlink rejected.");
            byte[] data = File.ReadAllBytes(file.FullName);
            return data;
        }
        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Seven Island presentation identity differs.");
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
                throw new InvalidDataException("Seven Island asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) && !files.ContainsKey(texture.GetString()!))
                throw new InvalidDataException("Missing presentation texture: " + texture.GetString());
        }
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
