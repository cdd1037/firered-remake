using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Battle;
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
public sealed record CampaignWildPage(string Text,WildRewardEvent? Reward=null,int? PartyIndex=null);
public sealed record CampaignWildBattle(string Id,PartyPokemon OwnedBefore,SourceBattleFrame Frame,
    ImmutableArray<WildBattleEvent> Events,int EventIndex,PokemonInstance DisplayPlayer,PokemonInstance DisplayOpponent,
    CampaignBattlePhase Phase,ImmutableArray<CampaignWildPage> Pages,int PageIndex,
    ImmutableArray<RandomTrace> RandomTrace)
{
    private SourceBattleFrame _frame=Frame??throw new InvalidDataException("canonical_battle_frame_required");
    public SourceBattleFrame Frame
    {
        get=>_frame;
        init=>_frame=value??throw new InvalidDataException("canonical_battle_frame_required");
    }
    [JsonIgnore] public WildBattleState State=>CampaignWildReadProjection.From(Frame);
    [JsonIgnore] public ImmutableArray<WildPendingMove> PendingMoves=>Frame.Rewards?.Pending?.Moves??[];
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public ImmutableArray<int> EntryLevels{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int ExperienceGained{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public ImmutableArray<SourceBattleEvent> SourceEvents{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignWildAppearance? DisplayPlayerAppearance{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignWildAppearance? DisplayOpponentAppearance{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int ActivePartyIndex{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignWildOrigin? Origin{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignPendingCapture? Capture{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignReplacementStage? Replacement{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableSortedSet<long>? FaintedMembers{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? OpponentHpAtLastSwitch{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignVoluntarySwitch? LastSwitch{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool PostBattleItemsSettled{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool ScriptedMarowak{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int ScriptedSnorlaxRoute{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignLostelleHypnoReceipt? LostelleHypno{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignCelioRoamerEncounter? CelioRoamer{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSourceTableReceipt? SourceTableReceipt{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSeafoamReceipt? SeafoamReceipt{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignPowerPlantReceipt? PowerPlantReceipt{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignEventIslandReceipt? EventIslandReceipt{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMewtwoReceipt? MewtwoReceipt{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignCelioRoamerSettlement? CelioRoamerSettlement{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<EncounterRandomTrace>? CelioRoamerEncounterTrace{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CampaignSevenIslandWildReceipt? SevenIslandReceipt { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignRockSmashReceipt? RockSmashReceipt{get;init;}
    [JsonIgnore] public int RewardPartyIndex=>(PageIndex<Pages.Length?Pages[PageIndex].PartyIndex:null)??Frame.Rewards?.Pending?.Recipient.Index??ActivePartyIndex;
}

public sealed partial class CampaignSession
{
    private CampaignState BeginWildBattle(CampaignState state,Route1WildPokemonResult? fishingPokemon=null,bool scriptedMarowak=false,int scriptedSnorlaxRoute=0,Route1WildPokemonResult? surfPokemon=null,CampaignLostelleHypnoReceipt? lostelleHypno=null,VictoryRoadWildOrigin? victoryRoadOrigin=null,SeviiRubyWildOrigin? seviiRubyOrigin=null,FourIslandWildOrigin? fourIslandOrigin=null,SixIslandWildOrigin? sixIslandOrigin=null,FiveIslandWildOrigin? fiveIslandOrigin=null,CeruleanCaveWildOrigin? ceruleanCaveOrigin=null,Route1WildPokemonResult? scriptedPokemon=null,CampaignMewtwoReceipt? mewtwoReceipt=null,SevenIslandWildOrigin? sevenIslandOrigin=null,Game.Domain.Randomness.RngState? sevenIslandGenerationRng=null,CampaignEventIslandReceipt? eventIslandReceipt=null,PowerPlantWildOrigin? powerPlantOrigin=null,CampaignPowerPlantReceipt? powerPlantReceipt=null,FishingEncounterOrigin? fishingOrigin=null,SeafoamWildOrigin? seafoamOrigin=null,CampaignSeafoamReceipt? seafoamReceipt=null,RngState? seafoamGenerationRng=null,SourceTableWildOrigin? sourceTableOrigin=null,RngState? sourceTableGenerationRng=null,CampaignRockSmashReceipt? rockSmashReceipt=null)
    {
        if(_content.ForestEnabled&&long.MaxValue-Revision<4096)throw new InvalidDataException("wild_battle_command_headroom_required");
        state=ResetFieldPoisonCounterForBattle(state);
        var exploration=state.Exploration??throw new InvalidDataException("exploration_missing");
        if(exploration.NextWildBattleId is <1 or long.MaxValue||state.WildBattle is not null||state.Battle is not null||state.Party.IsEmpty||state.Party.Length>6||(!_content.CaptureEnabled&&state.Party.Length!=1))throw new InvalidDataException("wild_battle_context_invalid");
        int active=_content.CaptureEnabled?Enumerable.Range(0,state.Party.Length).First(i=>!state.Party[i].IsEgg&&state.Party[i].Pokemon.CurrentHp>0):0;
        var owned=state.Party[active];
        if(!HasSupportedCampaignOriginalTrainer(state,owned))throw new InvalidDataException("wild_battle_owned_origin_required");
        Route1WildPokemonResult generated;
        if(scriptedPokemon is not null)generated=scriptedPokemon;
        else if(surfPokemon is not null)generated=surfPokemon;
        else if(fishingPokemon is not null)generated=fishingPokemon;
        else if(_content.ForestEnabled)
        {
            var early=exploration.EarlyEncounter??throw new InvalidDataException("early_encounter_missing");var table=_content.LandEncounters![state.World.Player.MapId];
            var result=GenerateCurrentLand(state,table,early);
            generated=new(result.Pokemon,result.GlobalRng,result.RandomTrace);
            if(IsSourceTableMap(table.Map))
                sourceTableOrigin=new(table.Map,SourceTableEncounterKind.Land,early.Pending!.Candidate.Slot);
            if(IsSeafoamMap(table.Map))
                seafoamOrigin=new(table.Map,SeafoamEncounterKind.Land,early.Pending!.Candidate.Slot);
            if(table.Map==PowerPlantEncounterTables.Land.Map)
            {
                powerPlantOrigin=new(table.Map,PowerPlantEncounterKind.Land,early.Pending!.Candidate.Slot);
                powerPlantReceipt=new(powerPlantOrigin,state.World.Rng,generated.GlobalRng,generated.Pokemon);
            }
            if(SevenIslandEncounterTables.Land.Any(t=>t.Map==table.Map))
                sevenIslandOrigin=new(table.Map,SevenIslandEncounterKind.Land,early.Pending!.Candidate.Slot);
            if(CeruleanCaveEncounterTables.Land.Any(t=>t.Map==table.Map))
                ceruleanCaveOrigin=new(table.Map,CeruleanCaveEncounterKind.Land,early.Pending!.Candidate.Slot);
            if(_content.FiveIslandEnabled&&FiveIslandEncounterTables.Land.Any(t=>t.Map==table.Map))
                fiveIslandOrigin=new(table.Map,FiveIslandEncounterKind.Land,early.Pending!.Candidate.Slot);
            if(_content.SixIslandEnabled&&SixIslandEncounterTables.Land.Any(t=>t.Map==table.Map))
                sixIslandOrigin=new(table.Map,SixIslandEncounterKind.Land,early.Pending!.Candidate.Slot);
            if(_content.FourIslandEnabled&&FourIslandEncounterTables.Land.Any(t=>t.Map==table.Map))
                fourIslandOrigin=new(table.Map,FourIslandEncounterKind.Land,early.Pending!.Candidate.Slot);
            if(_content.SeviiRubyEnabled&&SeviiRubyEncounterTables.Land.Any(t=>t.Map==table.Map))
                seviiRubyOrigin=new(table.Map,SeviiRubyEncounterKind.Land,early.Pending!.Candidate.Slot);
            if(_content.VictoryRoadEnabled&&VictoryRoadEncounterTables.Land.Any(t=>t.Map==table.Map))
                victoryRoadOrigin=new(table.Map,VictoryRoadEncounterKind.Land,early.Pending!.Candidate.Slot);
        }
        else generated=Route1WildPokemonFactory.Generate(OrdinaryEncounterPokemon,exploration.Encounter!,state.World.Rng,state.TrainerId);
        if(victoryRoadOrigin is not null&&(!_content.VictoryRoadEnabled||victoryRoadOrigin.Map!=state.World.Player.MapId||victoryRoadOrigin.Kind is VictoryRoadEncounterKind.GoodRod or VictoryRoadEncounterKind.SuperRod||
            victoryRoadOrigin.Kind!=(surfPokemon is not null?VictoryRoadEncounterKind.Surf:fishingPokemon is not null?VictoryRoadEncounterKind.OldRod:VictoryRoadEncounterKind.Land)))
            throw new InvalidDataException("victory_road_wild_field_origin");
        seafoamReceipt=PrepareSeafoamReceipt(state,generated,seafoamOrigin,seafoamReceipt,seafoamGenerationRng,
            scriptedPokemon is not null,surfPokemon is not null,fishingPokemon is not null,fishingOrigin);
        var sourceTableReceipt=PrepareSourceTableReceipt(state,generated,sourceTableOrigin,sourceTableGenerationRng,
            scriptedPokemon is not null,surfPokemon is not null,fishingPokemon is not null,fishingOrigin,scriptedSnorlax:scriptedSnorlaxRoute==16,rockSmash:rockSmashReceipt is not null);
        bool towerBattle=_content.PokemonTowerEnabled&&TowerMap(state.World.Player.MapId.Value)&&fishingPokemon is null||scriptedMarowak;
        var kind=PokemonTowerBattleContract.Classify(scriptedMarowak,HasSilphScope(state));
        var encounterKind=surfPokemon is not null?FirstSeviiEncounterKind.Surf:fishingPokemon is not null?FirstSeviiEncounterKind.OldRod:FirstSeviiEncounterKind.Land;
        if(lostelleHypno is not null||scriptedSnorlaxRoute!=0||scriptedMarowak)encounterKind=FirstSeviiEncounterKind.Land;
        var encounter=CanonicalWildEncounter(state,generated.Pokemon,encounterKind,victoryRoadOrigin,seviiRubyOrigin,
            fourIslandOrigin,sixIslandOrigin,fiveIslandOrigin,ceruleanCaveOrigin,sevenIslandOrigin,eventIslandReceipt?.Origin,powerPlantOrigin,lostelleHypno,towerBattle?kind:null,scriptedSnorlaxRoute!=0,fishingOrigin,seafoamOrigin,sourceTableOrigin,rockSmashReceipt?.Origin);
        var started=SourceWildBattlePolicy.StartOwned(SafariOrdinaryOwnership??throw new InvalidDataException("canonical_wild_owned_catalog"),
            SourceTrainerEntryParty(state),active,new(generated.Pokemon),generated.GlobalRng,encounter,CampaignBattleTerrain(state),encounterKind==FirstSeviiEncounterKind.OldRod&&fishingOrigin is null&&seafoamOrigin?.Kind!=SeafoamEncounterKind.GoodRod);
        if(!started.Succeeded)throw new InvalidDataException(started.Fault);
        var frame=PrepareCanonicalWildFrame(state,started.State!);
        var battle=CampaignWildReadProjection.From(frame);
        var b=new CampaignWildBattle("wild-battle:"+exploration.NextWildBattleId,owned,frame,ProjectCanonicalWildEvents(started.Events),0,battle.Player.Pokemon,battle.Opponent.Pokemon,CampaignBattlePhase.Events,[],0,[..generated.RandomTrace,..started.Trace])
        {RockSmashReceipt=rockSmashReceipt,SourceTableReceipt=sourceTableReceipt,SeafoamReceipt=seafoamReceipt,PowerPlantReceipt=powerPlantReceipt,EventIslandReceipt=eventIslandReceipt,SevenIslandReceipt=sevenIslandOrigin is null?null:new(sevenIslandGenerationRng??state.World.Rng,generated.GlobalRng,generated.Pokemon),MewtwoReceipt=mewtwoReceipt,SourceEvents=started.Events,EntryLevels=state.Party.Select(p=>p.Pokemon.Level).ToImmutableArray(),LostelleHypno=lostelleHypno,ScriptedSnorlaxRoute=scriptedSnorlaxRoute,ScriptedMarowak=scriptedMarowak,ActivePartyIndex=active,Origin=_content.CaptureEnabled?new(_content.MapDefinition(state.World.Player.MapId).RegionMapSection,generated.Pokemon.Level,_content.PokemonContent!.SpeciesNames[generated.Pokemon.SourceSpeciesId]):null,FaintedMembers=state.Party.Where(p=>p.Pokemon.CurrentHp==0).Select(p=>p.InstanceId).ToImmutableSortedSet(),OpponentHpAtLastSwitch=battle.Opponent.Pokemon.CurrentHp};
        if(fishingPokemon is null&&surfPokemon is null&&scriptedPokemon is null)exploration=_content.ForestEnabled?exploration with{EarlyEncounter=CompleteCurrentLandCandidate(exploration.EarlyEncounter!,state.World.Player.MapId)}:exploration with{Encounter=Route1EncounterRules.CompleteCandidate(exploration.Encounter!)};
        return PrepareWildPresentation(state with{WildBattle=b,Exploration=exploration with{NextWildBattleId=checked(exploration.NextWildBattleId+1)},World=state.World with{Rng=battle.Rng},UnownPokedexPersonality=generated.Pokemon.SourceSpeciesId==201&&!state.SeenSpecies.Contains(201)?generated.Pokemon.Personality:state.UnownPokedexPersonality,SeenSpecies=towerBattle&&!PokemonTowerBattleContract.Policy(kind).RegisterSeen?state.SeenSpecies:state.SeenSpecies.Add(battle.Opponent.Pokemon.SourceSpeciesId),Motions=[],PresentationTicks=0});
    }
    private Decision<CampaignState> DecideWildBattle(CampaignState state,CampaignCommand command)
    {
        var b=state.WildBattle!;
        if(b.Capture is not null)return DecideCapture(state,command);
        if(b.Phase==CampaignBattlePhase.ChooseMove&&b.Frame?.PendingSwitches.Any(p=>p.Kind==WildBattleCalledSwitchKind.BatonPass)==true)return DecideSourceWildPartyChoice(state,command);
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
            var learningFrame=b.Frame??throw new InvalidDataException("canonical_wild_learningFrame_required");
            var pending=learningFrame.Rewards?.Pending??throw new InvalidDataException("canonical_wild_learning_required");
            var move=pending.Moves[0];var owned=state.Party[pending.Recipient.Index];
            if(command.MoveSlot is < -1 or >3)return new(CommandStatus.Rejected,state,"wild_learning_slot_invalid");
            if(command.MoveSlot>=0&&command.MoveSlot<owned.Pokemon.Moves.Length&&PokemonMoveRules.IsHiddenMachineMove(owned.Pokemon.Moves[command.MoveSlot].Move))
                return new(CommandStatus.Rejected,state,"hm_move_cannot_be_forgotten");
            var learned=SourceFourRewards.Learn(CanonicalWildPokemon,learningFrame,WildContinuation(learningFrame),new(pending.Recipient,move.Level,move.Move,command.MoveSlot<0?null:command.MoveSlot));
            return CommitCanonicalWildResult(state with{WildBattle=b with{Pages=[],PageIndex=0}},learned);
        }
        if(b.Phase!=CampaignBattlePhase.ChooseMove)return new(CommandStatus.Rejected,state,"battle_presentation_pending");
        if(command.Kind=="battle_switch"&&_content.PartyTrainingEnabled)return DecideVoluntaryWildSwitch(state,command);
        if(command.Kind=="battle_poke_flute")return DecideWildPokeFlute(state,command);
        if(command.Kind=="battle_medicine")return DecideWildMedicine(state,command);
        if(command.Kind=="battle_menu"&&command.Text is "bag" or "party")return new(CommandStatus.Accepted,state);
        var frame=b.Frame??throw new InvalidDataException("canonical_wild_frame_required");
        var actor=frame.Get(new(0)).Occupant!.Value;
        SourceActionSelection selection;InventoryState? capturedInventory=null;
        if(command.Kind=="battle_move")selection=new(actor,SourceActionKind.Move,command.MoveSlot,new(1));
        else if(command.Kind=="battle_menu"&&command.Text=="run")selection=new(actor,SourceActionKind.Run);
        else if(command.Kind=="battle_potion")
        {
            if(command.PartyIndex!=b.ActivePartyIndex)return new(CommandStatus.Rejected,state,"wild_item_context_invalid");
            return DecideWildMedicine(state,command with{Kind="battle_medicine",Text="pret:ITEM_POTION",TargetInstanceId=state.Party[command.PartyIndex].InstanceId,MoveSlot=-1});
        }
        else if(command.Kind=="battle_ball")
        {
            var ball=command.Text switch
            {
                null or "pret:ITEM_POKE_BALL"=>CaptureBall.Poke,
                "pret:ITEM_GREAT_BALL" when _content.CeladonApproachEnabled=>CaptureBall.Great,
                "pret:ITEM_ULTRA_BALL" when _content.SilphEnabled=>CaptureBall.Ultra,
                "pret:ITEM_MASTER_BALL" when _content.SilphEnabled=>CaptureBall.Master,
                "pret:ITEM_LUXURY_BALL" when _content.LostCaveEnabled=>CaptureBall.Luxury,
                _=>(CaptureBall)0
            };
            if(ball==(CaptureBall)0)return new(CommandStatus.Rejected,state,"capture_ball_unsupported");
            if(!_content.CaptureEnabled||_content.QuestItems is not {} items||state.Inventory is null||!state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))return new(CommandStatus.Rejected,state,"capture_not_enabled");
            var capacityError=CaptureCapacityError(state);if(capacityError is not null)return new(CommandStatus.Rejected,state,capacityError);
            if(state.NextPokemonId is <2 or long.MaxValue||long.MaxValue-Revision<256)return new(CommandStatus.Rejected,state,"capture_counter_headroom_required");
            if(ball!=CaptureBall.Poke)
            {
                var inventoryError=ValidateCampaignInventory(state.Inventory);
                if(inventoryError is not null)return new(CommandStatus.Rejected,state,inventoryError);
                var ballItem=new ItemId("pret:"+CaptureBallReceipt(ball));
                var balls=state.Inventory.Balls??[];int index=-1;
                for(int n=0;n<balls.Length;n++)if(balls[n].Item==ballItem){index=n;break;}
                if(index<0)return new(CommandStatus.Rejected,state,ball switch{CaptureBall.Great=>"capture_no_great_ball",CaptureBall.Ultra=>"capture_no_ultra_ball",CaptureBall.Luxury=>"capture_no_luxury_ball",_=>"capture_no_master_ball"});
                var stack=balls[index];
                capturedInventory=state.Inventory with{Balls=stack.Quantity==1?balls.RemoveAt(index):balls.SetItem(index,stack with{Quantity=stack.Quantity-1})};
            }
            else
            {
                var spend=QuestChange(items,state.Inventory,QuestItemRules.PokeBallSymbol,1,false);
                if(!spend.Succeeded)return new(CommandStatus.Rejected,state,"capture_no_poke_ball");
                capturedInventory=spend.State;
            }
            selection=new(actor,SourceActionKind.Capture){Capture=new(ball,new(false,CanonicalWildPokemon.Species.Values.Where(p=>state.CaughtSpecies.Contains(p.SourceSpeciesId)).Select(p=>p.Id).ToImmutableHashSet(),frame.WildEncounter?.CelioRoamer is not null?CaptureEncounterKind.Roamer:CaptureEncounterKind.OrdinaryWild))};
        }
        else return new(CommandStatus.Rejected,state,"wild_battle_active");
        var turn=SourceWildBattlePolicy.BeginAction(CanonicalWildPokemon,frame,frame.Revision,selection);
        if(!turn.Succeeded)return new(CommandStatus.Rejected,state,turn.Fault);
        return CommitCanonicalWildResult(state with{Inventory=capturedInventory??state.Inventory},turn,resetTrace:true);
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
                case WildBattlePpCommandEvent pp when pp.PpSlot>=0:Update(pp.PpOwner,p=>p with{Moves=p.Moves.SetItem(pp.PpSlot,p.Moves[pp.PpSlot] with{CurrentPp=pp.After})});break;
                case WildBattleSnatchPressurePpEvent pp:Update(pp.Snatcher,p=>p with{Moves=p.Moves.SetItem(pp.Slot,p.Moves[pp.Slot] with{CurrentPp=pp.After})});break;
                case WildBattleGrudgePpEvent grudge:Update(grudge.Attacker,p=>p with{Moves=p.Moves.SetItem(grudge.ChosenSlot,p.Moves[grudge.ChosenSlot] with{CurrentPp=0})});break;
                case WildBattleWeatherDamageEvent weather:Update(weather.Side,p=>p with{CurrentHp=weather.HpAfter});break;
                case WildBattleDrainEvent drain:Update(drain.Actor,p=>p with{CurrentHp=drain.HpAfter});break;
                case WildBattleMoveCopiedEvent copied:Update(copied.Actor,p=>p with{Moves=p.Moves.SetItem(copied.MoveSlot,copied.Replacement)});break;
                case WildBattleTransformedEvent transformed:b=ApplyTransformDisplay(b,transformed);break;
                case WildBattlePlayerReplacedEvent replaced:b=b with{DisplayPlayer=BattleDisplayPokemon(replaced.Replacement),DisplayPlayerAppearance=null};break;
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
                case WildBattleAbilityDamageEvent ability:Update(ability.Target,p=>p with{CurrentHp=ability.HpAfter});break;
                case WildBattleConfusionSelfHitEvent self:Update(self.Side,p=>p with{CurrentHp=self.HpAfter});break;
                case WildBattleLeechSeedDrainEvent seed:Update(seed.DrainedSide,p=>p with{CurrentHp=seed.HpAfter});Update(seed.ReceivingSide,p=>p with{CurrentHp=seed.ReceiverHpAfter});break;
                case WildBattleBindingDamageEvent bind:Update(bind.Side,p=>p with{CurrentHp=bind.HpAfter});break;
                case WildBattleStatusDamageEvent status:Update(status.Side,p=>p with{CurrentHp=status.HpAfter});break;
            }
            return state with{WildBattle=b};
        }
        return ContinueCanonicalWild(state with{WildBattle=b});
    }
    private CampaignState SettleWildBattle(CampaignState state)
    {
        var b=state.WildBattle!;var exploration=state.Exploration!;
        if(exploration.Battles.Any(x=>x.Id==b.Id))throw new InvalidDataException("wild_settlement_duplicate");
        var party=state.Party;var pages=ImmutableArray.CreateBuilder<CampaignWildPage>();
        if(b.Frame!.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw)
        {
            pages.Add(new($"{state.PlayerName} is out of usable\nPOKéMON!"));
            int lost=WhiteOutLoss(state);
            pages.Add(new(state.Money==0?$"{state.PlayerName} whited out!":$"{state.PlayerName} panicked and lost\n{lost}...\n{state.PlayerName} whited out!"));
            foreach(var page in _content.TextPages(HasCenterRespawn(state)?"gText_PlayerScurriedToCenter":"gText_PlayerScurriedBackHome"))pages.Add(new(page.Replace("{PLAYER}",state.PlayerName,StringComparison.Ordinal)));
        }
        var history=new CampaignWildHistory(b.Id,b.Frame!.Party(BattleSide.Opponent).Members[0].Pokemon.SourceSpeciesId,b.Frame.Party(BattleSide.Opponent).Members[0].Pokemon.Level,b.Frame!.Outcome,b.ExperienceGained){RockSmashReceipt=b.RockSmashReceipt,SourceTableReceipt=b.SourceTableReceipt,SeafoamReceipt=b.SeafoamReceipt,Fishing=b.Frame.WildEncounter?.Fishing,PowerPlantReceipt=b.PowerPlantReceipt,EventIslandReceipt=b.EventIslandReceipt,SevenIsland=b.Frame.WildEncounter?.SevenIsland,SevenIslandReceipt=b.SevenIslandReceipt,MewtwoReceipt=b.MewtwoReceipt,CeruleanCaveOrigin=b.Frame.WildEncounter?.CeruleanCave,CelioRoamer=b.CelioRoamerSettlement,FiveIsland=b.Frame!.WildEncounter?.FiveIsland,SixIsland=b.Frame!.WildEncounter?.SixIsland,FourIsland=b.Frame!.WildEncounter?.FourIsland,SeviiRuby=b.Frame!.WildEncounter?.SeviiRuby,VictoryRoad=b.Frame!.WildEncounter?.VictoryRoad,LostelleHypno=b.LostelleHypno,ScriptedSnorlaxRoute=b.ScriptedSnorlaxRoute,ScriptedMarowak=b.ScriptedMarowak};
        var next=state with{Party=party,Exploration=exploration with{Battles=exploration.Battles.Add(history)},WildBattle=b with{Phase=CampaignBattlePhase.Results,Pages=pages.ToImmutable(),PageIndex=0}};
        return pages.Count==0?CompleteWildRewardReceiver(next):next;
    }
    private CampaignState ReturnFromWildBattle(CampaignState state)
    {
        var b=state.WildBattle!;
        if(!state.Exploration!.Battles.Any(x=>x.Id==b.Id))throw new InvalidDataException("wild_not_settled");
        if(!b.PendingMoves.IsEmpty)throw new InvalidDataException("wild_learning_pending");
        if(b.Frame!.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw)
        {
            // CB2_EndMarowakBattle sends defeat directly to CB2_WhiteOut. Its
            // suspended map script is abandoned, not resumed as a non-victory.
            // Random wild battles have no suspended caller to clear.
            var lost=b.RockSmashReceipt is not null||b.LostelleHypno is not null||b.ScriptedMarowak||b.ScriptedSnorlaxRoute!=0||IsMewtwoBattle(b)||IsEventIslandBattle(b)||IsPowerPlantStaticBattle(b)||IsArticunoBattle(b)||IsSourceScriptedBattle(b)?state with{Dialogue=null,Motions=[],PresentationTicks=0,
                Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false}}:state;
            if(b.ScriptedSnorlaxRoute!=0||IsMewtwoBattle(b)||IsEventIslandBattle(b)||IsPowerPlantStaticBattle(b)||IsArticunoBattle(b)||IsSourceScriptedBattle(b))lost=lost with{Script=lost.Script with{Flags=lost.Script.Flags.Remove(SpecialWildFlag)}};
            if(IsSourceScriptedBattle(b))lost=lost with{SourceScriptedPreparation=null,SourceScriptedReturn=null};
            if(IsArticunoBattle(b))lost=lost with{ArticunoPreparation=null,ArticunoReturn=null};
            if(IsPowerPlantStaticBattle(b))lost=lost with{PowerPlantPreparation=null,PowerPlantReturn=null};
            return WhiteOutFromRoute1(lost);
        }
        if(IsSourceScriptedBattle(b))return ReturnFromSourceScriptedBattle(state,b);
        if(b.RockSmashReceipt is not null)return ReturnFromRockSmashBattle(state,b);
        if(IsArticunoBattle(b))return ReturnFromArticunoBattle(state,b);
        if(IsPowerPlantStaticBattle(b))return ReturnFromPowerPlantBattle(state,b);
        if(IsEventIslandBattle(b))return ReturnFromEventIslandBattle(state,b);
        if(IsMewtwoBattle(b))return ReturnFromMewtwoBattle(state,b);
        if(b.LostelleHypno is not null)return ReturnFromLostelleHypnoBattle(state,b);
        if(b.ScriptedSnorlaxRoute!=0)return ReturnFromSnorlaxBattle(state,b);
        if(b.ScriptedMarowak)
        {
            if(state.Script.Wait is not {Kind:StoryWaitKind.Battle} wait)throw new InvalidDataException("tower_marowak_return_wait");
            var script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",b.Frame!.Outcome==WildBattleOutcome.Won?0:1)};
            var next=state with{WildBattle=null,Dialogue=null,Music=FieldTravelMusic(state),Exploration=ResetEncounterModifiers(state.Exploration),Script=script};
            return Pump(Advance(next,StoryRules.Continue(script,wait.CorrelationId,_content)));
        }
        return state with{WildBattle=null,Music=FieldTravelMusic(state),Exploration=ResetEncounterModifiers(state.Exploration)};
    }
    private CampaignState WhiteOutFromRoute1(CampaignState state)
    {
        state=ResetPokemonLeagueForWhiteOut(state);
        bool center=HasCenterRespawn(state);
        if((!center&&state.LastHealingLocation!="HEAL_LOCATION_PALLET_TOWN")||HasUnimplementedBadge(state))throw new InvalidDataException("whiteout_respawn_profile_not_supported");
        var flags=state.Script.Flags;
        foreach(var name in new[]{"FLAG_DEFEATED_LORELEI","FLAG_DEFEATED_BRUNO","FLAG_DEFEATED_AGATHA","FLAG_DEFEATED_LANCE","FLAG_DEFEATED_CHAMP","FLAG_SYS_ON_CYCLING_ROAD","FLAG_SYS_CRUISE_MODE","FLAG_SYS_SAFARI_MODE","FLAG_SYS_USE_STRENGTH","FLAG_SYS_FLASH_ACTIVE","FLAG_SYS_QL_DEPARTED"})flags=flags.SetItem(name,false);
        var vars=state.Script.Variables;
        foreach(var name in new[]{"VAR_MAP_SCENE_POKEMON_LEAGUE","VAR_MAP_SCENE_ROUTE16","VAR_MAP_SCENE_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE","VAR_QL_ENTRANCE"})vars=vars.SetItem(name,0);
        var trainers=state.BattledTrainers;
        foreach(var stage in new[]{"FIRST","REMATCH"})foreach(var species in new[]{"SQUIRTLE","BULBASAUR","CHARMANDER"})trainers=trainers.Remove("pret:TRAINER_CHAMPION_"+stage+"_"+species);
        int lost=WhiteOutLoss(state);
        bool indigo=_content.PokemonLeagueEnabled&&state.LastHealingLocation==PokemonLeagueHealingLocation;
        bool oneIsland=_content.FirstSeviiEnabled&&FirstSeviiCenterSourceMap(state)=="MAP_ONE_ISLAND_POKEMON_CENTER_1F";
        int x=indigo?13:oneIsland?5:center?7:8,y=indigo?12:center?4:5;var map=_content.MapFromSourceSymbol(center?(indigo?"MAP_INDIGO_PLATEAU_POKEMON_CENTER_1F":FirstSeviiCenterSourceMap(state)??(state.LastHealingLocation=="HEAL_LOCATION_CINNABAR_ISLAND"?"MAP_CINNABAR_ISLAND_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_SAFFRON_CITY"?"MAP_SAFFRON_CITY_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_FUCHSIA_CITY"?"MAP_FUCHSIA_CITY_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_LAVENDER_TOWN"?"MAP_LAVENDER_TOWN_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_CELADON_CITY"?"MAP_CELADON_CITY_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_ROUTE10"?"MAP_ROUTE10_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_VERMILION_CITY"?"MAP_VERMILION_CITY_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_CERULEAN_CITY"?"MAP_CERULEAN_CITY_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_ROUTE4"?"MAP_ROUTE4_POKEMON_CENTER_1F":state.LastHealingLocation=="HEAL_LOCATION_PEWTER_CITY"?"MAP_PEWTER_CITY_POKEMON_CENTER_1F":"MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F")):"MAP_PALLET_TOWN_PLAYERS_HOUSE_1F");var tile=_content.Maps[map].GetTile(x,y)??throw new InvalidDataException("whiteout_spawn_missing");
        var next=state with{WildBattle=null,SafariVisit=null,SafariBattle=null,SafariExitPending=false,Money=state.Money-lost,Party=state.Party.Select(p=>Game.Domain.FirstBattle.LabBattleRewards.Heal(BattlePokemon,p)).ToImmutableArray(),BattledTrainers=trainers,
            World=state.World with{Player=new(map,x,y,tile.Elevation,Direction.North)},Script=state.Script with{Flags=flags,Variables=vars},Exploration=ResetEncounterModifiers(state.Exploration!)};
        next=Pump(EnterMap(next,Direction.North));
        next=next with{LastTalked=center?HealingNurse(map).LocalId:"LOCALID_MOM",Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_LAST_TALKED",center?HealingNurse(map).SourceLocalId:1)}};
        return Pump(StartStory(next,center?"EventScript_AfterWhiteOutHeal":"EventScript_AfterWhiteOutMomHeal"));
    }
}
