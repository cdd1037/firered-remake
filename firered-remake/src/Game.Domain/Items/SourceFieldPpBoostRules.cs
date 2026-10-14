using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public enum SourcePpBoostKind { PpUp, PpMax }
/// <summary>Display capability, never an authorization receipt. Use resolves the actual catalog again.</summary>
public sealed record SourceFieldPpBoost(ItemId Item, SourcePpBoostKind Kind);
public sealed record SourceFieldPpBoostMove(int MoveSlot, PokemonMoveSlot Move, bool Eligible);
public sealed record SourceFieldPpBoostSelection(ImmutableArray<SourceFieldPpBoostMove> Moves, string? Error)
{
    public bool Succeeded => Error is null;
}
public sealed record SourceFieldPpBoostResult(InventoryState Inventory, PartyPokemon Pokemon,
    int PpUpsAdded, int MaximumPpAdded, int CurrentPpAdded, int FriendshipAdded, int ItemsConsumed, string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>Original party-menu PP Up / PP Max effects over existing owned identity and inventory.
/// Field-only: no battle turn, UI, RNG, regional catalog, save schema or item fingerprint changes.</summary>
public static class SourceFieldPpBoostRules
{
    public static bool Supports(EarlyShopCatalog catalog, ItemId item) => Resolve(catalog, item) is not null;

    public static SourceFieldPpBoost? Resolve(EarlyShopCatalog catalog, ItemId item)
    {
        if (catalog is null || catalog.Items.IsDefaultOrEmpty || catalog.Shops.IsDefault ||
            catalog.Items.Any(i => i is null || i.EffectBytes.IsDefault)) return null;
        try { EarlyShopInventoryRules.ValidateCatalog(catalog); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException) { return null; }
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        if (definition is not { Pocket: "POCKET_ITEMS", Type: "ITEM_TYPE_PARTY_MENU",
            FieldUseFunction: "FieldUseFunc_PpUp", BattleUsage: 0, BattleUseFunction: "NULL" }) return null;
        if (item.Value == "pret:ITEM_PP_UP" && definition.SourceItemSymbol == "ITEM_PP_UP" &&
            definition.SourceItemId == 69 && definition.EffectBytes.SequenceEqual(new[] { 0, 0, 0, 0, 32, 224, 5, 3, 2 }))
            return new(item, SourcePpBoostKind.PpUp);
        if (item == OrdinaryItemInventoryCatalog.PpMaxId && definition.SourceItemSymbol == "ITEM_PP_MAX" &&
            definition.SourceItemId == 71 && definition.EffectBytes.SequenceEqual(new[] { 0, 0, 0, 0, 0, 240, 5, 3, 2 }))
            return new(item, SourcePpBoostKind.PpMax);
        return null;
    }

    /// <summary>All actual compact move slots, including no-effect choices; indexes stay zero-based.
    /// The original menu permits a choice and reports no effect. Presentation may mark it ineligible.</summary>
    public static SourceFieldPpBoostSelection SelectMoves(EarlyShopCatalog catalog,
        CampaignPokemonContent pokemonContent, PartyPokemon pokemon, ItemId item)
    {
        if (Resolve(catalog, item) is null) return new([], "pp_boost_effect_unsupported");
        if (PokemonStateValidator.ValidateParty(pokemonContent, pokemon) is { } error) return new([], error);
        if (pokemon.IsEgg) return new([], "pp_boost_egg_unsupported");
        return new(pokemon.Pokemon.Moves.Select((move, index) =>
            new SourceFieldPpBoostMove(index, move, Eligible(move))).ToImmutableArray(), null);
    }

    public static SourceFieldPpBoostResult Use(EarlyShopCatalog catalog, CampaignPokemonContent pokemonContent,
        InventoryState inventory, PartyPokemon pokemon, ItemId item, int moveSlot, string currentMapSection)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(pokemon);
        SourceFieldPpBoostResult Fail(string error) => new(inventory, pokemon, 0, 0, 0, 0, 0, error);
        var capability = Resolve(catalog, item);
        if (capability is null) return Fail("pp_boost_effect_unsupported");
        if (EarlyShopInventoryRules.Validate(catalog, inventory) is { } inventoryError) return Fail(inventoryError);
        if (PokemonStateValidator.ValidateParty(pokemonContent, pokemon) is { } pokemonError) return Fail(pokemonError);
        if (pokemon.IsEgg) return Fail("pp_boost_egg_unsupported");
        if (moveSlot < 0 || moveSlot >= pokemon.Pokemon.Moves.Length) return Fail("pp_boost_move_slot");
        // Caller supplies the current map's authenticated region section, not its map ID or met data.
        if (string.IsNullOrEmpty(currentMapSection) || !currentMapSection.StartsWith("MAPSEC_", StringComparison.Ordinal) ||
            currentMapSection.Length == 7 || currentMapSection.Any(c => c != '_' && c is not (>= 'A' and <= 'Z') && c is not (>= '0' and <= '9')))
            return Fail("pp_boost_map_section");
        var definition = EarlyShopInventoryRules.Find(catalog, item)!;
        int count = OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory, definition), item);
        if (count == 0) return Fail("pp_boost_not_in_bag");
        var before = pokemon.Pokemon; var move = before.Moves[moveSlot];
        // PokemonItemUseNoEffect is mandatory for the field path, especially PP Max / Sketch.
        if (!Eligible(move)) return Fail("pp_boost_no_effect");
        int ppUps = capability.Kind == SourcePpBoostKind.PpMax ? 3 : move.PpUps + 1;
        int maximumPp = PokemonMoveRules.MaximumPp(pokemonContent.Catalog.Moves[move.Move], ppUps);
        int ppAdded = maximumPp - move.MaximumPp;
        var changedMove = move with { PpUps = ppUps, MaximumPp = maximumPp, CurrentPp = move.CurrentPp + ppAdded };
        int band = before.Friendship < 100 ? 0 : before.Friendship < 200 ? 1 : 2;
        int friendship = SourceItemFriendshipRules.Apply(before.Friendship, definition.EffectBytes[6 + band],
            pokemon.HeldItem, new(pokemon.Ball, pokemon.MetLocation, currentMapSection));
        var changed = pokemon with { Pokemon = before with {
            Moves = before.Moves.SetItem(moveSlot, changedMove), Friendship = friendship } };
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1), changed,
            ppUps - move.PpUps, ppAdded, ppAdded, friendship - before.Friendship, 1, null);
    }

    // Source checks pre-use calculated maximum PP >= 5, not remaining PP or raw base PP.
    private static bool Eligible(PokemonMoveSlot move) => move.PpUps < 3 && move.MaximumPp >= 5;
}
