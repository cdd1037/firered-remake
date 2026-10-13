using System.Security.Cryptography;
using System.Text.Json;

internal sealed record EventIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class EventIslandPresentationAssets
{
    internal const string Base = "content/generated/event-island-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "24f8691fae4700137fa0dbcfc6be277a8a68181cf88154a6ffb462d2b0d61b46";
    internal const string ManifestSha256 = "f71013483e4dfe651458570d13f16b457a2ea59856eea1d40324234d9a921843";
    internal const string SynthSha256 = "fdbb69c5d1db92476303fc9fb84c022a3fe45ca0e49c35913fc7e8d92fe77d0e";
    internal const int LockBytes = 23581;
    internal const int ManifestBytes = 100444;
    internal const int SynthBytes = 6981;
    internal const int SynthCueCount = 13;

    internal static EventIslandPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Event Island presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Event Island presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Event Island presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("Event Island presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Event Island presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/event-island-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-event-island-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 92 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 6 ||
            manifest.GetProperty("outputs").GetArrayLength() != 67)
            throw new InvalidDataException("Event Island presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/event-island-presentation/" + row.GetProperty("path").GetString(),
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
                throw new InvalidDataException("Event Island asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("Event Island texture differs: " + texture.GetString());
        }
        int[] owned = [249,250,410];
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in owned from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species,side,variant)).ToHashSet();
        if (sprites.Length != 12 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!,s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s=>s.GetInt32()).SequenceEqual(owned) ||
            sprites.Any(s=>s.GetProperty("renderSpeciesId").GetInt32()!=s.GetProperty("sourceSpeciesId").GetInt32() ||
                s.GetProperty("sourceFrameIndex").GetInt32()!=(s.GetProperty("sourceSpeciesId").GetInt32()==410?1:0)) ||
            manifest.GetProperty("assets").GetArrayLength()!=33 || manifest.GetProperty("speciesNames").GetArrayLength()!=3 ||
            manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").GetArrayLength()!=6)
            throw new InvalidDataException("Event Island source sprite inventory differs.");
        foreach(string property in new[]{"pokemonIcons","footprints"})
            if(manifest.GetProperty(property).GetArrayLength()!=3 || !manifest.GetProperty(property).EnumerateArray()
                .Select(r=>r.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned))
                throw new InvalidDataException("Event Island owned icon/footprint inventory differs.");
        var triangle=manifest.GetProperty("triangle").EnumerateArray().ToArray();
        if(triangle.Length!=11 || !triangle.Select(r=>r.GetProperty("interactionNumber").GetInt32()).SequenceEqual(Enumerable.Range(0,11)) ||
            triangle.Any(r=>r.GetProperty("objectPaletteSlot").GetInt32()!=10 || r.GetProperty("width").GetInt32()!=32 || r.GetProperty("height").GetInt32()!=32) ||
            manifest.GetProperty("fragments").GetArrayLength()!=4)
            throw new InvalidDataException("Event Island triangle effect inventory differs.");
        var geometry = manifest.GetProperty("moveAnimationGeometry");
        using var inheritedGeometry = JsonDocument.Parse(files[geometry.GetProperty("inheritedManifest").GetString()!]);
        foreach (var row in geometry.GetProperty("rows").EnumerateArray())
            if (!inheritedGeometry.RootElement.GetProperty("geometry").EnumerateArray().Any(g => JsonElement.DeepEquals(g,row)))
                throw new InvalidDataException("Event Island move-animation geometry differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount ||
            synth.RootElement.GetProperty("inheritedCues").GetArrayLength() != 5)
            throw new InvalidDataException("Event Island cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
