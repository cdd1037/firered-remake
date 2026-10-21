using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignEasyChatContext { Mail, Profile }
public sealed record CampaignEasyChatEditor(string Ticket, long Revision, SourceEasyChatEditPhase Phase,
    ImmutableArray<ushort> Words, string TemplateId, CampaignEasyChatContext Context)
{
    public string WaitId => Ticket + ":" + Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed partial class CampaignSession
{
    /// <summary>Read-only presentation projection. Mail retains its item transaction and command API.</summary>
    public CampaignEasyChatEditor? EasyChatEditor => State.ProfileEditor is { } profile
        ? new(profile.Ticket, profile.Revision, profile.Edit.Phase, profile.Edit.Words, "profile", CampaignEasyChatContext.Profile)
        : State.MailComposer is { Phase: not CampaignMailComposePhase.Preview } mail
            ? new(mail.Ticket, mail.Revision, MailEditPhase(mail.Phase), mail.Words, "mail", CampaignEasyChatContext.Mail)
            : null;
    public SourceEasyChatSelection EasyChatSelectionFacts => EasyChatSelection(State);
    public string EasyChatWordText(ushort word) => SourceEasyChatRules.WordText(_content.MailComposer!, word);
    private static SourceEasyChatSelection EasyChatSelection(CampaignState state) => new(
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR"),
        NationalDexEnabledIn(state), state.SeenSpecies.ToImmutableHashSet());

    private static SourceEasyChatEditAction EasyChatAction(string kind, string prefix) => kind.StartsWith(prefix, StringComparison.Ordinal)
        ? kind[prefix.Length..] switch
        {
            "word" => SourceEasyChatEditAction.Word, "done" => SourceEasyChatEditAction.Done,
            "cancel" => SourceEasyChatEditAction.Cancel, "back" => SourceEasyChatEditAction.Back,
            "delete_all" => SourceEasyChatEditAction.DeleteAll, "confirm" => SourceEasyChatEditAction.Confirm,
            _ => SourceEasyChatEditAction.Unsupported
        } : SourceEasyChatEditAction.Unsupported;

    private static SourceEasyChatEditPhase MailEditPhase(CampaignMailComposePhase phase) => phase switch
    {
        CampaignMailComposePhase.Edit => SourceEasyChatEditPhase.Edit,
        CampaignMailComposePhase.ConfirmDone => SourceEasyChatEditPhase.ConfirmDone,
        CampaignMailComposePhase.ConfirmCancel => SourceEasyChatEditPhase.ConfirmCancel,
        CampaignMailComposePhase.ConfirmDelete => SourceEasyChatEditPhase.ConfirmDelete,
        _ => throw new InvalidDataException("mail_composer_phase")
    };
    private static CampaignMailComposePhase MailComposePhase(SourceEasyChatEditPhase phase) => phase switch
    {
        SourceEasyChatEditPhase.Edit => CampaignMailComposePhase.Edit,
        SourceEasyChatEditPhase.ConfirmDone => CampaignMailComposePhase.ConfirmDone,
        SourceEasyChatEditPhase.ConfirmCancel => CampaignMailComposePhase.ConfirmCancel,
        SourceEasyChatEditPhase.ConfirmDelete => CampaignMailComposePhase.ConfirmDelete,
        _ => throw new InvalidDataException("mail_composer_phase")
    };
}
