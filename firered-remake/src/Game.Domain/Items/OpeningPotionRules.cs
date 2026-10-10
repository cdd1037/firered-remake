using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public static class OpeningPotionRules
{
    /// <summary>Only HP changes. Successful battle use commits an item action; the caller must
    /// synchronize its active combatant and resolve the opposing turn in the same transaction.</summary>
    public static PotionUseResult Use(OpeningItemsCatalog items, PokemonCatalog pokemonCatalog,
        InventoryState inventory, PokemonInstance pokemon, PotionUseContext context)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(pokemon);
        PotionUseResult Fail(string error) => new(inventory, pokemon, false, 0, 0, false, error);
        var inventoryError = OpeningInventoryRules.Validate(items, inventory);
        if (inventoryError is not null) return Fail(inventoryError);
        if (!Enum.IsDefined(context)) return Fail("potion_context_unsupported");
        var pokemonError = PokemonStateValidator.Validate(pokemonCatalog, pokemon);
        if (pokemonError is not null) return Fail(pokemonError);
        int count = OpeningInventoryRules.Quantity(inventory.BagItems, items.Potion.Id);
        if (count == 0) return Fail("potion_not_in_bag");
        if (pokemon.CurrentHp == 0 || pokemon.CurrentHp == pokemon.Stats.MaxHp) return Fail("potion_no_effect");
        int restored = Math.Min(items.Potion.HealHp, pokemon.Stats.MaxHp - pokemon.CurrentHp);
        var healed = pokemon with { CurrentHp = pokemon.CurrentHp + restored };
        var after = inventory with { BagItems = OpeningInventoryRules.SetQuantity(inventory.BagItems, items.Potion.Id, count - 1) };
        return new(after, healed, true, restored, 1, context == PotionUseContext.Battle, null);
    }

    /// <summary>Wrapper preserves entity identity, nickname, ownership, met data and held item.
    /// Eggs/status ailments remain explicitly outside the current campaign profile.</summary>
    public static PartyPotionUseResult UseOnParty(OpeningItemsCatalog items, CampaignPokemonContent pokemonContent,
        InventoryState inventory, PartyPokemon pokemon, PotionUseContext context)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        var error = PokemonStateValidator.ValidateParty(pokemonContent, pokemon);
        if (error is not null) return new(inventory, pokemon, false, 0, 0, false, error);
        var result = Use(items, pokemonContent.Catalog, inventory, pokemon.Pokemon, context);
        return new(result.Inventory, result.Succeeded ? pokemon with { Pokemon = result.Pokemon } : pokemon,
            result.Succeeded, result.HpRestored, result.ItemsConsumed, result.ConsumesBattleTurn, result.Error);
    }
}
