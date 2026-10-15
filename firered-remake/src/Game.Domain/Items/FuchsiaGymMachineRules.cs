using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Exact nineteen-machine teaching profile. Battle effect admission remains separate.</summary>
public static class FuchsiaGymMachineRules
{
    public const string Profile = "firered-fuchsia-gym-teaching-v1";
    public const int SpeciesCount = 107;
    public const int MachineCount = 19;
    public static ItemId Tm06Id => FuchsiaGymItemRules.Tm06Id;
    public static MoveId ToxicId { get; } = new("pret:MOVE_TOXIC");
    private static readonly HashSet<SpeciesId> IncompatibleSpecies =
        "CATERPIE METAPOD WEEDLE KAKUNA MAGIKARP DITTO".Split(' ')
            .Select(symbol => new SpeciesId("pret:SPECIES_" + symbol)).ToHashSet();

    public static void ValidateCatalog(PewterMachineCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Compatibility.IsDefault || catalog.Compatibility.Length != SpeciesCount || catalog.Compatibility.Any(row => row is null) ||
            catalog.AdditionalMachines is not { IsDefault: false, Length: 18 } extra ||
            extra.Any(machine => machine is null || machine.Machine?.Item is null || machine.Compatibility.IsDefault ||
                machine.Compatibility.Length != SpeciesCount || machine.Compatibility.Any(row => row is null)))
            throw new NotSupportedException("fuchsia_gym_machine_catalog_scope");
        // Validate the complete predecessor without projecting away any inherited row or property.
        FuchsiaMachineRules.ValidateCatalog(catalog with { Profile = FuchsiaMachineRules.Profile,
            AdditionalMachines = extra.Take(MachineCount - 2).ToImmutableArray() });
        var toxic = extra[^1];
        FuchsiaGymItemRules.ValidateMachine(toxic.Machine);
        if (toxic.Move != ToxicId || toxic.SourceMoveId != 92 || toxic.MovePp != 10)
            throw new NotSupportedException("fuchsia_gym_tm06_move_identity");
        // The source TM06 document is sorted numerically; inherited matrices retain historical row order.
        var expected = catalog.Compatibility.OrderBy(row => row.SourceSpeciesId).ToArray();
        for (int index = 0; index < SpeciesCount; index++)
        {
            var basis = expected[index];
            var row = toxic.Compatibility[index];
            if (row is null || row.Species != basis.Species || row.SourceSpeciesId != basis.SourceSpeciesId ||
                row.CanLearn != !IncompatibleSpecies.Contains(basis.Species))
                throw new NotSupportedException("fuchsia_gym_tm06_compatibility_source");
        }
    }
}
