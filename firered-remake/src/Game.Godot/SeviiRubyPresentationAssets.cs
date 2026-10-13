using System.Security.Cryptography;
using System.Text.Json;

internal sealed record SeviiRubyPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class SeviiRubyPresentationAssets
{
    internal const string Base = "content/generated/sevii-ruby-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "fa9e228142afc95229df85024cbdab491e04bedb74cf8d0e5aee579bd5de44b8";
    internal const string ManifestSha256 = "75f8ade3692f93f604fb62d68c611675219acc542d4255aeffc76b612abd7a35";
    internal const string SynthSha256 = "33d4b1cbf7fdcc4a06239c25a313c6fc2819f2ad71a86c11b4db914b963b6c35";
    internal const int LockBytes = 36155;
    internal const int ManifestBytes = 111853;
    internal const int SynthBytes = 1254;
    internal const int SynthCueCount = 3;

    internal static SeviiRubyPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Sevii Ruby presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Sevii Ruby presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Sevii Ruby presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("Sevii Ruby presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Sevii Ruby presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/sevii-ruby-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-sevii-ruby-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 140 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 9 ||
            manifest.GetProperty("outputs").GetArrayLength() != 65)
            throw new InvalidDataException("Sevii Ruby presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/sevii-ruby-presentation/" + row.GetProperty("path").GetString(),
                row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in sourceLock.GetProperty("inheritedInputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            files.Add(path, Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        foreach (var asset in manifest.GetProperty("assets").EnumerateArray())
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("rgbaPath").GetString()!;
            if (width is <= 0 or > 256 || height is <= 0 or > 256 || !files.TryGetValue(path, out var rgba) ||
                rgba.Length != width * height * 4 || Convert.ToHexStringLower(SHA256.HashData(rgba)) != asset.GetProperty("rgbaSha256").GetString())
                throw new InvalidDataException("Sevii Ruby asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("Sevii Ruby texture differs: " + texture.GetString());
        }
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in new[] { 218, 219 } from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (sprites.Length != 8 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            sprites.Any(s => s.GetProperty("speciesId").GetString() != (s.GetProperty("sourceSpeciesId").GetInt32() == 218 ? "pret:SPECIES_SLUGMA" : "pret:SPECIES_MAGCARGO")) ||
            manifest.GetProperty("pokemonIcons").GetArrayLength() != 2 || manifest.GetProperty("footprints").GetArrayLength() != 2 ||
            manifest.GetProperty("trainers").GetArrayLength() != 17 || manifest.GetProperty("portraits").GetArrayLength() != 11 ||
            manifest.GetProperty("portraits").EnumerateArray().Count(p => p.GetProperty("inherited").GetBoolean()) != 7 ||
            manifest.GetProperty("trainers").EnumerateArray().Count(t => t.GetProperty("doubleBattle").GetBoolean()) != 1 ||
            manifest.GetProperty("items").GetArrayLength() != 6 || !manifest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("item").GetString()).ToHashSet().SetEquals(
                ["pret:ITEM_RUBY", "pret:ITEM_RAINBOW_PASS", "pret:ITEM_HM06", "pret:ITEM_BLACK_BELT", "pret:ITEM_DIRE_HIT", "pret:ITEM_FIRE_STONE"]) ||
            manifest.GetProperty("tiles").GetArrayLength() != 2 || manifest.GetProperty("layers").GetArrayLength() != 6 ||
            manifest.GetProperty("terrains").GetArrayLength() != 1 || manifest.GetProperty("braille").GetProperty("glyphs").GetArrayLength() != 29 ||
            manifest.GetProperty("braille").GetProperty("messages").GetArrayLength() != 18 || manifest.GetProperty("assets").GetArrayLength() != 39)
            throw new InvalidDataException("Sevii Ruby presentation inventory differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount)
            throw new InvalidDataException("Sevii Ruby cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
