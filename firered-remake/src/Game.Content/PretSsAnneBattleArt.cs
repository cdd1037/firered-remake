using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record SsAnneArtFrame(int SourceTileOffset, int X, int Y, int Width, int Height, int Duration);
public sealed record SsAnneDepartureSheet(string Id, int Width, int Height, int PaletteSlot, bool Loops,
    ImmutableArray<SsAnneArtFrame> Frames);
public sealed record SsAnneDepartureCadence(int InitialWaitTicks, int FinishWaitTicks, int BoatMoveTickDivisor,
    int SmokePeriodTicks, int BoatExitXExclusive, int WakeBoatOffsetX, int WakeCenterY,
    int WakeDriftTickDivisor, int WakeMaximumDrift, int WakeDestroyXExclusive, int WakePriority,
    int WakeSubpriority, int SmokeBoatOffsetX, int SmokeCenterY, int SmokeMinimumSpawnX,
    int SmokeDriftTickDivisor, int SmokeSubpriority, string SoundId);

/// <summary>Original four trainer fronts and departure sheets. Loading does not execute the departure task.</summary>
public sealed class PretSsAnneBattleArt
{
    public const string Profile = "firered-ssanne-battle-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "48c8060d8f4af257caeaf4880c50da6712a3235c6f7fd76d31dc139955b0adf8";
    public const string GeneratedSha256 = "342f7a64dd616c390e82740f1f7e23a455cc095c19f45bf87aa3a8f7d075ebda";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public ImmutableArray<ForestTrainerPortrait> Portraits { get; }
    public SsAnneDepartureSheet Wake { get; }
    public SsAnneDepartureSheet Smoke { get; }
    public SsAnneDepartureCadence Departure { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }

    private PretSsAnneBattleArt(ImmutableArray<ForestTrainerPortrait> portraits, SsAnneDepartureSheet wake,
        SsAnneDepartureSheet smoke, SsAnneDepartureCadence departure,
        ImmutableArray<PretWorldSourceFile> files, ImmutableDictionary<string, byte[]> outputs)
    {
        Portraits = portraits; Wake = wake; Smoke = smoke; Departure = departure; SourceFiles = files; _outputs = outputs;
        AdditionalSourceHashes = ["ssanne-battle-art-manifest:" + GeneratedSha256, "ssanne-battle-art-lock:" + LockSha256,
            .. files.Select(f => "ssanne-battle-art:" + f.Path + ":" + f.Sha256)];
    }

    /// <summary>Authenticated palette-expanded PNG bytes; callers may load them without another path lookup.</summary>
    public byte[] ReadPng(string id) => ReadAsset(id, ".png");
    public byte[] ReadRgba(string id) => ReadAsset(id, ".rgba");
    private byte[] ReadAsset(string id, string extension) => _outputs.TryGetValue(id + extension, out var bytes)
        ? (byte[])bytes.Clone() : throw new InvalidDataException("Unknown S.S. Anne art asset: " + id);

