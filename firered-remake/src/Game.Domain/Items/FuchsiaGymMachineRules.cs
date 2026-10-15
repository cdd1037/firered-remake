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

    // Original tmhm_learnsets.h spans in numeric species order, including six one-line NONE declarations.
    private static readonly (int Start, int End)[] SourceSpans = [
        (11, 29), (31, 49), (51, 72), (74, 96), (98, 120), (122, 149),
        (151, 174), (176, 199), (201, 227), (229, 229), (231, 231), (233, 254),
        (256, 256), (258, 258), (260, 279), (281, 296), (298, 313), (315, 331),
        (333, 356), (358, 384), (386, 401), (403, 419), (421, 441), (443, 464),
        (466, 487), (489, 512), (514, 536), (538, 561), (563, 588), (590, 615),
        (617, 655), (657, 681), (683, 707), (709, 746), (748, 781), (783, 817),
        (819, 836), (858, 889), (891, 923), (925, 945), (947, 968), (970, 986),
        (988, 1004), (1006, 1023), (1025, 1045), (1047, 1068), (1070, 1087), (1089, 1108),
        (1110, 1128), (1130, 1149), (1151, 1176), (1178, 1205), (1264, 1290), (1292, 1319),
        (1321, 1341), (1366, 1386), (1388, 1413), (1445, 1471), (1473, 1499), (1530, 1552),
        (1554, 1576), (1603, 1620), (1622, 1639), (1641, 1659), (1661, 1682), (1684, 1706),
        (1708, 1728), (1730, 1750), (1752, 1774), (1776, 1792), (1813, 1842), (1879, 1894),
        (1896, 1912), (1914, 1930), (1932, 1946), (1948, 1965), (2010, 2033), (2035, 2063),
        (2065, 2081), (2103, 2124), (2126, 2147), (2178, 2198), (2200, 2225), (2227, 2253),
        (2304, 2321), (2323, 2341), (2343, 2364), (2390, 2415), (2417, 2443), (2487, 2521),
        (2523, 2545), (2547, 2570), (2572, 2599), (2675, 2694), (2696, 2733), (2735, 2752),
        (2754, 2772), (2774, 2791), (2793, 2811), (2813, 2835), (2837, 2861), (3044, 3044),
        (3046, 3075), (3106, 3106), (3108, 3122), (3348, 3378), (4950, 4972)
    ];

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
            var span = SourceSpans[index];
            var source = new OpeningItemSourceRef(FuchsiaMachineRules.SourcePath, basis.Species.Value[5..],
                span.Start, span.End);
            if (row is null || row.Species != basis.Species || row.SourceSpeciesId != basis.SourceSpeciesId ||
                row.Source != basis.Source || row.Source != source || row.CanLearn != !IncompatibleSpecies.Contains(basis.Species))
                throw new NotSupportedException("fuchsia_gym_tm06_compatibility_source");
        }
    }
}
