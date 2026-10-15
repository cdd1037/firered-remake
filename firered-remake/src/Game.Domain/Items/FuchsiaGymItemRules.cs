using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Bounded TM06 reward inventory metadata; inherited items and shop stock stay unchanged.</summary>
public static class FuchsiaGymItemRules
{
    public const string Profile = "firered-fuchsia-gym-inventory-v1";
    public const int InheritedItemCount = PokemonTowerItemRules.ItemCount;
    public const int ItemCount = InheritedItemCount + 1;
    public static ItemId Tm06Id { get; } = new("pret:ITEM_TM06");
    private static bool IsSupportedItem(QuestItemDefinition? item) => item is not null &&
        item.Id == Tm06Id && item.SourceItemSymbol == "ITEM_TM06" && item.SourceItemId == 294 &&
        item.Name == "TM06" && item.Price == 3000 && item.Importance == 0 && item.Registrability == 0 &&
        item.Pocket == "POCKET_TM_CASE" && item.Type == "ITEM_TYPE_PARTY_MENU" &&
        item.HoldEffect == "HOLD_EFFECT_NONE" && item.HoldEffectParam == 0 && item.FieldUseFunction == "NULL" &&
        item.BattleUsage == 0 && item.BattleUseFunction == "NULL" && item.SecondaryId == 0 &&
        item.CanToss && item.CanSell && item.CanGive;

    public static void ValidateMachine(PewterMachineItem machine)
    {
        if (machine is null || !IsSupportedItem(machine.Item) || string.IsNullOrWhiteSpace(machine.Description))
            throw new NotSupportedException("fuchsia_gym_tm06_source_metadata");
    }

    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Items.IsDefault || catalog.Items.Length != ItemCount ||
            catalog.Items.Any(item => item is null) || catalog.Items.Select(item => item.Id).Distinct().Count() != ItemCount)
            throw new NotSupportedException("fuchsia_gym_item_catalog_scope");
        PokemonTowerItemRules.ValidateShop(catalog with { Profile = PokemonTowerItemRules.Profile,
            Items = catalog.Items.Take(InheritedItemCount).ToImmutableArray() });
        var item = catalog.Items[InheritedItemCount];
        if (item.Id != Tm06Id || item.SourceItemSymbol != "ITEM_TM06" ||
            item.SourceItemId != 294 || item.Name != "TM06" || string.IsNullOrWhiteSpace(item.Description) ||
            item.Price != 3000 || item.Pocket != "POCKET_TM_CASE" || item.Importance != 0 ||
            item.Type != "ITEM_TYPE_PARTY_MENU" || item.FieldUseFunction != "NULL" ||
            item.BattleUsage != 0 || item.BattleUseFunction != "NULL" ||
            item.HoldEffectParam != 0 || item.HealHp != 0 || item.CureStatus != 0 || item.EffectBytes.IsDefault || !item.EffectBytes.IsEmpty ||
            item.UseSupport != EarlyShopUseSupport.NoFieldUse || item.EffectSource is not null ||
            !IsSupportedItem(item.SourceMetadata))
            throw new NotSupportedException("fuchsia_gym_tm06_item_definition");
    }
}
