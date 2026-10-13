using System.Runtime.CompilerServices;
using Game.Domain.Pokemon;
namespace Game.Domain.Items;

/// <summary>Authenticated successor machine catalogs retain their exact current Pokemon owner.</summary>
internal static class OwnedMachineCatalogBinding
{
    private static readonly ConditionalWeakTable<PewterMachineCatalog, OwnedBattlePokemonCatalog> Owners = new();
    internal static void Register(PewterMachineCatalog machines, OwnedBattlePokemonCatalog owned)
    {
        if (!ReferenceEquals(OwnedBattlePokemonCatalog.For(owned.Catalog), owned)) throw new ArgumentException("machine_owned_proof");
        Owners.Add(machines, owned);
    }
    internal static OwnedBattlePokemonCatalog? For(PewterMachineCatalog? machines) =>
        machines is not null && Owners.TryGetValue(machines, out var owner) ? owner : null;
}
