using Game.Domain;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using System.Collections.Immutable;
namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool SourceWildPartyChoicePending=>State.WildBattle?.State is {} battle&&WildBattleRules.IsSourceWild(battle)&&
        battle.CalledParty?.PendingSwitch is{Side:WildBattleSide.Player,Kind:WildBattleCalledSwitchKind.BatonPass};

    private CampaignState ProjectSourceWildOwnedParty(CampaignState state)
    {
        var b=state.WildBattle??throw new InvalidDataException("source_wild_owned_context");var battle=b.State;
        if(!WildBattleRules.IsSourceWild(battle))return state;
        var parties=battle.CalledParty??throw new InvalidDataException("source_wild_actual_party");
        if(parties.Player.Length!=state.Party.Length)throw new InvalidDataException("source_wild_owned_count");
        var owned=state.Party.ToBuilder();var fainted=b.FaintedMembers??ImmutableSortedSet<long>.Empty;
        for(int i=0;i<owned.Count;i++)
        {
            var member=parties.Player[i];
            if(member.IsEgg||owned[i].IsEgg||!SourceOwnedIdentity(member.Pokemon,owned[i].Pokemon))
                throw new InvalidDataException("source_wild_owned_identity");
            var pokemon=member.Pokemon;
            if(pokemon.CurrentHp==0&&!fainted.Contains(owned[i].InstanceId))
            {
                pokemon=pokemon with{Friendship=Math.Max(0,pokemon.Friendship-1)};
                fainted=fainted.Add(owned[i].InstanceId);
                member=member with{Pokemon=pokemon};parties=parties with{Player=parties.Player.SetItem(i,member)};
                if(i==parties.PlayerActiveIndex)battle=battle with{Player=WildBattleTransformationRules.ApplyPersistentPokemon(battle.Player,pokemon)};
            }
            owned[i]=owned[i] with{Pokemon=pokemon,Status=member.Status,HeldItem=WildBattleSourcePartyEffects.PersistentHeldItem(member)};
        }
        int index=parties.PlayerActiveIndex;
        if(index!=b.ActivePartyIndex)b=b with{ActivePartyIndex=index,OwnedBefore=owned[index],LastSwitch=null,
            ParticipatingMembers=(b.ParticipatingMembers??ImmutableSortedSet<long>.Empty).Add(owned[index].InstanceId),OpponentHpAtLastSwitch=battle.Opponent.Pokemon.CurrentHp};
        return state with{Party=owned.ToImmutable(),WildBattle=b with{State=battle with{CalledParty=parties},FaintedMembers=fainted}};
    }

    private Decision<CampaignState> DecideSourceWildPartyChoice(CampaignState state,CampaignCommand command)
    {
        var b=state.WildBattle!;
        if(command.Kind!="battle_switch")return new(CommandStatus.Rejected,state,"source_wild_party_choice_required");
        var result=WildBattleRules.ChooseSourceWildParty(BattlePokemon,b.State,command.PartyIndex);
        if(!result.Succeeded)return new(CommandStatus.Rejected,state,result.Fault);
        return new(CommandStatus.Accepted,PrepareWildPresentation(ProjectSourceWildOwnedParty(state with
        {
            World=state.World with{Rng=result.State!.Rng},
            WildBattle=b with{State=result.State,Events=result.Events,EventIndex=0,Phase=CampaignBattlePhase.Events,RandomTrace=result.Trace,Replacement=null}
        })));
    }
}
