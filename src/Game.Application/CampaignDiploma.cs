using Game.Domain.Events;

namespace Game.Application;

/// <summary>The designer's read-only native certificate. Completion never awards an item or flag.</summary>
public sealed record CampaignDiploma(bool National);

public sealed partial class CampaignSession
{
    private bool DiplomaWait(CampaignState state) =>
        InOrdinaryNativeActor(state, DesignerPath, DesignerPrefix + "Designer") &&
        state.Script.Wait?.Kind == StoryWaitKind.Presentation &&
        SizeRecordInstruction(state) is { Opcode: "waitstate" } instruction && instruction.Args.IsEmpty &&
        instruction.Source.Path == DesignerPath && instruction.Source.Symbol == DesignerPrefix + "ShowDiploma";

    private bool TryDecideDiploma(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        decision = new(CommandStatus.Rejected, state, "diploma_not_pending");
        if (state.Diploma is null) return command.Kind == "diploma_complete";
        if (!DiplomaWait(state)) return true;
        if (command.Kind != "diploma_complete")
        { decision = new(CommandStatus.Rejected, state, "diploma_active"); return true; }
        var next = state with { Diploma = null, Dialogue = null, SignMessage = null, ScreenIsBlack = false,
            Motions = [], PresentationEffects = [], PresentationTicks = 0 };
        decision = new(CommandStatus.Accepted, Pump(Advance(next,
            StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
        return true;
    }
}
