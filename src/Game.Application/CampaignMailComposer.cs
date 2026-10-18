using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignMailComposePhase { Edit, ConfirmDone, ConfirmCancel, ConfirmDelete, Preview }
public sealed record CampaignMailComposer(string Ticket, long Revision, CampaignMailComposePhase Phase,
    int PartyIndex, long? TargetInstanceId, ushort ItemId, ImmutableArray<ushort> Words, bool ReplaceConfirmed)
{
    public MailStationeryOrigin Origin { get; init; }
    public string WaitId => Ticket + ":" + Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed partial class CampaignSession
{
    public bool MailComposerEnabled => _content.MailComposer is not null;
    public bool CanGiveMail(ItemId item) => MailComposerEnabled && _content.EarlyShop?.Items.Any(i =>
        i.Id == item && i.Type == "ITEM_TYPE_MAIL" && SourceMailRules.ItemSymbol((ushort)i.SourceItemId) == i.SourceItemSymbol) == true;

    private Decision<CampaignState> OpenMailComposer(CampaignState state, CampaignCommand command, bool preview)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!MailComposerEnabled || state.MailComposer is not null || state.MailInteraction is not null ||
            state.ItemMenu is not {} menu ||
            !(menu is { Origin: "bag", View: "bag" } && IsStableFieldForParty(state with { ItemMenu = null })
                || !preview && command.Kind == "pc_give" && menu is { Origin: "pc", View: "withdraw" } && state.PcPoweredOn) || command.Text is not {} id || !CanGiveMail(new(id)))
            return No("mail_composer_context");
        var origin = menu.Origin == "pc" ? MailStationeryOrigin.Pc : MailStationeryOrigin.Bag;
        if (preview && menu.TargetInstanceId is not null) return No("mail_preview_context");
        if (ValidateCampaignInventory(state.Inventory) is {} invalid) return No(invalid);
        var definition = _content.EarlyShop!.Items.Single(i => i.Id.Value == id);
        if (OpeningInventoryRules.Quantity(origin == MailStationeryOrigin.Pc ? state.Inventory!.PcItems : state.Inventory!.BagItems, definition.Id) < 1) return No("inventory_insufficient_quantity");
        if (!preview)
        {
            if (HeldItemTargetError(state, command) is {} targetError) return No(targetError);
            if (menu.TargetInstanceId is not null && (menu.TargetInstanceId != command.TargetInstanceId || menu.TargetPartyIndex != command.PartyIndex))
                return No("held_item_target_stale");
            if (SourceMailComposerRules.CheckGive(_content.EarlyShop, state.Inventory, state.Mail ?? SourceMailState.Empty,
                state.Party, command.PartyIndex, (ushort)definition.SourceItemId, command.Choice == 1, _content.Machines, origin) is {} error) return No(error);
        }
        return new(CommandStatus.Accepted, state with { MailComposer = new("mail:" + (Revision + 1), 0,
            preview ? CampaignMailComposePhase.Preview : CampaignMailComposePhase.Edit,
            preview ? -1 : command.PartyIndex, preview ? null : command.TargetInstanceId, (ushort)definition.SourceItemId,
            Enumerable.Repeat(ushort.MaxValue, 9).ToImmutableArray(), command.Choice == 1) { Origin = origin } });
    }

    private Decision<CampaignState> DecideMailComposer(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.MailComposer is not {} draft || !MailComposerEnabled || state.ItemMenu is not {} menu ||
            (draft.Origin == MailStationeryOrigin.Pc ? menu is not { Origin: "pc", View: "withdraw" } : menu is not { Origin: "bag", View: "bag" }))
            return No("mail_composer_context");
        if (command.Text != draft.WaitId) return No("mail_composer_stale_input");
        if (draft.Phase == CampaignMailComposePhase.Preview)
            return command.Kind == "mail_compose_back" ? new(CommandStatus.Accepted, state with { MailComposer = null }) : No("mail_preview_active");
        if (draft.PartyIndex < 0 || draft.PartyIndex >= state.Party.Length || state.Party[draft.PartyIndex].InstanceId != draft.TargetInstanceId)
            return No("held_item_target_stale");
        var edit = SourceEasyChatEditRules.Apply(_content.MailComposer!, EasyChatSelection(state),
            new(MailEditPhase(draft.Phase), draft.Words), 9, EasyChatAction(command.Kind, "mail_compose_"), command.Choice, command.Quantity);
        if (!edit.Succeeded) return No("mail_composer_" + edit.Error);
        if (edit.Outcome == SourceEasyChatEditOutcome.Continue)
            return new(CommandStatus.Accepted, state with { MailComposer = draft with {
                Phase = MailComposePhase(edit.State.Phase), Words = edit.State.Words, Revision = draft.Revision + 1 } });
        if (edit.Outcome == SourceEasyChatEditOutcome.Cancel)
            return new(CommandStatus.Accepted, state with { MailComposer = null });
        if (ValidateCampaignInventory(state.Inventory) is {} invalid) return No(invalid);
        var author = SourceMailComposerRules.CreateAuthor(_content.MailComposer!, state.PlayerName, state.TrainerId);
        var result = SourceMailComposerRules.Give(_content.EarlyShop!, _content.MailComposer!, EasyChatSelection(state),
            state.Inventory!, state.Mail ?? SourceMailState.Empty, state.Party, draft.PartyIndex,
            draft.ItemId, draft.Words, author, draft.ReplaceConfirmed, _content.Machines, draft.Origin);
        if (!result.Succeeded) return No(result.Error!);
        return new(CommandStatus.Accepted, CommitInventory(state, result.Inventory) with { MailComposer = null, Mail = result.State, Party = result.Party,
            ItemMenu = menu.TargetInstanceId is not null ? menu with { Origin = "party", View = "held" } : menu });
    }
}
