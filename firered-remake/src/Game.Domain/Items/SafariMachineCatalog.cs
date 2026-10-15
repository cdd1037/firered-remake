using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Source-authenticated Safari teaching: the unchanged nineteen machines plus
/// Surf/Strength and three Safari TM pickups, for exactly the authenticated 110 owned species.
/// No general TM expansion.</summary>
public sealed class SafariMachineCatalog
{
    public const string Profile = "firered-safari-teaching-v1";
    public static ItemId Hm03Id { get; } = new("pret:ITEM_HM03");
    public static ItemId Hm04Id { get; } = new("pret:ITEM_HM04");
    private static readonly ConditionalWeakTable<PewterMachineCatalog, SafariMachineCatalog> Authenticated = new();
    public PewterMachineCatalog Catalog { get; }
    public SafariOwnedPokemonCatalog Pokemon { get; }
    private SafariMachineCatalog(PewterMachineCatalog catalog, SafariOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Pokemon = pokemon; }

    public static SafariMachineCatalog Authenticate(PewterMachineCatalog predecessor,
        PewterMachineCatalog candidate, SafariOwnedPokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(pokemon);
        FuchsiaGymMachineRules.ValidateCatalog(predecessor);
        if (candidate.Profile != Profile || candidate.Compatibility.IsDefault || candidate.Compatibility.Length != 110 ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 23 } machines ||
            candidate with { Profile = predecessor.Profile, Compatibility = predecessor.Compatibility,
                AdditionalMachines = predecessor.AdditionalMachines } != predecessor ||
            !candidate.Compatibility.Take(107).SequenceEqual(predecessor.Compatibility))
            throw new ArgumentException("safari_machine_scope");
        for (int index = 0; index < 18; index++)
        {
            var current = machines[index]; var previous = predecessor.AdditionalMachines!.Value[index];
            if (current is null || current.Compatibility.IsDefault || current.Compatibility.Length != 110 ||
                current with { Compatibility = previous.Compatibility } != previous ||
                !current.Compatibility.Take(107).SequenceEqual(previous.Compatibility))
                throw new ArgumentException("safari_machine_predecessor");
        }
        // Complete records include all 2,640 compatibility cells, source spans, item/move identities
        // and inventory settings. The content layer cannot certify itself by choosing a profile name.
        var proof = new SafariMachineCatalog(candidate with { }, pokemon);
        Authenticated.Add(proof.Catalog, proof);
        return proof;
    }

    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    internal static SafariMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Authenticated.TryGetValue(catalog, out var proof) ? proof : null;
}
