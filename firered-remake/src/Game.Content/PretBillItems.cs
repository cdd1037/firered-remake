using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretBillSourceItem(PewterMachineItem Metadata, ImmutableArray<int> EffectBytes,
    OpeningItemSourceRef? EffectSource);
public sealed record PretBillDexView(SpeciesId Species, int SourceSpeciesId, int NationalDex, string Name,
    int X, int Y, int Width, int Height, bool MarksSeen, bool MarksOwned, string RgbaBase64,
    string RgbaSha256, string PngPath, string PngSha256, OpeningItemSourceRef Source,
    OpeningItemSourceRef PaletteSource, OpeningItemSourceRef ScriptSource, OpeningItemSourceRef NameSource);
public sealed record PretBillItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups, ImmutableArray<PretMtMoonFossil> Rewards,
    ImmutableArray<PretBillDexView> DexViews, ImmutableArray<string> Limitations);

/// <summary>Authenticates northern-route items, exact pickups and Bill's four seen-only species pictures.</summary>
public static class PretBillItems
{
    public const string Profile = "firered-bill-items-v1";
    public const string LockSha256 = "a08115eec2f78fe08c3328661f079d51506f1d6100b473fe786b15b01166a9b4";
    public const string GeneratedSha256 = "9c7bb80bb9de6f3051665c0ac2b5b7604d207e327a5b64d2667b5b5dcf114fbf";
    public const string ManifestSha256 = "5717efe6f025697c7b9d58455a2af5ad3d86fc566c77fb5b1830e96f522d31c9";
    public const string RawManifestSha256 = "9369a425234bcff492facbab40405369f8960a33baf5cd64a5c69d38695e3189";
    public const string FullSourceHash = "631324a07903bc883452ba65c11c1025932b56bab22dee3bf6ec09a08dad02d2";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretBillItemsDocument Load(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "Bill item document differs.");
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretBillItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Profile == Profile &&
            document.SourceCommit == OpeningInventoryRules.SourceCommit && document.Inputs.Length == 53 && document.Items.Length == 9 && document.Icons.Length == 9 &&
            document.Pickups.Length == 7 && document.Rewards.Length == 3 && document.DexViews.Length == 4 &&
            !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "Bill item scope differs.");
        var inputs = document.Inputs.ToDictionary(input => input.Path, StringComparer.Ordinal);
        Require(Hash(Utf8.GetBytes(string.Concat(document.Inputs.OrderBy(input => input.Path, StringComparer.Ordinal)
            .Select(input => input.Path + "\0" + input.Sha256 + "\n")))) == FullSourceHash,
            "Bill item input identity differs.");
        void Source(OpeningItemSourceRef source) => Require(source is not null &&
            !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
            inputs.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256,
            "Bill item source reference differs.");
        foreach (var source in document.RulesSources) Source(source);
        string[] items = ["NUGGET", "FAME_CHECKER", "SS_TICKET", "ELIXIR", "ETHER",
            "PECHA_BERRY", "ORAN_BERRY", "BLUK_BERRY", "BERRY_POUCH"];
        for (int n = 0; n < document.Items.Length; n++)
        {
            var item = document.Items[n]; var icon = document.Icons[n];
            Require(item.Metadata.Item.Id.Value == "pret:ITEM_" + items[n] &&
                icon.Item == item.Metadata.Item.Id, "Bill item order or icon differs.");
            Source(item.Metadata.Item.Source); Source(item.Metadata.DescriptionSource);
            if (item.EffectSource is not null) Source(item.EffectSource);
            Source(icon.Source); Source(icon.PaletteSource); Source(icon.BindingSource);
            _ = PretPewterMachines.IconRgba(icon);
        }
        foreach (var pickup in document.Pickups)
        {
            Source(pickup.Source);
            if (pickup.ScriptSource is not null) Source(pickup.ScriptSource);
            Require(pickup.Quantity == 1 && !pickup.Underfoot && pickup.Elevation == 3 &&
                (pickup.Map is "Route24" or "Route25") && pickup.X >= 0 && pickup.Y >= 0 &&
                (pickup.Kind == "visible" ? pickup.LocalObjectId > 0 && pickup.Script is not null && pickup.ScriptSource is not null :
                pickup.Kind == "hidden" && pickup.LocalObjectId is null && pickup.Script is null && pickup.ScriptSource is null),
                "Bill pickup identity differs.");
        }
        Require(document.Pickups.Count(p => p.Kind == "hidden") == 5, "Bill hidden-item scope differs.");
        foreach (var reward in document.Rewards) Source(reward.Source);
        string[] species = ["EEVEE", "FLAREON", "JOLTEON", "VAPOREON"];
        int[] national = [133, 136, 135, 134];
        for (int n = 0; n < document.DexViews.Length; n++)
        {
            var view = document.DexViews[n];
            Source(view.Source); Source(view.PaletteSource); Source(view.ScriptSource); Source(view.NameSource);
            Require(view.Species.Value == "pret:SPECIES_" + species[n] && view.Name == species[n] &&
                view.SourceSpeciesId == national[n] && view.NationalDex == national[n] &&
                view.X == 10 && view.Y == 3 && view.MarksSeen && !view.MarksOwned,
                "Bill seen-only species view differs.");
            _ = DexRgba(view);
        }
        return document;
    }

    public static PretBillItemsDocument LoadProject(string root)
    {
        _ = Read(root, "scripts/bill-items.lock.json", LockSha256);
        _ = Read(root, "content/raw/bill-items/manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/bill-items/manifest.json", ManifestSha256));
        foreach (var output in manifest.RootElement.GetProperty("outputs").EnumerateArray())
            _ = Read(root, "content/generated/bill-items/" + output.GetProperty("path").GetString(),
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        var document = Load(Utf8.GetString(Read(root,
            "content/generated/bill-items/bill-items.json", GeneratedSha256)));
        foreach (var input in document.Inputs)
            Input(root, "content/raw/bill-items/", input.Path, input.ByteLength, input.GitBlobSha1, input.Sha256);
        return document;
    }

    public static EarlyShopCatalog LoadShopProject(string root)
    {
        var document = LoadProject(root);
        var old = PretCeruleanItems.LoadShopProject(root);
        var result = old with
        {
            Profile = BillItemRules.Profile,
            Items = old.Items.AddRange(document.Items.Take(3).Select(Definition))
        };
        EarlyShopInventoryRules.ValidateCatalog(result);
        return result;
    }

    private static EarlyShopItemDefinition Definition(PretBillSourceItem value)
    {
        var item = value.Metadata.Item;
        return new(item.Id, item.SourceItemSymbol, item.SourceItemId, item.Name, value.Metadata.Description,
            item.Price, item.Pocket, item.Importance, item.Type, item.FieldUseFunction, item.BattleUsage,
            item.BattleUseFunction, item.HoldEffectParam, 0, 0, value.EffectBytes, EarlyShopUseSupport.NoFieldUse,
            item.Source, value.EffectSource) { SourceMetadata = item };
    }

    public static byte[] DexRgba(PretBillDexView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Require(view.Width == 64 && view.Height == 64 && view.RgbaBase64.Length <= 22000,
            "Bill species picture dimensions differ.");
        var bytes = Convert.FromBase64String(view.RgbaBase64);
        Require(bytes.Length == 64 * 64 * 4 && Hash(bytes) == view.RgbaSha256,
            "Bill species picture pixels differ.");
        return bytes;
    }
    private static void Input(string root, string prefix, string path, int length, string blob, string hash)
    {
        var bytes = Read(root, prefix + path, hash, length);
        Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob, "Bill item Git blob differs: " + path);
    }
    private static byte[] Read(string root, string path, string hash, int? length = null)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes && (length is null || info.Length == length), "Bill item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        Require(Hash(bytes) == hash, "Bill item file differs: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Bill item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Bill item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Bill item symlink rejected.");
        }
        return result;
    }
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
