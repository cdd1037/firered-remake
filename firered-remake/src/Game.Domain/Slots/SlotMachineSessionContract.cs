using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.World;

namespace Game.Domain.Slots;

/// <summary>Explicit bounded admission. No production heap initialization is inferred from Alloc.</summary>
public sealed record SlotMachineSessionContract(SlotMachineRules Rules, SlotBiasState InitialBias, string InitialStateEvidence)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Rules);
        if (InitialBias.Bias is < SlotReward.None or > SlotReward.Seven || string.IsNullOrWhiteSpace(InitialStateEvidence))
            throw new ArgumentException("slot_initial_state_evidence_required");
    }
}

public sealed record SlotMachineSign(int Id, int X, int Y, Direction Facing)
{
    public string Script => GameCornerCoinSource.Prefix + "SlotMachine" + Id;
}

/// <summary>Original physical signs; their ID is never used as the random machine bias index.</summary>
public static class SlotMachineSource
{
    public const string Common = GameCornerCoinSource.Prefix + "SlotMachine";
    public const string ScriptSha256 = "1e05d3520f34fa7a6bad3542e0f99b205d0308cdb9d9e812c33a8f0b71498da5";
    public static ImmutableArray<SlotMachineSign> Signs { get; } = [
        new(0,0,7,Direction.West), new(1,0,9,Direction.West), new(2,0,10,Direction.West),
        new(3,5,6,Direction.East), new(5,5,9,Direction.East), new(6,5,10,Direction.East),
        new(7,6,6,Direction.West), new(8,6,7,Direction.West), new(9,6,9,Direction.West),
        new(10,11,6,Direction.East), new(11,11,7,Direction.East), new(12,11,8,Direction.East),
        new(13,11,10,Direction.East), new(14,12,6,Direction.West), new(16,12,9,Direction.West),
        new(17,12,10,Direction.West), new(19,17,7,Direction.East), new(20,17,8,Direction.East), new(21,17,9,Direction.East)
    ];
}
