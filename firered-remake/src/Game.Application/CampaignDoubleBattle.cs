using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
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
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignVsSeekerBattleReceipt? VsSeeker { get; init; }
    [JsonIgnore] public TrainerPartyCreationResult? VsSeekerParty { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public SourceTrainerItemInventory? VsSeekerInventory { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public MoveId? VsSeekerCurrentMove { get; init; }
    [JsonIgnore] public SourceBattleEvent? CurrentEvent => EventIndex < Events.Length ? Events[EventIndex] : null;
    [JsonIgnore] public int? SelectingSlot => Stage != CampaignDoubleBattleStage.Combat || CurrentEvent is not null ||
        Frame.FourTurn is not null ? null : new[] { 0, 2 }.Where(i => SourceFourMedicineRules.CanSelect(Frame,new(i)) && Frame.Get(new(i)).Registers.Turn?.Lock is null &&
        !PlayerSelections.Any(s => s.Actor.Slot.Value == i)).Select(i => (int?)i).FirstOrDefault();
    [JsonIgnore] public bool LearningPending => CurrentEvent is null && Frame.Rewards?.Pending is not null;
    [JsonIgnore] public int? ReplacementSlot => CurrentEvent is not null ? null :
        Frame.PendingSwitches.Where(s => s.Slot.Side == BattleSide.Player).Select(s => (int?)s.Slot.Value).FirstOrDefault();
}
public sealed record CampaignDoubleBattleHistory(string Id, string TrainerId, WildBattleOutcome Outcome,
    int ExperienceGained, int MoneyGained, ImmutableArray<int> OpponentsSeen)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignVsSeekerBattleReceipt? VsSeeker { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? MoneyMultiplier { get; init; }
}

public sealed partial class CampaignSession
{
    private PokemonCatalog DoubleBattlePokemon => _content.PresetDoublePokemon ??
        throw new InvalidDataException("preset_double_content_missing");
    private SourceTwinsTrainerDefinition TwinsDefinition => _content.Route8Twins ??
        throw new InvalidDataException("preset_double_definition_missing");
    private bool IsRoute16FlyDoubleScript(string script) =>
        _content.Route16FlyTrainers?.BattlesByScript.ContainsKey(script) == true;
    private bool IsRoute16FlyDoubleTrainer(TrainerId trainer) =>
        _content.Route16FlyTrainers?.Trainers.ContainsKey(trainer) == true;
    private bool IsSeviiRubyDoubleScript(string script)=>_content.SeviiRubyEnabled&&
        _content.SeviiRubyTrainers?.BattlesByScript.TryGetValue(script,out var battle)==true&&battle.Mode==4;
    private bool IsVictoryRoadDoubleScript(string script)=>_content.VictoryRoadEnabled&&_content.VictoryRoadTrainerParties?.BattlesByScript.TryGetValue(script,out var battle)==true&&battle.Mode==4;
    private bool IsCinnabarDoubleScript(string script)=>_content.CinnabarEnabled&&
        _content.CinnabarTrainerParties?.BattlesByScript.TryGetValue(script,out var battle)==true&&battle.Mode==4;
    private bool IsFirstSeviiDoubleScript(string script)=>_content.FirstSeviiEnabled&&
        _content.FirstSeviiTrainerParties!.BattlesBySource.Values.Any(b=>b.ScriptEntry==script&&b.Mode==4);
    private Game.Domain.Trainers.CeruleanTrainerBattleDefinition FirstSeviiDoubleScript(string script)=>
        _content.FirstSeviiTrainerParties!.BattlesBySource.Values.Single(b=>b.ScriptEntry==script&&b.Mode==4);
    private bool IsTwinsScript(string script) => script is "Route8_EventScript_Eli" or "Route8_EventScript_Anne" || IsCinnabarDoubleScript(script) || IsFirstSeviiDoubleScript(script) || IsVictoryRoadDoubleScript(script) || IsSeviiRubyDoubleScript(script) || IsSixIslandDoubleScript(script) || IsSevenIslandDoubleScript(script) || IsSeafoamDoubleScript(script) || IsRoute16FlyDoubleScript(script);
    private TrainerId DoubleTrainerId(string script)=>IsRoute16FlyDoubleScript(script)?Route16FlyDoubleTrainerEncounter.TrainerId:IsSeafoamDoubleScript(script)?_content.SeafoamTrainers!.BattlesByScript[script].Trainer:IsSevenIslandDoubleScript(script)?_content.SevenIslandTrainers!.BattlesByScript[script].Trainer:IsSixIslandDoubleScript(script)?SixIslandDoubleTrainerEncounter.TrainerId:IsSeviiRubyDoubleScript(script)?SeviiRubyDoubleTrainerEncounter.TrainerId:IsVictoryRoadDoubleScript(script)?VictoryRoadDoubleTrainerEncounter.TrainerId:IsFirstSeviiDoubleScript(script)?FirstSeviiDoubleTrainerEncounter.TrainerId:IsCinnabarDoubleScript(script)?CinnabarDoubleTrainerEncounter.TrainerId:SourceTwinsTrainerEncounter.TrainerId;
    private TrainerDefinition DoubleTrainer(TrainerId trainer)=>IsVsSeekerDouble(trainer)?_content.VsSeekerTrainers!.Trainers[trainer]:IsRoute16FlyDoubleTrainer(trainer)?_content.Route16FlyTrainers!.Trainers[trainer]:IsSeafoamDoubleTrainer(trainer)?_content.SeafoamTrainers!.Trainers[trainer]:IsSevenIslandDoubleTrainer(trainer)?_content.SevenIslandTrainers!.Trainers[trainer]:_content.SixIslandEnabled&&trainer==SixIslandDoubleTrainerEncounter.TrainerId?_content.SixIslandTrainers!.Trainers[trainer]:_content.SeviiRubyEnabled&&trainer==SeviiRubyDoubleTrainerEncounter.TrainerId?_content.SeviiRubyTrainers!.Trainers[trainer]:_content.VictoryRoadEnabled&&trainer==VictoryRoadDoubleTrainerEncounter.TrainerId?_content.VictoryRoadTrainerParties!.Trainers[trainer]:_content.FirstSeviiEnabled&&trainer==FirstSeviiDoubleTrainerEncounter.TrainerId?_content.FirstSeviiTrainerParties!.Trainers[trainer]:trainer==SourceTwinsTrainerEncounter.TrainerId?TwinsDefinition.Trainer:
        _content.CinnabarEnabled&&trainer==CinnabarDoubleTrainerEncounter.TrainerId?_content.CinnabarTrainerParties!.Trainers[trainer]:
        throw new InvalidDataException("preset_double_trainer_unsupported");
    private SourceTwinsBattleDefinition DoubleInteraction(TrainerId trainer,string script)
    {
        if(trainer==SourceTwinsTrainerEncounter.TrainerId)return TwinsDefinition.Interactions.Single(i=>i.ScriptEntry==script);
        var b=IsRoute16FlyDoubleTrainer(trainer)?_content.Route16FlyTrainers!.BattlesByScript.GetValueOrDefault(script)??throw new InvalidDataException("route16_double_script_missing"):IsSeafoamDoubleTrainer(trainer)?_content.SeafoamTrainers!.BattlesByScript.GetValueOrDefault(script)??throw new InvalidDataException("seafoam_double_script_missing"):IsSevenIslandDoubleTrainer(trainer)?_content.SevenIslandTrainers!.BattlesByScript.GetValueOrDefault(script)??throw new InvalidDataException("seven_island_double_script_missing"):trainer==SixIslandDoubleTrainerEncounter.TrainerId?_content.SixIslandTrainers!.BattlesByScript.GetValueOrDefault(script)??throw new InvalidDataException("six_island_double_script_missing"):trainer==SeviiRubyDoubleTrainerEncounter.TrainerId?_content.SeviiRubyTrainers!.BattlesByScript.GetValueOrDefault(script)??throw new InvalidDataException("sevii_ruby_double_script_missing"):trainer==VictoryRoadDoubleTrainerEncounter.TrainerId?_content.VictoryRoadTrainerParties!.BattlesByScript.GetValueOrDefault(script)??throw new InvalidDataException("victory_road_double_script_missing"):trainer==FirstSeviiDoubleTrainerEncounter.TrainerId?FirstSeviiDoubleScript(script):_content.CinnabarTrainerParties!.BattlesByScript.GetValueOrDefault(script)??throw new InvalidDataException("cinnabar_double_script_missing");
        if(b.Trainer!=trainer||b.Mode!=4||b.NotEnoughMonsText is null)throw new InvalidDataException("cinnabar_double_script_mismatch");
        return new(b.ScriptEntry,b.IntroText,b.DefeatText,b.PostBattleText,b.NotEnoughMonsText,b.Source);
    }
    private int DoublePrize(TrainerId trainer)=>trainer==SourceTwinsTrainerEncounter.TrainerId?TwinsDefinition.VictoryMoney:
        checked(2*4*DoubleTrainer(trainer).BaseMoney*DoubleTrainer(trainer).Party[^1].Level);
    private SourceTwinsEncounterResult PrepareDoubleBattle(CampaignState state,string? battleScript=null)
    {
        if(!_content.PresetDoublesEnabled||!(state.World.Player.MapId.Value=="map.route-8"||state.World.Player.MapId.Value=="map.route-16"&&battleScript is not null&&IsRoute16FlyDoubleScript(battleScript)||battleScript is not null&&IsSeafoamDoubleScript(battleScript)&&SeafoamTrainerActorMap(state.World.Player.MapId,battleScript)||battleScript is not null&&IsSevenIslandDoubleScript(battleScript)&&SevenIslandTrainerActorMap(state.World.Player.MapId,battleScript)||_content.SixIslandEnabled&&state.World.Player.MapId.Value=="map.six-island-water-path"&&battleScript is not null&&IsSixIslandDoubleScript(battleScript)||_content.SeviiRubyEnabled&&state.World.Player.MapId.Value=="map.one-island-kindle-road"&&battleScript is not null&&IsSeviiRubyDoubleScript(battleScript)||_content.VictoryRoadEnabled&&state.World.Player.MapId.Value=="map.victory-road-3f"&&battleScript is not null&&IsVictoryRoadDoubleScript(battleScript)||_content.FirstSeviiEnabled&&state.World.Player.MapId.Value=="map.three-island-bond-bridge"&&battleScript is not null&&IsFirstSeviiDoubleScript(battleScript)||_content.CinnabarEnabled&&state.World.Player.MapId.Value=="map.route-21-north"&&battleScript is not null&&IsCinnabarDoubleScript(battleScript))||
            state.DoubleBattle is not null||state.ForestTrainerBattle is not null||state.WildBattle is not null||state.Battle is not null||
            state.Party.Any(p=>!HasSupportedCampaignOriginalTrainer(state,p)))
            throw new InvalidDataException("preset_double_entry_context");
        var party=state.Party.Select(p=>new WildBattleCalledPartyMember(p.Pokemon,p.HeldItem,p.Status,p.IsEgg)).ToImmutableArray();
        var entry=battleScript is not null&&IsRoute16FlyDoubleScript(battleScript)?
            Route16FlyDoubleTrainerEncounter.Begin(_content.Route16FlyTrainers!,_binding.Ownership,battleScript,party,state.World.Rng,
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"),CampaignBattleTerrain(state)):
            battleScript is not null&&IsSeafoamDoubleScript(battleScript)?
            SeafoamDoubleTrainerEncounter.Begin(_content.SeafoamTrainers!,_content.SeafoamOpponents!,battleScript,party,state.World.Rng,
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"),CampaignBattleTerrain(state),successorOwned:_binding.Ownership):
            battleScript is not null&&IsSevenIslandDoubleScript(battleScript)?
            SevenIslandDoubleTrainerEncounter.Begin(_content.SevenIslandTrainers!,_content.SevenIslandOpponents!,battleScript,party,state.World.Rng,
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"),CampaignBattleTerrain(state),successorOwned:_binding.Ownership):
            battleScript is not null&&IsSixIslandDoubleScript(battleScript)?
            SixIslandDoubleTrainerEncounter.Begin(_content.SixIslandTrainers!,_content.SixIslandOpponents!,battleScript,party,state.World.Rng,
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"),CampaignBattleTerrain(state),successorOwned:_content.FiveIslandEnabled?SafariOrdinaryOwnership:null):
            battleScript is not null&&IsSeviiRubyDoubleScript(battleScript)?
            SeviiRubyDoubleTrainerEncounter.Begin(_content.SeviiRubyTrainers!,_content.SeviiRubyOpponents!,battleScript,party,state.World.Rng,
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"),CampaignBattleTerrain(state),successorOwned:_content.FourIslandEnabled?SafariOrdinaryOwnership:null):
            battleScript is not null&&IsVictoryRoadDoubleScript(battleScript)?
            VictoryRoadDoubleTrainerEncounter.Begin(_content.VictoryRoadTrainerParties!,_content.CinnabarOpponents!,battleScript,party,state.World.Rng,
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"),CampaignBattleTerrain(state),successorOwned:_content.SeviiRubyEnabled?SafariOrdinaryOwnership:null):
            battleScript is not null&&IsFirstSeviiDoubleScript(battleScript)?
            FirstSeviiDoubleTrainerEncounter.Begin(_content.FirstSeviiTrainerParties!,_content.CinnabarOpponents!,battleScript,FirstSeviiDoubleScript(battleScript).Source.StartLine,party,state.World.Rng,
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"),CampaignBattleTerrain(state),successorOwned:_content.SeviiRubyEnabled?SafariOrdinaryOwnership:null):
            battleScript is not null&&IsCinnabarDoubleScript(battleScript)?
            CinnabarDoubleTrainerEncounter.Begin(_content.CinnabarTrainerParties!,_content.CinnabarOpponents!,battleScript,party,state.World.Rng,
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"),CampaignBattleTerrain(state),successorOwned:_content.SeviiRubyEnabled?SafariOrdinaryOwnership:null):
            SourceTwinsTrainerEncounter.Begin(DoubleBattlePokemon,TwinsDefinition,party,state.World.Rng,
                state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),successorOwned:_content.SeviiRubyEnabled?SafariOrdinaryOwnership:null);
        if(!entry.Succeeded)throw new InvalidDataException(entry.Fault);
        // This successor still accepts only the original local gifts/captures. Its stable
        // schema has no infection or trade route. ValidatePlayerSave enforces that closed
        // provenance; a future Pokerus/trade profile must provide its own authoritative facts.
        var frame=entry.State! with{Obedience=CampaignObedienceContext(state),Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,state.TrainerId,state.PlayerName,
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection),Metadata=state.Party.Select((p,i)=>new SourceRewardMetadata(
            new(BattleSide.Player,i),p.OriginalTrainerName,p.MetLocation,p.Ball,SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
        if(SourceFourActionTurns.ValidateCapability(DoubleBattlePokemon,frame) is {} error)throw new InvalidDataException(error);
        return entry with{State=frame};
    }

    private CampaignState BeginDoubleBattle(CampaignState state,StoryEffect effect)
    {
        if(!_content.PresetDoublesEnabled||effect.Args.Length!=4||state.Script.Wait?.Kind!=StoryWaitKind.Battle||
            state.LastTalked is not {} actorId||!state.Actors.TryGetValue(actorId,out var actor)||!IsTwinsScript(actor.ScriptId)||
            !(state.World.Player.MapId.Value=="map.route-8"||state.World.Player.MapId.Value=="map.route-16"&&IsRoute16FlyDoubleScript(actor.ScriptId)||IsSeafoamDoubleScript(actor.ScriptId)&&SeafoamTrainerActorMap(state.World.Player.MapId,actor.ScriptId)||IsSevenIslandDoubleScript(actor.ScriptId)&&SevenIslandTrainerActorMap(state.World.Player.MapId,actor.ScriptId)||_content.SixIslandEnabled&&state.World.Player.MapId.Value=="map.six-island-water-path"&&IsSixIslandDoubleScript(actor.ScriptId)||_content.SeviiRubyEnabled&&state.World.Player.MapId.Value=="map.one-island-kindle-road"&&IsSeviiRubyDoubleScript(actor.ScriptId)||_content.VictoryRoadEnabled&&state.World.Player.MapId.Value=="map.victory-road-3f"&&IsVictoryRoadDoubleScript(actor.ScriptId)||_content.FirstSeviiEnabled&&state.World.Player.MapId.Value=="map.three-island-bond-bridge"&&IsFirstSeviiDoubleScript(actor.ScriptId)||_content.CinnabarEnabled&&state.World.Player.MapId.Value=="map.route-21-north"&&IsCinnabarDoubleScript(actor.ScriptId)))throw new InvalidDataException("preset_double_source_context");
        var trainerId=DoubleTrainerId(actor.ScriptId);
        var d=DoubleInteraction(trainerId,actor.ScriptId);
        bool rematch=effect.Opcode=="trainerbattle_rematch_double";
        // Both authenticated first-visit wrappers also contain the original post-win
        // rematch branch. With no VS Seeker it only reaches the ordinary dialogue.
        int expectedLine=rematch?(actor.ScriptId=="ThreeIsland_BondBridge_EventScript_Joy"?2205:2217):d.Source.StartLine;
        if(IsRoute16FlyDoubleScript(actor.ScriptId) && (!actor.Active ||
            actor.SourceLocalId != (actor.ScriptId == "Route16_EventScript_Lea" ? 8 : 9) ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            effect.Source.Line != d.Source.StartLine + (rematch ? 8 : 1)))
            throw new InvalidDataException("route16_double_occurrence");
        if(IsSeafoamDoubleScript(actor.ScriptId))RequireSeafoamDoubleOccurrence(state,effect,actor.ScriptId,rematch);
        if(IsSevenIslandDoubleScript(actor.ScriptId))RequireSevenIslandDoubleOccurrence(state,effect,actor.ScriptId,rematch);
        if(IsSixIslandDoubleScript(actor.ScriptId))RequireSixIslandDoubleOccurrence(state,effect,actor.ScriptId,rematch);
        if(IsSeviiRubyDoubleScript(actor.ScriptId)&&effect.Source.Line!=SeviiRubyTrainerSourceLine(actor.ScriptId+(rematch?"Rematch":"")))
            throw new InvalidDataException("sevii_ruby_double_occurrence");
        if(IsFirstSeviiDoubleScript(actor.ScriptId)&&effect.Source.Line!=expectedLine)
            throw new InvalidDataException("first_sevii_double_occurrence");
        if(effect.Source.Path!=d.Source.Path||effect.Source.Symbol!=d.ScriptEntry+(rematch?"Rematch":"")||
            effect.Args[0]!=trainerId.Value[5..]||effect.Args[1]!=(rematch?d.IntroText.Replace("Intro","RematchIntro",StringComparison.Ordinal):d.IntroText)||
            effect.Args[2]!=d.DefeatText||effect.Args[3]!=d.NotEnoughMonsText)
            throw new InvalidDataException("preset_double_source_mismatch");
        if(!_forestTrainerApproaching)state=FaceSelectedObject(state);
        if(state.BattledTrainers.Contains(trainerId.Value))
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
        var prepared=PrepareDoubleBattle(state,actor.ScriptId);var frame=prepared.State!;
        string id="double-trainer-battle:"+((state.DoubleBattleHistory?.Length??0)+1);
        return state with{DoubleBattle=new(id,trainerId,actorId,frame,CampaignDoubleBattleStage.Intro,
            prepared.Events,0,[],state.Party.Select(p=>p.Pokemon.Level).ToImmutableArray(),0,0,
            frame.Slots.Select(c=>c.Body!).ToImmutableArray(),frame.Slots.Select(c=>c.Member!.Value).ToImmutableArray(),prepared.Trace,[],0),
            Dialogue=new(d.IntroText,_content.TextPages(d.IntroText),0,false,false),
            Script=state.Script with{Locked=true,Variables=state.Script.Variables.SetItem("VAR_RESULT",0)},
            Actors=state.Actors.SetItem(actorId,actor with{MovementType=ForestTrainerFacingType(actor.Facing)}),Music=IsRoute16FlyDoubleTrainer(trainerId)?"MUS_ENCOUNTER_GIRL":IsSeafoamDoubleTrainer(trainerId)?SeafoamDoubleEncounterMusic(trainerId):IsSevenIslandDoubleTrainer(trainerId)?SevenIslandDoubleEncounterMusic(trainerId):trainerId==SourceTwinsTrainerEncounter.TrainerId?TwinsDefinition.EncounterMusic:trainerId==FirstSeviiDoubleTrainerEncounter.TrainerId||trainerId==SixIslandDoubleTrainerEncounter.TrainerId?"MUS_ENCOUNTER_GIRL":"MUS_ENCOUNTER_BOY"};
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
            if(b.PlayerSelections[^1].Kind==SourceActionKind.Item)return No("preset_double_item_already_used");
            return new(CommandStatus.Accepted,state with{DoubleBattle=b with{PlayerSelections=b.PlayerSelections.RemoveAt(b.PlayerSelections.Length-1)}});
        }
        var frame=b.Frame;
        // Forced turn locks are source controller choices, not fresh player input.
        var selections=b.PlayerSelections;
        foreach(int i in new[]{0,2})
            if(SourceFourMedicineRules.CanSelect(frame,new(i))&&!selections.Any(a=>a.Actor.Slot.Value==i)&&frame.Get(new(i)).Registers.Turn?.Lock is {} forced)
                selections=selections.Add(new(frame.Get(new(i)).Occupant!.Value,SourceActionKind.Move,forced.PpSlot,forced.Target));
        var unselected=new[]{0,2}.Where(i=>SourceFourMedicineRules.CanSelect(frame,new(i))&&!selections.Any(a=>a.Actor.Slot.Value==i)).ToArray();
        if(unselected.Length>0)
        {
            if(command.PartyIndex!=unselected[0]||command.Kind is not ("double_move" or "double_switch" or "double_medicine"))return No("preset_double_action_required");
            var cell=frame.Get(new(command.PartyIndex));SourceActionSelection selection;
            if(command.Kind=="double_medicine")
            {
                if(command.Choice is not {} target)return No("battle_medicine_target_identity");
                var invalid=ValidateBattleMedicineCommand(state,command with{PartyIndex=target});
                if(invalid is not null)return No(invalid);
                if(command.MoveSlot is < -1 or >3)return No("source_item_pp_slot");
                var medicine=SourceFourMedicineRules.Select(_content.PresetDoubleItems!,_content.PresetDoubleMechanics!,
                    frame,frame.Revision,cell.Occupant!.Value,state.Inventory!,state.Party,
                    new(new ItemId(command.Text!),command.TargetInstanceId!.Value,command.MoveSlot<0?null:command.MoveSlot),
                    _content.MapDefinition(state.World.Player.MapId).RegionMapSection);
                if(!medicine.Succeeded)return No(medicine.Fault!);
                // FR applies and spends ordinary medicine in the party menu, before the
                // partner chooses. Publish this result first; a later queue marker cannot heal again.
                return CommitDoubleResult(state with{Inventory=medicine.Inventory,
                    DoubleBattle=b with{PlayerSelections=selections.Add(medicine.Selection!)}},medicine.Battle);
            }
            else if(command.Kind=="double_switch")
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
                    invalid is not ("source_kernel_switch_owner_required" or "source_kernel_flinch_order_not_migrated" or "source_kernel_protection_order_required"))return No(invalid);
            }
            selections=selections.Add(selection);
            if(unselected.Length>1)return new(CommandStatus.Accepted,state with{DoubleBattle=b with{PlayerSelections=selections}});
        }
        else if(command.Kind!="ack")return No("preset_double_forced_turn_ready");
        if(b.VsSeekerParty is {} vsParty)
        {
            var prepared=SourcePresetTrainerEncounterEntry.PrepareEnemyActions(_content.PresetDoubleItems!,
                _binding.Ownership.Mechanics,vsParty,frame,frame.Revision,
                b.VsSeekerInventory??throw new InvalidDataException("vs_seeker_double_inventory"),
                b.VsSeekerCurrentMove??SourceTrainerEncounterPolicy.NoMove,_binding.Ownership);
            if(!prepared.Succeeded)return No(prepared.Fault!);
            var started=SourceFourActionTurns.BeginActions(DoubleBattlePokemon,prepared.State,prepared.State.Revision,selections.AddRange(prepared.Selections));
            if(!started.Succeeded)return No(started.Fault!);
            return CommitDoubleResult(state with{DoubleBattle=b with{PlayerSelections=[],VsSeekerInventory=prepared.Inventory,
                VsSeekerCurrentMove=prepared.SourceCurrentMove}},
                started with{Events=prepared.Events.AddRange(started.Events),Trace=prepared.Trace.AddRange(started.Trace)});
        }
        var ai=frame.TrainerTowerEncounter is not null
            ?TrainerTowerEnemyActions(frame)
            :IsRoute16FlyDoubleTrainer(b.Trainer)
            ?Route16FlyDoubleTrainerEncounter.ChooseEnemyActions(_content.Route16FlyTrainers!,_binding.Ownership,b.Trainer,frame,frame.Revision)
            :IsSeafoamDoubleTrainer(b.Trainer)
            ?SeafoamDoubleTrainerEncounter.ChooseEnemyActions(_content.SeafoamTrainers!,_content.SeafoamOpponents!,b.Trainer,frame,frame.Revision)
            :IsSevenIslandDoubleTrainer(b.Trainer)
            ?SevenIslandDoubleTrainerEncounter.ChooseEnemyActions(_content.SevenIslandTrainers!,_content.SevenIslandOpponents!,b.Trainer,frame,frame.Revision)
            :b.Trainer==SixIslandDoubleTrainerEncounter.TrainerId
            ?SixIslandDoubleTrainerEncounter.ChooseEnemyActions(_content.SixIslandTrainers!,_content.SixIslandOpponents!,frame,frame.Revision)
            :b.Trainer==SeviiRubyDoubleTrainerEncounter.TrainerId
            ?SeviiRubyDoubleTrainerEncounter.ChooseEnemyActions(_content.SeviiRubyTrainers!,_content.SeviiRubyOpponents!,frame,frame.Revision)
            :b.Trainer==VictoryRoadDoubleTrainerEncounter.TrainerId
            ?VictoryRoadDoubleTrainerEncounter.ChooseEnemyActions(_content.VictoryRoadTrainerParties!,_content.CinnabarOpponents!,frame,frame.Revision)
            :b.Trainer==FirstSeviiDoubleTrainerEncounter.TrainerId
            ?FirstSeviiDoubleTrainerEncounter.ChooseEnemyActions(_content.FirstSeviiTrainerParties!,_content.CinnabarOpponents!,frame,frame.Revision)
            :b.Trainer==CinnabarDoubleTrainerEncounter.TrainerId
            ?CinnabarDoubleTrainerEncounter.ChooseEnemyActions(_content.CinnabarTrainerParties!,_content.CinnabarOpponents!,frame,frame.Revision)
            :SourceTwinsTrainerEncounter.ChooseEnemyActions(DoubleBattlePokemon,frame,frame.Revision);
        if(!ai.Succeeded)return No(ai.Fault!);
        var begun=SourceFourActionTurns.BeginActions(DoubleBattlePokemon,ai.State,ai.State.Revision,selections.AddRange(ai.Selections));
        if(!begun.Succeeded)return No(begun.Fault!);
        return CommitDoubleResult(state with{DoubleBattle=b with{PlayerSelections=[]}},begun with{Trace=ai.Trace.AddRange(begun.Trace)});
    }

    private Decision<CampaignState> CommitDoubleResult(CampaignState state,SourceFourActionResult result)
    {
        if(!result.Succeeded)return new(CommandStatus.Rejected,state,result.Fault);
        var b=state.DoubleBattle!;var party=state.Party.ToBuilder();
        if(b.VsSeekerParty is not null)
        {
            var current=b.VsSeekerCurrentMove??SourceTrainerEncounterPolicy.NoMove;
            foreach(var e in result.Events)
                if(e is SourceAttackStringEvent attack)current=attack.CurrentMove;
                else if(e is SourceCalledPartySwitchedEvent)current=SourceTrainerEncounterPolicy.NoMove;
            b=b with{VsSeekerCurrentMove=result.State.FourTurn is null?SourceTrainerEncounterPolicy.NoMove:current};
        }
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
        void Change(BattlerSlot slot,Func<WildBattleCombatant,WildBattleCombatant> update)=>bodies=bodies.SetItem(slot.Value,update(bodies[slot.Value]));
        void Hp(BattlerSlot slot,int hp)=>Change(slot,m=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}});
        void Pp(BattlerSlot slot,int position,int pp)
        {
            Change(slot,m=>position<0||position>=m.Pokemon.Moves.Length?m:m with{Pokemon=m.Pokemon with{
                Moves=m.Pokemon.Moves.SetItem(position,m.Pokemon.Moves[position] with{CurrentPp=pp})}});
        }
        void PersistentPp(BattlerSlot slot,Game.Domain.Items.SourceItemPpRecovery recovery)=>Change(slot,m=>{
            var permanent=WildBattleTransformationRules.PersistentPokemon(m);
            if(recovery.Slot<0||recovery.Slot>=permanent.Moves.Length||permanent.Moves[recovery.Slot].Move!=recovery.Move)
                throw new InvalidDataException("preset_double_item_pp_receipt");
            return WildBattleTransformationRules.ApplyPersistentPokemon(m,permanent with{Moves=permanent.Moves.SetItem(recovery.Slot,
                permanent.Moves[recovery.Slot] with{CurrentPp=recovery.After})});
        });
        switch(e)
        {
            case SourceHpChangedEvent x:Hp(x.Target,x.HpAfter);break;
            case SourceRecoilEvent x:Hp(x.Actor,x.HpAfter);break;
            case SourceConfusionSelfHitEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceStatusDamageEvent x:Hp(x.Side,x.HpAfter);Change(x.Side,m=>m with{Status=x.Status});break;
            case SourceWeatherDamageEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceAbilityDamageEvent x:Hp(x.Target,x.HpAfter);break;
            case SourceRecoveredEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceDrainEvent x:Hp(x.Actor,x.HpAfter);break;
            case SourcePotionRecoveredEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceHeldItemHealEvent x:Hp(x.Actor,x.HpAfter);break;
            case SourceAbilityRecoveredEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceBindingDamageEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceIngrainHealEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceTowerHpEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceLeechSeedDrainEvent x:Hp(x.DrainedSide,x.HpAfter);Hp(x.ReceivingSide,x.ReceiverHpAfter);break;
            case SourceSelfDestructEvent x:Hp(x.Actor,0);break;
            case SourceMedicineAppliedEvent x:
                for(int i=0;i<members.Length;i++)
                    if(members[i]==new PartyMemberKey(x.TargetSide??BattleSide.Player,x.PartyIndex))
                    {
                        Hp(new(i),x.HpAfter);
                        if(x.StatusBefore!=x.StatusAfter)Change(new(i),m=>m with{Status=x.StatusAfter});
                        if(x.PpRecovery is {} recoveries)foreach(var itemRecovery in recoveries)PersistentPp(new(i),itemRecovery);
                        if(x.FriendshipAfter is {} friendship)Change(new(i),m=>WildBattleTransformationRules.ApplyPersistentPokemon(m,
                            WildBattleTransformationRules.PersistentPokemon(m) with{Friendship=friendship}));
                    }
                break;
            case SourceFaintedEvent x:Hp(x.Side,0);break;
            case SourceHeldItemConsumedEvent x:
                Hp(x.Side,x.HpAfter);Change(x.Side,m=>m with{HeldItem="ITEM_NONE"});
                if(x.PpRecovery is {} heldRecovery)PersistentPp(x.Side,heldRecovery);break;
            case SourceHeldItemKnockedOffEvent x:Change(x.Target,m=>m with{HeldItem="ITEM_NONE",KnockedOffItem=x.Item});break;
            case SourceHeldItemStolenEvent x:Change(x.Target,m=>m with{HeldItem="ITEM_NONE"});Change(x.Actor,m=>m with{HeldItem=x.Item});break;
            case SourceStatusChangedEvent x:Change(x.Side,m=>m with{Status=x.After});break;
            case SourceConfusionChangedEvent x:Change(x.Side,m=>m with{ConfusionTurnsRemaining=x.After});break;
            case SourceFocusEnergyEvent x:Change(x.Side,m=>m with{FocusEnergy=true});break;
            case SourceCalledOverlayChangedEvent x:Change(x.Side,m=>m with{CalledOverlay=x.After});break;
            case SourceTransformedEvent x:
                var actor=bodies[x.Actor.Value];var target=bodies[x.Target.Value];
                var donor=target.Pokemon with{Species=x.AppearanceSpecies,SourceSpeciesId=x.BattleView.SourceSpeciesId,
                    Personality=x.AppearancePersonality,OriginalTrainerId=x.AppearanceOriginalTrainerId};
                var form=new WildBattleTransformation(WildBattleTransformationRules.PersistentPokemon(actor),donor,
                    Enumerable.Range(0,4).Select(i=>i<actor.Pokemon.Moves.Length?actor.Pokemon.Moves[i].PpUps:0).ToImmutableArray());
                Change(x.Actor,m=>m with{Pokemon=x.BattleView,Transform=form,CopyMoves=null,CalledOverlay=target.CalledOverlay,
                    AttackStage=target.AttackStage,DefenseStage=target.DefenseStage,SpeedStage=target.SpeedStage,
                    AccuracyStage=target.AccuracyStage,EvasionStage=target.EvasionStage,SpecialAttackStageDelta=target.SpecialAttackStageDelta,
                    SpecialDefenseStageDelta=target.SpecialDefenseStageDelta,DisabledMove=null,DisableTurnsRemaining=0});break;
            case SourceMoveCopiedEvent x:
                Change(x.Actor,m=>{
                    var copies=m.CopyMoves?.Slots??[];
                    if(!x.Permanent&&!copies.Any(copy=>copy.Slot==x.MoveSlot))copies=copies.Add(new(x.MoveSlot,m.Pokemon.Moves[x.MoveSlot]));
                    var moves=m.Pokemon.Moves.SetItem(x.MoveSlot,x.Replacement);
                    if(x.Permanent)copies=copies.Select(copy=>copy with{PersistentSlot=moves[copy.Slot]}).ToImmutableArray();
                    return m with{Pokemon=m.Pokemon with{Moves=moves},CopyMoves=copies.IsEmpty?null:new(copies)};
                });break;
            case SourcePpCommandEvent x:Pp(x.PpOwner,x.PpSlot,x.After);break;
            case SourceMoveUsedEvent x:Pp(x.Actor,x.Slot,x.PpAfter);break;
            case SourceSpiteEvent x:Pp(x.Target,x.Slot,x.PpAfter);break;
            case SourceGrudgePpEvent x:Pp(x.Attacker,x.ChosenSlot,0);break;
            case SourceMagicCoatPressurePpEvent x:Pp(x.Reflector,x.Slot,x.After);break;
            case SourceSnatchPressurePpEvent x:Pp(x.Snatcher,x.Slot,x.After);break;
            case SourceRewardAppliedEvent x:
                for(int i=0;i<members.Length;i++)if(members[i]==x.Recipient)
                    Change(new(i),m=>WildBattleTransformationRules.ApplyPersistentPokemon(m,x.Reward.Pokemon));
                break;
            case SourceCalledPartyChangedEvent x:
                for(int i=0;i<members.Length;i++)if(members[i]==new PartyMemberKey(x.Side,x.PartyIndex))
                    Change(new(i),m=>WildBattleTransformationRules.ApplyPersistentPokemon(m,x.After.Pokemon) with{
                        Status=x.After.Status,HeldItem=x.After.HeldItem,KnockedOffItem=x.After.KnockedOffItem});
                break;
            case SourceCalledPartySwitchedEvent x:
                bodies=bodies.SetItem(x.Side.Value,(x.Replacement??throw new InvalidDataException("preset_double_switch_snapshot_missing")).Body);
                members=members.SetItem(x.Side.Value,new(x.Side.Side,x.IncomingIndex));break;
            case SourcePlayerReplacedEvent x:bodies=bodies.SetItem(x.Slot.Value,x.Replacement.Body);break;
            case SourcePlayerSwitchedEvent x:bodies=bodies.SetItem(x.Slot.Value,x.Replacement.Body);break;
            case SourceSubstituteDamagedEvent x:Change(x.Target,m=>m with{CalledVolatile=(m.CalledVolatile??new()) with{SubstituteHp=x.HpAfter}});break;
            case SourceMtMoonVolatileEvent {Volatile:"substitute"} x:Change(x.Side,m=>m with{CalledVolatile=(m.CalledVolatile??new()) with{SubstituteHp=x.After}});break;
            case SourceMtMoonVolatileEvent {Volatile:"stockpile"} x:Change(x.Side,m=>m with{CalledVolatile=(m.CalledVolatile??new()) with{StockpileCount=x.After}});break;
            case SourceStatChangedEvent x:
                Change(x.Target,m=>x.Stat switch {
                    WildBattleStat.Attack=>m with{AttackStage=x.After},WildBattleStat.Defense=>m with{DefenseStage=x.After},
                    WildBattleStat.Speed=>m with{SpeedStage=x.After},WildBattleStat.Accuracy=>m with{AccuracyStage=x.After},
                    WildBattleStat.Evasion=>m with{EvasionStage=x.After},WildBattleStat.SpecialAttack=>m with{SpecialAttackStageDelta=x.After-6},
                    WildBattleStat.SpecialDefense=>m with{SpecialDefenseStageDelta=x.After-6},_=>m});break;
        }
        return battle with{DisplayedSlots=bodies,DisplayedMembers=members};
    }

    private CampaignState SettleDoubleBattle(CampaignState state)
    {
        var b=state.DoubleBattle!;
        if(b.Frame.TrainerTowerEncounter is not null)return SettleTrainerTowerBattle(state,b.Frame,b.Id,[0,1]);
        if(b.Frame.Outcome==WildBattleOutcome.Ongoing||b.Frame.Rewards?.Pending is not null||
            (state.DoubleBattleHistory??[]).Any(h=>h.Id==b.Id))throw new InvalidDataException("preset_double_settlement_context");
        bool won=b.Frame.Outcome==WildBattleOutcome.Won;
        var prize=SourceBattleMoneyRules.SettleTrainerPrize(state.Money,DoublePrize(b.Trainer),b.Frame.Outcome,b.Frame.Field.MoneyMultiplier);
        int award=prize.DisplayAmount,money=prize.Money;
        var pages=ImmutableArray.CreateBuilder<string>();
        if(won)
        {
            pages.Add($"{state.PlayerName} defeated\n{(b.VsSeeker is not null?DoubleTrainer(b.Trainer).TrainerClass[14..].Replace('_',' '):IsRoute16FlyDoubleTrainer(b.Trainer)?"YOUNG COUPLE":IsSeafoamDoubleTrainer(b.Trainer)?SeafoamDoubleTrainerClass(b.Trainer):IsSevenIslandDoubleTrainer(b.Trainer)?SevenIslandDoubleTrainerClass(b.Trainer):b.Trainer==SeviiRubyDoubleTrainerEncounter.TrainerId?"CRUSH KIN":b.Trainer==VictoryRoadDoubleTrainerEncounter.TrainerId?"COOL COUPLE":b.Trainer==CinnabarDoubleTrainerEncounter.TrainerId?"SIS AND BRO":"TWINS")} {DoubleTrainer(b.Trainer).SourceTrainerName}!");
            string defeat=b.VsSeeker is {} receipt?VsSeekerOccurrence(receipt).Arguments[2]:DoubleInteraction(b.Trainer,state.Actors[b.ActorId].ScriptId).DefeatText;
            pages.AddRange(_content.TextPages(defeat));pages.Add($"{state.PlayerName} got ¥{award}\nfor winning!");
        }
        else
        {
            pages.Add($"{state.PlayerName} is out of usable\nPOKéMON!");
            int loss=WhiteOutLoss(state);pages.Add(state.Money==0?$"{state.PlayerName} whited out!":$"{state.PlayerName} panicked and lost\n{loss}...\n{state.PlayerName} whited out!");
        }
        var history=new CampaignDoubleBattleHistory(b.Id,b.Trainer.Value,b.Frame.Outcome,b.ExperienceGained,prize.Awarded,[0,1]){VsSeeker=b.VsSeeker,MoneyMultiplier=b.Frame.Field.MoneyMultiplier};
        if(won)
        {
            var payday=SourceBattleMoneyRules.SettlePayDay(money,b.Frame.Field.PayDayMoney,WildBattleOutcome.Won,b.Frame.Field.MoneyMultiplier);money=payday.Money;
            if(payday.Amount!=0)pages.Add($"{state.PlayerName} picked up\n¥{payday.DisplayAmount}!");
            var pickup=SafariOrdinaryOwnership is {} safari?VermilionPickupRules.ApplySafariOwned(safari,state.Party,state.World.Rng,WildBattleOutcome.Won):VermilionPickupRules.Apply(BattlePokemon,state.Party,state.World.Rng,WildBattleOutcome.Won,
                ssAnneRules:true,route11Rules:true,rockTunnelRules:true,celadonApproachRules:true,celadonGymRules:true,rocketHideoutRules:true,pokemonTowerRules:true,snorlaxRules:true,fuchsiaRules:true);
            state=state with{Party=pickup.Party,World=state.World with{Rng=pickup.Rng}};
            b=b with{Frame=b.Frame with{Rng=pickup.Rng},Trace=pickup.Trace};
        }
        var settled=state with{Money=money,DoubleBattleHistory=(state.DoubleBattleHistory??[]).Add(history),
            DoubleBattle=b with{Stage=CampaignDoubleBattleStage.Finished,MoneyGained=history.MoneyGained,FinishPages=pages.ToImmutable(),FinishPageIndex=0}};
        settled=ResetVsSeekerAfterBattle(settled,b.Trainer);
        return settled with{DoubleBattle=settled.DoubleBattle! with{Frame=settled.DoubleBattle.Frame with{Rng=settled.World.Rng}}};
    }

    private CampaignState BeginDoubleEvolution(CampaignState state)
    {
        var b=state.DoubleBattle!;
        if(b.Frame.Outcome!=WildBattleOutcome.Won)return ReturnFromDoubleBattle(state);
        var proposals=EligibleLevelEvolutions(state,b.LevelsBeforeBattle);
        return proposals.IsEmpty?ReturnFromDoubleBattle(state):state with{Evolution=new(b.Id,proposals,0,CampaignEvolutionStage.Proposal,[],0,[])};
    }

    private CampaignState ReturnFromDoubleBattle(CampaignState state)
    {
        var b=state.DoubleBattle!;
        if(b.Frame.TrainerTowerEncounter is not null)return ReturnFromTrainerTowerBattle(state,b.Frame,b.Id);
        if(!(state.DoubleBattleHistory??[]).Any(h=>h.Id==b.Id))throw new InvalidDataException("preset_double_history_missing");
        var next=state with{DoubleBattle=null,Dialogue=null,Motions=[],PresentationTicks=0,
            Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false}};
        if(b.Frame.Outcome!=WildBattleOutcome.Won)return WhiteOutFromRoute1(next);
        next=ClearVsSeekerLastTalked(next,b.VsSeeker);
        return next with{BattledTrainers=next.BattledTrainers.Add(b.Trainer.Value),
            Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",0)},
            Music=_content.MapDefinition(next.World.Player.MapId).Music,Exploration=ResetEncounterModifiers(next.Exploration!)};
    }

    private IEnumerable<TrainerId> SupportedDoubleTrainerIds => ExistingDoubleTrainerIds.Concat(
        _content.PresetDoublesEnabled && _content.Route16FlyTrainers is not null ? [Route16FlyDoubleTrainerEncounter.TrainerId] : Array.Empty<TrainerId>()).Concat(
            VsSeekerEnabled && _content.VsSeekerTrainers is { } vs ? vs.Trainers.Values.Where(t=>t.DoubleBattle).Select(t=>t.Id) : []).Distinct();
    private IEnumerable<TrainerId> ExistingDoubleTrainerIds=>!_content.PresetDoublesEnabled?[]:
        _content.SeafoamEnabled?[SourceTwinsTrainerEncounter.TrainerId,CinnabarDoubleTrainerEncounter.TrainerId,FirstSeviiDoubleTrainerEncounter.TrainerId,VictoryRoadDoubleTrainerEncounter.TrainerId,SeviiRubyDoubleTrainerEncounter.TrainerId,SixIslandDoubleTrainerEncounter.TrainerId,..SevenIslandDoubleTrainerEncounter.TrainerIds,..SeafoamDoubleTrainerEncounter.TrainerIds]:
        _content.SevenIslandEnabled?[SourceTwinsTrainerEncounter.TrainerId,CinnabarDoubleTrainerEncounter.TrainerId,FirstSeviiDoubleTrainerEncounter.TrainerId,VictoryRoadDoubleTrainerEncounter.TrainerId,SeviiRubyDoubleTrainerEncounter.TrainerId,SixIslandDoubleTrainerEncounter.TrainerId,..SevenIslandDoubleTrainerEncounter.TrainerIds]:
        _content.SixIslandEnabled?[SourceTwinsTrainerEncounter.TrainerId,CinnabarDoubleTrainerEncounter.TrainerId,FirstSeviiDoubleTrainerEncounter.TrainerId,VictoryRoadDoubleTrainerEncounter.TrainerId,SeviiRubyDoubleTrainerEncounter.TrainerId,SixIslandDoubleTrainerEncounter.TrainerId]:_content.SeviiRubyEnabled?[SourceTwinsTrainerEncounter.TrainerId,CinnabarDoubleTrainerEncounter.TrainerId,FirstSeviiDoubleTrainerEncounter.TrainerId,VictoryRoadDoubleTrainerEncounter.TrainerId,SeviiRubyDoubleTrainerEncounter.TrainerId]:_content.VictoryRoadEnabled?[SourceTwinsTrainerEncounter.TrainerId,CinnabarDoubleTrainerEncounter.TrainerId,FirstSeviiDoubleTrainerEncounter.TrainerId,VictoryRoadDoubleTrainerEncounter.TrainerId]:_content.FirstSeviiEnabled?[SourceTwinsTrainerEncounter.TrainerId,CinnabarDoubleTrainerEncounter.TrainerId,FirstSeviiDoubleTrainerEncounter.TrainerId]:_content.CinnabarEnabled?[SourceTwinsTrainerEncounter.TrainerId,CinnabarDoubleTrainerEncounter.TrainerId]:[SourceTwinsTrainerEncounter.TrainerId];
    private int TwinsWonFlagCount(CampaignState state)=>SupportedDoubleTrainerIds.Count(id=>state.BattledTrainers.Contains(id.Value));
    private bool SeenByTwins(CampaignState state,int species)=>(state.DoubleBattleHistory??[]).Any(h=>
        SupportedDoubleTrainerIds.Contains(new TrainerId(h.TrainerId))&&DoubleTrainer(new(h.TrainerId)).Party.Any(p=>p.SourceSpeciesId==species));
    private string? ValidateDoubleBattleHistory(CampaignState state)
    {
        var history=state.DoubleBattleHistory??[];
        var ids=SupportedDoubleTrainerIds.ToHashSet();
        var recorded=state.BattledTrainers.Where(id=>ids.Contains(new TrainerId(id))).ToHashSet(StringComparer.Ordinal);
        if(!_content.PresetDoublesEnabled)return history.Length!=0||state.BattledTrainers.Contains(SourceTwinsTrainerEncounter.TrainerId.Value)||state.DoubleBattle is not null?"save_preset_double_profile_missing":null;
        if(history.IsDefault)return "save_preset_double_history_invalid";
        var winners=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<history.Length;i++)
        {
            var h=history[i];
            if(h is null||!ids.Contains(new TrainerId(h.TrainerId))||winners.Contains(h.TrainerId)&&h.VsSeeker is null||h.Id!="double-trainer-battle:"+(i+1))return "save_preset_double_history_invalid";
            if(h.VsSeeker is {} receipt && !ValidVsSeekerHistory(state,receipt,h.TrainerId,h.Outcome,h.MoneyGained,h.MoneyMultiplier,winners,true))
                return "save_vs_seeker_double_history";
            if(h.VsSeeker is null && !ExistingDoubleTrainerIds.Contains(new(h.TrainerId)) &&
                !(_content.Route16FlyTrainers is not null && h.TrainerId==Route16FlyDoubleTrainerEncounter.TrainerId.Value))
                return "save_vs_seeker_double_origin";
            var trainer=DoubleTrainer(new(h.TrainerId));
            if(h.Outcome is not (WildBattleOutcome.Won or WildBattleOutcome.Lost or WildBattleOutcome.Draw)||h.ExperienceGained<0||
                h.ExperienceGained>trainer.Party.Sum(p=>DoubleBattlePokemon.Species[p.Species].ExperienceYield*p.Level/7*150/100)||
                !SourceBattleMoneyRules.ValidMultiplier(h.MoneyMultiplier)||h.MoneyGained<0||
                h.MoneyGained>(h.Outcome==WildBattleOutcome.Won?DoublePrize(trainer.Id)*(h.MoneyMultiplier??1):0)||
                h.OpponentsSeen.IsDefault||!h.OpponentsSeen.SequenceEqual(new[]{0,1})||
                trainer.Party.Any(p=>!state.SeenSpecies.Contains(p.SourceSpeciesId)))return "save_preset_double_history_invalid";
            if(h.Outcome==WildBattleOutcome.Won)winners.Add(h.TrainerId);
        }
        return recorded.SetEquals(winners)?null:"save_preset_double_flag_mismatch";
    }
}
