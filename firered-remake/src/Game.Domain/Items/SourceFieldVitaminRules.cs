using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>SixStats order, distinct from the positional order of BattleStats.</summary>
public enum SourceVitaminStat { Hp, Attack, Defense, Speed, SpecialAttack, SpecialDefense }
/// <summary>Display capability only. Use reauthenticates the actual catalog and item.</summary>
public sealed record SourceFieldVitamin(ItemId Item, SourceVitaminStat Stat);
public sealed record SourceFieldVitaminResult(InventoryState Inventory, PartyPokemon Pokemon,
    int EvsAdded, int MaximumHpAdded, int CurrentHpAdded, int FriendshipAdded, int ItemsConsumed, string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>Original FireRed field vitamins over existing inventory and permanent Pokémon identity.
/// Does not level up, rebuild moves, cure status, revive, or alter any serialized item definition.</summary>
public static class SourceFieldVitaminRules
{
    private sealed record SourceEffect(string Symbol, int Id, SourceVitaminStat Stat, string EffectSymbol,
        ImmutableArray<int> EffectBytes);
    // Complete source rows, verified against the pinned item_effects.h. Historical pickup-only
    // inventory declarations intentionally omit effects; this service does not rewrite them.
    private static readonly ImmutableArray<SourceEffect> Effects = [
        new("ITEM_HP_UP", 63, SourceVitaminStat.Hp, "sItemEffect_HPUp", [0,0,0,0,1,224,10,5,3,2]),
        new("ITEM_PROTEIN", 64, SourceVitaminStat.Attack, "sItemEffect_Protein", [0,0,0,0,2,224,10,5,3,2]),
        new("ITEM_IRON", 65, SourceVitaminStat.Defense, "sItemEffect_Iron", [0,0,0,0,0,225,10,5,3,2]),
        new("ITEM_CARBOS", 66, SourceVitaminStat.Speed, "sItemEffect_Carbos", [0,0,0,0,0,226,10,5,3,2]),
        new("ITEM_CALCIUM", 67, SourceVitaminStat.SpecialAttack, "sItemEffect_Calcium", [0,0,0,0,0,232,10,5,3,2]),
        new("ITEM_ZINC", 70, SourceVitaminStat.SpecialDefense, "sItemEffect_Zinc", [0,0,0,0,0,228,10,5,3,2])
    ];

    public static bool Supports(EarlyShopCatalog catalog, ItemId item) => ResolveEffect(catalog, item) is not null;

    public static SourceFieldVitamin? Resolve(EarlyShopCatalog catalog, ItemId item) =>
        ResolveEffect(catalog, item) is { } effect ? new(item, effect.Stat) : null;

    private static SourceEffect? ResolveEffect(EarlyShopCatalog catalog, ItemId item)
    {
        if (catalog is null || catalog.Items.IsDefaultOrEmpty || catalog.Shops.IsDefault ||
            catalog.Items.Any(i => i is null || i.EffectBytes.IsDefault)) return null;
        try { EarlyShopInventoryRules.ValidateCatalog(catalog); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException) { return null; }
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        if (catalog.SourceCommit != PokemonFactory.SourceCommit ||
            definition is not { Pocket: "POCKET_ITEMS", Type: "ITEM_TYPE_PARTY_MENU", Importance: 0,
                FieldUseFunction: "FieldUseFunc_Medicine", BattleUsage: 0, BattleUseFunction: "NULL" }) return null;
        var source = Effects.FirstOrDefault(e => item.Value == "pret:" + e.Symbol);
        if (source is null || definition.SourceItemSymbol != source.Symbol || definition.SourceItemId != source.Id) return null;
        if (!definition.EffectBytes.IsEmpty && !definition.EffectBytes.SequenceEqual(source.EffectBytes)) return null;
        return source;
    }

    /// <summary>Commit both returned values together after success. A failure preserves both exact
    /// inputs and returns zero deltas. The map section must come from the current authenticated map.</summary>
    public static SourceFieldVitaminResult Use(EarlyShopCatalog catalog, CampaignPokemonContent pokemonContent,
        InventoryState inventory, PartyPokemon pokemon, ItemId item, string currentMapSection)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(pokemon);
        SourceFieldVitaminResult Fail(string error) => new(inventory, pokemon, 0, 0, 0, 0, 0, error);
        var capability = ResolveEffect(catalog, item);
        if (capability is null) return Fail("vitamin_effect_unsupported");
        if (EarlyShopInventoryRules.Validate(catalog, inventory) is { } inventoryError) return Fail(inventoryError);
        if (PokemonStateValidator.ValidateParty(pokemonContent, pokemon) is { } pokemonError) return Fail(pokemonError);
        if (pokemon.IsEgg) return Fail("vitamin_egg_unsupported");
        if (currentMapSection is null || currentMapSection.Length is <= 7 or > 128 ||
            !currentMapSection.StartsWith("MAPSEC_", StringComparison.Ordinal) ||
            !currentMapSection[7..].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_'))
            return Fail("vitamin_map_section");
        var definition = EarlyShopInventoryRules.Find(catalog, item)!;
        int count = OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory, definition), item);
        if (count == 0) return Fail("vitamin_not_in_bag");

        var before = pokemon.Pokemon;
        int currentEv = before.Evs.Values[(int)capability.Stat], totalEvs = before.Evs.Values.Sum();
        // Both the original party precheck and effect executor enforce these caps. Friendship
        // alone cannot make a capped target eligible. HP Up also has a party-menu Shedinja guard.
        if (currentEv >= 100 || totalEvs >= 510 ||
            capability.Stat == SourceVitaminStat.Hp && before.Species.Value == "pret:SPECIES_SHEDINJA")
            return Fail("vitamin_no_effect");
        int added = Math.Min(capability.EffectBytes[6], Math.Min(100 - currentEv, 510 - totalEvs));
        int value = currentEv + added;
        var evs = capability.Stat switch
        {
            SourceVitaminStat.Hp => before.Evs with { Hp = value },
            SourceVitaminStat.Attack => before.Evs with { Attack = value },
            SourceVitaminStat.Defense => before.Evs with { Defense = value },
            SourceVitaminStat.Speed => before.Evs with { Speed = value },
            SourceVitaminStat.SpecialAttack => before.Evs with { SpecialAttack = value },
            SourceVitaminStat.SpecialDefense => before.Evs with { SpecialDefense = value },
            _ => throw new InvalidOperationException("vitamin_stat")
        };
        // CalculateMonStats refreshes every stat, including EVs earned since the last refresh.
        // The current ownership catalog already contains FireRed's effective Deoxys form stats.
        var stats = PokemonStatCalculation.Calculate(pokemonContent.Catalog.Species[before.Species],
            before.Level, before.Nature, before.Ivs, evs, pokemonContent.Catalog.MechanicsData);
        int hpAdded = before.CurrentHp == 0 ? 0 : stats.MaxHp - before.Stats.MaxHp;
        int band = before.Friendship < 100 ? 0 : before.Friendship < 200 ? 1 : 2;
        int friendship = SourceItemFriendshipRules.Apply(before.Friendship, capability.EffectBytes[7 + band],
            pokemon.HeldItem, new(pokemon.Ball, pokemon.MetLocation, currentMapSection));
        var changed = pokemon with { Pokemon = before with {
            Evs = evs, Stats = stats, StatCalculationEvs = null, CurrentHp = before.CurrentHp + hpAdded,
            Friendship = friendship } };
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1), changed,
            added, stats.MaxHp - before.Stats.MaxHp, hpAdded, friendship - before.Friendship, 1, null);
    }
}
