using System.Collections.Immutable;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override ImmutableArray<WildBattleCalledPartyMember> HealingParty(BattleSide side) => Frame.Party(side).Members;
    internal override BattlerSlot? HealingActiveSlot(BattlerSlot actor, int partyIndex)
    {
        if (Frame.Get(actor).Member?.Index == partyIndex) return actor;
        if (SourceDoubleBattle && !Frame.IsSourceAbsent(actor.Partner) &&
            Frame.Get(actor.Partner).Member?.Index == partyIndex) return actor.Partner;
        return null;
    }
    internal override void WriteHealingPartyMember(BattleSide side, int partyIndex, WildBattleCalledPartyMember member)
    {
        var party = Frame.Party(side);
        Frame = Frame with { Parties = Frame.Parties.SetItem((int)side, party with { Members = party.Members.SetItem(partyIndex, member) }) };
        // Keep the body/party projection valid while the shared recipe publishes the retained
        // party-before-status events. Present bodies immediately clear this override on cure;
        // source-absent bodies retain it because their live battle status is untouched.
        var cell = Frame.Slots.FirstOrDefault(c => c.Member == new PartyMemberKey(side, partyIndex));
        if (cell?.Body is { } body &&
            WildBattleTransformationRules.PersistentStatus(body) != member.Status)
            Set(cell.Slot, body with { SourcePartyStatusOverride = member.Status });
    }
}
