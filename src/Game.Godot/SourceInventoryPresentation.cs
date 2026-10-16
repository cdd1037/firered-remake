/// <summary>Source display timelines only. Accepted commands own all inventory/storage mutations.</summary>
internal static class MachineCaseLayout
{
    internal const int ListX = 80, ListY = 10, ListWidth = 152, RowHeight = 16, MaximumRows = 5;
    internal static int KeepCursorVisible(int cursor, int firstRow, int stackCount)
        => SourceVerticalMenuPresentation.KeepSelectionVisible(cursor, firstRow, stackCount + 1, MaximumRows);
    internal static int VisibleRows(int stackCount, int firstRow) => Math.Min(MaximumRows, stackCount + 1 - firstRow);
}

internal sealed class MachineDiscPresentation
{
    internal int Displayed { get; private set; } = -1;
    internal int Requested { get; private set; } = -1;
    internal int OffsetY { get; private set; } = 20;
    internal bool Moving { get; private set; }
    private bool raising, initialized;
    internal static (int X, int Y) Position(int index)
    {
        if (index is < -1 or > 57) throw new ArgumentOutOfRangeException(nameof(index));
        if (index == -1) return (27, 54);
        int order = index >= 50 ? index - 50 : index + 8;
        return (41 - 14 * order / 58, 46 + 8 * order / 58);
    }
    internal void Select(int index)
    {
        _ = Position(index);
        if (!initialized) { initialized = true; Displayed = Requested = index; OffsetY = index < 0 ? 20 : 0; return; }
        if (Requested == index) return;
        Requested = index; raising = false; Moving = true;
    }
    internal void AdvanceFrame()
    {
        if (!Moving) return;
        if (!raising)
        {
            if (OffsetY < 20) { OffsetY += 10; return; }
            Displayed = Requested;
            if (Requested < 0) { Moving = false; return; }
            raising = true;
        }
        else if (OffsetY > 0) OffsetY -= 10;
        else Moving = false;
    }
    internal void Reset() { initialized = raising = Moving = false; Displayed = Requested = -1; OffsetY = 20; }
}

internal enum StorageHandAction { None, Grab, Place, Shift, GroupGrab, GroupPlace }
internal readonly record struct StorageHandPose(int HandFrame, int HandY, int MovingY, int TargetY,
    int MovingX, int TargetX, bool AtContact, bool TargetOnTop);

internal sealed class StorageHandPresentation
{
    internal StorageHandAction Action { get; private set; }
    internal int Frame { get; private set; }
    internal bool Active => Action != StorageHandAction.None;
    internal void Begin(StorageHandAction action)
    {
        if (action == StorageHandAction.None || !Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        Action = action; Frame = 0;
    }
    internal void Reset() { Action = StorageHandAction.None; Frame = 0; }
    internal void AdvanceFrame()
    {
        if (!Active) return;
        Frame++;
        if (Frame >= (Action == StorageHandAction.Shift ? 18 : Action == StorageHandAction.Grab ? 20 : 19)) Reset();
    }
    internal StorageHandPose Pose(IReadOnlyList<int> shiftOffsets)
    {
        if (shiftOffsets.Count != 17) throw new ArgumentException("Storage shift requires 17 source samples.", nameof(shiftOffsets));
        if (Action == StorageHandAction.Shift)
        {
            int step = Math.Clamp(Frame, 0, 16), vertical = (step + 1) / 2;
            return new(Frame >= 17 ? 3 : 2, 0, -8 + vertical, -vertical, -shiftOffsets[step], shiftOffsets[step], true, step >= 8);
        }
        bool grabbing = Action is StorageHandAction.Grab or StorageHandAction.GroupGrab;
        int downStart = Action == StorageHandAction.Grab ? 1 : 0;
        int down = Math.Clamp(Frame - downStart, 0, 8);
        bool contact = Frame >= downStart + 9;
        int offset = contact ? Math.Max(0, 8 - (Frame - downStart - 9)) : down;
        return new(grabbing ? contact ? 3 : 2 : Frame >= 18 ? 0 : contact ? 2 : 3, offset,
            grabbing ? contact ? offset - 8 : 0 : contact ? 0 : offset - 8, 0, 0, 0, contact, false);
    }
}
