using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretEventIslandRegion EventIslandRegion { get; }
    public EventIslandFieldData EventIslandField => EventIslandRegion.Field;

}
