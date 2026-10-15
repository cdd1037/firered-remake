using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private static void ValidateTrainerFanClubComposition(PretTrainerFanClub room,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        PretStoryCompilation scripts)
    {
        ValidateSourceHouseComposition("trainer_fan_club", room.NewMaps, maps, previous, scripts,
            "map.saffron-city", 14, PretTrainerFanClub.ScriptPath,
            "SaffronCity_PokemonTrainerFanClub_EventScript_MeetFirstFans");
        if (previous.Count != 364 || maps.Count != 365 ||
            scripts.UnsupportedInstructions.Any(d => d.Path.StartsWith(PretTrainerFanClub.ScriptPath, StringComparison.Ordinal)))
            throw new InvalidDataException("trainer_fan_club_current_composition");
        const string prefix = "SaffronCity_PokemonTrainerFanClub_";
        foreach (var actor in room.NewMaps[PretTrainerFanClub.RoomMapId].Events.GetProperty("object_events").EnumerateArray())
        {
            string label = actor.GetProperty("script").GetString()!;
            if (scripts.FindProgram(label) is not { Instructions.IsEmpty: false } program ||
                program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("trainer_fan_club_actor_story:" + label);
        }
        var headers = scripts.MapHeaders.GetValueOrDefault(prefix + "MapScripts", []);
        if (headers.Length != 2 ||
            !headers.Any(h => h.EventKind == "MAP_SCRIPT_ON_TRANSITION" && h.Target == prefix + "OnTransition") ||
            !headers.Any(h => h.EventKind == "MAP_SCRIPT_ON_FRAME_TABLE" && h.Target == prefix + "OnFrame") ||
            scripts.MapConditions.GetValueOrDefault(prefix + "OnFrame", []).Length != 1 ||
            scripts.Movements.GetValueOrDefault(prefix + "Movement_FanApproachPlayer", []).IsEmpty)
            throw new InvalidDataException("trainer_fan_club_original_map_programs");
    }
}
