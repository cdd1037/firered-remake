using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public enum CampaignDoubleBattleStage { Intro, Combat, Finished }
public sealed record CampaignDoubleBattle(string Id, TrainerId Trainer, string ActorId,
    SourceBattleFrame Frame, CampaignDoubleBattleStage Stage,
    ImmutableArray<SourceBattleEvent> Events, int EventIndex,
    ImmutableArray<SourceActionSelection> PlayerSelections,
    ImmutableArray<int> LevelsBeforeBattle, int ExperienceGained, int MoneyGained,
    ImmutableArray<WildBattleCombatant> DisplayedSlots, ImmutableArray<PartyMemberKey> DisplayedMembers,
    ImmutableArray<RandomTrace> Trace, ImmutableArray<string> FinishPages, int FinishPageIndex)
{
    [JsonIgnore] public SourceBattleEvent? CurrentEvent => EventIndex < Events.Length ? Events[EventIndex] : null;
    [JsonIgnore] public int? SelectingSlot => Stage != CampaignDoubleBattleStage.Combat || CurrentEvent is not null ||
        Frame.FourTurn is not null ? null : new[] { 0, 2 }.Where(i => Frame.CanAct(new(i)) && Frame.Get(new(i)).Registers.Turn?.Lock is null &&
        !PlayerSelections.Any(s => s.Actor.Slot.Value == i)).Select(i => (int?)i).FirstOrDefault();
    [JsonIgnore] public bool LearningPending => CurrentEvent is null && Frame.Rewards?.Pending is not null;
    [JsonIgnore] public int? ReplacementSlot => CurrentEvent is not null ? null :
        Frame.PendingSwitches.Where(s => s.Slot.Side == BattleSide.Player).Select(s => (int?)s.Slot.Value).FirstOrDefault();
}
public sealed record CampaignDoubleBattleHistory(string Id, string TrainerId, WildBattleOutcome Outcome,
    int ExperienceGained, int MoneyGained, ImmutableArray<int> OpponentsSeen);

