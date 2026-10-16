using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

/// <summary>Presentation-only Summary choices. No mode can issue a game command.</summary>
public enum SummaryInteractionMode { ReadOnly, Reorder, Replace, Delete, SelectNew }

public sealed record SummaryMoveSelection(SummaryInteractionMode Mode, int Cursor, int SwapSource = -1, MoveId? NewMove = null,
    ImmutableArray<MoveId> Candidates = default, int FirstRow = 0)
{
    public bool CanReorder => Mode == SummaryInteractionMode.Reorder;
    public bool Swapping => CanReorder && SwapSource >= 0;
    public string Control => Mode switch
    {
        SummaryInteractionMode.ReadOnly => "",
        SummaryInteractionMode.Reorder => Swapping ? "PLACE" : "SWITCH",
        _ => "PICK"
    };
    public int RowCount(int knownMoves) => Mode == SummaryInteractionMode.SelectNew ? Candidates.Length + 1 : knownMoves + (Swapping ? 0 : 1);
    public MoveId? SelectedMove(ImmutableArray<PokemonMoveSlot> moves) => Mode == SummaryInteractionMode.SelectNew
        ? Cursor >= 0 && Cursor < Candidates.Length ? Candidates[Cursor] : null
        : Cursor >= 0 && Cursor < moves.Length
        ? moves[Cursor].Move : Cursor == moves.Length && Mode == SummaryInteractionMode.Replace ? NewMove : null;
    public bool IsCancel(int knownMoves) => Cursor == (Mode == SummaryInteractionMode.SelectNew ? Candidates.Length : knownMoves) && !Swapping;
}

/// <summary>Reopening is required if a menu's authority, identity or revision changes.</summary>
public sealed record SummarySelectionStamp(string Context, long Revision, long InstanceId)
{
    // Only the accepted play-time-only command path may rebase an already-current draft.
    public SummarySelectionStamp AfterPlayTimeOnlyCommit(long before, long after) =>
        Revision == before ? this with { Revision = after } : this;
    public bool Matches(string context, long revision, long instanceId) =>
        Context == context && Revision == revision && InstanceId == instanceId;
}

public static class SummaryInteractionPresentationAssets
{
    public const int MoveX = 120, MoveY = 18, MoveWidth = 120, MoveHeight = 28;
    public static string[] BattlePartyActions(int partyCount, bool egg, bool sendOut) => partyCount == 1 || egg
        ? ["SUMMARY", "CANCEL"] : [sendOut ? "SEND OUT" : "SHIFT", "SUMMARY", "CANCEL"];
    public static int MoveRowAt(float x, float y, int rows) => x >= MoveX && x < MoveX + MoveWidth &&
        y >= MoveY && y < Math.Min(160, MoveY + rows * MoveHeight) ? (int)(y - MoveY) / MoveHeight : -1;
}
