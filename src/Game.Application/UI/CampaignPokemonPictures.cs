using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // A displayed source showmonpic can await A or B without a map-specific service.
    private Decision<CampaignState> DecidePokemonPictureButton(CampaignState state, CampaignCommand command)
    {
        if (state.MonPicture is null || state.Script.Wait is not { Kind: StoryWaitKind.ButtonPress } wait)
            return new(CommandStatus.Rejected, state, "picture_button_not_pending");
        if (command.Text is not ("A" or "B"))
            return new(CommandStatus.Rejected, state, "button_press_requires_a_or_b");
        var next = state with { PresentationEffects = [], PresentationTicks = 0, Motions = [] };
        return new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, wait.CorrelationId, _content))));
    }
}
