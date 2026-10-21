using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public OrdinaryTrainerCatalog? MemorialPillarTrainers { get; private init; }
}
