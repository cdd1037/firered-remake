using System.Security.Cryptography;
using System.Text.Json;

internal sealed record SixIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class SixIslandPresentationAssets
{
    internal const string Base = "content/generated/six-island-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "a5c54b34b0be5c2144c9ae436137596ac7e0bb2426f9647ecba32b86622f4c35";
    internal const string ManifestSha256 = "fa0d64e6517314783ff82f5d2e381ca599dfed9158ea8cedf4e8bd72091b3859";
    internal const string SynthSha256 = "c49c4c4a0fceb502275bab2414559715da6ed425f1b3bb323922385e6bb838f9";
    internal const int LockBytes = 51701;
    internal const int ManifestBytes = 223743;
    internal const int SynthBytes = 6615;
    internal const int SynthCueCount = 15;

    internal static SixIslandPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Six Island presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Six Island presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Six Island presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("Six Island presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Six Island presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/six-island-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-six-island-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 200 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 17 ||
            manifest.GetProperty("outputs").GetArrayLength() != 155)
            throw new InvalidDataException("Six Island presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/six-island-presentation/" + row.GetProperty("path").GetString(),
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
                throw new InvalidDataException("Six Island asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("Six Island texture differs: " + texture.GetString());
        }
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        int[] owned = [161,162,177,178,193,202,182], opponentOnly = [191,192,170,171,185];
        var expectedSprites = (from species in owned.Concat(opponentOnly) from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (sprites.Length != 48 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            manifest.GetProperty("pokemonIcons").GetArrayLength() != 12 || manifest.GetProperty("footprints").GetArrayLength() != 7 ||
            !manifest.GetProperty("footprints").EnumerateArray().Select(s=>s.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s=>s.GetInt32()).ToHashSet().SetEquals(owned) ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s=>s.GetInt32()).ToHashSet().SetEquals(opponentOnly) ||
            manifest.GetProperty("trainers").GetArrayLength() != 11 || manifest.GetProperty("portraits").GetArrayLength() != 8 ||
            manifest.GetProperty("trainers").EnumerateArray().Count(t=>t.GetProperty("doubleBattle").GetBoolean()) != 1 ||
            manifest.GetProperty("items").GetArrayLength() != 3 || !manifest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("item").GetString()).ToHashSet().SetEquals(
                ["pret:ITEM_SUN_STONE", "pret:ITEM_DRAGON_SCALE", "pret:ITEM_DREAM_MAIL"]) ||
            manifest.GetProperty("tiles").GetArrayLength() != 2 || !manifest.GetProperty("tiles").EnumerateArray().Select(t => t.GetProperty("metatileId").GetInt32()).ToHashSet().SetEquals([0x357, 0x358]) ||
            manifest.GetProperty("layers").GetArrayLength() != 6 || manifest.GetProperty("terrains").GetArrayLength() != 3 ||
            manifest.GetProperty("braille").GetProperty("messages").GetArrayLength() != 21 ||
            manifest.GetProperty("assets").GetArrayLength() != 90)
            throw new InvalidDataException("Six Island presentation inventory differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount ||
            synth.RootElement.GetProperty("inheritedCues").GetArrayLength() != 2)
            throw new InvalidDataException("Six Island cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
