using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretPowerPlantRegion PowerPlantRegion { get; }
    public bool PowerPlantEnabled => PowerPlantRegion is not null;
    public GamePokemonCatalog PowerPlantOwnership { get; }
    public GameOpponentPokemonCatalog PowerPlantOpponents { get; }
    public MachineCatalog PowerPlantMachines { get; }

}
