using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCityServiceItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PewterMachineItem> Items, ImmutableArray<PretPewterMachineIcon> Icons);

/// <summary>Authenticates Fan Club voucher, Old Rod and Bicycle exchange content. Registers no gameplay handlers.</summary>
public sealed class PretCityServices
{
    public const string Profile = "firered-city-services-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    public ImmutableArray<PewterMachineItem> Items { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons { get; }
    public JsonElement Services => Dependencies.GetProperty("services");
    public ImmutableArray<MapId> ReusedMapIds => [new("map.cerulean-city-bike-shop")];
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretCityServices(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement scriptsInventory,
        JsonElement npcSources, PretCityServiceItemsDocument items)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        ScriptInventory = scriptsInventory; NpcSources = npcSources; Items = items.Items; Icons = items.Icons;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VERMILION_CITY_POKEMON_FAN_CLUB" => new("map.vermilion-city-pokemon-fan-club"),
        "MAP_VERMILION_CITY_HOUSE1" => new("map.vermilion-city-house-1"),
        _ => PretVermilionRegion.MapIdFromSourceSymbol(symbol)
    };

    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var values = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unsupported connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new Game.Domain.World.ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), values);
    }

    public static PretCityServices Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/city-services");
        var rawDirectory = Path.Combine(project, PretSourcePaths.FireRedRawDirectory);
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project,"content/sources/city-services.lock.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "City services manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "city-services/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate City services output.");
        }
        using var catalog = JsonDocument.Parse(outputs["city-services-catalog.json"]);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        byte[] Raw(string path) => File.ReadAllBytes(SafeFile(rawDirectory, path));
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;

            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated city gift and exchange source profile; runtime certification is separate.", Profile, inventory);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact))
            {


                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate City services runtime map.");
                continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "City services script reference closure is incomplete.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in scriptInventory.RootElement.GetProperty("filePreambles").EnumerateObject())
        {
            var path = property.Name;
            var text = Encoding.UTF8.GetString(Raw(path));
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Require(symbol.Length > 0, "City services script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_GOT_BIKE_VOUCHER"] == 0x241 && constants["FLAG_GOT_OLD_ROD"] == 0x240 &&
            constants["FLAG_GOT_BICYCLE"] == 0x271 && constants["BAG_KEYITEMS_COUNT"] == 30 &&
            constants["SPOKE_TO_WOMAN_LAST"] == 2 && constants["SPOKE_TO_FAT_MAN_LAST"] == 3,
            "City-service constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(!dependencies.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependencies.RootElement.GetProperty("services").GetArrayLength() == 3 &&
            !dependencies.RootElement.GetProperty("purchase").GetProperty("canPurchase").GetBoolean(),
            "City-service dependency scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        Require(npcSources.RootElement.GetProperty("graphics").GetArrayLength() == 8, "City-service actor inventory differs.");
        var itemDocument = ContentJson.ParseStrict<PretCityServiceItemsDocument>(Encoding.UTF8.GetString(outputs["items.json"]), ContentJson.CreateOptions());
        Require(itemDocument.SchemaVersion == 1 &&
            itemDocument.Items.Length == 3 && itemDocument.Icons.Length == 3, "City-service item scope differs.");
        int[] sourceIds = [352, 262, 360]; string[] symbols = ["BIKE_VOUCHER", "OLD_ROD", "BICYCLE"];
        string[] use = ["FieldUseFunc_OakStopsYou", "FieldUseFunc_Rod", "FieldUseFunc_Bike"];
        for (var i = 0; i < 3; i++)
        {
            var item = itemDocument.Items[i].Item; var icon = itemDocument.Icons[i];
            Require(item.Id.Value == "pret:ITEM_" + symbols[i] && item.SourceItemId == sourceIds[i] &&
                item.Pocket == "POCKET_KEY_ITEMS" && item.Price == 0 && item.Importance == 1 && item.FieldUseFunction == use[i] &&
                !item.CanToss && !item.CanGive && !item.CanSell && item.SecondaryId == 0 && item.BattleUsage == 0 &&
                item.Registrability == (i == 0 ? 0 : 1), "City-service key item declaration differs.");
            Require(icon.Item == item.Id && icon.Width == 24 && icon.Height == 24 &&
                PngRgba8.Decode(outputs[icon.PngPath], icon.Width, icon.Height).Length == 24 * 24 * 4 ,
                "City-service icon identity differs.");
        }
        // The existing Cerulean loader owns the reused map; verify its certificate and selected bytes without rendering or duplicating it.
        var reused = catalog.RootElement.GetProperty("reusedMaps");
        Require(reused.GetArrayLength() == 1 && reused[0].GetProperty("stableId").GetString() == "map.cerulean-city-bike-shop" , "City-service reused map certificate differs.");
        foreach (var output in reused[0].GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "cerulean-region/" + path)) continue;
        }
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(),
            npcSources.RootElement.Clone(), itemDocument);
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe City services path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("City services directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("City services file symlink is unsupported: " + path);
        }
        return result;
    }
}
