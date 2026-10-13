using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string FiveIslandWarehouseMap = "map.five-island-rocket-warehouse";
    private const string FiveIslandMeadowMap = "map.five-island-meadow";
    private const string FiveIslandWarehouseSource = "data/maps/FiveIsland_RocketWarehouse/scripts.inc";
    private const string FiveIslandMeadowSource = "data/maps/FiveIsland_Meadow/scripts.inc";
    private const string FiveIslandDoorFlag = "FLAG_UNLOCKED_ROCKET_WAREHOUSE";
    private const string FiveIslandRocketsFlag = "FLAG_DEFEATED_ROCKETS_IN_WAREHOUSE";
    private const string FiveIslandSapphireFlag = "FLAG_RECOVERED_SAPPHIRE";
    private static bool FiveIslandMap(MapId map) => map.Value == "map.five-island" ||
        map.Value.StartsWith("map.five-island-", StringComparison.Ordinal);
    private static bool FiveIslandEditedMap(MapId map) => map.Value is FiveIslandMeadowMap or FiveIslandWarehouseMap;
    private static bool FiveIslandSource(string path) => path.StartsWith("data/maps/FiveIsland", StringComparison.Ordinal);
    private bool InFiveIslandScript(CampaignState state) => _content.FiveIslandEnabled && state.Script.Stack.Any(f =>
        _content.FindProgram(f.ProgramId)?.Instructions.Any(i => FiveIslandSource(i.Source.Path)) == true);

    private ImmutableSortedDictionary<string, FieldMetatileEdit> FiveIslandExpectedEdits(CampaignState state, MapId? mapId = null)
    {
        var map = mapId ?? state.World.Player.MapId;
        string? program = map.Value == FiveIslandMeadowMap && state.Script.Flags.GetValueOrDefault(FiveIslandDoorFlag)
            ? "FiveIsland_Meadow_EventScript_SetWarehouseDoorUnlocked"
            : map.Value == FiveIslandWarehouseMap && state.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_ADMIN")
                ? "FiveIsland_RocketWarehouse_EventScript_SetArrowsForReEntry" : null;
        var result = ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty;
        if (program is null) return result;
        var source = _content.FindProgram(program) ?? throw new InvalidDataException("five_island_metatile_program");
        foreach (var instruction in source.Instructions.Where(i => i.Opcode == "setmetatile"))
        {
            var a = instruction.Args;
            int x = SeviiValue(state, a[0]), y = SeviiValue(state, a[1]), tile = SeviiValue(state, a[2]);
            var cell = _content.Maps[map].GetTile(x, y) ?? throw new InvalidDataException("five_island_metatile_cell");
            if (_content.MetatileDefinition(map, tile) is null) throw new InvalidDataException("five_island_metatile_definition");
            result = result.Add(x + ":" + y, new(x, y, tile, SeviiValue(state, a[3]) == 0 ? 0 : 3, cell.Elevation));
        }
        if (result.Count != (map.Value == FiveIslandMeadowMap ? 1 : 2)) throw new InvalidDataException("five_island_metatile_scope");
        return result;
    }

    private bool TryApplyFiveIslandFieldEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.FiveIslandEnabled || !FiveIslandSource(effect.Source.Path)) return false;
        bool warehouse = effect.Source.Path == FiveIslandWarehouseSource;
        bool meadow = effect.Source.Path == FiveIslandMeadowSource;
        if (!warehouse && !meadow) return false;
        if (state.World.Player.MapId.Value != (warehouse ? FiveIslandWarehouseMap : FiveIslandMeadowMap))
            throw new InvalidDataException("five_island_effect_map_context");
        var a = effect.Args;
        if (effect.Opcode == "call_if_defeated" && warehouse)
        {
            if (effect.Source.Symbol != "FiveIsland_RocketWarehouse_OnLoad" ||
                !a.SequenceEqual(new[] { "TRAINER_TEAM_ROCKET_ADMIN", "FiveIsland_RocketWarehouse_EventScript_SetArrowsForReEntry" }))
                throw new InvalidDataException("five_island_trainer_branch_source");
            if (state.BattledTrainers.Contains("pret:" + a[0]))
                next = state with { Script = state.Script with { Stack = state.Script.Stack.Add(new(a[1], 0)) } };
            return true;
        }
        if (effect.Opcode == "getplayerxy" && warehouse)
        {
            if (!state.Script.Locked || effect.Source.Symbol is not
                ("FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin1" or "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin2") ||
                !a.SequenceEqual(new[] { "VAR_0x8004", "VAR_0x8005" })) throw new InvalidDataException("five_island_playerxy_source");
            next = state with { Script = state.Script with { Variables = state.Script.Variables
                .SetItem(a[0], state.World.Player.X).SetItem(a[1], state.World.Player.Y) } };
            return true;
        }
        if (effect.Opcode == "specialvar" && warehouse && a.Length == 2 && a[1] == "GetPlayerFacingDirection")
        {
            if (!state.Script.Locked || effect.Source.Symbol != "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin1" || a[0] != "VAR_RESULT")
                throw new InvalidDataException("five_island_facing_source");
            int direction = state.World.Player.Facing switch { Direction.North => 2, Direction.South => 1, Direction.West => 3, Direction.East => 4,
                _ => throw new InvalidDataException("five_island_facing") };
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(a[0], direction) } };
            return true;
        }
        if (effect.Opcode == "setmetatile")
        {
            string program = meadow ? "FiveIsland_Meadow_EventScript_SetWarehouseDoorUnlocked" : "FiveIsland_RocketWarehouse_EventScript_SetArrowsForReEntry";
            if (effect.Source.Symbol != program) throw new InvalidDataException("five_island_metatile_source");
            var instruction = _content.FindProgram(program)?.Instructions.SingleOrDefault(i => i.Source == effect.Source);
            if (instruction is null || !instruction.Args.SequenceEqual(a)) throw new InvalidDataException("five_island_metatile_operands");
            int x = SeviiValue(state, a[0]), y = SeviiValue(state, a[1]);
            var expected = FiveIslandExpectedEdits(state);
            if (!expected.TryGetValue(x + ":" + y, out var edit)) throw new InvalidDataException("five_island_metatile_progress");
            next = state with { MapEdits = (state.MapEdits ?? ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty).SetItem(x + ":" + y, edit) };
            return true;
        }
        if (effect.Opcode == "special" && a.SequenceEqual(new[] { "DrawWholeMapView" }))
        {
            if (!state.Script.Locked || effect.Source.Symbol != (meadow ? "FiveIsland_Meadow_EventScript_OpenWarehouseDoor" : "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin1") ||
                !(state.MapEdits ?? ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty).SequenceEqual(FiveIslandExpectedEdits(state)))
                throw new InvalidDataException("five_island_redraw_context");
            return true;
        }
        return false;
    }
}
