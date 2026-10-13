using System.Security.Cryptography;
using System.Text.Json;

internal sealed record FourIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class FourIslandPresentationAssets
{
    internal const string Base = "content/generated/four-island-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "d62ac37ea20b737311bcc5d993912f2cbb4e9f284199ca8f2d2aaa18eb21f664";
    internal const string ManifestSha256 = "3ff9b3b7532710a88d5a2b3da785a61fcb018c1b9bff7b354dd6936b91619d7a";
    internal const string SynthSha256 = "04b7c9a2af54636bc76da9806c862fe0026fc975abd6454947ab5b3defb42aa5";
    internal const int LockBytes = 29566;
    internal const int ManifestBytes = 128985;
    internal const int SynthBytes = 3204;
    internal const int SynthCueCount = 7;

    internal static FourIslandPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Four Island presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Four Island presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Four Island presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("Four Island presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Four Island presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/four-island-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-four-island-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 114 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 9 ||
            manifest.GetProperty("outputs").GetArrayLength() != 105)
            throw new InvalidDataException("Four Island presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/four-island-presentation/" + row.GetProperty("path").GetString(),
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
                throw new InvalidDataException("Four Island asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("Four Island texture differs: " + texture.GetString());
        }
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in new[] { 86, 87, 194, 195, 220, 221, 225 } from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (sprites.Length != 28 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            manifest.GetProperty("pokemonIcons").GetArrayLength() != 7 || manifest.GetProperty("footprints").GetArrayLength() != 7 ||
            manifest.GetProperty("trainers").GetArrayLength() != 1 || manifest.GetProperty("portraits").GetArrayLength() != 1 ||
            manifest.GetProperty("trainers")[0].GetProperty("trainer").GetString() != "pret:TRAINER_TEAM_ROCKET_GRUNT_45" ||
            manifest.GetProperty("items").GetArrayLength() != 2 || !manifest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("item").GetString()).ToHashSet().SetEquals(
                ["pret:ITEM_HM07", "pret:ITEM_NEVER_MELT_ICE"]) ||
            manifest.GetProperty("tiles").GetArrayLength() != 2 || !manifest.GetProperty("tiles").EnumerateArray().Select(t => t.GetProperty("metatileId").GetInt32()).ToHashSet().SetEquals([0x35A, 0x35B]) ||
            manifest.GetProperty("layers").GetArrayLength() != 6 || manifest.GetProperty("terrains").GetArrayLength() != 2 ||
            manifest.GetProperty("assets").GetArrayLength() != 53)
            throw new InvalidDataException("Four Island presentation inventory differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount)
            throw new InvalidDataException("Four Island cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
