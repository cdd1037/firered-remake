using System.Security.Cryptography;
using System.Text.Json;

internal sealed record CelioPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class CelioPresentationAssets
{
    internal const string Base = "content/generated/celio-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "a8830aadada3dd30be755be3ac982ec53911bcd4396737fdb268204ef7dade2d";
    internal const string ManifestSha256 = "d7ae9d6c4fe63c768da9207d5cb13606722b3a22f68ff3293f83b086ebe8d52d";
    internal const string SynthSha256 = "29a84e6ae649bf31f976a6fa1ddf6d374105fadd7382ab9af50b6410c13a3588";
    internal const int LockBytes = 17193;
    internal const int ManifestBytes = 73313;
    internal const int SynthBytes = 2757;
    internal const int SynthCueCount = 3;

    internal static CelioPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Celio presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Celio presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Celio presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("Celio presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Celio presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/celio-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-celio-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 66 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 5 ||
            manifest.GetProperty("outputs").GetArrayLength() != 55)
            throw new InvalidDataException("Celio presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/celio-presentation/" + row.GetProperty("path").GetString(),
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
                throw new InvalidDataException("Celio asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("Celio texture differs: " + texture.GetString());
        }
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        int[] owned = [243,244,245];
        var expectedSprites = (from species in owned from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (sprites.Length != 12 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            manifest.GetProperty("pokemonIcons").GetArrayLength() != 3 || manifest.GetProperty("footprints").GetArrayLength() != 3 ||
            !manifest.GetProperty("footprints").EnumerateArray().Select(s => s.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s => s.GetInt32()).ToHashSet().SetEquals(owned) ||
            manifest.GetProperty("newOpponentOnlySpecies").GetArrayLength() != 0 ||
            manifest.GetProperty("tiles").GetArrayLength() != 3 || !manifest.GetProperty("tiles").EnumerateArray().Select(t => t.GetProperty("metatileId").GetInt32()).ToHashSet().SetEquals([858,859,863]) ||
            manifest.GetProperty("layers").GetArrayLength() != 9 || manifest.GetProperty("assets").GetArrayLength() != 27 ||
            manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").GetArrayLength() != 6)
            throw new InvalidDataException("Celio presentation inventory differs.");
        var geometry = manifest.GetProperty("moveAnimationGeometry");
        using var inheritedGeometry = JsonDocument.Parse(files[geometry.GetProperty("inheritedManifest").GetString()!]);
        foreach (var row in geometry.GetProperty("rows").EnumerateArray())
            if (!inheritedGeometry.RootElement.GetProperty("geometry").EnumerateArray().Any(g => JsonElement.DeepEquals(g,row)))
                throw new InvalidDataException("Celio move-animation geometry differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount ||
            synth.RootElement.GetProperty("inheritedCues").GetArrayLength() != 4)
            throw new InvalidDataException("Celio cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
