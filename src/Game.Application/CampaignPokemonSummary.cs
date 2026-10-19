using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Events;

namespace Game.Application;

/// <summary>Transient owned Summary binding. A null location means the moving creature, which
/// remains held. Otherwise Location is the exact compact party or physical box slot last viewed.</summary>
public sealed record CampaignStorageSummary(PokemonStorageMoveLocation? Location, long InstanceId,
    PokemonSummaryPage Page = PokemonSummaryPage.Info)
{
    [JsonIgnore] public bool IsHeld => Location is null;
}

public sealed partial class CampaignSession
{
    /// <summary>Read the live bound owner, never a duplicate creature stored by presentation.</summary>
    public PartyPokemon? StorageSummaryPokemon => State.Pc?.Summary is {} summary &&
        ValidateStorageSummaryState(State) is null ? StorageSummaryPokemonAt(State, summary.Location) : null;

    public bool CanReorderFieldSummaryMoves => !State.Party.IsDefaultOrEmpty && State.Pc is null && IsStableFieldForParty(State);

    private static bool IsStorageSummaryCommand(string kind) => kind is "storage_summary_open" or
        "storage_summary_page" or "storage_summary_browse" or "storage_summary_swap" or "storage_summary_close";

    private static PartyPokemon? StorageSummaryPokemonAt(CampaignState state, PokemonStorageMoveLocation? location)
    {
        if (location is null) return state.Pc?.MoveCursor?.Pokemon;
        if (location.SlotIndex < 0) return null;
        if (location.IsParty) return !state.Party.IsDefault && location.SlotIndex < state.Party.Length
            ? state.Party[location.SlotIndex] : null;
        return state.Storage is {} storage && !storage.Boxes.IsDefault && location.BoxIndex >= 0 &&
            location.BoxIndex < storage.Boxes.Length && storage.Boxes[location.BoxIndex] is {} box &&
            !box.Slots.IsDefault && location.SlotIndex < box.Slots.Length ? box.Slots[location.SlotIndex] : null;
    }

    private string? StorageSummaryTargetError(CampaignState state, PokemonStorageMoveLocation? location, long instanceId)
    {
        var pc = state.Pc!;
        if (pc.MoveCursor is not null)
        {
            if (location is not null || pc.MoveCursor.Pokemon.InstanceId != instanceId)
                return "storage_summary_target_identity";
        }
        else
        {
            if (location is null) return "storage_move_location_missing";
            if (location.BoxIndex is < 0 or > PokemonStorageMoveLocation.PartyBox) return "storage_box_index";
            if (location.SlotIndex < 0 || location.SlotIndex >= (location.IsParty ? 6 : PokemonStorageState.SlotsPerBox))
                return location.IsParty ? "storage_party_index" : "storage_slot_index";
            if (pc.View == CampaignPcView.Deposit && !location.IsParty || pc.View == CampaignPcView.Withdraw && location.IsParty)
                return "storage_summary_context";
            if (!location.IsParty && location.BoxIndex != state.Storage!.CurrentBox) return "storage_move_box_not_selected";
            var pokemon = StorageSummaryPokemonAt(state, location);
            if (pokemon is null) return "storage_slot_empty";
            if (pokemon.InstanceId != instanceId) return "storage_summary_target_identity";
        }
        return null;
    }

    private string? ValidateStorageSummaryState(CampaignState state)
    {
        if (state.Pc?.Summary is not {} summary) return null;
        var pc = state.Pc;
        if (state.Storage is null || pc.View is not (CampaignPcView.Deposit or CampaignPcView.Withdraw or CampaignPcView.Move) ||
            pc.Customization is not null || pc.ReleaseTarget is not null || pc.ItemCursor is not null ||
            pc.ItemPrompt is not null || pc.ItemBagTarget is not null || !Enum.IsDefined(summary.Page))
            return "storage_summary_context";
        if (StorageSummaryTargetError(state, summary.Location, summary.InstanceId) is {} targetError) return targetError;
        return StorageSummaryPokemonAt(state, summary.Location)!.IsEgg && summary.Page != PokemonSummaryPage.Info
            ? "summary_egg" : null;
    }

    private Decision<CampaignState> DecideStorageSummary(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.Pc is not {} pc || state.Storage is not {} storage ||
            pc.View is not (CampaignPcView.Deposit or CampaignPcView.Withdraw or CampaignPcView.Move) ||
            pc.Customization is not null || pc.ReleaseTarget is not null || pc.ItemCursor is not null ||
            pc.ItemPrompt is not null || pc.ItemBagTarget is not null) return No("storage_summary_context");
        if (ValidateStorageMoveOwnership(state) is {} invalid) return No(invalid);
        Decision<CampaignState> Menu(CampaignStorageSummary? value) =>
            new(CommandStatus.Accepted, state with { Pc = pc with { Summary = value } });
        bool SameTarget(CampaignStorageSummary summary) => command.StorageLocation == summary.Location &&
            (summary.IsHeld ? command.HeldInstanceId == summary.InstanceId && command.TargetInstanceId is null
                : command.TargetInstanceId == summary.InstanceId && command.HeldInstanceId is null);

