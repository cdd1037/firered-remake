using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretCopycatHouse CopycatHouse { get; }
    public PokeDollShopStock? PokeDollShopStock =>
        PokeDollInventoryCatalog.For(OrdinaryItemInventoryCatalog.BaseCatalog(EarlyShop!)) is not null ? new() : null;

    private static FieldPickupDefinition CopycatHiddenPickup(PretCopycatHouse house,
        EarlyShopCatalog shop, PretStoryCompilation scripts)
    {
        EarlyShopInventoryRules.ValidateCatalog(shop);
        var map = house.NewMaps[PretCopycatHouse.Floor2MapId];
        var hidden = map.Events.GetProperty("bg_events").EnumerateArray()
            .Single(e => e.GetProperty("type").GetString() == "hidden_item");
        const string symbol = "ITEM_NUGGET", flag = "FLAG_HIDDEN_ITEM_SAFFRON_CITY_COPYCATS_HOUSE_2F_NUGGET";
        var item = shop.Items.SingleOrDefault(i => i.Id.Value == "pret:" + symbol);
        if (hidden.GetProperty("item").GetString() != symbol || hidden.GetProperty("flag").GetString() != flag ||
            hidden.GetProperty("x").GetInt32() != 2 || hidden.GetProperty("y").GetInt32() != 1 ||
            hidden.GetProperty("elevation").GetInt32() != 0 || hidden.GetProperty("quantity").GetInt32() != 1 ||
            hidden.GetProperty("underfoot").GetBoolean() || item?.SourceItemSymbol != symbol ||
            !scripts.TryConstant(flag, out int flagId))
            throw new InvalidDataException("copycat_hidden_source");
        return new("copycat:" + flag, map.SourceMapSymbol, true, null, 2, 1, 0,
            "EventScript_HiddenItemScript", symbol, item.SourceItemId, item.Id, item.Name, item.Pocket, 1, flag, flagId);
    }

    private static void ValidateCopycatHouseComposition(PretCopycatHouse house,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        PretStoryCompilation scripts)
    {
        if (house.NewMaps.Count != 2 || maps.Count != previous.Count + 2 ||
            previous.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            house.NewMaps.Any(p => previous.ContainsKey(p.Key) || !ReferenceEquals(p.Value, maps[p.Key])))
            throw new InvalidDataException("copycat_map_composition");
        var town = maps[new("map.saffron-city")];
        var first = house.NewMaps[PretCopycatHouse.Floor1MapId];
        var second = house.NewMaps[PretCopycatHouse.Floor2MapId];
        var door = town.FieldWarps.Single(w => w.Index == 1);
        if (door.DestinationMapId != first.Id || door.DestinationWarpId != 1 ||
            first.FieldWarps.Length != 4 || second.FieldWarps.Length != 1 ||
            first.FieldWarps.Take(3).Any(w => w.DestinationMapId != town.Id || w.DestinationWarpId != 1) ||
            first.FieldWarps[3].DestinationMapId != second.Id || first.FieldWarps[3].DestinationWarpId != 0 ||
            second.FieldWarps[0].DestinationMapId != first.Id || second.FieldWarps[0].DestinationWarpId != 3)
            throw new InvalidDataException("copycat_original_doors");
        foreach (var program in scripts.Programs.Values.Where(p => p.Instructions.Any(i =>
            i.Source.Path.StartsWith("data/maps/SaffronCity_CopycatsHouse_", StringComparison.Ordinal))))
            if (program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("copycat_story_incomplete:" + program.Id);
        if (scripts.FindProgram("SaffronCity_CopycatsHouse_2F_EventScript_Copycat") is not { Instructions.IsEmpty: false })
            throw new InvalidDataException("copycat_story_missing");
    }
}
