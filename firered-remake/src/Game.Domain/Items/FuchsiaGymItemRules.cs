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
    private static readonly QuestItemDefinition ExpectedItem = new(Tm06Id, "ITEM_TM06", 294, "TM06", 3000,
        0, 0, "POCKET_TM_CASE", "ITEM_TYPE_PARTY_MENU", "HOLD_EFFECT_NONE", 0, "NULL", 0, "NULL", 0,
        true, true, true, new("src/data/items.json", "ITEM_TM06", 4712, 4728));
    private const string Description = "A move that badly\npoisons the foe.\nIts poison damage\nworsens every turn.";
    private const string DeclarationDescription = "A move that leaves the foe badly\npoisoned. Its poison damage worsens\nevery turn.";
    private static readonly OpeningItemSourceRef DescriptionSource = new("src/move_descriptions.c",
        "gMoveDescription_Toxic", 95, 95);

    public static void ValidateMachine(PewterMachineItem machine)
    {
        if (machine is null || machine.Item != ExpectedItem || machine.Description != Description ||
            machine.DeclarationDescription != DeclarationDescription || machine.DescriptionSource != DescriptionSource)
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
        if (item.Id != Tm06Id || item.SourceItemSymbol != ExpectedItem.SourceItemSymbol ||
            item.SourceItemId != ExpectedItem.SourceItemId || item.Name != ExpectedItem.Name || item.Description != Description ||
            item.Price != ExpectedItem.Price || item.Pocket != ExpectedItem.Pocket || item.Importance != ExpectedItem.Importance ||
            item.Type != ExpectedItem.Type || item.FieldUseFunction != ExpectedItem.FieldUseFunction ||
            item.BattleUsage != ExpectedItem.BattleUsage || item.BattleUseFunction != ExpectedItem.BattleUseFunction ||
            item.HoldEffectParam != 0 || item.HealHp != 0 || item.CureStatus != 0 || item.EffectBytes.IsDefault || !item.EffectBytes.IsEmpty ||
            item.UseSupport != EarlyShopUseSupport.NoFieldUse || item.Source != ExpectedItem.Source || item.EffectSource is not null ||
            item.SourceMetadata != ExpectedItem)
            throw new NotSupportedException("fuchsia_gym_tm06_item_definition");
    }
}
