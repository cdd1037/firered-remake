using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretRoute5DayCare Route5DayCare { get; }
    public bool Route5DayCareEnabled => true;

    private static void ValidateRoute5DayCareComposition(PretRoute5DayCare daycare,ImmutableDictionary<MapId, PretWorldMap> maps,PretStoryCompilation scripts)
    {
        if (!maps.ContainsKey(PretRoute5DayCare.HouseMapId) )
            throw new InvalidDataException("daycare_map_composition");
        var house = maps[PretRoute5DayCare.HouseMapId]; var route = maps[new("map.route-5")];
        var door = route.FieldWarps.Single(w => w.Index == 1);
        if (door.X != 23 || door.Y != 25 || door.DestinationMapId != house.Id || door.DestinationWarpId != 1 ||
            house.FieldWarps.Length != 3 || house.FieldWarps.Any(w => w.X != w.Index + 3 || w.Y != 7 ||
                w.DestinationMapId != route.Id || w.DestinationWarpId != 1))
            throw new InvalidDataException("daycare_original_three_exit_door");
        // EggHatch shares this source file but is a separate unsupported root.
        var service = scripts.Programs.Values.Where(p => p.Id.StartsWith("Route5_PokemonDayCare_EventScript_", StringComparison.Ordinal)).ToArray();
        if (service.Length == 0 || service.Any(p => p.Instructions.IsEmpty || p.Instructions.Any(i => i.Op == StoryOp.Unsupported)) ||
            scripts.FindProgram(PretRoute5DayCare.EntryScript) is not { Instructions.IsEmpty: false })
            throw new InvalidDataException("daycare_story_incomplete");
    }
}
