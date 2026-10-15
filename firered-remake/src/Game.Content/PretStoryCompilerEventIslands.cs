using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableHashSet<string>EventIslandMapSources = new[] {"data/maps/BirthIsland_Exterior/scripts.inc","data/maps/BirthIsland_Harbor/scripts.inc","data/maps/NavelRock_1F/scripts.inc","data/maps/NavelRock_B1F/scripts.inc","data/maps/NavelRock_Base/scripts.inc","data/maps/NavelRock_BasePath_B10F/scripts.inc","data/maps/NavelRock_BasePath_B11F/scripts.inc","data/maps/NavelRock_BasePath_B1F/scripts.inc","data/maps/NavelRock_BasePath_B2F/scripts.inc","data/maps/NavelRock_BasePath_B3F/scripts.inc","data/maps/NavelRock_BasePath_B4F/scripts.inc","data/maps/NavelRock_BasePath_B5F/scripts.inc","data/maps/NavelRock_BasePath_B6F/scripts.inc","data/maps/NavelRock_BasePath_B7F/scripts.inc","data/maps/NavelRock_BasePath_B8F/scripts.inc","data/maps/NavelRock_BasePath_B9F/scripts.inc","data/maps/NavelRock_Exterior/scripts.inc","data/maps/NavelRock_Fork/scripts.inc","data/maps/NavelRock_Harbor/scripts.inc","data/maps/NavelRock_Summit/scripts.inc","data/maps/NavelRock_SummitPath_2F/scripts.inc","data/maps/NavelRock_SummitPath_3F/scripts.inc","data/maps/NavelRock_SummitPath_4F/scripts.inc","data/maps/NavelRock_SummitPath_5F/scripts.inc"}.ToImmutableHashSet(StringComparer.Ordinal);
    private static bool IsEventIslandMapSource(PretSourceScriptFile file) =>
        EventIslandMapSources.Contains(file.Path) && FirstSeviiSource(file,file.Path);
    private static bool IsEventIslandAssemblyData(PretSourceScriptFile file,string label,string[] body) =>
        IsEventIslandMapSource(file) && label == file.Path.Split('/')[2] + "_MapScripts" && body.SequenceEqual(new[] { ".byte 0" });
    private static (StoryOp,int,int)? EventIslandInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        bool eventPath = file.Path.StartsWith("data/maps/NavelRock", StringComparison.Ordinal) || file.Path.StartsWith("data/maps/BirthIsland", StringComparison.Ordinal);
        if (!IsEventIslandMapSource(file)) return eventPath ? (StoryOp.Unsupported,0,int.MaxValue) : null;
        string? species = label switch
        {
            "NavelRock_Summit_EventScript_HoOh" => "SPECIES_HO_OH",
            "NavelRock_Base_EventScript_Lugia" => "SPECIES_LUGIA",
            "BirthIsland_Exterior_EventScript_Deoxys" => "SPECIES_DEOXYS", _ => null
        };
        if (opcode == "seteventmon")
        {
            if (species is null || !operands.SequenceEqual(new[] { species,species == "SPECIES_DEOXYS" ? "30" : "70" }))
                throw new InvalidDataException("event_island_eventmon_source_operands");
            // Keep a distinct event-mon creation operation. Application owns RNG/fateful metadata.
            return (StoryOp.Special,2,2);
        }
        if (opcode == "special" && operands.SequenceEqual(new[] { "StartLegendaryBattle" }))
            return species is null ? (StoryOp.Unsupported,0,int.MaxValue) : (StoryOp.TrainerBattle,1,1);
        if (opcode is "setweather" or "doweather")
        {
            if (label != "NavelRock_Summit_EventScript_HoOh" ||
                opcode == "setweather" && !operands.SequenceEqual(new[] { "0" }) || opcode == "doweather" && !operands.IsEmpty)
                throw new InvalidDataException("event_island_weather_source_operands");
            return (StoryOp.Special,opcode == "setweather" ? 1 : 0,opcode == "setweather" ? 1 : 0);
        }
        if (opcode == "setfieldeffectargument")
        {
            if (label != "BirthIsland_Exterior_EventScript_Deoxys" || operands.Length != 2 ||
                !(operands.SequenceEqual(new[] { "0","LOCALID_BIRTH_ISLAND_EXTERIOR_ROCK" }) ||
                  operands.SequenceEqual(new[] { "1","MAP_NUM(MAP_BIRTH_ISLAND_EXTERIOR)" }) ||
                  operands.SequenceEqual(new[] { "2","MAP_GROUP(MAP_BIRTH_ISLAND_EXTERIOR)" })))
                throw new InvalidDataException("event_island_rock_argument_source");
            return (StoryOp.Special,2,2);
        }
        return null;
    }
}
