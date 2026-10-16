namespace Game.Application;

public enum CampaignQuestLogHeldItemOrigin { Party, Bag, Pc }
/// <summary>Accepted menu transaction context, never retained in campaign/save state.</summary>
public sealed record CampaignQuestLogHeldItemUse(long InstanceId, CampaignQuestLogHeldItemOrigin Origin);

public static class CampaignQuestLogHeldItemCapture
{
    public static CampaignQuestLogHeldItemUse? Observe(CampaignState before, CampaignState after, CampaignCommand command)
    {
        if (command is { Kind: "mail_compose_confirm", Choice: 0 } &&
            before.MailComposer is { Phase: CampaignMailComposePhase.ConfirmDone, TargetInstanceId: {} mailTarget } draft &&
            after.MailComposer is null)
            return new(mailTarget, draft.Origin == Game.Domain.Pokemon.MailStationeryOrigin.Pc ? CampaignQuestLogHeldItemOrigin.Pc :
                before.ItemMenu?.TargetInstanceId is not null ? CampaignQuestLogHeldItemOrigin.Party : CampaignQuestLogHeldItemOrigin.Bag);
        if (command.Kind is not ("field_give" or "field_take" or "pc_give") ||
            command.TargetInstanceId is not {} target || before.ItemMenu is not {} menu ||
            command.PartyIndex < 0 || command.PartyIndex >= before.Party.Length ||
            before.Party[command.PartyIndex].InstanceId != target) return null;
        if (command.Kind == "field_take")
            return menu is { Origin: "party", View: "held" } ? new(target, CampaignQuestLogHeldItemOrigin.Party) : null;
        if (command.Kind == "pc_give")
            return menu is { Origin: "pc", View: "withdraw" } ? new(target, CampaignQuestLogHeldItemOrigin.Pc) : null;
        return menu is { Origin: "bag", View: "bag" or "berries" or "machines" }
            ? new(target, menu.TargetInstanceId is not null ? CampaignQuestLogHeldItemOrigin.Party : CampaignQuestLogHeldItemOrigin.Bag) : null;
    }
}
