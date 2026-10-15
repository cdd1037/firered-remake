using Game.Domain.Events;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string TrainerFanClubMap = "map.saffron-city-pokemon-trainer-fan-club";
    private const string TrainerFanClubScript = "data/maps/SaffronCity_PokemonTrainerFanClub/scripts.inc";

    private static TrainerFanClubState ReadTrainerFanClub(CampaignState state) => new(
        checked((ushort)state.Script.Variables[TrainerFanClubRules.CounterVariable]),
        checked((ushort)state.Script.Variables[TrainerFanClubRules.LoseFanHoursVariable]),
        checked((ushort)state.Script.Variables[TrainerFanClubRules.SceneVariable]));

    private static CampaignState WriteTrainerFanClub(CampaignState state, TrainerFanClubState fans) => state with
    {
        Script = state.Script with { Variables = state.Script.Variables
            .SetItem(TrainerFanClubRules.CounterVariable, fans.PackedCounter)
            .SetItem(TrainerFanClubRules.LoseFanHoursVariable, fans.LoseFanHours)
            .SetItem(TrainerFanClubRules.SceneVariable, fans.Scene) }
    };

    private CampaignState InitializeTrainerFanClub(CampaignState state) => !_content.TrainerFanClubEnabled
        ? state : WriteTrainerFanClub(state, TrainerFanClubRules.Reset(TrainerFanClubState.Empty));

    private string? ValidateTrainerFanClub(CampaignState state)
    {
        if (!_content.TrainerFanClubEnabled) return null;
        if (!state.Script.Variables.ContainsKey(TrainerFanClubRules.CounterVariable) ||
            !state.Script.Variables.ContainsKey(TrainerFanClubRules.LoseFanHoursVariable) ||
            !state.Script.Variables.ContainsKey(TrainerFanClubRules.SceneVariable)) return "save_trainer_fan_club_missing";
        var fans = ReadTrainerFanClub(state);
        if (fans.Scene > 2 || fans.ActivityCounter > 20 || fans.LoseFanHours > 999 ||
            fans.GotInitialFans != (state.HallOfFame is not null) ||
            fans.GotInitialFans != (fans.Scene != 0) ||
            !fans.GotInitialFans && (fans.PackedCounter != 0 || fans.LoseFanHours != 0) ||
            fans.GotInitialFans && fans.FanFlags == 0 ||
            TrainerFanClubRules.FirstClearHideFlags.Any(f => state.Script.Flags.GetValueOrDefault(f) == fans.GotInitialFans))
            return "save_trainer_fan_club_invalid";
        return null;
    }

    private CampaignState UpdateTrainerFanClubGameClear(CampaignState state)
    {
        if (!_content.TrainerFanClubEnabled) throw new InvalidDataException("trainer_fan_club_catalog_missing");
        var result = TrainerFanClubRules.OnGameClear(ReadTrainerFanClub(state), state.PlayTime.Hours);
        var next = WriteTrainerFanClub(state, result.State);
        if (result.FirstFansInitialized) next = next with
        {
            Script = next.Script with { Flags = next.Script.Flags.SetItems(TrainerFanClubRules.FirstClearHideFlags
                .Select(flag => new KeyValuePair<string, bool>(flag, false))) }
        };
        return next;
    }

    private CampaignState TryGainTrainerFanFromLance(CampaignState state)
    {
        if (!_content.TrainerFanClubEnabled) throw new InvalidDataException("trainer_fan_club_catalog_missing");
        var result = TrainerFanClubRules.TryGainFromCounter(ReadTrainerFanClub(state), 0, state.World.Rng);
        return WriteTrainerFanClub(state, result.State) with { World = state.World with { Rng = result.Rng } };
    }

    private bool TryApplyTrainerFanClubEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Source.Path != TrainerFanClubScript || effect.Opcode is not ("special" or "specialvar")) return false;
        if (!_content.TrainerFanClubEnabled || state.World.Player.MapId.Value != TrainerFanClubMap)
            throw new InvalidDataException("trainer_fan_club_effect_context");
        var source = _content.FindProgram(effect.Source.Symbol)?.Instructions.SingleOrDefault(i =>
            i.Source == effect.Source && i.Opcode == effect.Opcode && i.Args.SequenceEqual(effect.Args));
        if (source is null) throw new InvalidDataException("trainer_fan_club_effect_source");
        var args = effect.Args;
        var fans = ReadTrainerFanClub(state);
        int member = state.Script.Variables.GetValueOrDefault("VAR_0x8004");
        if (effect.Opcode == "specialvar")
        {
            if (args.Length != 2 || args[0] != "VAR_RESULT") throw new InvalidDataException("trainer_fan_club_query_arguments");
            int value = args[1] switch
            {
                "Script_IsFanClubMemberFanOfPlayer" => TrainerFanClubRules.IsPlayerFan(fans, member) ? 1 : 0,
                "Script_GetNumFansOfPlayerInTrainerFanClub" => TrainerFanClubRules.CountFans(fans),
                _ => throw new InvalidDataException("trainer_fan_club_query_unsupported")
            };
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(args[0], value) } };
            return true;
        }
        if (args.Length != 1) throw new InvalidDataException("trainer_fan_club_special_arguments");
        switch (args[0])
        {
            case "Script_BufferFanClubTrainerName":
                // This single-player model has no link records or link-battle admission.
                // Do not replace the empty-history fallback with invented trainer names.
                next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1",
                    TrainerFanClubRules.BufferTrainerNameWithEmptyLinkHistory(member, state.RivalName)) };
                return true;
            case "Script_TryLoseFansFromPlayTime":
                if (effect.Source.Symbol != "SaffronCity_PokemonTrainerFanClub_EventScript_UpdateFanMemberPositions")
                    throw new InvalidDataException("trainer_fan_club_loss_source");
                var loss = TrainerFanClubRules.TryLoseFromPlayTime(fans, state.PlayTime.Hours, state.World.Rng);
                next = WriteTrainerFanClub(state, loss.State) with { World = state.World with { Rng = loss.Rng } };
                return true;
            default: throw new InvalidDataException("trainer_fan_club_special_unsupported");
        }
    }
}
