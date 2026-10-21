using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{

    // BattleScript_EffectSpikes mutates the opposing side before attackstring/ppreduce.
    // Failure at three layers still spends base PP, suppressing every Pressure holder.
    internal void ExecuteSpikes(BattlerSlot actor, int slot, PokemonMoveDefinition move)
    {
        var target = Invocation?.Target ?? throw new SourceRecipeUnsupported("source_spikes_invocation");
        var side = actor.Opposite.Side;
        int before = SpikesLayers(side);
        if (before == 3) SpikesSuppressPressure();
        else
        {
            SetSpikesLayers(side, before + 1);
            Add(n => new SourceCalledSpikesChangedEvent(n, Turn, side, before, before + 1));
        }
        SpendEarlyPartyPp(actor, target, slot, move);
        if (before == 3) SourceCalledProtectionFailure(actor, target, move.Id, "spikes-at-maximum");
        else SpikesResultMessage(actor);
    }
}

/// <summary>Cmd_switchineffects and the passive Spikes script shared by both battle owners.
/// The owner calls this once per actual entry, before Truant, ON_SWITCHIN and item effects.
/// This synchronous command consumes the source side latch without serializing a half-script.</summary>
internal static class SourceSpikesRules
{
    internal static int Damage(int maxHp, int layers) => layers is >= 1 and <= 3 && maxHp > 0
        ? Math.Max(1, maxHp / ((5 - layers) * 2)) : throw new ArgumentOutOfRangeException(nameof(layers));

    internal static void Enter(PokemonCatalog catalog, BattlerSlot slot, int layers, int turn,
        Func<BattlerSlot, WildBattleCombatant> get, Action<BattlerSlot, WildBattleCombatant> set,
        Action<Func<long, SourceBattleEvent>> add, Action<BattlerSlot> faint, Action? resultMessage = null)
    {
        var mon = get(slot);
        if (layers == 0 || mon.Pokemon.CurrentHp == 0 || WildBattleEffective.HasType(catalog, mon, "TYPE_FLYING") ||
            WildBattleEffective.Ability(mon) == "ABILITY_LEVITATE") return;
        // FireRed has no Rooted override, type multiplier, accuracy or RNG here. Substitute,
        // Endure and Focus Band never intercept this passive HP update.
        int before = mon.Pokemon.CurrentHp, damage = Damage(mon.Pokemon.Stats.MaxHp, layers), after = Math.Max(0, before - damage);
        set(slot, mon with { Pokemon = mon.Pokemon with { CurrentHp = after } });
        add(n => new SourceHpChangedEvent(n, turn, slot.Opposite, slot, new MoveId("pret:MOVE_SPIKES"), before, after, damage, false, 100));
        resultMessage?.Invoke();
        if (after == 0) faint(slot);
    }
}
