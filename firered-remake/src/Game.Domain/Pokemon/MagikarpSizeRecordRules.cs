using System.Globalization;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>The numeric result values returned by the original CompareMagikarpSize special.</summary>
public enum MagikarpSizeComparisonOutcome
{
    Cancelled = 0,
    WrongSpeciesOrEgg = 1,
    Smaller = 2,
    NewRecord = 3,
    Equal = 4
}

public sealed record MagikarpSizeComparison(MagikarpSizeComparisonOutcome Outcome,
    ushort PreviousRecord, ushort UpdatedRecord, ushort? CandidateRecord,
    uint? PreviousSizeTenthsCentimeters, uint? CandidateSizeTenthsCentimeters)
{
    public bool RecordChanged => PreviousRecord != UpdatedRecord;
}

/// <summary>The source state immediately before attempting the Net Ball gift. A failed gift does
/// not reverse UpdatedRecord or RecordFlagAfterComparison. Inventory and persistence are external.</summary>
public sealed record MagikarpRecordScriptDecision(MagikarpSizeComparison Comparison,
    bool RecordFlagAfterComparison, bool AttemptNetBallGift);

/// <summary>RNG-free source Magikarp sizing and record comparison. The persisted record is the
/// 16-bit personality/IV hash, not a length. No Pokemon, inventory, save or UI state is mutated.</summary>
public static class MagikarpSizeRecordRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourcePath = "src/pokemon_size_record.c";
    public const string ScriptPath = "data/maps/Route12_FishingHouse/scripts.inc";
    public const ushort DefaultRecord = 0;
    public const int PartyCapacity = 6;
    public const int MagikarpHeightDecimeters = 9;
    public static SpeciesId MagikarpId { get; } = new("pret:SPECIES_MAGIKARP");

    // Original sBigMonSizeTable fields, including the source's skipped index 14 lookup.
    private static readonly (uint Base, uint Divisor, uint Threshold)[] SizeTable =
    [
        (290, 1, 0), (300, 1, 10), (400, 2, 110), (500, 4, 310),
        (600, 20, 710), (700, 50, 2710), (800, 100, 7710), (900, 150, 17710),
        (1000, 150, 32710), (1100, 100, 47710), (1200, 50, 57710), (1300, 20, 62710),
        (1400, 5, 64710), (1500, 2, 65210), (1600, 1, 65410), (1700, 1, 65510)
    ];

    /// <summary>GetMonSizeHash: only PID bits 0..15 and each IV's low nibble participate.</summary>
    public static ushort Hash(uint personality, SixStats ivs)
    {
        ArgumentNullException.ThrowIfNull(ivs);
        if (ivs.Values.Any(value => value is < 0 or > 31))
            throw new ArgumentOutOfRangeException(nameof(ivs), "Source IVs must be between 0 and 31.");
        uint pid = personality & 0xffff;
        uint high = (uint)(((ivs.Attack & 15) ^ (ivs.Defense & 15)) * (ivs.Hp & 15)) ^ (pid & 0xff);
        uint low = (uint)(((ivs.SpecialAttack & 15) ^ (ivs.SpecialDefense & 15)) * (ivs.Speed & 15)) ^ (pid >> 8);
        return checked((ushort)((high << 8) + low));
    }

    /// <summary>GetMonSize for the original Magikarp dex height 9. The returned integer is in
    /// tenths of a centimeter; comparison precedes the lossy inch display conversion.</summary>
    public static uint SizeTenthsCentimeters(ushort record)
    {
        int index;
        for (index = 1; index < 15; index++)
            if (record < SizeTable[index].Threshold) break;
        if (index < 15) index--;
        var row = SizeTable[index];
        // The C source promotes to u64 before subtracting. For 65410..65509 its index-15
        // subtraction wraps, then adding 1700 wraps back to the intended 1600..1699 factor.
        ulong factor = unchecked(row.Base + unchecked((ulong)record - row.Threshold) / row.Divisor);
        return checked((uint)(MagikarpHeightDecimeters * factor / 10));
    }

    /// <summary>English UNITS_IMPERIAL: truncate size * 100 / 254, then print one decimal.
    /// This is integer truncation, not floating-point rounding.</summary>
    public static string FormatInches(uint sizeTenthsCentimeters) =>
        FormatTenths(checked((uint)((ulong)sizeTenthsCentimeters * 100 / 254)));

    public static string FormatCentimeters(uint sizeTenthsCentimeters) => FormatTenths(sizeTenthsCentimeters);

    private static string FormatTenths(uint value) =>
        (value / 10).ToString(CultureInfo.InvariantCulture) + "." + (value % 10).ToString(CultureInfo.InvariantCulture);

    /// <summary>CompareMonSize. Cancel/invalid selections leave the source string buffers alone,
    /// represented here by null sizes. Eggs are refused; fainting and OT are not restrictions.</summary>
    public static MagikarpSizeComparison Compare(int selectedPartyIndex, PartyPokemon? selected, ushort currentRecord)
    {
        if (selectedPartyIndex is < 0 or >= PartyCapacity)
            return new(MagikarpSizeComparisonOutcome.Cancelled, currentRecord, currentRecord, null, null, null);
        if (selected is null || selected.IsEgg || selected.Pokemon.Species != MagikarpId)
            return new(MagikarpSizeComparisonOutcome.WrongSpeciesOrEgg, currentRecord, currentRecord, null, null, null);
        ushort candidate = Hash(selected.Pokemon.Personality, selected.Pokemon.Ivs);
        uint oldSize = SizeTenthsCentimeters(currentRecord), newSize = SizeTenthsCentimeters(candidate);
        var outcome = newSize == oldSize ? MagikarpSizeComparisonOutcome.Equal
            : newSize < oldSize ? MagikarpSizeComparisonOutcome.Smaller : MagikarpSizeComparisonOutcome.NewRecord;
        return new(outcome, currentRecord, outcome == MagikarpSizeComparisonOutcome.NewRecord ? candidate : currentRecord,
            candidate, oldSize, newSize);
    }

    /// <summary>Original script branches: with no prior record flag, even a smaller/tied valid
    /// Magikarp enters NewRecordMagikarp. Set the flag before giveitem; only comparison result 3
    /// changes the numeric record. A full bag does not make the same record reward repeatable.</summary>
    public static MagikarpRecordScriptDecision Evaluate(int selectedPartyIndex, PartyPokemon? selected,
        ushort currentRecord, bool recordAlreadySet)
    {
        var comparison = Compare(selectedPartyIndex, selected, currentRecord);
        bool valid = comparison.Outcome is MagikarpSizeComparisonOutcome.Smaller
            or MagikarpSizeComparisonOutcome.NewRecord or MagikarpSizeComparisonOutcome.Equal;
        bool reward = comparison.Outcome == MagikarpSizeComparisonOutcome.NewRecord || valid && !recordAlreadySet;
        return new(comparison, recordAlreadySet || reward, reward);
    }
}
