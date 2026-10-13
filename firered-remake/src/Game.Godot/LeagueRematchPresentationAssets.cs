using System.Security.Cryptography;
using System.Text.Json;

internal sealed record LeagueRematchPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Authenticated original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class LeagueRematchPresentationAssets
{
    internal const string Base = "content/generated/league-rematch-presentation/";
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal const string LockSha256 = "73a1d20cd18f68221997f012c6657f8fd2a75a36c34097748afe21057dfcdcc1";
    internal const string ManifestSha256 = "be5ffbab5ae51c6b4f1ac58e86374ad119a694cccfebba2cb05ce5f43ba73170";
    internal const string SynthSha256 = "70a7ed0b3eba74c1291aed615baeddee07ceeaa85380b50d3b2411ca6d02cdcd";
    internal const int LockBytes = 13267;
    internal const int ManifestBytes = 72646;
    internal const int SynthBytes = 4027;
    internal const int SynthCueCount = 5;

    internal static LeagueRematchPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe league rematch presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("League rematch presentation symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("League rematch presentation directory symlink rejected.");
            if (file.Length != size)
                throw new InvalidDataException("League rematch presentation size differs: " + path);
            byte[] data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("League rematch presentation hash differs: " + path);
            return data;
        }

        using var locked = JsonDocument.Parse(Read("scripts/league-rematch-presentation.lock.json", LockSha256, LockBytes));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, ManifestBytes));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        const string profile = "firered-league-rematch-presentation-v1";
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 42 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 15 ||
            manifest.GetProperty("outputs").GetArrayLength() != 41)
            throw new InvalidDataException("League rematch presentation identity differs.");

        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
            Read("content/raw/league-rematch-presentation/" + row.GetProperty("path").GetString(),
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
                throw new InvalidDataException("League rematch asset pixels differ: " + path);
            if (asset.TryGetProperty("texturePath", out var texture) &&
                (!files.TryGetValue(texture.GetString()!, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString()))
                throw new InvalidDataException("League rematch texture differs: " + texture.GetString());
        }
        int[] admitted = [169, 200, 214, 230, 248];
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in admitted from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (sprites.Length != 20 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            manifest.GetProperty("pokemonIcons").GetArrayLength() != 0 || manifest.GetProperty("footprints").GetArrayLength() != 0 ||
            manifest.GetProperty("newOwnedSpecies").GetArrayLength() != 0 ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual(admitted) ||
            manifest.GetProperty("assets").GetArrayLength() != 24 || manifest.GetProperty("speciesNames").GetArrayLength() != 6 ||
            manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").GetArrayLength() != 12)
            throw new InvalidDataException("League rematch presentation inventory differs.");
        var inheritedSprites = manifest.GetProperty("inheritedSprites").EnumerateArray().ToArray();
        var expectedInheritedSprites = (from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (208, side, variant)).ToHashSet();
        if (inheritedSprites.Length != 4 || !inheritedSprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedInheritedSprites))
            throw new InvalidDataException("Inherited Steelix sprite inventory differs.");
        using var inheritedTrainers = JsonDocument.Parse(files[manifest.GetProperty("inheritedTrainerManifest").GetString()!]);
        var trainers = manifest.GetProperty("trainers").EnumerateArray().ToArray();
        if (trainers.Length != 7 || !trainers.Select(t => t.GetProperty("sourceTrainerId").GetInt32()).Order().SequenceEqual(Enumerable.Range(735, 7)))
            throw new InvalidDataException("League rematch trainer inventory differs.");
        foreach (var trainer in trainers)
        {
            var prior = inheritedTrainers.RootElement.GetProperty("trainers").EnumerateArray().Single(t =>
                t.GetProperty("trainer").GetString() == trainer.GetProperty("inheritedTrainer").GetString());
            foreach (string key in new[] { "name", "trainerClass", "className", "trainerPic", "portrait" })
                if (!JsonElement.DeepEquals(prior.GetProperty(key), trainer.GetProperty(key)))
                    throw new InvalidDataException("League rematch inherited trainer presentation differs.");
        }
        var geometry = manifest.GetProperty("moveAnimationGeometry");
        using var inheritedGeometry = JsonDocument.Parse(files[geometry.GetProperty("inheritedManifest").GetString()!]);
        foreach (var row in geometry.GetProperty("rows").EnumerateArray())
            if (!inheritedGeometry.RootElement.GetProperty("geometry").EnumerateArray().Any(g => JsonElement.DeepEquals(g,row)))
                throw new InvalidDataException("League rematch move-animation geometry differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        if (files[Base + "synth-manifest.json"].Length != SynthBytes ||
            Convert.ToHexStringLower(SHA256.HashData(files[Base + "synth-manifest.json"])) != SynthSha256 ||
            synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            synth.RootElement.GetProperty("cues").GetArrayLength() != SynthCueCount ||
            synth.RootElement.GetProperty("inheritedCues").GetArrayLength() != 5)
            throw new InvalidDataException("League rematch cue metadata differs.");
        return new(manifest.Clone(), files);
    }
}
