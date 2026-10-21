using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

// SpecialStatus damage registers, owned by battler position and cleared by ActionFinished.
// dmg stores the first nonzero receipt, including Substitute, rather than an accumulated total.
public sealed record SourceHitDamageReceipt(int Damage = 0, int Physical = 0, int Special = 0);

internal static class SourceHitItemRules
{
    internal static SourceHitDamageReceipt Record(SourceHitDamageReceipt before, int lost, bool substitute,
        bool noEffect, bool physical, bool passive = false)
    {
        if (noEffect) return before.Damage == 0 ? before with { Damage = 0xFFFF } : before;
        var next = before;
        if (before.Damage == 0 && (substitute || !passive)) next = next with { Damage = lost };
        if (!substitute && !passive) next = physical ? next with { Physical = lost } : next with { Special = lost };
        return next;
    }
    internal static bool KingsRockDraw(int commandDamage, bool noEffect, SourceHitDamageReceipt receipt) =>
        commandDamage != 0 && !noEffect && (receipt.Physical != 0 || receipt.Special != 0);
    internal static bool KingsRockProc(ushort roll, int attackerParameter, bool affected, int targetHp) =>
        roll % 100 < attackerParameter && affected && targetHp != 0;
    internal static int ShellBellHeal(int commandDamage, bool noEffect, SourceHitDamageReceipt receipt,
        bool sameBattler, int hp, int maxHp) => commandDamage != 0 && !noEffect && receipt.Damage is not (0 or 0xFFFF) &&
        !sameBattler && hp != 0 && hp != maxHp ? Math.Max(1, receipt.Damage / 8) : 0;
    internal static bool Valid(SourceHitDamageReceipt? receipt) => receipt is null ||
        receipt.Damage is >= 0 and <= 65535 && receipt.Physical is >= 0 and <= 65535 && receipt.Special is >= 0 and <= 65535 && receipt != new SourceHitDamageReceipt();
}

internal sealed partial class FrameRecipeContext
{
    internal void HitItemsPerHitMoveEnd(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) { }
    internal void RecordHitItemDamage(BattlerSlot target, int lost, bool substitute, bool noEffect, bool physical)
    {
        if (SourceHeldDamageEnabled) SetHitItemReceipt(target, SourceHitItemRules.Record(HitItemReceipt(target), lost, substitute, noEffect, physical));
    }
    internal void ApplyHitItems(BattlerSlot actor, BattlerSlot target, MoveId move, bool kingsRockAffected, int commandDamage, bool noEffect)
    {
        if (!SourceHeldDamageEnabled) return;
        var attacker = Get(actor); var receipt = HitItemReceipt(target);
        if (attacker.HeldItem == "ITEM_KINGS_ROCK" && SourceHitItemRules.KingsRockDraw(commandDamage, noEffect, receipt))
        {
            // Approved original-bug fix: use the actual attacker's held parameter, not numeric slot 0.
            // It draws before the move flag, target HP, Shield Dust, Substitute and Inner Focus gates.
            ushort roll = Draw($"wild-battle.slot{actor.Value}.kings-rock", 100);
            if (SourceHitItemRules.KingsRockProc(roll, SourceBattleHeldItems.Parameter(attacker.HeldItem), kingsRockAffected, Get(target).Pokemon.CurrentHp) && !Get(target).Flinched)
                ApplyFlinch(target);
        }
        else if (attacker.HeldItem == "ITEM_SHELL_BELL")
        {
            int heal = SourceHitItemRules.ShellBellHeal(commandDamage, noEffect, receipt, actor == target,
                attacker.Pokemon.CurrentHp, attacker.Pokemon.Stats.MaxHp);
            if (heal == 0) return;
            SetHitItemReceipt(target, receipt with { Damage = 0 });
            int before = attacker.Pokemon.CurrentHp, after = Math.Min(attacker.Pokemon.Stats.MaxHp, before + heal);
            Set(actor, attacker with { Pokemon = attacker.Pokemon with { CurrentHp = after } });
            Add(n => new SourceHeldItemHealEvent(n, Turn, actor, target, move, attacker.HeldItem, receipt.Damage, before, after, after - before));
        }
    }
}
