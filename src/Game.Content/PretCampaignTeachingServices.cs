using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretOriginalTutorRooms OriginalTutorRooms { get; }

    public PretMoveReminderHouse MoveReminderHouse { get; }
    public MoveReminderCatalog? MoveReminder { get; }

    private static void ValidateMoveReminderComposition(PretMoveReminderHouse house,ImmutableDictionary<MapId, PretWorldMap> maps,PretStoryCompilation scripts)
        => ValidateSourceHouseComposition("move_reminder", house.NewMaps, maps, scripts,
            "map.two-island", 1, "data/maps/TwoIsland_House/scripts.inc", "TwoIsland_House_EventScript_MoveManiac");

    public PretMoveDeleterHouse MoveDeleterHouse { get; }
    public MoveDeleterCatalog? MoveDeleter { get; }

    private static void ValidateSourceHouseComposition(string scope,ImmutableDictionary<MapId, PretWorldMap> houseMaps,ImmutableDictionary<MapId, PretWorldMap> maps,PretStoryCompilation scripts,string townId,int townWarp,string scriptPath,string entry)
    {
        if (houseMaps.Count != 1 || !maps.ContainsKey(houseMaps.Single().Key) )
            throw new InvalidDataException(scope + "_map_composition");
        var house = houseMaps.Single().Value;
        var town = maps[new(townId)];
        var door = town.FieldWarps.Single(w => w.Index == townWarp);
        var back = house.FieldWarps.Single();
        if (door.DestinationMapId != house.Id || door.DestinationWarpId != 0 ||
            back.DestinationMapId != town.Id || back.DestinationWarpId != townWarp)
            throw new InvalidDataException(scope + "_original_door");
        foreach (var program in scripts.Programs.Values.Where(p => p.Instructions.Any(i => i.Source.Path == scriptPath)))
            if (program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException(scope + "_story_incomplete:" + program.Id);
        if (scripts.FindProgram(entry) is not { Instructions.IsEmpty: false })
            throw new InvalidDataException(scope + "_story_missing");
    }
}
