using System.Security.Cryptography;
using System.Text.Json;

internal sealed record PowerPlantPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class PowerPlantPresentationAssets
{
    internal const string Base = "content/generated/power-plant-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "3c0e20b5527adcf81bef68f59ffd5e1a167d09314ebb30c4b55792422517b7ff";
    internal const string ManifestSha256 = "60981c2d56a7104cbae659daa6110787e7f31005f76fb3dcf17fdefc442cc001";
    internal const string SynthSha256 = "dfc0091de6f44432f3bfeba4afa86048a3201732b5ca00d1f34000048a7669bd";
    internal const int LockBytes = 14681;
    internal const int ManifestBytes = 69105;
    internal const int SynthBytes = 4003;
    internal const int SynthCueCount = 4;

    internal static PowerPlantPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Power Plant presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Power Plant presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Power Plant presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("Power Plant presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Power Plant presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/power-plant-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-power-plant-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 53 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 9 ||
            manifest.GetProperty("outputs").GetArrayLength() != 39)
            throw new InvalidDataException("Power Plant presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/power-plant-presentation/" + row.GetProperty("path").GetString(),
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
                throw new InvalidDataException("Power Plant asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("Power Plant texture differs: " + texture.GetString());
        }
        int[] owned = [125,135,145];
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in owned from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species,side,variant)).ToHashSet();
        if (sprites.Length != 12 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!,s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s=>s.GetInt32()).SequenceEqual(owned) ||
            sprites.Any(s=>s.GetProperty("renderSpeciesId").GetInt32()!=s.GetProperty("sourceSpeciesId").GetInt32() ||
                s.GetProperty("sourceFrameIndex").GetInt32()!=0) ||
            manifest.GetProperty("assets").GetArrayLength()!=19 || manifest.GetProperty("speciesNames").GetArrayLength()!=3 ||
            manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").GetArrayLength()!=6)
            throw new InvalidDataException("Power Plant source sprite inventory differs.");
        foreach(string property in new[]{"pokemonIcons","footprints"})
            if(manifest.GetProperty(property).GetArrayLength()!=3 || !manifest.GetProperty(property).EnumerateArray()
                .Select(r=>r.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned))
                throw new InvalidDataException("Power Plant owned icon/footprint inventory differs.");
        var geometry = manifest.GetProperty("moveAnimationGeometry");
        using var inheritedGeometry = JsonDocument.Parse(files[geometry.GetProperty("inheritedManifest").GetString()!]);
        foreach (var row in geometry.GetProperty("rows").EnumerateArray())
            if (!inheritedGeometry.RootElement.GetProperty("geometry").EnumerateArray().Any(g => JsonElement.DeepEquals(g,row)))
                throw new InvalidDataException("Power Plant move-animation geometry differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount ||
            synth.RootElement.GetProperty("inheritedCues").GetArrayLength() != 6)
            throw new InvalidDataException("Power Plant cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
