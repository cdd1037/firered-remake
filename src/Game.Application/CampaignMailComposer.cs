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
    public string WaitId => Ticket + ":" + Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed partial class CampaignSession
{
    public bool MailComposerEnabled => _content.MailComposer is not null;
    public SourceEasyChatSelection MailSelectionFacts => MailSelection(State);
    public string MailWordText(ushort word) => SourceEasyChatRules.WordText(_content.MailComposer!, word);
    private static SourceEasyChatSelection MailSelection(CampaignState state) => new(
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR"),
        NationalDexEnabledIn(state), state.SeenSpecies.ToImmutableHashSet());
    public bool CanGiveMail(ItemId item) => MailComposerEnabled && _content.EarlyShop?.Items.Any(i =>
        i.Id == item && i.Type == "ITEM_TYPE_MAIL" && SourceMailRules.ItemSymbol((ushort)i.SourceItemId) == i.SourceItemSymbol) == true;

    private Decision<CampaignState> OpenMailComposer(CampaignState state, CampaignCommand command, bool preview)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!MailComposerEnabled || state.MailComposer is not null || state.MailInteraction is not null ||
            state.ItemMenu is not { Origin: "bag", View: "bag" } menu ||
            !IsStableFieldForParty(state with { ItemMenu = null }) || command.Text is not {} id || !CanGiveMail(new(id)))
            return No("mail_composer_context");
        if (preview && menu.TargetInstanceId is not null) return No("mail_preview_context");
        if (ValidateCampaignInventory(state.Inventory) is {} invalid) return No(invalid);
        var definition = _content.EarlyShop!.Items.Single(i => i.Id.Value == id);
        if (OpeningInventoryRules.Quantity(state.Inventory!.BagItems, definition.Id) < 1) return No("inventory_insufficient_quantity");
        if (!preview)
        {
            if (HeldItemTargetError(state, command) is {} targetError) return No(targetError);
            if (menu.TargetInstanceId is not null && (menu.TargetInstanceId != command.TargetInstanceId || menu.TargetPartyIndex != command.PartyIndex))
                return No("held_item_target_stale");
            if (SourceMailComposerRules.CheckGive(_content.EarlyShop, state.Inventory, state.Mail ?? SourceMailState.Empty,
                state.Party, command.PartyIndex, (ushort)definition.SourceItemId, command.Choice == 1, _content.Machines) is {} error) return No(error);
        }
        return new(CommandStatus.Accepted, state with { MailComposer = new("mail:" + (Revision + 1), 0,
            preview ? CampaignMailComposePhase.Preview : CampaignMailComposePhase.Edit,
            preview ? -1 : command.PartyIndex, preview ? null : command.TargetInstanceId, (ushort)definition.SourceItemId,
            Enumerable.Repeat(ushort.MaxValue, 9).ToImmutableArray(), command.Choice == 1) });
    }

    private Decision<CampaignState> DecideMailComposer(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.MailComposer is not {} draft || !MailComposerEnabled || state.ItemMenu is not { Origin: "bag", View: "bag" } menu)
            return No("mail_composer_context");
        if (command.Text != draft.WaitId) return No("mail_composer_stale_input");
        Decision<CampaignState> Phase(CampaignMailComposePhase phase) => new(CommandStatus.Accepted,
            state with { MailComposer = draft with { Phase = phase, Revision = draft.Revision + 1 } });
        if (draft.Phase == CampaignMailComposePhase.Preview)
            return command.Kind == "mail_compose_back" ? new(CommandStatus.Accepted, state with { MailComposer = null }) : No("mail_preview_active");
        if (draft.PartyIndex < 0 || draft.PartyIndex >= state.Party.Length || state.Party[draft.PartyIndex].InstanceId != draft.TargetInstanceId)
            return No("held_item_target_stale");
        if (draft.Phase == CampaignMailComposePhase.Edit)
        {
            if (command.Kind == "mail_compose_word")
            {
                if (command.Choice is not (>= 0 and < 9) || command.Quantity is < 0 or > ushort.MaxValue)
                    return No("mail_composer_word_context");
                ushort word = (ushort)command.Quantity;
                if (word != ushort.MaxValue && !SourceEasyChatRules.IsSelectable(_content.MailComposer!, MailSelection(state), word))
                    return No("mail_composer_word_unavailable");
                return new(CommandStatus.Accepted, state with { MailComposer = draft with {
                    Words = draft.Words.SetItem(command.Choice.Value, word), Revision = draft.Revision + 1 } });
            }
            return command.Kind switch {
                "mail_compose_done" => Phase(draft.Words.All(w => w == ushort.MaxValue) ? CampaignMailComposePhase.ConfirmCancel : CampaignMailComposePhase.ConfirmDone),
                "mail_compose_cancel" or "mail_compose_back" => Phase(CampaignMailComposePhase.ConfirmCancel),
                "mail_compose_delete_all" => Phase(CampaignMailComposePhase.ConfirmDelete),
                _ => No("mail_composer_active") };
        }
        if (command.Kind == "mail_compose_back" || command is { Kind: "mail_compose_confirm", Choice: 1 })
            return Phase(CampaignMailComposePhase.Edit);
        if (command is not { Kind: "mail_compose_confirm", Choice: 0 }) return No("mail_composer_confirmation_required");
        if (draft.Phase == CampaignMailComposePhase.ConfirmCancel)
            return new(CommandStatus.Accepted, state with { MailComposer = null });
        if (draft.Phase == CampaignMailComposePhase.ConfirmDelete)
            return new(CommandStatus.Accepted, state with { MailComposer = draft with { Phase = CampaignMailComposePhase.Edit,
                Words = Enumerable.Repeat(ushort.MaxValue, 9).ToImmutableArray(), Revision = draft.Revision + 1 } });
        if (draft.Phase != CampaignMailComposePhase.ConfirmDone) return No("mail_composer_phase");
        if (ValidateCampaignInventory(state.Inventory) is {} invalid) return No(invalid);
        var author = SourceMailComposerRules.CreateAuthor(_content.MailComposer!, state.PlayerName, state.TrainerId);
        var result = SourceMailComposerRules.Give(_content.EarlyShop!, _content.MailComposer!, MailSelection(state),
            state.Inventory!, state.Mail ?? SourceMailState.Empty, state.Party, draft.PartyIndex,
            draft.ItemId, draft.Words, author, draft.ReplaceConfirmed, _content.Machines);
        if (!result.Succeeded) return No(result.Error!);
        return new(CommandStatus.Accepted, state with { MailComposer = null, Mail = result.State, Party = result.Party,
            Inventory = result.Inventory,
            ItemMenu = menu.TargetInstanceId is not null ? menu with { Origin = "party", View = "held" } : menu });
    }
}
