using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public static class EarlyShopMedicineRules
{
    /// <summary>The caller must atomically synchronize active battle status/stats and resolve the enemy turn.
    /// This does not implement command/UI routing. Sleep and canonical Toxic cures require their explicit content flags.</summary>
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
        if (pokemon.IsEgg) return Fail("medicine_egg_unsupported");
        var (definition, effect) = AuthenticatedFieldMedicine.Resolve(catalog, item);
        bool awakening = pokemonContent.AllowSleepStatus && effect == FieldMedicineEffect.CureSleep;
        if (definition is null || !awakening && effect is not (FieldMedicineEffect.HealHp or FieldMedicineEffect.CureStatus))
            return Fail("medicine_effect_unsupported");
        int count = OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory, definition), item);
        if (count == 0) return Fail("medicine_not_in_bag");
        if (effect == FieldMedicineEffect.HealHp && item != OpeningInventoryRules.PotionId)
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
        // Party validation above rejects mixtures and battle-only Toxic counter bits.
        bool poisonCure = definition.CureStatus == Game.Domain.WildBattle.WildBattleStatus.Poison &&
            SourcePartyStatusRules.IsPoison(pokemon.Status, pokemonContent.AllowToxicStatus);
        if (awakening ? !Game.Domain.WildBattle.WildBattleStatus.IsSleeping(pokemon.Status) :
            !poisonCure && pokemon.Status != definition.CureStatus) return Fail("medicine_no_effect");
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1), pokemon with { Status = 0 },
            true, 0, pokemon.Status, 1, context == PotionUseContext.Battle, null);
    }
}
