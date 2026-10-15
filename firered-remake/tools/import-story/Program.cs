using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Events;

var root = Path.GetFullPath(args[0]);
var input = JsonSerializer.Deserialize<StoryImportInput>(Console.In.ReadToEnd()) ?? throw new InvalidDataException("Missing story import input.");
var output = StoryImport.Compile(input);
var path = Path.Combine(root, "content/generated/story/runtime.json");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
using var document = JsonDocument.Parse(JsonSerializer.Serialize(output));
using var bytes = new MemoryStream();
using (var writer = new Utf8JsonWriter(bytes)) WriteCanonical(writer, document.RootElement);
var json = System.Text.Encoding.UTF8.GetString(bytes.ToArray()) + "\n";
if (args.Contains("--check"))
{
    if (!System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)),
        System.Text.Json.Nodes.JsonNode.Parse(json))) throw new InvalidDataException("Compiled story resource is stale.");
}
else File.WriteAllText(path, json);
Console.Error.WriteLine($"Compiled {output.Compilation.Programs.Count} story programs and {output.TextBank.Count} text labels.");

static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
{
    if (element.ValueKind == JsonValueKind.Object)
    {
        writer.WriteStartObject();
        foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            writer.WritePropertyName(property.Name);
            WriteCanonical(writer, property.Value);
        }
        writer.WriteEndObject();
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
        writer.WriteStartArray();
        foreach (var value in element.EnumerateArray()) WriteCanonical(writer, value);
        writer.WriteEndArray();
    }
    else element.WriteTo(writer);
}

public sealed record StoryImportInput(PretSourceScriptFile[] Sources, Dictionary<string,int> Constants,
    Dictionary<string,string> TextBank, ImmutableDictionary<string,StoryMenuDefinition> Menus,
    ImmutableSortedDictionary<int,string> StarterSpeciesNames, string NicknameCharacters,
    ImmutableSortedDictionary<string,string> MoveNames);

