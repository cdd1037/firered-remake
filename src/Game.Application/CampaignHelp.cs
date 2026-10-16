using Game.Domain.Options;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool SawHelpIntroduction => State.Script.Flags.GetValueOrDefault("FLAG_SYS_SAW_HELP_SYSTEM_INTRO");
    // Native Help suspends normal callbacks; it is not restricted to save-safe field states.
    public bool CanOpenHelp => EffectiveOptions.ButtonMode == SourceButtonMode.Help && State.Diploma is null &&
        State.Evolution is null && State.HelpContext != "HELPCONTEXT_UNUSED";
    private Decision<CampaignState> DecideHelpIntroSeen(CampaignState state)
    {
        if ((state.Options ?? SourceGameOptions.Defaults).ButtonMode != SourceButtonMode.Help ||
            state.Diploma is not null || state.Evolution is not null || state.HelpContext == "HELPCONTEXT_UNUSED")
            return new(CommandStatus.Rejected, state, "help_unavailable");
        if (state.Script.Flags.GetValueOrDefault("FLAG_SYS_SAW_HELP_SYSTEM_INTRO")) return new(CommandStatus.Accepted, state);
        return new(CommandStatus.Accepted, state with { Script = state.Script with
            { Flags = state.Script.Flags.SetItem("FLAG_SYS_SAW_HELP_SYSTEM_INTRO", true) } });
    }
}
