using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretFourIslandDayCare FourIslandDayCare { get; }
    public PokemonBreedingData? BreedingMetadata => PokemonContent?.Catalog.BreedingData;
    public IReadOnlyDictionary<int, string> DayCareCompatibilityTextSymbols { get; } =
        new Dictionary<int, string> { [70] = "gDaycareText_GetAlongVeryWell",[50] = "gDaycareText_GetAlong",[20] = "gDaycareText_DontLikeOther",[0] = "gDaycareText_PlayOther" }.ToImmutableDictionary();

    private static void ValidateFourIslandDayCareComposition(PretFourIslandDayCare daycare,ImmutableDictionary<MapId, PretWorldMap> maps,PretStoryCompilation scripts)
    {
        if (!maps.ContainsKey(PretFourIslandDayCare.HouseMapId) )
            throw new InvalidDataException("four_daycare_map_composition");
        var house = maps[PretFourIslandDayCare.HouseMapId]; var town = maps[new("map.four-island")];
        var door = town.FieldWarps.Single(w => w.Index == 1);
        if (door.X != 12 || door.Y != 13 || door.DestinationMapId != house.Id || door.DestinationWarpId != 0 ||
            house.FieldWarps.Length != 1 || house.FieldWarps[0] is not { X: 4, Y: 7, DestinationWarpId: 1 } ||
            house.FieldWarps[0].DestinationMapId != town.Id)
            throw new InvalidDataException("four_daycare_original_door");
        foreach (var root in new[] { PretFourIslandDayCare.EntryScript,PretFourIslandDayCare.ManEntryScript,"EventScript_EggHatch" })
            if (scripts.FindProgram(root) is not { Instructions.IsEmpty: false } p || p.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("four_daycare_story_missing: " + root);
    }
}
