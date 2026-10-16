using Game.Domain.Pokemon;
using System.Collections.Immutable;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed partial class PretCampaignCatalog
{
    public VsSeekerCatalog? VsSeeker { get; private init; }
    public VsSeekerTrainerCatalog? VsSeekerTrainers { get; private init; }
    public ImmutableArray<VsSeekerBattleOccurrence> VsSeekerBattleOccurrences { get; private init; } = [];
    public ImmutableDictionary<string,string> VsSeekerTexts { get; private init; } = ImmutableDictionary<string,string>.Empty;
}
