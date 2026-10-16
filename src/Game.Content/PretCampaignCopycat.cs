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
        EarlyShop!.Items.Any(i => i.Id == ItemIds.PokeDollId) ? new() : null;



    private static void ValidateCopycatHouseComposition(PretCopycatHouse house,ImmutableDictionary<MapId, PretWorldMap> maps,PretStoryCompilation scripts)
    {
        if (house.NewMaps.Count != 2 )
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
