using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override BattlerSlot? InfatuationSource(BattlerSlot slot) => Frame.Get(slot).Registers.InfatuationSource;
    internal override void SetInfatuationSource(BattlerSlot slot, BattlerSlot? source)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with {
            Registers = cell.Registers with { InfatuationSource = source } }) };
    }
    internal override bool ContactCommandNoEffect => commandNoEffect;
    internal override SourceHitDamageReceipt HitItemReceipt(BattlerSlot slot) => Frame.Get(slot).Position.HitItemReceipt ?? new();
    internal override void SetHitItemReceipt(BattlerSlot slot, SourceHitDamageReceipt receipt)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Position = cell.Position with {
            HitItemReceipt = receipt == new SourceHitDamageReceipt() ? null : receipt } }) };
    }
    private void RecordContactDamage(BattlerSlot target, MoveId id, int damage, int lost)
    {
        // Delayed datahpupdate executes with gCurrentMove=NONE, hence physical TYPE_NORMAL.
        string type = delayedContactDamage ? "TYPE_NORMAL" : catalog.Moves[id].Type;
        bool physical = TypeRules.Category(Enum.Parse<ElementType>(type[5..], true)) == DamageCategory.Physical;
        bool noEffect = fixedDamageNoEffect ?? (damage == 0 && !HeldDamageEffectiveZero(target));
        if (noEffect) commandNoEffect = true;
        SetHitItemReceipt(target, SourceHitItemRules.Record(HitItemReceipt(target), lost,
            substitute: false, noEffect, physical));
    }
    private bool delayedContactDamage;
    private void ClearContactReceipts()
    {
        foreach (var slot in ActiveSlots) if (Frame.Get(slot).Position.HitItemReceipt is not null)
            SetHitItemReceipt(slot, new());
    }
    internal override void Route3ContactAbility(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition move, string purpose, int damage)
    {
        SourceColorChange(catalog, actor, target, move);
        ContactAbility(catalog, actor, target, move, purpose, damage);
    }
    internal override string TypeChangeHitType(PokemonMoveDefinition move) => move.Type;
    internal override string? TypeChangeLastHitType(BattlerSlot actor) => Frame.Get(actor).Registers.History?.LastHitByType;
    internal override BattlerSlot? TypeChangeLastHitBy(BattlerSlot actor) => Frame.Get(actor).Registers.LastHitBy;
    internal override bool TypeChangeMultipleTurns(BattlerSlot source) =>
        Frame.Get(source).Registers.Turn?.Lock is { Kind: not WildBattleCalledLockKind.Recharge };
}
