using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The twelve original fixed coin backgrounds; reuses existing source bytes and map resources.</summary>
public static class PretGameCornerHiddenCoins
{
    public const string ScriptPath = "data/scripts/obtain_item.inc";


    internal static ImmutableArray<FieldPickupDefinition> Pickups(PretWorldMap map, PretStoryCompilation scripts)
    {
        var backgrounds = map.Events.GetProperty("bg_events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "hidden_item" && e.GetProperty("item").GetString() == "ITEM_NONE").ToArray();
        if (map.Id.Value != GameCornerCoinSource.Map || map.SourceMapSymbol != "MAP_CELADON_CITY_GAME_CORNER" ||
            backgrounds.Length != 12 || backgrounds.Sum(e => e.GetProperty("quantity").GetInt32()) != 260 ||
            !scripts.TryConstant("ITEM_NONE", out int itemId) || itemId != 0)
            throw new InvalidDataException("hidden_coins_map_scope");
        var result = ImmutableArray.CreateBuilder<FieldPickupDefinition>();
        foreach (var bg in backgrounds)
        {
            string flag = bg.GetProperty("flag").GetString()!;
            int quantity = bg.GetProperty("quantity").GetInt32();
            if (bg.GetProperty("underfoot").GetBoolean() || quantity is < 1 or > 127 ||
                !scripts.TryConstant(flag, out int flagId))
                throw new InvalidDataException("hidden_coins_background:" + flag);
            // Position, amount and one-time flag come directly from authenticated map data.
            result.Add(new("hidden-coins:" + flag, map.SourceMapSymbol, true, null,
                bg.GetProperty("x").GetInt32(), bg.GetProperty("y").GetInt32(), bg.GetProperty("elevation").GetInt32(),
                "EventScript_HiddenItemScript", "ITEM_NONE", 0, new ItemId("pret:ITEM_NONE"), "COINS", "", quantity, flag, flagId));
        }
        return result.ToImmutable();
    }
}
