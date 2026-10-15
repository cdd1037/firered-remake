using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCityServiceItemsDocument(int SchemaVersion, ImmutableArray<MachineItem> Items, ImmutableArray<PretPewterMachineIcon> Icons);

/// <summary>Authenticates Fan Club voucher, Old Rod and Bicycle exchange content. Registers no gameplay handlers.</summary>
public sealed class PretCityServices
{
    public const string Profile = "firered-city-services-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Dependencies { get; }
    public ImmutableArray<MachineItem> Items { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons { get; }
    public JsonElement Services => Dependencies.GetProperty("services");
    public ImmutableArray<MapId> ReusedMapIds => [new("map.cerulean-city-bike-shop")];

    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretCityServices(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement dependencies, JsonElement catalog, JsonElement npcSources, PretCityServiceItemsDocument items)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; Dependencies = dependencies; Catalog = catalog;
        NpcSources = npcSources; Items = items.Items; Icons = items.Icons;
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

    public static PretCityServices Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null, SharedMachineCatalog? shared = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/city-services");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "City services manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "maps/regions/city-services/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(path.StartsWith("content/generated/", StringComparison.Ordinal) ? project : directory,path)), "Duplicate City services output.");
        }
        using var catalog = JsonDocument.Parse(outputs["city-services-catalog.json"]);

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated city gift and exchange source profile; runtime certification is separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("mapPath").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact))
            {

                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate City services runtime map.");
                continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_GOT_BIKE_VOUCHER"] == 0x241 && constants["FLAG_GOT_OLD_ROD"] == 0x240 &&
            constants["FLAG_GOT_BICYCLE"] == 0x271 && constants["BAG_KEYITEMS_COUNT"] == 30 &&
            constants["SPOKE_TO_WOMAN_LAST"] == 2 && constants["SPOKE_TO_FAT_MAN_LAST"] == 3,
            "City-service constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(dependencies.RootElement.GetProperty("services").GetArrayLength() == 3 &&
            !dependencies.RootElement.GetProperty("purchase").GetProperty("canPurchase").GetBoolean(),
            "City-service dependency scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        Require(npcSources.RootElement.GetProperty("graphics").GetArrayLength() == 8, "City-service actor inventory differs.");
        var itemDocument = ContentJson.ParseStrict<PretCityServiceItemsDocument>(Encoding.UTF8.GetString(outputs["items.json"]), (shared ?? SharedMachineCatalog.LoadProject(project)).CreateJsonOptions());
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
                outputs.ContainsKey(icon.PngPath) ,
                "City-service icon identity differs.");
        }
        // The existing Cerulean loader owns the reused map; verify its certificate and selected bytes without rendering or duplicating it.
        var reused = catalog.RootElement.GetProperty("reusedMaps");
        Require(reused.GetArrayLength() == 1 && reused[0].GetProperty("stableId").GetString() == "map.cerulean-city-bike-shop" , "City-service reused map certificate differs.");
        foreach (var output in reused[0].GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "maps/regions/cerulean/" + path)) continue;
        }
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), dependencies.RootElement.Clone(), catalog.RootElement.Clone(), npcSources.RootElement.Clone(), itemDocument);
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
