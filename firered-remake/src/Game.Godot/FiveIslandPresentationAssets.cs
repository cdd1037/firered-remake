using System.Security.Cryptography;
using System.Text.Json;

internal sealed record FiveIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class FiveIslandPresentationAssets
{
    internal const string Base = "content/generated/five-island-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "d2e48264188c6c273806bc832d13c2bef11081e8d47be1dc8eefc457275fcecf";
    internal const string ManifestSha256 = "839ab8c43dfe6b5ff504dfb74e2ce30fc6fba761e43170970f00ce1756db0106";
    internal const string SynthSha256 = "3a51837796752ade545fa6d27b0b5e62d68b550248eae2e0abeea98bcc6dfe70";
    internal const int LockBytes = 33711;
    internal const int ManifestBytes = 126510;
    internal const int SynthBytes = 4333;
    internal const int SynthCueCount = 6;

    internal static FiveIslandPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Five Island presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Five Island presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Five Island presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("Five Island presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Five Island presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/five-island-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-five-island-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 130 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 9 ||
            manifest.GetProperty("outputs").GetArrayLength() != 87)
            throw new InvalidDataException("Five Island presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/five-island-presentation/" + row.GetProperty("path").GetString(),
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
                throw new InvalidDataException("Five Island asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("Five Island texture differs: " + texture.GetString());
        }
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        int[] owned = [187,188,189], opponentOnly = [228,229,137];
        var expectedSprites = (from species in owned.Concat(opponentOnly) from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (sprites.Length != 24 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            manifest.GetProperty("pokemonIcons").GetArrayLength() != 6 || manifest.GetProperty("footprints").GetArrayLength() != 3 ||
            !manifest.GetProperty("footprints").EnumerateArray().Select(s=>s.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s=>s.GetInt32()).ToHashSet().SetEquals(owned) ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s=>s.GetInt32()).ToHashSet().SetEquals(opponentOnly) ||
            manifest.GetProperty("trainers").GetArrayLength() != 9 || manifest.GetProperty("portraits").GetArrayLength() != 3 ||
            manifest.GetProperty("trainers").EnumerateArray().Count(t=>t.GetProperty("doubleBattle").GetBoolean()) != 0 ||
            manifest.GetProperty("items").GetArrayLength() != 4 || !manifest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("item").GetString()).ToHashSet().SetEquals(
                ["pret:ITEM_BIG_PEARL", "pret:ITEM_UP_GRADE", "pret:ITEM_SAPPHIRE", "pret:ITEM_TM36"]) ||
            manifest.GetProperty("tiles").GetArrayLength() != 2 || !manifest.GetProperty("tiles").EnumerateArray().Select(t => t.GetProperty("metatileId").GetInt32()).ToHashSet().SetEquals([0x2af, 0x2a0]) ||
            manifest.GetProperty("layers").GetArrayLength() != 6 || manifest.GetProperty("mapMusic").GetArrayLength() != 5 ||
            manifest.GetProperty("assets").GetArrayLength() != 46)
            throw new InvalidDataException("Five Island presentation inventory differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount ||
            synth.RootElement.GetProperty("inheritedCues").GetArrayLength() != 5)
            throw new InvalidDataException("Five Island cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
