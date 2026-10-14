using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{

    public bool RegionalFlavorEnabled { get; }
    public bool SupportsRegionalFlavor(MapId map) => RegionalFlavorEnabled &&
        SourceMaps.TryGetValue(map, out var source) && (RegionalFlavorMaps.Contains(source.SourceMapSymbol) ||
            IsRegionalServiceMap(map) || SafariRegion?.SourceMaps.ContainsKey(map)==true || SilphRegion?.NewMaps.ContainsKey(map)==true || SaffronGymRegion?.NewMaps.ContainsKey(map)==true || ViridianGymRegion?.NewMaps.ContainsKey(map)==true || VictoryRoadRegion?.SourceMaps.ContainsKey(map)==true || PokemonLeagueRegion?.SourceMaps.ContainsKey(map)==true || SeviiRubyRegion?.SourceMaps.ContainsKey(map)==true || FourIslandRegion?.SourceMaps.ContainsKey(map)==true || SixIslandRegion?.SourceMaps.ContainsKey(map)==true || FiveIslandRegion?.SourceMaps.ContainsKey(map)==true || SevenIslandRegion?.SourceMaps.ContainsKey(map)==true || EventIslandRegion?.NewMaps.ContainsKey(map)==true || PowerPlantRegion?.NewMaps.ContainsKey(map)==true || TrainerTowerRegion?.Maps.ContainsKey(map)==true || FightingDojoRegion?.NewMaps.ContainsKey(map)==true || FossilLabRegion?.NewMaps.ContainsKey(map)==true || NpcTradeHaden.NewMaps.ContainsKey(map) || GoodRodService.NewMaps.ContainsKey(map) || SeafoamRegion.NewMaps.ContainsKey(map) || MoveReminderHouse.NewMaps.ContainsKey(map) || MoveDeleterHouse.NewMaps.ContainsKey(map) || OriginalTutorRooms.NewMaps.ContainsKey(map) || CopycatHouse.NewMaps.ContainsKey(map) || SuperRodService.NewMaps.ContainsKey(map) || CapeBrinkTutor.NewMaps.ContainsKey(map) || Route16FlyHouse.NewMaps.ContainsKey(map) || Route5DayCare.NewMaps.ContainsKey(map) || FourIslandDayCare.NewMaps.ContainsKey(map) || OriginalTmAcquisition.NewMaps.ContainsKey(map));

    private static readonly ImmutableHashSet<string> RegionalFlavorMaps = new[]
    {
        "MAP_CELADON_CITY", "MAP_CELADON_CITY_GAME_CORNER", "MAP_CELADON_CITY_GYM",
        "MAP_CELADON_CITY_POKEMON_CENTER_1F", "MAP_CELADON_CITY_POKEMON_CENTER_2F",
        "MAP_LAVENDER_TOWN", "MAP_LAVENDER_TOWN_HOUSE1", "MAP_LAVENDER_TOWN_MART",
        "MAP_LAVENDER_TOWN_POKEMON_CENTER_1F", "MAP_LAVENDER_TOWN_POKEMON_CENTER_2F",
        "MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE",
        "MAP_POKEMON_TOWER_1F", "MAP_POKEMON_TOWER_2F", "MAP_POKEMON_TOWER_3F",
        "MAP_POKEMON_TOWER_4F", "MAP_POKEMON_TOWER_5F", "MAP_POKEMON_TOWER_6F", "MAP_POKEMON_TOWER_7F",
        "MAP_ROUTE7", "MAP_ROUTE7_EAST_ENTRANCE", "MAP_ROUTE8", "MAP_ROUTE8_WEST_ENTRANCE",
        "MAP_UNDERGROUND_PATH_EAST_ENTRANCE", "MAP_UNDERGROUND_PATH_EAST_WEST_TUNNEL",
        "MAP_UNDERGROUND_PATH_WEST_ENTRANCE"
    }.ToImmutableHashSet(StringComparer.Ordinal);

    private static void AppendRegionalFlavorSources(string root, List<PretSourceScriptFile> sources,
        List<string> textFiles, List<string> hashes)
    {
        string Read(string folder, string path, string expected)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, "content/raw", folder, path));
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
                throw new InvalidDataException("regional_flavor_source_hash: " + path);
            hashes.Add("regional-flavor-source:" + path + ":" + expected);
            return Encoding.UTF8.GetString(bytes);
        }
        const string scriptPath = "data/scripts/flavor_text.inc";
        const string scriptHash = "3290aee116c25c28710fa3f6bcf2c0ffd00a8a5746b0228940f34d3c6b318991";
        string script = Read("regional-flavor", scriptPath, scriptHash);
        string text = Read("viridian-region", "data/text/flavor_text.inc", "04564490bd4c5dfd447480a797fad714536b4d36c19a276e56dcf5336f9e1028");
        Read("pokemon-tower-region", "src/field_control_avatar.c", "305daa295f88ce682b2b1868700d2f76012c659cb0df2905e6177a51ee4e9b8d");
        Read("pokemon-tower-region", "src/metatile_behavior.c", "a1491016f9b6ee56c38fd66bf890dfcb5d37c7dc0dfda9fe7c0a5282376b9042");
        Read("pokemon-tower-region", "include/constants/metatile_behaviors.h", "88955bf5c4b8d702d01d28161017362a2617ee37dce7ef77c0e288bda379da75");
        Read("viridian-region", "src/script.c", "1cc4aade1f821e5f218ba7c191b1217e5a3cf942d0bfd0f32f775ce1f808437c");
        Read("pokemon-tower-region", "data/event_scripts.s", "3b1bd10d26c0a74fb3452ab42fc9d14dfca785f8b71389db06c71f5dfdd50a27");
        var prior = sources.SingleOrDefault(s => s.Path == scriptPath);
        if (prior is not null && (prior.Text != script || prior.SourceRef.Sha256 != scriptHash))
            throw new InvalidDataException("regional_flavor_script_conflict");
        if (prior is null) sources.Add(new(scriptPath, script,
            new(SourceKind.Pret, scriptPath, "regional-flavor", "pret/pokefirered", PretWorldMap.SourceCommit, scriptHash)));
        if (!textFiles.Contains(text, StringComparer.Ordinal)) textFiles.Add(text);
        hashes.Add("regional-flavor-application:fr-regional-flavor-v1");
        hashes.Add("regional-ordinary-gender-v1");
        hashes.Add("regional-sign-walkaway-v1");
    }

    private static void ValidateRegionalFlavorComposition(ImmutableDictionary<MapId, PretWorldMap> maps,
        PretStoryCompilation scripts, ImmutableDictionary<string, string>.Builder texts)
    {
        if (!RegionalFlavorMaps.SetEquals(maps.Values.Select(m => m.SourceMapSymbol).Where(RegionalFlavorMaps.Contains)))
            throw new InvalidDataException("regional_flavor_map_scope");
        foreach (string label in RegionalFlavorRules.Scripts)
        {
            var program = scripts.FindProgram(label) ?? throw new InvalidDataException("regional_flavor_program_missing: " + label);
            if (program.Instructions.Length != 2 || program.Instructions[0] is not { Op: StoryOp.Dialog, Opcode: "msgbox" } message ||
                message.Source.Path != "data/scripts/flavor_text.inc" || message.Args.Length != 2 ||
                message.Args[1] != "MSGBOX_SIGN" || !texts.ContainsKey(message.Args[0]) || program.Instructions[1].Op != StoryOp.End)
                throw new InvalidDataException("regional_flavor_program_contract: " + label);
        }
    }

    private static FieldBackgroundTrigger RegionalBackgroundTrigger(JsonElement source, bool enabled)
    {
        int x = source.GetProperty("x").GetInt32(), y = source.GetProperty("y").GetInt32();
        int elevation = source.GetProperty("elevation").GetInt32();
        if (source.TryGetProperty("script", out var script))
        {
            Direction? facing = !enabled ? null : source.GetProperty("player_facing_dir").GetString() switch
            {
                "BG_EVENT_PLAYER_FACING_ANY" => null,
                "BG_EVENT_PLAYER_FACING_NORTH" => Direction.North,
                "BG_EVENT_PLAYER_FACING_SOUTH" => Direction.South,
                "BG_EVENT_PLAYER_FACING_EAST" => Direction.East,
                "BG_EVENT_PLAYER_FACING_WEST" => Direction.West,
                _ => throw new InvalidDataException("regional_background_facing_unsupported")
            };
            return new(x, y, elevation, script.GetString()!) { PlayerFacing = facing };
        }
        return new(x, y, elevation, "EventScript_HiddenItemScript", source.GetProperty("item").GetString(),
            source.GetProperty("flag").GetString(), source.GetProperty("quantity").GetInt32())
        { Underfoot = enabled && source.GetProperty("underfoot").GetBoolean() };
    }
}
