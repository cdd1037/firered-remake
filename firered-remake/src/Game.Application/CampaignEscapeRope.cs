using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignEscapeRope(TilePosition Arrival);

public sealed partial class CampaignSession
{
    private string? ValidateEscapeRopeState(CampaignState state)
    {
        if (state.EscapeRope is not null) return "save_event_active";
        if (!_content.MtMoonEnabled)
            return state.EscapeDestination is null ? null : "save_escape_rope_profile_missing";
        return _content.FiveIslandEnabled?FiveIslandEscapeRopeRules.ValidateDestination(state.EscapeDestination):_content.SixIslandEnabled?SixIslandEscapeRopeRules.ValidateDestination(state.EscapeDestination):_content.FourIslandEnabled?FourIslandEscapeRopeRules.ValidateDestination(state.EscapeDestination):_content.SeviiRubyEnabled?SeviiRubyEscapeRopeRules.ValidateDestination(state.EscapeDestination):_content.VictoryRoadEnabled?VictoryRoadEscapeRopeRules.ValidateDestination(state.EscapeDestination):_content.FirstSeviiEnabled?FirstSeviiEscapeRopeRules.ValidateDestination(state.EscapeDestination):_content.CinnabarEnabled?CinnabarEscapeRopeRules.ValidateDestination(state.EscapeDestination):EscapeRopeRules.ValidateDestination(state.EscapeDestination,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,pokemonTowerRules:_content.PokemonTowerEnabled,silphRules:_content.SilphEnabled);
    }

    private CampaignState RecordEscapeWarp(CampaignState before, CampaignState moved, Direction direction)
    {
        if (!_content.MtMoonEnabled) return moved;
        var p = before.World.Player;
        var map = _content.Maps[p.MapId];
        // Arrow exits trigger from the occupied tile; doors/ladders trigger from the front tile.
        if (map.SourceBehaviorAt(p.X, p.Y) is not (>= 0x62 and <= 0x65))
        {
            var (dx, dy) = Delta(direction);
            p = p with { X = p.X + dx, Y = p.Y + dy };
        }
        return moved with { EscapeDestination = _content.FiveIslandEnabled?FiveIslandEscapeRopeRules.RecordWarp(before.EscapeDestination,p with{Facing=direction},moved.World.Player.MapId):_content.SixIslandEnabled?SixIslandEscapeRopeRules.RecordWarp(before.EscapeDestination,p with{Facing=direction},moved.World.Player.MapId):_content.FourIslandEnabled?FourIslandEscapeRopeRules.RecordWarp(before.EscapeDestination,p with{Facing=direction},moved.World.Player.MapId):_content.SeviiRubyEnabled?SeviiRubyEscapeRopeRules.RecordWarp(before.EscapeDestination,p with{Facing=direction},moved.World.Player.MapId):_content.VictoryRoadEnabled?VictoryRoadEscapeRopeRules.RecordWarp(before.EscapeDestination,p with{Facing=direction},moved.World.Player.MapId):_content.FirstSeviiEnabled?FirstSeviiEscapeRopeRules.RecordWarp(before.EscapeDestination,p with{Facing=direction},moved.World.Player.MapId):_content.CinnabarEnabled?CinnabarEscapeRopeRules.RecordWarp(before.EscapeDestination,p with{Facing=direction},moved.World.Player.MapId):EscapeRopeRules.RecordWarp(before.EscapeDestination,
            p with { Facing = direction }, moved.World.Player.MapId,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,pokemonTowerRules:_content.PokemonTowerEnabled,silphRules:_content.SilphEnabled) };
    }

