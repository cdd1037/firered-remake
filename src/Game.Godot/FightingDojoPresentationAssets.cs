using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Content;

internal sealed record FightingDojoDexDocument(int SchemaVersion,
    ImmutableArray<PokedexMenuEntry> Entries);
internal sealed record FightingDojoPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Two source dex footprints plus authenticated bindings to existing League and Black Belt art.</summary>
internal static class FightingDojoPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/fighting-dojo/";
    internal const string Profile = "firered-fighting-dojo-presentation-v1";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    internal static FightingDojoPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Fighting Dojo presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Fighting Dojo resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Fighting Dojo directory symlink rejected.");
            var bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }
        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Fighting Dojo presentation identity differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
        }
        foreach (var asset in new[] { "sprites", "pokemonIcons", "footprints", "portraits" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
        {
            string path = asset.GetProperty("texturePath").GetString()!;
            if (!files.ContainsKey(path)) files.Add(path, Read(path));
        }
        foreach (string kind in new[] { "sprites", "pokemonIcons", "footprints", "portraits" })
        foreach (var asset in manifest.GetProperty(kind).EnumerateArray())
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("texturePath").GetString()!;
            if (width is <= 0 or > 64 || height is <= 0 or > 64 || !files.TryGetValue(path, out var rgba) ||
                !NativeImages.HasSize(rgba, width, height))
                throw new InvalidDataException("Fighting Dojo pixels differ: " + path);
        }
        var dex = JsonSerializer.Deserialize<FightingDojoDexDocument>(files[Base + "dex-entries.json"], new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true
        }) ?? throw new InvalidDataException("Fighting Dojo dex document missing.");
        if (dex.SchemaVersion != 1 ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(new[] { 106, 107 }) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(new[] { 106, 107 }) ||
            !manifest.GetProperty("trainers").EnumerateArray().Select(e => e.GetProperty("sourceTrainerId").GetInt32()).SequenceEqual(Enumerable.Range(317, 5)) ||
            manifest.GetProperty("audio").GetProperty("newCueCount").GetInt32() != 0)
            throw new InvalidDataException("Fighting Dojo presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!, e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png))
                throw new InvalidDataException("Fighting Dojo dex texture differs.");
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
