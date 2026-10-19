using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignTrainerTowerHistory(string Id, TrainerTowerChallengeKind Course, int FloorIndex,
    int TrainerIndex, int Level, WildBattleOutcome Outcome, ImmutableArray<int> OpponentsSeen);
public sealed record CampaignTrainerTowerMenu(string SourceSymbol, ImmutableArray<string> Options);

public sealed partial class CampaignSession
{
    private const string TrainerTowerLobby = "map.trainer-tower-lobby";
    private const string TrainerTowerShared = "data/scripts/trainer_tower.inc";
    private static bool IsTrainerTowerMap(MapId map) => map.Value.StartsWith("map.trainer-tower-", StringComparison.Ordinal);
    private static int TrainerTowerFloorNumber(MapId map) => map.Value == "map.trainer-tower-roof" ? 9 :
        map.Value.Length == "map.trainer-tower-1f".Length && map.Value.StartsWith("map.trainer-tower-", StringComparison.Ordinal) &&
        map.Value[^1] == 'f' && map.Value[^2] is >= '1' and <= '8' ? map.Value[^2] - '0' : 0;
    private TrainerTowerProgressState TowerProgress(CampaignState state) => state.TrainerTower ?? TrainerTowerProgressRules.Create();
    private TrainerTowerFloorTemplate TowerTemplate(CampaignState state)
    {
        int floor = TrainerTowerFloorNumber(state.World.Player.MapId);
        if (floor is < 1 or > 8) throw new InvalidDataException("trainer_tower_floor_required");
        var catalog = _content.TrainerTowerCatalog ?? throw new InvalidDataException("trainer_tower_catalog_missing");
        return catalog.Template(catalog.Course(TowerProgress(state).SelectedCourse).Floors[floor - 1].TemplateSymbol);
    }
    public TrainerTowerBattleFormat TrainerTowerLayout(MapId map) => TrainerTowerLayout(State, map);
    private TrainerTowerBattleFormat TrainerTowerLayout(CampaignState state, MapId map)
    {
        int floor = TrainerTowerFloorNumber(map);
        return floor is >= 1 and <= 8 ? _content.TrainerTowerCatalog!.Course(state.TrainerTower?.SelectedCourse ?? TrainerTowerChallengeKind.Single).Floors[floor - 1].Format : TrainerTowerBattleFormat.Single;
    }
    private IReadOnlyDictionary<MapId,IFieldMap> TrainerTowerMovementMaps(CampaignState state) =>
        _binding.TrainerTowerMaps[state.TrainerTower?.SelectedCourse ?? TrainerTowerChallengeKind.Single].Raw;
    private IReadOnlyDictionary<MapId,ConnectedFieldMap> TrainerTowerConnectedMaps(CampaignState state) =>
        _binding.TrainerTowerMaps[state.TrainerTower?.SelectedCourse ?? TrainerTowerChallengeKind.Single].Connected;

