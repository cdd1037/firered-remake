using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool TryApplyMapCoordinateEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode is not ("getplayerxy" or "setescapewarp")) return false;
        var map = _content.MapDefinition(state.World.Player.MapId);
        var transition = map.Programs.OnTransition is { } label ? _content.FindProgram(label) : null;
        if (transition is null || !transition.Instructions.Any(i => i.Source.Path == effect.Source.Path)) return false;
        var args = effect.Args;
        if (effect.Opcode == "getplayerxy")
        {
            if (args.Length != 2 || args.Any(a => !a.StartsWith("VAR_", StringComparison.Ordinal)))
                throw new InvalidDataException("map_player_coordinates_operands");
            next = state with { Script = state.Script with { Variables = state.Script.Variables
                .SetItem(args[0], state.World.Player.X).SetItem(args[1], state.World.Player.Y) } };
            return true;
        }
        if (args.Length is not (3 or 4) || args.Length == 4 && SeviiValue(state, args[1]) != 255)
            throw new InvalidDataException("map_escape_warp_operands");
        var destination = new EscapeRopeDestination(_content.MapFromSourceSymbol(args[0]),
            SeviiValue(state, args[^2]), SeviiValue(state, args[^1]));
        if (!_content.EscapeRoutes.Exits.TryGetValue(map.Id, out var exits) || !exits.Contains(destination))
            throw new InvalidDataException("map_escape_warp_destination");
        next = state with { EscapeDestination = destination };
        return true;
    }
}
