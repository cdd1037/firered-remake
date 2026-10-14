using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignMailPhase { Reader, SendToPc, EraseAttached, MailboxList, MailboxActions, MailboxGive, EraseMailbox, Message }
/// <summary>Transient source callback/confirmation owner. The persistent message always stays in Mail.</summary>
public sealed record CampaignMailInteraction(string Origin, CampaignMailPhase Phase, int PartyIndex = -1,
    long? TargetInstanceId = null, int MailboxSlot = -1, string? MessageSymbol = null,
    CampaignMailPhase? ReturnPhase = null);

public sealed partial class CampaignSession
{
    public SourceMailMessage? MailReader
    {
        get
        {
            if (State.MailInteraction is not { Phase: CampaignMailPhase.Reader } pending) return null;
            var mail = State.Mail ?? SourceMailState.Empty;
            return pending.Origin == "pc"
                ? SourceMailRules.TryReadMailbox(mail, State.Party, pending.MailboxSlot).Message
                : pending.PartyIndex >= 0 && pending.PartyIndex < State.Party.Length &&
                  State.Party[pending.PartyIndex].InstanceId == pending.TargetInstanceId
                    ? SourceMailRules.TryReadAttached(mail, State.Party, State.Party[pending.PartyIndex].Mail).Message : null;
        }
    }

    private bool MailPcCallback(CampaignState state)
    {
        if (state.ItemMenu is not { Origin: "pc" } || !state.PcPoweredOn || !state.Script.Locked ||
            state.Script.Completed || state.Script.Wait?.Kind != StoryWaitKind.Inventory || state.Script.Stack.IsEmpty) return false;
        var frame = state.Script.Stack[^1];
        var program = _content.FindProgram(frame.ProgramId);
        int wait = frame.InstructionIndex - 1;
        if (program is null || wait < 1 || wait >= program.Instructions.Length ||
            program.Instructions[wait] is not { Op: StoryOp.WaitState, Opcode: "waitstate" } instruction) return false;
        var native = program.Instructions[wait - 1];
        return native.Opcode == "special" && native.Args.Length == 1 &&
            (state.Pc?.View == CampaignPcView.PlayerItems && instruction.Source.Path == CenterPcSource &&
             frame.ProgramId == "EventScript_AccessPlayersPC" && native.Args[0] == "PlayerPC" ||
             state.Pc is null && state.World.Player.MapId.Value == "map.players-house-2f" &&
             instruction.Source.Path == BedroomPcSource && frame.ProgramId == BedroomPcScript && native.Args[0] == "BedroomPC");
    }

    private bool TryDecideMail(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        bool openMailbox = command.Kind == "mailbox_open" || command is { Kind: "menu_select", Text: "mailbox" } &&
            state.ItemMenu is { Origin: "pc", View: "top" };
        bool commandForMail = command.Kind.StartsWith("mail_", StringComparison.Ordinal) ||
            command.Kind.StartsWith("mailbox_", StringComparison.Ordinal);
        decision = new(CommandStatus.Rejected, state, "mail_context_invalid");
        if (state.MailInteraction is null && !commandForMail && !openMailbox) return false;
        if (_content.NpcTradeMailMessage is null)
        {
            if (openMailbox && !commandForMail) return false;
            decision = new(CommandStatus.Rejected, state, "mail_profile_missing"); return true;
        }
        decision = DecideMail(state, command, openMailbox);
        return true;
    }

