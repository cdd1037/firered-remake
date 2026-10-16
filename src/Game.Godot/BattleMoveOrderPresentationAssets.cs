/// <summary>Local SELECT draft. Opening, navigating and cancelling never dispatch game commands.</summary>
public sealed record BattleMoveOrderSelection(long SessionRevision, string BattleId, long BattleRevision,
    int ActorSlot, long InstanceId, int SourceSlot)
{
    public BattleMoveOrderSelection AfterPlayTimeOnlyCommit(long before, long after) =>
        SessionRevision == before ? this with { SessionRevision = after } : this;
    public int InitialDestination => SourceSlot == 0 ? 1 : 0;
    public int CancelCursor => SourceSlot;
    public bool Matches(long sessionRevision, string battleId, long battleRevision, int actorSlot, long instanceId) =>
        SessionRevision == sessionRevision && BattleId == battleId && BattleRevision == battleRevision &&
        ActorSlot == actorSlot && InstanceId == instanceId;
    public static int Navigate(int cursor, int occupiedMoves, bool horizontal)
    {
        int candidate = cursor ^ (horizontal ? 1 : 2);
        return candidate < occupiedMoves ? candidate : cursor;
    }
}
