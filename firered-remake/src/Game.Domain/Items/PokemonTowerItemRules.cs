using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Source metadata only. Generic inventory and item execution admission remain separate.</summary>
public static class PokemonTowerItemRules
{
    public const string Profile = "firered-pokemon-tower-items-v1";
    public const int InheritedItemCount = 56, ItemCount = 61;
    public static ItemId ElixirId { get; } = new("pret:ITEM_ELIXIR");
    public static ItemId CleanseTagId { get; } = new("pret:ITEM_CLEANSE_TAG");
    public static ItemId XAccuracyId { get; } = new("pret:ITEM_X_ACCURACY");
    public static ItemId BigMushroomId { get; } = new("pret:ITEM_BIG_MUSHROOM");
    public static ItemId SootheBellId { get; } = new("pret:ITEM_SOOTHE_BELL");
    public static ItemId PokeFluteId { get; } = new("pret:ITEM_POKE_FLUTE");
    private sealed record Expected(ItemId Item, ImmutableArray<int> Effects);
    private static readonly Expected[] Additions = [
        new(new("pret:ITEM_ELIXIR"), [0,0,0,0,8,0,10]),
        new(new("pret:ITEM_CLEANSE_TAG"), []),
        new(new("pret:ITEM_X_ACCURACY"), [0,0,16,0,0,96,1,1]),
        new(new("pret:ITEM_SOOTHE_BELL"), []),
        new(new("pret:ITEM_POKE_FLUTE"), []),
    ];
    public static void ValidateSourceItem(MachineItem metadata, ImmutableArray<int> effects)
    {
        var expected = Additions.FirstOrDefault(e => e.Item == metadata?.Item.Id);
        if (expected is null || string.IsNullOrWhiteSpace(metadata!.Description) || effects.IsDefault ||
            !effects.SequenceEqual(expected.Effects))
            throw new NotSupportedException("regional_item_definition");
    }


}
