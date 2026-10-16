using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretSuperRodService SuperRodService { get; }
    public bool SuperRodEnabled => true;
    public GamePokemonCatalog SuperRodOwnership { get; }
    public GameOpponentPokemonCatalog SuperRodOpponents { get; }
    public MachineCatalog SuperRodMachines { get; }
    public ImmutableDictionary<MapId, OldRodEncounterTable> SuperRodEncounters { get; }

    private static void ValidateSuperRodComposition(PretSuperRodService service,ImmutableDictionary<MapId, PretWorldMap> maps,PretStoryCompilation scripts)
    {
        if (service.NewMaps.Count != 1 ||
            !maps.ContainsKey(PretSuperRodService.HouseMapId) )
            throw new InvalidDataException("super_rod_current_map_composition");
        var route = maps[new("map.route-12")];
        var house = maps[PretSuperRodService.HouseMapId];
        var door = route.FieldWarps.Single(w => w.Index == 0);
        if (door.DestinationMapId != house.Id || door.DestinationWarpId != 1 ||
            house.FieldWarps.Length != 3 || house.FieldWarps.Any(w => w.DestinationMapId != route.Id || w.DestinationWarpId != 0))
            throw new InvalidDataException("super_rod_original_door");
        foreach (var program in scripts.Programs.Values.Where(p => p.Instructions.Any(i =>
            i.Source.Path == "data/maps/Route12_FishingHouse/scripts.inc")))
            if (program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("super_rod_story_incomplete:" + program.Id);
        if (scripts.FindProgram("Route12_FishingHouse_EventScript_FishingGuruBrother") is not { Instructions.IsEmpty: false } ||
            !scripts.TryConstant("ITEM_SUPER_ROD", out int item) || item != 264 ||
            !scripts.TryConstant("FLAG_GOT_SUPER_ROD", out int flag) || flag != 0x255 ||
            !service.Constants.TryGetValue("VAR_MAGIKARP_SIZE_RECORD", out int record) || record != 0x4040)
            throw new InvalidDataException("super_rod_story_constants");
    }
}
