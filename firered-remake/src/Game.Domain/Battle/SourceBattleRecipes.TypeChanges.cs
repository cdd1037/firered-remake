using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasTypeChangeRecipe(string effect) => effect is "EFFECT_CONVERSION" or "EFFECT_CONVERSION_2" or "EFFECT_CAMOUFLAGE";
    internal virtual bool TypeChangeEnabled(PokemonCatalog catalog) => true;
    internal virtual bool TypeChangeNoEffect(PokemonMoveDefinition move) => ContactCommandNoEffect;
    internal abstract string TypeChangeHitType(PokemonMoveDefinition move);
    internal abstract string? TypeChangeLastHitType(BattlerSlot actor);
    internal abstract BattlerSlot? TypeChangeLastHitBy(BattlerSlot actor);
    internal abstract bool TypeChangeMultipleTurns(BattlerSlot source);

    internal void SetBattleType(BattlerSlot slot, PokemonMoveDefinition move, string type)
    {
        var body = Get(slot); var before = body.CalledOverlay;
        var after = (before ?? new()) with { Type1 = type, Type2 = type };
        Set(slot, body with { CalledOverlay = after });
        Add(n => new SourceCalledOverlayChangedEvent(n, Turn, slot, move.Id, before, after));
    }

    // TARGET_TURN_DAMAGED is retained SpecialStatus damage, including a prior delayed
    // physical receipt followed by a zero-loss special hit. ActionFinished owns cleanup.
    // Both owners supply the live Hidden Power/Weather Ball type register.
    internal void SourceColorChange(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition? move)
    {
        if (!TypeChangeEnabled(catalog) || move is null || !SourceMoveDefinitionRules.IsValid(move) ||
            move.Id == WildBattleRules.StruggleMove || move.Power == 0 || TypeChangeNoEffect(move) ||
            HitItemReceipt(target) is not { } receipt || receipt.Physical == 0 && receipt.Special == 0) return;
        var holder = Get(target);
        if (holder.Pokemon.CurrentHp == 0 || WildBattleEffective.Ability(holder) != "ABILITY_COLOR_CHANGE") return;
        string type = TypeChangeHitType(move);
        if (WildBattleEffective.HasType(catalog, holder, type)) return;
        RecordAbility(target, "ABILITY_COLOR_CHANGE");
        Add(n => new SourceAbilityActivatedEvent(n, Turn, target, "ABILITY_COLOR_CHANGE"));
        SetBattleType(target, move, type);
    }

    internal void ExecuteTypeChange(PokemonCatalog catalog, BattlerSlot actor, int slot,
        PokemonMoveDefinition move, string purpose, ref bool handled)
    {
        if (!HasTypeChangeRecipe(move.Effect)) return;
        if (Invocation is null || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_type_change_definition");
        handled = true;
        SpendEarlyPartyPp(actor, actor, slot, move);
        var body = Get(actor);
        void Fail(string reason) => Add(n => new SourceMoveFailedEvent(n, Turn, actor, actor, move.Id, reason));
        if (move.Effect == "EFFECT_CAMOUFLAGE")
        {
            var terrain = SettingWeather.Terrain ?? throw new SourceRecipeUnsupported("called_camouflage_terrain");
            var type = WildBattleSourcePartyEffects.CamouflageType(terrain);
            if (WildBattleEffective.HasType(catalog, body, type)) Fail("already-type");
            else SetBattleType(actor, move, type);
            return;
        }
        if (move.Effect == "EFFECT_CONVERSION")
        {
            string MoveType(int index)
            {
                string type = catalog.Moves[body.Pokemon.Moves[index].Move].Type;
                return type == "TYPE_MYSTERY" ? WildBattleEffective.HasType(catalog, body, "TYPE_GHOST") ? "TYPE_GHOST" : "TYPE_NORMAL" : type;
            }
            if (!Enumerable.Range(0, body.Pokemon.Moves.Length).Any(i => !WildBattleEffective.HasType(catalog, body, MoveType(i))))
            { Fail("no-different-move-type"); return; }
            int index; string chosen;
            do { do { index = Draw($"{purpose}.conversion-slot", 4) & 3; } while (index >= body.Pokemon.Moves.Length); chosen = MoveType(index); }
            while (WildBattleEffective.HasType(catalog, body, chosen));
            SetBattleType(actor, move, chosen);
            return;
        }
        if (body.LastLandedMove is not { } landed || landed.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" || TypeChangeLastHitType(actor) is not { } attackType)
        { Fail("no-landed-move"); return; }
        if (TypeChangeLastHitBy(actor) is { } source && catalog.Moves[landed].Effect is "EFFECT_RAZOR_WIND" or "EFFECT_SKULL_BASH" or "EFFECT_SKY_ATTACK" or "EFFECT_SOLAR_BEAM" or "EFFECT_SEMI_INVULNERABLE" or "EFFECT_BIDE" && TypeChangeMultipleTurns(source))
        { Fail("source-still-multiple-turns"); return; }
        var types = WildBattleEffective.Types(catalog, body);
        // Explicitly approved Conversion2-only policy. Never restore the original row
        // rejection loop/fallback or use this sampler for Conversion or move callers.
        var candidates = WildBattleSourcePartyEffects.Conversion2Candidates(attackType, types.Type1, types.Type2);
        if (candidates.IsEmpty) { Fail("no-resistant-type"); return; }
        int choice = WildBattleSourcePartyEffects.Conversion2ChoiceIndex(Draw($"{purpose}.conversion2-candidate", candidates.Length), candidates.Length);
        SetBattleType(actor, move, candidates[choice]);
    }
}
