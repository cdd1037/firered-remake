using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>MOVEEND_UPDATE_LAST_MOVES and per-source damage history use actor slots. Event routing
/// is canonical before this reducer runs, so allies and identical species retain independent history.</summary>
internal static class SourceMoveHistoryReducer
{
    internal static SourceBattleFrame Apply(PokemonCatalog catalog, SourceBattleFrame frame, SourceMoveInvocation invocation,
        ImmutableArray<SourceBattleEvent> emitted, bool commandNoEffect, string? effectiveType = null, bool recordDamage = true)
    {
        Apply(catalog, frame.Turn, invocation, emitted, commandNoEffect, s => frame.Get(s),
            (slot, cell) => frame = frame with { Slots = frame.Slots.SetItem(slot.Value, cell) }, effectiveType, recordDamage);
        return frame;
    }
    internal static void Apply(PokemonCatalog catalog, int turn, SourceMoveInvocation invocation,
        ImmutableArray<SourceBattleEvent> emitted, bool commandNoEffect,
        Func<BattlerSlot, SourceBattleSlot> get, Action<BattlerSlot, SourceBattleSlot> set,
        string? effectiveType = null, bool recordDamage = true)
    {
        var actorCell = get(invocation.Actor); var actor = actorCell.Body!;
        var targetSlot = emitted.OfType<SourceConfusionSelfHitEvent>().Any() ? invocation.Actor : invocation.Target;
        bool obeys = invocation.Flags.HasFlag(WildBattleInvocationFlags.Obeys);
        bool noEffect = commandNoEffect || !obeys || emitted.Any(e => e is SourceMoveMissedEvent || e is SourceActionBlockedEvent ||
            e is SourceMoveFailedEvent { Reason: not ("already-full-hp" or "already-asleep" or "already-poisoned" or "already-paralyzed" or "already-confused" or "nothing-happened" or "mist" or "already-substitute" or "substitute-insufficient-hp" or "stockpile-empty") } ||
            e is SourceStatAtMinimumEvent or SourceStatAtMaximumEvent ||
            e is SourceAbilityPreventedEvent { Ability: "ABILITY_SOUNDPROOF" or "ABILITY_LEVITATE" or "ABILITY_WONDER_GUARD" } ||
            e is SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect });
        MoveId unavailable = new("pret:MOVE_UNAVAILABLE");
        var history = actorCell.Registers.History ?? new();
        actor = actor with { LastUsedMove = obeys ? invocation.ChosenMove : unavailable,
            LastCalledMove = obeys && emitted.OfType<SourceCalledMoveTransitionEvent>().Any() && invocation.ChosenMove != unavailable ? invocation.CurrentMove : null };
        set(invocation.Actor, actorCell with { Body = actor,
            Registers = actorCell.Registers with { History = history with {
                LastPrintedMove = invocation.Flags.HasFlag(WildBattleInvocationFlags.AttackStringPrinted) ? invocation.ChosenMove : history.LastPrintedMove,
                LastResultingMove = obeys ? invocation.CurrentMove : unavailable } } });
        var targetCell = get(targetSlot); var target = targetCell.Body!; var targetHistory = targetCell.Registers.History ?? new();
        bool landed = !noEffect && invocation.ChosenMove != unavailable;
        bool mirror = landed && target.Pokemon.CurrentHp > 0 && targetSlot != invocation.Actor &&
            catalog.Moves[invocation.OriginalChosenMove].Flags.Contains("FLAG_MIRROR_MOVE_AFFECTED");
        set(targetSlot, targetCell with {
            Body = target with { LastLandedMove = landed ? invocation.CurrentMove : unavailable },
            Registers = targetCell.Registers with { LastHitBy = target.Pokemon.CurrentHp > 0 ? invocation.Actor : targetCell.Registers.LastHitBy,
                History = targetHistory with {
                    LastHitByType = landed ? effectiveType ?? (invocation.CurrentMove.Value == "pret:MOVE_NONE" ? "TYPE_NORMAL" : catalog.Moves[invocation.CurrentMove].Type) : targetHistory.LastHitByType,
                    LastTakenMove = mirror ? invocation.ChosenMove : targetHistory.LastTakenMove,
                    LastTakenFrom = mirror ? targetHistory.LastTakenFrom.SetItem(invocation.Actor.Value, invocation.ChosenMove) : targetHistory.LastTakenFrom } } });
        if (!recordDamage || invocation.CurrentMove.Value == "pret:MOVE_NONE") return;
        var current = catalog.Moves[invocation.CurrentMove];
        if (current.Effect == "EFFECT_PAIN_SPLIT" || commandNoEffect || emitted.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect && e.Move == current.Id)) return;
        string type = current.Effect == "EFFECT_HIDDEN_POWER" ? current.Type : effectiveType ?? current.Type;
        foreach (var hit in emitted.OfType<SourceHpChangedEvent>())
        {
            if (hit.Actor == hit.Target || hit.Move != current.Id) continue;
            var cell = get(hit.Target);
            var memory = RecordDamage(cell.Registers.Damage, turn, hit.Actor, Math.Max(0, hit.HpBefore - hit.HpAfter), type);
            set(hit.Target, cell with { Registers = cell.Registers with { Damage = memory } });
        }
        return;
    }
    internal static SourceDamageRegisters RecordDamage(SourceDamageRegisters? prior, int turn, BattlerSlot source, int hp, string type)
    {
        var memory = prior is not null && prior.Turn == turn ? prior :
            new SourceDamageRegisters(WildBattleRules.CalledMoveKernelRulesVersion, turn, 0, null, 0, null);
        return TypeRules.Category(Enum.Parse<ElementType>(type[5..], true)) == DamageCategory.Physical
            ? memory with { PhysicalDamage = hp, PhysicalSource = source }
            : memory with { SpecialDamage = hp, SpecialSource = source };
    }

}