static class StoryImport
{
    public static PretStoryRuntime Compile(StoryImportInput input)
    {
        var sources = input.Sources;
        var constants = input.Constants;
        var compiled = PretStoryCompiler.Compile(sources, constants, includeGiftOpcodes: true, includeRoute3Opcodes: true, includeMtMoonOpcodes: true, includeBillOpcodes: true, includeSsAnneOpcodes: true, includeVermilionGymOpcodes: true, includeRockTunnelOpcodes: true, includeRocketHideoutOpcodes: true, includePokemonTowerOpcodes: true, includeSnorlaxOpcodes: true, includePresetDoubleOpcodes: true, includeSafariOpcodes: true, includeSilphOpcodes: true, includeSaffronGymData: true, includeCinnabarData: true, includeFirstSevii: true, includeViridianGym: true, includeVictoryRoad: true, includePokemonLeague: true, includeSeviiRuby: true, includeFourIsland: true, includeSixIsland: true, includeFiveIsland: true, includeCelio: true, includeSevenIsland: true, includeEventIslands: true, includePowerPlant: true, includeTrainerTower: true, includeSeafoam: true, includeGameCornerCoins: true, includeGameCornerSlots: true);
        compiled = compiled with { Menus = compiled.Menus.AddRange(input.Menus) };
        compiled = CompleteSurfSourceFallthrough(compiled);
        compiled = CompleteFourIslandSourceFallthrough(compiled);
        compiled = CompleteFourIslandDayCareFallthrough(compiled);
        return new(1, compiled, input.TextBank.ToImmutableDictionary(StringComparer.Ordinal), input.StarterSpeciesNames,
            input.NicknameCharacters, input.MoveNames);
    }
    private static PretStoryCompilation CompleteSurfSourceFallthrough(PretStoryCompilation compiled)
    {
        StoryProgram Program(string label, int[] lines, string[] opcodes)
        {
            var program=compiled.FindProgram(label)??throw new InvalidDataException("surf_source_program_missing: "+label);
            if(!program.Instructions.Select(i=>i.Source.Line).SequenceEqual(lines)||
                !program.Instructions.Select(i=>i.Opcode).SequenceEqual(opcodes)||
                program.Instructions.Any(i=>i.Source.Path!="data/scripts/surf.inc"||i.Source.Symbol!=label))
                throw new InvalidDataException("surf_source_fallthrough_identity: "+label);
            return program;
        }
        var use=Program("EventScript_UseSurf",[2,3,4,5,6,7,8,9,10,11],
            ["goto_if_questlog","checkpartymove","goto_if_eq","bufferpartymonnick","setfieldeffectargument",
             "lockall","msgbox","goto_if_eq","msgbox","dofieldeffect"]);
        var release=Program("EventScript_UseSurfRelease",[13],["releaseall"]);
        var end=Program("EventScript_UseSurfEnd",[15],["end"]);
        var programs=compiled.Programs.SetItem(use.Id,use with{Instructions=use.Instructions.AddRange(release.Instructions).AddRange(end.Instructions)})
            .SetItem(release.Id,release with{Instructions=release.Instructions.AddRange(end.Instructions)});
        return compiled with{Programs=programs};
    }
    private static PretStoryCompilation CompleteFourIslandSourceFallthrough(PretStoryCompilation compiled)
    {
        StoryProgram Program(string label, int[] lines, string[] opcodes)
        {
            var program = compiled.FindProgram(label) ?? throw new InvalidDataException("four_island_fallthrough_missing: " + label);
            if (!program.Instructions.Select(i => i.Source.Line).SequenceEqual(lines) ||
                !program.Instructions.Select(i => i.Opcode).SequenceEqual(opcodes) ||
                program.Instructions.Any(i => i.Source.Path != "data/scripts/field_moves.inc" || i.Source.Symbol != label))
                throw new InvalidDataException("four_island_fallthrough_identity: " + label);
            return program;
        }
        var cant = Program("EventScript_CantUseWaterfall", [192], ["lockall"]);
        var wall = Program("EventScript_WaterCrashingDown", [194], ["msgbox"]);
        var end = Program("EventScript_EndWaterfall", [196, 197], ["releaseall", "end"]);
        return compiled with { Programs = compiled.Programs
            .SetItem(cant.Id, cant with { Instructions = cant.Instructions.AddRange(wall.Instructions).AddRange(end.Instructions) })
            .SetItem(wall.Id, wall with { Instructions = wall.Instructions.AddRange(end.Instructions) }) };
    }
    private static PretStoryCompilation CompleteFourIslandDayCareFallthrough(PretStoryCompilation compiled)
    {
        var branch = compiled.FindProgram("FourIsland_EventScript_TrySetDayCareManPos")
            ?? throw new InvalidDataException("daycare_position_program_missing");
        var end = compiled.FindProgram("FourIsland_EventScript_EndSetDayCareManPos")
            ?? throw new InvalidDataException("daycare_position_end_missing");
        if (!branch.Instructions.Select(i => i.Opcode).SequenceEqual(new[] { "goto_if_unset","setobjectxyperm" }) ||
            !branch.Instructions.Select(i => i.Source.Line).SequenceEqual(new[] { 17,18 }) ||
            branch.Instructions.Any(i => i.Source.Path != "data/maps/FourIsland/scripts.inc" || i.Source.Symbol != branch.Id || i.Op == StoryOp.Unsupported) ||
            !end.Instructions.Select(i => i.Opcode).SequenceEqual(new[] { "return" }) ||
            end.Instructions[0].Source.Line != 20 || end.Instructions[0].Source.Path != "data/maps/FourIsland/scripts.inc")
            throw new InvalidDataException("daycare_position_source_fallthrough");
        return compiled with { Programs = compiled.Programs.SetItem(branch.Id,
            branch with { Instructions = branch.Instructions.AddRange(end.Instructions) }) };
    }
}
