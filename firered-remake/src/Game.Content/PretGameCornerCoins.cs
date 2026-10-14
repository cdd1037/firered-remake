using System.Collections.Immutable;
using System.Security.Cryptography;
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
    public const string ScriptsSha256 = "1e05d3520f34fa7a6bad3542e0f99b205d0308cdb9d9e812c33a8f0b71498da5";
    public const string RestaurantScriptsSha256 = "c550a75439d24dc9b2d62087f232cfba03448d392cd8a893466957fdd9f7f037";
    public const string ItemsSha256 = "4fbb252e764306ac9f52c620ddba15e041f35cbce5a5b17d8acffd4cb3652511";
    public const string ContractSha256 = "4917bf28f9fbd64d3cb356e3e363e71bf72466f56943dd0da0e9244496561cb0";
    public const string SourceInputsSha256 = "2aec56dc05142567222ecec017a8c33c228cfb4a836e5f009296f05da35187b1";
    public const string LockSha256 = "3a525efa44a9d9bc4004ce5bbda003ae5496f7b7691eab1a2f0f3f89189ccf63";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } =
        ["game-corner-coins-items:" + ItemsSha256, "game-corner-coins-contract:" + ContractSha256,
         "game-corner-coins-inputs:" + SourceInputsSha256, "game-corner-coins-lock:" + LockSha256];
    public ImmutableArray<PewterMachineItem> Items { get; }
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

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException("game_corner_coins_" + message);
    }
    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        var bytes = PretRuntimeMapValidation.ReadBounded(PretRuntimeMapValidation.SafeFile(root, relative), length ?? 4 * 1024 * 1024);
        Need((length is null || bytes.Length == length) && Hash(bytes) == hash, "bytes:" + relative);
        return bytes;
    }

    public static PretGameCornerCoins Load(string root, string? dependencyRoot = null)
    {
        using var locked = JsonDocument.Parse(Read(root, "scripts/game-corner-coins.lock.json", LockSha256));
        using var evidence = JsonDocument.Parse(Read(root, Base + "source-input-hashes.json", SourceInputsSha256));
        using var facts = JsonDocument.Parse(Read(root, Base + "source-contract.json", ContractSha256));
        foreach (var document in new[] { locked.RootElement, evidence.RootElement, facts.RootElement })
            Need(document.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit &&
                document.GetProperty("sourceTree").GetString() == PretCanonicalPokemonCatalog.SourceTree, "source_identity");
        Need(locked.RootElement.GetProperty("profile").GetString() == Profile &&
            facts.RootElement.GetProperty("profile").GetString() == Profile, "profile");
        var inputs = evidence.RootElement.GetProperty("inputs");
        Need(inputs.GetArrayLength() == 25 && locked.RootElement.GetProperty("files").GetArrayLength() == 8, "inventory_count");
        var sourceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in inputs.EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var hash = input.GetProperty("sha256").GetString()!;
            var bytes = Read(input.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root,
                input.GetProperty("localPath").GetString()!, hash, input.GetProperty("byteLength").GetInt32());
            var blob = Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes]));
            Need(blob == input.GetProperty("gitBlobSha1").GetString() && sourceHashes.TryAdd(path, hash), "source_blob:" + path);
        }
        foreach (var output in locked.RootElement.GetProperty("files").EnumerateArray())
            _ = Read(root, output.GetProperty("path").GetString()!, output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        _ = Read(root, "scripts/import-game-corner-coins.py", locked.RootElement.GetProperty("importerSha256").GetString()!);
        Need(sourceHashes[ScriptPath] == ScriptsSha256 && sourceHashes[RestaurantScriptPath] == RestaurantScriptsSha256, "service_scripts");
        var items = ContentJson.ParseStrict<PretCityServiceItemsDocument>(Encoding.UTF8.GetString(Read(root, Base + "items.json", ItemsSha256)), ContentJson.CreateOptions());
        Need(items.SchemaVersion == 1 && items.Profile == Profile && items.SourceCommit == PretWorldMap.SourceCommit &&
            items.Items.Length == 1 && items.Icons.Length == 1, "item_scope");
        var item = items.Items[0].Item; var icon = items.Icons[0];
        Need(item.Id.Value == "pret:ITEM_COIN_CASE" && item.SourceItemId == 260 && item.SourceItemSymbol == "ITEM_COIN_CASE" &&
            item.Pocket == "POCKET_KEY_ITEMS" && item.Type == "ITEM_TYPE_BAG_MENU" && item.FieldUseFunction == "FieldUseFunc_CoinCase" &&
            item.Price == 0 && item.Importance == 1 && item.Registrability == 0 && item.BattleUsage == 0 && item.SecondaryId == 0 &&
            !item.CanGive && !item.CanToss && !item.CanSell, "item_metadata");
        var rgba = Convert.FromBase64String(icon.RgbaBase64);
        Need(icon.Item == item.Id && icon.Width == 24 && icon.Height == 24 && rgba.Length == 24 * 24 * 4 &&
            Hash(rgba) == icon.RgbaSha256 && icon.PngPath == "coin-case.png", "icon");
        _ = Read(root, Base + icon.PngPath, icon.PngSha256);
        foreach (var reference in new[] { item.Source, items.Items[0].DescriptionSource, icon.Source, icon.PaletteSource, icon.BindingSource })
            Need(sourceHashes.TryGetValue(reference.Path, out var sourceHash) && sourceHash == reference.Sha256 &&
                reference.StartLine > 0 && reference.EndLine >= reference.StartLine, "item_source_reference");
        var contract = facts.RootElement;
        Need(contract.GetProperty("mapsAdded").GetArrayLength() == 0 && contract.GetProperty("services").GetArrayLength() == 5 &&
            contract.GetProperty("scripts").GetArrayLength() == 24 && contract.GetProperty("maximumCoins").GetInt32() == 9999 &&
            contract.GetProperty("excluded").EnumerateArray().Select(e => e.GetString()).SequenceEqual(new[] { "slot-machines", "hidden-coins", "prize-redemption" }) &&
            contract.GetProperty("bagUse").GetProperty("text").GetString() == "Your COINS:\n{STR_VAR_1}{PAUSE_UNTIL_PRESS}", "service_scope");
        return new(root, items, contract.Clone());
    }
}
