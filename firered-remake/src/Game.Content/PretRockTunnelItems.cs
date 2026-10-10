using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretRockTunnelItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups, ImmutableArray<string> Limitations);

/// <summary>Authenticates seven RockTunnel item declarations, icons and fifteen source pickups.</summary>
public static class PretRockTunnelItems
{
    public const string Profile = "firered-rocktunnel-items-v1";
    public const string LockSha256 = "c99287370abebd3548605abec13caefaaa0d7678da5f6988612731fc34389616";
    public const string GeneratedSha256 = "0407ecaa41200a8b998da5f6ee2749217615d03271fa17c9582881519bf7c174";
    public const string ManifestSha256 = "a0079b7231bf86638f7f10aa62b157315fdaa1b815e4096bf5e73f924d9ce0d5";
    public const string RawManifestSha256 = "44eb4a61e9c067fbe47c1a51ad76caa66c26d234576954d51efa264ca1247ccf";
    public const string FullSourceHash = "075b625277306e4dc9aba7df78fd3087c98cdb8f2b927461abe5655300bf7e34";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretRockTunnelItemsDocument Load(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "RockTunnel item document differs.");
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretRockTunnelItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Profile == Profile &&
            document.SourceCommit == OpeningInventoryRules.SourceCommit && document.Inputs.Length == 53 && document.Items.Length == 7 && document.Icons.Length == 7 &&
            document.Pickups.Length == 15 &&
            !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "RockTunnel item scope differs.");
        var inputs = document.Inputs.ToDictionary(input => input.Path, StringComparer.Ordinal);
        Require(Hash(Utf8.GetBytes(string.Concat(document.Inputs.OrderBy(input => input.Path, StringComparer.Ordinal)
            .Select(input => input.Path + "\0" + input.Sha256 + "\n")))) == FullSourceHash,
            "RockTunnel item input identity differs.");
        void Source(OpeningItemSourceRef source) => Require(source is not null &&
            !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
            inputs.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256,
            "RockTunnel item source reference differs.");
        foreach (var source in document.RulesSources) Source(source);
        string[] items = ["MAX_ETHER", "PERSIM_BERRY", "NANAB_BERRY", "REVIVE", "EVERSTONE", "PEARL", "HARD_STONE"];
        var definitions = document.Items;
        for (int n = 0; n < definitions.Length; n++)
        {
            var item = definitions[n]; var icon = document.Icons[n];
            Require(item.Metadata.Item.Id.Value == "pret:ITEM_" + items[n] && icon.Item == item.Metadata.Item.Id,
                "RockTunnel item order or icon differs.");
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
                (pickup.Map is "Route9" or "Route10" or "RockTunnel_1F" or "RockTunnel_B1F") && pickup.X >= 0 && pickup.Y >= 0 &&
                (pickup.Kind == "visible" ? pickup.LocalObjectId > 0 && pickup.Script is not null && pickup.ScriptSource is not null :
                pickup.Kind == "hidden" && pickup.LocalObjectId is null && pickup.Script is null && pickup.ScriptSource is null),
                "RockTunnel pickup identity differs.");
        }
        Require(document.Pickups.Count(p => p.Kind == "hidden") == 8, "RockTunnel hidden-item scope differs.");
        return document;
    }

    public static PretRockTunnelItemsDocument LoadProject(string root)
    {
        _ = Read(root, "scripts/rocktunnel-items.lock.json", LockSha256);
        _ = Read(root, "content/raw/rocktunnel-items/manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/rocktunnel-items/manifest.json", ManifestSha256));
        foreach (var output in manifest.RootElement.GetProperty("outputs").EnumerateArray())
            _ = Read(root, "content/generated/rocktunnel-items/" + output.GetProperty("path").GetString(),
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        var document = Load(Utf8.GetString(Read(root,
            "content/generated/rocktunnel-items/rocktunnel-items.json", GeneratedSha256)));
        foreach (var input in document.Inputs)
            Input(root, "content/raw/rocktunnel-items/", input.Path, input.ByteLength, input.GitBlobSha1, input.Sha256);
        return document;
    }

    /// <summary>Preserves all32 inherited definitions, including Revive; adds six new unique IDs.</summary>
    public static EarlyShopCatalog LoadShopProject(string root, EarlyShopCatalog route11Shop)
    {
        Route11ItemRules.ValidateShop(route11Shop);
        var document = LoadProject(root);
        var sourceRevive = Definition(document, RockTunnelItemRules.ReviveId)!;
        var inheritedRevive = route11Shop.Items.Single(i => i.Id == RockTunnelItemRules.ReviveId);
        Require(inheritedRevive.EffectBytes.SequenceEqual(sourceRevive.EffectBytes) &&
            inheritedRevive with { EffectBytes = sourceRevive.EffectBytes } == sourceRevive,
            "RockTunnel changed inherited Revive metadata or active field support.");
        var result = route11Shop with { Profile = RockTunnelItemRules.Profile,
            Items = route11Shop.Items.AddRange(document.Items.Where(i => i.Metadata.Item.Id != RockTunnelItemRules.ReviveId).Select(PretSsAnneItems.Definition)) };
        EarlyShopInventoryRules.ValidateCatalog(result);
        return result;
    }
    public static EarlyShopItemDefinition? Definition(PretRockTunnelItemsDocument document, ItemId id)
    {
        var source = document.Items.SingleOrDefault(i => i.Metadata.Item.Id == id);
        if (source is null) return null;
        var definition = PretSsAnneItems.Definition(source);
        return id == RockTunnelItemRules.ReviveId ? definition with { UseSupport = EarlyShopUseSupport.FieldRevive } : definition;
    }

    private static void Input(string root, string prefix, string path, int length, string blob, string hash)
    {
        var bytes = Read(root, prefix + path, hash, length);
        Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob, "RockTunnel item Git blob differs: " + path);
    }
    private static byte[] Read(string root, string path, string hash, int? length = null)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes && (length is null || info.Length == length), "RockTunnel item file length differs: " + path);
        var bytes = PretRuntimeMapValidation.ReadBounded(p, length ?? ContentJson.MaximumJsonBytes);
        Require(length is null || bytes.Length == length, "RockTunnel item read length differs.");
        Require(Hash(bytes) == hash, "RockTunnel item file differs: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe RockTunnel item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("RockTunnel item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("RockTunnel item symlink rejected.");
        }
        return result;
    }
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
