using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsAuthenticatedReceivedMail(SourceMailMessage? message) =>
        message is not null && _content.NpcTradeMailMessage is {} source &&
        SourceMailRules.ValidateMessage(message) is null && CanonicalJson.Hash(message) == CanonicalJson.Hash(source);

    private bool IsValidCampaignMailMessage(CampaignState state, SourceMailMessage? message)
    {
        if (IsAuthenticatedReceivedMail(message)) return true;
        if (message is null || _content.MailComposer is not {} catalog ||
            SourceMailComposerRules.ValidateMessage(catalog, message) is not null ||
            SourceEasyChatRules.ValidateWords(catalog, message.Words, requireNonempty: true) is not null) return false;
        try
        {
            var author = SourceMailComposerRules.CreateAuthor(catalog, state.PlayerName, state.TrainerId);
            return message.AuthorName == author.Name && message.AuthorNameBytes.SequenceEqual(author.NameBytes) &&
                message.TrainerIdBytes.SequenceEqual(author.TrainerIdBytes);
        }
        catch (ArgumentException) { return false; }
    }

    private string? ValidateCampaignMail(CampaignState state)
    {
        var mail = state.Mail ?? SourceMailState.Empty;
        if (SourceMailRules.Validate(mail, state.Party, state.Storage) is {} error) return error;
        var messages = mail.Attached.Concat(mail.Mailbox).Append(state.Route5DayCare?.Resident?.Mail).Append(state.FourIslandDayCare?.First?.Mail).Append(state.FourIslandDayCare?.Second?.Mail).OfType<SourceMailMessage>().ToArray();
        if (messages.Any(m => !IsValidCampaignMailMessage(state, m))) return "save_mail_source_invalid";
        var receipts = state.NpcTrades ?? [];
        if (receipts.IsDefault || receipts.Any(r => r is null)) return "save_mail_history_invalid";
        int issued = 0, departed = 0;
        foreach (var receipt in receipts)
        {
            if (IsAuthenticatedReceivedMail(receipt.OutgoingMail))
            {
                if (++departed > issued) return "save_mail_departure_before_acquisition";
            }
            if (receipt.TradeId == 1 && IsAuthenticatedReceivedMail(receipt.IncomingMail)) issued++;
        }
        // The original received Dontae letter keeps its one-time acquisition proof.
        // Player-authored stationery uses the shared inventory transaction and normal save model.
        if (issued > 1 || messages.Count(IsAuthenticatedReceivedMail) + departed > issued) return "save_mail_acquisition_missing";
        return null;
    }

    private string? ValidateNpcTradeMailReceipt(CampaignState state, CampaignNpcTradeReceipt receipt, NpcTradePokemonDefinition definition)
    {
        bool outgoingHasMail = receipt.Outgoing.Mail != SourceMailState.NoMail;
        if (outgoingHasMail != (receipt.OutgoingMail is not null) ||
            receipt.OutgoingMail is {} sent && (!IsValidCampaignMailMessage(state, sent) || receipt.Outgoing.HeldItem != sent.ItemSymbol))
            return "save_npc_trade_outgoing_mail";
        if (definition.Mail == SourceMailState.NoMail)
            return receipt.IncomingMail is not null || receipt.Incoming.Mail != SourceMailState.NoMail
                ? "save_npc_trade_incoming_mail" : null;
        if (definition.TradeId != 1 || !IsAuthenticatedReceivedMail(receipt.IncomingMail) ||
            receipt.Incoming.Mail != 0 || receipt.Incoming.HeldItem != receipt.IncomingMail!.ItemSymbol)
            return "save_npc_trade_incoming_mail";
        return null;
    }

    private bool CampaignMailHeldItemSupported(CampaignState state, PartyPokemon pokemon)
    {
        if (SourceMailRules.ValidateAttachmentReference(pokemon) is not null || pokemon.Mail == SourceMailState.NoMail)
            return false;
        if (state.Party.Any(p => p.InstanceId == pokemon.InstanceId) && state.Mail is {} mail &&
            !mail.Attached.IsDefault && mail.Attached.Length == SourceMailState.AttachedCapacity &&
            IsValidCampaignMailMessage(state, mail.Attached[pokemon.Mail])) return true;
        return (state.NpcTrades ?? []).Any(r => r.Outgoing.InstanceId == pokemon.InstanceId &&
            IsValidCampaignMailMessage(state, r.OutgoingMail) && r.Outgoing.HeldItem == pokemon.HeldItem && r.Outgoing.Mail == pokemon.Mail);
    }
}