    private Decision<CampaignState> DecideMail(CampaignState state, CampaignCommand command, bool openMailbox)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        Decision<CampaignState> Set(CampaignMailInteraction? pending) => new(CommandStatus.Accepted, state with { MailInteraction = pending });
        var mail = state.Mail ?? SourceMailState.Empty;
        if (SourceMailRules.Validate(mail, state.Party, state.Storage) is { } invalid) return No(invalid);
        var pending = state.MailInteraction;
        if (pending is null)
        {
            if (openMailbox)
            {
                if (state.ItemMenu?.View != "top" || !MailPcCallback(state)) return No("mail_pc_callback_context_invalid");
                bool empty = mail.Mailbox.All(message => message is null);
                return new(CommandStatus.Accepted, state with {
                    ItemMenu = state.ItemMenu with { View = "mailbox" },
                    MailInteraction = new("pc", empty ? CampaignMailPhase.Message : CampaignMailPhase.MailboxList,
                        MessageSymbol: empty ? "gText_TheresNoMailHere" : null) });
            }
            if (command.Kind is not ("mail_read" or "mail_take")) return No("mail_not_pending");
            if (state.ItemMenu is not { Origin: "party", View: "held" } menu ||
                !IsStableFieldForParty(state with { ItemMenu = null, MailInteraction = null })) return No("mail_party_context_invalid");
            if (HeldItemTargetError(state, command) is { } targetError) return No(targetError);
            if (menu.TargetPartyIndex != command.PartyIndex || menu.TargetInstanceId != command.TargetInstanceId)
                return No("held_item_target_stale");
            var read = SourceMailRules.TryReadAttached(mail, state.Party, state.Party[command.PartyIndex].Mail);
            if (!read.Succeeded) return No(read.Error!);
            return Set(new("party", command.Kind == "mail_read" ? CampaignMailPhase.Reader : CampaignMailPhase.SendToPc,
                command.PartyIndex, command.TargetInstanceId));
        }
        if (pending.Origin == "pc")
        {
            if (state.ItemMenu?.View != "mailbox" || !MailPcCallback(state)) return No("mail_pc_callback_context_invalid");
        }
        else
        {
            if (state.ItemMenu is not { Origin: "party", View: "held" } menu ||
                !IsStableFieldForParty(state with { ItemMenu = null, MailInteraction = null }) ||
                pending.PartyIndex < 0 || pending.PartyIndex >= state.Party.Length ||
                state.Party[pending.PartyIndex].InstanceId != pending.TargetInstanceId ||
                menu.TargetInstanceId != pending.TargetInstanceId || menu.TargetPartyIndex != pending.PartyIndex)
                return No("mail_party_context_invalid");
        }
        Decision<CampaignState> Phase(CampaignMailPhase phase) => Set(pending with { Phase = phase, MessageSymbol = null, ReturnPhase = null });
        Decision<CampaignState> Back()
        {
            if (pending.Origin == "party") return Set(null);
            if (pending.Phase == CampaignMailPhase.MailboxList || pending.Phase == CampaignMailPhase.Message && pending.ReturnPhase is null)
                return new(CommandStatus.Accepted, state with { MailInteraction = null, ItemMenu = state.ItemMenu! with { View = "top" } });
            return Phase(CampaignMailPhase.MailboxList);
        }
        Decision<CampaignState> Completed(SourceMailState changedMail, System.Collections.Immutable.ImmutableArray<PartyPokemon> party,
            Game.Domain.Items.InventoryState? inventory, string symbol) => new(CommandStatus.Accepted, state with {
                Mail = changedMail, Party = party, Inventory = inventory ?? state.Inventory,
                MailInteraction = pending with { Phase = CampaignMailPhase.Message, MessageSymbol = symbol,
                    ReturnPhase = pending.Origin == "pc" ? CampaignMailPhase.MailboxList : null } });
        bool back = command.Kind is "mail_back" or "menu_back";
        if (pending.Phase == CampaignMailPhase.Message)
        {
            if (command.Kind is not ("mail_ack" or "mail_back" or "menu_back")) return No("mail_message_active");
            if (pending.ReturnPhase is { } phase) return Phase(phase);
            if (pending.Origin == "pc") return Back();
            return new(CommandStatus.Accepted, ReturnToEventIslandField(state with { MailInteraction = null, ItemMenu = null }));
        }
        if (pending.Phase == CampaignMailPhase.Reader)
            return back || command.Kind == "mail_ack" ? Back() : No("mail_reader_active");
        if (pending.Phase == CampaignMailPhase.SendToPc)
        {
            if (back || command is { Kind: "mail_send_pc", Choice: 1 }) return Phase(CampaignMailPhase.EraseAttached);
            if (command is not { Kind: "mail_send_pc", Choice: 0 }) return No("mail_confirmation_required");
            if (HeldItemTargetError(state, command) is { } targetError) return No(targetError);
            if (command.TargetInstanceId != pending.TargetInstanceId || command.PartyIndex != pending.PartyIndex)
                return No("held_item_target_stale");
            var result = SourceMailRules.SendToMailbox(mail, state.Party, pending.PartyIndex);
            return result.Succeeded ? Completed(result.State, result.Party, null, "gText_MailSentToPC") : No(result.Error!);
        }
        if (pending.Phase is CampaignMailPhase.EraseAttached or CampaignMailPhase.EraseMailbox)
        {
            if (back || command is { Kind: "mail_erase_confirm", Choice: 1 }) return Back();
            if (command is not { Kind: "mail_erase_confirm", Choice: 0 }) return No("mail_erase_confirmation_required");
            if (_content.EarlyShop is not { } shop || state.Inventory is null) return No("mail_inventory_missing");
            if (ValidateCampaignInventory(state.Inventory) is { } error) return No(error);
            bool attached = pending.Phase == CampaignMailPhase.EraseAttached;
            if (attached && (command.PartyIndex != pending.PartyIndex || command.TargetInstanceId != pending.TargetInstanceId))
                return No("held_item_target_stale");
            var result = attached
                ? SourceMailRules.EraseAttachedToBag(shop, ShopInventoryView(state.Inventory), mail, state.Party, pending.PartyIndex, true)
                : SourceMailRules.EraseMailboxToBag(shop, ShopInventoryView(state.Inventory), mail, state.Party, pending.MailboxSlot, true);
            return result.Succeeded ? Completed(result.State, result.Party, MergeShopInventory(state.Inventory, result.Inventory),
                attached ? "gText_MailTakenFromPkmn" : "gText_MailReturnedToBagMessageErased") : No(result.Error!);
        }
        if (back) return Back();
        if (pending.Phase == CampaignMailPhase.MailboxList && command.Kind == "mailbox_select")
        {
            if (command.Choice is not { } slot) return No("mail_mailbox_index");
            var read = SourceMailRules.TryReadMailbox(mail, state.Party, slot);
            return read.Succeeded ? Set(pending with { Phase = CampaignMailPhase.MailboxActions, MailboxSlot = slot }) : No(read.Error!);
        }
        if (pending.Phase == CampaignMailPhase.MailboxActions)
        {
            var read = SourceMailRules.TryReadMailbox(mail, state.Party, pending.MailboxSlot);
            if (!read.Succeeded) return No(read.Error!);
            return command.Kind switch {
                "mailbox_read" => Phase(CampaignMailPhase.Reader),
                "mailbox_give" when state.Party.IsEmpty => No("mail_party_empty"),
                "mailbox_give" => Phase(CampaignMailPhase.MailboxGive),
                "mailbox_erase" => Phase(CampaignMailPhase.EraseMailbox),
                _ => No("mail_menu_active") };
        }
        if (pending.Phase == CampaignMailPhase.MailboxGive && command.Kind == "mailbox_give_target")
        {
            if (HeldItemTargetError(state, command) is { } targetError) return No(targetError);
            var result = SourceMailRules.GiveFromMailbox(mail, state.Party, pending.MailboxSlot, command.PartyIndex);
            return result.Succeeded ? Completed(result.State, result.Party, null, "gText_MailTransferredFromMailbox") : No(result.Error!);
        }
        return No("mail_menu_active");
    }
}
