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
    public GamePokemonCatalog EventIslandOwnership { get; }
    public GameOpponentPokemonCatalog EventIslandOpponents { get; }
    public MachineCatalog EventIslandMachines { get; }

}
