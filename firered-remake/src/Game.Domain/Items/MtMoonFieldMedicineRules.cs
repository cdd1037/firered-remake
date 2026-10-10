using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.Items;

public sealed record MtMoonFieldMedicineResult(InventoryState Inventory, PartyPokemon Pokemon, bool Succeeded,
    int HpRestored, int PpRestored, int ItemsConsumed, string? Error);

/// <summary>Pure field-only Revive and selected-move Ether effects. Source has no status or friendship changes here.</summary>
public static class MtMoonFieldMedicineRules
{
    public static MtMoonFieldMedicineResult Use(EarlyShopCatalog catalog, CampaignPokemonContent pokemonContent,
        InventoryState inventory, PartyPokemon pokemon, ItemId item, int? moveSlot = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(pokemon);
        MtMoonFieldMedicineResult Fail(string error) => new(inventory, pokemon, false, 0, 0, 0, error);
        if (catalog.Profile is not (EarlyShopInventoryRules.MtMoonProfile or RocketHideoutItemRules.Profile)) return Fail("medicine_profile_unsupported");
        var error = EarlyShopInventoryRules.Validate(catalog, inventory);
        if (error is not null) return Fail(error);
        error = PokemonStateValidator.ValidateParty(pokemonContent, pokemon);
        if (error is not null) return Fail(error);
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        if (definition?.UseSupport is not (EarlyShopUseSupport.FieldRevive or EarlyShopUseSupport.FieldEther))
            return Fail("medicine_effect_unsupported");
        int count = OpeningInventoryRules.Quantity(inventory.BagItems, item);
        if (count == 0) return Fail("medicine_not_in_bag");
        var before = pokemon.Pokemon;
        int hp = 0, pp = 0;
        PokemonInstance after;
        if (definition.UseSupport == EarlyShopUseSupport.FieldRevive)
        {
            if (moveSlot is not null) return Fail("medicine_invalid_slot");
            if (before.CurrentHp != 0) return Fail("medicine_no_effect");
            hp = Math.Max(1, before.Stats.MaxHp / 2);
            after = before with { CurrentHp = hp };
        }
        else
        {
            if (moveSlot is null || moveSlot < 0 || moveSlot >= before.Moves.Length) return Fail("medicine_invalid_slot");
            var slot = before.Moves[moveSlot.Value];
            pp = Math.Min(10, slot.MaximumPp - slot.CurrentPp);
            if (pp == 0) return Fail("medicine_no_effect");
            after = before with { Moves = before.Moves.SetItem(moveSlot.Value, slot with { CurrentPp = slot.CurrentPp + pp }) };
        }
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1), pokemon with { Pokemon = after },
            true, hp, pp, 1, null);
    }
}
