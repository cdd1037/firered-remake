using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    // Set once by the current composition's constructor, after all owned species are admitted.
    public MoveTutorCatalog? MoveTutors { get; }
}