        if (pc.Summary is not {} current)
        {
            if (command.Kind != "storage_summary_open") return No("storage_summary_missing");
            var selected = new CampaignStorageSummary(command.StorageLocation,
                (pc.MoveCursor is null ? command.TargetInstanceId : command.HeldInstanceId) ?? -1);
            if (!SameTarget(selected)) return No("storage_summary_target_identity");
            if (StorageSummaryTargetError(state, selected.Location, selected.InstanceId) is {} targetError) return No(targetError);
            return Menu(selected);
        }
        // Closing does not change an owned Pokemon. As for the other native PC modals, B needs
        // only a valid current owner and the shared expected revision to restore the source menu.
        if (command.Kind is "storage_summary_close" or "pc_back") return Menu(null);
        if (command.Kind is not ("storage_summary_page" or "storage_summary_browse" or "storage_summary_swap"))
            return No("storage_summary_active");
        if (!SameTarget(current)) return No("storage_summary_target_identity");
        var pokemon = StorageSummaryPokemonAt(state, current.Location)!;
        if (command.Kind == "storage_summary_page")
        {
            if (command.Choice is not (>= 0 and <= 2)) return No("summary_page");
            var page = (PokemonSummaryPage)command.Choice.Value;
            if (pokemon.IsEgg && page != PokemonSummaryPage.Info) return No("summary_egg");
            if (Math.Abs((int)page - (int)current.Page) > 1) return No("summary_page_boundary");
            return Menu(current with { Page = page });
        }
        if (command.Kind == "storage_summary_browse")
        {
            if (command.Choice is not (-1 or 1)) return No("summary_direction");
            int? index = current.IsHeld ? PokemonSummaryRules.SeekPartyIndex([pokemon], 0, current.Page, command.Choice.Value)
                : current.Location!.IsParty ? PokemonSummaryRules.SeekPartyIndex(state.Party, current.Location.SlotIndex, current.Page, command.Choice.Value)
                : PokemonSummaryRules.SeekBoxSlot(storage.Boxes[current.Location.BoxIndex], current.Location.SlotIndex, current.Page, command.Choice.Value);
            if (index is null) return No("summary_boundary");
            var location = current.Location! with { SlotIndex = index.Value };
            return Menu(current with { Location = location, InstanceId = StorageSummaryPokemonAt(state, location)!.InstanceId });
        }
        if (current.Page != PokemonSummaryPage.Moves) return No("summary_moves_page_required");
        if (command.Choice is not {} second) return No("summary_move_slot");
        var swapped = PokemonSummaryRules.SwapMoveSlots(_content.PokemonContent, pokemon, command.MoveSlot, second);
        if (!swapped.Succeeded) return No(swapped.Error!);
        if (swapped.Outcome == PokemonSummaryMoveOutcome.Unchanged) return new(CommandStatus.Accepted, state);
        var replacement = swapped.Pokemon!;
        CampaignState next;
        if (current.IsHeld) next = state with { Pc = pc with { MoveCursor = pc.MoveCursor! with { Pokemon = replacement } } };
        else if (current.Location!.IsParty) next = state with { Party = state.Party.SetItem(current.Location.SlotIndex, replacement) };
        else
        {
            var box = storage.Boxes[current.Location.BoxIndex];
            next = state with { Storage = storage with { Boxes = storage.Boxes.SetItem(current.Location.BoxIndex,
                box with { Slots = box.Slots.SetItem(current.Location.SlotIndex, replacement) }) } };
        }
        return ValidateStorageMoveOwnership(next) is {} error ? No(error) : new(CommandStatus.Accepted, next);
    }

    private Decision<CampaignState> DecideFieldSummarySwap(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.Pc is not null || !IsStableFieldForParty(state)) return No("field_party_event_active");
        return DecideOwnedPartySummarySwap(state, command);
    }

    // Both Day-Care callers first authenticate their actor, source waitstate and Party
    // callback. This narrow admission retains that suspended script; it never pumps
    // it or changes the field party gate. Resident/withdrawal menus cannot use it.
    private bool TryDecideDayCarePartySummary(CampaignState state, CampaignCommand command,
        out Decision<CampaignState> decision)
    {
        decision = new(CommandStatus.Rejected, state, "daycare_callback_context");
        if (command.Kind != "party_summary_swap") return false;
        if (state.Pc is null && state.DayCareInteraction?.Stage == CampaignRoute5DayCareStage.Party &&
            state.Script.Wait?.Kind == StoryWaitKind.PartySelection)
            decision = DecideOwnedPartySummarySwap(state, command);
        return true;
    }

    // One transaction changes complete owned move slots for field and authenticated
    // service Summary. Session.Execute still supplies the expected-revision guard.
    private Decision<CampaignState> DecideOwnedPartySummarySwap(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length) return No("summary_party_index");
        var pokemon = state.Party[command.PartyIndex];
        if (command.TargetInstanceId != pokemon.InstanceId || command.StorageLocation is not null || command.HeldInstanceId is not null)
            return No("summary_target_identity");
        if (command.Choice is not {} second) return No("summary_move_slot");
        if (ValidateStorageMoveOwnership(state) is {} invalid) return No(invalid);
        var swapped = PokemonSummaryRules.SwapMoveSlots(_content.PokemonContent, pokemon, command.MoveSlot, second);
        if (!swapped.Succeeded) return No(swapped.Error!);
        if (swapped.Outcome == PokemonSummaryMoveOutcome.Unchanged) return new(CommandStatus.Accepted, state);
        var next = state with { Party = state.Party.SetItem(command.PartyIndex, swapped.Pokemon!) };
        return ValidateStorageMoveOwnership(next) is {} error ? No(error) : new(CommandStatus.Accepted, next);
    }
}
