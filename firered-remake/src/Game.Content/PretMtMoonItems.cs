using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public sealed record PretMtMoonItem(PewterMachineItem Metadata, ImmutableArray<int> EffectBytes,
    OpeningItemSourceRef? EffectSource, EarlyShopUseSupport UseSupport);
public sealed record PretMtMoonPickup(string Map, string Kind, ItemId Item, int Quantity, int? LocalObjectId,
    int X, int Y, int Elevation, string Flag, bool Underfoot, string? Script, OpeningItemSourceRef Source,
    OpeningItemSourceRef? ScriptSource);
public sealed record PretMtMoonFossil(ItemId Item, string Script, OpeningItemSourceRef Source);
public sealed record PretMtMoonItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretMtMoonItem> Items, ImmutableArray<PewterAdditionalMachine> Machines,
    ImmutableArray<PewterMachineCompatibility> Tm39Compatibility, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups, ImmutableArray<PretMtMoonFossil> Fossils, ImmutableArray<string> Limitations);

/// <summary>All reviewed input, generated and icon bytes are authenticated before bounded composition.</summary>
public static class PretMtMoonItems
{
    public const string Profile = "firered-mtmoon-items-v1";
    public const string LockSha256 = "602d603294843e02b7daa03b23356aecd38ce30d79c1b1ad65725564079fa35a";
    public const string GeneratedSha256 = "ce611fe3fbce03b7790dc8a9cf9a3c0bb0056504d53a92505b64e4fd3e2e718c";
    public const string ManifestSha256 = "2c3a68278b51ab488b16d7d2e188d5e288b8f440617e7974f155c6696c7576a9";
    public const string FullSourceHash = "7ed97ab3bf4f9041329a0604db4b74adb17a651e7d928d6ed8bd54c3decf2ce3";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretMtMoonItemsDocument Load(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "MtMoon item document differs.");
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var d = ContentJson.ParseStrict<PretMtMoonItemsDocument>(json, options);
        Require(d.SchemaVersion == 1 && d.Profile == Profile && d.SourceCommit == OpeningInventoryRules.SourceCommit &&
            d.Inputs.Length == 45 && d.Items.Length == 12 && d.Machines.Length == 3 && d.Tm39Compatibility.Length == 56 &&
            d.Icons.Length == 12 && d.Pickups.Length == 19 && d.Fossils.Length == 2 && !d.Limitations.IsDefaultOrEmpty &&
            !d.RulesSources.IsDefaultOrEmpty, "MtMoon item scope differs.");
        var inputs = d.Inputs.ToDictionary(i => i.Path, StringComparer.Ordinal);
        Require(Hash(Utf8.GetBytes(string.Concat(d.Inputs.OrderBy(i => i.Path, StringComparer.Ordinal).Select(i => i.Path + "\0" + i.Sha256 + "\n")))) == FullSourceHash,
            "MtMoon item input identity differs.");
        void Source(OpeningItemSourceRef s)
            => Require(s is not null && !string.IsNullOrWhiteSpace(s.Symbol) && s.StartLine > 0 && s.EndLine >= s.StartLine &&
                inputs.TryGetValue(s.Path, out var i) && i.Sha256 == s.Sha256, "MtMoon item source reference differs.");
        foreach (var s in d.RulesSources) Source(s);
        foreach (var i in d.Items)
        {
            Source(i.Metadata.Item.Source); Source(i.Metadata.DescriptionSource);
            if (i.EffectSource is not null) Source(i.EffectSource);
        }
        foreach (var m in d.Machines)
        {
            Source(m.Machine.Item.Source); Source(m.Machine.DescriptionSource);
            foreach (var c in m.Compatibility) Source(c.Source);
        }
        foreach (var c in d.Tm39Compatibility) Source(c.Source);
        foreach (var i in d.Icons)
        {
            Source(i.Source); Source(i.PaletteSource); Source(i.BindingSource);
            _ = PretPewterMachines.IconRgba(i);
        }
        foreach (var p in d.Pickups)
        {
            Source(p.Source); if (p.ScriptSource is not null) Source(p.ScriptSource);
            Require(p.Quantity == 1 && !p.Underfoot && p.X >= 0 && p.Y >= 0 &&
                (p.Kind == "visible" ? p.LocalObjectId > 0 && p.Script is not null && p.ScriptSource is not null :
                p.Kind == "hidden" && p.LocalObjectId is null && p.Script is null && p.ScriptSource is null), "MtMoon pickup identity differs.");
        }
        Require(d.Pickups.Count(p => p.Map != "Route4") == 18 && d.Pickups.Count(p => p.Kind == "hidden") == 8 &&
            d.Pickups.Single(p => p.Map == "Route4").Item == new ItemId("pret:ITEM_TM05"), "MtMoon pickup coverage differs.");
        foreach (var f in d.Fossils) Source(f.Source);
        return d;
    }

    public static PretMtMoonItemsDocument LoadProject(string root)
    {
        _ = Read(root, "scripts/mtmoon-items.lock.json", LockSha256);
        _ = Read(root, "content/raw/mtmoon-items/manifest.json", "cb41e8025d2510f093747a68b970b1d824231747fe69ed7a31a65373c2dcfa12");
        var manifestBytes = Read(root, "content/generated/mtmoon-items/manifest.json", ManifestSha256);
        using var manifest = JsonDocument.Parse(manifestBytes);
        foreach (var o in manifest.RootElement.GetProperty("outputs").EnumerateArray())
            _ = Read(root, "content/generated/mtmoon-items/" + o.GetProperty("path").GetString(), o.GetProperty("sha256").GetString()!, o.GetProperty("byteLength").GetInt32());
        var d = Load(Utf8.GetString(Read(root, "content/generated/mtmoon-items/mtmoon-items.json", GeneratedSha256)));
        foreach (var i in d.Inputs) Input(root, "content/raw/mtmoon-items/", i.Path, i.ByteLength, i.GitBlobSha1, i.Sha256);
        return d;
    }

    public static EarlyShopCatalog LoadShopProject(string root)
    {
        var d = LoadProject(root);
        _ = Read(root, "scripts/early-shop.lock.json", PretEarlyShop.LockSha256);
        var old = PretEarlyShop.Load(Utf8.GetString(Read(root, "content/generated/early-shop/early-shop.json", PretEarlyShop.GeneratedSha256)));
        foreach (var i in old.Inputs) Input(root, "content/raw/early-shop/", i.Path, i.ByteLength, i.GitBlobSha1, i.Sha256);
        foreach (var icon in old.Icons) _ = Read(root, "content/generated/early-shop/" + icon.PngPath, icon.PngSha256);
        var items = d.Items.Take(9).Select(value =>
        {
            var i = value.Metadata.Item;
            return new EarlyShopItemDefinition(i.Id, i.SourceItemSymbol, i.SourceItemId, i.Name, value.Metadata.Description,
                i.Price, i.Pocket, i.Importance, i.Type, i.FieldUseFunction, i.BattleUsage, i.BattleUseFunction,
                i.HoldEffectParam, 0, 0, value.EffectBytes, value.UseSupport, i.Source, value.EffectSource) { SourceMetadata = i };
        });
        var result = old.Catalog with { Profile = EarlyShopInventoryRules.MtMoonProfile, Items = old.Catalog.Items.AddRange(items) };
        EarlyShopInventoryRules.ValidateCatalog(result);
        return result;
    }
    private static void Input(string root, string prefix, string path, int length, string blob, string hash)
    {
        var bytes = Read(root, prefix + path, hash, length);
        Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob, "MtMoon item Git blob differs: " + path);
    }
    private static byte[] Read(string root, string path, string hash, int? length = null)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes && (length is null || info.Length == length), "MtMoon item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        Require(Hash(bytes) == hash, "MtMoon item file differs: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe MtMoon item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("MtMoon item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("MtMoon item symlink rejected.");
        }
        return result;
    }
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
