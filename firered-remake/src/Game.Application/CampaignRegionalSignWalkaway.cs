using Game.Domain;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool RegionalSignWalkawayActive => IsRegionalWalkawaySign(State);

    private bool IsRegionalWalkawaySign(CampaignState state) =>
        _content.RegionalFlavorEnabled && _content.SupportsRegionalFlavor(state.World.Player.MapId) &&
        state.SignMessage is { Cancelable: true } && !state.Script.Completed &&
        state.Script.Wait is not null && state.Dialogue is not null;

    // Fixed pret 037335f, field_control_avatar.c:301-332. Six source frames
    // decrement the timer; only a subsequent input frame may cancel the script.
    // Ready/idle frames are not commands and cannot grow a replay journal forever.
    private Decision<CampaignState> DecideRegionalSignWalkaway(CampaignState state, CampaignCommand command)
    {
        if (!IsRegionalWalkawaySign(state)) return new(CommandStatus.Rejected, state, "regional_sign_not_active");
        var sign = state.SignMessage!;
        if (command.Kind == "sign_tick")
            return sign.InhibitTicks is > 0 and <= 6
                ? new(CommandStatus.Accepted, state with { SignMessage = sign with { InhibitTicks = sign.InhibitTicks - 1 } })
                : new(CommandStatus.Rejected, state, "regional_sign_timer_not_active");
        if (sign.InhibitTicks != 0) return new(CommandStatus.Rejected, state, "regional_sign_inhibited");
        if (command.Text is not ("direction" or "start") || !Enum.IsDefined(command.Direction))
            return new(CommandStatus.Rejected, state, "regional_sign_input_invalid");
        bool differentDirection = command.Direction != state.World.Player.Facing;
        if (differentDirection && sign.WalkawayDisabled)
            return new(CommandStatus.Rejected, state, "regional_sign_walkaway_disabled");
        if (!differentDirection && command.Text != "start")
            return new(CommandStatus.Rejected, state, "regional_sign_same_direction");

        // event_scripts.s:1166-1169 replaces the running script with
        // DoPicboxCancel; release; end. Do not resume the interrupted script or
        // turn/step the player, roll encounters, or apply a queued A input.
        var next = state with
        {
            Script = state.Script with
            {
                Stack = [], Wait = null, Locked = false, Completed = true,
                Revision = checked(state.Script.Revision + 1)
            },
            Dialogue = null, SignMessage = null, MessagePointer = null, MonPicture = null,
            GiftDelivery = null, Motions = [], PresentationTicks = 0, PresentationEffects = []
        };
        return new(CommandStatus.Accepted, next);
    }
}
