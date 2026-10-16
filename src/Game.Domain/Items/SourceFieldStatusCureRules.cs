using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>A display capability, not an executable authorization token. Use resolves its own proof.</summary>
public sealed record SourceFieldStatusCure(ItemId Item, uint PermanentStatusMask);

/// <summary>
/// Pure, consumable field status cures over existing authenticated shop admission. Historical
/// UseSupport/effect snapshots are never changed; the separate exact source table supplies effects.
/// No HP, PP, friendship, stats, held effects, battle turn or RNG ownership.
/// </summary>
public static class SourceFieldStatusCureRules
{
    public static bool Supports(EarlyShopCatalog catalog, SourceBattleItemCatalog sourceItems, ItemId item) =>
        Resolve(catalog, sourceItems, item) is not null;

    public static SourceFieldStatusCure? Resolve(EarlyShopCatalog catalog, SourceBattleItemCatalog sourceItems, ItemId item)
    {
        if (catalog is null || sourceItems is null || catalog.Items.IsDefaultOrEmpty || catalog.Shops.IsDefault ||
            catalog.Items.Any(i => i is null || i.EffectBytes.IsDefault)) return null;
        try { EarlyShopInventoryRules.ValidateCatalog(catalog); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { return null; }
        var admitted = EarlyShopInventoryRules.Find(catalog, item);
        var source = sourceItems.Find(item);
        if (admitted is null || source is null || !SourceBattleItemCatalog.SupportsFixedEffect(source) ||
            source.Reusable || admitted.SourceItemSymbol != source.Symbol || admitted.SourceItemId != source.SourceItemId ||
            admitted.Name != source.Name || admitted.Pocket != source.Pocket ||
            admitted.Pocket is not ("POCKET_ITEMS" or "POCKET_BERRY_POUCH") ||
            admitted.Type != "ITEM_TYPE_PARTY_MENU" || admitted.FieldUseFunction != "FieldUseFunc_Medicine" ||
            admitted.BattleUsage != source.BattleUsage || admitted.BattleUseFunction != "BattleUseFunc_Medicine" ||
            admitted.BattleUseFunction != source.BattleUseFunction || admitted.HoldEffectParam != source.HoldEffectParam)
            return null;
        var effect = source.EffectBytes;
        // Confusion is a live battle condition, so ITEM3_CONFUSION is harmless in this field view.
        // Requiring all other bytes to be empty excludes combination HP/PP/stat/friendship items.
        if (effect.Length != 6 || effect[0] != 0 || effect[1] != 0 || effect[2] != 0 ||
            (effect[3] & ~0x3f) != 0 || effect[4] != 0 || effect[5] != 0) return null;
        uint mask = SourceItemStatusCure.Mask(effect[3]);
        if (mask == 0) return null;
        if (!admitted.EffectBytes.IsEmpty && !admitted.EffectBytes.SequenceEqual(effect.Select(b => (int)b))) return null;
        return new(item, mask);
    }

    public static EarlyMedicineUseResult Use(EarlyShopCatalog catalog, SourceBattleItemCatalog sourceItems,
        CampaignPokemonContent pokemonContent, InventoryState inventory, PartyPokemon pokemon, ItemId item)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(pokemon);
        EarlyMedicineUseResult Fail(string error) => new(inventory, pokemon, false, 0, 0, 0, false, error);
        var capability = Resolve(catalog, sourceItems, item);
        if (capability is null) return Fail("medicine_effect_unsupported");
        if (EarlyShopInventoryRules.Validate(catalog, inventory) is { } inventoryError) return Fail(inventoryError);
        if (SourceBattleInventoryRules.Validate(sourceItems, inventory) is { } sourceInventoryError) return Fail(sourceInventoryError);
        if (PokemonStateValidator.ValidateParty(pokemonContent, pokemon) is { } pokemonError) return Fail(pokemonError);
        var definition = EarlyShopInventoryRules.Find(catalog, item)!;
        int count = OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory, definition), item);
        if (count == 0) return Fail("medicine_not_in_bag");
        uint cured = pokemon.Status & capability.PermanentStatusMask;
        if (cured == 0) return Fail("medicine_no_effect");
        // Source HealStatusConditions has no HP gate: HP0 status is cured without reviving the target.
        // Egg selection, identity and persistent status admission are checked above before one debit.
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1),
            pokemon with { Status = pokemon.Status & ~capability.PermanentStatusMask }, true, 0, cured, 1, false, null);
    }
}
