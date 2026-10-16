using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

[Flags]
public enum HallOfFamePcButtons { None = 0, A = 1, B = 2, Up = 64, Down = 128 }
public sealed record HallOfFamePcCursor(int TeamIndex, int HallNumber, int MemberIndex, bool Corrupted = false);
public sealed record HallOfFamePcTransition(HallOfFamePcCursor Cursor, bool Exit, bool SelectionChanged);
public sealed record HallOfFamePcPosition(int X, int Y);

/// <summary>Read-only Task_HofPC_* navigation from the pinned English FireRed source.
/// Member counts and archive indices are oldest-first, matching the stored Hall array.
/// No live Pokémon, game RNG, save operation or award enters these rules.</summary>
public static class HallOfFamePcRules
{
    public const int MaximumTeams = 50;
    public const int MaximumHallNumber = 999;
    public const int EggSpecies = 412;
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string NextHelpSymbol = "gText_UPDOWNPick_ABUTTONNext_BBUTTONBack";
    public const string LastHelpSymbol = "gText_UPDOWNPick_ABUTTONBBUTTONCancel";
    public const string CorruptedHelpSymbol = "gText_ABUTTONExit";
    public static ImmutableArray<HallOfFamePcPosition> FullTeamPositions { get; } =
        [new(120, 40), new(56, 40), new(184, 40), new(120, 88), new(200, 88), new(40, 88)];
    public static ImmutableArray<HallOfFamePcPosition> HalfTeamPositions { get; } =
        [new(120, 64), new(56, 64), new(184, 64)];

    public static bool ValidCounts(ImmutableArray<int> counts) => !counts.IsDefault &&
        counts.Length is >= 1 and <= MaximumTeams && counts.All(n => n is >= 1 and <= 6);

    public static HallOfFamePcCursor Open(ImmutableArray<int> counts, int enteredHallCount, bool archiveReadable = true)
    {
        // Empty or malformed typed archives cannot safely emulate the C code's -1 index.
        // Use its failed-read screen; never repair, fabricate, truncate or reorder records.
        if (!archiveReadable || !ValidCounts(counts) || enteredHallCount is < 0 or > MaximumHallNumber)
            return new(-1, 0, 0, true);
        return new(counts.Length - 1, enteredHallCount, 0);
    }

    public static string HelpSymbol(HallOfFamePcCursor cursor) => cursor.Corrupted ? CorruptedHelpSymbol :
        cursor.TeamIndex == 0 ? LastHelpSymbol : NextHelpSymbol;

    public static HallOfFamePcPosition Position(int memberCount, int memberIndex)
    {
        if (memberCount is < 1 or > 6 || memberIndex < 0 || memberIndex >= memberCount)
            throw new ArgumentOutOfRangeException(nameof(memberIndex));
        return (memberCount > 3 ? FullTeamPositions : HalfTeamPositions)[memberIndex];
    }

    public static HallOfFamePcTransition Apply(HallOfFamePcCursor cursor, ImmutableArray<int> counts, HallOfFamePcButtons buttons)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        if ((buttons & ~(HallOfFamePcButtons.A | HallOfFamePcButtons.B | HallOfFamePcButtons.Up | HallOfFamePcButtons.Down)) != 0)
            throw new ArgumentOutOfRangeException(nameof(buttons));
        bool Pressed(HallOfFamePcButtons button) => (buttons & button) != 0;
        if (cursor.Corrupted)
            return new(cursor, Pressed(HallOfFamePcButtons.A), false); // Error screen accepts A only.
        if (!ValidCounts(counts) || cursor.TeamIndex < 0 || cursor.TeamIndex >= counts.Length ||
            cursor.MemberIndex < 0 || cursor.MemberIndex >= counts[cursor.TeamIndex] || cursor.HallNumber is < 0 or > MaximumHallNumber)
            throw new ArgumentException("hall_pc_cursor_invalid", nameof(cursor));
        // Preserve JOY_NEW priority, including Up+Down at an end of the member list.
        if (Pressed(HallOfFamePcButtons.A))
            return cursor.TeamIndex == 0 ? new(cursor, true, false) :
                new(cursor with { TeamIndex = cursor.TeamIndex - 1, HallNumber = Math.Max(0, cursor.HallNumber - 1), MemberIndex = 0 }, false, true);
        if (Pressed(HallOfFamePcButtons.B)) return new(cursor, true, false);
        if (Pressed(HallOfFamePcButtons.Up) && cursor.MemberIndex > 0)
            return new(cursor with { MemberIndex = cursor.MemberIndex - 1 }, false, true);
        if (Pressed(HallOfFamePcButtons.Down) && cursor.MemberIndex < counts[cursor.TeamIndex] - 1)
            return new(cursor with { MemberIndex = cursor.MemberIndex + 1 }, false, true);
        return new(cursor, false, false);
    }
}