    private static ImmutableDictionary<TrainerTowerChallengeKind, CampaignTrainerTowerMaps> BindTrainerTowerMaps(ICampaignCatalog content)
    {
        var raw = content.Maps.ToImmutableDictionary();
        var connected = content.ConnectedFieldMaps!.ToImmutableDictionary();
        return Enum.GetValues<TrainerTowerChallengeKind>().ToImmutableDictionary(course => course, course =>
        {
            var rawCourse = raw; var connectedCourse = connected;
            foreach (var floor in content.TrainerTowerCatalog!.Course(course).Floors)
            {
                MapId id = new($"map.trainer-tower-{floor.Index + 1}f");
                if (!raw.ContainsKey(id) || !connected.ContainsKey(id))
                    throw new InvalidDataException("trainer_tower_map_missing");
                rawCourse = rawCourse.SetItem(id, content.TrainerTowerMap(id, floor.Format)
                    ?? throw new InvalidDataException("trainer_tower_map_missing"));
                connectedCourse = connectedCourse.SetItem(id, content.TrainerTowerConnectedMap(id, floor.Format)
                    ?? throw new InvalidDataException("trainer_tower_connected_map_missing"));
            }
            return new CampaignTrainerTowerMaps(rawCourse, connectedCourse);
        });
    }
    private Decision<CampaignState> DecideTrainerTowerTime(CampaignState state, CampaignCommand command)
    {
        if (command.Quantity is < 1 or > 3600 || command.Choice is not null || command.Text is not null)
            return new(CommandStatus.Rejected,state,"trainer_tower_elapsed_frames_invalid");
        if (state.TrainerTower is not {TimerRunning:true} tower) return new(CommandStatus.Accepted,state,"trainer_tower_clock_stopped");
        return new(CommandStatus.Accepted,state with { TrainerTower=TrainerTowerProgressRules.AdvanceElapsedFrames(tower,command.Quantity) });
    }
    private string? ValidateTrainerTowerProgress(CampaignState state)
    {
        if (ValidateTrainerTowerFieldSave(state) is {} fieldError) return fieldError;
        try { if (state.TrainerTower is {} tower) TrainerTowerProgressRules.ValidateStableSave(tower,_content.TrainerTowerCatalog!); }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException) { return "save_trainer_tower_progress"; }
        if (IsTrainerTowerMap(state.World.Player.MapId) && (!RainbowQuestComplete(state) || state.TrainerTower is null)) return "save_trainer_tower_access";
        return ValidateTrainerTowerHistory(state.TrainerTowerHistory);
    }

    private bool TryApplyTrainerTowerEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;        bool source=effect.Source.Path==TrainerTowerShared||effect.Source.Path.StartsWith("data/maps/TrainerTower_",StringComparison.Ordinal);
        if(!source)return false;
        if(TryApplyTrainerTowerElevatorEffect(state,effect,out next))return true;
        if(!IsTrainerTowerMap(state.World.Player.MapId))throw new InvalidDataException("trainer_tower_source_map");
        var tower=TowerProgress(state); next=state=state with{TrainerTower=tower};
        var a=effect.Args;
        int Value(string token)=>DepartmentValue(state,token);
        CampaignState Result(CampaignState s,int value)=>s with{Script=s.Script with{Variables=s.Script.Variables.SetItem("VAR_RESULT",value)}};
        string opcode=effect.Opcode;
        if(opcode=="warp")
        {
            bool loss=effect.Source.Path==TrainerTowerShared&&effect.Source.Symbol is "TrainerTower_EventScript_WarpToLobbyLost" or "TrainerTower_EventScript_WarpToLobby";
            bool lift=effect.Source.Path=="data/maps/TrainerTower_Elevator/scripts.inc"&&effect.Source.Symbol=="TrainerTower_Elevator_EventScript_SelectLobby";
            if(!(loss&&a.SequenceEqual(new[]{"MAP_TRAINER_TOWER_LOBBY","9","7"})||lift&&a.SequenceEqual(new[]{"MAP_TRAINER_TOWER_LOBBY","17","8"})))
                throw new InvalidDataException("trainer_tower_lobby_warp_source");
            next=EnterScriptWarp(state,new MapId(TrainerTowerLobby),loss?9:17,loss?7:8);
            return true;
        }
        if(opcode=="addvar")
        {
            string expected=effect.Source.Symbol switch{"TrainerTower_EventScript_DoDoubleBattle"=>"VAR_TEMP_3",
                "TrainerTower_EventScript_DoSecondKnockoutBattle" or "TrainerTower_EventScript_DoThirdKnockoutBattle"=>"VAR_TEMP_1",_=>""};
            if(effect.Source.Path!=TrainerTowerShared||a.Length!=2||expected.Length==0||a[0]!=expected||Value(a[1])!=1)
                throw new InvalidDataException("trainer_tower_counter_source");
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem(expected,unchecked((ushort)(Value(expected)+1)))}};return true;
        }
        if(opcode=="getplayerxy")
        {
            if(!a.SequenceEqual(new[]{"VAR_TEMP_D","VAR_RESULT"})||state.World.Player.MapId.Value!=TrainerTowerLobby)throw new InvalidDataException("trainer_tower_player_coordinates");
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem(a[0],state.World.Player.X).SetItem(a[1],state.World.Player.Y)}};return true;
        }
        if(opcode=="setobjectxy")
        {
            if(!a.SequenceEqual(new[]{"LOCALID_PLAYER","9","7"})||state.World.Player.MapId.Value!=TrainerTowerLobby)throw new InvalidDataException("trainer_tower_player_position");
            next=state with{World=state.World with{Player=state.World.Player with{X=9,Y=7}}};return true;
        }
        if(opcode=="setmetatile")
        {
            if(state.World.Player.MapId.Value!=TrainerTowerLobby||a.Length!=4||Value(a[0])!=17||Value(a[1])!=10||
                a[2] is not("METATILE_TrainerTower_Floor_ShadeBottomLeft" or "METATILE_TrainerTower_CounterBarrier")||
                Value(a[3])!=(a[2].EndsWith("CounterBarrier",StringComparison.Ordinal)?1:0))throw new InvalidDataException("trainer_tower_counter_metatile");
            var tile=_content.Maps[state.World.Player.MapId].GetTile(17,10)!;
            next=state with{MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SetItem("17:10",new(17,10,Value(a[2]),Value(a[3])*3,tile.Elevation))};return true;
        }
        if(opcode=="special"&&a[0]=="DrawWholeMapView")return true;
        if(opcode=="ttower_dobattle") { next=BeginTrainerTowerBattle(state,effect); return true; }
        if(opcode=="multichoice")
        {
            // Ordinary choices come from the same catalog definition as the interpreter and UI.
            // Facility side effects remain in the native instructions after the player's answer.
            if(StoryMenuRules.Pending(state.Script,_content) is null)
                throw new InvalidDataException("trainer_tower_menu_definition_missing");
            return true;
        }
        if(opcode=="special" && a[0]=="ShowBattleRecords")
        { next=state with{TrainerTowerRecordsVisible=true};return true; }
        if(opcode=="special"&&a[0]=="CallTrainerTowerFunc")
        {
            if(Value("VAR_0x8004")!=1)throw new InvalidDataException("trainer_tower_direct_native_id");
            opcode="TrainerTower:ttower_getspeech";a=["VAR_0x8005","VAR_0x8006"];
        }
        if(!opcode.StartsWith("TrainerTower:",StringComparison.Ordinal))return false;
        switch(opcode[13..])
        {
            case "ttower_initfloor":
                int floor=TrainerTowerFloorNumber(state.World.Player.MapId);
                if(floor==9){next=Result(state,3);return true;}
                var template=TowerTemplate(state);var format=TrainerTowerLayout(state,state.World.Player.MapId);
                next=Result(state with{Script=state.Script with{Variables=state.Script.Variables.SetItems(_content.TrainerTowerNpcGraphics(format,template))}},(int)format);return true;
            case "ttower_getspeech":
                int speech=Value(a[0]),trainer=a.Length>1?Value(a[1]):Value("VAR_0x8006");
                var t=TowerTemplate(state).Trainers[trainer];
                var words=speech switch{2=>t.SpeechBefore,3=>t.SpeechWin,4=>t.SpeechLose,5=>t.SpeechAfter,_=>throw new InvalidDataException("trainer_tower_speech_kind")};
                // The source Easy Chat formatter starts with a 3x2 grid; exact width-driven
                // 2x3 fallback remains a presentation concern, never a gameplay branch.
                string text=string.Join(" ",words.Take(3).Select(w=>w.Text))+"\n"+string.Join(" ",words.Skip(3).Select(w=>w.Text));
                var speechVariables=state.Script.Variables.SetItem("VAR_0x8005",speech).SetItem("VAR_0x8006",trainer);string speechColor=state.TextColor;
                if(speech!=5)
                {
                    int gender=_content.TrainerTowerTextGender(TowerTemplate(state),trainer,Value("VAR_TEMP_3")!=0);
                    speechVariables=speechVariables.SetItem("VAR_PREV_TEXT_COLOR",Value("VAR_TEXT_COLOR")).SetItem("VAR_TEXT_COLOR",gender);
                    speechColor=gender==0?"MALE":"FEMALE";
                }
                next=state with{TextBuffers=state.TextBuffers.SetItem("gStringVar4",text),TextColor=speechColor,
                    Script=state.Script with{Variables=speechVariables}};return true;
            case "ttower_getchallengetype":next=Result(state,(int)TrainerTowerLayout(state,state.World.Player.MapId));return true;
            case "ttower_clearedfloor":
                // Source ends without waitmovement after the final doubles/KO actor
                // motion. Keep its presentation acknowledgement before stable saves.
                next=state with{TrainerTower=TrainerTowerProgressRules.AddFloorCleared(tower),
                    Script=state.Motions.IsEmpty?state.Script:state.Script with{Wait=state.Script.Wait! with{Kind=StoryWaitKind.Presentation}}};return true;
            case "ttower_isfloorcleared":
                int n=TrainerTowerFloorNumber(state.World.Player.MapId);
                next=Result(state,n==0||TrainerTowerProgressRules.GetFloorAlreadyCleared(tower,n,n==9?8:TowerTemplate(state).EncodedFloorCount)?1:0);return true;
            case "ttower_startchallenge":next=state with{TrainerTower=TrainerTowerProgressRules.Start(tower,(TrainerTowerChallengeKind)Value("VAR_0x8005"))};return true;
            case "ttower_getownerstate":var owner=TrainerTowerProgressRules.GetOwnerState(tower);next=Result(state with{TrainerTower=owner.State},owner.Result);return true;
            case "ttower_giveprize":
                var prize=_content.TrainerTowerCatalog!.Course(tower.SelectedCourse).PrizeItem;
                bool added=false;var inventory=state.Inventory!;
                if(!tower.CurrentRecord.ReceivedPrize)
                { var change=EarlyInventoryAddRules.Add(ShopCatalog,inventory,new ItemId("pret:"+prize),1);added=change.Succeeded;if(added)inventory=change.State; }
                var given=TrainerTowerProgressRules.GiveChallengePrize(tower,prize,added);
                next=Result(state with{Inventory=inventory,TrainerTower=given.State,TextBuffers=state.TextBuffers.SetItem("STR_VAR_2",ShopCatalog.Items.Single(i=>i.SourceItemSymbol==prize).Name)},(int)given.Result);return true;
            case "ttower_checkfinaltime":var time=TrainerTowerProgressRules.CheckFinalTime(tower);next=Result(state with{TrainerTower=time.State},(int)time.Result);return true;
            case "ttower_resumetimer":next=state with{TrainerTower=TrainerTowerProgressRules.ResumeTimer(tower)};return true;
            case "ttower_setlost":next=state with{TrainerTower=TrainerTowerProgressRules.SetPlayerLost(tower)};return true;
            case "ttower_getchallengestatus":var status=TrainerTowerProgressRules.ConsumeChallengeStatus(tower);next=Result(state with{TrainerTower=status.State},(int)status.Status);return true;
            case "ttower_gettime":
                tower=TrainerTowerProgressRules.GetCurrentTime(tower);var shown=TrainerTowerProgressRules.FormatTime(tower.CurrentRecord.TimerFrames);
                next=state with{TrainerTower=tower,TextBuffers=state.TextBuffers.SetItem("STR_VAR_1",shown.Minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2))
                    .SetItem("STR_VAR_2",shown.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2)).SetItem("STR_VAR_3",shown.Centiseconds.ToString("00",CultureInfo.InvariantCulture))};return true;
            case "ttower_checkdoubles":next=Result(state,state.Party.Length==1?1:state.Party.Count(p=>!p.IsEgg&&p.Pokemon.CurrentHp>0)>=2?0:2);return true;
            case "ttower_getnumfloors":case "ttower_shouldexit":next=Result(state,0);return true;
            case "ttower_encountermusic":next=state with{Music=_content.TrainerTowerEncounterMusic(TowerTemplate(state),Value("VAR_TEMP_1"))};return true;
            case "ttower_getbeatchallenge":next=Result(state,tower.CurrentRecord.SpokeToOwner?1:0);return true;
            default:throw new InvalidDataException("trainer_tower_native_unsupported: "+opcode);
        }
    }

    private CampaignState BeginTrainerTowerBattle(CampaignState state,StoryEffect effect)
    {
        if(effect.Source.Path!=TrainerTowerShared||state.Script.Wait?.Kind!=StoryWaitKind.Battle||state.ForestTrainerBattle is not null||state.DoubleBattle is not null||state.WildBattle is not null)
            throw new InvalidDataException("trainer_tower_battle_context");
        // No independent allocator is needed: every Tower exit requires settlement.
        // Reject exhaustion before floor progress, party creation or entry RNG work.
        string id=NextTrainerTowerBattleId(state.TrainerTowerHistory);
        var tower=TowerProgress(state);int floor=TrainerTowerFloorNumber(state.World.Player.MapId);var format=TrainerTowerLayout(state,state.World.Player.MapId);
        int trainer=state.Script.Variables.GetValueOrDefault("VAR_TEMP_1");
        if(tower.FloorChain is null)tower=TrainerTowerProgressRules.BeginFloor(tower,_content.TrainerTowerCatalog!,floor);
        if(tower.FloorChain!.TrainerIndex!=trainer)throw new InvalidDataException("trainer_tower_knockout_sequence");
        var made=_content.TrainerTowerCatalog!.CreateBattle(_content.TrainerTowerOpponents!,tower.SelectedCourse,floor-1,trainer,state.Party.Where(p=>!p.IsEgg).Max(p=>p.Pokemon.Level),state.World.Rng);
        var party=state.Party.Select(p=>new WildBattleCalledPartyMember(p.Pokemon,p.HeldItem,p.Status,IsEgg:p.IsEgg)).ToImmutableArray();
        var entry=SourceTrainerTowerEncounter.Begin(_content.TrainerTowerOpponents!,made,party,EffectiveBattleStyle(state),
            state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
            state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"),currentOwned:_binding.Ownership);
        if(!entry.Succeeded)throw new InvalidDataException(entry.Fault);
        var frame=entry.State! with{Obedience=CampaignObedienceContext(state)};
        string actor=format switch{TrainerTowerBattleFormat.Double=>"LOCALID_TOWER_TRAINER_DOUBLES1",TrainerTowerBattleFormat.Knockout=>trainer switch{0=>"LOCALID_TOWER_TRAINER_KNOCKOUT",1=>"LOCALID_TOWER_TRAINER_DOUBLES1",_=>"LOCALID_TOWER_TRAINER_SINGLES"},_=>"LOCALID_TOWER_TRAINER_SINGLES"};
        var next=ResetFieldPoisonCounterForBattle(state) with{TrainerTower=tower,Dialogue=null,Motions=[],PresentationTicks=0,World=state.World with{Rng=frame.Rng},Music="MUS_VS_TRAINER"};
        if(format==TrainerTowerBattleFormat.Double)
            return next with{DoubleBattle=new(id,made.Trainer.Id,actor,frame,CampaignDoubleBattleStage.Combat,entry.Events.Where(e=>e is not SourceTrainerStartedEvent).ToImmutableArray(),0,[],state.Party.Select(p=>p.Pokemon.Level).ToImmutableArray(),0,0,
                entry.EntrySlots.Select(c=>c.Body!).ToImmutableArray(), entry.EntrySlots.Select(c=>c.Member!.Value).ToImmutableArray(),entry.Trace,[],0),
                SeenSpecies=state.SeenSpecies.Union(frame.Party(BattleSide.Opponent).Members.Select(m=>m.Pokemon.SourceSpeciesId))};
        int active=frame.Get(new(0)).Member!.Value.Index;
        var view=new CampaignWildBattle(id,state.Party[active],frame,ProjectTrainerWildEvents(entry.Events),0,frame.Get(new(0)).Body!.Pokemon,frame.Get(new(1)).Body!.Pokemon,
            CampaignBattlePhase.Events,[],0,entry.Trace){DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(entry.EntrySlots)), SourceEvents=entry.Events,ActivePartyIndex=active,OpponentHpAtLastSwitch=frame.Get(new(1)).Body!.Pokemon.CurrentHp};
        return PrepareForestTrainerPresentation(ProjectSourceTrainerOwnedParty(next with{ForestTrainerBattle=new(id,made.Trainer.Id,actor,view,CampaignForestTrainerStage.Combat,state.Party.Select(p=>p.Pokemon.Level).ToImmutableArray(),
            ProjectTrainerEvents(frame,entry.Events),0,0,[0]),SeenSpecies=state.SeenSpecies.Add(frame.Get(new(1)).Body!.Pokemon.SourceSpeciesId)}));
    }

    private SourceTwinsAiResult TrainerTowerEnemyActions(SourceBattleFrame frame)
    {
        var chosen=SourceTrainerTowerEncounter.PrepareActions(_content.PresetDoubleItems!,DoubleBattlePokemon,frame,frame.Revision,currentOwned:_binding.Ownership);
        return new(chosen.State,chosen.Actions.Select(a=>new SourceTwinsEnemyChoice(a.Actor,a.Selection.Target??new BattlerSlot(0),a.Selection,[],[],[])).ToImmutableArray(),chosen.Trace,chosen.Fault);
    }

    private CampaignState SettleTrainerTowerBattle(CampaignState state,SourceBattleFrame frame,string id,ImmutableArray<int> sent)
    {
        if(ValidateTrainerTowerHistory(state.TrainerTowerHistory) is {} historyError)throw new InvalidDataException(historyError);
        if(state.TrainerTowerHistory is {Length:1} history && history[0].Id==id)throw new InvalidDataException("trainer_tower_settlement_duplicate");
        if(id!=NextTrainerTowerBattleId(state.TrainerTowerHistory))throw new InvalidDataException("trainer_tower_settlement_identity");
        var h=ActiveTrainerTowerReceipt(state,frame,id,finished:false);
        if(sent.IsDefault || !sent.SequenceEqual(h.OpponentsSeen))throw new InvalidDataException("trainer_tower_settlement_seen");
        var settled=SourceTrainerTowerEncounter.Settle(CanonicalTrainerPokemon,frame,state.Money,currentOwned:_binding.Ownership);
        var roster=frame.TrainerTowerEncounter!.Roster;
        var course=Enum.Parse<TrainerTowerChallengeKind>(roster.Course);
        if(settled.PerformPickup)
        {
            var pickup = VermilionPickupRules.Apply(_binding.Ownership, state.Party, frame.Rng, WildBattleOutcome.Won);
            state=state with{Party=pickup.Party,World=state.World with{Rng=pickup.Rng}};frame=frame with{Rng=pickup.Rng};
            if(frame.Layout==SourceBattleLayout.Singles)frame=SyncSourceTrainerPickup(frame,pickup.Party);
        }
        state=state with{TrainerTowerHistory=[h]};
        var template=_content.TrainerTowerCatalog!.Template(_content.TrainerTowerCatalog.Course(course).Floors[roster.FloorIndex].TemplateSymbol);
        var lines=(frame.Layout==SourceBattleLayout.PresetDoubles?template.Trainers.Take(2):template.Trainers.Skip(roster.TrainerIndex).Take(1))
            .Select(t=>string.Join(" ",(frame.Outcome==WildBattleOutcome.Won?t.SpeechLose:t.SpeechWin).Select(w=>w.Text))).ToImmutableArray();
        if(state.ForestTrainerBattle is {} single)return state with{ForestTrainerBattle=single with{Frame=frame,Stage=CampaignForestTrainerStage.Finished,
            Presentation=single.Presentation with{Frame=frame,Phase=CampaignBattlePhase.Results,Pages=lines.Select(t=>new CampaignWildPage(t)).ToImmutableArray(),PageIndex=0,Events=[],EventIndex=0},TrainerEvents=[],TrainerEventIndex=0}};
        return state with{DoubleBattle=state.DoubleBattle! with{Frame=frame,Stage=CampaignDoubleBattleStage.Finished,FinishPages=lines,FinishPageIndex=0}};
    }
    private CampaignState ReturnFromTrainerTowerBattle(CampaignState state,SourceBattleFrame frame,string id)
    {
        var expected=ActiveTrainerTowerReceipt(state,frame,id,finished:true);
        if(ValidateTrainerTowerHistory(state.TrainerTowerHistory) is {} historyError)throw new InvalidDataException(historyError);
        if(state.TrainerTowerHistory is not {Length:1} history ||
            (history[0] with{OpponentsSeen=expected.OpponentsSeen})!=expected ||
            !history[0].OpponentsSeen.SequenceEqual(expected.OpponentsSeen))
            throw new InvalidDataException("trainer_tower_history_missing");
        var tower=TowerProgress(state);
        tower=frame.Outcome==WildBattleOutcome.Won?TrainerTowerProgressRules.WinBattle(tower):TrainerTowerProgressRules.LoseBattle(tower);
        int outcome=frame.Outcome switch{WildBattleOutcome.Won=>1,WildBattleOutcome.Lost=>2,_=>3};
        var next=state with{TrainerTower=tower,ForestTrainerBattle=null,DoubleBattle=null,Dialogue=null,Motions=[],PresentationTicks=0,
            Music=_content.MapDefinition(state.World.Player.MapId).Music,Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",outcome)}};
        return Pump(Advance(next,StoryRules.Continue(next.Script,next.Script.Wait!.CorrelationId,_content)));
    }
}
