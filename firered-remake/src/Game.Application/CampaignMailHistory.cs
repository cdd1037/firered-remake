using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsAuthenticatedReceivedMail(SourceMailMessage? message) =>
        message is not null && _content.NpcTradeMailMessage is {} source &&
        SourceMailRules.ValidateMessage(message) is null && CanonicalJson.Hash(message) == CanonicalJson.Hash(source);

    private string? ValidateCampaignMail(CampaignState state)
    {
        var mail = state.Mail ?? SourceMailState.Empty;
        if (SourceMailRules.Validate(mail, state.Party, state.Storage) is {} error) return error;
        var messages = mail.Attached.Concat(mail.Mailbox).OfType<SourceMailMessage>().ToArray();
        if (messages.Any(m => !IsAuthenticatedReceivedMail(m))) return "save_mail_source_invalid";
        var receipts = state.NpcTrades ?? [];
        if (receipts.IsDefault || receipts.Any(r => r is null)) return "save_mail_history_invalid";
        int issued = 0, departed = 0;
        foreach (var receipt in receipts)
        {
            if (receipt.OutgoingMail is not null)
            {
                if (++departed > issued) return "save_mail_departure_before_acquisition";
            }
            if (receipt.TradeId == 1 && IsAuthenticatedReceivedMail(receipt.IncomingMail)) issued++;
        }
        // This field release supports the single original Dontae letter. Composition is still
        // outside the current product, so duplicated authored payloads cannot mint extra letters.
        if (issued > 1 || messages.Length + departed > issued) return "save_mail_acquisition_missing";
        return null;
    }

    private string? ValidateNpcTradeMailReceipt(CampaignNpcTradeReceipt receipt, NpcTradePokemonDefinition definition)
    {
        bool outgoingHasMail = receipt.Outgoing.Mail != SourceMailState.NoMail;
        if (outgoingHasMail != (receipt.OutgoingMail is not null) ||
            receipt.OutgoingMail is {} sent && (!IsAuthenticatedReceivedMail(sent) || receipt.Outgoing.HeldItem != sent.ItemSymbol))
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
            IsAuthenticatedReceivedMail(mail.Attached[pokemon.Mail])) return true;
        return (state.NpcTrades ?? []).Any(r => r.Outgoing.InstanceId == pokemon.InstanceId &&
            IsAuthenticatedReceivedMail(r.OutgoingMail) && r.Outgoing.HeldItem == pokemon.HeldItem && r.Outgoing.Mail == pokemon.Mail);
    }
}
