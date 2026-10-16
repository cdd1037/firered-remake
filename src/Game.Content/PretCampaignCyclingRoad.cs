using Game.Domain.Trainers;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretCyclingRoadRegion CyclingRoadRegion { get; }
    public bool CyclingRoadEnabled => CyclingRoadRegion is not null;
    public CyclingRoadTrainerCatalog CyclingRoadTrainers => CyclingRoadRegion.Trainers;
}
