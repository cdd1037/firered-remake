using System.Security.Cryptography;
using System.Text.Json;

internal sealed record SevenIslandPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class SevenIslandPresentationAssets
{
    internal const string Base = "content/generated/seven-island-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "fb19e75a93d4e2d4358103e30507bfc3a4b99668fb503c1c4779bd8bf2b7ffdb";
    internal const string ManifestSha256 = "a4b469cdfdd57bb7062767cf9e78c487ee41dc384b194069ee6e5436f3f904b5";
    internal const string SynthSha256 = "b4540c690a17791ffd4579fb484548c685f8df14e385fd18f3c9c269a62637f0";
    internal const int LockBytes = 70631;
    internal const int ManifestBytes = 534613;
    internal const int SynthBytes = 8210;
    internal const int SynthCueCount = 15;

    internal static SevenIslandPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Seven Island presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Seven Island presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Seven Island presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("Seven Island presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Seven Island presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/seven-island-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-seven-island-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 268 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 26 ||
            manifest.GetProperty("outputs").GetArrayLength() != 403)
            throw new InvalidDataException("Seven Island presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/seven-island-presentation/" + row.GetProperty("path").GetString(),
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
                throw new InvalidDataException("Seven Island asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("Seven Island texture differs: " + texture.GetString());
        }
        int[] owned = [231,232,246,247,248,227,201];
        int[] opponents = [203,237,241,204,235,217];
        int[] rendered = [231,232,246,247,227,201,203,237,241,204,235,217, ..Enumerable.Range(413,27)];
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in rendered from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species,side,variant)).ToHashSet();
        if (sprites.Length != 156 || !sprites.Select(s => (s.GetProperty("renderSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!,s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            sprites.Any(s => s.GetProperty("sourceSpeciesId").GetInt32() !=
                (s.GetProperty("renderSpeciesId").GetInt32() >= 413 ? 201 : s.GetProperty("renderSpeciesId").GetInt32())) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual(owned) ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual(opponents) ||
            manifest.GetProperty("assets").GetArrayLength() != 216 || manifest.GetProperty("speciesNames").GetArrayLength() != 13 ||
            manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").GetArrayLength() != 80)
            throw new InvalidDataException("Seven Island canonical/form sprite inventory differs.");
        var icons = manifest.GetProperty("pokemonIcons").EnumerateArray().ToArray();
        if (icons.Length != 34 || !icons.Select(i => i.GetProperty("renderSpeciesId").GetInt32()).ToHashSet()
                .SetEquals(owned.Concat(Enumerable.Range(413,27))) ||
            icons.Any(i => i.GetProperty("sourceFrameIndex").GetInt32()!=0 || i.GetProperty("sourceFrameCount").GetInt32()!=2) ||
            manifest.GetProperty("footprints").GetArrayLength()!=7 ||
            !manifest.GetProperty("footprints").EnumerateArray().Select(s=>s.GetProperty("sourceSpeciesId").GetInt32()).ToHashSet().SetEquals(owned))
            throw new InvalidDataException("Seven Island owned icon/footprint inventory differs.");
        var inheritedSprites = manifest.GetProperty("inheritedSprites").EnumerateArray().ToArray();
        if (inheritedSprites.Length!=4 || !inheritedSprites.Select(s=>(s.GetProperty("renderSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!,s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(
                from side in new[]{"front","back"} from variant in new[]{"normal","shiny"} select (248,side,variant)))
            throw new InvalidDataException("Inherited Tyranitar sprites differ.");
        var trainers=manifest.GetProperty("trainers").EnumerateArray().ToArray();
        var portraits=manifest.GetProperty("portraits").EnumerateArray().ToArray();
        if (trainers.Length!=18 || trainers.Select(t=>t.GetProperty("trainer").GetString()).Distinct().Count()!=18 ||
            trainers.Count(t=>t.GetProperty("doubleBattle").GetBoolean())!=2 || portraits.Length!=15 ||
            portraits.Count(p=>!p.GetProperty("inherited").GetBoolean())!=4 ||
            trainers.Any(t=>!portraits.Any(p=>p.GetProperty("id").GetString()==t.GetProperty("portrait").GetString())))
            throw new InvalidDataException("Seven Island trainer inventory differs.");
        var geometry = manifest.GetProperty("moveAnimationGeometry");
        using var inheritedGeometry = JsonDocument.Parse(files[geometry.GetProperty("inheritedManifest").GetString()!]);
        foreach (var row in geometry.GetProperty("rows").EnumerateArray())
            if (!inheritedGeometry.RootElement.GetProperty("geometry").EnumerateArray().Any(g => JsonElement.DeepEquals(g,row)))
                throw new InvalidDataException("Seven Island move-animation geometry differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount ||
            synth.RootElement.GetProperty("inheritedCues").GetArrayLength() != 6)
            throw new InvalidDataException("Seven Island cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
