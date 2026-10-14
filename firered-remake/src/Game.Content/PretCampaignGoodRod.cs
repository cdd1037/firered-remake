using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretGoodRodService GoodRodService { get; }
    public bool GoodRodEnabled => GoodRodService is not null;
    public ImmutableDictionary<MapId, OldRodEncounterTable> GoodRodEncounters { get; }

    private static void ValidateGoodRodComposition(PretGoodRodService service,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        PretStoryCompilation scripts)
    {
        if (previous.Count != 319 || maps.Count != 320 || service.NewMaps.Count != 1 ||
            !maps.Keys.Except(previous.Keys).SequenceEqual([PretGoodRodService.HouseMapId]) ||
            previous.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            !ReferenceEquals(maps[PretGoodRodService.HouseMapId], service.NewMaps[PretGoodRodService.HouseMapId]))
            throw new InvalidDataException("good_rod_current_map_composition");
        if (service.GoodRodTables.Length != 22 || service.GoodRodTables.Any(table => !maps.ContainsKey(table.Map)))
            throw new InvalidDataException("good_rod_current_encounter_maps");

        var city = maps[new("map.fuchsia-city")];
        var house = maps[PretGoodRodService.HouseMapId];
        foreach (int index in new[] { 7, 8, 9 })
        {
            var warp = city.FieldWarps.Single(w => w.Index == index);
            if (warp.DestinationMapId != house.Id || warp.DestinationWarpId != (index == 7 ? 1 : 3))
                throw new InvalidDataException("good_rod_original_city_warp");
        }
        if (house.FieldWarps.Length != 4 || house.FieldWarps.Any(warp =>
            warp.DestinationMapId != city.Id || warp.DestinationWarpId != (warp.Index == 3 ? 8 : 7)))
            throw new InvalidDataException("good_rod_original_house_warp");

        foreach (string suffix in new[] { "FishingGurusBrother", "AlreadyGotGoodRod", "GiveGoodRod", "NoRoomForGoodRod" })
        {
            string label = "FuchsiaCity_House2_EventScript_" + suffix;
            if (scripts.FindProgram(label) is not { } program || program.Instructions.IsEmpty ||
                program.Instructions.Any(instruction => instruction.Op == StoryOp.Unsupported))
                throw new InvalidDataException("good_rod_story_incomplete:" + label);
        }
        if (!scripts.TryConstant("ITEM_GOOD_ROD", out int item) || item != 263 ||
            !scripts.TryConstant("FLAG_GOT_GOOD_ROD", out int flag) || flag != 0x244)
            throw new InvalidDataException("good_rod_story_constants");
    }
}
