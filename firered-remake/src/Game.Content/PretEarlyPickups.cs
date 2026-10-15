using System.Collections.Immutable;
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
    public ImmutableArray<PretEarlyPickupDefinition> Pickups { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public ImmutableDictionary<string, int> Constants { get; }
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
    }

    public PretEarlyPickupDefinition? FindItemBall(string mapSymbol, int sourceLocalId) =>
        Pickups.SingleOrDefault(p => p.Kind == "itemBall" && p.MapSymbol == mapSymbol && p.SourceLocalId == sourceLocalId);

    public PretEarlyPickupDefinition? FindHiddenItem(string mapSymbol, int x, int y) =>
        Pickups.SingleOrDefault(p => p.Kind == "hiddenItem" && p.MapSymbol == mapSymbol && p.X == x && p.Y == y);

    public static PretEarlyPickups Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        byte[] Read(string relative)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, relative));
            return bytes;
        }
        using var manifestDocument = JsonDocument.Parse(Read("content/generated/early-pickups/manifest.json"));
        var manifest = manifestDocument.RootElement;
        var output = manifest.GetProperty("outputs")[0];
        Require(output.GetProperty("path").GetString() == "early-pickups.json" , "Early-pickups catalog binding differs.");
        using var catalogDocument = JsonDocument.Parse(Read("content/generated/early-pickups/early-pickups.json"));
        var catalog = catalogDocument.RootElement;
        Require(catalog.GetProperty("schemaVersion").GetInt32() == 1 , "Early-pickups source inventory differs.");
        string Raw(string path) => new UTF8Encoding(false, true).GetString(Read(PretSourcePaths.FireRedRawDirectory + "/" + path));
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var e in catalog.GetProperty("inputFiles").EnumerateArray())
        {
            string path = e.GetProperty("path").GetString()!;
            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var options = ContentJson.CreateOptions();
        T Decode<T>(JsonElement element) => element.Deserialize<T>(options) ?? throw new InvalidDataException("Missing early-pickups value.");
        var pickups = Decode<ImmutableArray<PretEarlyPickupDefinition>>(catalog.GetProperty("pickups"));
        Require(pickups.Length == 8 && pickups.Count(p => p.Kind == "itemBall") == 5 &&
            pickups.Count(p => p.Kind == "hiddenItem") == 3 && pickups.Select(p => p.Flag).Distinct(StringComparer.Ordinal).Count() == 8,
            "Early-pickups bounded scope differs.");
        foreach (var p in pickups)
        {
            Require(p.Id == p.Flag && p.ItemId == "pret:" + p.ItemSymbol && p.Quantity == 1 && !p.Underfoot &&
                p.ItemSymbol is "ITEM_POTION" or "ITEM_ANTIDOTE" or "ITEM_POKE_BALL" &&
                p.Pocket is "POCKET_ITEMS" or "POCKET_POKE_BALLS", "Unsupported early-pickup definition.");
        }
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in catalog.GetProperty("constants").EnumerateObject())
        {
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        }
        foreach (var p in pickups)
            Require(constants[p.Flag] == p.SourceFlagId && constants[p.ItemSymbol] == p.SourceItemId, "Early-pickup constant differs.");
        var ui = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var text in catalog.GetProperty("texts").EnumerateArray())
        {
            ui.Add(text.GetProperty("symbol").GetString()!, text.GetProperty("text").GetString()!);
        }
        Require(ui.Count == 6, "Early-pickups source texts differ.");
        var scripts = new[] { "data/scripts/item_ball_scripts.inc","data/scripts/obtain_item.inc" }.Select(path =>
            new PretSourceScriptFile(path, Raw(path), new(SourceKind.Pret,path,"early-pickups-source-library","pret/pokefirered",                PretWorldMap.SourceCommit))).ToImmutableArray();
        return new(pickups, scripts, ImmutableDictionary<string, string>.Empty.Add("data/text/obtain_item.inc", Raw("data/text/obtain_item.inc")),
            ui.ToImmutable(), constants.ToImmutable(), inventory, catalog.Clone());
    }
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
