using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>One original DaycareMon: the existing owned Pokemon boxed at deposit, an independent
/// u32 step counter, and optional detached original Mail. No new creature or ownership identity.
/// The same resident/growth representation can later serve the independent Four Island slots.</summary>
public sealed record SourceDayCareResident(PartyPokemon Pokemon, uint Steps, SourceMailMessage? Mail = null);

/// <summary>Route 5 has exactly one slot, independent of Four Island's later two-parent service.</summary>
public sealed record Route5DayCareState(SourceDayCareResident? Resident)
{
    public static Route5DayCareState Empty { get; } = new(Resident: null);
}

public sealed record SourceDayCareQuote(long InstanceId, int LevelBefore, int LevelAfter,
    int LevelsGained, int Cost, uint Steps);

public sealed record SourceDayCareLearnedMove(int Level, MoveId Move, MoveId? ForgottenMove);

public sealed record Route5DayCareResult(Route5DayCareState State, ImmutableArray<PartyPokemon> Party,
    SourceMailState Mail, bool Succeeded, string? Error, SourceDayCareQuote? Quote = null,
    int MoneyPaid = 0, ImmutableArray<SourceDayCareLearnedMove> LearnedMoves = default);
