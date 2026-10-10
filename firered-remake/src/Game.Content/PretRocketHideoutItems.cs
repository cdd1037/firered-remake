using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;
namespace Game.Content;

public sealed record PretRocketHideoutItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons, ImmutableArray<string> Limitations);
public sealed record PretRocketHideoutPickup(string Map, string Kind, int? LocalId, int? EventIndex,
    int X, int Y, int Elevation, string Flag, int SourceFlagId, bool InitiallyHidden, bool Underfoot,
    ItemId Item, int Quantity, string? Script, string? ScriptCommand, OpeningItemSourceRef MapSource,
    OpeningItemSourceRef? ScriptSource);
public sealed record PretRocketHideoutPickupsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretRocketHideoutPickup> Pickups, ImmutableArray<string> Limitations);

public static class PretRocketHideoutItems
{
    public const string Profile = RocketHideoutItemRules.Profile;
    public const string LockSha256 = "c1663441cafb564854e7e671dca3c43480117c2b4aa7ac3b6fcefe81c7dd586b";
    public const string GeneratedSha256 = "d05a01abc57a9f2211fd5a2958cd3415a7a4e4e3d092601ac4fd01be56900ff7";
    public const string ManifestSha256 = "fef7b2f03b685f7894f05f11d074ada750a89787918e6936bb3d3cc0552ba20f";
    public const string PickupsSha256 = "739feb923de18a616bd2bbdf1aa8af3c4568511439bb4d37697104a064d006b1";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static void Require(bool condition, string message) => PretCeladonApproachSource.Require(condition,message);
    public static PretRocketHideoutItemsDocument Load(string json)
    {
        Require(PretCeladonApproachSource.Hash(Utf8.GetBytes(json)) == GeneratedSha256,"Rocket Hideout item document differs.");
        var options = ContentJson.CreateOptions(); options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false));
        var document = ContentJson.ParseStrict<PretRocketHideoutItemsDocument>(json,options);
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == OpeningInventoryRules.SourceCommit &&
            document.Inputs.Length == 41 && document.Items.Length == 10 && document.Icons.Length == 10 && !document.RulesSources.IsDefaultOrEmpty &&
            !document.Limitations.IsDefaultOrEmpty,"Rocket Hideout item scope differs.");
        var inputs = document.Inputs.ToDictionary(i => i.Path,StringComparer.Ordinal);
        void Source(OpeningItemSourceRef s) => ValidateSource(inputs,s);
        foreach (var s in document.RulesSources) Source(s);
        foreach (var item in document.Items)
        {
            RocketHideoutItemRules.ValidateSourceItem(item.Metadata,item.EffectBytes,item.EffectSource);
            Source(item.Metadata.Item.Source); Source(item.Metadata.DescriptionSource);
            if (item.EffectSource is not null) Source(item.EffectSource);
        }
        Require(document.Items.Select(i => i.Metadata.Item.Id).Distinct().Count() == 10,"Duplicate Rocket Hideout source items.");
        foreach (var icon in document.Icons)
        {
            Require(document.Items.Any(i => i.Metadata.Item.Id == icon.Item) && icon.PngPath == icon.Item.Value[5..].ToLowerInvariant()+".png","Rocket Hideout icon identity differs.");
            Source(icon.Source); Source(icon.PaletteSource); Source(icon.BindingSource); _ = PretPewterMachines.IconRgba(icon);
        }
        Require(document.Icons.Select(i => i.Item).Distinct().Count() == 10,"Duplicate Rocket Hideout icons.");
        return document;
    }
    internal static void ValidateSource(IReadOnlyDictionary<string,PretPewterMachineInput> inputs, OpeningItemSourceRef s) =>
        Require(s is not null && !string.IsNullOrWhiteSpace(s.Symbol) && s.StartLine > 0 && s.EndLine >= s.StartLine &&
            inputs.TryGetValue(s.Path,out var input) && input.Sha256 == s.Sha256,"Rocket Hideout item source reference differs.");
    public static PretRocketHideoutItemsDocument LoadProject(string root)
    {
        var source = PretCeladonApproachSource.Load(root,"rocket-hideout-items",Profile,LockSha256,41);
        var outputs = PretCeladonApproachSource.Outputs(root,"rocket-hideout-items",Profile,ManifestSha256,LockSha256,41);
        Require(outputs.Count == 13,"Rocket Hideout item output set differs.");
        var document = Load(Utf8.GetString(outputs["rocket-hideout-items.json"]));
        Require(document.Inputs.Select(f => (f.Path,f.Sha256,f.GitBlobSha1,f.ByteLength)).SequenceEqual(source.Files.Select(f => (f.Path,f.Sha256,f.GitBlobSha1,f.ByteLength))),"Rocket Hideout source inventory differs.");
        foreach (var icon in document.Icons) Require(PretCeladonApproachSource.Hash(outputs[icon.PngPath]) == icon.PngSha256,"Rocket Hideout item texture differs.");
        return document;
    }
    public static EarlyShopCatalog LoadShopProject(string root, EarlyShopCatalog inherited)
    {
        CeladonGymItemRules.ValidateShop(inherited); var document = LoadProject(root);
        var result = inherited with { Profile = Profile, Items = inherited.Items.AddRange(document.Items.Select(Definition)) };
        RocketHideoutItemRules.ValidateShop(result); return result;
    }
    private static EarlyShopItemDefinition Definition(PretBillSourceItem item)
    {
        var result = PretSsAnneItems.Definition(item);
        return result.Id == RocketHideoutItemRules.NetBallId || result.Id == RocketHideoutItemRules.NestBallId ?
            result with { UseSupport = EarlyShopUseSupport.SeparateCapture } : result;
    }
    public static EarlyShopItemDefinition? Definition(PretRocketHideoutItemsDocument document, ItemId id)
    {
        var item = document.Items.SingleOrDefault(i => i.Metadata.Item.Id == id); return item is null ? null : Definition(item);
    }
    public static PretRocketHideoutPickupsDocument LoadPickupsProject(string root)
    {
        var items = LoadProject(root);
        var bytes = PretCeladonApproachSource.Read(root,"content/generated/rocket-hideout-items/rocket-hideout-pickups.json",PickupsSha256);
        var document = ContentJson.ParseStrict<PretRocketHideoutPickupsDocument>(Utf8.GetString(bytes),ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == OpeningInventoryRules.SourceCommit &&
            document.Pickups.Length == 18 && document.Pickups.Count(p => p.Kind == "visible") == 14 && document.Pickups.Count(p => p.Kind == "hidden") == 4 &&
            document.Pickups.Count(p => p.InitiallyHidden) == 2 && document.Pickups.Select(p => p.Flag).Distinct().Count() == 18 && !document.Limitations.IsDefaultOrEmpty,"Rocket Hideout pickup scope differs.");
        var inputs = items.Inputs.ToDictionary(i => i.Path,StringComparer.Ordinal);
        foreach (var pickup in document.Pickups)
        {
            Require(pickup.Quantity == 1 && pickup.Map is "MAP_ROCKET_HIDEOUT_B1F" or "MAP_ROCKET_HIDEOUT_B2F" or "MAP_ROCKET_HIDEOUT_B3F" or "MAP_ROCKET_HIDEOUT_B4F" &&
                pickup.X >= 0 && pickup.Y >= 0 && pickup.Elevation is 0 or 3 && !pickup.Underfoot && pickup.SourceFlagId > 0,"Rocket Hideout pickup shape differs.");
            Require(pickup.Kind == "visible" ? pickup.LocalId is > 0 && pickup.EventIndex is null && pickup.Script is not null && pickup.ScriptCommand is "finditem" or "giveitem" && pickup.ScriptSource is not null :
                pickup.LocalId is null && pickup.EventIndex is >= 0 && pickup.Script is null && pickup.ScriptCommand is null && pickup.ScriptSource is null,"Rocket Hideout pickup event differs.");
            ValidateSource(inputs,pickup.MapSource); if (pickup.ScriptSource is not null) ValidateSource(inputs,pickup.ScriptSource);
            Require(pickup.InitiallyHidden == (pickup.Item == RocketHideoutItemRules.LiftKeyId || pickup.Item == RocketHideoutItemRules.SilphScopeId),"Rocket key visibility differs.");
        }
        return document;
    }
}
