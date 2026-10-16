using Game.Domain;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private FieldBackgroundTrigger? InteractionBackgroundAt(CampaignState state, int x, int y,
        Direction facing)
    {
        var player = state.World.Player;
        bool enabled = _content.SupportsRegionalFlavor(player.MapId);
        var candidate = _content.MapDefinition(player.MapId).BackgroundTriggers.FirstOrDefault(t =>
            t.X == x && t.Y == y && (t.Elevation == 0 || t.Elevation == player.Elevation));
        if(candidate is not null&&(candidate.Underfoot||ItemfinderSource?.Underfoot.Any(i=>i.Map==_content.MapDefinition(player.MapId).SourceMapSymbol&&i.X==candidate.X&&i.Y==candidate.Y)==true))return null;
        return enabled && candidate is not null && (candidate.Underfoot ||
            candidate.PlayerFacing is { } required && required != facing ||
            candidate.HiddenFlag is { } flag && state.Script.Flags.GetValueOrDefault(flag)) ? null : candidate;
    }

    private CampaignState StartBackgroundStory(CampaignState state, FieldBackgroundTrigger background) =>
        StartStory(_content.SupportsRegionalFlavor(state.World.Player.MapId) ? RegionalFacing(state) : state, background.ScriptId);

    // Source TrySetUpWalkIntoSignpostScript is distinct from the A lookup: a
    // scripted signpost ignores BG facing but still matches coordinates/elevation.
    private CampaignState? RegionalWalkIntoSign(CampaignState state, int x, int y)
    {
        var player = state.World.Player;
        if (!_content.SupportsRegionalFlavor(player.MapId) || player.Facing is Direction.East or Direction.West) return null;
        int behavior = SourceBehavior(state, x, y) ?? -1;
        string? script = behavior is 0x87 or 0x88 ? RegionalFlavorRules.ScriptFor(behavior, player.Facing) :
            behavior == 0x84 ? _content.MapDefinition(player.MapId).BackgroundTriggers.FirstOrDefault(t =>
                t.X == x && t.Y == y && (t.Elevation == 0 || t.Elevation == player.Elevation))?.ScriptId : null;
        if (script is null) return null;
        var next = RegionalFacing(state) with { SignMessage = new(Cancelable: true, InhibitTicks: 6, SignStyle: true) };
        return Pump(StartStory(next, script));
    }

    // Source GetInteractionScript: actors (including the counter reach) have already
    // been selected; an ineligible background event must allow the tile fallback.
    private Decision<CampaignState> DecideRegionalInteraction(CampaignState state,
        FieldBackgroundTrigger? background, int x, int y)
    {
        var player = state.World.Player;
        if (!_content.SupportsRegionalFlavor(player.MapId))
            throw new InvalidDataException("regional_flavor_context_invalid");
        if (background?.HiddenItem is not null)
            return BeginHiddenPickup(RegionalFacing(state), background);
        int behavior = SourceBehavior(state, x, y) ?? -1;
        if (background is not null)
        {
            var context = RegionalFacing(state);
            if (behavior == 0x84 || behavior is 0x87 or 0x88 && player.Facing == Direction.North)
                context = context with { SignMessage = new(SignStyle: true) };
            return new(CommandStatus.Accepted, Pump(StartStory(context, background.ScriptId)));
        }
        // Keep the existing reviewed PC service, at its source metatile precedence.
        if (IsSupportedHealingCenter(player.MapId) && behavior == 0x83)
            return new(CommandStatus.Accepted, Pump(StartStory(RegionalFacing(state), CenterPcScript)));
        string? script = RegionalFlavorRules.ScriptFor(behavior, player.Facing);
        if (script is null || behavior == 0x85 && (_content.FindProgram(script) is null))
            return new(CommandStatus.Rejected, state, "nothing_to_interact");
        var next = RegionalFacing(state);
        // Source MsgSetSignpost applies to the exterior Center/Mart signs.
        if (behavior is 0x87 or 0x88) next = next with { SignMessage = new(SignStyle: true) };
        return new(CommandStatus.Accepted, Pump(StartStory(next, script)));
    }

    private static CampaignState RegionalFacing(CampaignState state) => state with
    {
        Script = state.Script with
        {
            Variables = state.Script.Variables.SetItem("VAR_FACING", SourceDirectionValue(state.World.Player.Facing))
        }
    };
}
