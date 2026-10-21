using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private Decision<CampaignState> DecideStorageMove(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.Pc is not { View: CampaignPcView.Move } pc || state.Storage is not {} storage)
            return No("storage_move_cursor_context");
        if (ValidateStorageMoveOwnership(state) is {} invalid) return No(invalid);
        if (command.Kind == "storage_group_grab" || pc.GroupCursor is not null) return DecideStorageGroupMove(state, command);
        var content = _content.PokemonContent;
        PokemonStorageMoveResult result;
        switch (command.Kind)
        {
            case "storage_move_grab":
                if (command.StorageLocation is not {} source) return No("storage_move_location_missing");
                if (command.TargetInstanceId is not {} expectedSource) return No("storage_move_target_identity");
                result = PokemonStorageMoveRules.Grab(content, state.Party, storage, pc.MoveCursor, source, expectedSource);
                break;
            case "storage_move_place":
                if (command.StorageLocation is not {} place) return No("storage_move_location_missing");
                if (command.HeldInstanceId is not {} expectedPlaced) return No("storage_move_held_identity");
                result = PokemonStorageMoveRules.Place(content, state.Party, storage, pc.MoveCursor, place, expectedPlaced);
                break;
            case "storage_move_shift":
                if (command.StorageLocation is not {} shift) return No("storage_move_location_missing");
                if (command.HeldInstanceId is not {} expectedHeld) return No("storage_move_held_identity");
                if (command.TargetInstanceId is not {} expectedTarget) return No("storage_move_target_identity");
                result = PokemonStorageMoveRules.Shift(content, state.Party, storage, pc.MoveCursor, shift, expectedHeld, expectedTarget);
                break;
            case "storage_box":
                if (command.Choice is not {} box) return No("storage_box_index");
                result = PokemonStorageMoveRules.SelectBox(content, state.Party, storage, pc.MoveCursor, box);
                break;
            case "storage_move_hide_party":
                result = PokemonStorageMoveRules.CheckHideParty(content, state.Party, storage, pc.MoveCursor);
                break;
            case "pc_back":
            case "storage_move_close":
                result = PokemonStorageMoveRules.CheckExit(content, state.Party, storage, pc.MoveCursor);
                if (!result.Succeeded) return No(result.Diagnostic!.Code);
                // Return to the native five-row menu. That menu alone resumes the original
                // ShowPokemonStorageSystemPC wait and EventScript_PCMainMenu callback.
                return new(CommandStatus.Accepted, state with { Pc = pc with { View = CampaignPcView.StorageMenu } });
            default:
                return No("storage_move_active");
        }
        if (!result.Succeeded) return No(result.Diagnostic!.Code);
        var next = state with { Party = result.Party, Storage = result.Storage, Pc = pc with { MoveCursor = result.Cursor } };
        if (ValidateStorageMoveOwnership(next) is {} error) return No(error);
        return new(CommandStatus.Accepted, next);
    }

    // The held cursor participates in the same current ownership and Mail checks as
    // party/boxes. Stable save validation continues to reject the open native PC first.
    private string? ValidateStorageMoveOwnership(CampaignState state) =>
        ValidateStorageState(state) ?? ValidateCampaignMail(state) ?? ValidateNpcTradeHistory(state) ?? ValidateCapturedParty(state);
}
