using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableDictionary<string,string> EventIslandMapSourceHashes = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["data/maps/BirthIsland_Exterior/scripts.inc"] = "04f37aed5bbf5fa74a85151f9887397dbdc46b19d3e8679ac10bb8c774258b08",
        ["data/maps/BirthIsland_Harbor/scripts.inc"] = "ef49a1d018038f07c65dfc6bcc64033ad2beea78acba0087ccf59bf37b543d71",
        ["data/maps/NavelRock_1F/scripts.inc"] = "4e3856eaca5412cdd9c70c335b9ea2225bb4aa75ccae0e30dca62d17678755a0",
        ["data/maps/NavelRock_B1F/scripts.inc"] = "2e2f5c94260df4d01e8bcefd1962bc90771a8a6284b036354fd68b38a660219b",
        ["data/maps/NavelRock_Base/scripts.inc"] = "c51810c7c0f63c8700fb7d49815166287cefa9c43d46b5ec1882a03b795b2b40",
        ["data/maps/NavelRock_BasePath_B10F/scripts.inc"] = "0f640bc5afa049497b802ac58fdc7d1ac071d91085ee5cffd60d8480ef0ec109",
        ["data/maps/NavelRock_BasePath_B11F/scripts.inc"] = "53fd2ff6c735101221f2542c16e675c3f884086f894830d38b72b8976c657cfe",
        ["data/maps/NavelRock_BasePath_B1F/scripts.inc"] = "09d6f9378d3922ceaa899ff3e0c12c0221667ccbccd37e96b18f74d77afb2a93",
        ["data/maps/NavelRock_BasePath_B2F/scripts.inc"] = "ec90d977f48275763d881242ef1e30ffdc5b50778e2ca6014dda9facd3760b70",
        ["data/maps/NavelRock_BasePath_B3F/scripts.inc"] = "f7286dc2d9e06a6d8e5b256769fc9b2974a80e529f544514ddb84ac27a2ca50c",
        ["data/maps/NavelRock_BasePath_B4F/scripts.inc"] = "26c407eac8aa00658b86f0c8aa31cc84f3a74d4c6844b12bc4aa0bcb7b693695",
        ["data/maps/NavelRock_BasePath_B5F/scripts.inc"] = "f4377bc9850deb556cb524726b207c9bb54d0ac2db83f8f740fc11d250e4c621",
        ["data/maps/NavelRock_BasePath_B6F/scripts.inc"] = "d42441a68fb8a0cff60d9f1e258d4eafda59f1af890448f99467595d2c9cfe88",
        ["data/maps/NavelRock_BasePath_B7F/scripts.inc"] = "b30afb9183b20b3a00beb3d1acb764ff751f68a7bcc0f1ea2d9a78ad28726e30",
        ["data/maps/NavelRock_BasePath_B8F/scripts.inc"] = "172a4629d545ea246fd208636c0d2fb627ca45ca08bc81c2af3315c63b223aea",
        ["data/maps/NavelRock_BasePath_B9F/scripts.inc"] = "5852ea39c393ac8a75d4ea05e62187d93e058b92dd0eaed24abb40bedb33af03",
        ["data/maps/NavelRock_Exterior/scripts.inc"] = "1f94ede410647b34c416a96114459e31ad2322d075b1bbbb5b0d3fc04b6600ac",
        ["data/maps/NavelRock_Fork/scripts.inc"] = "ececd14f5fcda54066e96bf31de7826b95c904ef9dbf7b1315bc38c95baaa71f",
        ["data/maps/NavelRock_Harbor/scripts.inc"] = "c775b8b8c9c5cbc2dea95d4313ef384ab790d865e120439debb856b4c774f1b8",
        ["data/maps/NavelRock_Summit/scripts.inc"] = "3dffe6191b7c9cd9cb60083cc370b326afcc8057ec5d874cc7e372e5cbc528d8",
        ["data/maps/NavelRock_SummitPath_2F/scripts.inc"] = "c8292e701ec23d9f820288b8578210235905ad70a4176bca11d5ec52582032b9",
        ["data/maps/NavelRock_SummitPath_3F/scripts.inc"] = "dd73e613d6edfc90edd79880a924550e1ffa30e29766f307afd68cde218c65c4",
        ["data/maps/NavelRock_SummitPath_4F/scripts.inc"] = "859b32f4d9e333a588d225bbbdbcd0c53b061a3d2a2974b5de359098760f0664",
        ["data/maps/NavelRock_SummitPath_5F/scripts.inc"] = "47451d98230434a2b67d3a588478342d4e0b2e8a0c818d4a43f8b1ce1031f919",
    }.ToImmutableDictionary(StringComparer.Ordinal);
    private static bool IsEventIslandMapSource(PretSourceScriptFile file) =>
        EventIslandMapSourceHashes.TryGetValue(file.Path, out var hash) && FirstSeviiSource(file, file.Path, hash);
    private static bool IsEventIslandAssemblyData(PretSourceScriptFile file, string label, string[] body) =>
        IsEventIslandMapSource(file) && label == file.Path.Split('/')[2] + "_MapScripts" && body.SequenceEqual(new[] { ".byte 0" });
    private static (StoryOp,int,int)? EventIslandInstructionShape(PretSourceScriptFile file, string label, string opcode, ImmutableArray<string> operands)
    {
        bool eventPath = file.Path.StartsWith("data/maps/NavelRock", StringComparison.Ordinal) || file.Path.StartsWith("data/maps/BirthIsland", StringComparison.Ordinal);
        if (!IsEventIslandMapSource(file)) return eventPath ? (StoryOp.Unsupported, 0, int.MaxValue) : null;
        string? species = label switch
        {
            "NavelRock_Summit_EventScript_HoOh" => "SPECIES_HO_OH",
            "NavelRock_Base_EventScript_Lugia" => "SPECIES_LUGIA",
            "BirthIsland_Exterior_EventScript_Deoxys" => "SPECIES_DEOXYS", _ => null
        };
        if (opcode == "seteventmon")
        {
            if (species is null || !operands.SequenceEqual(new[] { species, species == "SPECIES_DEOXYS" ? "30" : "70" }))
                throw new InvalidDataException("event_island_eventmon_source_operands");
            // Keep a distinct event-mon creation operation. Application owns RNG/fateful metadata.
            return (StoryOp.Special, 2, 2);
        }
        if (opcode == "special" && operands.SequenceEqual(new[] { "StartLegendaryBattle" }))
            return species is null ? (StoryOp.Unsupported, 0, int.MaxValue) : (StoryOp.TrainerBattle, 1, 1);
        if (opcode is "setweather" or "doweather")
        {
            if (label != "NavelRock_Summit_EventScript_HoOh" ||
                opcode == "setweather" && !operands.SequenceEqual(new[] { "0" }) || opcode == "doweather" && !operands.IsEmpty)
                throw new InvalidDataException("event_island_weather_source_operands");
            return (StoryOp.Special, opcode == "setweather" ? 1 : 0, opcode == "setweather" ? 1 : 0);
        }
        if (opcode == "setfieldeffectargument")
        {
            if (label != "BirthIsland_Exterior_EventScript_Deoxys" || operands.Length != 2 ||
                !(operands.SequenceEqual(new[] { "0", "LOCALID_BIRTH_ISLAND_EXTERIOR_ROCK" }) ||
                  operands.SequenceEqual(new[] { "1", "MAP_NUM(MAP_BIRTH_ISLAND_EXTERIOR)" }) ||
                  operands.SequenceEqual(new[] { "2", "MAP_GROUP(MAP_BIRTH_ISLAND_EXTERIOR)" })))
                throw new InvalidDataException("event_island_rock_argument_source");
            return (StoryOp.Special, 2, 2);
        }
        return null;
    }
}