    private Decision<CampaignState> UseFieldEscapeRope(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!_content.MtMoonEnabled || state.ItemMenu is not { Origin: "bag", View: "bag" } menu ||
            command.Text != EscapeRopeRules.Item.Value || command.Quantity != 1 || command.MoveSlot != -1)
            return No("field_escape_rope_context_invalid");
        if (ValidateCampaignInventory(state.Inventory) is { } error) return No(error);
        var use = _content.FiveIslandEnabled?FiveIslandEscapeRopeRules.Use(FiveIslandShopProof,SevenIslandPredecessorInventory(ShopInventoryView(state.Inventory!)),state.World.Player.MapId,state.EscapeDestination,PotionUseContext.Field):_content.SixIslandEnabled?SixIslandEscapeRopeRules.Use(SixIslandShopCatalog.For(ShopCatalog)!,ShopInventoryView(state.Inventory!),state.World.Player.MapId,state.EscapeDestination,PotionUseContext.Field):_content.FourIslandEnabled?FourIslandEscapeRopeRules.Use(FourIslandShopCatalog.For(ShopCatalog)!,ShopInventoryView(state.Inventory!),state.World.Player.MapId,state.EscapeDestination,PotionUseContext.Field):_content.SeviiRubyEnabled?SeviiRubyEscapeRopeRules.Use(SeviiRubyShopCatalog.For(ShopCatalog)!,ShopInventoryView(state.Inventory!),state.World.Player.MapId,state.EscapeDestination,PotionUseContext.Field):_content.VictoryRoadEnabled?VictoryRoadEscapeRopeRules.Use(VictoryRoadShopProof,ShopInventoryView(state.Inventory!),state.World.Player.MapId,state.EscapeDestination,PotionUseContext.Field):_content.FirstSeviiEnabled?FirstSeviiEscapeRopeRules.Use(FirstSeviiShopProof,ShopInventoryView(state.Inventory!),state.World.Player.MapId,state.EscapeDestination,PotionUseContext.Field):_content.CinnabarEnabled?CinnabarEscapeRopeRules.Use(CinnabarShopProof,ShopInventoryView(state.Inventory!),state.World.Player.MapId,state.EscapeDestination,PotionUseContext.Field):EscapeRopeRules.Use(ShopCatalog, ShopInventoryView(state.Inventory!),
            state.World.Player.MapId, state.EscapeDestination, PotionUseContext.Field,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,pokemonTowerRules:_content.PokemonTowerEnabled,silphRules:_content.SilphEnabled);
        if (!use.Succeeded) return No(use.Error!);
        var arrival = use.Arrival!;
        if (!_content.Maps.TryGetValue(arrival.MapId, out var map) ||
            map.GetTile(arrival.X, arrival.Y) is not { Blocked: false } tile || tile.Elevation != arrival.Elevation)
            return No("escape_rope_arrival_invalid");
        _ = checked(state.World.Revision + 1);
        _ = _content.TextPages("gText_PlayerUsedVar2");
        // Source removes the item before the used-item message. Its acknowledgement is a
        // locked continuation, not a second use and not a cancellable inventory choice.
        return new(CommandStatus.Accepted, state with
        {
            Inventory = MergeShopInventory(state.Inventory!, SevenIslandShopProof.RestoreFromPredecessor(ShopInventoryView(state.Inventory!),use.Inventory)),
            EscapeRope = new(arrival),
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", "ESCAPE ROPE"),
            ItemMenu = menu with { View = "message", MessageSymbol = "gText_PlayerUsedVar2", ReturnView = "bag" }
        });
    }

    private Decision<CampaignState> DecideEscapeRope(CampaignState state, CampaignCommand command)
    {
        if (command.Kind is not ("menu_ack" or "menu_back"))
            return new(CommandStatus.Rejected, state, "escape_rope_message_pending");
        if (!_content.MtMoonEnabled || state.EscapeRope is not { } escape ||
            state.ItemMenu is not { Origin: "bag", View: "message", MessageSymbol: "gText_PlayerUsedVar2" })
            throw new InvalidDataException("escape_rope_context_invalid");
        var flags = state.Script.Flags;
        foreach (string flag in new[] { "FLAG_SYS_ON_CYCLING_ROAD", "FLAG_SYS_CRUISE_MODE", "FLAG_SYS_SAFARI_MODE",
            "FLAG_SYS_USE_STRENGTH", "FLAG_SYS_FLASH_ACTIVE", "FLAG_SYS_QL_DEPARTED" }) flags = flags.SetItem(flag, false);
        var variables = state.Script.Variables;
        foreach (string variable in new[] { "VAR_MAP_SCENE_ROUTE16", "VAR_MAP_SCENE_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE",
            "VAR_QL_ENTRANCE" }) variables = variables.SetItem(variable, 0);
        var next = state with
        {
            EscapeRope = null, ItemMenu = null,
            Script = state.Script with { Flags = flags, Variables = variables },
            World = state.World with { Revision = checked(state.World.Revision + 1), Player = escape.Arrival },
            Exploration = ResetEncounterModifiers(state.Exploration!)
        };
        return new(CommandStatus.Accepted, Pump(EnterMap(next, escape.Arrival.Facing)));
    }
}