public sealed partial class CampaignSession
{
    private PokemonCatalog DoubleBattlePokemon => _content.PresetDoublePokemon ??
        throw new InvalidDataException("preset_double_content_missing");
    private SourceTwinsTrainerDefinition TwinsDefinition => _content.Route8Twins ??
        throw new InvalidDataException("preset_double_definition_missing");
    private static bool IsTwinsScript(string script) => script is "Route8_EventScript_Eli" or "Route8_EventScript_Anne";
    private SourceTwinsEncounterResult PrepareDoubleBattle(CampaignState state)
    {
        if(!_content.PresetDoublesEnabled||state.World.Player.MapId.Value!="map.route-8"||
            state.DoubleBattle is not null||state.ForestTrainerBattle is not null||state.WildBattle is not null||state.Battle is not null||
            state.Party.Any(p=>p.IsEgg||p.Pokemon.OriginalTrainerId!=state.TrainerId||p.OriginalTrainerName!=state.PlayerName||p.OriginalTrainerGender!=state.PlayerGender))
            throw new InvalidDataException("preset_double_entry_context");
        var party=state.Party.Select(p=>new WildBattleCalledPartyMember(p.Pokemon,p.HeldItem,p.Status,p.IsEgg)).ToImmutableArray();
        var entry=SourceTwinsTrainerEncounter.Begin(DoubleBattlePokemon,TwinsDefinition,party,state.World.Rng,
            state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"));
        if(!entry.Succeeded)throw new InvalidDataException(entry.Fault);
        // This successor still accepts only the original local gifts/captures. Its stable
        // schema has no infection or trade route. ValidatePlayerSave enforces that closed
        // provenance; a future Pokerus/trade profile must provide its own authoritative facts.
        var frame=entry.State! with{Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,state.TrainerId,state.PlayerName,
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection),Metadata=state.Party.Select((p,i)=>new SourceRewardMetadata(
            new(BattleSide.Player,i),p.OriginalTrainerName,p.MetLocation,p.Ball,SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
        if(SourceFourActionTurns.ValidateCapability(DoubleBattlePokemon,frame) is {} error)throw new InvalidDataException(error);
        return entry with{State=frame};
    }

    private CampaignState BeginDoubleBattle(CampaignState state,StoryEffect effect)
    {
        if(!_content.PresetDoublesEnabled||effect.Args.Length!=4||state.Script.Wait?.Kind!=StoryWaitKind.Battle||
            state.LastTalked is not {} actorId||!state.Actors.TryGetValue(actorId,out var actor)||!IsTwinsScript(actor.ScriptId)||
            state.World.Player.MapId.Value!="map.route-8")throw new InvalidDataException("preset_double_source_context");
        var d=TwinsDefinition.Interactions.Single(x=>x.ScriptEntry==actor.ScriptId);
        bool rematch=effect.Opcode=="trainerbattle_rematch_double";
        if(effect.Source.Path!=d.Source.Path||effect.Source.Symbol!=d.ScriptEntry+(rematch?"Rematch":"")||
            effect.Args[0]!="TRAINER_TWINS_ELI_ANNE"||effect.Args[1]!=(rematch?d.IntroText.Replace("Intro","RematchIntro",StringComparison.Ordinal):d.IntroText)||
            effect.Args[2]!=d.DefeatText||effect.Args[3]!=d.NotEnoughMonsText)
            throw new InvalidDataException("preset_double_source_mismatch");
        if(!_forestTrainerApproaching)state=FaceSelectedObject(state);
        if(state.BattledTrainers.Contains(SourceTwinsTrainerEncounter.TrainerId.Value))
        {
            // The current campaign has no VS Seeker. The source rematch wrapper reaches
            // its post-battle speech without creating a second encounter.
            var next=state with{Script=state.Script with{Locked=false,Variables=state.Script.Variables.SetItem("VAR_RESULT",rematch?0:1)}};
            return Advance(next,StoryRules.Continue(next.Script,next.Script.Wait!.CorrelationId,_content));
        }
        if(rematch)throw new InvalidDataException("preset_double_unearned_rematch");
        if(state.Party.Count(p=>!p.IsEgg&&p.Pokemon.CurrentHp>0)<2)
            return state with{Dialogue=new(d.NotEnoughMonsText,_content.TextPages(d.NotEnoughMonsText),0,false,true),
                Script=state.Script with{Stack=[],Locked=false,Wait=state.Script.Wait! with{Kind=StoryWaitKind.Dialog}}};
        var prepared=PrepareDoubleBattle(state);var frame=prepared.State!;
        string id="double-trainer-battle:"+((state.DoubleBattleHistory?.Length??0)+1);
        return state with{DoubleBattle=new(id,SourceTwinsTrainerEncounter.TrainerId,actorId,frame,CampaignDoubleBattleStage.Intro,
            prepared.Events,0,[],state.Party.Select(p=>p.Pokemon.Level).ToImmutableArray(),0,0,
            frame.Slots.Select(c=>c.Body!).ToImmutableArray(),frame.Slots.Select(c=>c.Member!.Value).ToImmutableArray(),prepared.Trace,[],0),
            Dialogue=new(d.IntroText,_content.TextPages(d.IntroText),0,false,false),
            Script=state.Script with{Locked=true,Variables=state.Script.Variables.SetItem("VAR_RESULT",0)},
            Actors=state.Actors.SetItem(actorId,actor with{MovementType=ForestTrainerFacingType(actor.Facing)}),Music=TwinsDefinition.EncounterMusic};
    }

    private static SourceTurnContinuation DoubleContinuation(SourceBattleFrame f)=>
        new(f.Revision,f.EventSequence,f.NextAction,f.FourTurn?.Phase??throw new InvalidDataException("preset_double_no_continuation"));

    private Decision<CampaignState> DecideDoubleBattle(CampaignState state,CampaignCommand command)
    {
        var b=state.DoubleBattle!;
        Decision<CampaignState> No(string error)=>new(CommandStatus.Rejected,state,error);
        if(!_content.PresetDoublesEnabled||state.Script.Wait?.Kind!=StoryWaitKind.Battle)return No("preset_double_script_context");
        if(b.Stage==CampaignDoubleBattleStage.Intro)
        {
            if(command.Kind!="ack")return No("preset_double_intro_pending");
            if(state.Dialogue is {} d&&d.Page+1<d.Pages.Length)return new(CommandStatus.Accepted,state with{Dialogue=d with{Page=d.Page+1}});
            return new(CommandStatus.Accepted,ResetFieldPoisonCounterForBattle(state) with{Dialogue=null,Motions=[],PresentationTicks=0,
                DoubleBattle=b with{Stage=CampaignDoubleBattleStage.Combat},World=state.World with{Rng=b.Frame.Rng},
                Music="MUS_VS_TRAINER",SeenSpecies=state.SeenSpecies.Union(b.Frame.Party(BattleSide.Opponent).Members.Select(p=>p.Pokemon.SourceSpeciesId))});
        }
        if(b.Stage==CampaignDoubleBattleStage.Finished)
        {
            if(command.Kind!="ack")return No("preset_double_result_pending");
            if(b.FinishPageIndex+1<b.FinishPages.Length)return new(CommandStatus.Accepted,state with{DoubleBattle=b with{FinishPageIndex=b.FinishPageIndex+1}});
            return new(CommandStatus.Accepted,BeginDoubleEvolution(state));
        }
        if(b.CurrentEvent is not null)
        {
            if(command.Kind!="ack")return No("preset_double_event_pending");
            var next=ProjectDoubleDisplayEvent(b,b.CurrentEvent);
            next=next with{EventIndex=next.EventIndex+1};
            if(next.CurrentEvent is null)next=next with{DisplayedSlots=next.Frame.Slots.Select(c=>c.Body!).ToImmutableArray(),DisplayedMembers=next.Frame.Slots.Select(c=>c.Member!.Value).ToImmutableArray()};
            return new(CommandStatus.Accepted,state with{DoubleBattle=next});
        }
        if(b.Frame.Rewards?.Pending is {} learning)
        {
            if(command.Kind!="double_learn"||command.MoveSlot is < -1 or >3)return No("preset_double_learning_pending");
            var candidate=learning.Moves[0];
            return CommitDoubleResult(state,SourceFourRewards.Learn(DoubleBattlePokemon,b.Frame,DoubleContinuation(b.Frame),
                new(learning.Recipient,candidate.Level,candidate.Move,command.MoveSlot<0?null:command.MoveSlot)));
        }
        if(!b.Frame.PendingSwitches.IsEmpty)
        {
            var pending=b.Frame.PendingSwitches[0];
            if(command.Kind!="double_switch"||command.PartyIndex!=pending.Slot.Value||command.Choice is not {} index)return No("preset_double_replacement_pending");
            var incoming=new PartyMemberKey(pending.Slot.Side,index);
            var result=pending.Kind==WildBattleCalledSwitchKind.BatonPass
                ?SourceFourActionTurns.ChooseActionSwitch(DoubleBattlePokemon,b.Frame,DoubleContinuation(b.Frame),pending.Slot,incoming)
                :SourceFourActionTurns.ChooseReplacement(DoubleBattlePokemon,b.Frame,DoubleContinuation(b.Frame),pending.Slot,incoming);
            return CommitDoubleResult(state,result);
        }
        if(b.Frame.FourTurn is {Phase:SourceFourActionPhase.BattleEnded})
            return command.Kind=="ack"?new(CommandStatus.Accepted,SettleDoubleBattle(state)):No("preset_double_end_pending");
        if(b.Frame.FourTurn is not null)
            return command.Kind=="ack"?CommitDoubleResult(state,SourceFourActionTurns.Advance(DoubleBattlePokemon,b.Frame,DoubleContinuation(b.Frame))):No("preset_double_resolution_pending");
        if(command.Kind=="double_cancel")
        {
            if(b.PlayerSelections.IsEmpty)return No("preset_double_no_selection");
            return new(CommandStatus.Accepted,state with{DoubleBattle=b with{PlayerSelections=b.PlayerSelections.RemoveAt(b.PlayerSelections.Length-1)}});
        }
        var frame=b.Frame;
        // Forced turn locks are source controller choices, not fresh player input.
        var selections=b.PlayerSelections;
        foreach(int i in new[]{0,2})
            if(frame.CanAct(new(i))&&!selections.Any(a=>a.Actor.Slot.Value==i)&&frame.Get(new(i)).Registers.Turn?.Lock is {} forced)
                selections=selections.Add(new(frame.Get(new(i)).Occupant!.Value,SourceActionKind.Move,forced.PpSlot,forced.Target));
        var unselected=new[]{0,2}.Where(i=>frame.CanAct(new(i))&&!selections.Any(a=>a.Actor.Slot.Value==i)).ToArray();
        if(unselected.Length>0)
        {
            if(command.PartyIndex!=unselected[0]||command.Kind is not ("double_move" or "double_switch"))return No("preset_double_action_required");
            var cell=frame.Get(new(command.PartyIndex));SourceActionSelection selection;
            if(command.Kind=="double_switch")
            {
                if(command.Choice is not {} incoming||!SourceBattleIdentityRules.LegalReserveIndices(frame,BattleSide.Player).Contains(incoming)||
                    selections.Any(s=>s.Incoming==new PartyMemberKey(BattleSide.Player,incoming)))return No("preset_double_reserve_invalid");
                selection=new(cell.Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,incoming));
            }
            else
            {
                if(command.MoveSlot==WildBattleRules.StruggleSlot)
                {
                    if(!SourceFourActionTurns.RequiresStruggle(DoubleBattlePokemon,frame,cell.Slot))return No("preset_double_struggle_not_required");
                    selection=SourceActionSelection.Struggle(cell.Occupant!.Value,frame.Slots.First(c=>c.Slot.Side==BattleSide.Opponent&&frame.CanAct(c.Slot)).Slot);
                }
                else
                {
                    if(command.MoveSlot<0||command.MoveSlot>=cell.Body!.Pokemon.Moves.Length||command.Choice is not (>=0 and <=3))return No("preset_double_move_or_target_invalid");
                    selection=new(cell.Occupant!.Value,SourceActionKind.Move,command.MoveSlot,new(command.Choice.Value));
                }
                if(SourceBattleKernel.ValidateAction(DoubleBattlePokemon,frame,new(cell.Occupant.Value,frame.Revision,command.MoveSlot,selection.Target!.Value)) is {} invalid&&
                    invalid is not ("source_kernel_switch_owner_required" or "source_kernel_flinch_order_not_migrated"))return No(invalid);
            }
            selections=selections.Add(selection);
            if(unselected.Length>1)return new(CommandStatus.Accepted,state with{DoubleBattle=b with{PlayerSelections=selections}});
        }
        else if(command.Kind!="ack")return No("preset_double_forced_turn_ready");
        var ai=SourceTwinsTrainerEncounter.ChooseEnemyActions(DoubleBattlePokemon,frame,frame.Revision);
        if(!ai.Succeeded)return No(ai.Fault!);
        var begun=SourceFourActionTurns.BeginActions(DoubleBattlePokemon,ai.State,ai.State.Revision,selections.AddRange(ai.Selections));
        if(!begun.Succeeded)return No(begun.Fault!);
        return CommitDoubleResult(state with{DoubleBattle=b with{PlayerSelections=[]}},begun with{Trace=ai.Trace.AddRange(begun.Trace)});
    }

    private Decision<CampaignState> CommitDoubleResult(CampaignState state,SourceFourActionResult result)
    {
        if(!result.Succeeded)return new(CommandStatus.Rejected,state,result.Fault);
        var b=state.DoubleBattle!;var party=state.Party.ToBuilder();
        var members=result.State.Party(BattleSide.Player).Members;
        if(members.Length!=party.Count)throw new InvalidDataException("preset_double_party_count");
        for(int i=0;i<party.Count;i++)
        {
            var m=members[i];if(!SourceOwnedIdentity(m.Pokemon,party[i].Pokemon)||m.IsEgg!=party[i].IsEgg)
                throw new InvalidDataException("preset_double_owned_identity");
            party[i]=party[i] with{Pokemon=m.Pokemon,Status=m.Status,HeldItem=WildBattleSourcePartyEffects.PersistentHeldItem(m)};
        }
        int xp=result.Events.OfType<SourceRewardAppliedEvent>().Sum(e=>e.Reward.ExperienceCredited);
        var next=b with{Frame=result.State,Events=result.Events,EventIndex=0,Trace=result.Trace,ExperienceGained=checked(b.ExperienceGained+xp)};
        if(result.Events.IsEmpty)next=next with{DisplayedSlots=result.State.Slots.Select(c=>c.Body!).ToImmutableArray(),DisplayedMembers=result.State.Slots.Select(c=>c.Member!.Value).ToImmutableArray()};
        return new(CommandStatus.Accepted,state with{Party=party.ToImmutable(),DoubleBattle=next,World=state.World with{Rng=result.State.Rng}});
    }

    private static CampaignDoubleBattle ProjectDoubleDisplayEvent(CampaignDoubleBattle battle,SourceBattleEvent e)
    {
        var bodies=battle.DisplayedSlots;var members=battle.DisplayedMembers;
        void Hp(BattlerSlot slot,int hp){var m=bodies[slot.Value];bodies=bodies.SetItem(slot.Value,m with{Pokemon=m.Pokemon with{CurrentHp=hp}});}
        switch(e)
        {
            case SourceHpChangedEvent x:Hp(x.Target,x.HpAfter);break;
            case SourceRecoilEvent x:Hp(x.Actor,x.HpAfter);break;
            case SourceConfusionSelfHitEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceStatusDamageEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceWeatherDamageEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceAbilityDamageEvent x:Hp(x.Target,x.HpAfter);break;
            case SourceRecoveredEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceDrainEvent x:Hp(x.Actor,x.HpAfter);break;
            case SourceFaintedEvent x:Hp(x.Side,0);break;
            case SourceHeldItemConsumedEvent x:Hp(x.Side,x.HpAfter);bodies=bodies.SetItem(x.Side.Value,bodies[x.Side.Value] with{HeldItem="ITEM_NONE"});break;
            case SourceStatusChangedEvent x:bodies=bodies.SetItem(x.Side.Value,bodies[x.Side.Value] with{Status=x.After});break;
            case SourceCalledPartySwitchedEvent x:bodies=bodies.SetItem(x.Side.Value,battle.Frame.Get(x.Side).Body!);members=members.SetItem(x.Side.Value,new(x.Side.Side,x.IncomingIndex));break;
            case SourcePlayerReplacedEvent x:bodies=bodies.SetItem(x.Slot.Value,x.Replacement.Body);break;
            case SourcePlayerSwitchedEvent x:bodies=bodies.SetItem(x.Slot.Value,x.Replacement.Body);break;
        }
        return battle with{DisplayedSlots=bodies,DisplayedMembers=members};
    }

    private CampaignState SettleDoubleBattle(CampaignState state)
    {
        var b=state.DoubleBattle!;
        if(b.Frame.Outcome==WildBattleOutcome.Ongoing||b.Frame.Rewards?.Pending is not null||
            (state.DoubleBattleHistory??[]).Any(h=>h.Id==b.Id))throw new InvalidDataException("preset_double_settlement_context");
        bool won=b.Frame.Outcome==WildBattleOutcome.Won;int award=won?TwinsDefinition.VictoryMoney:0;
        int money=Math.Min(999999,checked(state.Money+award));
        var pages=ImmutableArray.CreateBuilder<string>();
        if(won)
        {
            pages.Add($"{state.PlayerName} defeated\nTWINS ELI & ANNE!");
            var def=TwinsDefinition.Interactions.Single(d=>d.ScriptEntry==state.Actors[b.ActorId].ScriptId);
            pages.AddRange(_content.TextPages(def.DefeatText));pages.Add($"{state.PlayerName} got ¥{award}\nfor winning!");
        }
        else
        {
            pages.Add($"{state.PlayerName} is out of usable\nPOKéMON!");
            int loss=WhiteOutLoss(state);pages.Add(state.Money==0?$"{state.PlayerName} whited out!":$"{state.PlayerName} panicked and lost\n{loss}...\n{state.PlayerName} whited out!");
        }
        var history=new CampaignDoubleBattleHistory(b.Id,b.Trainer.Value,b.Frame.Outcome,b.ExperienceGained,money-state.Money,[0,1]);
        if(won)
        {
            var payday=VermilionPayDayRules.Settle(money,b.Frame.Field.PayDayMoney,WildBattleOutcome.Won);money=payday.Money;
            if(payday.Collected!=0)pages.Add($"{state.PlayerName} picked up\n¥{payday.Collected}!");
            var pickup=VermilionPickupRules.Apply(BattlePokemon,state.Party,state.World.Rng,WildBattleOutcome.Won,
                ssAnneRules:true,route11Rules:true,rockTunnelRules:true,celadonApproachRules:true,celadonGymRules:true,rocketHideoutRules:true,pokemonTowerRules:true,snorlaxRules:true,fuchsiaRules:true);
            state=state with{Party=pickup.Party,World=state.World with{Rng=pickup.Rng}};
            b=b with{Frame=b.Frame with{Rng=pickup.Rng},Trace=pickup.Trace};
        }
        return state with{Money=money,DoubleBattleHistory=(state.DoubleBattleHistory??[]).Add(history),
            DoubleBattle=b with{Stage=CampaignDoubleBattleStage.Finished,MoneyGained=history.MoneyGained,FinishPages=pages.ToImmutable(),FinishPageIndex=0}};
    }

    private CampaignState BeginDoubleEvolution(CampaignState state)
    {
        var b=state.DoubleBattle!;
        if(b.Frame.Outcome!=WildBattleOutcome.Won)return ReturnFromDoubleBattle(state);
        var proposals=LevelEvolutionRules.ForParty(_content.PokemonContent!,state.Party,b.LevelsBeforeBattle);
        return proposals.IsEmpty?ReturnFromDoubleBattle(state):state with{Evolution=new(b.Id,proposals,0,CampaignEvolutionStage.Proposal,[],0,[])};
    }

    private CampaignState ReturnFromDoubleBattle(CampaignState state)
    {
        var b=state.DoubleBattle!;
        if(!(state.DoubleBattleHistory??[]).Any(h=>h.Id==b.Id))throw new InvalidDataException("preset_double_history_missing");
        var next=state with{DoubleBattle=null,Dialogue=null,Motions=[],PresentationTicks=0,
            Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false}};
        if(b.Frame.Outcome!=WildBattleOutcome.Won)return WhiteOutFromRoute1(next);
        return next with{BattledTrainers=next.BattledTrainers.Add(b.Trainer.Value),
            Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",0)},
            Music=_content.MapDefinition(next.World.Player.MapId).Music,Exploration=ResetEncounterModifiers(next.Exploration!)};
    }

    private int TwinsWonFlagCount(CampaignState state)=>_content.PresetDoublesEnabled&&state.BattledTrainers.Contains(SourceTwinsTrainerEncounter.TrainerId.Value)?1:0;
    private bool SeenByTwins(CampaignState state,int species)=>_content.PresetDoublesEnabled&&species is 35 or 39&&(state.DoubleBattleHistory??[]).Length>0;
    private string? ValidateDoubleBattleHistory(CampaignState state)
    {
        var history=state.DoubleBattleHistory??[];
        bool won=state.BattledTrainers.Contains(SourceTwinsTrainerEncounter.TrainerId.Value);
        if(!_content.PresetDoublesEnabled)return history.Length!=0||won||state.DoubleBattle is not null?"save_preset_double_profile_missing":null;
        if(history.IsDefault)return "save_preset_double_history_invalid";
        bool winner=false;
        for(int i=0;i<history.Length;i++)
        {
            var h=history[i];
            if(h is null||winner||h.Id!="double-trainer-battle:"+(i+1)||h.TrainerId!=SourceTwinsTrainerEncounter.TrainerId.Value||
                h.Outcome is not (WildBattleOutcome.Won or WildBattleOutcome.Lost or WildBattleOutcome.Draw)||h.ExperienceGained<0||h.ExperienceGained>TwinsDefinition.Trainer.Party.Sum(p=>DoubleBattlePokemon.Species[p.Species].ExperienceYield*p.Level/7*150/100)||
                h.MoneyGained<0||h.MoneyGained>(h.Outcome==WildBattleOutcome.Won?528:0)||h.OpponentsSeen.IsDefault||!h.OpponentsSeen.SequenceEqual(new[]{0,1})||
                !state.SeenSpecies.Contains(35)||!state.SeenSpecies.Contains(39))return "save_preset_double_history_invalid";
            winner=h.Outcome==WildBattleOutcome.Won;
        }
        return won==winner?null:"save_preset_double_flag_mismatch";
    }
}
