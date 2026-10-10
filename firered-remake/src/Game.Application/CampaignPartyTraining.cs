using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed record CampaignVoluntarySwitch(PartyPokemon Outgoing,string WithdrawalTextSymbol,string SendOutTextSymbol);
public sealed record CampaignWildRewardReceiver(int PartyIndex,WildVictoryRewardResult Result);

public sealed partial class CampaignSession
{
    private Decision<CampaignState> DecideVoluntaryWildSwitch(CampaignState state,CampaignCommand command)
    {
        var b=state.WildBattle!;int index=command.PartyIndex;
        if(index<0||index>=state.Party.Length||index==b.ActivePartyIndex||state.Party[index].IsEgg||state.Party[index].Pokemon.CurrentHp==0)
            return new(CommandStatus.Rejected,state,"replacement_member_invalid");
        if(b.ParticipatingMembers is null||b.OpponentHpAtLastSwitch is null)return new(CommandStatus.Fault,state,"wild_participation_missing");
        var incoming=state.Party[index];var outgoing=state.Party[b.ActivePartyIndex];
        bool reserves=state.Party.Where((p,i)=>i!=index).Any(p=>!p.IsEgg&&p.Pokemon.CurrentHp>0);
        var turn=WildBattleRules.IsSourceWild(b.State)?WildBattleRules.SwitchSourceWildPlayer(BattlePokemon,b.State,index):WildBattleRules.SwitchPlayer(BattlePokemon,b.State,OwnedBattleMember(state,b,incoming),reserves);
        if(!turn.Succeeded)return new(CommandStatus.Rejected,state,turn.Fault);
        if(_content.Route22Enabled&&!WildBattleRules.IsSourceWild(turn.State)&&turn.Events.OfType<WildBattlePlayerSwitchedEvent>().SingleOrDefault() is {} switched)
        {
            b=RememberKnockedOff(b,outgoing.InstanceId,switched.Previous);
            var preserved=outgoing with{Pokemon=WildBattleTransformationRules.PersistentPokemon(switched.Previous),Status=WildBattleTransformationRules.PersistentStatus(switched.Previous),HeldItem=OwnedBattleHeldItem(switched.Previous)};
            var fainted=b.FaintedMembers??ImmutableSortedSet<long>.Empty;
            if(preserved.Pokemon.CurrentHp==0&&!fainted.Contains(preserved.InstanceId))
            {preserved=preserved with{Pokemon=preserved.Pokemon with{Friendship=Math.Max(0,preserved.Pokemon.Friendship-1)}};fainted=fainted.Add(preserved.InstanceId);}
            state=state with{Party=state.Party.SetItem(b.ActivePartyIndex,preserved)};
            b=b with{FaintedMembers=fainted};
        }
        int hp=b.State.Opponent.Pokemon.CurrentHp,maximum=b.State.Opponent.Pokemon.Stats.MaxHp,snapshot=b.OpponentHpAtLastSwitch!.Value;
        int loss=hp>=snapshot?0:(snapshot-hp)*100/snapshot;
        string withdrawal=hp>=snapshot?"sText_PkmnThatsEnough":loss<30?"sText_PkmnComeBack":loss<70?"sText_PkmnOkComeBack":"sText_PkmnGoodComeBack";
        int percentage=Math.Max(1,hp*100/maximum);
        string send=hp==0||percentage>=70?"sText_GoPkmn2":percentage>=40?"sText_DoItPkmn":percentage>=10?"sText_GoForItPkmn":"sText_YourFoesWeakGetEmPkmn";
        b=RememberKnockedOff(b,incoming.InstanceId,turn.State!.Player);
        var next=b with{State=turn.State!,ActivePartyIndex=index,OwnedBefore=incoming,ParticipatingMembers=b.ParticipatingMembers!.Add(incoming.InstanceId),OpponentHpAtLastSwitch=hp,
            LastSwitch=new(outgoing,withdrawal,send),Events=turn.Events,EventIndex=0,Phase=CampaignBattlePhase.Events,RandomTrace=turn.Trace};
        return new(CommandStatus.Accepted,PrepareWildPresentation(PenalizeWildFaint(state with{WildBattle=next,
            Party=state.Party.SetItem(index,incoming with{Pokemon=WildBattleTransformationRules.PersistentPokemon(turn.State!.Player),Status=WildBattleTransformationRules.PersistentStatus(turn.State.Player),HeldItem=OwnedBattleHeldItem(turn.State.Player)}),World=state.World with{Rng=turn.State!.Rng}})));
    }

