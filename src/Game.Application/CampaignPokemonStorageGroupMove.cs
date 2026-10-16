using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private Decision<CampaignState> DecideStorageGroupMove(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.Pc is not { View: CampaignPcView.Move, MoveCursor: null } pc || state.Storage is not {} storage)
            return No("storage_rectangle_context");
        if (ValidateStorageMoveOwnership(state) is {} invalid) return No(invalid);
        PokemonStorageGroupResult result;
        switch (command.Kind)
        {
            case "storage_group_grab" when command.StorageRectangle is {} rectangle:
                result = PokemonStorageRectangleRules.Grab(_content.PokemonContent, state.Party, storage, pc.GroupCursor, rectangle, command.StorageIdentities);
                break;
            case "storage_group_place" when command.StorageLocation is {} location:
                result = PokemonStorageRectangleRules.Place(_content.PokemonContent, state.Party, storage, pc.GroupCursor, location, command.StorageIdentities);
                break;
            case "storage_box" when command.Choice is {} box:
                var selected = PokemonStorageRules.SelectBox(_content.PokemonContent, state.Party, storage, box);
                if (!selected.Succeeded) return No(selected.Diagnostic!.Code);
                result = new(selected.Storage, pc.GroupCursor);
                break;
            // Native B is unable while carrying a rectangle. No implicit undo, discard, party, or menu.
            default: return No(pc.GroupCursor is null ? "storage_rectangle_payload" : "storage_move_holding");
        }
        if (!result.Succeeded) return No(result.Diagnostic!.Code);
        var next = state with { Storage = result.Storage, Pc = pc with { GroupCursor = result.Cursor } };
        if (ValidateStorageMoveOwnership(next) is {} error) return No(error);
        return new(CommandStatus.Accepted, next);
    }
}
