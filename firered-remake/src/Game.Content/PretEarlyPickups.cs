using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretEarlyPickupDefinition(string Id, string MapSymbol, string Kind, int SourceEventIndex,
    int? SourceLocalId, int X, int Y, int Elevation, string Script, string ItemSymbol, int SourceItemId,
    string ItemId, string ItemName, string Pocket, int Quantity, string Flag, int SourceFlagId, bool Underfoot,
    OpeningItemSourceRef EventSource, OpeningItemSourceRef ItemSource, OpeningItemSourceRef ScriptSource);

/// <summary>
/// Authenticated source definitions for five item balls and three hidden items. This supplies source
/// libraries and exact identities; it grants no inventory, map, VM or save-state execution permissions.
/// </summary>
public sealed class PretEarlyPickups
{
    public const string Profile = "firered-early-field-pickups-v1";
    public const string LockSha256 = "2fa09d60b48e4af8fec827a4fc5fba30c6ef92c91d3afa87bc9cd10e76a1e6c6";
    public const string CatalogSha256 = "cd2ea001ffbade1723101dbbd0dbfd2d781c432e5f90f37ab1da7712b6f8e356";
    public const string ManifestSha256 = "6d526c16b3882bb2a778a3f847aa703afe97ca255fbc3b1d6c43cc7ba0862172";
    public const string FullSourceHash = "512d97fdbaf4a7d27f66671e7a79e414e9fcbdb5fa5c90d9ea5f2dd13b03a735";
    public ImmutableArray<PretEarlyPickupDefinition> Pickups { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    /// <summary>Reviewed original-line blocks, macro and native dependencies, excluded branches and scope limits.</summary>
    public JsonElement SourceContract { get; }

    private PretEarlyPickups(ImmutableArray<PretEarlyPickupDefinition> pickups,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> textSources,
        ImmutableDictionary<string, string> uiTexts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement sourceContract)
    {
        Pickups = pickups; Scripts = scripts; TextSources = textSources; SourceUiTexts = uiTexts;
        Constants = constants; SourceFiles = files; SourceContract = sourceContract;
        AdditionalSourceHashes = ["early-pickups-manifest:" + ManifestSha256, "early-pickups-lock:" + LockSha256,
            .. files.Select(f => "early-pickups:" + f.Path + ":" + f.Sha256)];
    }

    public PretEarlyPickupDefinition? FindItemBall(string mapSymbol, int sourceLocalId) =>
        Pickups.SingleOrDefault(p => p.Kind == "itemBall" && p.MapSymbol == mapSymbol && p.SourceLocalId == sourceLocalId);

    public PretEarlyPickupDefinition? FindHiddenItem(string mapSymbol, int x, int y) =>
        Pickups.SingleOrDefault(p => p.Kind == "hiddenItem" && p.MapSymbol == mapSymbol && p.X == x && p.Y == y);

    public static PretEarlyPickups Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        byte[] Read(string relative, string sha, int? length = null)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, relative));
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned early-pickups bytes differ: " + relative);
            return bytes;
        }
        using var lockDocument = JsonDocument.Parse(Read("content/early-pickups-source-lock.json", LockSha256));
        using var manifestDocument = JsonDocument.Parse(Read("content/generated/early-pickups/manifest.json", ManifestSha256));
        var manifest = manifestDocument.RootElement;
        Require(manifest.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit &&
            manifest.GetProperty("profile").GetString() == Profile && manifest.GetProperty("inputCount").GetInt32() == 23 &&
            manifest.GetProperty("pickupCount").GetInt32() == 8 && manifest.GetProperty("outputs").GetArrayLength() == 1 &&
            manifest.GetProperty("lockSha256").GetString() == LockSha256 &&
            manifest.GetProperty("fullSourceHash").GetString() == FullSourceHash, "Early-pickups manifest identity differs.");
        var output = manifest.GetProperty("outputs")[0];
        Require(output.GetProperty("path").GetString() == "early-pickups.json" &&
            output.GetProperty("sha256").GetString() == CatalogSha256, "Early-pickups catalog binding differs.");
        using var catalogDocument = JsonDocument.Parse(Read("content/generated/early-pickups/early-pickups.json",
            CatalogSha256, output.GetProperty("byteLength").GetInt32()));
        var catalog = catalogDocument.RootElement;
        var sourceLock = lockDocument.RootElement;
        Require(catalog.GetProperty("schemaVersion").GetInt32() == 1 && catalog.GetProperty("profile").GetString() == Profile &&
            catalog.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit &&
            sourceLock.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), sourceLock.GetProperty("files")), "Early-pickups source inventory differs.");
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var e in sourceLock.GetProperty("files").EnumerateArray())
        {
            string path = e.GetProperty("path").GetString()!, sha = e.GetProperty("sha256").GetString()!, blob = e.GetProperty("gitBlobSha1").GetString()!;
            int length = e.GetProperty("byteLength").GetInt32();
            byte[] bytes = Read("content/raw/early-pickups/" + path, sha, length);
            byte[] header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Early-pickups Git blob differs: " + path);
            Require(raw.TryAdd(path, new UTF8Encoding(false, true).GetString(bytes)), "Duplicate early-pickups source: " + path);
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(raw.Count == 23 && Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Early-pickups source set differs.");
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var options = ContentJson.CreateOptions();
        T Decode<T>(JsonElement element) => element.Deserialize<T>(options) ?? throw new InvalidDataException("Missing early-pickups value.");
        void Source(OpeningItemSourceRef source)
        {
            Require(byPath.TryGetValue(source.Path, out var file) && source.Sha256 == file.Sha256 &&
                !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
                source.EndLine <= raw[source.Path].Split('\n').Length, "Early-pickups source reference differs.");
        }
        var pickups = Decode<ImmutableArray<PretEarlyPickupDefinition>>(catalog.GetProperty("pickups"));
        Require(pickups.Length == 8 && pickups.Count(p => p.Kind == "itemBall") == 5 &&
            pickups.Count(p => p.Kind == "hiddenItem") == 3 && pickups.Select(p => p.Flag).Distinct(StringComparer.Ordinal).Count() == 8,
            "Early-pickups bounded scope differs.");
        foreach (var p in pickups)
        {
            Source(p.EventSource); Source(p.ItemSource); Source(p.ScriptSource);
            Require(p.Id == p.Flag && p.ItemId == "pret:" + p.ItemSymbol && p.Quantity == 1 && !p.Underfoot &&
                p.ItemSymbol is "ITEM_POTION" or "ITEM_ANTIDOTE" or "ITEM_POKE_BALL" &&
                p.Pocket is "POCKET_ITEMS" or "POCKET_POKE_BALLS", "Unsupported early-pickup definition.");
            using var map = JsonDocument.Parse(raw[p.EventSource.Path]);
            var ev = map.RootElement.GetProperty(p.Kind == "itemBall" ? "object_events" : "bg_events")[p.SourceEventIndex];
            Require(map.RootElement.GetProperty("id").GetString() == p.MapSymbol && ev.GetProperty("x").GetInt32() == p.X &&
                ev.GetProperty("y").GetInt32() == p.Y && ev.GetProperty("elevation").GetInt32() == p.Elevation &&
                ev.GetProperty("flag").GetString() == p.Flag, "Early-pickup map identity differs.");
            if (p.Kind == "itemBall")
                Require(p.SourceLocalId == p.SourceEventIndex + 1 && ev.GetProperty("script").GetString() == p.Script &&
                    ev.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_ITEM_BALL", "Early item-ball identity differs.");
            else
                Require(p.SourceLocalId is null && ev.GetProperty("item").GetString() == p.ItemSymbol &&
                    ev.GetProperty("quantity").GetInt32() == p.Quantity && !ev.GetProperty("underfoot").GetBoolean(), "Early hidden-item identity differs.");
        }
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in catalog.GetProperty("constants").EnumerateObject())
        {
            Source(Decode<OpeningItemSourceRef>(property.Value.GetProperty("source")));
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        }
        foreach (var p in pickups)
            Require(constants[p.Flag] == p.SourceFlagId && constants[p.ItemSymbol] == p.SourceItemId, "Early-pickup constant differs.");
        var ui = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var text in catalog.GetProperty("texts").EnumerateArray())
        {
            Source(Decode<OpeningItemSourceRef>(text.GetProperty("source")));
            ui.Add(text.GetProperty("symbol").GetString()!, text.GetProperty("text").GetString()!);
        }
        Require(ui.Count == 6, "Early-pickups source texts differ.");
        foreach (var block in catalog.GetProperty("scriptBlocks").EnumerateArray())
        {
            var source = Decode<OpeningItemSourceRef>(block.GetProperty("source")); Source(source);
            var lines = raw[source.Path].Split('\n');
            foreach (var row in block.GetProperty("lines").EnumerateArray())
                Require(lines[row.GetProperty("line").GetInt32() - 1] == row.GetProperty("text").GetString(), "Early-pickups original instruction differs.");
        }
        var scripts = new[] { "data/scripts/item_ball_scripts.inc", "data/scripts/obtain_item.inc" }.Select(path =>
            new PretSourceScriptFile(path, raw[path], new(SourceKind.Pret, path, "early-pickups-source-library", "pret/pokefirered",
                PretWorldMap.SourceCommit, byPath[path].Sha256))).ToImmutableArray();
        return new(pickups, scripts, ImmutableDictionary<string, string>.Empty.Add("data/text/obtain_item.inc", raw["data/text/obtain_item.inc"]),
            ui.ToImmutable(), constants.ToImmutable(), inventory, catalog.Clone());
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe early-pickups path.");
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Early-pickups directory symlink is unsupported.");
        string result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Early-pickups symlink is unsupported: " + path);
        }
        return result;
    }
}
