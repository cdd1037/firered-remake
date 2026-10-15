using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretCeladonGymRegion? CeladonGymRegion { get; }
    public PretCeladonGymPokemon? CeladonGymPokemon { get; }
    public PretCeladonGymItemsDocument? CeladonGymItems { get; }
    public CeladonGymTrainerCatalog? CeladonGymTrainers { get; }
    public bool CeladonGymEnabled => CeladonGymRegion is not null;

}