    public static PretSsAnneBattleArt Load(string root)
    {
        var lockBytes = Read(root, "scripts/ssanne-battle-art.lock.json", LockSha256, 5576);
        using var sourceLock = JsonDocument.Parse(lockBytes);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/ssanne-battle-art/manifest.json", GeneratedSha256, 11760));
        var document = manifest.RootElement;
        var locked = sourceLock.RootElement;
        Require(locked.GetProperty("repository").GetString() == "pret/pokefirered" &&
            locked.GetProperty("commit").GetString() == SourceCommit && locked.GetProperty("tree").GetString() == SourceTree &&
            locked.GetProperty("gameVersion").GetString() == "FIRERED" && locked.GetProperty("revision").GetInt32() == 0,
            "S.S. Anne art source identity differs.");
        Require(document.GetProperty("schemaVersion").GetInt32() == 1 && document.GetProperty("profile").GetString() == Profile &&
            document.GetProperty("sourceCommit").GetString() == SourceCommit && document.GetProperty("sourceTree").GetString() == SourceTree &&
            document.GetProperty("lockSha256").GetString() == LockSha256 &&
            document.GetProperty("paletteExpansion").GetString() == "(rgb5 << 3) | (rgb5 >> 2)" &&
            document.GetProperty("inputs").GetArrayLength() == 22 && document.GetProperty("assets").GetArrayLength() == 6 &&
            document.GetProperty("outputs").GetArrayLength() == 12 &&
            JsonElement.DeepEquals(document.GetProperty("inputs"), locked.GetProperty("files")), "S.S. Anne art manifest differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var input in document.GetProperty("inputs").EnumerateArray())
        {
            string path = input.GetProperty("path").GetString()!, hash = input.GetProperty("sha256").GetString()!;
            int length = input.GetProperty("byteLength").GetInt32();
            string blob = input.GetProperty("gitBlobSha1").GetString()!;
            var bytes = Read(root, "content/raw/ssanne-battle-art/" + path, hash, length);
            using var gitHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            gitHash.AppendData(Encoding.ASCII.GetBytes($"blob {bytes.Length}\0")); gitHash.AppendData(bytes);
            Require(Convert.ToHexStringLower(gitHash.GetHashAndReset()) == blob, "S.S. Anne source Git blob differs.");
            files.Add(new(path, blob, length, hash, $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{path}"));
        }
        Require(files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == 22, "Duplicate S.S. Anne art input.");
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in document.GetProperty("outputs").EnumerateArray())
        {
            string path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(root, "content/generated/ssanne-battle-art/" + path,
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32())), "Duplicate S.S. Anne art output.");
        }
        var portraits = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        var sheets = new Dictionary<string, SsAnneDepartureSheet>(StringComparer.Ordinal);
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            Require(outputs[id + ".rgba"].Length == width * height * 4, "S.S. Anne RGBA geometry differs.");
            byte[] png = outputs[id + ".png"];
            Require(png.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) &&
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)) == width &&
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)) == height, "S.S. Anne PNG geometry differs.");
            if (asset.GetProperty("kind").GetString() == "trainer")
            {
                var expected = id switch
                {
                    "sailor-front" => (Stem: "sailor", Id: 85), "fisherman-front" => (Stem: "fisherman", Id: 94),
                    "gentleman-front" => (Stem: "gentleman", Id: 123), "rival-late-front" => (Stem: "rival_late", Id: 124),
                    _ => throw new InvalidDataException("Unknown S.S. Anne source portrait.")
                };
                Require(width == 64 && height == 64 && asset.GetProperty("sourceTrainerPicId").GetInt32() == expected.Id &&
                    asset.GetProperty("x").GetInt32() == 144 && asset.GetProperty("y").GetInt32() == 8 &&
                    asset.GetProperty("sourceFrame").GetInt32() == 0 &&
                    asset.GetProperty("sourceImage").GetString() == "graphics/trainers/front_pics/" + expected.Stem + "_front_pic.png" &&
                    asset.GetProperty("sourcePalette").GetString() == "graphics/trainers/palettes/" + expected.Stem + ".pal",
                    "S.S. Anne trainer source geometry differs.");
                portraits.Add(new(id, 144, 8, (byte[])outputs[id + ".rgba"].Clone()));
            }
            else
            {
                var sheet = asset.Deserialize<SsAnneDepartureSheet>(JsonOptions) ?? throw new InvalidDataException("Missing ship sheet.");
                Require(sheet.Id is "wake" or "smoke" && sheet.Width == 16 && sheet.PaletteSlot == 10 &&
                    sheet.Height == (id == "wake" ? 64 : 72) && sheet.Loops == (id == "wake") &&
                    asset.GetProperty("sourceImage").GetString() == "graphics/ss_anne/" + id + ".png" &&
                    asset.GetProperty("sourcePalette").GetString() == "graphics/object_events/palettes/ss_anne.pal",
                    "Ship sheet original palette/geometry differs.");
                SsAnneArtFrame[] expected = id == "wake" ? [new(0,0,0,16,32,12),new(8,0,32,16,32,12)] :
                    [new(0,0,0,16,16,10),new(4,0,16,16,16,20),new(8,0,32,16,16,20),new(12,0,48,16,16,30)];
                Require(sheet.Frames.SequenceEqual(expected) && sheets.TryAdd(id, sheet), "Ship source animation differs.");
            }
        }
        Require(portraits.Count == 4 && sheets.Count == 2 && portraits.Select(p => p.Id).Distinct().Count() == 4,
            "S.S. Anne art inventory differs.");
        var departure = document.GetProperty("departure").Deserialize<SsAnneDepartureCadence>(JsonOptions) ??
            throw new InvalidDataException("Missing ship cadence.");
        Require(departure == new SsAnneDepartureCadence(50,40,5,70,-120,80,109,6,22,-18,2,255,49,78,-32,4,8,"SE_SS_ANNE_HORN"),
            "Original ship task cadence differs.");
        return new(portraits.ToImmutable(), sheets["wake"], sheets["smoke"], departure, files.ToImmutable(), outputs.ToImmutable());
    }

    private static byte[] Read(string root, string relative, string hash, int length)
    {
        Require(length is > 0 and <= 1_000_000 && !Path.IsPathRooted(relative) && !relative.Contains('\\') &&
            !relative.Split('/').Any(p => p is "" or "." or ".."), "Unsafe S.S. Anne art path or size.");
        var info = new FileInfo(Path.Combine(Path.GetFullPath(root), relative));
        Require(info.LinkTarget is null, "S.S. Anne art symlink rejected.");
        for (var parent = info.Directory; parent is not null; parent = parent.Parent)
            Require(parent.LinkTarget is null, "S.S. Anne art directory symlink rejected.");
        using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(stream.Length == length, "S.S. Anne art size differs: " + relative);
        byte[] bytes = new byte[length]; stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1 && Convert.ToHexStringLower(SHA256.HashData(bytes)) == hash,
            "S.S. Anne art hash differs: " + relative);
        return bytes;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