    private CampaignState SettleWildTrainingVictory(CampaignState state)
    {
        var b=state.WildBattle!;var participants=b.ParticipatingMembers??throw new InvalidDataException("wild_participation_missing");
        var indices=Enumerable.Range(0,state.Party.Length).Where(i=>participants.Contains(state.Party[i].InstanceId)&&!state.Party[i].IsEgg&&state.Party[i].Pokemon.CurrentHp>0).ToArray();
        var receivers=ImmutableArray.CreateBuilder<CampaignWildRewardReceiver>();
        // Preflight every receiver before publishing the first reward. Each receiver's state is
        // installed only when its source-ordered presentation/learning segment starts.
        foreach(int i in indices)
            receivers.Add(new(i,WildVictoryRewards.Apply(BattlePokemon,state.Party[i],b.State.Opponent.Pokemon,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length,forestRules:_content.ForestEnabled,route22Rules:_content.Route22Enabled,route3Rules:_content.Route3Enabled,mtMoonRules:_content.MtMoonEnabled,ceruleanRules:_content.CeruleanEnabled,billRules:_content.BillEnabled,vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,pokemonTowerRules:_content.PokemonTowerEnabled,snorlaxRules:_content.SnorlaxEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled,defeatedBattleView:b.State.Opponent)));
        var history=new CampaignWildHistory(b.Id,WildBattleTransformationRules.PersistentPokemon(b.State.Opponent).SourceSpeciesId,b.State.Opponent.Pokemon.Level,b.State.Outcome,receivers.Sum(r=>r.Result.ExperienceGained)){ScriptedSnorlaxRoute=b.ScriptedSnorlaxRoute,ScriptedMarowak=b.ScriptedMarowak};
        var next=state with{Exploration=state.Exploration! with{Battles=state.Exploration.Battles.Add(history)},WildBattle=b with{RewardReceivers=receivers.ToImmutable(),RewardReceiverIndex=0,Phase=CampaignBattlePhase.Results}};
        return StartWildRewardReceiver(next);
    }

    private CampaignState StartWildRewardReceiver(CampaignState state)
    {
        var b=state.WildBattle!;var receivers=b.RewardReceivers!.Value;
        while(b.RewardReceiverIndex<receivers.Length)
        {
            var receiver=receivers[b.RewardReceiverIndex];var reward=receiver.Result;
            var pages=ImmutableArray.CreateBuilder<CampaignWildPage>();
            foreach(var e in reward.Events)
            {
                if(e is WildRewardExperienceEvent xp)pages.Add(new($"{reward.Pokemon.Nickname} gained\n{xp.Gained} EXP. Points!",e));
                if(e is WildRewardLevelEvent level){pages.Add(new($"{reward.Pokemon.Nickname} grew to\nlevel {level.After}!",e));pages.Add(new("",e));}
                if(e is WildRewardMoveLearnedEvent learned)pages.Add(new($"{reward.Pokemon.Nickname} learned\n{BattleContent.MoveNames[learned.Move.Value]}!",e));
            }
            state=state with{Party=state.Party.SetItem(receiver.PartyIndex,reward.Pokemon)};
            b=b with{Reward=reward,Pages=pages.ToImmutable(),PageIndex=0,PendingMoves=reward.PendingMoves};
            if(!b.Pages.IsEmpty||!b.PendingMoves.IsEmpty)return state with{WildBattle=b};
            b=b with{RewardReceiverIndex=b.RewardReceiverIndex+1};
        }
        return _content.VermilionEnabled?FinishVermilionWildRewards(state with{WildBattle=b}):_content.ForestEnabled?BeginWildEvolution(state with{WildBattle=b}):ReturnFromWildBattle(state with{WildBattle=b});
    }

    private CampaignState CompleteWildRewardReceiver(CampaignState state)
    {
        var b=state.WildBattle!;
        if(b.PostBattleItemsSettled)return BeginWildEvolution(state);
        if(b.RewardReceivers is null)return ReturnFromWildBattle(state);
        if(!b.PendingMoves.IsEmpty)throw new InvalidDataException("wild_learning_pending");
        return StartWildRewardReceiver(state with{WildBattle=b with{RewardReceiverIndex=b.RewardReceiverIndex+1,DisplayPlayer=state.Party[b.ActivePartyIndex].Pokemon}});
    }
}
