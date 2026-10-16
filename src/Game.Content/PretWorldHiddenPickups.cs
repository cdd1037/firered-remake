using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Hidden pickups are a projection of the selected world's background events,
/// never a regional selection. Coins retain their distinct source ITEM_NONE semantics.</summary>
public static class PretWorldHiddenPickups
{
    public static ImmutableArray<FieldPickupDefinition> Load(PretCurrentWorld world, SharedItemCatalog items)
    {
        var constants = PretSharedConstants.ForPackage(Path.Combine(world.ProjectRoot, "content/generated/maps"));
        var pickups = ImmutableArray.CreateBuilder<FieldPickupDefinition>();
        foreach (var map in world.Maps.Values.OrderBy(m => m.SourceMapSymbol, StringComparer.Ordinal))
        foreach (var row in map.Events.GetProperty("bg_events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "hidden_item"))
        {
            string symbol = row.GetProperty("item").GetString()!;
            string flag = row.GetProperty("flag").GetString()!;
            int quantity = row.GetProperty("quantity").GetInt32();
            int x = row.GetProperty("x").GetInt32(), y = row.GetProperty("y").GetInt32();
            if (!constants.TryGetValue(flag, out int flagId) || !constants.TryGetValue(symbol, out int itemId) ||
                quantity is < 1 or > 127 || x < 0 || y < 0 || x >= map.Width || y >= map.Height)
                throw new InvalidDataException("world_hidden_pickup_invalid:" + flag);
            bool coins = symbol == "ITEM_NONE";
            var item = coins ? null : items.Definition(new("pret:" + symbol));
            if (coins ? itemId != 0 || row.GetProperty("underfoot").GetBoolean() : item!.SourceItemId != itemId)
                throw new InvalidDataException("world_hidden_pickup_item:" + symbol);
            pickups.Add(new("hidden:" + flag, map.SourceMapSymbol, true, null, x, y,
                row.GetProperty("elevation").GetInt32(), "EventScript_HiddenItemScript", symbol, itemId,
                new ItemId("pret:" + symbol), item?.Name ?? "COINS", item?.Pocket ?? "", quantity, flag, flagId));
        }
        if (pickups.Count == 0 || pickups.Select(p => p.Flag).Distinct(StringComparer.Ordinal).Count() != pickups.Count ||
            pickups.Select(p => (p.MapSymbol, p.X, p.Y)).Distinct().Count() != pickups.Count)
            throw new InvalidDataException("world_hidden_pickup_identity");
        return pickups.ToImmutable();
    }
}
