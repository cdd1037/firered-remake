using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCeruleanItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    PretMtMoonItem Item, EarlyShopStock Shop, PretPewterMachineIcon Icon, ImmutableArray<string> Limitations);

/// <summary>Authenticates original source, the generated data and palette-derived Super Potion art.</summary>
public static class PretCeruleanItems
{
    public const string Profile = "firered-cerulean-items-v1";
    public const string LockSha256 = "f0068062f99c125a3b07f34453ae88b2158379eadbbe016edbd3f71dbd3f6744";
    public const string GeneratedSha256 = "dad705fe2a360618b4bc330cc296dff18bb366a4abb2b18feb4866c718283616";
    public const string ManifestSha256 = "04a28a231a4e34fc61d70a3bf9bdd122de6b0b2ed16a7e842fe62319bc2629f5";
    public const string RawManifestSha256 = "acc31c52834f3939b16cc3e1ba5be5b1faaab089b8eb82e1e7be56df3f1a9084";
    public const string FullSourceHash = "01cd8d5caac1e6741f33efed327f2ef82b0dc0e759842a90e6cf2e5b2ca50aa4";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretCeruleanItemsDocument Load(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "Cerulean item document differs.");
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretCeruleanItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Profile == Profile &&
            document.SourceCommit == OpeningInventoryRules.SourceCommit && document.Inputs.Length == 14 &&
            !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "Cerulean item scope differs.");
        var inputs = document.Inputs.ToDictionary(input => input.Path, StringComparer.Ordinal);
        Require(Hash(Utf8.GetBytes(string.Concat(document.Inputs.OrderBy(input => input.Path, StringComparer.Ordinal)
            .Select(input => input.Path + "\0" + input.Sha256 + "\n")))) == FullSourceHash,
            "Cerulean item input identity differs.");
        void Source(OpeningItemSourceRef source) => Require(source is not null &&
            !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
            inputs.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256,
            "Cerulean item source reference differs.");
        foreach (var source in document.RulesSources) Source(source);
        Source(document.Item.Metadata.Item.Source);
        Source(document.Item.Metadata.DescriptionSource);
        Require(document.Item.EffectSource is not null, "Cerulean item effect source missing.");
        Source(document.Item.EffectSource!);
        Source(document.Shop.Source);
        Source(document.Icon.Source);
        Source(document.Icon.PaletteSource);
        Source(document.Icon.BindingSource);
        CeruleanItemRules.ValidateSuperPotion(Definition(document.Item));
        CeruleanItemRules.ValidateStock(document.Shop);
        Require(document.Icon.Item == CeruleanItemRules.SuperPotionId, "Cerulean item icon identity differs.");
        _ = PretPewterMachines.IconRgba(document.Icon);
        return document;
    }

    public static PretCeruleanItemsDocument LoadProject(string root)
    {
        _ = Read(root, "scripts/cerulean-items.lock.json", LockSha256);
        _ = Read(root, "content/raw/cerulean-items/manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/cerulean-items/manifest.json", ManifestSha256));
        foreach (var output in manifest.RootElement.GetProperty("outputs").EnumerateArray())
            _ = Read(root, "content/generated/cerulean-items/" + output.GetProperty("path").GetString(),
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        var document = Load(Utf8.GetString(Read(root,
            "content/generated/cerulean-items/cerulean-items.json", GeneratedSha256)));
        foreach (var input in document.Inputs)
            Input(root, "content/raw/cerulean-items/", input.Path, input.ByteLength, input.GitBlobSha1, input.Sha256);
        return document;
    }

    public static EarlyShopCatalog LoadShopProject(string root)
    {
        var document = LoadProject(root);
        var old = PretMtMoonItems.LoadShopProject(root);
        var result = old with
        {
            Profile = CeruleanItemRules.Profile,
            Items = old.Items.Add(Definition(document.Item)),
            Shops = old.Shops.Add(document.Shop)
        };
        EarlyShopInventoryRules.ValidateCatalog(result);
        return result;
    }

    private static EarlyShopItemDefinition Definition(PretMtMoonItem value)
    {
        var item = value.Metadata.Item;
        return new(item.Id, item.SourceItemSymbol, item.SourceItemId, item.Name, value.Metadata.Description,
            item.Price, item.Pocket, item.Importance, item.Type, item.FieldUseFunction, item.BattleUsage,
            item.BattleUseFunction, item.HoldEffectParam, 50, 0, value.EffectBytes, value.UseSupport,
            item.Source, value.EffectSource) { SourceMetadata = item };
    }
    private static void Input(string root, string prefix, string path, int length, string blob, string hash)
    {
        var bytes = Read(root, prefix + path, hash, length);
        Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob, "Cerulean item Git blob differs: " + path);
    }
    private static byte[] Read(string root, string path, string hash, int? length = null)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes && (length is null || info.Length == length), "Cerulean item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        Require(Hash(bytes) == hash, "Cerulean item file differs: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Cerulean item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Cerulean item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Cerulean item symlink rejected.");
        }
        return result;
    }
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
