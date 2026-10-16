namespace Game.Domain.Randomness;

public readonly record struct RngState(uint Seed, ulong Cursor = 0, string AlgorithmId = Gen3Rng.AlgorithmId);
public sealed record RandomTrace(ulong Cursor, string Purpose, uint Before, uint After, ushort Raw, int Bound, int Result);
public sealed record RngDraw(RngState State, ushort Value, RandomTrace Trace);

/// <summary>Exact unsigned 32-bit LCG and high-16-bit output from the pinned FireRed Random function.</summary>
public static class Gen3Rng
{
    public const string AlgorithmId = "gen3-lcg32-hi16-v1";
    public static RngDraw Next(RngState state, string purpose, int bound = 65536)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (state.AlgorithmId != AlgorithmId) throw new NotSupportedException("Unknown RNG algorithm; explicit migration is required.");
        if (bound is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(bound));
        if (state.Cursor == ulong.MaxValue) throw new OverflowException("RNG cursor exhausted.");
        uint seed = unchecked(state.Seed * 0x41C64E6Du + 0x6073u);
        ushort raw = (ushort)(seed >> 16);
        var next = new RngState(seed, state.Cursor + 1);
        return new(next, raw, new(next.Cursor, purpose, state.Seed, seed, raw, bound, raw % bound));
    }
}
