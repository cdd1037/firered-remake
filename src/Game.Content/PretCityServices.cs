using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCityServiceItemsDocument(int SchemaVersion, ImmutableArray<MachineItem> Items, ImmutableArray<ItemIcon> Icons);

/// <summary>Authenticates Fan Club voucher, Old Rod and Bicycle exchange content. Registers no gameplay handlers.</summary>
public sealed class PretCityServices
{
    public const string Profile = "firered-city-services-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<MachineItem> Items { get; }
    public ImmutableArray<ItemIcon> Icons { get; }
    public ImmutableArray<MapId> ReusedMapIds => [new("map.cerulean-city-bike-shop")];

    public string GeneratedDirectory { get; }

    private PretCityServices(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        PretCityServiceItemsDocument items)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants;
         Items = items.Items; Icons = items.Icons;
    }

    public static PretCityServices Load(PretCurrentWorld world, SharedMachineCatalog? shared = null)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/city-services");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
            return bytes;
        }
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["items.json"] = ReadPinned(directory, "items.json"),
        };
        var maps = world.MapsInDirectory(directory);

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_GOT_BIKE_VOUCHER"] == 0x241 && constants["FLAG_GOT_OLD_ROD"] == 0x240 &&
            constants["FLAG_GOT_BICYCLE"] == 0x271 && constants["BAG_KEYITEMS_COUNT"] == 30 &&
            constants["SPOKE_TO_WOMAN_LAST"] == 2 && constants["SPOKE_TO_FAT_MAN_LAST"] == 3,
            "City-service constants differ.");
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
                File.Exists(GeneratedResourceFile.SafePath(icon.PngPath.StartsWith("content/generated/", StringComparison.Ordinal) ? project : directory, icon.PngPath)) ,
                "City-service icon identity differs.");
        }
        return new(directory, maps, constants.ToImmutable(), itemDocument);
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
}
