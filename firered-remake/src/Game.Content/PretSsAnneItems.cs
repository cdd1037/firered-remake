using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretSsAnneItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items, PretBillSourceItem BerryPouch, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups, ImmutableArray<string> Limitations);

/// <summary>Authenticates fourteen ship item declarations, their icons, twelve pickups and the Berry Pouch dependency.</summary>
public static class PretSsAnneItems
{
    public const string Profile = "firered-ssanne-items-v1";
    public const string LockSha256 = "13eb19643061145dcefa8d1d2453c11a5235968c6452298adbaf9a83c61151ce";
    public const string GeneratedSha256 = "7081249b04ccae721f82b01e9cd334091f15d8818da78905ebb13776ee98f69f";
    public const string ManifestSha256 = "a7602ccf9ab22c2c85dc3fa71bdc712b9583139619296388cd5cad7dc2d4c2b8";
    public const string RawManifestSha256 = "65f8cd27b141ae26e226a401a5d8ec44d605ea592f5c7664f2791724044e400e";
    public const string FullSourceHash = "3b83e43dd97e6bb13a4079994b92caf6d0e2b31b88c959b18b4aa2bd6bd5e472";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretSsAnneItemsDocument Load(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "S.S. Anne item document differs.");
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretSsAnneItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Profile == Profile &&
            document.SourceCommit == OpeningInventoryRules.SourceCommit && document.Inputs.Length == 66 && document.Items.Length == 14 && document.Icons.Length == 15 &&
            document.Pickups.Length == 12 && document.BerryPouch is not null &&
            !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "S.S. Anne item scope differs.");
        var inputs = document.Inputs.ToDictionary(input => input.Path, StringComparer.Ordinal);
        Require(Hash(Utf8.GetBytes(string.Concat(document.Inputs.OrderBy(input => input.Path, StringComparer.Ordinal)
            .Select(input => input.Path + "\0" + input.Sha256 + "\n")))) == FullSourceHash,
            "S.S. Anne item input identity differs.");
        void Source(OpeningItemSourceRef source) => Require(source is not null &&
            !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
            inputs.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256,
            "S.S. Anne item source reference differs.");
        foreach (var source in document.RulesSources) Source(source);
        string[] items = ["CHERI_BERRY", "CHESTO_BERRY", "ETHER", "GREAT_BALL", "HM01", "HYPER_POTION", "LAVA_COOKIE", "PECHA_BERRY", "SS_TICKET", "STARDUST", "SUPER_POTION", "TM31", "TM44", "X_ATTACK", "BERRY_POUCH"];
        var definitions = document.Items.Add(document.BerryPouch!);
        for (int n = 0; n < definitions.Length; n++)
        {
            var item = definitions[n]; var icon = document.Icons[n];
            Require(item.Metadata.Item.Id.Value == "pret:ITEM_" + items[n] && icon.Item == item.Metadata.Item.Id,
                "S.S. Anne item order or icon differs.");
            Source(item.Metadata.Item.Source); Source(item.Metadata.DescriptionSource);
            if (item.EffectSource is not null) Source(item.EffectSource);
            Source(icon.Source); Source(icon.PaletteSource); Source(icon.BindingSource);
            _ = PretPewterMachines.IconRgba(icon);
        }
        foreach (var pickup in document.Pickups)
        {
            Source(pickup.Source);
            if (pickup.ScriptSource is not null) Source(pickup.ScriptSource);
            Require(pickup.Quantity == 1 && !pickup.Underfoot && pickup.Elevation is 0 or 3 &&
                pickup.Map.StartsWith("SSAnne_", StringComparison.Ordinal) && pickup.X >= 0 && pickup.Y >= 0 &&
                (pickup.Kind == "visible" ? pickup.LocalObjectId > 0 && pickup.Script is not null && pickup.ScriptSource is not null :
                pickup.Kind == "hidden" && pickup.LocalObjectId is null && pickup.Script is null && pickup.ScriptSource is null),
                "S.S. Anne pickup identity differs.");
        }
        Require(document.Pickups.Count(p => p.Kind == "hidden") == 5, "S.S. Anne hidden-item scope differs.");
        return document;
    }

    public static PretSsAnneItemsDocument LoadProject(string root)
    {
        _ = Read(root, "scripts/ssanne-items.lock.json", LockSha256);
        _ = Read(root, "content/raw/ssanne-items/manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/ssanne-items/manifest.json", ManifestSha256));
        foreach (var output in manifest.RootElement.GetProperty("outputs").EnumerateArray())
            _ = Read(root, "content/generated/ssanne-items/" + output.GetProperty("path").GetString(),
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        var document = Load(Utf8.GetString(Read(root,
            "content/generated/ssanne-items/ssanne-items.json", GeneratedSha256)));
        foreach (var input in document.Inputs)
            Input(root, "content/raw/ssanne-items/", input.Path, input.ByteLength, input.GitBlobSha1, input.Sha256);
        return document;
    }

    /// <summary>Appends only ship additions to the authenticated four-Mart Vermilion catalog.</summary>
    public static EarlyShopCatalog LoadShopProject(string root, EarlyShopCatalog vermilionShop)
    {
        VermilionItemRules.ValidateShop(vermilionShop);
        var document = LoadProject(root);
        string[] added = ["STARDUST", "X_ATTACK", "GREAT_BALL", "HYPER_POTION", "LAVA_COOKIE", "CHERI_BERRY", "CHESTO_BERRY", "PECHA_BERRY", "BERRY_POUCH"];
        var result = vermilionShop with
        {
            Profile = SsAnneItemRules.Profile,
            Items = vermilionShop.Items.AddRange(added.Select(s => Definition(document, new ItemId("pret:ITEM_" + s))!))
        };
        EarlyShopInventoryRules.ValidateCatalog(result);
        return result;
    }

    public static EarlyShopItemDefinition? Definition(PretSsAnneItemsDocument document, ItemId id)
    {
        var source = document.Items.Add(document.BerryPouch!).FirstOrDefault(i => i.Metadata.Item.Id == id);
        return source is null ? null : Definition(source);
    }
    public static EarlyShopItemDefinition Definition(PretBillSourceItem value)
    {
        var item = value.Metadata.Item;
        var use = item.SourceItemSymbol switch
        {
            "ITEM_ETHER" => EarlyShopUseSupport.FieldEther,
            "ITEM_SUPER_POTION" or "ITEM_HYPER_POTION" => EarlyShopUseSupport.Potion,
            "ITEM_CHERI_BERRY" or "ITEM_PECHA_BERRY" => EarlyShopUseSupport.StatusCure,
            "ITEM_CHESTO_BERRY" => EarlyShopUseSupport.DeferredSleep,
            "ITEM_GREAT_BALL" => EarlyShopUseSupport.SeparateCapture,
            _ => EarlyShopUseSupport.NoFieldUse
        };
        int heal = use == EarlyShopUseSupport.Potion ? item.HoldEffectParam : 0;
        uint cure = item.SourceItemSymbol == "ITEM_CHERI_BERRY" ? 64u : item.SourceItemSymbol == "ITEM_PECHA_BERRY" ? 8u : 0u;
        return new(item.Id, item.SourceItemSymbol, item.SourceItemId, item.Name, value.Metadata.Description,
            item.Price, item.Pocket, item.Importance, item.Type, item.FieldUseFunction, item.BattleUsage,
            item.BattleUseFunction, item.HoldEffectParam, heal, cure, value.EffectBytes, use,
            item.Source, value.EffectSource) { SourceMetadata = item };
    }

    private static void Input(string root, string prefix, string path, int length, string blob, string hash)
    {
        var bytes = Read(root, prefix + path, hash, length);
        Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob, "S.S. Anne item Git blob differs: " + path);
    }
    private static byte[] Read(string root, string path, string hash, int? length = null)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes && (length is null || info.Length == length), "S.S. Anne item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        Require(Hash(bytes) == hash, "S.S. Anne item file differs: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe S.S. Anne item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("S.S. Anne item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("S.S. Anne item symlink rejected.");
        }
        return result;
    }
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
