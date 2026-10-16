using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignStorageCustomizationKind { BoxName, WallpaperSets, Wallpapers, Markings }

/// <summary>Transient source menu binding. Markings are a draft until OK; null Location with an
/// InstanceId identifies the moving creature. Box controls retain either kind of moving cursor.</summary>
public sealed record CampaignStorageCustomization(CampaignStorageCustomizationKind Kind, int BoxIndex,
    PokemonStorageMoveLocation? Location = null, long? InstanceId = null, int Markings = 0, int? WallpaperSet = null);

public sealed partial class CampaignSession
{
    private static bool IsStorageCustomizationCommand(string kind) => kind is
        "storage_box_name_open" or "storage_box_name" or "storage_wallpaper_open" or
        "storage_wallpaper_category" or "storage_wallpaper" or "storage_mark_open" or
        "storage_mark_toggle" or "storage_mark_confirm" or "storage_customization_cancel";

    private string? ValidateStorageCustomizationState(CampaignState state)
    {
        if (state.Pc?.Customization is not {} menu) return null;
        var pc = state.Pc;
        if (pc.View is not (CampaignPcView.Deposit or CampaignPcView.Withdraw or CampaignPcView.Move or CampaignPcView.MoveItems) ||
            pc.ReleaseTarget is not null || pc.ItemPrompt is not null || pc.ItemBagTarget is not null ||
            state.Storage is not {} storage || menu.BoxIndex != storage.CurrentBox || !Enum.IsDefined(menu.Kind))
            return "storage_customization_context";
        if (menu.Kind == CampaignStorageCustomizationKind.Markings)
        {
            if (pc.View == CampaignPcView.MoveItems || menu.InstanceId is null || menu.Markings is < 0 or > 15 || menu.WallpaperSet is not null)
                return "storage_markings_context";
            if (pc.MoveCursor is {} held)
                return menu.Location is not null || held.Pokemon.InstanceId != menu.InstanceId ? "storage_markings_target_identity" : null;
            if (menu.Location is not {} location || pc.View == CampaignPcView.Deposit && !location.IsParty ||
                pc.View == CampaignPcView.Withdraw && location.IsParty) return "storage_markings_context";
            return PokemonStorageCustomizationRules.SetMarkings(_content.PokemonContent, state.Party, storage,
                null, location, menu.InstanceId.Value, menu.Markings).Diagnostic?.Code;
        }
        return menu.Location is not null || menu.InstanceId is not null || menu.Markings != 0 ||
            (menu.Kind == CampaignStorageCustomizationKind.Wallpapers ? menu.WallpaperSet is not (>= 0 and < 4) : menu.WallpaperSet is not null)
            ? "storage_customization_context" : null;
    }

    private Decision<CampaignState> DecideStorageCustomization(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.Pc is not {} pc || pc.View is not (CampaignPcView.Deposit or CampaignPcView.Withdraw or CampaignPcView.Move or CampaignPcView.MoveItems) ||
            state.Storage is not {} storage) return No("storage_customization_context");
        string? Validate(CampaignState next) => pc.View == CampaignPcView.MoveItems
            ? ValidateStorageItemOwnership(next) : ValidateStorageMoveOwnership(next);
        if (Validate(state) is {} invalid) return No(invalid);
        Decision<CampaignState> Menu(CampaignStorageCustomization? value) =>
            new(CommandStatus.Accepted, state with { Pc = pc with { Customization = value } });
        Decision<CampaignState> Commit(PokemonStorageCustomizationResult result)
        {
            if (!result.Succeeded) return No(result.Diagnostic!.Code);
            var next = state with { Party = result.Party, Storage = result.Storage,
                Pc = pc with { MoveCursor = result.Cursor, Customization = null } };
            return Validate(next) is {} error ? No(error) : new(CommandStatus.Accepted, next);
        }
        bool SameMarkTarget(CampaignStorageCustomization menu) => command.StorageLocation == menu.Location &&
            (menu.Location is null ? command.HeldInstanceId == menu.InstanceId && command.TargetInstanceId is null
                : command.TargetInstanceId == menu.InstanceId && command.HeldInstanceId is null);

