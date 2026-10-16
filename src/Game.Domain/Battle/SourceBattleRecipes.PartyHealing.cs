using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal virtual void PartyHealingSynchronize() { }
    internal abstract ImmutableArray<WildBattleCalledPartyMember> HealingParty(BattleSide side);
    internal abstract BattlerSlot? HealingActiveSlot(BattlerSlot actor, int partyIndex);
    internal abstract void WriteHealingPartyMember(BattleSide side, int partyIndex, WildBattleCalledPartyMember member);

    // Cmd_healpartystatus uses live abilities only for the actor and source-present partner.
    // Every other real party member uses its permanent ability, including fainted reserves.
    // Keep the existing party-change/status event order at the singles compatibility boundary.
    internal void ExecutePartyHealing(BattlerSlot actor, int slot, PokemonMoveDefinition move)
    {
        if (move.Effect != "EFFECT_HEAL_BELL" || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_party_healing_move_definition");
        SpendEarlyPartyPp(actor, actor, slot, move);
        PartyHealingSynchronize();
        bool bell = move.Id.Value == "pret:MOVE_HEAL_BELL";
        string reason = bell ? "heal-bell" : "aromatherapy";
        var party = HealingParty(actor.Side);
        for (int index = 0; index < party.Length; index++)
        {
            var before = party[index];
            var active = HealingActiveSlot(actor, index);
            string ability = active is { } target ? WildBattleEffective.Ability(Get(target)) : before.Pokemon.AbilityId;
            if (bell && (before.IsEgg || ability == "ABILITY_SOUNDPROOF"))
            {
                if (active is { } blocked && ability == "ABILITY_SOUNDPROOF")
                {
                    RecordAbility(blocked, ability);
                    Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, blocked, move.Id, ability));
                }
                continue;
            }
            var after = before with { Status = 0 };
            WriteHealingPartyMember(actor.Side, index, after);
            if (!WildBattleSourcePartyEffects.SameMember(before, after))
                Add(n => new SourceCalledPartyChangedEvent(n, Turn, actor.Side, index, reason, before, after));
            if (active is { } cured)
            {
                ChangeStatus(cured, 0, reason);
                var body = Get(cured);
                Set(cured, body with { Nightmare = false, SourcePartyStatusOverride = null,
                    Transform = body.Transform is { } form ? form with { PartyStatusOverride = null } : null });
            }
        }
    }
}
