using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Unown is one canonical species. Its personality selects one of 28 visual forms.</summary>
public static class UnownFormRules
{
    public static SpeciesId Species { get; } = new("pret:SPECIES_UNOWN");
    public const int SourceSpeciesId = 201;
    public const int FormCount = 28;
    public const uint PokedexPortraitTrainerId = 8; // SHINY_ODDS in the source portrait caller.

    public static int Form(uint personality) => (int)((
        ((personality & 0x03000000u) >> 18) |
        ((personality & 0x00030000u) >> 12) |
        ((personality & 0x00000300u) >> 6) |
         (personality & 0x00000003u)) % FormCount);

    /// <summary>Source picture/icon table index, never a PokemonInstance species identity.</summary>
    public static int RenderSpeciesId(uint personality)
    {
        int form = Form(personality);
        return form == 0 ? SourceSpeciesId : 412 + form;
    }

    public static int ResolveRenderSpeciesId(int species, uint personality) =>
        species == SourceSpeciesId ? RenderSpeciesId(personality) : species;

    public static string FormLabel(uint personality)
    {
        int form = Form(personality);
        return form < 26 ? ((char)('A' + form)).ToString() : form == 26 ? "!" : "?";
    }

    public static ImmutableArray<MapId> Chambers { get; } =
    [
        new("map.seven-island-tanoby-ruins-monean-chamber"),
        new("map.seven-island-tanoby-ruins-liptoo-chamber"),
        new("map.seven-island-tanoby-ruins-weepth-chamber"),
        new("map.seven-island-tanoby-ruins-dilford-chamber"),
        new("map.seven-island-tanoby-ruins-scufib-chamber"),
        new("map.seven-island-tanoby-ruins-rixy-chamber"),
        new("map.seven-island-tanoby-ruins-viapois-chamber")
    ];

    // src/wild_encounter.c sUnownLetterSlots. Explicit source map order, not lexical order.
    private static readonly ImmutableArray<ImmutableArray<int>> SlotForms =
    [
        [0,0,0,0,0,0,0,0,0,0,0,27],
        [2,2,2,3,3,3,7,7,7,20,20,14],
        [13,13,13,13,18,18,18,18,8,8,4,4],
        [15,15,11,11,9,9,17,17,17,16,16,16],
        [24,24,19,19,6,6,6,5,5,5,10,10],
        [21,21,21,22,22,22,23,23,12,12,1,1],
        [25,25,25,25,25,25,25,25,25,25,25,26]
    ];

    public static bool IsChamber(MapId chamber) => Chambers.Contains(chamber);

    public static int SourceSlotForm(MapId chamber, int sourceSlotIndex)
    {
        int index = Chambers.IndexOf(chamber);
        if (index < 0) throw new ArgumentException("unown_source_chamber", nameof(chamber));
        if (sourceSlotIndex is < 0 or >= 12)
            throw new ArgumentOutOfRangeException(nameof(sourceSlotIndex), "unown_source_slot");
        return SlotForms[index][sourceSlotIndex];
    }

    /// <summary>HandleSetPokedexFlag writes the PID only when the requested flag is first set.
    /// A first capture therefore replaces an earlier seen representative.</summary>
    public static uint? PokedexRepresentativeAfterSeen(uint? current, bool wasSeen, uint personality) =>
        wasSeen ? current : personality;
    public static uint? PokedexRepresentativeAfterCaught(uint? current, bool wasCaught, uint personality) =>
        wasCaught ? current : personality;

    public static bool PokedexPortraitIsShiny(uint personality) =>
        ((PokedexPortraitTrainerId >> 16) ^ (PokedexPortraitTrainerId & 0xffff) ^
         (personality >> 16) ^ (personality & 0xffff)) < 8;
}
