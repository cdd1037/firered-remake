using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Original compact trainerItems[4] plus fixed initial itemsNo. Both enemy positions
/// share this inventory. Consumed bits clear slots without compacting them again.</summary>
public sealed record SourceTrainerItemInventory(TrainerId Trainer, int ItemsNo,
    ImmutableArray<string> InitialItems, uint ConsumedMask = 0)
{
    public ImmutableArray<string> RemainingItems => InitialItems.Select((item, i) =>
        (ConsumedMask & (1u << i)) == 0 ? item : "ITEM_NONE").ToImmutableArray();
    public static SourceTrainerItemInventory Create(TrainerDefinition trainer)
    {
        if (trainer?.Items.IsDefault != false || trainer.Items.Length != 4)
            throw new ArgumentException("source_trainer_item_definition");
        var compact = trainer.Items.Where(i => i != "ITEM_NONE").ToImmutableArray();
        return new(trainer.Id, compact.Length, compact.AddRange(Enumerable.Repeat("ITEM_NONE", 4 - compact.Length)));
    }
    internal string? Validate(SourceBattleItemCatalog catalog, TrainerDefinition trainer)
    {
        if (trainer?.Items.IsDefault != false || trainer.Items.Length != 4 || InitialItems.IsDefault || InitialItems.Length != 4 ||
            Trainer != trainer.Id || ItemsNo is < 0 or > 4 || (ConsumedMask & ~((1u << ItemsNo) - 1)) != 0)
            return "source_trainer_item_inventory";
        var expected = Create(trainer);
        if (ItemsNo != expected.ItemsNo || !InitialItems.SequenceEqual(expected.InitialItems) ||
            InitialItems.Take(ItemsNo).Any(item => catalog.Find(new("pret:" + item)) is null))
            return "source_trainer_item_inventory_source";
        return null;
    }
}

public enum SourceTrainerItemKind { FullRestore = 1, HealHp = 2, CureCondition = 3, XStat = 4, GuardSpecs = 5 }
public sealed record SourceTrainerItemChoice(int InventorySlot, ItemId Item, SourceTrainerItemKind Kind, byte Flags);
public sealed record SourceTrainerItemPolicyResult(SourceTrainerItemChoice? Choice, string? Fault)
{
    public bool Succeeded => Fault is null;
}

/// <summary>Original GetAI_ItemType / ShouldUseItem. Pure decision only: no inventory or body
/// mutation, and no RNG. The source action script applies the effect when the item executes.</summary>
public static class SourceTrainerItemPolicy
{
    public static SourceTrainerItemPolicyResult Decide(SourceBattleItemCatalog items, TrainerDefinition trainer,
        SourceBattleFrame frame, BattlerSlot actor, SourceTrainerItemInventory inventory)
    {
        if (SourceBattleIdentityRules.Validate(frame) is { } shape) return new(null, shape);
        if (items is null || inventory is null) return new(null, "source_trainer_item_missing_input");
        if (inventory.Validate(items, trainer) is { } error) return new(null, error);
        if (frame.BattleKind != WildBattleCalledBattleKind.Trainer || actor.Side != BattleSide.Opponent ||
            actor.Value >= frame.BattlerCount || !frame.CanAct(actor)) return new(null, "source_trainer_item_actor");
        var user = frame.Get(actor).Body!; var mon = user.Pokemon;
        int validMons = frame.Party(BattleSide.Opponent).Members.Count(m => !m.IsEgg && m.Pokemon.CurrentHp > 0);
        var remaining = inventory.RemainingItems;
        for (int i = 0; i < 4; i++)
        {
            if (i > 0 && validMons > inventory.ItemsNo - i + 1 || remaining[i] == "ITEM_NONE") continue;
            var item = items.Find(new("pret:" + remaining[i]))!;
            if (!item.IsFixedEffect) continue; // original null item-effect-table row
            var effect = item.EffectBytes;
            if (effect.Length < 6) return new(null, "source_trainer_item_effect_shape");
            SourceTrainerItemKind? kind = unchecked((byte)item.SourceItemId) == 19 ? SourceTrainerItemKind.FullRestore
                : (effect[4] & 4) != 0 ? SourceTrainerItemKind.HealHp
                : (effect[3] & 0x3f) != 0 ? SourceTrainerItemKind.CureCondition
                : (effect[0] & 0x3f) != 0 || effect[1] != 0 || effect[2] != 0 ? SourceTrainerItemKind.XStat
                : (effect[3] & 0x80) != 0 ? SourceTrainerItemKind.GuardSpecs : null;
            bool use = false; byte flags = 0;
            switch (kind)
            {
                case SourceTrainerItemKind.FullRestore:
                    use = mon.CurrentHp > 0 && mon.CurrentHp < mon.Stats.MaxHp / 4;
                    break;
                case SourceTrainerItemKind.HealHp:
                    // GetItemEffectParamOffset(item, 4, ITEM4_HEAL_HP): only the two
                    // preceding EV bits consume arguments before HP in the fixed table.
                    int offset = 6 + ((effect[4] & 1) != 0 ? 1 : 0) + ((effect[4] & 2) != 0 ? 1 : 0);
                    if (offset >= effect.Length) return new(null, "source_trainer_item_hp_parameter");
                    use = mon.CurrentHp > 0 && (mon.CurrentHp < mon.Stats.MaxHp / 4 || mon.Stats.MaxHp - mon.CurrentHp > effect[offset]);
                    break;
                case SourceTrainerItemKind.CureCondition:
                    if ((effect[3] & 0x20) != 0 && WildBattleStatus.IsSleeping(user.Status)) flags |= 0x20;
                    if ((effect[3] & 0x10) != 0 && (user.Status & (WildBattleStatus.Poison | WildBattleSourceFieldEffects.ToxicPoison)) != 0) flags |= 0x10;
                    if ((effect[3] & 8) != 0 && (user.Status & WildBattleStatus.Burn) != 0) flags |= 8;
                    if ((effect[3] & 4) != 0 && (user.Status & WildBattleStatus.Freeze) != 0) flags |= 4;
                    if ((effect[3] & 2) != 0 && (user.Status & WildBattleStatus.Paralysis) != 0) flags |= 2;
                    if ((effect[3] & 1) != 0 && user.ConfusionTurnsRemaining > 0) flags |= 1;
                    use = flags != 0;
                    break;
                case SourceTrainerItemKind.XStat:
                    if (user.FirstTurnCounter == 0) break;
                    if ((effect[0] & 15) != 0) flags |= 1;
                    if ((effect[1] & 0xf0) != 0) flags |= 2;
                    if ((effect[1] & 15) != 0) flags |= 4;
                    if ((effect[2] & 15) != 0) flags |= 8;
                    if ((effect[2] & 0xf0) != 0) flags |= 0x20;
                    if ((effect[0] & 0x30) != 0) flags |= 0x80;
                    use = true; // even at a capped stage / already active Focus Energy
                    break;
                case SourceTrainerItemKind.GuardSpecs:
                    use = user.FirstTurnCounter != 0 && frame.Sides[(int)actor.Side].MistTurnsRemaining == 0;
                    break;
                default:
                    return new(null, null); // source stops on the first non-null unrecognized table row
            }
            if (use) return new(new(i, item.Id, kind!.Value, flags), null);
        }
        return new(null, null);
    }
}
