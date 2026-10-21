using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

// STATUS2_SUBSTITUTE is cleared by MOVEEND_SUBSTITUTE, after the breaking hit's
// secondary, contact and held-item commands. HP and status are distinct registers.
internal sealed class SourceSubstituteLifecycle
{
    internal readonly HashSet<BattlerSlot> Faded = [];
    internal readonly HashSet<BattlerSlot> Hit = [];
    internal void BeginAction() { Faded.Clear(); Hit.Clear(); }
}

internal static class SourceSubstituteRules
{
    internal static int Cost(int maxHp) => Math.Max(1, maxHp / 4);
    internal static bool Supported(WildBattleCombatant body) => body.CalledVolatile is not { } value ||
        value != new WildBattleCalledVolatileState() &&
        value.SubstituteHp is >= 0 and <= 255 && value.TauntTurnsRemaining is >= 0 and <= 2 &&
        value.PerishSongTimer is null or >= 0 and <= 3 && value.StockpileCount is >= 0 and <= 3 &&
        (body.Pokemon.CurrentHp > 0 || value.SubstituteHp == 0 && !value.Grudge && !value.Tormented && value.TauntTurnsRemaining == 0 && value.StockpileCount == 0) &&
        value == new WildBattleCalledVolatileState { SubstituteHp = value.SubstituteHp,
            Tormented = value.Tormented, TauntTurnsRemaining = value.TauntTurnsRemaining,
            PerishSongTimer = value.PerishSongTimer, Grudge = value.Grudge, StockpileCount = value.StockpileCount };
    internal static (int After, int Lost) Damage(int hp, int damage)
    {
        int after = Math.Max(0, hp - damage);
        return (after, hp - after);
    }
}

