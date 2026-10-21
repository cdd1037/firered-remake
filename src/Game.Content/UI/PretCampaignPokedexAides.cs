using Game.Domain.Events;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PokedexAideCatalog PokedexAides { get; private set; } = PokedexAideCatalog.Empty;

    private void BindPokedexAides()
        => PokedexAides = PokedexAideCatalog.Bind(_scripts, Maps.Keys.Select(MapDefinition), Inventory);
}
