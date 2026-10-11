using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasLateTurnRecipe(string effect) => effect is "EFFECT_BIDE" or "EFFECT_PERISH_SONG" or "EFFECT_DESTINY_BOND" or "EFFECT_GRUDGE";
    internal virtual bool BideInCanceller => true;
    internal virtual SourceTurnLock? BideLock(BattlerSlot actor) => null;
    internal virtual void ChangeBideLock(BattlerSlot actor, SourceTurnLock? value, string reason) => throw new SourceRecipeUnsupported("source_bide_storage");
    internal virtual void StartBide(BattlerSlot actor, PokemonMoveDefinition move) => throw new SourceRecipeUnsupported("source_bide_storage");
    internal virtual void BideConditionsPassed() { }
    internal virtual BattlerSlot? BideReleaseTarget(BattlerSlot actor, BattlerSlot stored, PokemonMoveDefinition move) => stored;
    internal virtual bool BideAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose) => FixedDamageAccuracy(actor, target, move, purpose);
    internal virtual bool BideImmune(PokemonCatalog catalog, BattlerSlot target, PokemonMoveDefinition move) => FixedDamageType(catalog, target, move).Immune;
    internal virtual void BideTypeResult(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, bool immune)
    { if (immune) Add(n => new SourceEffectivenessEvent(n, Turn, actor, target, move.Id, WildBattleEffectiveness.NoEffect)); }
    internal virtual int BideDamage(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, int damage, string purpose, bool immune) =>
        FixedDamageApply(actor, target, move, FixedDamageAdjust(target, move, damage, purpose), false, 100, immune);
    internal virtual void BideFinish(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose, int lost)
    {
        SourceDamageResultMessage(actor, target, move);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }
    internal void ExecuteLateTurn(BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        switch (move.Effect)
        {
            case "EFFECT_BIDE": StartBide(actor, move); break;
            case "EFFECT_PERISH_SONG": ApplyPerishSong(actor, target, move); break;
            case "EFFECT_DESTINY_BOND": SetDestinyBond(actor, true, "move"); break;
            case "EFFECT_GRUDGE": ApplyGrudge(actor, move); break;
        }
    }
    internal void SetLateTurnVolatile(BattlerSlot slot, WildBattleCalledVolatileState value) =>
        Set(slot, Get(slot) with { CalledVolatile = value == new WildBattleCalledVolatileState() ? null : value });
    internal void SetDestinyBond(BattlerSlot actor, bool value, string reason)
    {
        if (Get(actor).DestinyBond == value) return;
        Set(actor, Get(actor) with { DestinyBond = value });
        Add(n => new SourceTowerVolatileEvent(n, Turn, actor, "destiny-bond", value, reason));
    }
    internal void ClearGrudge(BattlerSlot actor, string reason)
    {
        if (Get(actor).CalledVolatile is not { Grudge: true } value) return;
        SetLateTurnVolatile(actor, value with { Grudge = false });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "grudge", 1, 0, reason));
    }
    internal void ApplyGrudge(BattlerSlot actor, PokemonMoveDefinition move)
    {
        var value = Get(actor).CalledVolatile ?? new();
        if (value.Grudge) { Add(n => new SourceMoveFailedEvent(n, Turn, actor, actor, move.Id, "already-grudge")); return; }
        SetLateTurnVolatile(actor, value with { Grudge = true });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "grudge", 0, 1, "move"));
    }
    internal virtual bool PerishSongPressureEnabled => true;
    internal virtual ulong LateTurnRngCursor => 0;
    internal void PerishSongPressure(BattlerSlot actor)
    {
        if (!PerishSongPressureEnabled) return;
        int pressure = ActiveSlots.Count(s => s != actor && WildBattleEffective.Ability(Get(s)) == "ABILITY_PRESSURE");
        var body = Get(actor); int slot = -1;
        for (int i = 0; i < body.Pokemon.Moves.Length; i++) if (body.Pokemon.Moves[i].Move.Value == "pret:MOVE_PERISH_SONG") { slot = i; break; }
        if (pressure == 0 || slot < 0) return;
        var pp = body.Pokemon.Moves[slot]; int after = Math.Max(0, pp.CurrentPp - pressure);
        Set(actor, body with { Pokemon = body.Pokemon with { Moves = body.Pokemon.Moves.SetItem(slot, pp with { CurrentPp = after }) } });
        Add(n => new SourcePpCommandEvent(n, Turn, actor, actor, slot, Invocation!.ChosenMove, new("pret:MOVE_PERISH_SONG"),
            pp.CurrentPp, after, pressure, LateTurnRngCursor) { PpOwner = actor });
    }
    internal void ApplyPerishSong(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        bool changed = false;
        // Cmd_trysetperishsong traverses all numeric positions, including source-absent ones.
        foreach (var recipient in ActiveSlots)
        {
            var value = Get(recipient).CalledVolatile ?? new();
            if (WildBattleEffective.Ability(Get(recipient)) == "ABILITY_SOUNDPROOF" || value.PerishSongTimer is not null) continue;
            SetLateTurnVolatile(recipient, value with { PerishSongTimer = 3 });
            Add(n => new SourceMtMoonVolatileEvent(n, Turn, recipient, "perish-song", -1, 3, "move")); changed = true;
        }
        PerishSongPressure(actor);
        if (!changed) { Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "perish-song-already-active")); return; }
        foreach (var recipient in ActiveSlots)
            if (WildBattleEffective.Ability(Get(recipient)) == "ABILITY_SOUNDPROOF")
            { RecordAbility(recipient, "ABILITY_SOUNDPROOF"); Add(n => new SourceAbilityActivatedEvent(n, Turn, recipient, "ABILITY_SOUNDPROOF")); }
    }
    internal void StoreBideDamage(BattlerSlot source, BattlerSlot target, int damage)
    {
        if (BideLock(target) is not { } locked || damage < 0 || damage == 0 && locked.StoredDamage == 0) return;
        // datahpupdate also refreshes the last source on effective zero damage. Before
        // any energy exists that source is unused, so retain the existing empty encoding.
        ChangeBideLock(target, locked with { StoredDamage = checked(locked.StoredDamage + damage), StoredDamageSource = source }, "stored-damage");
    }
    internal bool ContinueBide(PokemonCatalog catalog, BattlerSlot actor, PokemonMoveDefinition move, string purpose)
    {
        if (BideLock(actor) is not { } locked) return false;
        if (locked.TurnsRemaining > 1)
        { ChangeBideLock(actor, locked with { TurnsRemaining = locked.TurnsRemaining - 1 }, "storing-energy"); return true; }
        ChangeBideLock(actor, null, "released");
        BideConditionsPassed();
        if (locked.StoredDamage == 0)
        { Add(n => new SourceMoveFailedEvent(n, Turn, actor, Invocation!.Target, move.Id, "bide-no-energy")); return true; }
        var recipient = BideReleaseTarget(actor, locked.StoredDamageSource!.Value, move);
        if (recipient is not { } target)
        { Add(n => new SourceMoveFailedEvent(n, Turn, actor, Invocation!.Target, move.Id, "deferred-ko-no-target")); return true; }
        RetaliationTarget(target);
        if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) CancellerCancelMultiTurn(actor, "protected");
        if (!BideAccuracy(actor, target, move, purpose)) { SourceGenericMiss(actor, target, move); return true; }
        bool immune = BideImmune(catalog, target, move);
        int lost = BideDamage(actor, target, move, checked(locked.StoredDamage * 2), purpose, immune);
        BideTypeResult(catalog, actor, target, move, immune);
        BideFinish(catalog, actor, target, move, purpose, lost);
        return true;
    }
    internal void TakeDestinyBond(BattlerSlot attacker)
    {
        int hp = Get(attacker).Pokemon.CurrentHp;
        if (hp == 0) return;
        Set(attacker, Get(attacker) with { Pokemon = Get(attacker).Pokemon with { CurrentHp = 0 } });
        Add(n => new SourceTowerHpEvent(n, Turn, attacker, "destiny-bond", hp, 0, hp));
        ForestFaint(attacker);
    }
    // The source uses the chosen move POSITION, even when a caller executed another move.
    internal void ApplyGrudgePp(BattlerSlot fainted, bool passive)
    {
        if (passive || Get(fainted).CalledVolatile is not { Grudge: true } || Invocation is not { } invocation ||
            invocation.Actor.Side == fainted.Side || Get(invocation.Actor).Pokemon.CurrentHp == 0 || invocation.CurrentMove == WildBattleRules.StruggleMove) return;
        var actor = Get(invocation.Actor); int slot = invocation.ChosenSlot;
        if (slot < 0 || slot >= actor.Pokemon.Moves.Length) throw new SourceRecipeUnsupported("called_grudge_chosen_slot");
        int pp = actor.Pokemon.Moves[slot].CurrentPp;
        Set(invocation.Actor, actor with { Pokemon = actor.Pokemon with { Moves = actor.Pokemon.Moves.SetItem(slot, actor.Pokemon.Moves[slot] with { CurrentPp = 0 }) } });
        Add(n => new SourceGrudgePpEvent(n, Turn, fainted, invocation.Actor, actor.Pokemon.Moves[slot].Move, slot, pp));
    }
}
