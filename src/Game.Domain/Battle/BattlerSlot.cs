using System.Text.Json.Serialization;

namespace Game.Domain.Battle;

public enum SourceBattleLayout { Singles, PresetDoubles }
public enum KoReplacementTiming { SourceImmediate, EndOfTurn }

/// <summary>FireRed non-link positions. A side is never an actor or a unique target.</summary>
public readonly record struct BattlerSlot
{
    [JsonConstructor]
    public BattlerSlot(int value)
    {
        if (value is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
    public int Value { get; }
    [JsonIgnore] public BattleSide Side => (BattleSide)(Value & 1);
    [JsonIgnore] public int Position => Value >> 1;
    [JsonIgnore] public BattlerSlot Partner => new(Value ^ 2);
    [JsonIgnore] public BattlerSlot Opposite => new(Value ^ 1);
    [JsonIgnore] public byte Mask => (byte)(1 << Value);
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Stable actual party identity, independent of species/personality equality.</summary>
public readonly record struct PartyMemberKey
{
    [JsonConstructor]
    public PartyMemberKey(BattleSide side, int index)
    {
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        if (index is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(index));
        Side = side;
        Index = index;
    }
    public BattleSide Side { get; }
    public int Index { get; }
}

/// <summary>Position-owned links use BattlerSlot; permission to act uses this exact entry identity.</summary>
public readonly record struct OccupantToken(BattlerSlot Slot, PartyMemberKey Member, long EntrySerial);

/// <summary>No entropy, clocks, random GUIDs or process counters enter command/continuation identity.</summary>
public readonly record struct SourceActionToken(long Revision, long EventSequence, int Ordinal, OccupantToken Owner);
public readonly record struct SourceContinuationToken(long Revision, long EventSequence, int NextAction,
    SourceActionToken Action);
