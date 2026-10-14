using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>A source-authenticated shelf using the shared machine pocket and shop transaction owner.</summary>
public abstract class AuthenticatedMachineShopStock
{
    private protected AuthenticatedMachineShopStock() { }
    public abstract string Id { get; }
    public abstract ImmutableArray<ItemId> Items { get; }
    public abstract PewterMachineCatalog Machines { get; }
    public abstract void Validate();
}
