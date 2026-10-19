using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using System.Collections.Immutable;
namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool SourceWildPartyChoicePending=>State.WildBattle?.Frame?.PendingSwitches.Any(p=>p.Slot.Value==0&&p.Kind==WildBattleCalledSwitchKind.BatonPass)==true;

    private CampaignState ProjectSourceWildOwnedParty(CampaignState state)
    {
        var b=state.WildBattle??throw new InvalidDataException("source_wild_owned_context");
        var frame=b.Frame??throw new InvalidDataException("canonical_wild_frame_required");var actual=frame.Party(BattleSide.Player);
        if(actual.Members.Length!=state.Party.Length)throw new InvalidDataException("source_wild_owned_count");
        var owned=state.Party.ToBuilder();var members=actual.Members;
        for(int i=0;i<owned.Count;i++)
        {
            var member=members[i];
            if(member.IsEgg!=owned[i].IsEgg||!SourceOwnedIdentity(member.Pokemon,owned[i].Pokemon))
                throw new InvalidDataException("source_wild_owned_identity");
            var pokemon=member.Pokemon;
            owned[i]=owned[i] with{Pokemon=pokemon,Status=member.Status,HeldItem=WildBattleSourcePartyEffects.PersistentHeldItem(member)};
        }
        int index=frame.Get(new(0)).Member!.Value.Index;
        if(index!=b.ActivePartyIndex)b=b with{ActivePartyIndex=index,OwnedBefore=owned[index],
            OpponentHpAtLastSwitch=frame.Get(new(1)).Body!.Pokemon.CurrentHp};
        return state with{Party=owned.ToImmutable(),WildBattle=b with{Frame=frame}};
    }

    private Decision<CampaignState> DecideSourceWildPartyChoice(CampaignState state,CampaignCommand command)
    {
        var b=state.WildBattle!;var frame=b.Frame??throw new InvalidDataException("canonical_wild_frame_required");
        if(command.Kind!="battle_switch")return new(CommandStatus.Rejected,state,"source_wild_party_choice_required");
        var result=SourceFourActionTurns.ChooseActionSwitch(CanonicalWildPokemon,frame,WildContinuation(frame),new(0),new(BattleSide.Player,command.PartyIndex));
        return CommitCanonicalWildResult(state with{WildBattle=b with{Replacement=null,LastSwitch=null}},result);
    }
}
