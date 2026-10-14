using System.Runtime.CompilerServices;
using Game.Domain.Pokemon;
namespace Game.Domain.Items;

/// <summary>Authenticated successor machine catalogs retain their exact current Pokemon owner.</summary>
internal static class OwnedMachineCatalogBinding
{
    private static readonly ConditionalWeakTable<PewterMachineCatalog, OwnedBattlePokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PewterMachineCatalog, PewterMachineCatalog> Parents = new();
    internal static void Register(PewterMachineCatalog machines, OwnedBattlePokemonCatalog owned, PewterMachineCatalog? predecessor = null)
    {
        if (!ReferenceEquals(OwnedBattlePokemonCatalog.For(owned.Catalog), owned)) throw new ArgumentException("machine_owned_proof");
        if (predecessor is not null)
        {
            if (For(predecessor) is not { } before || !ReferenceEquals(owned.Mechanics, before.Mechanics) ||
                !ReferenceEquals(OwnedBattlePokemonCatalog.SourceAncestor(owned, c => ReferenceEquals(c, before.Catalog) ? before : null), before))
                throw new ArgumentException("machine_source_predecessor");
        }
        Owners.Add(machines, owned);
        if (predecessor is not null) Parents.Add(machines, predecessor);
    }
    internal static T? SourceAncestor<T>(PewterMachineCatalog? machines, Func<PewterMachineCatalog, T?> resolve) where T : class
    {
        while (machines is not null && For(machines) is not null)
        {
            if (resolve(machines) is { } source) return source;
            machines = Parents.TryGetValue(machines, out var parent) ? parent : null;
        }
        return null;
    }
    internal static OwnedBattlePokemonCatalog? For(PewterMachineCatalog? machines) =>
        machines is not null && Owners.TryGetValue(machines, out var owner) ? owner : null;
}
