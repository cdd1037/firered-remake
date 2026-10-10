using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public sealed record PretPokemonTowerItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons, ImmutableArray<string> Limitations);
public sealed record PretPokemonTowerPickup(string Map, string Kind, int? LocalId, int? EventIndex,
    int X, int Y, int Elevation, string Flag, int SourceFlagId, bool InitiallyHidden, bool Underfoot,
    bool RequiresItemfinder, bool AllowsOrdinaryInteraction, ItemId Item, int Quantity, string? Script,
    string? ScriptCommand, OpeningItemSourceRef MapSource, OpeningItemSourceRef? ScriptSource);
public sealed record PretPokemonTowerPickupsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPokemonTowerPickup> Pickups, ImmutableArray<string> Limitations)
{
    /// <summary>Only these nine records may be registered with ordinary adjacent-A interaction.</summary>
    [JsonIgnore] public ImmutableArray<PretPokemonTowerPickup> OrdinaryPickups =>
        Pickups.Where(p => p.AllowsOrdinaryInteraction && !p.RequiresItemfinder && !p.Underfoot).ToImmutableArray();
}
public sealed record PretPokemonTowerFluteReward(ItemId Item, int Quantity, string Map, int LocalId,
    int X, int Y, int Elevation, string ActorHideFlag, int SourceActorHideFlagId, string ObtainedFlag,
    int SourceObtainedFlagId, string Fanfare, ImmutableArray<string> SourceOrder,
    OpeningItemSourceRef MapSource, OpeningItemSourceRef ScriptSource);
public sealed record PretPokemonTowerFluteContract(ItemId Item, bool Consumed, bool Registrable,
    bool FieldUsesAwakeningOnEntirePlayerParty, bool FieldChecksSoundproof, bool BattleTargetsBothParties,
    bool BattleChecksSoundproof, bool BattleClearsActiveNightmare, int SleepStatusMask, uint NightmareStatusMask,
    int BattleSleepingPartyStatusWrite, bool BattleConsumesAction, string Fanfare, bool FanfareOnlyWhenWaking,
    string FieldNoEffectMessage, string BattleNoEffectMessage, bool ExecutionImplemented,
    ImmutableArray<OpeningItemSourceRef> Sources);
public sealed record PretPokemonTowerUnderfootContract(ItemId Item, ItemId RequiredItem, int SourceRequiredItemId,
    string Map, int X, int Y, int Elevation, string Flag, int SourceFlagId, bool BlocksOrdinaryInteraction,
    bool RequiresExactPlayerTile, int ForcedQuantity, int OccupyingActorLocalId, string OccupyingActorHideFlag,
    int SourceOccupyingActorHideFlagId, bool HasExplicitRescueFlagCheck, bool SetPickupFlagOnlyAfterSuccessfulAdd,
    bool ExecutionImplemented, ImmutableArray<OpeningItemSourceRef> Sources);
public sealed record PretPokemonTowerItemUseNote(ItemId Item, string Status, ImmutableArray<string> Rules,
    ImmutableArray<OpeningItemSourceRef> Sources);
public sealed record PretPokemonTowerItemContractsDocument(int SchemaVersion, string Profile, string SourceCommit,
    PretPokemonTowerFluteReward FluteReward, PretPokemonTowerFluteContract PokeFlute,
    PretPokemonTowerUnderfootContract SootheBell, ImmutableArray<PretPokemonTowerItemUseNote> ItemUseNotes,
    ImmutableArray<string> Limitations);

