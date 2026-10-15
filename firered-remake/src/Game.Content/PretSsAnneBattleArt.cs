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
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public ImmutableArray<ForestTrainerPortrait> Portraits { get; }
    public SsAnneDepartureSheet Wake { get; }
    public SsAnneDepartureSheet Smoke { get; }
    public SsAnneDepartureCadence Departure { get; }

    private PretSsAnneBattleArt(ImmutableArray<ForestTrainerPortrait> portraits, SsAnneDepartureSheet wake,
        SsAnneDepartureSheet smoke, SsAnneDepartureCadence departure,
        ImmutableDictionary<string, byte[]> outputs)
    {
        Portraits = portraits; Wake = wake; Smoke = smoke; Departure = departure; _outputs = outputs;
    }

    /// <summary>Authenticated palette-expanded PNG bytes; callers may load them without another path lookup.</summary>
    public byte[] ReadPng(string id) => ReadAsset(id);

    private byte[] ReadAsset(string id) => _outputs.TryGetValue(id + ".png", out var bytes)
        ? (byte[])bytes.Clone() : throw new InvalidDataException("Unknown S.S. Anne art asset: " + id);

    public static PretSsAnneBattleArt Load(string root)
    {
        using var manifest = JsonDocument.Parse(Read(root,"content/generated/battle/art/ssanne-battle-art/manifest.json"));
        var document = manifest.RootElement;
        Require(document.GetProperty("schemaVersion").GetInt32() == 1 &&
            document.GetProperty("paletteExpansion").GetString() == "(rgb5 << 3) | (rgb5 >> 2)" && document.GetProperty("assets").GetArrayLength() == 6 , "S.S. Anne art manifest differs.");
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in document.GetProperty("outputs").EnumerateArray())
        {
            string path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(root,"content/generated/battle/art/ssanne-battle-art/" + path)), "Duplicate S.S. Anne art output.");
        }
        var portraits = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        var sheets = new Dictionary<string, SsAnneDepartureSheet>(StringComparer.Ordinal);
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            Require((outputs[id + ".png"]).Length > 0, "S.S. Anne PNG payload differs.");
            byte[] png = outputs[id + ".png"];
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
                portraits.Add(new(id, 144, 8, outputs[id + ".png"]));
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
        return new(portraits.ToImmutable(), sheets["wake"], sheets["smoke"], departure, outputs.ToImmutable());
    }

    private static byte[] Read(string root,string relative)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Contains('\\') &&
            !relative.Split('/').Any(p => p is "" or "." or ".."), "Unsafe S.S. Anne art path or size.");
        var info = new FileInfo(Path.Combine(Path.GetFullPath(root), relative));
        Require(info.LinkTarget is null, "S.S. Anne art symlink rejected.");
        for (var parent = info.Directory; parent is not null; parent = parent.Parent)
            Require(parent.LinkTarget is null, "S.S. Anne art directory symlink rejected.");
var bytes = File.ReadAllBytes(info.FullName);
return bytes;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
