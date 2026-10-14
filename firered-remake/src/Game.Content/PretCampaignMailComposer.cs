using Game.Domain.Pokemon;
namespace Game.Content;
public sealed partial class PretCampaignCatalog
{
    public PretMailPresentation? MailPresentation { get; init; }
    public SourceEasyChatCatalog? MailComposer { get; init; }
}
