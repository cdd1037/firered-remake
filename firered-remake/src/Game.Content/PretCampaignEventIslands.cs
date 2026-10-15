using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretEventIslandRegion EventIslandRegion { get; }
    public bool EventIslandsEnabled => EventIslandRegion is not null;
    public EventIslandFieldData EventIslandField => EventIslandRegion.Field;
    public EventIslandOwnedPokemonCatalog EventIslandOwnership { get; }
    public EventIslandOpponentPokemonCatalog EventIslandOpponents { get; }
    public EventIslandMachineCatalog EventIslandMachines { get; }
    public EventIslandShopCatalog EventIslandShop { get; }


}