/// <summary>Authenticates metadata and source contracts; does not enable any gameplay handler.</summary>
public static class PretPokemonTowerItems
{
    public const string Profile = PokemonTowerItemRules.Profile;
    public const string LockSha256 = "8605005350d51d4646abb8573071ef73a2549fb2f2d01a3f2be810b4943ea94a";
    public const string GeneratedSha256 = "7f5aec6c7cd10466da34aa00eb688ec52624d74adddd1c78e31910e0a340e399";
    public const string ManifestSha256 = "03d6e3a46790b0fa2eedf5e43b1b7b355076c99bdf866d774f6033c99ae0449d";
    public const string PickupsSha256 = "a5e2150903da5a4549682abf5847f55527d050dab0d68361969d7ec4ba83120e";
    public const string ContractsSha256 = "7acf906cc15abe1ac5a0877ccc7208316a1264bce2032c1ccce7d0e6d1bed57a";
    private const string Stem = "pokemon-tower-items";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static void Require(bool condition, string message) => PretCeladonApproachSource.Require(condition, message);
    private static void Source(IReadOnlyDictionary<string, PretPewterMachineInput> inputs, OpeningItemSourceRef s) =>
        PretRocketHideoutItems.ValidateSource(inputs, s);
    public static PretPokemonTowerItemsDocument Load(string json)
    {
        Require(PretCeladonApproachSource.Hash(Utf8.GetBytes(json)) == GeneratedSha256, "Pokemon Tower item document differs.");
        var options = ContentJson.CreateOptions(); options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretPokemonTowerItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == OpeningInventoryRules.SourceCommit &&
            document.Inputs.Length == 44 && document.Items.Length == 5 && document.Icons.Length == 5 &&
            !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty, "Pokemon Tower item scope differs.");
        var inputs = document.Inputs.ToDictionary(i => i.Path, StringComparer.Ordinal);
        foreach (var s in document.RulesSources) Source(inputs, s);
        foreach (var item in document.Items)
        {
            PokemonTowerItemRules.ValidateSourceItem(item.Metadata, item.EffectBytes, item.EffectSource);
            Source(inputs, item.Metadata.Item.Source); Source(inputs, item.Metadata.DescriptionSource);
            if (item.EffectSource is not null) Source(inputs, item.EffectSource);
        }
        Require(document.Items.Select(i => i.Metadata.Item.Id).Distinct().Count() == 5, "Duplicate Pokemon Tower source items.");
        foreach (var icon in document.Icons)
        {
            Require(document.Items.Any(i => i.Metadata.Item.Id == icon.Item) &&
                icon.PngPath == icon.Item.Value[5..].ToLowerInvariant() + ".png", "Pokemon Tower icon identity differs.");
            Source(inputs, icon.Source); Source(inputs, icon.PaletteSource); Source(inputs, icon.BindingSource);
            _ = PretPewterMachines.IconRgba(icon);
        }
        Require(document.Icons.Select(i => i.Item).Distinct().Count() == 5, "Duplicate Pokemon Tower icons.");
        return document;
    }
    public static PretPokemonTowerItemsDocument LoadProject(string root)
    {
        var source = PretCeladonApproachSource.Load(root, Stem, Profile, LockSha256, 44);
        var outputs = PretCeladonApproachSource.Outputs(root, Stem, Profile, ManifestSha256, LockSha256, 44);
        Require(outputs.Count == 8, "Pokemon Tower item output set differs.");
        var document = Load(Utf8.GetString(outputs["pokemon-tower-items.json"]));
        Require(document.Inputs.Select(i => (i.Path, i.Sha256, i.GitBlobSha1, i.ByteLength)).SequenceEqual(
            source.Files.Select(i => (i.Path, i.Sha256, i.GitBlobSha1, i.ByteLength))), "Pokemon Tower source inventory differs.");
        foreach (var icon in document.Icons)
            Require(PretCeladonApproachSource.Hash(outputs[icon.PngPath]) == icon.PngSha256, "Pokemon Tower item texture differs.");
        return document;
    }
    /// <summary>Preserves all prior 56 entries and all five stocks. Metadata validation is explicit; generic inventory admission is separate.</summary>
    public static EarlyShopCatalog LoadShopProject(string root, EarlyShopCatalog inherited)
    {
        RocketHideoutItemRules.ValidateShop(inherited); var document = LoadProject(root);
        var result = inherited with { Profile = Profile, Items = inherited.Items.AddRange(document.Items.Select(PretSsAnneItems.Definition)) };
        PokemonTowerItemRules.ValidateShop(result); return result;
    }
    public static EarlyShopItemDefinition? Definition(PretPokemonTowerItemsDocument document, ItemId id)
    {
        var source = document.Items.SingleOrDefault(i => i.Metadata.Item.Id == id);
        return source is null ? null : PretSsAnneItems.Definition(source);
    }
    public static ImmutableArray<string> SourceHashesProject(string root)
    {
        _ = LoadProject(root);
        return PretCeladonApproachSource.Load(root, Stem, Profile, LockSha256, 44).Hashes.AddRange(new[] {
            Stem + "-generated:" + GeneratedSha256, Stem + "-manifest:" + ManifestSha256,
            Stem + "-pickups:" + PickupsSha256, Stem + "-contracts:" + ContractsSha256 });
    }
    public static PretPokemonTowerPickupsDocument LoadPickupsProject(string root)
    {
        var items = LoadProject(root);
        var bytes = PretCeladonApproachSource.Read(root, "content/generated/" + Stem + "/pokemon-tower-pickups.json", PickupsSha256);
        var document = ContentJson.ParseStrict<PretPokemonTowerPickupsDocument>(Utf8.GetString(bytes), ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == OpeningInventoryRules.SourceCommit &&
            document.Pickups.Length == 10 && document.Pickups.Count(p => p.Kind == "visible") == 8 &&
            document.Pickups.Count(p => p.Kind == "hidden") == 2 && document.OrdinaryPickups.Length == 9 &&
            document.Pickups.Select(p => p.Flag).Distinct().Count() == 10 && !document.Limitations.IsDefaultOrEmpty,
            "Pokemon Tower pickup scope differs.");
        var inputs = items.Inputs.ToDictionary(i => i.Path, StringComparer.Ordinal);
        foreach (var pickup in document.Pickups)
        {
            Require(pickup.Quantity == 1 && pickup.Map is "MAP_POKEMON_TOWER_3F" or "MAP_POKEMON_TOWER_4F" or
                "MAP_POKEMON_TOWER_5F" or "MAP_POKEMON_TOWER_6F" or "MAP_POKEMON_TOWER_7F" &&
                pickup.X >= 0 && pickup.Y >= 0 && pickup.Elevation == 3 && !pickup.InitiallyHidden && pickup.SourceFlagId > 0,
                "Pokemon Tower pickup shape differs.");
            Require(pickup.Kind == "visible" ? pickup.LocalId is > 0 && pickup.EventIndex is null &&
                pickup.Script is not null && pickup.ScriptCommand == "finditem" && pickup.ScriptSource is not null :
                pickup.Kind == "hidden" && pickup.LocalId is null && pickup.EventIndex is >= 0 && pickup.Script is null &&
                pickup.ScriptCommand is null && pickup.ScriptSource is null, "Pokemon Tower pickup event differs.");
            bool special = pickup.Item == PokemonTowerItemRules.SootheBellId;
            Require(pickup.Underfoot == special && pickup.RequiresItemfinder == special && pickup.AllowsOrdinaryInteraction == !special,
                "Pokemon Tower Itemfinder-only pickup boundary differs.");
            if (special) Require(pickup.Map == "MAP_POKEMON_TOWER_7F" && pickup.X == 11 && pickup.Y == 4 &&
                pickup.EventIndex == 0 && pickup.Flag == "FLAG_HIDDEN_ITEM_POKEMON_TOWER_7F_SOOTHE_BELL" &&
                pickup.SourceFlagId == 1159, "Soothe Bell location differs.");
            Source(inputs, pickup.MapSource); if (pickup.ScriptSource is not null) Source(inputs, pickup.ScriptSource);
        }
        return document;
    }
    public static PretPokemonTowerItemContractsDocument LoadContractsProject(string root)
    {
        var items = LoadProject(root);
        var bytes = PretCeladonApproachSource.Read(root, "content/generated/" + Stem + "/pokemon-tower-item-contracts.json", ContractsSha256);
        var document = ContentJson.ParseStrict<PretPokemonTowerItemContractsDocument>(Utf8.GetString(bytes), ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == OpeningInventoryRules.SourceCommit &&
            document.ItemUseNotes.Length == 4 && !document.Limitations.IsDefaultOrEmpty, "Pokemon Tower item contract scope differs.");
        var inputs = items.Inputs.ToDictionary(i => i.Path, StringComparer.Ordinal);
        var reward = document.FluteReward;
        Require(reward.Item == PokemonTowerItemRules.PokeFluteId && reward.Quantity == 1 &&
            reward.Map == "MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE" && reward.LocalId == 1 &&
            reward.X == 3 && reward.Y == 3 && reward.Elevation == 3 && reward.ActorHideFlag == "FLAG_HIDE_POKEHOUSE_FUJI" &&
            reward.SourceActorHideFlagId == 53 && reward.ObtainedFlag == "FLAG_GOT_POKE_FLUTE" &&
            reward.SourceObtainedFlagId == 573 && reward.Fanfare == "MUS_OBTAIN_KEY_ITEM" && reward.SourceOrder.Length == 7,
            "Fuji home flute reward differs.");
        Source(inputs, reward.MapSource); Source(inputs, reward.ScriptSource);
        var flute = document.PokeFlute;
        Require(flute.Item == PokemonTowerItemRules.PokeFluteId && !flute.Consumed && !flute.Registrable &&
            flute.FieldUsesAwakeningOnEntirePlayerParty && !flute.FieldChecksSoundproof && flute.BattleTargetsBothParties &&
            flute.BattleChecksSoundproof && flute.BattleClearsActiveNightmare && flute.SleepStatusMask == 7 &&
            flute.NightmareStatusMask == 1u << 27 && flute.BattleSleepingPartyStatusWrite == 0 && flute.BattleConsumesAction &&
            flute.Fanfare == "MUS_POKE_FLUTE" && flute.FanfareOnlyWhenWaking && !flute.ExecutionImplemented &&
            !flute.Sources.IsDefaultOrEmpty, "Poke Flute source behavior contract differs.");
        foreach (var s in flute.Sources) Source(inputs, s);
        var bell = document.SootheBell;
        Require(bell.Item == PokemonTowerItemRules.SootheBellId && bell.RequiredItem == new ItemId("pret:ITEM_ITEMFINDER") &&
            bell.SourceRequiredItemId == 261 && bell.Map == "MAP_POKEMON_TOWER_7F" && bell.X == 11 && bell.Y == 4 &&
            bell.Elevation == 3 && bell.Flag == "FLAG_HIDDEN_ITEM_POKEMON_TOWER_7F_SOOTHE_BELL" && bell.SourceFlagId == 1159 &&
            bell.BlocksOrdinaryInteraction && bell.RequiresExactPlayerTile && bell.ForcedQuantity == 1 && bell.OccupyingActorLocalId == 1 &&
            bell.OccupyingActorHideFlag == "FLAG_HIDE_TOWER_FUJI" && bell.SourceOccupyingActorHideFlagId == 52 &&
            !bell.HasExplicitRescueFlagCheck && bell.SetPickupFlagOnlyAfterSuccessfulAdd && !bell.ExecutionImplemented &&
            !bell.Sources.IsDefaultOrEmpty, "Soothe Bell source pickup contract differs.");
        foreach (var s in bell.Sources) Source(inputs, s);
        foreach (var note in document.ItemUseNotes)
        {
            Require(items.Items.Any(i => i.Metadata.Item.Id == note.Item) && note.Status == "metadata-only" &&
                !note.Rules.IsDefaultOrEmpty && !note.Sources.IsDefaultOrEmpty, "Pokemon Tower item-use note differs.");
            foreach (var s in note.Sources) Source(inputs, s);
        }
        return document;
    }
}
