using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Game.Content;

public sealed record MemorialPillarDexDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PokedexMenuEntry> Entries);
public sealed record MemorialPillarPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Original opponent-only Hoothoot/Noctowl pixels, seen-Dex portraits, geometry and normal cry bindings.</summary>
public static class PretMemorialPillarPresentation
{
    public const string Base = "content/generated/memorial-pillar-presentation/";
    public const string Profile = "firered-memorial-pillar-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "3c9cfc4491f61419743b9f8fed45befae27027081232a5f21fe8fa2dd4f6256b";
    public const string ManifestSha256 = "cf47a71062dce135198cc53f6cfa92b6354e0f08745b1d3308f88babdbe4becd";

    public static MemorialPillarPresentationData Load(string root, string? inheritedRoot = null)
    {
        byte[] Read(string path, string hash, int length)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || length is <= 0 or > 4_000_000 ||
                hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException("Unsafe Memorial Pillar presentation resource.");
            bool local = path.StartsWith(Base, StringComparison.Ordinal) ||
                path.StartsWith("content/raw/memorial-pillar-presentation/", StringComparison.Ordinal) || path == "scripts/memorial-pillar-presentation.lock.json";
            var file = new FileInfo(Path.Combine(local ? root : inheritedRoot ?? root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Memorial Pillar resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Memorial Pillar directory symlink rejected.");
            if (file.Length != length) throw new InvalidDataException("Memorial Pillar presentation size differs: " + path);
            var bytes = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("Memorial Pillar presentation bytes differ: " + path);
            return bytes;
        }
        using var locked = JsonDocument.Parse(Read("scripts/memorial-pillar-presentation.lock.json", LockSha256, 6127));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, 16615));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != Profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != Profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 19 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 3 ||
            manifest.GetProperty("outputs").GetArrayLength() != 18)
            throw new InvalidDataException("Memorial Pillar presentation identity differs.");
        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
        {
            var bytes = Read(row.GetProperty("rawPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) !=
                row.GetProperty("gitBlobSha1").GetString()) throw new InvalidDataException("Memorial Pillar source Git blob differs.");
        }
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
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expected = (from species in new[] { 163, 164 } from side in new[] { "front", "back" }
                        from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToArray();
        if (sprites.Length != 8 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).SequenceEqual(expected) ||
            manifest.GetProperty("newOwnedSpecies").GetArrayLength() != 0 ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual(new[] { 163, 164 }) ||
            manifest.GetProperty("pokemonIcons").GetArrayLength() != 0 || manifest.GetProperty("footprints").GetArrayLength() != 0)
            throw new InvalidDataException("Memorial Pillar opponent-only inventory differs.");
        foreach (var row in sprites)
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string side = row.GetProperty("side").GetString()!;
            (int Offset, int Width, int Height) geometry = (species, side) switch
            {
                (163, "front") => (13, 40, 40), (163, "back") => (8, 48, 48),
                (164, "front") => (3, 40, 64), (164, "back") => (3, 48, 64),
                _ => throw new InvalidDataException("Unexpected Memorial Pillar battler.")
            };
            if (row.GetProperty("speciesId").GetString() != "pret:SPECIES_" + (species == 163 ? "HOOTHOOT" : "NOCTOWL") ||
                row.GetProperty("width").GetInt32() != 64 || row.GetProperty("height").GetInt32() != 64 ||
                row.GetProperty("sourceWidth").GetInt32() != geometry.Width || row.GetProperty("sourceHeight").GetInt32() != geometry.Height ||
                row.GetProperty("sourceYOffset").GetInt32() != geometry.Offset || row.GetProperty("sourceEnemyElevation").GetInt32() != 0 ||
                row.GetProperty("x").GetInt32() != (side == "front" ? 144 : 40) ||
                row.GetProperty("y").GetInt32() != (side == "front" ? 8 : 48) + geometry.Offset)
                throw new InvalidDataException("Memorial Pillar battler geometry differs.");
            var rgba = files[row.GetProperty("rgbaPath").GetString()!];
            var png = files[row.GetProperty("texturePath").GetString()!];
            if (rgba.Length != 64 * 64 * 4 || Convert.ToHexStringLower(SHA256.HashData(rgba)) != row.GetProperty("rgbaSha256").GetString() ||
                Convert.ToHexStringLower(SHA256.HashData(png)) != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("Memorial Pillar battler pixels differ.");
        }
        var moves = manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray().ToArray();
        if (moves.Length != 4) throw new InvalidDataException("Memorial Pillar move geometry inventory differs.");
        foreach (var row in moves)
        {
            var sprite = sprites.Single(s => s.GetProperty("sourceSpeciesId").GetInt32() == row.GetProperty("species").GetInt32() &&
                s.GetProperty("side").GetString() == row.GetProperty("side").GetString() && s.GetProperty("paletteVariant").GetString() == "normal");
            if (row.GetProperty("yOffset").GetInt32() != sprite.GetProperty("sourceYOffset").GetInt32() || row.GetProperty("elevation").GetInt32() != 0 ||
                row.GetProperty("sourceWidth").GetInt32() != sprite.GetProperty("sourceWidth").GetInt32() ||
                row.GetProperty("sourceHeight").GetInt32() != sprite.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Memorial Pillar move geometry differs.");
        }
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true };
        var dex = JsonSerializer.Deserialize<MemorialPillarDexDocument>(files[Base + "dex-entries.json"], options)
            ?? throw new InvalidDataException("Memorial Pillar seen-Dex document missing.");
        if (dex.SchemaVersion != 1 || dex.Profile != Profile || dex.SourceCommit != SourceCommit ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(new[] { 163, 164 }) || dex.Entries.Any(e => e.Portrait is null || e.Footprint is not null))
            throw new InvalidDataException("Memorial Pillar seen-Dex inventory differs.");
        foreach (var entry in dex.Entries)
        {
            var row = sprites.Single(s => s.GetProperty("sourceSpeciesId").GetInt32() == entry.SourceSpeciesId &&
                s.GetProperty("side").GetString() == "front" && s.GetProperty("paletteVariant").GetString() == "normal");
            if (entry.Portrait != new PokedexMenuTexture(row.GetProperty("texturePath").GetString()!, row.GetProperty("sha256").GetString()!,
                row.GetProperty("rgbaSha256").GetString()!, 64, 64)) throw new InvalidDataException("Memorial Pillar Dex portrait differs.");
        }
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        var audio = manifest.GetProperty("audio");
        if (synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || synth.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            audio.GetProperty("newCueCount").GetInt32() != 2 || !audio.GetProperty("originalPackUnchanged").GetBoolean() ||
            audio.GetProperty("physicalAudioVerified").GetBoolean() || !JsonElement.DeepEquals(audio.GetProperty("cues"), synth.RootElement.GetProperty("cues")) ||
            !audio.GetProperty("cues").EnumerateArray().Select(c => c.GetProperty("species").GetInt32()).SequenceEqual(new[] { 163, 164 }))
            throw new InvalidDataException("Memorial Pillar normal cry inventory differs.");
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
