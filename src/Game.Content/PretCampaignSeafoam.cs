using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretSeafoamRegion SeafoamRegion { get; }
    public bool SeafoamEnabled => SeafoamRegion is not null;
    public GamePokemonCatalog SeafoamOwnership { get; }
    public GameOpponentPokemonCatalog SeafoamOpponents { get; }
    public MachineCatalog SeafoamMachines { get; }
    public SeafoamTrainerCatalog SeafoamTrainers { get; }
    public SeafoamWaterEncounterCatalog SeafoamWaterEncounters { get; }
    public IFieldMap SeafoamMap(MapId map,bool stoppedCurrent) => SeafoamRegion.SeafoamMap(map, stoppedCurrent);
    public ConnectedFieldMap SeafoamConnectedMap(MapId map,bool stoppedCurrent) =>
        PretSeafoamRegion.ConnectedMap(SeafoamRegion.SeafoamMap(map, stoppedCurrent));

    private static void ValidateSeafoamComposition(PretSeafoamRegion region,ImmutableDictionary<MapId, PretWorldMap> maps,ImmutableDictionary<MapId, PretWorldMap> previous,PretStoryCompilation scripts)
    {
        if (region.NewMaps.Count != 7 )
            throw new InvalidDataException("seafoam_map_composition");
        foreach (var map in region.NewMaps.Values)
        {
            foreach (var warp in map.FieldWarps)
                if (!maps.ContainsKey(warp.DestinationMapId)) throw new InvalidDataException("seafoam_warp_destination");
        }
        foreach (string script in new[] { "SeafoamIslands_B3F_OnTransition","SeafoamIslands_B4F_OnTransition","SeafoamIslands_B4F_OnLoad","SeafoamIslands_B4F_EventScript_Articuno","Route20_OnTransition" })
        {
            if (scripts.FindProgram(script) is not { } program || program.Instructions.IsEmpty ||
                program.Instructions.Any(instruction => instruction.Op == StoryOp.Unsupported))
                throw new InvalidDataException("seafoam_story_incomplete:" + script);
        }
    }
}
