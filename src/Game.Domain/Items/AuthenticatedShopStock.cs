using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>A separately source-authenticated shelf using unchanged catalog item definitions and inventory rules.</summary>
public abstract record AuthenticatedShopStock
{
    private protected AuthenticatedShopStock() { }
    public abstract string Id { get; }
    public abstract ImmutableArray<ItemId> Items { get; }
    public abstract void Validate(EarlyShopCatalog catalog);
}
