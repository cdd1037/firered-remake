using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasFieldUtilityRecipe(string effect) => effect is "EFFECT_PAY_DAY" or "EFFECT_RAPID_SPIN" or "EFFECT_TELEPORT" or "EFFECT_UPROAR";
    internal abstract int PayDayMoney { get; set; }
    internal abstract void ClearRapidSpinLinks(BattlerSlot actor);
    internal virtual bool UproarLocked(BattlerSlot actor) => Get(actor).CalledTurnState?.Lock?.Kind == WildBattleCalledLockKind.Uproar;
    internal bool SharedUproarPreventsSleep(BattlerSlot actor) => WildBattleEffective.Ability(Get(actor)) != "ABILITY_SOUNDPROOF" && ActiveSlots.Any(UproarLocked);
    internal void SharedUproarWake(BattlerSlot actor)
    {
        if (!WildBattleStatus.IsSleeping(Get(actor).Status) || !SharedUproarPreventsSleep(actor)) return;
        ChangeStatus(actor, WildBattleStatus.None, "uproar-wake");
        if (Get(actor).Nightmare) Set(actor, Get(actor) with { Nightmare = false });
    }
    internal void ExecuteFieldUtility(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move, string purpose)
    {
        if (!HasFieldUtilityRecipe(move.Effect) || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_field_utility_definition");
        if (move.Effect == "EFFECT_UPROAR")
        {
            if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) CancellerCancelMultiTurn(actor, "protected");
            ExecuteRechargeRampage(catalog, actor, move, purpose); return;
        }
        if (move.Effect == "EFFECT_TELEPORT")
        {
            SpendEarlyPartyPp(actor, actor, slot, move);
            string? reason = ForceSwitchTrainer ? "trainer-battle" : TeleportBlocked(catalog, actor) ? "cannot-escape" : null;
            if (reason is not null) Add(n => new SourceMoveFailedEvent(n, Turn, actor, actor, move.Id, reason));
            else TeleportEscape();
            return;
        }
        bool hit = ChargeAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int damage = ChargeDamage(catalog, actor, target, move, critical, percent, purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        ChargeReportType(catalog, actor, target, move);
        var secondary = SecondaryEffect(actor, move, purpose, ChargeImmune(catalog, target, move), certain: move.Effect == "EFFECT_RAPID_SPIN");
        if (secondary.Apply)
        {
            if (move.Effect == "EFFECT_PAY_DAY" && !SourceCalledSecondaryBlocked(target))
            {
                int before = PayDayMoney;
                PayDayMoney = actor.Side == BattleSide.Player ? Math.Min(65535, before + Get(actor).Pokemon.Level * 5) : before;
                Add(n => new SourcePayDayEvent(n, Turn, actor, before, PayDayMoney));
            }
            else if (move.Effect == "EFFECT_RAPID_SPIN" && Get(actor).Pokemon.CurrentHp > 0) RapidSpinFree(actor);
        }
        ChargeFinish(catalog, actor, target, move, purpose, lost);
    }
    internal void RapidSpinFree(BattlerSlot actor)
    {
        var body = Get(actor);
        var seed = LeechSeedSource(actor);
        ClearRapidSpinLinks(actor);
        if (body.BindTurnsRemaining > 0)
        {
            Set(actor, Get(actor) with { BindTurnsRemaining = 0, BindingMove = null });
            Add(n => new SourceBindingChangedEvent(n, Turn, actor, body.BindTurnsRemaining, 0, "rapid-spin") { Move = body.BindingMove });
        }
        if (seed is not null) Add(n => new SourceLeechSeedChangedEvent(n, Turn, actor, seed, null, "rapid-spin"));
        int spikes = SpikesLayers(actor.Side);
        if (spikes > 0) { SetSpikesLayers(actor.Side, 0); Add(n => new SourceCalledSpikesChangedEvent(n, Turn, actor.Side, spikes, 0)); }
    }
    internal virtual bool TeleportBlocked(PokemonCatalog catalog, BattlerSlot actor)
    {
        var body = Get(actor);
        if (body.HeldItem == "ITEM_SMOKE_BALL" || WildBattleEffective.Ability(body) == "ABILITY_RUN_AWAY") return false;
        if (ActiveSlots.Any(s => s.Side != actor.Side && (WildBattleEffective.Ability(Get(s)) == "ABILITY_SHADOW_TAG" ||
            WildBattleEffective.Ability(Get(s)) == "ABILITY_ARENA_TRAP" && WildBattleEffective.Ability(body) != "ABILITY_LEVITATE" && !WildBattleEffective.HasType(catalog, body, "TYPE_FLYING")))) return true;
        if (WildBattleEffective.HasType(catalog, body, "TYPE_STEEL") && ActiveSlots.Any(s => s != actor && WildBattleEffective.Ability(Get(s)) == "ABILITY_MAGNET_PULL")) return true;
        return body.BindTurnsRemaining > 0 || body.Rooted || MeanLookSource(actor) is not null;
    }
    internal virtual void TeleportEscape() => ForceSwitchEscape();
}
