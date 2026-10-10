using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public static class EarlyShopMedicineRules
{
    /// <summary>The caller must atomically synchronize active battle status/stats and resolve the enemy turn.
    /// This does not implement command/UI routing. Awakening admits source sleep only on the explicit early-starter content; toxic and unrelated field items stay unsupported.</summary>
    public static EarlyMedicineUseResult Use(EarlyShopCatalog catalog, OpeningItemsCatalog opening,
        CampaignPokemonContent pokemonContent, InventoryState inventory, PartyPokemon pokemon,
        ItemId item, PotionUseContext context)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(pokemon);
        EarlyMedicineUseResult Fail(string error) => new(inventory, pokemon, false, 0, 0, 0, false, error);
        var error = EarlyShopInventoryRules.Validate(catalog, inventory);
        if (error is not null) return Fail(error);
        if (!Enum.IsDefined(context)) return Fail("medicine_context_unsupported");
        error = PokemonStateValidator.ValidateParty(pokemonContent, pokemon);
        if (error is not null) return Fail(error);
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        bool awakening=pokemonContent.AllowSleepStatus&&(definition?.SourceItemSymbol=="ITEM_AWAKENING" || catalog.Profile is (SsAnneItemRules.Profile or Route11ItemRules.Profile) && definition?.SourceItemSymbol=="ITEM_CHESTO_BERRY")&&definition.UseSupport==EarlyShopUseSupport.DeferredSleep;
        if (definition is null || !awakening&&definition.UseSupport is not (EarlyShopUseSupport.Potion or EarlyShopUseSupport.StatusCure))
            return Fail("medicine_effect_unsupported");
        int count = OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory, definition), item);
        if (count == 0) return Fail("medicine_not_in_bag");
        if ((catalog.Profile is CeruleanItemRules.Profile or BillItemRules.Profile or VermilionItemRules.Profile or SsAnneItemRules.Profile or Route11ItemRules.Profile) && item == CeruleanItemRules.SuperPotionId || catalog.Profile is (SsAnneItemRules.Profile or Route11ItemRules.Profile) && item == SsAnneItemRules.HyperPotionId)
        {
            var target = pokemon.Pokemon;
            if (target.CurrentHp == 0 || target.CurrentHp == target.Stats.MaxHp)
                return Fail("medicine_no_effect");
            int restored = Math.Min(definition.HealHp, target.Stats.MaxHp - target.CurrentHp);
            return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1),
                pokemon with { Pokemon = target with { CurrentHp = target.CurrentHp + restored } },
                true, restored, 0, 1, context == PotionUseContext.Battle, null);
        }
        if (definition.UseSupport == EarlyShopUseSupport.Potion)
        {
            // Reuse the exact legacy healing engine through its own restricted view. The full source
            // inventory above is already validated; only the returned HP and Potion count are committed.
            var potionView = inventory with
            {
                BagItems = inventory.BagItems.Where(s => s.Item == OpeningInventoryRules.PotionId).ToImmutableArray(),
                PcItems = inventory.PcItems.Where(s => s.Item == OpeningInventoryRules.PotionId).ToImmutableArray()
            };
            var result = OpeningPotionRules.UseOnParty(opening, pokemonContent, potionView, pokemon, context);
            if (!result.Succeeded) return Fail(result.Error!);
            return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1), result.Pokemon,
                true, result.HpRestored, 0, 1, result.ConsumesBattleTurn, null);
        }
        // Only the already-supported, standalone status1 value may be cured. Never strip unknown bits.
        if (awakening ? !Game.Domain.WildBattle.WildBattleStatus.IsSleeping(pokemon.Status) : pokemon.Status != definition.CureStatus) return Fail("medicine_no_effect");
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1), pokemon with { Status = 0 },
            true, 0, pokemon.Status, 1, context == PotionUseContext.Battle, null);
    }
}
