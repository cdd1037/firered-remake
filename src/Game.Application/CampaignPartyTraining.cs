using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed record CampaignVoluntarySwitch(PartyPokemon Outgoing,string WithdrawalTextSymbol,string SendOutTextSymbol);

public sealed partial class CampaignSession
{
    private Decision<CampaignState> DecideVoluntaryWildSwitch(CampaignState state,CampaignCommand command)
    {
        var b=state.WildBattle!;int index=command.PartyIndex;
        if(index<0||index>=state.Party.Length||index==b.ActivePartyIndex||state.Party[index].IsEgg||state.Party[index].Pokemon.CurrentHp==0)
            return new(CommandStatus.Rejected,state,"replacement_member_invalid");
        var frame=b.Frame??throw new InvalidDataException("canonical_wild_frame_required");
        int hp=frame.Get(new(1)).Body!.Pokemon.CurrentHp,maximum=frame.Get(new(1)).Body!.Pokemon.Stats.MaxHp,snapshot=b.OpponentHpAtLastSwitch??hp;
        int loss=hp>=snapshot?0:(snapshot-hp)*100/snapshot;
        string withdrawal=hp>=snapshot?"sText_PkmnThatsEnough":loss<30?"sText_PkmnComeBack":loss<70?"sText_PkmnOkComeBack":"sText_PkmnGoodComeBack";
        int percentage=Math.Max(1,hp*100/maximum);
        string send=hp==0||percentage>=70?"sText_GoPkmn2":percentage>=40?"sText_DoItPkmn":percentage>=10?"sText_GoForItPkmn":"sText_YourFoesWeakGetEmPkmn";
        var result=SourceWildBattlePolicy.BeginAction(CanonicalWildPokemon,frame,frame.Revision,
            new(frame.Get(new(0)).Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,index)));
        return CommitCanonicalWildResult(state with{WildBattle=b with{LastSwitch=new(state.Party[b.ActivePartyIndex],withdrawal,send)}},result,resetTrace:true);
    }

    private CampaignState CompleteWildRewardReceiver(CampaignState state)
    {
        var b=state.WildBattle!;
        if(!b.PendingMoves.IsEmpty)throw new InvalidDataException("wild_learning_pending");
        state=state with{WildBattle=b with{Pages=[],PageIndex=0,DisplayPlayer=state.Party[b.ActivePartyIndex].Pokemon}};
        if(state.Exploration!.Battles.Any(h=>h.Id==b.Id))
            return b.Frame!.Outcome==WildBattleOutcome.Won?FinishVermilionWildRewards(state):ReturnFromWildBattle(state);
        return PrepareWildPresentation(state);
    }
}