        if (pc.Customization is {} pending)
        {
            // Original naming has no B/Cancel exit. B edits the keyboard; blank OK keeps the old name.
            if (pending.Kind == CampaignStorageCustomizationKind.BoxName && command.Kind is "pc_back" or "storage_customization_cancel")
                return No("storage_box_name_active");
            if (command.Kind == "storage_customization_cancel") return Menu(null);
            if (command.Kind == "pc_back") return pending.Kind == CampaignStorageCustomizationKind.Wallpapers
                ? Menu(pending with { Kind = CampaignStorageCustomizationKind.WallpaperSets, WallpaperSet = null }) : Menu(null);
            if (pending.Kind == CampaignStorageCustomizationKind.Markings)
            {
                if (command.Kind is not ("storage_mark_toggle" or "storage_mark_confirm")) return No("storage_customization_active");
                if (!SameMarkTarget(pending)) return No("storage_markings_target_identity");
                if (command.Kind == "storage_mark_toggle")
                    return command.Choice is >= 0 and < 4
                        ? Menu(pending with { Markings = pending.Markings ^ (1 << command.Choice.Value) })
                        : No("storage_markings_choice");
                return Commit(PokemonStorageCustomizationRules.SetMarkings(_content.PokemonContent, state.Party, storage,
                    pc.MoveCursor, pending.Location, pending.InstanceId!.Value, pending.Markings));
            }
            bool expected = pending.Kind switch
            {
                CampaignStorageCustomizationKind.BoxName => command.Kind == "storage_box_name",
                CampaignStorageCustomizationKind.WallpaperSets => command.Kind == "storage_wallpaper_category",
                CampaignStorageCustomizationKind.Wallpapers => command.Kind == "storage_wallpaper",
                _ => false
            };
            if (!expected) return No("storage_customization_active");
            if (command.StorageBoxIndex != pending.BoxIndex) return No("storage_customization_box_identity");
            if (pending.Kind == CampaignStorageCustomizationKind.BoxName)
                return Commit(PokemonStorageCustomizationRules.RenameBox(_content.PokemonContent, state.Party, storage,
                    pc.MoveCursor, pending.BoxIndex, command.Text));
            if (pending.Kind == CampaignStorageCustomizationKind.WallpaperSets)
                return command.Choice is >= 0 and < 4
                    ? Menu(pending with { Kind = CampaignStorageCustomizationKind.Wallpapers, WallpaperSet = command.Choice })
                    : No("storage_wallpaper_category");
            if (command.Choice is not (>= 0 and < PokemonStorageCustomizationRules.WallpaperCount)) return No("storage_wallpaper_id");
            if (command.Choice / 4 != pending.WallpaperSet) return No("storage_wallpaper_category");
            return Commit(PokemonStorageCustomizationRules.ChangeWallpaper(_content.PokemonContent, state.Party, storage,
                pc.MoveCursor, pending.BoxIndex, command.Choice.Value));
        }

        if (command.Kind is "storage_box_name_open" or "storage_wallpaper_open")
        {
            if (command.StorageBoxIndex != storage.CurrentBox) return No("storage_customization_box_identity");
            return Menu(new(command.Kind == "storage_box_name_open" ? CampaignStorageCustomizationKind.BoxName :
                CampaignStorageCustomizationKind.WallpaperSets, storage.CurrentBox));
        }
        if (command.Kind != "storage_mark_open") return No("storage_customization_missing");
        if (pc.View == CampaignPcView.MoveItems) return No("storage_markings_context");
        PokemonStorageMoveLocation? target;
        long instance;
        if (pc.MoveCursor is {} cursor)
        {
            if (command.HeldInstanceId != cursor.Pokemon.InstanceId || command.TargetInstanceId is not null || command.StorageLocation is not null)
                return No("storage_markings_target_identity");
            target = null; instance = cursor.Pokemon.InstanceId;
        }
        else
        {
            if (command.StorageLocation is not {} location) return No("storage_move_location_missing");
            if (pc.View == CampaignPcView.Deposit && !location.IsParty || pc.View == CampaignPcView.Withdraw && location.IsParty)
                return No("storage_markings_context");
            if (command.TargetInstanceId is not {} selected || command.HeldInstanceId is not null) return No("storage_markings_target_identity");
            target = location; instance = selected;
        }
        // Use the same Domain admission before reading a coordinate. A no-op result is discarded;
        // opening/canceling a menu never mutates the selected creature or consumes RNG.
        var check = PokemonStorageCustomizationRules.SetMarkings(_content.PokemonContent, state.Party, storage, pc.MoveCursor, target, instance, 0);
        if (!check.Succeeded) return No(check.Diagnostic!.Code);
        var pokemon = pc.MoveCursor?.Pokemon ?? (target!.IsParty ? state.Party[target.SlotIndex] : storage.Boxes[target.BoxIndex].Slots[target.SlotIndex]!);
        return Menu(new(CampaignStorageCustomizationKind.Markings, storage.CurrentBox, target, instance, pokemon.Markings));
    }
}
