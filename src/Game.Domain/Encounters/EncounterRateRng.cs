using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>FireRed WildEncounterRandom uses ISO_RANDOMIZE2, not the global Random LCG increment.</summary>
public static class EncounterRateRng
{
    public const string AlgorithmId = "firered-wild-rate-lcg32-hi16-v1";
    public static (EncounterRateRngState State, RandomTrace Trace) Next(EncounterRateRngState state)
    {
        if (state.AlgorithmId != AlgorithmId) throw new NotSupportedException("Unknown wild encounter rate RNG algorithm.");
        if (state.Cursor == ulong.MaxValue) throw new OverflowException("Wild encounter rate RNG cursor exhausted.");
        uint seed = unchecked(state.Seed * 1103515245u + 12345u);
        ushort raw = (ushort)(seed >> 16);
        var next = state with { Seed = seed, Cursor = state.Cursor + 1 };
        return (next, new(next.Cursor, "encounter.rate", state.Seed, seed, raw, 1600, raw % 1600));
    }
}
