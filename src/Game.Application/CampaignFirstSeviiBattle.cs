using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Replayable proof of the original CreateMon call, kept with its wild-history receipt.</summary>
public sealed record CampaignLostelleHypnoReceipt(string Map, string SourcePath, string ScriptEntry,
    int SourceLine, RngState GenerationStart, PokemonInstance Generated);
public sealed record CampaignLostelleHypnoPreparation(CampaignLostelleHypnoReceipt Receipt, Route1WildPokemonResult Generated);

public sealed partial class CampaignSession
{
    private const string LostelleMap="map.three-island-berry-forest";
    private const string LostelleRegion="MAPSEC_BERRY_FOREST";
    private const string BikerScene="ThreeIsland_EventScript_BattleBikersScene";
    private static readonly (string Trainer,int Line,string Actor)[] FirstSeviiBikers = [
        ("pret:TRAINER_BIKER_GOON",211,"LOCALID_THREE_ISLAND_BIKER1"),
        ("pret:TRAINER_BIKER_GOON_2",221,"LOCALID_THREE_ISLAND_BIKER2"),
        ("pret:TRAINER_BIKER_GOON_3",231,"LOCALID_THREE_ISLAND_BIKER3"),
        ("pret:TRAINER_CUE_BALL_PAXTON",248,"LOCALID_PAXTON")];

    private bool IsFirstSeviiBikerHistory(CampaignForestTrainerHistory history) =>
        _content.FirstSeviiEnabled && FirstSeviiBikers.Any(biker => biker.Trainer == history.TrainerId) &&
        RegisteredTrainerHistoryMatches(history);

    // Only this exact unconditional source sequence can replay a won trainer. The
    // next attempt starts at Goon after loss; the persistent party/RNG pass untouched.
    private int FirstSeviiNextBiker(CampaignState state)
    {
        int next=0;
        foreach(var h in state.ForestTrainerHistory??[])
        {
            if(h is null)throw new InvalidDataException("first_sevii_biker_history_null");
            if(!FirstSeviiBikers.Any(b=>b.Trainer==h.TrainerId))continue;
            if(!IsFirstSeviiBikerHistory(h)||next>=FirstSeviiBikers.Length||h.TrainerId!=FirstSeviiBikers[next].Trainer)
                throw new InvalidDataException("first_sevii_biker_history_order");
            next=h.Outcome==WildBattleOutcome.Won?next+1:h.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw?0:
                throw new InvalidDataException("first_sevii_biker_history_outcome");
        }
        return next;
    }

