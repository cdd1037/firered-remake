using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

public sealed record QuestInventoryLimits(int KeyItemSlots, int BallSlots, int MaximumStack);
public sealed record QuestItemDefinition(ItemId Id, string SourceItemSymbol, int SourceItemId,
    string Name, int Price, int Importance, int Registrability, string Pocket, string Type,
    string HoldEffect, int HoldEffectParam, string FieldUseFunction, int BattleUsage,
    string BattleUseFunction, int SecondaryId, bool CanToss, bool CanSell, bool CanGive,
    OpeningItemSourceRef Source);
public sealed record QuestItemsCatalog(string SourceCommit, string Profile, bool CompleteItemCatalog,
    QuestInventoryLimits Limits, ImmutableArray<QuestItemDefinition> Items);
