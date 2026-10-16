using Game.Domain.Options;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    /// <summary>Missing preferences have exactly the original new-game defaults.</summary>
    public SourceGameOptions EffectiveOptions => State.Options ?? SourceGameOptions.Defaults;

    public bool CanEditOptions => _content.PokemonStorageEnabled && IsStableFieldForOptions(State);

    private static bool IsStableFieldForOptions(CampaignState state) =>
        IsStableFieldForParty(state) && state.Pc is null;

    private string? ValidateOptionsState(CampaignState state)
    {
        if (state.Options is null) return null;
        if (!_content.PokemonStorageEnabled) return "save_options_profile_missing";
        return SourceGameOptions.Validate(state.Options);
    }

    private Decision<CampaignState> DecideOptions(CampaignState state, CampaignCommand command)
    {
        if (!_content.PokemonStorageEnabled) return new(CommandStatus.Rejected, state, "options_not_enabled");
        if (!IsStableFieldForOptions(state)) return new(CommandStatus.Rejected, state, "options_event_active");
        if (command.Options is not { } options) return new(CommandStatus.Rejected, state, "options_payload_required");
        if (SourceGameOptions.Validate(options) is { } error) return new(CommandStatus.Rejected, state, error);
        // The original menu commits every setting together when B / CANCEL closes it.
        // Keep default preferences absent so pre-existing saves and state hashes stay identical.
        SourceGameOptions? stored = options == SourceGameOptions.Defaults ? null : options;
        return new(CommandStatus.Accepted, state.Options == stored ? state : state with { Options = stored });
    }

    private ForestTrainerBattleStyle EffectiveBattleStyle(CampaignState state) =>
        _content.PokemonStorageEnabled && state.Options?.BattleStyle == SourceBattleStyle.Set
            ? ForestTrainerBattleStyle.Set : ForestTrainerBattleStyle.Shift;
}