    private void RequireFirstSeviiBikerOccurrence(CampaignState state,TrainerId trainer,ForestTrainerBattleDefinition definition)
    {
        int next=FirstSeviiNextBiker(state);
        if(next>=FirstSeviiBikers.Length||definition.ScriptEntry!=BikerScene||definition.Source.StartLine!=FirstSeviiBikers[next].Line||
            trainer.Value!=FirstSeviiBikers[next].Trainer||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_THREE_ISLAND")!=3||
            state.LastTalked!=FirstSeviiBikers[next].Actor||!state.Actors.TryGetValue(state.LastTalked,out var actor)||
            !actor.Active||state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED")!=actor.SourceLocalId)
            throw new InvalidDataException("first_sevii_biker_source_occurrence");
    }

    private string? ValidateFirstSeviiTrainerHistory(CampaignState state)
    {
        try{_=FirstSeviiNextBiker(state);}catch(InvalidDataException){return "save_first_sevii_biker_order";}
        return null;
    }

    private bool IsFirstSeviiWildMap(MapId map)=>_content.FirstSeviiEnabled&&
        map.Value is "map.one-island" or "map.three-island-bond-bridge" or LostelleMap;

    private EarlyLandWildPokemonResult GenerateFirstSeviiLand(CampaignState state,EarlyLandEncounterTable table,EarlyLandEncounterState encounter)
    {
        if(FirstSeviiEncounterTables.Land.Any(t=>t.Map==table.Map))
            return FirstSeviiWildPokemonFactory.Generate(_content.CinnabarOwnership!,table,encounter,state.World.Rng,state.TrainerId);
        // The successor shares its one field encounter process with all predecessor
        // maps. Validate the actual successor candidate before ordinary generation.
        if(encounter.RulesVersion!=EarlyLandEncounterRules.FirstSeviiRulesVersion)throw new InvalidDataException("first_sevii_land_profile");
        EarlyLandEncounterRules.ValidateState(encounter,table);
        return CinnabarWildPokemonFactory.Generate(_content.CinnabarOwnership!,table,
            encounter with { RulesVersion=EarlyLandEncounterRules.CinnabarRulesVersion },state.World.Rng,state.TrainerId);
    }

    private static bool IsLostelleSource(StoryEffect effect)=>effect.Source.Path==FirstSeviiHypnoBattleContract.SourcePath&&
        effect.Source.Symbol==FirstSeviiHypnoBattleContract.ScriptEntry;

    private void RequireLostelleCaller(CampaignState state,StoryEffect effect)
    {
        if(!IsLostelleSource(effect)||state.World.Player.MapId.Value!=LostelleMap||state.Script.QuestLogState!=0||
            state.LastTalked is not {} actorId||!state.Actors.TryGetValue(actorId,out var actor)||!actor.Active||
            actor.ScriptId!=FirstSeviiHypnoBattleContract.ScriptEntry||actor.LocalId!="LOCALID_BERRY_FOREST_LOSTELLE"||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED")!=actor.SourceLocalId||
            state.Script.Flags.GetValueOrDefault("FLAG_RESCUED_LOSTELLE")||state.Script.Flags.GetValueOrDefault("FLAG_HIDE_LOSTELLE_IN_BERRY_FOREST"))
            throw new InvalidDataException("first_sevii_lostelle_caller");
    }

    private bool TryApplyFirstSeviiBattleEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.FirstSeviiEnabled||!IsLostelleSource(effect))return false;
        var args=effect.Args;
        int Value(string token)=>state.Script.Variables.TryGetValue(token,out int value)?value:int.TryParse(token,out value)?value:
            _content.TryConstant(token,out value)?value:throw new InvalidDataException("first_sevii_hypno_operand");
        if(effect.Opcode=="playmoncry")
        {
            RequireLostelleCaller(state,effect);
            if(effect.Source.Line!=22||args.Length!=2||Value(args[0])!=97||Value(args[1])!=2)
                throw new InvalidDataException("first_sevii_hypno_cry_source");
            return true;
        }
        if(effect.Opcode=="setwildbattle")
        {
            RequireLostelleCaller(state,effect);
            if(effect.Source.Line!=24||args.Length!=2||Value(args[0])!=97||Value(args[1])!=30||state.LostelleHypnoPreparation is not null||
                (state.Exploration?.Battles??[]).Any(b=>b.LostelleHypno is not null&&b.Outcome is WildBattleOutcome.Won or WildBattleOutcome.Escaped or WildBattleOutcome.Caught))
                throw new InvalidDataException("first_sevii_hypno_preparation_source");
            var made=FirstSeviiHypnoBattleContract.Create(_content.CinnabarOwnership!,state.TrainerId,state.World.Rng);
            var receipt=new CampaignLostelleHypnoReceipt(LostelleMap,effect.Source.Path,effect.Source.Symbol,effect.Source.Line,state.World.Rng,made.Pokemon);
            next=state with{LostelleHypnoPreparation=new(receipt,new(made.Pokemon,made.Rng,made.Trace)),World=state.World with{Rng=made.Rng}};
            return true;
        }
        if(effect.Opcode=="dowildbattle")
        {
            RequireLostelleCaller(state,effect);
            if(effect.Source.Line!=25||args.Length!=0||state.LostelleHypnoPreparation is not {} pending||
                state.Script.Wait?.Kind!=StoryWaitKind.Battle||state.World.Rng!=pending.Generated.GlobalRng)
                throw new InvalidDataException("first_sevii_hypno_start_source");
            next=BeginWildBattle(state with{LostelleHypnoPreparation=null},pending.Generated,lostelleHypno:pending.Receipt);
            return true;
        }
        if(effect.Opcode=="special"&&args.SequenceEqual(new[]{"QuestLog_CutRecording"}))
        {
            RequireLostelleCaller(state,effect);
            if(effect.Source.Line!=26||!(state.Exploration?.Battles.LastOrDefault() is {LostelleHypno:not null,Outcome:WildBattleOutcome.Won or WildBattleOutcome.Escaped or WildBattleOutcome.Caught}))
                throw new InvalidDataException("first_sevii_hypno_rescue_continuation");
            return true;
        }
        return false;
    }

    private CampaignState ReturnFromLostelleHypnoBattle(CampaignState state,CampaignWildBattle battle)
    {
        var outcome=FirstSeviiHypnoBattleContract.Resolve(battle.Frame!.Outcome);
        if(!outcome.ContinueRescue||outcome.WhiteOut||state.World.Player.MapId.Value!=LostelleMap||
            state.Script.Wait is not {Kind:StoryWaitKind.Battle} wait||
            !state.Exploration!.Battles.Any(h=>h.Id==battle.Id&&IsLostelleHypnoHistory(state,h)))
            throw new InvalidDataException("first_sevii_hypno_return");
        if(outcome.PreserveCapturedPokemon&&!(state.Exploration.Captured??[]).Any(c=>c.BattleId==battle.Id&&IsLostelleHypnoCapture(state,c)))
            throw new InvalidDataException("first_sevii_hypno_capture_not_delivered");
        var next=state with{WildBattle=null,Dialogue=null,Music=FieldTravelMusic(state),Exploration=ResetEncounterModifiers(state.Exploration)};
        return Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content)));
    }

    private bool IsLostelleHypnoHistory(CampaignState state,CampaignWildHistory history)
    {
        if(!_content.FirstSeviiEnabled||history.LostelleHypno is not {} receipt||history.Safari||history.ScriptedMarowak||history.ScriptedSnorlaxRoute!=0||
            history.SourceSpeciesId!=97||history.Level!=30||history.Outcome==WildBattleOutcome.Ongoing||!Enum.IsDefined(history.Outcome)||
            history.Outcome!=WildBattleOutcome.Won&&history.ExperienceGained!=0||receipt.Map!=LostelleMap||
            receipt.SourcePath!=FirstSeviiHypnoBattleContract.SourcePath||receipt.ScriptEntry!=FirstSeviiHypnoBattleContract.ScriptEntry||receipt.SourceLine!=24||
            receipt.GenerationStart.AlgorithmId!=Gen3Rng.AlgorithmId||receipt.GenerationStart.Cursor>ulong.MaxValue-4||
            receipt.GenerationStart.Cursor+4>state.World.Rng.Cursor||receipt.Generated is null||!FirstSeviiHypnoBattleContract.SourceInitialInstance(receipt.Generated))return false;
        var made=FirstSeviiHypnoBattleContract.Create(_content.CinnabarOwnership!,state.TrainerId,receipt.GenerationStart);
        return made.Rng.Cursor==receipt.GenerationStart.Cursor+4&&SameSourceOwnedPokemon(made.Pokemon,receipt.Generated);
    }

    private bool IsLostelleHypnoCapture(CampaignState state,CampaignCapturedPokemon capture)
    {
        var history=state.Exploration?.Battles.SingleOrDefault(b=>b.Id==capture.BattleId&&b.LostelleHypno is not null);
        if(history is null||!IsLostelleHypnoHistory(state,history)||history.Outcome!=WildBattleOutcome.Caught||capture.SourceSpeciesId!=97||
            capture.MetLevel!=30||capture.MetLocation!=LostelleRegion||capture.Personality!=history.LostelleHypno!.Generated.Personality||
            capture.Ivs!=history.LostelleHypno.Generated.Ivs)return false;
        var owned=OwnedPokemon(state).SingleOrDefault(p=>p.InstanceId==capture.InstanceId);
        return owned is not null&&SourceOwnedIdentity(owned.Pokemon,history.LostelleHypno.Generated)&&owned.MetLevel==30&&owned.MetLocation==LostelleRegion;
    }


    private string? ValidateFirstSeviiBattleProgress(CampaignState state)
    {
        if(state.LostelleHypnoPreparation is not null)return "save_first_sevii_hypno_active";
        bool completed=false;
        foreach(var history in state.Exploration?.Battles??[])
        {
            if(history is null)return "save_first_sevii_hypno_history_null";
            if(history.LostelleHypno is null)continue;
            if(completed||!IsLostelleHypnoHistory(state,history))return "save_first_sevii_hypno_receipt";
            completed=FirstSeviiHypnoBattleContract.Resolve(history.Outcome).ContinueRescue;
        }
        if(completed!=state.Script.Flags.GetValueOrDefault("FLAG_RESCUED_LOSTELLE"))return "save_first_sevii_hypno_rescue";
        return null;
    }
}
