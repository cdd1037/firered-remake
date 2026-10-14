using Game.Domain.Events;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public FameCheckerCatalog? FameChecker { get; init; }
}
