using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.FirstBattle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.World;
namespace Game.Application;

public enum CampaignBattlePhase { Events, ChooseMove, Results }
public sealed record CampaignBattlePage(string Kind,string Text,LabBattleRewardEvent? Reward=null);
public sealed record CampaignBattleHistory(string BattleId,string TrainerId,FirstBattleOutcome Outcome,int ExperienceGained,int MoneyGained,int? HpFraction,bool QuestLogRequested);
public sealed record CampaignLabBattle(string Id,TrainerId Trainer,PartyPokemon OwnedBefore,FirstBattleState State,
    ImmutableArray<FirstBattleEvent> Events,int EventIndex,PokemonInstance DisplayPlayer,PokemonInstance DisplayOpponent,
    CampaignBattlePhase Phase,LabBattleRewardResult? Reward,ImmutableArray<CampaignBattlePage> Pages,int PageIndex,
    ImmutableArray<RandomTrace> RandomTrace,FirstBattleAiDecision? AiDecision);

public sealed partial class CampaignSession
{
    private CampaignBattleContent BattleContent=>_content.BattleContent??throw new InvalidDataException("trainerbattle_earlyrival_not_implemented");
    private PokemonCatalog BattlePokemon=>_binding.Pokemon??throw new InvalidDataException("pokemon_content_missing");
    private (TrainerPartyCreationResult Created,FirstBattleResult Started) PrepareLabBattle(CampaignState state,TrainerId trainer)
    {
        if(state.Party.Length!=1||state.NextBattleId is <1 or long.MaxValue||state.Battle is not null)throw new InvalidDataException("lab_battle_party_or_id_invalid");
        var owned=state.Party[0];
        if(owned.Pokemon.OriginalTrainerId!=state.TrainerId||owned.OriginalTrainerName!=state.PlayerName||owned.OriginalTrainerGender!=state.PlayerGender)throw new InvalidDataException("lab_battle_local_owner_required");
        var definition=BattleContent.Trainers.Battles[trainer];
        _=_content.TextPages(definition.DefeatText);_=_content.TextPages(definition.VictoryText);
        var created=TrainerPartyFactory.Create(BattleContent.Trainers,BattlePokemon,trainer,state.World.Rng);
        if(created.Party.Length!=1)throw new InvalidDataException("lab_battle_single_member_required");
        var start=FirstBattleRules.Start(BattlePokemon,new(owned.Pokemon,HeldItem:owned.HeldItem,Status:owned.Status),new(created.Party[0].Pokemon,HeldItem:created.Party[0].HeldItem),created.Rng);
        if(!start.Succeeded)throw new InvalidDataException(start.Fault);
        return (created,start);
    }
    // Preflight before the source approach locks controls; do not publish these tentative pure draws.
    private void PreflightLabBattle(CampaignState state)
    {
        // The ordinal is a diagnostic UInt64, separate from the 32-bit LCG. Reject artificial
        // near-exhaustion saves before the source approach locks input; reserve a full LCG period.
        if(long.MaxValue-Revision<(1L<<32)||long.MaxValue-state.Script.Revision<(1L<<32)||long.MaxValue-state.Script.EffectSequence<(1L<<32))throw new InvalidDataException("lab_battle_counter_headroom_required");
        if(ulong.MaxValue-state.World.Rng.Cursor<(1UL<<32))throw new InvalidDataException("lab_battle_rng_headroom_required");
        if(!state.Script.Variables.TryGetValue("VAR_STARTER_MON",out var starter))throw new InvalidDataException("starter_identity_missing");
        var choice=_content.PokemonContent!.StarterChoices.Single(c=>c.Index==starter);
        var trainer=BattleContent.Trainers.Trainers.Values.Single(t=>t.Party.Single().SourceSpeciesId==choice.RivalSourceSpeciesId).Id;
        _=PrepareLabBattle(state,trainer);
    }
    private CampaignState BeginLabBattle(CampaignState state,StoryEffect effect)
    {
        state=ResetFieldPoisonCounterForBattle(state);
        var a=effect.Args;var trainer=new TrainerId("pret:"+a[0]);
        var definition=BattleContent.Trainers.Battles[trainer];
        if(a[1]!="RIVAL_BATTLE_TUTORIAL"||a[2]!=definition.DefeatText||a[3]!=definition.VictoryText||effect.Source.Path!=definition.Source.Path||effect.Source.Line!=definition.Source.StartLine)throw new InvalidDataException("source_trainer_battle_definition_mismatch");
        if(state.Script.Wait?.Kind!=StoryWaitKind.Battle)throw new InvalidDataException("battle_script_wait_missing");
        var prepared=PrepareLabBattle(state,trainer);var started=prepared.Started.State!;
        var id="lab-battle:"+state.NextBattleId;
        if(state.SettledBattles.Contains(id))throw new InvalidDataException("battle_id_already_settled");
        var battle=new CampaignLabBattle(id,trainer,state.Party[0],started,prepared.Started.Events,0,started.Player.Pokemon,started.Opponent.Pokemon,CampaignBattlePhase.Events,null,[],0,[..prepared.Created.Trace,..prepared.Started.Trace],null);
        return PrepareBattlePresentation(state with{Battle=battle,NextBattleId=checked(state.NextBattleId+1),World=state.World with{Rng=started.Rng},SeenSpecies=state.SeenSpecies.Add(started.Opponent.Pokemon.SourceSpeciesId),Dialogue=null,Motions=[],PresentationTicks=0});
    }
    private Decision<CampaignState> DecideBattle(CampaignState state,CampaignCommand command)
    {
        var b=state.Battle!;
        if(state.Script.Wait?.Kind!=StoryWaitKind.Battle)return new(CommandStatus.Fault,state,"battle_script_context_invalid");
        if(command.Kind=="ack")
        {
            if(b.Phase==CampaignBattlePhase.Events)return new(CommandStatus.Accepted,PrepareBattlePresentation(state with{Battle=b with{EventIndex=b.EventIndex+1}}));
            if(b.Phase==CampaignBattlePhase.Results)
            {
                if(b.PageIndex+1<b.Pages.Length)return new(CommandStatus.Accepted,ProjectBattleResult(state,b.PageIndex+1));
                return new(CommandStatus.Accepted,ReturnFromLabBattle(state));
            }
            return new(CommandStatus.Rejected,state,"battle_move_required");
        }
        if(command.Kind=="battle_menu")
        {
            if(b.Phase!=CampaignBattlePhase.ChooseMove)return new(CommandStatus.Rejected,state,"battle_presentation_pending");
            if(command.Text=="party")
            {
                if(b.State.TutorialFlags.HasFlag(FirstBattleTutorialFlags.PartyMenu))return new(CommandStatus.Accepted,state);
                var flags=b.State.TutorialFlags|FirstBattleTutorialFlags.PartyMenu;var sequence=checked(b.State.EventSequence+1);
                var updated=b.State with{TutorialFlags=flags,EventSequence=sequence,Revision=checked(b.State.Revision+1)};
                var tutorial=new FirstBattleTutorialEvent(sequence,updated.Turn,FirstBattleTutorialFlags.PartyMenu,flags);
                return new(CommandStatus.Accepted,PrepareBattlePresentation(state with{Battle=b with{State=updated,Events=[tutorial],EventIndex=0,Phase=CampaignBattlePhase.Events}}));
            }
            if(command.Text=="bag"&&_content.OpeningItems is not null)return new(CommandStatus.Accepted,state);
            return new(CommandStatus.Rejected,state,command.Text=="run"?"trainer_battle_cannot_run":"battle_bag_not_implemented");
        }
        bool usePotion=command.Kind=="battle_potion";
        if(command.Kind!="battle_move"&&!usePotion)return new(CommandStatus.Rejected,state,"battle_active");
        if(b.Phase!=CampaignBattlePhase.ChooseMove)return new(CommandStatus.Rejected,state,"battle_presentation_pending");
        int slot=command.MoveSlot;PotionUseResult? potion=null;
        if(usePotion)
        {
            if(_content.OpeningItems is null||state.Inventory is null)return new(CommandStatus.Rejected,state,"battle_items_not_enabled");
            if(command.PartyIndex!=0)return new(CommandStatus.Rejected,state,"battle_item_target_invalid");
            if(!b.State.TutorialFlags.HasFlag(FirstBattleTutorialFlags.PartyMenu))return new(CommandStatus.Rejected,state,"battle_item_target_menu_required");
            potion=UseCampaignPotion(state.Inventory,b.State.Player.Pokemon,PotionUseContext.Battle);
            if(!potion.Succeeded)return new(CommandStatus.Rejected,state,potion.Error);
        }
        else if(FirstBattleRules.RequiresStruggle(b.State.Player))
        {if(slot!=FirstBattleRules.StruggleSlot)return new(CommandStatus.Rejected,state,"player_struggle_required");}
        else if(slot<0||slot>=b.State.Player.Pokemon.Moves.Length||b.State.Player.Pokemon.Moves[slot].CurrentPp<=0)return new(CommandStatus.Rejected,state,"battle_move_slot_or_pp_invalid");
        FirstBattleAiDecision? ai=null;int opponentSlot=FirstBattleRules.StruggleSlot;var rng=b.State.Rng;
        if(!FirstBattleRules.RequiresStruggle(b.State.Opponent))
        {
            var p=b.State.Player;var o=b.State.Opponent;
            ai=FirstBattleAi.Choose(BattlePokemon,new(o.Pokemon,p.Pokemon,o.AttackStage,o.DefenseStage,p.AttackStage,p.DefenseStage,BattleContent.Trainers.Trainers[b.Trainer].AiFlags),rng);
            if(ai.Status!=TransitionStatus.Accepted)return new(CommandStatus.Fault,state,ai.Diagnostic?.Code);
            opponentSlot=ai.MoveSlot!.Value;rng=ai.Rng;
        }
        var turn=usePotion?FirstBattleRules.ResolvePotionTurn(BattlePokemon,b.State with{Rng=rng},potion!.Pokemon,opponentSlot):FirstBattleRules.ResolveTurn(BattlePokemon,b.State with{Rng=rng},slot,opponentSlot);
        if(!turn.Succeeded)return new(CommandStatus.Fault,state,turn.Fault);
        var next=b with{State=turn.State!,Events=turn.Events,EventIndex=0,Phase=CampaignBattlePhase.Events,RandomTrace=[..(ai?.Trace??[]),..turn.Trace],AiDecision=ai};
        return new(CommandStatus.Accepted,PrepareBattlePresentation(state with{Battle=next,Inventory=potion?.Inventory??state.Inventory,Party=state.Party.SetItem(0,state.Party[0] with{Pokemon=turn.State!.Player.Pokemon}),World=state.World with{Rng=turn.State!.Rng}}));
    }
    private CampaignState PrepareBattlePresentation(CampaignState state)
    {
        var b=state.Battle!;
        while(b.EventIndex<b.Events.Length)
        {
            var effect=b.Events[b.EventIndex];
            if(effect is FirstBattleOrderEvent or FirstBattleTurnReadyEvent or FirstBattleEndedEvent){b=b with{EventIndex=b.EventIndex+1};continue;}
            PokemonInstance Update(FirstBattleSide side,Func<PokemonInstance,PokemonInstance> update)=>update(side==FirstBattleSide.Player?b.DisplayPlayer:b.DisplayOpponent);
            void Display(FirstBattleSide side,PokemonInstance mon){b=side==FirstBattleSide.Player?b with{DisplayPlayer=mon}:b with{DisplayOpponent=mon};}
            switch(effect)
            {
                case FirstBattleMoveUsedEvent used when used.Slot>=0:
                    Display(used.Actor,Update(used.Actor,p=>p with{Moves=p.Moves.SetItem(used.Slot,p.Moves[used.Slot] with{CurrentPp=used.PpAfter})}));break;
                case FirstBattlePotionRecoveredEvent potion:Display(potion.Side,Update(potion.Side,p=>p with{CurrentHp=potion.HpAfter}));break;
                case FirstBattleHpChangedEvent hp:Display(hp.Target,Update(hp.Target,p=>p with{CurrentHp=hp.HpAfter}));break;
                case FirstBattleRecoilEvent recoil:Display(recoil.Actor,Update(recoil.Actor,p=>p with{CurrentHp=recoil.HpAfter}));break;
            }
            return state with{Battle=b};
        }
        if(b.State.Outcome==FirstBattleOutcome.Ongoing)return state with{Battle=b with{Phase=CampaignBattlePhase.ChooseMove,DisplayPlayer=b.State.Player.Pokemon,DisplayOpponent=b.State.Opponent.Pokemon}};
        return SettleLabBattle(state with{Battle=b});
    }
    private CampaignState SettleLabBattle(CampaignState state)
    {
        var b=state.Battle!;
        if(b.Reward is not null||state.SettledBattles.Contains(b.Id))throw new InvalidDataException("battle_settlement_already_committed");
        var definition=BattleContent.Trainers.Battles[b.Trainer];var trainer=BattleContent.Trainers.Trainers[b.Trainer];
        var region=_content.MapDefinition(state.World.Player.MapId).RegionMapSection;
        var reward=LabBattleRewards.Apply(BattlePokemon,b.OwnedBefore,b.State.Player.Pokemon,b.State.Opponent.Pokemon,b.State.Outcome,state.Money,trainer,region);
        var pages=ImmutableArray.CreateBuilder<CampaignBattlePage>();
        foreach(var e in reward.Events)
        {
            if(e is LabBattleExperienceEvent xp)pages.Add(new("experience",$"{reward.Pokemon.Nickname} gained\n{xp.Gained} EXP. Points!",e));
            if(e is LabBattleLevelEvent level){pages.Add(new("level",$"{reward.Pokemon.Nickname} grew to\nlevel {level.After}!",e));pages.Add(new("stats","",e));}
        }
        foreach(var text in _content.TextPages(b.State.Outcome==FirstBattleOutcome.Won?definition.DefeatText:definition.VictoryText))pages.Add(new("source",text.Replace("{RIVAL}",state.RivalName,StringComparison.Ordinal).Replace("{PLAYER}",state.PlayerName,StringComparison.Ordinal)));
        foreach(var e in reward.Events.OfType<LabBattleMoneyEvent>())pages.Add(new("money",$"{state.PlayerName} got {e.Awarded}\nfor winning!",e));
        int? fraction=null;
        if(b.State.Outcome==FirstBattleOutcome.Won){var mon=reward.Pokemon.Pokemon;fraction=(mon.CurrentHp<mon.Stats.MaxHp/3*2?1:0)+(mon.CurrentHp<mon.Stats.MaxHp/3?1:0);}
        var history=new CampaignBattleHistory(b.Id,b.Trainer.Value,b.State.Outcome,reward.ExperienceGained,reward.MoneyGained,fraction,b.State.Outcome==FirstBattleOutcome.Won);
        return state with{Party=[reward.Pokemon],Money=reward.Money,SettledBattles=state.SettledBattles.Add(b.Id),BattleHistory=state.BattleHistory.Add(history),Battle=b with{Reward=reward,Phase=CampaignBattlePhase.Results,Pages=pages.ToImmutable(),PageIndex=0,DisplayPlayer=b.State.Player.Pokemon,DisplayOpponent=b.State.Opponent.Pokemon}};
    }
    private static CampaignState ProjectBattleResult(CampaignState state,int index)
    {
        var b=state.Battle!;var player=b.DisplayPlayer;
        if(b.Pages[index].Reward is LabBattleLevelEvent)player=b.Reward!.Pokemon.Pokemon;
        return state with{Battle=b with{PageIndex=index,DisplayPlayer=player}};
    }
    private CampaignState ReturnFromLabBattle(CampaignState state)
    {
        var b=state.Battle!;
        if(b.Reward is null||!state.SettledBattles.Contains(b.Id))throw new InvalidDataException("battle_not_settled");
        var returned=EarlyRivalCallback.Resolve(BattleContent.Trainers.Battles[b.Trainer],(EarlyRivalOutcome)(int)b.State.Outcome);
        if(!returned.ContinuePostBattleScript||returned.WhiteOut)throw new InvalidDataException("early_rival_callback_unsupported");
        var party=returned.HealBeforeResume?state.Party.Select(p=>LabBattleRewards.Heal(BattlePokemon,p)).ToImmutableArray():state.Party;
        var next=state with{Battle=null,Party=party,BattledTrainers=state.BattledTrainers.Add(b.Trainer.Value),Dialogue=null,Motions=[],PresentationTicks=0,Music=_content.MapDefinition(state.World.Player.MapId).Music,
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",returned.SpecialVarResult)}};
        return Pump(Advance(next,StoryRules.Continue(next.Script,next.Script.Wait!.CorrelationId,_content)));
    }
}
