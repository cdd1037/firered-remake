using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>AdjustFriendship's two battle-owned events. Entry runs before battler copies;
/// faint runs only at the actual source faint command, never while projecting a result.</summary>
internal static class SourceBattleFriendshipRules
{
    internal static (ImmutableArray<WildBattleCombatant> Party, string? Fault) TrainerEntry(
        TrainerDefinition trainer, ImmutableArray<WildBattleCombatant> party, SourceFourRewardState? rewards)
    {
        if (trainer.TrainerClass is not ("TRAINER_CLASS_LEADER" or "TRAINER_CLASS_ELITE_FOUR" or "TRAINER_CLASS_CHAMPION"))
            return (party, null);
        // These are the same permanent ball/met facts used by the subsequent EXP owner.
        // A missing context cannot silently pretend there were no location/ball bonuses.
        if (rewards?.Context is not { BattleKind: SourceRewardBattleKind.OrdinaryTrainer } context ||
            string.IsNullOrWhiteSpace(context.CurrentRegion) || rewards.Metadata.IsDefault || rewards.Metadata.Length != party.Length ||
            rewards.Metadata.Where((m, i) => m is null || m.Key != new PartyMemberKey(BattleSide.Player, i) ||
                string.IsNullOrWhiteSpace(m.Ball) || string.IsNullOrWhiteSpace(m.MetLocation)).Any())
            return (party, "source_trainer_entry_friendship_context");
        return (party.Select((mon, i) => {
            if (mon.IsEgg) return mon;
            var facts = rewards.Metadata[i]; int before = mon.Pokemon.Friendship;
            int after = SourceItemFriendshipRules.Apply(before, before < 100 ? 3 : before < 200 ? 2 : 1,
                mon.HeldItem, new(facts.Ball, facts.MetLocation, context.CurrentRegion));
            return mon with { Pokemon = mon.Pokemon with { Friendship = after } };
        }).ToImmutableArray(), null);
    }

    internal static WildBattleCombatant Faint(SourceBattleFrame frame, BattlerSlot slot)
    {
        var body = frame.Get(slot).Body!;
        if (slot.Side != BattleSide.Player) return body;
        if (body.Pokemon.CurrentHp != 0) throw new ArgumentException("source_friendship_faint_requires_zero_hp");
        var original = WildBattleTransformationRules.PersistentPokemon(body);
        if (frame.Party(BattleSide.Player).Members[frame.Get(slot).Member!.Value.Index].IsEgg) return body;
        // The source compares retained opponent battle bodies, not the damage dealer or
        // only currently living opponents. The higher opposite flank owns doubles' threshold.
        int Level(int position) => frame.Get(new(position)).Body?.Pokemon.Level ??
            throw new SourceRecipeUnsupported("source_friendship_opposing_battle_body_required");
        int opposingLevel = Level(1);
        if (frame.Layout == SourceBattleLayout.PresetDoubles)
            opposingLevel = Math.Max(opposingLevel, Level(3));
        int delta = opposingLevel - body.Pokemon.Level >= 30 ? original.Friendship < 200 ? -5 : -10 : -1;
        int friendship = SourceItemFriendshipRules.Apply(original.Friendship, delta, body.HeldItem, null);
        // This body is already dead. Revive/replacement reloads its permanent owner;
        // no live attacker's cached friendship or transformed donor is changed here.
        return WildBattleTransformationRules.ApplyPersistentPokemon(body, original with { Friendship = friendship });
    }
}
