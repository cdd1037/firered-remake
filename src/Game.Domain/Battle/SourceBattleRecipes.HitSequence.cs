using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasHitSequenceRecipe(string effect) => effect is
        "EFFECT_RAGE" or "EFFECT_FURY_CUTTER" or "EFFECT_TRIPLE_KICK" or "EFFECT_FAKE_OUT";

    internal void ChangeFuryCutterCounter(BattlerSlot actor, int counter, string reason)
    {
        int before = Get(actor).FuryCutterCounter;
        if (before == counter) return;
        Set(actor, Get(actor) with { FuryCutterCounter = counter });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "fury-cutter", before, counter, reason));
    }

    internal void ChangeRage(BattlerSlot actor, bool value, string reason)
    {
        bool before = Get(actor).Rage;
        if (before == value) return;
        Set(actor, Get(actor) with { Rage = value });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "rage", before ? 1 : 0, value ? 1 : 0, reason));
    }

    // MOVEEND_RAGE is enemy-only and requires actual datahpupdate body damage.
    // Neither a Substitute receipt nor Endure's zero loss can build Rage.
    internal void BuildRage(BattlerSlot actor, BattlerSlot target, MoveId move)
    {
        var receipt = HitItemReceipt(target);
        if (actor.Side != target.Side && !ContactCommandNoEffect && SourceCalledHitReachedPokemon(target) &&
            (receipt.Physical > 0 || receipt.Special > 0) && Get(target).Rage &&
            Get(target).Pokemon.CurrentHp > 0 && Get(target).AttackStage < 12)
            ChangeStage(target, target, WildBattleStat.Attack, 1, move);
    }

    internal virtual void HitSequenceReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceGenericEffectiveness(catalog, actor, target, move);

    internal void ExecuteHitSequence(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        if (move.Effect == "EFFECT_TRIPLE_KICK")
        { ExecuteTripleKick(catalog, actor, target, slot, move, purpose); return; }
        if (move.Effect == "EFFECT_FAKE_OUT" && Get(actor).FirstTurnCounter == 0)
        {
            SpendEarlyPartyPp(actor, target, slot, move);
            Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "not-first-turn"));
            return;
        }
        bool fury = move.Effect == "EFFECT_FURY_CUTTER";
        if (fury) SpendEarlyPartyPp(actor, target, slot, move);
        bool hit = SourceGenericAccuracy(actor, target, move, purpose);
        if (move.Effect == "EFFECT_RAGE")
        {
            // SetMoveEffect(primary) still checks the TARGET's Substitute before
            // its MOVE_EFFECT_RAGE case writes the ATTACKER's status2.
            if (!hit) ChangeRage(actor, false, "missed");
            else if (Get(target).CalledVolatile?.SubstituteHp is not > 0) ChangeRage(actor, true, "move");
        }
        if (!fury) SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit)
        {
            if (fury) ChangeFuryCutterCounter(actor, 0, "missed");
            SourceGenericMiss(actor, target, move); return;
        }
        int? power = null;
        if (fury)
        {
            power = WildBattleRules.NextFuryCutterPower(Get(actor).FuryCutterCounter);
            ChangeFuryCutterCounter(actor, Math.Min(5, Get(actor).FuryCutterCounter + 1), "hit");
        }
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        // Fury Cutter's no-effect branch returns to furycuttercalc before variance.
        if (fury && SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, 100, power) == 0)
        {
            ChangeFuryCutterCounter(actor, 0, "no-effect");
            HitSequenceReportType(catalog, actor, target, move); return;
        }
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int rawDamage = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent, power);
        int damage = AdjustHeldDamage(target, move.Id, rawDamage, purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        HitSequenceReportType(catalog, actor, target, move);
        var secondary = SecondaryEffect(actor, move, purpose, rawDamage == 0, certain: move.Effect == "EFFECT_FAKE_OUT");
        if (secondary.Apply && move.Effect == "EFFECT_FAKE_OUT") ApplySecondaryFlinch(target, secondary.Certain);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }

    private void ExecuteTripleKick(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        int actual = 0, lastDamage = 0;
        bool missed = false;
        for (int hit = 1; hit <= 3; hit++)
        {
            if (Get(actor).Pokemon.CurrentHp == 0 || Get(target).Pokemon.CurrentHp == 0 ||
                WildBattleStatus.IsSleeping(Get(actor).Status) && Invocation!.ChosenMove.Value != "pret:MOVE_SLEEP_TALK") break;
            if (!SourceGenericAccuracy(actor, target, move, purpose)) { missed = true; break; }
            bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int rawDamage = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent, hit * 10);
            int damage = AdjustHeldDamage(target, move.Id, rawDamage, purpose);
            if (FixedDamageType(catalog, target, move).Immune) break;
            lastDamage = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
            actual++;
            if (lastDamage > 0) MtMoonRageOnDamage(actor, target, move.Id);
            SourceGenericContact(catalog, actor, target, move, purpose, lastDamage);
            HitItemsPerHitMoveEnd(catalog, actor, target, move);
            SourceMultiHitMoveEnd(catalog, actor, target, move);
            if (Endured(target)) break;
        }
        if (missed && actual == 0) SourceGenericMiss(actor, target, move);
        HitSequenceReportType(catalog, actor, target, move);
        Add(n => new SourceMultiHitEvent(n, Turn, actor, target, move.Id, 3, actual));
        SourceGenericSecondary(catalog, actor, target, move, purpose, lastDamage);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        ForestOutcome(true);
        SourceMultiHitFinalMoveEnd();
    }
}
