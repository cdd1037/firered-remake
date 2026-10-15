using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretPokemonTowerRegion
{
    public PretTowerPurifiedGround PurifiedGround { get; }
    public PretTowerFog Fog { get; }
    public PretTowerMarowakGate MarowakGate { get; }
    public PretTowerFujiRescue FujiRescue { get; }
    public PretTowerFluteReward FluteReward { get; }
    public PretTowerScriptedMarowak ScriptedMarowak { get; }
    public ImmutableArray<PretTowerGhostMode> GhostModes { get; }
    public ImmutableArray<PretTowerCoordinateTrigger> RivalTriggers { get; }
    public ImmutableArray<PretTowerRivalBranch> RivalBranches { get; }
    public ImmutableArray<PretTowerPickup> Pickups { get; }

    private PretPokemonTowerRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        PretRuntimeMapData house, ImmutableArray<PretWorldSourceFile> files, ImmutableDictionary<string, int> constants,
        ImmutableHashSet<string> outputs, JsonElement contract, JsonElement city)
    {
        GeneratedDirectory = directory; NewMaps = maps; ExistingHouse = house; SourceFiles = files; Constants = constants; _outputs = outputs; SourceContract = contract; ExternalLavenderEvents = city;
        InitiallySetFlags = [constants["FLAG_HIDE_POKEHOUSE_FUJI"]];
        var deps = Dependencies; var mechanics = Mechanics;
        SourceRef Source(string path, string symbol)
        {
            return new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit);
        }
        SourceRef Ref(JsonElement s)
        {
            var source = Source(s.GetProperty("path").GetString()!, s.GetProperty("symbol").GetString()!);
            Need(s.GetProperty("kind").GetString() == "pret" , "Tower source reference differs.");
            return source;
        }
        ImmutableArray<PretTowerCoordinateTrigger> Triggers(int floor) => maps[FloorMapId(floor)].Events.GetProperty("coord_events").EnumerateArray().Select((e, index) =>
            new PretTowerCoordinateTrigger(FloorMapId(floor), index, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
                e.GetProperty("var").GetString()!, constants[e.GetProperty("var").GetString()!], int.Parse(e.GetProperty("var_value").GetString()!, CultureInfo.InvariantCulture),
                e.GetProperty("script").GetString()!, maps[FloorMapId(floor)].SourceRef)).ToImmutableArray();
        var purified = contract.GetProperty("purifiedGround"); var healing = mechanics.GetProperty("healing");
        var triggers = Triggers(5);
        Need(JsonElement.DeepEquals(maps[FloorMapId(5)].Events.GetProperty("coord_events"), purified.GetProperty("triggers")), "Purified trigger source differs.");
        PurifiedGround = new(FloorMapId(5), constants[purified.GetProperty("temporaryVariable").GetString()!],
            healing.GetProperty("purifiedCoordinates").EnumerateArray().Select(p => (p[0].GetInt32(), p[1].GetInt32())).ToImmutableArray(),
            triggers.Where(t => t.Script == purified.GetProperty("entryScript").GetProperty("label").GetString()).ToImmutableArray(),
            triggers.Where(t => t.Script == purified.GetProperty("exitScript").GetProperty("label").GetString()).ToImmutableArray(),
            healing.GetProperty("fullPartyHpPpStatus").GetBoolean(), healing.GetProperty("ppBonusesApplied").GetBoolean(),
            healing.GetProperty("updatesLastHealLocation").GetBoolean(), Ref(healing.GetProperty("sources")[0]));
        Need(PurifiedGround.EntryTriggers.Length == 8 && PurifiedGround.ResetTriggers.Length == 9 && PurifiedGround.NoEncounterCells.Length == 9 &&
            PurifiedGround.NoEncounterCells.All(p => maps[FloorMapId(5)].GetSourceCell(p.X, p.Y)!.EncounterType == 0), "Purified source terrain/trigger scope differs.");

        var fog = contract.GetProperty("fog");
        Fog = new(fog.GetProperty("floors").EnumerateArray().Select(f => FloorMapId(f.GetInt32())).ToImmutableArray(),
            constants[fog.GetProperty("weather").GetString()!], 4, 0, 16, 12, 8, 3, 5, 64, 255,
            fog.GetProperty("dependsOnSilphScope").GetBoolean(), false,
            Path.Combine(directory, "presentation/fog-horizontal-source.png"), Ref(fog.GetProperty("sourceRef")));
        Need(Fog.MapIds.SequenceEqual(Enumerable.Range(3, 5).Select(FloorMapId)) && Fog.MapIds.All(id => maps[id].Events.GetProperty("weather").GetString() == "WEATHER_FOG_HORIZONTAL"), "Tower fog map declaration differs.");

        var gate = contract.GetProperty("marowakGate"); var gateTriggers = Triggers(6);
        Need(JsonElement.DeepEquals(maps[FloorMapId(6)].Events.GetProperty("coord_events"), gate.GetProperty("triggers")), "Marowak trigger source differs.");
        MarowakGate = new(constants[gate.GetProperty("variable").GetString()!], 0, gate.GetProperty("successValue").GetInt32(),
            constants[gate.GetProperty("successOutcome").GetString()!], true, Direction.North, true, gateTriggers,
            Source("src/battle_setup.c", "CB2_EndMarowakBattle"));
        var m = deps.GetProperty("marowak");
        ScriptedMarowak = new(m.GetProperty("species").GetString()!, m.GetProperty("level").GetInt32(), m.GetProperty("gender").GetString()!,
            m.GetProperty("fixedIv").GetInt32(), m.GetProperty("nature").GetString()!, m.GetProperty("heldItem").GetString()!,
            m.GetProperty("moves").EnumerateArray().Select(x => x.GetString()!).ToImmutableArray(), true, true);
        var ghost = constants["BATTLE_TYPE_GHOST"]; var unveiled = constants["BATTLE_TYPE_GHOST_UNVEILED"];
        GhostModes = [new(PretTowerGhostModeKind.DisguisedWild, ghost, true, false, false, true, true, true, true),
            new(PretTowerGhostModeKind.OrdinaryWild, 0, false, true, true, false, true, false, true),
            new(PretTowerGhostModeKind.DisguisedMarowak, ghost, true, false, false, true, true, true, false),
            new(PretTowerGhostModeKind.RevealedMarowak, ghost | unveiled, false, true, true, true, true, false, false)];

        var rescue = contract.GetProperty("fujiRescue"); var exits = contract.GetProperty("dynamicExit"); var warp = exits.GetProperty("rescueWarp");
        Need(!exits.GetProperty("hasMapDynamicWarp").GetBoolean(), "Tower must not use elevator-style dynamic exits.");
        var rescueSource = Source("data/maps/PokemonTower_7F/scripts.inc", "PokemonTower_7F_EventScript_MrFuji");
        FujiRescue = new(rescue.GetProperty("setFlags").EnumerateArray().Select(x => constants[x.GetString()!]).ToImmutableArray(),
            rescue.GetProperty("clearFlags").EnumerateArray().Select(x => constants[x.GetString()!]).ToImmutableArray(), constants[rescue.GetProperty("initialHidden").GetString()!],
            new(MapIdFromSourceSymbol(warp.GetProperty("map").GetString()!), warp.GetProperty("warpId").GetInt32(), warp.GetProperty("x").GetInt32(), warp.GetProperty("y").GetInt32(),
                !mechanics.GetProperty("escape").GetProperty("fujiCoordinateWarpMustNotReplaceEscapeAnchor").GetBoolean(), rescueSource),
            MapIdFromSourceSymbol(exits.GetProperty("houseExteriorReturn").GetProperty("map").GetString()!), exits.GetProperty("houseExteriorReturn").GetProperty("warpId").GetInt32(), false,
            rescueSource.Symbol, rescueSource);
        var reward = deps.GetProperty("reward"); var rewardSource = Source("data/maps/LavenderTown_VolunteerPokemonHouse/scripts.inc", "LavenderTown_VolunteerPokemonHouse_EventScript_MrFuji");
        FluteReward = new(HouseMapId, rewardSource.Symbol, reward.GetProperty("item").GetString()!, constants[reward.GetProperty("item").GetString()!],
            reward.GetProperty("quantity").GetInt32(), constants[reward.GetProperty("flag").GetString()!], reward.GetProperty("checkCapacityBeforeFlag").GetBoolean(),
            false, true, true, true, true, true, rewardSource);
        RivalTriggers = Triggers(2);
        RivalBranches = contract.GetProperty("rivalBranches").EnumerateArray().Select(b => new PretTowerRivalBranch(b.GetProperty("starterVar").GetInt32(),
            b.GetProperty("trainer").GetString()!, constants[b.GetProperty("trainer").GetString()!])).ToImmutableArray();
        Pickups = deps.GetProperty("pickups").EnumerateArray().Select(p =>
        {
            var hidden = p.GetProperty("kind").GetString() == "hidden"; var e = p.GetProperty(hidden ? "event" : "actor");
            var underfoot = hidden && e.GetProperty("underfoot").GetBoolean();
            return new PretTowerPickup(MapIdFromSourceSymbol(p.GetProperty("map").GetString()!), hidden ? null : p.GetProperty("localId").GetInt32(),
                hidden ? p.GetProperty("eventIndex").GetInt32() : null, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
                p.GetProperty("item").GetString()!, p.GetProperty("quantity").GetInt32(), e.GetProperty("flag").GetString()!, constants[e.GetProperty("flag").GetString()!],
                hidden, underfoot, underfoot, hidden ? null : e.GetProperty("script").GetString()!, Ref(p.GetProperty(hidden ? "sourceRef" : "scriptSource")));
        }).ToImmutableArray();
        Need(Pickups.Length == 10 && Pickups.Count(p => p.Underfoot) == 1 && Pickups.Single(p => p.Underfoot).ItemSymbol == "ITEM_SOOTHE_BELL", "Tower pickup inventory differs.");
        var npcGraphics = NpcSources.GetProperty("graphics").EnumerateArray()
            .Select(n => n.GetProperty("graphicsId").GetString()!).ToHashSet(StringComparer.Ordinal);
        Need(npcGraphics.Count == 15 && maps.Values.SelectMany(m => m.Events.GetProperty("object_events").EnumerateArray())
            .Concat(house.Events.GetProperty("object_events").EnumerateArray()).All(e => npcGraphics.Contains(e.GetProperty("graphics_id").GetString()!)), "Tower NPC source closure incomplete.");
    }
}
