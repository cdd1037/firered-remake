using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Original seven-item Vermilion stock; all prior item definitions remain unchanged.</summary>
public static class VermilionItemRules
{
    public const string Profile = "firered-vermilion-shop-v1";
    public const string Mart = "VermilionCity_Mart_Items";
    public static ImmutableArray<ItemId> Stock { get; } =
        new[] { "POKE_BALL", "SUPER_POTION", "ANTIDOTE", "PARALYZE_HEAL", "AWAKENING", "ICE_HEAL", "REPEL" }
        .Select(symbol => new ItemId("pret:ITEM_" + symbol)).ToImmutableArray();


}
