using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Capture;
using Game.Domain.Events;
using Game.Domain.World;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed record CampaignWildAppearance(SpeciesId Species,uint Personality,uint OriginalTrainerId);
public sealed record CampaignWildPage(string Text,WildRewardEvent? Reward=null);
public sealed record CampaignWildBattle(string Id,PartyPokemon OwnedBefore,WildBattleState State,
    ImmutableArray<WildBattleEvent> Events,int EventIndex,PokemonInstance DisplayPlayer,PokemonInstance DisplayOpponent,
    CampaignBattlePhase Phase,WildVictoryRewardResult? Reward,ImmutableArray<CampaignWildPage> Pages,int PageIndex,
    ImmutableArray<RandomTrace> RandomTrace,ImmutableArray<WildPendingMove> PendingMoves)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignWildAppearance? DisplayPlayerAppearance{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignWildAppearance? DisplayOpponentAppearance{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int ActivePartyIndex{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignWildOrigin? Origin{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignPendingCapture? Capture{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignReplacementStage? Replacement{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableSortedSet<long>? FaintedMembers{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableSortedSet<long>? ParticipatingMembers{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? OpponentHpAtLastSwitch{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignVoluntarySwitch? LastSwitch{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<CampaignWildRewardReceiver>? RewardReceivers{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int RewardReceiverIndex{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool PostBattleItemsSettled{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableSortedDictionary<long,string>? KnockedOffHeldItems{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool ScriptedMarowak{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int ScriptedSnorlaxRoute{get;init;}
    [JsonIgnore] public int RewardPartyIndex=>RewardReceivers is {} receivers&&RewardReceiverIndex<receivers.Length?receivers[RewardReceiverIndex].PartyIndex:ActivePartyIndex;
}

public sealed partial class CampaignSession
{
    private string CampaignWildRulesVersion=>_content.FuchsiaRouteEnabled?WildBattleRules.FuchsiaRulesVersion:_content.SnorlaxEnabled?WildBattleRules.SnorlaxRulesVersion:_content.PokemonTowerEnabled?WildBattleRules.TowerRulesVersion:_content.RocketHideoutEnabled?WildBattleRules.RocketHideoutRulesVersion:_content.CeladonGymEnabled?WildBattleRules.CeladonGymRulesVersion:_content.CeladonApproachEnabled?WildBattleRules.CeladonApproachRulesVersion:_content.RockTunnelEnabled?WildBattleRules.RockTunnelRulesVersion:_content.Route11Enabled?WildBattleRules.Route11RulesVersion:_content.VermilionGymEnabled?WildBattleRules.VermilionGymRulesVersion:_content.SsAnneEnabled?WildBattleRules.SsAnneRulesVersion:_content.VermilionEnabled?WildBattleRules.VermilionRulesVersion:_content.BillEnabled?WildBattleRules.BillRulesVersion:_content.CeruleanEnabled?WildBattleRules.CeruleanRulesVersion:_content.MtMoonEnabled?WildBattleRules.MtMoonRulesVersion:_content.Route3Enabled?WildBattleRules.Route3RulesVersion:_content.Route22Enabled?WildBattleRules.Route22RulesVersion:_content.EarlyPartyEnabled?WildBattleRules.EarlyPartyRulesVersion:_content.EarlyStarterEnabled?WildBattleRules.EarlyStarterRulesVersion:_content.PewterEnabled?WildBattleRules.PewterRulesVersion:_content.ForestEnabled?WildBattleRules.ForestRulesVersion:WildBattleRules.RulesVersion;
    private CampaignState BeginWildBattle(CampaignState state,Route1WildPokemonResult? fishingPokemon=null,bool scriptedMarowak=false,int scriptedSnorlaxRoute=0)
    {
        if(_content.ForestEnabled&&long.MaxValue-Revision<4096)throw new InvalidDataException("wild_battle_command_headroom_required");
        state=ResetFieldPoisonCounterForBattle(state);
        var exploration=state.Exploration??throw new InvalidDataException("exploration_missing");
        if(exploration.NextWildBattleId is <1 or long.MaxValue||state.WildBattle is not null||state.Battle is not null||state.Party.IsEmpty||state.Party.Length>6||(!_content.CaptureEnabled&&state.Party.Length!=1))throw new InvalidDataException("wild_battle_context_invalid");
        int active=_content.CaptureEnabled?Enumerable.Range(0,state.Party.Length).First(i=>!state.Party[i].IsEgg&&state.Party[i].Pokemon.CurrentHp>0):0;
        var owned=state.Party[active];
        if(owned.Pokemon.OriginalTrainerId!=state.TrainerId||owned.OriginalTrainerName!=state.PlayerName)throw new InvalidDataException("wild_battle_local_owner_required");
        Route1WildPokemonResult generated;
        if(fishingPokemon is not null)generated=fishingPokemon;
        else if(_content.ForestEnabled)
        {
            var early=exploration.EarlyEncounter??throw new InvalidDataException("early_encounter_missing");var table=_content.LandEncounters![state.World.Player.MapId];
            var result=EarlyLandWildPokemonFactory.Generate(BattlePokemon,table,early,state.World.Rng,state.TrainerId,ceruleanRules:_content.CeruleanEnabled,billRules:_content.BillEnabled,vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,pokemonTowerRules:_content.PokemonTowerEnabled,snorlaxRules:_content.SnorlaxEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled);
            generated=new(result.Pokemon,result.GlobalRng,result.RandomTrace);
        }
        else generated=Route1WildPokemonFactory.Generate(BattlePokemon,exploration.Encounter!,state.World.Rng,state.TrainerId);
        // Start owns source AI-init -> held-item -> order/turn-number RNG sequencing.
        var player=new WildBattleCombatant(owned.Pokemon,HeldItem:owned.HeldItem,Status:owned.Status){AttackBadgeBoost=_content.PewterEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),SpeedBadgeBoost=_content.VermilionGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),DefenseBadgeBoost=_content.FuchsiaGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET")};
        bool towerBattle=_content.PokemonTowerEnabled&&TowerMap(state.World.Player.MapId.Value)&&fishingPokemon is null||scriptedMarowak;
        var kind=PokemonTowerBattleContract.Classify(scriptedMarowak,HasSilphScope(state));
        var started=_content.FuchsiaGymEnabled?WildBattleRules.StartSourceWild(BattlePokemon,SourceTrainerEntryParty(state),active,new(generated.Pokemon),generated.GlobalRng,
            CampaignBattleTerrain(state),fishingPokemon is not null,towerBattle?kind:null,scriptedSnorlaxRoute!=0):scriptedSnorlaxRoute!=0?WildBattleRules.StartSnorlax(BattlePokemon,player,new(generated.Pokemon),generated.GlobalRng,CampaignBattleTerrain(state),fuchsiaRules:_content.FuchsiaRouteEnabled):towerBattle?WildBattleRules.StartPokemonTower(BattlePokemon,player,new(generated.Pokemon),generated.GlobalRng,kind,snorlaxRules:_content.SnorlaxEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled):
            WildBattleRules.Start(BattlePokemon,player,new(generated.Pokemon),generated.GlobalRng,CampaignWildRulesVersion,terrain:CampaignBattleTerrain(state),oldRodFishing:fishingPokemon is not null);

        if(!started.Succeeded)throw new InvalidDataException(started.Fault);
        var battle=started.State!;
        var b=new CampaignWildBattle("wild-battle:"+exploration.NextWildBattleId,owned,battle,started.Events,0,battle.Player.Pokemon,battle.Opponent.Pokemon,CampaignBattlePhase.Events,null,[],0,[..generated.RandomTrace,..started.Trace],[])
        {ScriptedSnorlaxRoute=scriptedSnorlaxRoute,ScriptedMarowak=scriptedMarowak,ActivePartyIndex=active,Origin=_content.CaptureEnabled?new(_content.MapDefinition(state.World.Player.MapId).RegionMapSection,generated.Pokemon.Level,_content.PokemonContent!.SpeciesNames[generated.Pokemon.SourceSpeciesId]):null,FaintedMembers=_content.CaptureEnabled?ImmutableSortedSet<long>.Empty:null};
        if(_content.PartyTrainingEnabled)b=b with{ParticipatingMembers=ImmutableSortedSet<long>.Empty.Add(owned.InstanceId),OpponentHpAtLastSwitch=battle.Opponent.Pokemon.CurrentHp};
        if(fishingPokemon is null)exploration=_content.ForestEnabled?exploration with{EarlyEncounter=EarlyLandEncounterRules.CompleteCandidate(exploration.EarlyEncounter!,_content.LandEncounters![state.World.Player.MapId])}:exploration with{Encounter=Route1EncounterRules.CompleteCandidate(exploration.Encounter!)};
        return PrepareWildPresentation(state with{WildBattle=b,Exploration=exploration with{NextWildBattleId=checked(exploration.NextWildBattleId+1)},World=state.World with{Rng=battle.Rng},SeenSpecies=towerBattle&&!PokemonTowerBattleContract.Policy(kind).RegisterSeen?state.SeenSpecies:state.SeenSpecies.Add(battle.Opponent.Pokemon.SourceSpeciesId),Motions=[],PresentationTicks=0});
    }
    private Decision<CampaignState> DecideWildBattle(CampaignState state,CampaignCommand command)
    {
        var b=state.WildBattle!;
        if(b.Capture is not null)return DecideCapture(state,command);
        if(b.Phase==CampaignBattlePhase.ChooseMove&&WildBattleRules.IsSourceWild(b.State)&&b.State.CalledParty?.PendingSwitch is{Kind:WildBattleCalledSwitchKind.BatonPass})return DecideSourceWildPartyChoice(state,command);
        if(b.Replacement is not null&&b.Phase==CampaignBattlePhase.ChooseMove)return DecideWildReplacement(state,command);
        if(command.Kind=="ack")
        {
            if(b.Phase==CampaignBattlePhase.Events)return new(CommandStatus.Accepted,PrepareWildPresentation(state with{WildBattle=b with{EventIndex=b.EventIndex+1}}));
            if(b.Phase==CampaignBattlePhase.Results)
            {
                if(b.PageIndex+1<b.Pages.Length)
                {
                    int index=b.PageIndex+1;var display=b.Pages[index].Reward is WildRewardLevelEvent&&b.RewardPartyIndex==b.ActivePartyIndex?state.Party[b.ActivePartyIndex].Pokemon:b.DisplayPlayer;
                    return new(CommandStatus.Accepted,state with{WildBattle=b with{PageIndex=index,DisplayPlayer=display}});
                }
                if(!b.PendingMoves.IsEmpty)return new(CommandStatus.Rejected,state,"wild_move_learning_required");
                return new(CommandStatus.Accepted,CompleteWildRewardReceiver(state));
            }
            return new(CommandStatus.Rejected,state,"battle_move_required");
        }
        if(command.Kind=="wild_learn")
        {
            if(b.Phase!=CampaignBattlePhase.Results||b.PageIndex+1<b.Pages.Length||b.PendingMoves.IsEmpty)return new(CommandStatus.Rejected,state,"wild_learning_not_pending");
            var move=b.PendingMoves[0];var owned=state.Party[b.RewardPartyIndex];
            if(command.MoveSlot is < -1 or >3)return new(CommandStatus.Rejected,state,"wild_learning_slot_invalid");
            if (_content.SsAnneEnabled && command.MoveSlot >= 0 && command.MoveSlot < owned.Pokemon.Moves.Length && PokemonMoveRules.IsHiddenMachineMove(owned.Pokemon.Moves[command.MoveSlot].Move))
                return new(CommandStatus.Rejected, state, "hm_move_cannot_be_forgotten");
            if(command.MoveSlot>=0 && !PokemonMoveRules.IsLearningSupported(BattlePokemon,move.Move))
                return new(CommandStatus.Rejected,state,"pokemon_learning_effect_unsupported");
            if(command.MoveSlot>=0)
            {
                var learned=PokemonMoveRules.Replace(BattlePokemon,owned.Pokemon.Moves,command.MoveSlot,move.Move);
                owned=owned with{Pokemon=owned.Pokemon with{Moves=learned}};
            }
            var next=state with{Party=state.Party.SetItem(b.RewardPartyIndex,owned),WildBattle=b with{PendingMoves=b.PendingMoves.RemoveAt(0),DisplayPlayer=b.RewardPartyIndex==b.ActivePartyIndex?owned.Pokemon:b.DisplayPlayer}};
            return new(CommandStatus.Accepted,next.WildBattle!.PendingMoves.IsEmpty?CompleteWildRewardReceiver(next):next);
        }
        if(b.Phase!=CampaignBattlePhase.ChooseMove)return new(CommandStatus.Rejected,state,"battle_presentation_pending");
        if(command.Kind=="battle_switch"&&_content.PartyTrainingEnabled)return DecideVoluntaryWildSwitch(state,command);
        if(command.Kind=="battle_poke_flute")return DecideWildPokeFlute(state,command);
        if(command.Kind=="battle_medicine")return DecideWildMedicine(state,command);
        if(command.Kind=="battle_menu"&&command.Text is "bag" or "party")return new(CommandStatus.Accepted,state);
        WildBattleResult turn;PotionUseResult? potion=null;InventoryState? capturedInventory=null;bool reserves=HasWildReserves(state,b);
        if(command.Kind=="battle_move")turn=WildBattleRules.ResolveMove(BattlePokemon,b.State,command.MoveSlot,reserves);
        else if(command.Kind=="battle_menu"&&command.Text=="run")turn=WildBattleRules.Run(BattlePokemon,b.State,reserves);
        else if(command.Kind=="battle_potion")
        {
            if(_content.OpeningItems is null||state.Inventory is null||command.PartyIndex!=b.ActivePartyIndex)return new(CommandStatus.Rejected,state,"wild_item_context_invalid");
            potion=UseCampaignPotion(state.Inventory,WildBattleTransformationRules.PersistentPokemon(b.State.Player),PotionUseContext.Battle);
            if(!potion.Succeeded)return new(CommandStatus.Rejected,state,potion.Error);
            turn=WildBattleRules.ResolvePotion(BattlePokemon,b.State,potion.Pokemon,reserves);
        }
        else if(command.Kind=="battle_ball")
        {
            bool great=command.Text=="pret:ITEM_GREAT_BALL"&&_content.CeladonApproachEnabled;
            if(!great&&command.Text is not (null or "pret:ITEM_POKE_BALL"))return new(CommandStatus.Rejected,state,"capture_ball_unsupported");
            if(!_content.CaptureEnabled||_content.QuestItems is not {} items||state.Inventory is null||!state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))return new(CommandStatus.Rejected,state,"capture_not_enabled");
            var capacityError=CaptureCapacityError(state);if(capacityError is not null)return new(CommandStatus.Rejected,state,capacityError);
            if(state.NextPokemonId is <2 or long.MaxValue||long.MaxValue-Revision<256)return new(CommandStatus.Rejected,state,"capture_counter_headroom_required");
            if(great)
            {
                var inventoryError=ValidateCampaignInventory(state.Inventory);
                if(inventoryError is not null)return new(CommandStatus.Rejected,state,inventoryError);
                var balls=state.Inventory.Balls??[];int index=-1;
                for(int n=0;n<balls.Length;n++)if(balls[n].Item==SsAnneItemRules.GreatBallId){index=n;break;}
                if(index<0)return new(CommandStatus.Rejected,state,"capture_no_great_ball");
                var stack=balls[index];
                capturedInventory=state.Inventory with{Balls=stack.Quantity==1?balls.RemoveAt(index):balls.SetItem(index,stack with{Quantity=stack.Quantity-1})};
            }
            else
            {
                var spend=QuestChange(items,state.Inventory,QuestItemRules.PokeBallSymbol,1,false);
                if(!spend.Succeeded)return new(CommandStatus.Rejected,state,"capture_no_poke_ball");
                capturedInventory=spend.State;
            }
            turn=WildBattleRules.ResolveBall(BattlePokemon,b.State,great?CaptureBall.Great:CaptureBall.Poke,reserves);
        }
        else return new(CommandStatus.Rejected,state,"wild_battle_active");
        if(!turn.Succeeded)return new(CommandStatus.Rejected,state,turn.Fault);
        b=RememberKnockedOff(b,state.Party[b.ActivePartyIndex].InstanceId,turn.State!.Player);
        var nextBattle=b with{State=turn.State!,Events=turn.Events,EventIndex=0,Phase=CampaignBattlePhase.Events,RandomTrace=turn.Trace};
        return new(CommandStatus.Accepted,PrepareWildPresentation(PenalizeWildFaint(state with{WildBattle=nextBattle,Inventory=capturedInventory??potion?.Inventory??state.Inventory,Party=state.Party.SetItem(b.ActivePartyIndex,state.Party[b.ActivePartyIndex] with{Pokemon=WildBattleTransformationRules.PersistentPokemon(turn.State!.Player),Status=WildBattleTransformationRules.PersistentStatus(turn.State.Player),HeldItem=OwnedBattleHeldItem(turn.State.Player)}),World=state.World with{Rng=turn.State!.Rng}})));
    }
    private CampaignState PrepareWildPresentation(CampaignState state)
    {
        var b=state.WildBattle!;
        while(b.EventIndex<b.Events.Length)
        {
            var e=b.Events[b.EventIndex];
            if(HiddenEarlyPartyEvent(e)){b=b with{EventIndex=b.EventIndex+1};continue;}
            if(e is WildBattleStatusChangedEvent {Reason:"sleep-countdown"} or WildBattleLeechSeedChangedEvent {Reason:not "move"}){b=b with{EventIndex=b.EventIndex+1};continue;}
            if(e is WildBattleOrderEvent or WildBattleTurnReadyEvent or WildBattleEndedEvent or WildBattleDefenseCurledEvent || e is WildBattleBindingChangedEvent binding && binding.Reason is not("bind" or "wrap" or "expired")){b=b with{EventIndex=b.EventIndex+1};continue;}
            void Update(WildBattleSide side,Func<PokemonInstance,PokemonInstance> update){b=side==WildBattleSide.Player?b with{DisplayPlayer=update(b.DisplayPlayer)}:b with{DisplayOpponent=update(b.DisplayOpponent)};}
            switch(e)
            {
                case WildBattlePpCommandEvent pp:Update(pp.Actor,p=>p with{Moves=p.Moves.SetItem(pp.PpSlot,p.Moves[pp.PpSlot] with{CurrentPp=pp.After})});break;
                case WildBattleGrudgePpEvent grudge:Update(grudge.Attacker,p=>p with{Moves=p.Moves.SetItem(grudge.ChosenSlot,p.Moves[grudge.ChosenSlot] with{CurrentPp=0})});break;
                case WildBattleWeatherDamageEvent weather:Update(weather.Side,p=>p with{CurrentHp=weather.HpAfter});break;
                case WildBattleDrainEvent drain:Update(drain.Actor,p=>p with{CurrentHp=drain.HpAfter});break;
                case WildBattleTransformedEvent transformed:b=ApplyTransformDisplay(b,transformed);break;
                case WildBattlePlayerSwitchedEvent changed:b=b with{DisplayPlayer=BattleDisplayPokemon(changed.Replacement),DisplayPlayerAppearance=null};break;
                case WildBattleMoveUsedEvent used when used.Slot>=0:Update(used.Actor,p=>p with{Moves=p.Moves.SetItem(used.Slot,p.Moves[used.Slot] with{CurrentPp=used.PpAfter})});break;
                case WildBattleTowerHpEvent towerHp:Update(towerHp.Side,p=>p with{CurrentHp=towerHp.HpAfter});break;
                case WildBattleSpiteEvent spite:Update(spite.Target,p=>p with{Moves=p.Moves.SetItem(spite.Slot,p.Moves[spite.Slot] with{CurrentPp=spite.PpAfter})});break;
                case WildBattleHpChangedEvent hp:Update(hp.Target,p=>p with{CurrentHp=hp.HpAfter});break;
                case WildBattleHeldItemConsumedEvent item:Update(item.Side,p=>p with{CurrentHp=item.HpAfter});break;
                case WildBattleIngrainHealEvent roots:Update(roots.Side,p=>p with{CurrentHp=roots.HpAfter});break;
                case WildBattleRecoveredEvent recovered:Update(recovered.Side,p=>p with{CurrentHp=recovered.HpAfter});break;
                case WildBattlePotionRecoveredEvent potion:Update(potion.Side,p=>p with{CurrentHp=potion.HpAfter});break;
                case WildBattleMedicineAppliedEvent {ActiveTarget:true} medicine:Update(WildBattleSide.Player,p=>p with{CurrentHp=medicine.HpAfter});break;
                case WildBattleRecoilEvent recoil:Update(recoil.Actor,p=>p with{CurrentHp=recoil.HpAfter});break;
                case WildBattleConfusionSelfHitEvent self:Update(self.Side,p=>p with{CurrentHp=self.HpAfter});break;
                case WildBattleLeechSeedDrainEvent seed:Update(seed.DrainedSide,p=>p with{CurrentHp=seed.HpAfter});Update(seed.ReceivingSide,p=>p with{CurrentHp=seed.ReceiverHpAfter});break;
                case WildBattleBindingDamageEvent bind:Update(bind.Side,p=>p with{CurrentHp=bind.HpAfter});break;
                case WildBattleStatusDamageEvent status:Update(status.Side,p=>p with{CurrentHp=status.HpAfter});break;
            }
            return state with{WildBattle=b};
        }
        if(b.State.Outcome==WildBattleOutcome.Ongoing)return state with{WildBattle=b with{Phase=CampaignBattlePhase.ChooseMove,Replacement=b.State.CalledParty?.PendingSwitch is{Kind:WildBattleCalledSwitchKind.BatonPass}?CampaignReplacementStage.Select:b.State.AwaitingReplacement?b.Replacement??CampaignReplacementStage.UseNext:null,DisplayPlayer=BattleDisplayPokemon(b.State.Player),DisplayOpponent=BattleDisplayPokemon(b.State.Opponent),DisplayPlayerAppearance=BattleAppearance(b.State.Player),DisplayOpponentAppearance=BattleAppearance(b.State.Opponent)}};
        if(b.State.Outcome==WildBattleOutcome.Caught)return BeginCapturePresentation(state with{WildBattle=b});
        return SettleWildBattle(state with{WildBattle=b});
    }
    private CampaignState SettleWildBattle(CampaignState state)
    {
        var b=state.WildBattle!;var exploration=state.Exploration!;
        if(exploration.Battles.Any(x=>x.Id==b.Id))throw new InvalidDataException("wild_settlement_duplicate");
        if(_content.PartyTrainingEnabled&&b.State.Outcome==WildBattleOutcome.Won)return SettleWildTrainingVictory(state);
        WildVictoryRewardResult? reward=null;var party=state.Party;var pages=ImmutableArray.CreateBuilder<CampaignWildPage>();
        // AdjustFriendshipOnBattleFaint: Route 1 opponents are at most level 5, so the
        // imported local starter always uses FAINT_SMALL (-1 at every friendship tier).
        // This shares the settlement ledger: presentation ACK retries cannot debit twice.
        if(!_content.CaptureEnabled&&b.State.Player.Pokemon.CurrentHp==0)
        {
            var owned=party[0];party=party.SetItem(0,owned with{Pokemon=owned.Pokemon with{Friendship=Math.Max(0,owned.Pokemon.Friendship-1)}});
        }
        if(b.State.Outcome==WildBattleOutcome.Won)
        {
            reward=WildVictoryRewards.Apply(BattlePokemon,party[b.ActivePartyIndex],b.State.Opponent.Pokemon,_content.MapDefinition(state.World.Player.MapId).RegionMapSection);
            party=party.SetItem(b.ActivePartyIndex,reward.Pokemon);
            foreach(var e in reward.Events)
            {
                if(e is WildRewardExperienceEvent xp)pages.Add(new($"{reward.Pokemon.Nickname} gained\n{xp.Gained} EXP. Points!",e));
                if(e is WildRewardLevelEvent level){pages.Add(new($"{reward.Pokemon.Nickname} grew to\nlevel {level.After}!",e));pages.Add(new("",e));}
                if(e is WildRewardMoveLearnedEvent learned)pages.Add(new($"{reward.Pokemon.Nickname} learned\n{BattleContent.MoveNames[learned.Move.Value]}!",e));
            }
        }
        else if(b.State.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw)
        {
            pages.Add(new($"{state.PlayerName} is out of usable\nPOKéMON!"));
            int lost=WhiteOutLoss(state);
            pages.Add(new(state.Money==0?$"{state.PlayerName} whited out!":$"{state.PlayerName} panicked and lost\n{lost}...\n{state.PlayerName} whited out!"));
            foreach(var page in _content.TextPages(HasCenterRespawn(state)?"gText_PlayerScurriedToCenter":"gText_PlayerScurriedBackHome"))pages.Add(new(page.Replace("{PLAYER}",state.PlayerName,StringComparison.Ordinal)));
        }
        var history=new CampaignWildHistory(b.Id,WildBattleTransformationRules.PersistentPokemon(b.State.Opponent).SourceSpeciesId,b.State.Opponent.Pokemon.Level,b.State.Outcome,reward?.ExperienceGained??0){ScriptedSnorlaxRoute=b.ScriptedSnorlaxRoute,ScriptedMarowak=b.ScriptedMarowak};
        var next=state with{Party=party,Exploration=exploration with{Battles=exploration.Battles.Add(history)},WildBattle=b with{Reward=reward,Phase=CampaignBattlePhase.Results,Pages=pages.ToImmutable(),PageIndex=0,PendingMoves=reward?.PendingMoves??[]}};
        return pages.Count==0&&next.WildBattle!.PendingMoves.IsEmpty?ReturnFromWildBattle(next):next;
    }
    private CampaignState ReturnFromWildBattle(CampaignState state)
    {
        var b=state.WildBattle!;
        if(!state.Exploration!.Battles.Any(x=>x.Id==b.Id))throw new InvalidDataException("wild_not_settled");
        if(!b.PendingMoves.IsEmpty)throw new InvalidDataException("wild_learning_pending");
        if(b.State.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw)
        {
            // CB2_EndMarowakBattle sends defeat directly to CB2_WhiteOut. Its
            // suspended map script is abandoned, not resumed as a non-victory.
            // Random wild battles have no suspended caller to clear.
            var lost=b.ScriptedMarowak||b.ScriptedSnorlaxRoute!=0?state with{Dialogue=null,Motions=[],PresentationTicks=0,
                Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false}}:state;
            if(b.ScriptedSnorlaxRoute!=0)lost=lost with{Script=lost.Script with{Flags=lost.Script.Flags.Remove(SpecialWildFlag)}};
            return WhiteOutFromRoute1(lost);
        }
        if(b.ScriptedSnorlaxRoute!=0)return ReturnFromSnorlaxBattle(state,b);
        if(b.ScriptedMarowak)
        {
            if(state.Script.Wait is not {Kind:StoryWaitKind.Battle} wait)throw new InvalidDataException("tower_marowak_return_wait");
            var script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",b.State.Outcome==WildBattleOutcome.Won?0:1)};
            var next=state with{WildBattle=null,Dialogue=null,Music=_content.MapDefinition(state.World.Player.MapId).Music,Exploration=ResetEncounterModifiers(state.Exploration),Script=script};
            return Pump(Advance(next,StoryRules.Continue(script,wait.CorrelationId,_content)));
        }
        return state with{WildBattle=null,Music=_content.MapDefinition(state.World.Player.MapId).Music,Exploration=ResetEncounterModifiers(state.Exploration)};
    }
    private CampaignState WhiteOutFromRoute1(CampaignState state)
    {
        bool center=HasCenterRespawn(state);
        if((!center&&state.LastHealingLocation!="HEAL_LOCATION_PALLET_TOWN")||HasUnimplementedBadge(state))throw new InvalidDataException("whiteout_respawn_profile_not_supported");
        var flags=state.Script.Flags;
        foreach(var name in new[]{"FLAG_DEFEATED_LORELEI","FLAG_DEFEATED_BRUNO","FLAG_DEFEATED_AGATHA","FLAG_DEFEATED_LANCE","FLAG_DEFEATED_CHAMP","FLAG_SYS_ON_CYCLING_ROAD","FLAG_SYS_CRUISE_MODE","FLAG_SYS_SAFARI_MODE","FLAG_SYS_USE_STRENGTH","FLAG_SYS_FLASH_ACTIVE","FLAG_SYS_QL_DEPARTED"})flags=flags.SetItem(name,false);
        var vars=state.Script.Variables;
        foreach(var name in new[]{"VAR_MAP_SCENE_POKEMON_LEAGUE","VAR_MAP_SCENE_ROUTE16","VAR_MAP_SCENE_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE","VAR_QL_ENTRANCE"})vars=vars.SetItem(name,0);
        var trainers=state.BattledTrainers;
        foreach(var stage in new[]{"FIRST","REMATCH"})foreach(var species in new[]{"SQUIRTLE","BULBASAUR","CHARMANDER"})trainers=trainers.Remove("pret:TRAINER_CHAMPION_"+stage+"_"+species);
        int lost=WhiteOutLoss(state);
        int x=center?7:8,y=center?4:5;var map=_content.MapFromSourceSymbol(center?(state.LastHealingLocation=="HEAL_LOCATION_FUCHSIA_CITY"?"MAP_FUCHSIA_CITY_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_LAVENDER_TOWN"?"MAP_LAVENDER_TOWN_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_CELADON_CITY"?"MAP_CELADON_CITY_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_ROUTE10"?"MAP_ROUTE10_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_VERMILION_CITY"?"MAP_VERMILION_CITY_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_CERULEAN_CITY"?"MAP_CERULEAN_CITY_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_ROUTE4"?"MAP_ROUTE4_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_PEWTER_CITY"?"MAP_PEWTER_CITY_POKEMON_CENTER_1F":"MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F"):"MAP_PALLET_TOWN_PLAYERS_HOUSE_1F");var tile=_content.Maps[map].GetTile(x,y)??throw new InvalidDataException("whiteout_spawn_missing");
        var next=state with{WildBattle=null,Money=state.Money-lost,Party=state.Party.Select(p=>Game.Domain.FirstBattle.LabBattleRewards.Heal(BattlePokemon,p)).ToImmutableArray(),BattledTrainers=trainers,
            World=state.World with{Player=new(map,x,y,tile.Elevation,Direction.North)},Script=state.Script with{Flags=flags,Variables=vars},Exploration=ResetEncounterModifiers(state.Exploration!)};
        next=Pump(EnterMap(next,Direction.North));
        next=next with{LastTalked=center?HealingNurse(map).LocalId:"LOCALID_MOM",Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_LAST_TALKED",center?HealingNurse(map).SourceLocalId:1)}};
        return Pump(StartStory(next,center?"EventScript_AfterWhiteOutHeal":"EventScript_AfterWhiteOutMomHeal"));
    }
}
