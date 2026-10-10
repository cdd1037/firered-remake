using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Only the original TM19 reward item is added; every prior item and shop is unchanged.</summary>
public static class CeladonGymItemRules
{
    public const string Profile = "firered-celadon-gym-items-v1";
    public static ItemId Tm19Id { get; } = new("pret:ITEM_TM19");
    private static readonly QuestItemDefinition ExpectedItem = new(new("pret:ITEM_TM19"),"ITEM_TM19",307,"TM19",3000,0,0,"POCKET_TM_CASE","ITEM_TYPE_PARTY_MENU","HOLD_EFFECT_NONE",0,"NULL",0,"NULL",0,true,true,true,new("src/data/items.json","ITEM_TM19",4933,4949,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"));
    private const string Description = "A harsh attack that\nabsorbs half the\ndamage it inflicted\nto restore HP.";
    private const string DeclarationDescription = "The user strikes the foe with\ntentacles or roots, stealing the\ntarget's HP and healing itself.";
    private static readonly OpeningItemSourceRef DescriptionSource = new("src/move_descriptions.c","gMoveDescription_GigaDrain",205,205,"1d6227e39ba85d694822a6b3a158c36b8ac3313ccf9e8d3264c67199bebaea0b");
    public static void ValidateMachine(PewterMachineItem machine)
    {
        if (machine is null || machine.Item != ExpectedItem || machine.Description != Description ||
            machine.DeclarationDescription != DeclarationDescription || machine.DescriptionSource != DescriptionSource)
            throw new NotSupportedException("Exact source TM19 metadata required.");
    }
    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Items.IsDefault || catalog.Items.Length != 46 ||
            catalog.Items.Any(i => i is null) || catalog.Items.Select(i => i.Id).Distinct().Count() != 46)
            throw new NotSupportedException("Exact Celadon Gym 46-item profile required.");
        LavenderItemRules.ValidateShop(catalog with { Profile = LavenderItemRules.Profile, Items = catalog.Items.Take(45).ToImmutableArray() });
        var item = catalog.Items[45];
        if (item.Id != Tm19Id || item.SourceItemSymbol != ExpectedItem.SourceItemSymbol ||
            item.SourceItemId != ExpectedItem.SourceItemId || item.Name != ExpectedItem.Name || item.Description != Description ||
            item.Price != ExpectedItem.Price || item.Pocket != ExpectedItem.Pocket || item.Importance != ExpectedItem.Importance ||
            item.Type != ExpectedItem.Type || item.FieldUseFunction != ExpectedItem.FieldUseFunction ||
            item.BattleUsage != ExpectedItem.BattleUsage || item.BattleUseFunction != ExpectedItem.BattleUseFunction ||
            item.HoldEffectParam != 0 || item.HealHp != 0 || item.CureStatus != 0 || item.EffectBytes.IsDefault || !item.EffectBytes.IsEmpty ||
            item.UseSupport != EarlyShopUseSupport.NoFieldUse || item.Source != ExpectedItem.Source || item.EffectSource is not null ||
            item.SourceMetadata != ExpectedItem)
            throw new NotSupportedException("Celadon Gym source item differs.");
    }
    public static bool IsBerry(ItemId item) => LavenderItemRules.IsBerry(item);
    public static string? ValidateBerries(InventoryState state) => LavenderItemRules.ValidateBerries(state);
}
