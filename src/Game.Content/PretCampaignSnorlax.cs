using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretSnorlaxRegion SnorlaxRegion { get; }
    public PretSnorlaxArt? SnorlaxArt { get; }

    public bool IsSnorlaxStablePosition(MapId map,int x,int y) =>
        PretCyclingRoadRegion.IsRoadOrGate(map) || IsCurrentStaticWarpCellResolved(map,x,y) || Route16FlyHouse.AllowsAdditionalFootDestination(map,x,y) || map.Value=="map.route-12" || SnorlaxRegion is not { } region || !region.NewMaps.ContainsKey(map) || region.AllowsFootDestination(map, x, y);

    public bool IsSnorlaxWarpAllowed(MapId map,int sourceWarpId) =>
        SnorlaxRegion is not { } region || !region.NewMaps.ContainsKey(map) ||
        IsCurrentStaticWarpResolved(map, sourceWarpId);

    private static void ValidateSnorlaxComposition(ImmutableDictionary<MapId, PretWorldMap> maps,CampaignPokemonContent pokemon,PretStoryCompilation scripts,ImmutableDictionary<string, string>.Builder texts)
    {
        foreach (var route in new[] { "Route12","Route16" })
        {
            var program = scripts.FindProgram(route + "_EventScript_Snorlax")
                ?? throw new InvalidDataException("snorlax_event_program_missing: " + route);
            if (program.Instructions.Any(i => i.Op == StoryOp.Unsupported) ||
                program.Instructions.Count(i => i.Opcode == "setwildbattle" && i.Op == StoryOp.Special &&
                    i.Args.SequenceEqual(new[] { "SPECIES_SNORLAX","30" })) != 1 ||
                program.Instructions.Count(i => i.Opcode == "dowildbattle" && i.Op == StoryOp.TrainerBattle && i.Args.IsEmpty) != 1 ||
                program.Instructions.Count(i => i.Opcode == "specialvar" && i.Op == StoryOp.Special &&
                    i.Args.SequenceEqual(new[] { "VAR_RESULT","GetBattleOutcome" })) != 1)
                throw new InvalidDataException("snorlax_event_program_contract: " + route);
            if (scripts.FindProgram(route + "_OnResume") is null ||
                !texts.ContainsKey(route + "_Text_MonSprawledOutInSlumber"))
                throw new InvalidDataException("snorlax_resume_or_text_missing: " + route);
        }
        if (scripts.FindProgram("EventScript_AwakenSnorlax") is null)
            throw new InvalidDataException("snorlax_wake_program_missing");
        foreach (var label in new[] { "Text_WantToUsePokeFlute","Text_PlayedPokeFlute","Text_SnorlaxWokeUp","Text_SnorlaxReturnedToMountains" })
            if (!texts.ContainsKey(label)) throw new InvalidDataException("snorlax_wake_text_missing: " + label);
    }
}
