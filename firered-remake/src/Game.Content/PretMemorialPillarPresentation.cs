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

    public static MemorialPillarPresentationData Load(string root, string? inheritedRoot = null)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") )
                throw new InvalidDataException("Unsafe Memorial Pillar presentation resource.");
            bool local = path.StartsWith(Base, StringComparison.Ordinal) ||
                path.StartsWith(PretSourcePaths.FireRedRawDirectory + "/", StringComparison.Ordinal) || path == "scripts/memorial-pillar-presentation.lock.json";
            var file = new FileInfo(Path.Combine(local ? root : inheritedRoot ?? root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Memorial Pillar resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Memorial Pillar directory symlink rejected.");
            var bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }
        using var locked = JsonDocument.Parse(Read("scripts/memorial-pillar-presentation.lock.json"));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || manifest.GetProperty("schemaVersion").GetInt32() != 1 )
            throw new InvalidDataException("Memorial Pillar presentation identity differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in sourceLock.GetProperty("inheritedInputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
        }
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
        }
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expected = (from species in new[] { 163,164 } from side in new[] { "front","back" }
                        from variant in new[] { "normal","shiny" } select (species, side, variant)).ToArray();
        if (sprites.Length != 8 || !sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).SequenceEqual(expected) ||
            manifest.GetProperty("newOwnedSpecies").GetArrayLength() != 0 ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual(new[] { 163,164 }) ||
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
            var rgba = PngRgba8.Decode(files[row.GetProperty("texturePath").GetString()!]);
            if (rgba.Length != 64 * 64 * 4 )
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
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true };
        var dex = JsonSerializer.Deserialize<MemorialPillarDexDocument>(files[Base + "dex-entries.json"], options)
            ?? throw new InvalidDataException("Memorial Pillar seen-Dex document missing.");
        if (dex.SchemaVersion != 1 ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(new[] { 163,164 }) || dex.Entries.Any(e => e.Portrait is null || e.Footprint is not null))
            throw new InvalidDataException("Memorial Pillar seen-Dex inventory differs.");
        using var synth = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        var audio = manifest.GetProperty("audio");
        if (synth.RootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
            audio.GetProperty("newCueCount").GetInt32() != 2 || !audio.GetProperty("originalPackUnchanged").GetBoolean() ||
            audio.GetProperty("physicalAudioVerified").GetBoolean() || !JsonElement.DeepEquals(audio.GetProperty("cues"), synth.RootElement.GetProperty("cues")) ||
            !audio.GetProperty("cues").EnumerateArray().Select(c => c.GetProperty("species").GetInt32()).SequenceEqual(new[] { 163,164 }))
            throw new InvalidDataException("Memorial Pillar normal cry inventory differs.");
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
