using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Domain.Battle;

internal static class SourcePreparedMoveRules
{
    // Other volatile families retain their own admission checks.
    internal static WildBattleCombatant WithoutPrepared(WildBattleCombatant body)
    {
        if (body.CalledVolatile is not { } value) return body;
        var rest = value with { MagicCoat = false, SnatchOrderIndex = null };
        return body with { CalledVolatile = rest == new WildBattleCalledVolatileState() ? null : rest };
    }
}

internal abstract partial class SourceRecipeContext
{
    internal static bool HasPreparedMoveRecipe(string effect) => effect is "EFFECT_FOCUS_PUNCH" or "EFFECT_SNATCH" or "EFFECT_MAGIC_COAT";
    internal virtual int PreparedActionIndex => 0;
    internal virtual int PreparedBattlerCount => 2;
    internal virtual bool PreparedMagicPressure => true;
    internal virtual BattlerSlot PreparedReflectTarget(BattlerSlot original, BattlerSlot reflector) => original;
    internal virtual (int Physical, int Special) PreparedDamage(BattlerSlot actor) => (0, 0);
    internal virtual void SetPreparedInvocation(SourceMoveInvocation value) => throw new SourceRecipeUnsupported("prepared_invocation_owner_required");
    internal virtual void PreparedPp(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, bool attackString = true) => SpendEarlyPartyPp(actor, target, Invocation!.PpSlot, move);
    internal virtual void PreparedNoEffect() { }
    internal virtual bool PreparedProtectionBlocks(BattlerSlot actor, int slot, PokemonMoveDefinition move) => false;
    internal virtual MoveId? PreparedPreviousMove(BattlerSlot owner) => null;
    private bool preparedRegistersSwapped;
    internal void ResetPreparedRegisters() => preparedRegistersSwapped = false;
    private void PreparedVolatile(BattlerSlot slot, WildBattleCalledVolatileState value) => Set(slot, Get(slot) with {
        CalledVolatile = value == new WildBattleCalledVolatileState() ? null : value });

    internal void ExecutePreparedMove(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose, ref bool handled)
    {
        if (!HasPreparedMoveRecipe(move.Effect)) return;
        if (Invocation is not { } invocation || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("prepared_move_source_definition");
        handled = true;
        if (move.Effect is "EFFECT_SNATCH" or "EFFECT_MAGIC_COAT")
        {
            bool coat = move.Effect == "EFFECT_MAGIC_COAT";
            SetPreparedInvocation(invocation with { Target = coat ? actor : invocation.Target,
                Flags = invocation.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure });
            bool arm = WildBattleSourcePreparedMoves.SnatchCanArm(PreparedActionIndex, PreparedBattlerCount);
            var prior = Get(actor).CalledVolatile ?? new();
            if (arm) PreparedVolatile(actor, coat ? prior with { MagicCoat = true } : prior with { SnatchOrderIndex = PreparedActionIndex });
            // trysetmagiccoat reports its existing compatibility event before attackstring/PP.
            if (arm && coat) Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "magic-coat", prior.MagicCoat ? 1 : 0, 1, "move"));
            PreparedPp(actor, Invocation!.Target, move);
            if (arm && !coat) Add(n => new SourceSnatchReadyEvent(n, Turn, actor, PreparedActionIndex));
            if (!arm) Add(n => new SourceMoveFailedEvent(n, Turn, actor, Invocation!.Target, move.Id, coat ? "magic-coat-last-action" : "snatch-last-action"));
            return;
        }
        var damage = PreparedDamage(actor);
        if (damage.Physical != 0 || damage.Special != 0)
        {
            if (Get(invocation.Target).Protected) PreparedNoEffect();
            PreparedPp(actor, invocation.Target, move, attackString: false);
            Add(n => new SourceFocusPunchLostEvent(n, Turn, actor, damage.Physical, damage.Special));
            return;
        }
        if (!PreparedProtectionBlocks(actor, slot, move)) SourceGenericHit(catalog, actor, invocation.Target, slot, move, purpose);
    }

    // attackcanceler prioritizes Magic Coat, then scans the retained turn queue for Snatch.
    // Each consumed flag permits the next genuine interceptor, but cannot recurse forever.
    internal bool TryPreparedInterception(PokemonMoveDefinition move, bool reflect = true, bool snatch = true)
    {
        if (Invocation is not { } invocation) return false;
        bool coat = reflect && move.Flags.Contains("FLAG_MAGIC_COAT_AFFECTED") && Get(invocation.Target).CalledVolatile is { MagicCoat: true };
        BattlerSlot? interceptor = coat ? invocation.Target : snatch && move.Flags.Contains("FLAG_SNATCH_AFFECTED")
            ? ActiveSlots.Where(s => Get(s).CalledVolatile?.SnatchOrderIndex is not null).OrderBy(s => Get(s).CalledVolatile!.SnatchOrderIndex).Cast<BattlerSlot?>().FirstOrDefault() : null;
        if (interceptor is not { } recipient) return false;
        var actor = invocation.Actor;
        if (!coat || PreparedMagicPressure) PreparedPressure(actor, recipient, coat);
        var armed = Get(recipient).CalledVolatile!;
        PreparedVolatile(recipient, coat ? armed with { MagicCoat = false } : armed with { SnatchOrderIndex = null });
        PreparedPp(actor, invocation.Target, move);
        var target = coat ? PreparedReflectTarget(actor, recipient) : invocation.Actor == invocation.Target ? recipient : actor;
        if (coat) Add(n => new SourceMagicCoatEvent(n, Turn, actor, recipient, move.Id));
        else Add(n => new SourceMoveSnatchedEvent(n, Turn, actor, recipient, move.Id, target));
        SetPreparedInvocation(Invocation! with { Actor = recipient, Target = target, PpOwner = invocation.PpOwner,
            Flags = Invocation!.Flags | WildBattleInvocationFlags.AttackStringPrinted | WildBattleInvocationFlags.NoPpDeduct | WildBattleInvocationFlags.AllowNoPp });
        if (!coat) preparedRegistersSwapped = !preparedRegistersSwapped;
        return true;
    }
    private void PreparedPressure(BattlerSlot original, BattlerSlot recipient, bool coat)
    {
        var body = Get(recipient); string name = coat ? "pret:MOVE_MAGIC_COAT" : "pret:MOVE_SNATCH";
        if (WildBattleSourcePreparedMoves.InterceptionPressureSlot(WildBattleEffective.Ability(Get(original)), body.Pokemon.Moves, new(name)) is not { } slot) return;
        int before = body.Pokemon.Moves[slot].CurrentPp, after = WildBattleSourcePreparedMoves.SnatchPressurePpAfter(before);
        Set(recipient, body with { Pokemon = body.Pokemon with { Moves = body.Pokemon.Moves.SetItem(slot, body.Pokemon.Moves[slot] with { CurrentPp = after }) } });
        if (coat) Add(n => new SourceMagicCoatPressurePpEvent(n, Turn, original, recipient, slot, before, after));
        else Add(n => new SourceSnatchPressurePpEvent(n, Turn, original, recipient, slot, before, after));
    }
    internal void FinishPreparedRegisters()
    {
        if (!preparedRegistersSwapped || Invocation is not { } invocation) return;
        SetPreparedInvocation(invocation with { Actor = invocation.Target, Target = invocation.Actor });
        preparedRegistersSwapped = false;
        if (Invocation!.Actor != invocation.PpOwner)
            Set(invocation.PpOwner, Get(invocation.PpOwner) with { LastUsedMove = PreparedPreviousMove(invocation.PpOwner) });
    }
}
