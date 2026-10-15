using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

/// <summary>One authenticated key item and five ordinary services on existing maps. No slots, hidden coins or prizes.</summary>
public sealed class PretGameCornerCoins
{
    public const string Base = "content/generated/game-corner-coins/";
    public const string Profile = "firered-game-corner-coins-v1";
    public const string ScriptPath = "data/maps/CeladonCity_GameCorner/scripts.inc";
    public const string RestaurantScriptPath = "data/maps/CeladonCity_Restaurant/scripts.inc";
    public ImmutableArray<MachineItem> Items { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contract { get; }
    public string GeneratedDirectory { get; }
    public string ItemUseText => Contract.GetProperty("bagUse").GetProperty("text").GetString()!;

    private PretGameCornerCoins(string root, PretCityServiceItemsDocument document, JsonElement contract)
    {
        GeneratedDirectory = Path.Combine(root, Base); Items = document.Items; Icons = document.Icons; Contract = contract;
        Constants = contract.GetProperty("constants").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
    }
    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException("game_corner_coins_" + message);
    }
    private static byte[] Read(string root,string relative)
    {
        var bytes = File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(root, relative));
        return bytes;
    }

    public static PretGameCornerCoins Load(string root, string? dependencyRoot = null, SharedMachineCatalog? shared = null)
    {
        using var facts = JsonDocument.Parse(Read(root, Base + "source-contract.json"));
        var items = ContentJson.ParseStrict<PretCityServiceItemsDocument>(Encoding.UTF8.GetString(Read(root,Base + "items.json")), (shared ?? SharedMachineCatalog.LoadProject(dependencyRoot ?? root)).CreateJsonOptions());
        Need(items.SchemaVersion == 1 &&
            items.Items.Length == 1 && items.Icons.Length == 1, "item_scope");
        var item = items.Items[0].Item; var icon = items.Icons[0];
        Need(item.Id.Value == "pret:ITEM_COIN_CASE" && item.SourceItemId == 260 && item.SourceItemSymbol == "ITEM_COIN_CASE" &&
            item.Pocket == "POCKET_KEY_ITEMS" && item.Type == "ITEM_TYPE_BAG_MENU" && item.FieldUseFunction == "FieldUseFunc_CoinCase" &&
            item.Price == 0 && item.Importance == 1 && item.Registrability == 0 && item.BattleUsage == 0 && item.SecondaryId == 0 &&
            !item.CanGive && !item.CanToss && !item.CanSell, "item_metadata");
        var pngBytes = Read(root, icon.PngPath.StartsWith("content/generated/", StringComparison.Ordinal) ? icon.PngPath : Base + icon.PngPath);
        Need(icon.Item == item.Id && icon.Width == 24 && icon.Height == 24 && pngBytes.Length > 0&& icon.Region is not null, "icon");
        var contract = facts.RootElement;
        Need(contract.GetProperty("mapsAdded").GetArrayLength() == 0 && contract.GetProperty("services").GetArrayLength() == 5 && contract.GetProperty("maximumCoins").GetInt32() == 9999 &&
            contract.GetProperty("excluded").EnumerateArray().Select(e => e.GetString()).SequenceEqual(new[] { "slot-machines","hidden-coins","prize-redemption" }) &&
            contract.GetProperty("bagUse").GetProperty("text").GetString() == "Your COINS:\n{STR_VAR_1}{PAUSE_UNTIL_PRESS}", "service_scope");
        return new(root, items, contract.Clone());
    }
}
