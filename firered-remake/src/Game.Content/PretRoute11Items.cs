using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretRoute11ItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups, ImmutableArray<string> Limitations);

/// <summary>Authenticates seven Route11 item declarations, icons and six source pickups.</summary>
public static class PretRoute11Items
{
    public const string Profile = "firered-route11-items-v1";
    public const string LockSha256 = "e0dff7a985548067c1ad7a55b653af1a001f7ca60827776bd1824ec3a58b4cf9";
    public const string GeneratedSha256 = "e123faaa476a334e60612cf73110db48b66f484bcc29ab9b3883f56e27acdd25";
    public const string ManifestSha256 = "164feca76909629e912e6f5057d0830fc5d5acd2128f0ed2281936fc62addd3d";
    public const string RawManifestSha256 = "33bb849d22a932171c6a9a2bcf1379bd1f0d36a4bdc736493a530120175cae04";
    public const string FullSourceHash = "d10a010f6935492bcf16e6af934d13272204899d7b70aa0b3622ce2ee73dc7ec";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretRoute11ItemsDocument Load(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "Route11 item document differs.");
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretRoute11ItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Profile == Profile &&
            document.SourceCommit == OpeningInventoryRules.SourceCommit && document.Inputs.Length == 28 && document.Items.Length == 7 && document.Icons.Length == 7 &&
            document.Pickups.Length == 6 &&
            !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "Route11 item scope differs.");
        var inputs = document.Inputs.ToDictionary(input => input.Path, StringComparer.Ordinal);
        Require(Hash(Utf8.GetBytes(string.Concat(document.Inputs.OrderBy(input => input.Path, StringComparer.Ordinal)
            .Select(input => input.Path + "\0" + input.Sha256 + "\n")))) == FullSourceHash,
            "Route11 item input identity differs.");
        void Source(OpeningItemSourceRef source) => Require(source is not null &&
            !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
            inputs.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256,
            "Route11 item source reference differs.");
        foreach (var source in document.RulesSources) Source(source);
        string[] items = ["AWAKENING", "ETHER", "ESCAPE_ROPE", "GREAT_BALL", "PARALYZE_HEAL", "X_DEFEND", "ASPEAR_BERRY"];
        var definitions = document.Items;
        for (int n = 0; n < definitions.Length; n++)
        {
            var item = definitions[n]; var icon = document.Icons[n];
            Require(item.Metadata.Item.Id.Value == "pret:ITEM_" + items[n] && icon.Item == item.Metadata.Item.Id,
                "Route11 item order or icon differs.");
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
                (pickup.Map is "Route11" or "Route2") && pickup.X >= 0 && pickup.Y >= 0 &&
                (pickup.Kind == "visible" ? pickup.LocalObjectId > 0 && pickup.Script is not null && pickup.ScriptSource is not null :
                pickup.Kind == "hidden" && pickup.LocalObjectId is null && pickup.Script is null && pickup.ScriptSource is null),
                "Route11 pickup identity differs.");
        }
        Require(document.Pickups.Count(p => p.Kind == "hidden") == 1, "Route11 hidden-item scope differs.");
        return document;
    }

    public static PretRoute11ItemsDocument LoadProject(string root)
    {
        _ = Read(root, "scripts/route11-items.lock.json", LockSha256);
        _ = Read(root, "content/raw/route11-items/manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/route11-items/manifest.json", ManifestSha256));
        foreach (var output in manifest.RootElement.GetProperty("outputs").EnumerateArray())
            _ = Read(root, "content/generated/route11-items/" + output.GetProperty("path").GetString(),
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        var document = Load(Utf8.GetString(Read(root,
            "content/generated/route11-items/route11-items.json", GeneratedSha256)));
        foreach (var input in document.Inputs)
            Input(root, "content/raw/route11-items/", input.Path, input.ByteLength, input.GitBlobSha1, input.Sha256);
        return document;
    }

    /// <summary>Appends X Defend and Aspear Berry to the authenticated thirty-item ship catalog.</summary>
    public static EarlyShopCatalog LoadShopProject(string root, EarlyShopCatalog vermilionShop)
    {
        SsAnneItemRules.ValidateShop(vermilionShop);
        var document = LoadProject(root);
        string[] added = ["X_DEFEND", "ASPEAR_BERRY"];
        var result = vermilionShop with
        {
            Profile = Route11ItemRules.Profile,
            Items = vermilionShop.Items.AddRange(added.Select(s => Definition(document, new ItemId("pret:ITEM_" + s))!))
        };
        EarlyShopInventoryRules.ValidateCatalog(result);
        return result;
    }

    public static EarlyShopItemDefinition? Definition(PretRoute11ItemsDocument document, ItemId id)
    {
        var source = document.Items.FirstOrDefault(i => i.Metadata.Item.Id == id);
        return source is null ? null : Definition(source);
    }
    public static EarlyShopItemDefinition Definition(PretBillSourceItem value)
    {
        var item = value.Metadata.Item;
        var use = item.SourceItemSymbol switch
        {
            "ITEM_ETHER" => EarlyShopUseSupport.FieldEther,
            "ITEM_SUPER_POTION" or "ITEM_HYPER_POTION" => EarlyShopUseSupport.Potion,
            "ITEM_CHERI_BERRY" or "ITEM_PECHA_BERRY" or "ITEM_ASPEAR_BERRY" or "ITEM_PARALYZE_HEAL" => EarlyShopUseSupport.StatusCure,
            "ITEM_CHESTO_BERRY" or "ITEM_AWAKENING" => EarlyShopUseSupport.DeferredSleep,
            "ITEM_GREAT_BALL" => EarlyShopUseSupport.SeparateCapture,
            _ => EarlyShopUseSupport.NoFieldUse
        };
        int heal = use == EarlyShopUseSupport.Potion ? item.HoldEffectParam : 0;
        uint cure = item.SourceItemSymbol is "ITEM_CHERI_BERRY" or "ITEM_PARALYZE_HEAL" ? 64u : item.SourceItemSymbol == "ITEM_PECHA_BERRY" ? 8u : item.SourceItemSymbol == "ITEM_ASPEAR_BERRY" ? 32u : 0u;
        return new(item.Id, item.SourceItemSymbol, item.SourceItemId, item.Name, value.Metadata.Description,
            item.Price, item.Pocket, item.Importance, item.Type, item.FieldUseFunction, item.BattleUsage,
            item.BattleUseFunction, item.HoldEffectParam, heal, cure, value.EffectBytes, use,
            item.Source, value.EffectSource) { SourceMetadata = item };
    }

    private static void Input(string root, string prefix, string path, int length, string blob, string hash)
    {
        var bytes = Read(root, prefix + path, hash, length);
        Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob, "Route11 item Git blob differs: " + path);
    }
    private static byte[] Read(string root, string path, string hash, int? length = null)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes && (length is null || info.Length == length), "Route11 item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        Require(Hash(bytes) == hash, "Route11 item file differs: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Route11 item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Route11 item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Route11 item symlink rejected.");
        }
        return result;
    }
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
