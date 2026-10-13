using System.Security.Cryptography;
using System.Text.Json;

internal sealed record MewtwoPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class MewtwoPresentationAssets
{
    internal const string Base = "content/generated/mewtwo-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "f28873123459b1d1de87e871a2ebdcd0648a8a5cdc71bb338936948b3f557a38";
    internal const string ManifestSha256 = "44f0534e2bf81d2aef828968a7914eb9361b3d14128da853c2514eb099ad05f6";
    internal const string SynthSha256 = "49387f26a75cc7997b62a1bee2b5ce4fb4cb737b8910f452da94089fb973c7d0";
    internal const int LockBytes = 6963;
    internal const int ManifestBytes = 20383;
    internal const int SynthBytes = 1680;
    internal const int SynthCueCount = 3;

    internal static MewtwoPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Mewtwo presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Mewtwo presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Mewtwo presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("Mewtwo presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Mewtwo presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/mewtwo-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-mewtwo-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 25 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 4 ||
            manifest.GetProperty("outputs").GetArrayLength() != 13)
            throw new InvalidDataException("Mewtwo presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/mewtwo-presentation/" + row.GetProperty("path").GetString(),
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
                throw new InvalidDataException("Mewtwo asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("Mewtwo texture differs: " + texture.GetString());
        }
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (150, side, variant)).ToHashSet();
        if (sprites.Length != 4 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            manifest.GetProperty("pokemonIcons").GetArrayLength() != 1 || manifest.GetProperty("footprints").GetArrayLength() != 1 ||
            manifest.GetProperty("pokemonIcons")[0].GetProperty("sourceSpeciesId").GetInt32() != 150 ||
            manifest.GetProperty("footprints")[0].GetProperty("sourceSpeciesId").GetInt32() != 150 ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual([150]) ||
            manifest.GetProperty("assets").GetArrayLength() != 6 ||
            manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").GetArrayLength() != 2)
            throw new InvalidDataException("Mewtwo presentation inventory differs.");
        var geometry = manifest.GetProperty("moveAnimationGeometry");
        using var inheritedGeometry = JsonDocument.Parse(files[geometry.GetProperty("inheritedManifest").GetString()!]);
        foreach (var row in geometry.GetProperty("rows").EnumerateArray())
            if (!inheritedGeometry.RootElement.GetProperty("geometry").EnumerateArray().Any(g => JsonElement.DeepEquals(g,row)))
                throw new InvalidDataException("Mewtwo move-animation geometry differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount ||
            synth.RootElement.GetProperty("inheritedCues").GetArrayLength() != 1)
            throw new InvalidDataException("Mewtwo cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
