using Game.Content;
using System.Text.Json;

internal sealed record SeviiRubyPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class SeviiRubyPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/sevii-ruby/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    internal static SeviiRubyPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Sevii Ruby presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Sevii Ruby presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Sevii Ruby presentation directory symlink rejected.");
            byte[] data = File.ReadAllBytes(file.FullName);
            return data;
        }
        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Sevii Ruby presentation identity differs.");
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
                throw new InvalidDataException("Sevii Ruby asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) && !files.ContainsKey(texture.GetString()!))
                throw new InvalidDataException("Missing presentation texture: " + texture.GetString());
        }
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in new[] { 218, 219 } from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            sprites.Any(s => s.GetProperty("speciesId").GetString() != (s.GetProperty("sourceSpeciesId").GetInt32() == 218 ? "pret:SPECIES_SLUGMA" : "pret:SPECIES_MAGCARGO")) || !manifest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("item").GetString()).ToHashSet().SetEquals(
                ["pret:ITEM_RUBY", "pret:ITEM_RAINBOW_PASS", "pret:ITEM_HM06", "pret:ITEM_BLACK_BELT", "pret:ITEM_DIRE_HIT", "pret:ITEM_FIRE_STONE"]))
            throw new InvalidDataException("Sevii Ruby presentation inventory differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Sevii Ruby cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
