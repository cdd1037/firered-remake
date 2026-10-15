using System.Collections.Immutable;
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
            IsRegionalServiceMap(map) || SafariRegion?.SourceMaps.ContainsKey(map)==true || SilphRegion?.NewMaps.ContainsKey(map)==true || SaffronGymRegion?.NewMaps.ContainsKey(map)==true || ViridianGymRegion?.NewMaps.ContainsKey(map)==true || VictoryRoadRegion?.SourceMaps.ContainsKey(map)==true || PokemonLeagueRegion?.SourceMaps.ContainsKey(map)==true || SeviiRubyRegion?.SourceMaps.ContainsKey(map)==true || FourIslandRegion?.SourceMaps.ContainsKey(map)==true || SixIslandRegion?.SourceMaps.ContainsKey(map)==true || FiveIslandRegion?.SourceMaps.ContainsKey(map)==true || SevenIslandRegion?.SourceMaps.ContainsKey(map)==true || EventIslandRegion?.NewMaps.ContainsKey(map)==true || PowerPlantRegion?.NewMaps.ContainsKey(map)==true || TrainerTowerRegion?.Maps.ContainsKey(map)==true || FightingDojoRegion?.NewMaps.ContainsKey(map)==true || FossilLabRegion?.NewMaps.ContainsKey(map)==true || NpcTradeHaden.NewMaps.ContainsKey(map) || GoodRodService.NewMaps.ContainsKey(map) || SeafoamRegion.NewMaps.ContainsKey(map) || MoveReminderHouse.NewMaps.ContainsKey(map) || MoveDeleterHouse.NewMaps.ContainsKey(map) || OriginalTutorRooms.NewMaps.ContainsKey(map) || CopycatHouse.NewMaps.ContainsKey(map) || SuperRodService.NewMaps.ContainsKey(map) || CapeBrinkTutor.NewMaps.ContainsKey(map) || Route16FlyHouse.NewMaps.ContainsKey(map) || Route5DayCare.NewMaps.ContainsKey(map) || FourIslandDayCare.NewMaps.ContainsKey(map) || OriginalTmAcquisition.NewMaps.ContainsKey(map) || SchoolHouseInteriors.NewMaps.ContainsKey(map) || LostCaveRegion.NewMaps.ContainsKey(map));

    private static readonly ImmutableHashSet<string> RegionalFlavorMaps = new[]
    {
        "MAP_CELADON_CITY","MAP_CELADON_CITY_GAME_CORNER","MAP_CELADON_CITY_GYM","MAP_CELADON_CITY_POKEMON_CENTER_1F","MAP_CELADON_CITY_POKEMON_CENTER_2F","MAP_LAVENDER_TOWN","MAP_LAVENDER_TOWN_HOUSE1","MAP_LAVENDER_TOWN_MART","MAP_LAVENDER_TOWN_POKEMON_CENTER_1F","MAP_LAVENDER_TOWN_POKEMON_CENTER_2F","MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE","MAP_POKEMON_TOWER_1F","MAP_POKEMON_TOWER_2F","MAP_POKEMON_TOWER_3F","MAP_POKEMON_TOWER_4F","MAP_POKEMON_TOWER_5F","MAP_POKEMON_TOWER_6F","MAP_POKEMON_TOWER_7F","MAP_ROUTE7","MAP_ROUTE7_EAST_ENTRANCE","MAP_ROUTE8","MAP_ROUTE8_WEST_ENTRANCE","MAP_UNDERGROUND_PATH_EAST_ENTRANCE","MAP_UNDERGROUND_PATH_EAST_WEST_TUNNEL","MAP_UNDERGROUND_PATH_WEST_ENTRANCE"
    }.ToImmutableHashSet(StringComparer.Ordinal);

    private static void AppendRegionalFlavorSources(string root,List<PretSourceScriptFile> sources,List<string> textFiles)
    {
        string Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, PretSourcePaths.FireRedRawDirectory, path));
            return Encoding.UTF8.GetString(bytes);
        }
        const string scriptPath = "data/scripts/flavor_text.inc";
        string script = Read(scriptPath);
        string text = Read("data/text/flavor_text.inc");
        Read("src/field_control_avatar.c");
        Read("src/metatile_behavior.c");
        Read("include/constants/metatile_behaviors.h");
        Read("src/script.c");
        Read("data/event_scripts.s");
        var prior = sources.SingleOrDefault(s => s.Path == scriptPath);
        if (prior is not null && (prior.Text != script ))
            throw new InvalidDataException("regional_flavor_script_conflict");
        if (prior is null) sources.Add(new(scriptPath, script,
            new(SourceKind.Pret,scriptPath,"regional-flavor","pret/pokefirered",PretWorldMap.SourceCommit)));
        if (!textFiles.Contains(text, StringComparer.Ordinal)) textFiles.Add(text);
    }

    private static void ValidateRegionalFlavorComposition(ImmutableDictionary<MapId, PretWorldMap> maps,PretStoryCompilation scripts,ImmutableDictionary<string, string>.Builder texts)
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

    private static FieldBackgroundTrigger RegionalBackgroundTrigger(JsonElement source,bool enabled)
    {
        int x = source.GetProperty("x").GetInt32(),y = source.GetProperty("y").GetInt32();
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