internal sealed partial class FrameRecipeContext
{
    private readonly SourceSubstituteLifecycle localSubstituteLifecycle = new();
    internal SourceSubstituteLifecycle SubstituteLifecycle => localSubstituteLifecycle;
    internal bool SubstitutePresent(BattlerSlot target) => Get(target).CalledVolatile?.SubstituteHp > 0 || SubstituteLifecycle.Faded.Contains(target);
    internal bool SubstituteHitReachedPokemon(BattlerSlot target) => !SubstituteLifecycle.Hit.Contains(target);
    internal void BeginSubstituteAction() => SubstituteLifecycle.BeginAction();
    internal void EndSubstitutePass() => SubstituteLifecycle.Faded.Clear();
    internal void SubstituteCostReceipt(BattlerSlot actor, PokemonMoveDefinition move, int cost)
    {
        HitItemsCommandDamage(cost);
        RecordHitItemDamage(actor, cost, substitute: false, noEffect: false, physical: true);
    }
    internal void ApplySubstitute(BattlerSlot actor, PokemonMoveDefinition move)
    {
        if (SubstitutePresent(actor)) { SourceStatRecoveryFail(actor, actor, move, "already-substitute"); return; }
        var body = Get(actor); int cost = SourceSubstituteRules.Cost(body.Pokemon.Stats.MaxHp);
        int before = body.Pokemon.CurrentHp;
        if (before <= cost) { SourceStatRecoveryFail(actor, actor, move, "substitute-insufficient-hp"); return; }
        SubstituteCostReceipt(actor, move, cost);
        Set(actor, body with { Pokemon = body.Pokemon with { CurrentHp = before - cost },
            BindTurnsRemaining = 0, BindingMove = null,
            CalledVolatile = (body.CalledVolatile ?? new()) with { SubstituteHp = cost } });
        SubstituteClearsBinding(actor);
        Add(n => new SourceHpChangedEvent(n, Turn, actor, actor, move.Id, before, before - cost, cost, false, 100));
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "substitute", 0, cost, "move"));
        if (body.BindTurnsRemaining > 0)
            Add(n => new SourceBindingChangedEvent(n, Turn, actor, body.BindTurnsRemaining, 0, "substitute") { Move = body.BindingMove });
    }

    // IGNORE_SUBSTITUTE costs, passive damage and healing never enter this boundary.
    internal bool ConsumeSubstituteDamage(BattlerSlot actor, BattlerSlot target, MoveId move,
        int damage, bool critical, int percent, out int lost, bool noEffect = false, int? preparedBasePower = null, int? preparedFuryCutterCounter = null, int? preparedAnimationPower = null, ushort? preparedAnimationWeather = null)
    {
        SubstituteLifecycle.Hit.Remove(target); lost = 0;
        var value = Get(target).CalledVolatile;
        if (value is null || value.SubstituteHp == 0 || damage < 0 || noEffect) return false;
        var result = SourceSubstituteRules.Damage(value.SubstituteHp, damage); lost = result.Lost;
        var next = value with { SubstituteHp = result.After };
        Set(target, Get(target) with { CalledVolatile = next == new WildBattleCalledVolatileState() ? null : next });
        SubstituteLifecycle.Hit.Add(target);
        if (result.After == 0) SubstituteLifecycle.Faded.Add(target);
        Add(n => new SourceSubstituteDamagedEvent(n, Turn, actor, target, move, value.SubstituteHp, result.After, damage, critical, percent) { PreparedBasePower = preparedBasePower, PreparedFuryCutterCounter = preparedFuryCutterCounter, PreparedAnimationPower = preparedAnimationPower, PreparedAnimationWeather = preparedAnimationWeather });
        if (critical) Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        return true;
    }

    // Explicit script-level jumps, not a blanket status-move rule. ChangeStatBuffs
    // itself permits Haze/Psych Up/Tickle; target secondaries use the separate gate.
    internal bool SubstituteScriptBlocks(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move)
    {
        if (Invocation is null || !SubstitutePresent(Invocation.Target)) return false;
        var target = Invocation.Target;
        string ability = WildBattleEffective.Ability(Get(target));
        bool directStage = move.Effect is "EFFECT_ATTACK_DOWN" or "EFFECT_DEFENSE_DOWN" or "EFFECT_SPEED_DOWN" or
            "EFFECT_SPECIAL_ATTACK_DOWN" or "EFFECT_SPECIAL_DEFENSE_DOWN" or "EFFECT_ACCURACY_DOWN" or "EFFECT_EVASION_DOWN" or
            "EFFECT_ATTACK_DOWN_2" or "EFFECT_DEFENSE_DOWN_2" or "EFFECT_SPEED_DOWN_2" or "EFFECT_SPECIAL_ATTACK_DOWN_2" or
            "EFFECT_SPECIAL_DEFENSE_DOWN_2" or "EFFECT_ACCURACY_DOWN_2" or "EFFECT_EVASION_DOWN_2";
        bool blocked = directStage || move.Effect switch {
            "EFFECT_SLEEP" or "EFFECT_LEECH_SEED" or "EFFECT_LOCK_ON" or "EFFECT_NIGHTMARE" or "EFFECT_WILL_O_WISP" or
                "EFFECT_PAIN_SPLIT" or "EFFECT_DREAM_EATER" or "EFFECT_MEAN_LOOK" or "EFFECT_SWAGGER" or "EFFECT_FLATTER" => true,
            "EFFECT_POISON" or "EFFECT_TOXIC" => ability != "ABILITY_IMMUNITY",
            "EFFECT_CONFUSE" => ability != "ABILITY_OWN_TEMPO",
            "EFFECT_PARALYZE" => ability != "ABILITY_LIMBER",
            "EFFECT_YAWN" => ability is not ("ABILITY_VITAL_SPIRIT" or "ABILITY_INSOMNIA"),
            "EFFECT_CURSE" => WildBattleEffective.HasType(catalog, Get(actor), "TYPE_GHOST"),
            _ => false
        };
        if (!blocked) return false;
        SpendEarlyPartyPp(actor, target, slot, move);
        if (move.Effect is "EFFECT_SWAGGER" or "EFFECT_FLATTER" ||
            Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) SourceGenericMiss(actor, target, move);
        else SourceStatRecoveryFail(actor, target, move, "substitute");
        return true;
    }
}
