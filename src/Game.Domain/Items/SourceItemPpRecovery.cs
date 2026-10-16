using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>An actual permanent move-slot PP recovery, shared by bag and held-item event metadata.</summary>
public sealed record SourceItemPpRecovery(MoveId Move, int Slot, int Before, int After);
