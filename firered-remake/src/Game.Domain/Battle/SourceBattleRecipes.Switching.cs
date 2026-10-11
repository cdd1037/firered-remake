using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasSwitchRecipe(string effect) => effect is "EFFECT_ROAR" or "EFFECT_BATON_PASS";
    internal abstract bool ForceSwitchTrainer { get; }
    internal abstract bool ForceSwitchRootedCheck { get; }
    internal abstract bool ForceSwitchHasReserve(BattlerSlot target);
    internal abstract bool ForceSwitchAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose);
    internal abstract void ForceSwitch(PokemonCatalog catalog, BattlerSlot target, MoveId move);
    internal abstract void ForceSwitchEscape();
    internal abstract ImmutableArray<int> BatonPassReserves(BattlerSlot actor);
    internal abstract void PauseBatonPass(BattlerSlot actor, ImmutableArray<int> legal);
    internal virtual void BatonPassFailed() { }

    internal void ExecuteSwitch(PokemonCatalog catalog, BattlerSlot actor, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        var target = move.Effect == "EFFECT_BATON_PASS" ? actor : Invocation?.Target ?? actor.Opposite;
        SpendEarlyPartyPp(actor, target, slot, move);
        void Fail(string reason) => Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, reason));
        if (move.Effect == "EFFECT_BATON_PASS")
        {
            var legal = BatonPassReserves(actor);
            if (legal.IsEmpty) { BatonPassFailed(); Fail("no-eligible-reserve"); }
            else PauseBatonPass(actor, legal);
            return;
        }
        if (move.Effect != "EFFECT_ROAR") throw new SourceRecipeUnsupported("source_switch_recipe");
        var own = Get(actor); var other = Get(target);
        if (WildBattleEffective.Ability(other) == "ABILITY_SUCTION_CUPS")
        {
            RecordAbility(target, "ABILITY_SUCTION_CUPS");
            Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, "ABILITY_SUCTION_CUPS"));
            return;
        }
        if (ForceSwitchRootedCheck && other.Rooted) { Fail("rooted"); return; }
        if (!ForceSwitchAccuracy(actor, target, move, purpose)) return;
        if (ForceSwitchTrainer && !ForceSwitchHasReserve(target)) { Fail("no-force-out-reserve"); return; }
        if (own.Pokemon.Level < other.Pokemon.Level)
        {
            int roll = Draw($"{purpose}.force-out-level", 256) & 255;
            if (((roll * (own.Pokemon.Level + other.Pokemon.Level) >> 8) + 1) <= other.Pokemon.Level / 4)
            { Fail("force-out-level"); return; }
        }
        Add(n => new SourceForcedOutEvent(n, Turn, actor, target, move.Id, ForceSwitchTrainer));
        if (ForceSwitchTrainer) ForceSwitch(catalog, target, move.Id);
        else ForceSwitchEscape();
    }
}
