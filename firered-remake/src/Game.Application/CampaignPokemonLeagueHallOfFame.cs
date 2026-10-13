using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

public enum CampaignPokemonLeagueCompletionStage
{
    RecordingFieldEffect, AwaitingSave, SaveFailed, DisplayingHallOfFame, ExteriorCredits, Credits, ResetRequested
}
public sealed record CampaignPokemonLeagueCompletion(CampaignPokemonLeagueCompletionStage Stage,string RequestId,
    int ChallengeNumber,int PartyCount,bool FieldEffectCompleted,bool SaveSucceeded);
public sealed record CampaignHallOfFamePokemon(int PartySlot,long InstanceId,int SpeciesOrEgg,uint OriginalTrainerId,
    uint Personality,int Level,string Nickname,ImmutableArray<byte> NicknameBytes);
public sealed record CampaignHallOfFameTeam(int ChallengeNumber,string TrainerBattleId,ImmutableArray<CampaignHallOfFamePokemon> Members);
public sealed record CampaignHallOfFameArchive(ImmutableArray<CampaignHallOfFameTeam> Teams,int? FirstHallOfFamePlayTime,bool FanClubUpdateDeferred);
public sealed record CampaignHallOfFameSaveRequest(string RequestId,CampaignState ContinueState,long Generation,string PayloadHash);

/// <summary>Hall-of-Fame gameplay commits are independent from file I/O. The save
/// adapter must durably write the supplied Continue snapshot before acknowledging it.</summary>
public sealed partial class CampaignSession
{
    public CampaignHallOfFameSaveRequest? PokemonLeagueHallOfFameSaveRequest
    {
        get
        {
            if(State.PokemonLeagueCompletion is not{Stage:CampaignPokemonLeagueCompletionStage.AwaitingSave} pending)return null;
            var snapshot=PokemonLeagueContinueSnapshot(State);
            if(ValidatePlayerSave(snapshot) is {} error)throw new InvalidDataException("pokemon_league_continue_snapshot: "+error);
            return new(pending.RequestId,snapshot,Revision,CanonicalJson.Hash(snapshot));
        }
    }
    private static ImmutableArray<byte> PokemonLeagueNicknameBytes(string nickname)
    {
        // English revision-0 charmap, the existing ten-character naming alphabet.
        // Owned strings do not retain unused raw storage bytes; normalize that tail
        // to EOS while preserving all ten visible character bytes exactly.
        if(nickname.Length is <1 or >10)throw new InvalidDataException("pokemon_league_nickname_length");
        var bytes=Enumerable.Repeat((byte)255,10).ToArray();
        for(int i=0;i<nickname.Length;i++)bytes[i]=nickname[i] switch
        {
            >= 'A' and <= 'Z'=>(byte)(0xbb+nickname[i]-'A'),>= 'a' and <= 'z'=>(byte)(0xd5+nickname[i]-'a'),
            >= '0' and <= '9'=>(byte)(0xa1+nickname[i]-'0'),' '=>(byte)0,'.'=>(byte)0xad,','=>(byte)0xb8,
            '!'=>(byte)0xab,'?'=>(byte)0xac,'/'=>(byte)0xba,'-'=>(byte)0xae,'♂'=>(byte)0xb5,'♀'=>(byte)0xb6,
            '…'=>(byte)0xb0,'“'=>(byte)0xb1,'”'=>(byte)0xb2,'‘'=>(byte)0xb3,'\''=>(byte)0xb4,
            _=>throw new InvalidDataException("pokemon_league_nickname_encoding")
        };
        return bytes.ToImmutableArray();
    }
    private CampaignState EnterPokemonLeagueHallOfFame(CampaignState state)
    {
        if(state.PokemonLeagueCompletion is not{Stage:CampaignPokemonLeagueCompletionStage.RecordingFieldEffect,FieldEffectCompleted:true} pending||
            state.HallOfFame is not null||state.LastHealingLocation!="HEAL_LOCATION_PALLET_TOWN"||!state.ScreenIsBlack||
            state.Script.Variables.GetValueOrDefault("VAR_TEMP_1")!=1||state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR")||
            state.PokemonLeague?.Resets.LastOrDefault() is not{Reason:"hall-of-fame"} reset||reset.ChallengeNumber!=pending.ChallengeNumber)
            throw new InvalidDataException("pokemon_league_hof_entry_context");
        var champion=(state.ForestTrainerHistory??[]).LastOrDefault(h=>h.PokemonLeagueChallenge==pending.ChallengeNumber&&IsPokemonLeagueChampion(new(h.TrainerId))&&h.Outcome==WildBattleOutcome.Won)
            ??throw new InvalidDataException("pokemon_league_champion_receipt_missing");
        bool ribbon=state.Party.Any(p=>!p.IsEgg&&!p.ChampionRibbon);
        var party=state.Party.Select(p=>Game.Domain.FirstBattle.LabBattleRewards.Heal(BattlePokemon,p) with{ChampionRibbon=p.ChampionRibbon||!p.IsEgg}).ToImmutableArray();
        var members=party.Select((p,i)=>new CampaignHallOfFamePokemon(i,p.InstanceId,NameRaterRules.SpeciesOrEgg(p),p.Pokemon.OriginalTrainerId,
            p.Pokemon.Personality,p.Pokemon.Level,p.Nickname,PokemonLeagueNicknameBytes(p.Nickname))).ToImmutableArray();
        var flags=state.Script.Flags.SetItem("FLAG_SYS_GAME_CLEAR",true);
        var stats=state.GameStats??ImmutableSortedDictionary<string,int>.Empty;
        // SAVE_HALL_OF_FAME's statistic is prepared once with this record. A failed
        // write/retry reuses the same payload and never appends or awards again.
        stats=stats.SetItem("GAME_STAT_ENTERED_HOF",Math.Min(999,stats.GetValueOrDefault("GAME_STAT_ENTERED_HOF")+1));
        if(ribbon){flags=flags.SetItem("FLAG_SYS_RIBBON_GET",true);stats=stats.SetItem("GAME_STAT_RECEIVED_RIBBONS",Math.Min(0xffffff,stats.GetValueOrDefault("GAME_STAT_RECEIVED_RIBBONS")+1));}
        return state with{Party=party,GameStats=stats,Script=state.Script with{Flags=flags},
            HallOfFame=new([new(pending.ChallengeNumber,champion.Id,members)],null,true),
            PokemonLeagueCompletion=pending with{Stage=CampaignPokemonLeagueCompletionStage.AwaitingSave}};
    }
    private CampaignState PokemonLeagueContinueSnapshot(CampaignState state)
    {
        if(state.HallOfFame is null||state.PokemonLeagueCompletion is not{FieldEffectCompleted:true} completion||
            completion.Stage is CampaignPokemonLeagueCompletionStage.RecordingFieldEffect)throw new InvalidDataException("pokemon_league_continue_snapshot_context");
        var map=new MapId("map.pallet-town");var cell=_content.Maps[map].GetTile(6,8)??throw new InvalidDataException("pokemon_league_continue_tile");
        var next=state with{PokemonLeagueCompletion=null,World=state.World with{Player=new(map,6,8,cell.Elevation,Direction.South)},
            Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false,Variables=state.Script.Variables.SetItem("VAR_MAP_SCENE_INDIGO_PLATEAU_EXTERIOR",0),Flags=state.Script.Flags.Remove("FLAG_DONT_SHOW_MAP_NAME_POPUP")},
            Dialogue=null,Motions=[],PresentationTicks=0,PresentationEffects=[],ScreenIsBlack=false,ScreenIsWhite=false,PlayerVisible=true,
            LastTalked=null,Music=_content.MapDefinition(map).Music,SavedMusic="MUS_DUMMY",HelpContext="map",IsCycling=false,IsSurfing=false,HealingEffect=null,FieldEffectArguments=null,
            Exploration=ResetEncounterModifiers(state.Exploration!),MapEdits=null,SelectedObjectSlot=0};
        // The source Continue warp uses Pallet (6,8). Preserve Oak's original scene2,
        // but this first-clear profile deliberately stops before its postgame trigger.
        // Build the source object pool without running OnFrame/postgame scripts.
        next=EnterMap(next,Direction.South) with{EntryPrograms=[],ArrivalFacing=null};
        return RefreshActors(next);
    }
    private bool PokemonLeagueCompletedContinue(CampaignState state)=>_content.PokemonLeagueEnabled&&state.HallOfFame is not null&&
        state.PokemonLeagueCompletion is null&&
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR")&&(_content.NationalDexEnabled||
            !state.Script.Flags.GetValueOrDefault("FLAG_SYS_NATIONAL_DEX")&&state.Script.Variables.GetValueOrDefault("VAR_NATIONAL_DEX")==0);
    private bool TryDecidePokemonLeague(CampaignState state,CampaignCommand command,out Decision<CampaignState> decision)
    {
        decision=new(CommandStatus.Rejected,state,"pokemon_league_command_not_available");
        if(!_content.PokemonLeagueEnabled)return command.Kind.StartsWith("league_",StringComparison.Ordinal);
        var pending=state.PokemonLeagueCompletion;
        if(pending is null)
        {
            return command.Kind.StartsWith("league_",StringComparison.Ordinal);
        }
        Decision<CampaignState> Accepted(CampaignState result)=>new(CommandStatus.Accepted,result);
        if(pending.Stage==CampaignPokemonLeagueCompletionStage.RecordingFieldEffect)
        {
            if(pending.FieldEffectCompleted)return command.Kind.StartsWith("league_",StringComparison.Ordinal);
            if(command.Kind!="league_record_complete"||command.Quantity is <7 or >32767||state.Script.Wait is not{Kind:StoryWaitKind.Presentation} wait||
                command.Text!=wait.CorrelationId||!state.PresentationEffects.Any(e=>e.Opcode=="waitfieldeffect"&&e.Args.SequenceEqual(new[]{"FLDEFF_HALL_OF_FAME_RECORD"})))
            {decision=new(CommandStatus.Rejected,state,"pokemon_league_record_completion_required");return true;}
            var next=state with{PokemonLeagueCompletion=pending with{FieldEffectCompleted=true},PresentationEffects=[],PresentationTicks=0,Motions=[]};
            decision=Accepted(Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content))));return true;
        }
        if(pending.Stage==CampaignPokemonLeagueCompletionStage.ExteriorCredits)
            return command.Kind.StartsWith("league_",StringComparison.Ordinal);
        if(command.Text!=pending.RequestId)
        {decision=new(CommandStatus.Rejected,state,"pokemon_league_request_mismatch");return true;}
        switch(pending.Stage)
        {
            case CampaignPokemonLeagueCompletionStage.AwaitingSave:
                if(command.Kind=="league_save_result"&&command.Choice is 0 or 1)
                    decision=Accepted(state with{PokemonLeagueCompletion=pending with{Stage=command.Choice==1?CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame:CampaignPokemonLeagueCompletionStage.SaveFailed,SaveSucceeded=command.Choice==1}});
                else decision=new(CommandStatus.Rejected,state,"pokemon_league_save_result_required");
                return true;
            case CampaignPokemonLeagueCompletionStage.SaveFailed:
                decision=command.Kind=="league_save_retry"?Accepted(state with{PokemonLeagueCompletion=pending with{Stage=CampaignPokemonLeagueCompletionStage.AwaitingSave}}):new(CommandStatus.Rejected,state,"pokemon_league_save_retry_required");return true;
            case CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame:
                if(command.Kind!="league_hall_of_fame_exit"||!pending.SaveSucceeded){decision=new(CommandStatus.Rejected,state,"pokemon_league_display_exit_required");return true;}
                var map=new MapId("map.indigo-plateau-exterior");var tile=_content.Maps[map].GetTile(11,6)??throw new InvalidDataException("pokemon_league_credits_tile");
                var next=state with{PokemonLeagueCompletion=pending with{Stage=CampaignPokemonLeagueCompletionStage.ExteriorCredits},World=state.World with{Player=new(map,11,6,tile.Elevation,Direction.South)},
                    Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false,Variables=state.Script.Variables.SetItem("VAR_MAP_SCENE_INDIGO_PLATEAU_EXTERIOR",1),Flags=state.Script.Flags.SetItem("FLAG_DONT_SHOW_MAP_NAME_POPUP",true)},
                    ScreenIsBlack=false,ScreenIsWhite=false,Dialogue=null,PlayerVisible=true,Motions=[],PresentationTicks=0,PresentationEffects=[],Music="MUS_CREDITS"};
                decision=Accepted(Pump(EnterMap(next,Direction.South)));return true;
            case CampaignPokemonLeagueCompletionStage.Credits:
                if(command.Kind!="league_credits_complete"||!pending.SaveSucceeded){decision=new(CommandStatus.Rejected,state,"pokemon_league_credits_completion_required");return true;}
                decision=Accepted(state with{PokemonLeagueCompletion=pending with{Stage=CampaignPokemonLeagueCompletionStage.ResetRequested},
                    Script=state.Script with{Flags=state.Script.Flags.Remove("FLAG_DONT_SHOW_MAP_NAME_POPUP")},PresentationEffects=[],PresentationTicks=0,Motions=[]});return true;
            default:decision=new(CommandStatus.Rejected,state,"pokemon_league_soft_reset_required");return true;
        }
    }
    private string? ValidatePokemonLeagueHallOfFame(CampaignState state)
    {
        if(state.HallOfFame is not {} archive)
            return state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR")||OwnedPokemon(state).Any(p=>p.ChampionRibbon)||
                state.GameStats?.GetValueOrDefault("GAME_STAT_ENTERED_HOF")>0?"save_pokemon_league_hof_missing":null;
        if(!PokemonLeagueCompletedContinue(state)||archive.Teams.IsDefault||archive.Teams.Length!=1||archive.FirstHallOfFamePlayTime is not null||
            !archive.FanClubUpdateDeferred||!_content.NationalDexEnabled&&(state.LastHealingLocation!="HEAL_LOCATION_PALLET_TOWN"||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_OAK")!=2)||state.Script.Flags.GetValueOrDefault("FLAG_HIDE_POSTGAME_GOSSIPERS")||
            new[]{"FLAG_LUGIA_FLEW_AWAY","FLAG_HO_OH_FLEW_AWAY","FLAG_DEOXYS_FLEW_AWAY"}.Any(f=>state.Script.Flags.GetValueOrDefault(f))||
            state.GameStats?.GetValueOrDefault("GAME_STAT_ENTERED_HOF")!=1||!state.Script.Flags.GetValueOrDefault("FLAG_SYS_RIBBON_GET")||
            state.GameStats.GetValueOrDefault("GAME_STAT_RECEIVED_RIBBONS")!=1)return "save_pokemon_league_hof_state";
        var team=archive.Teams[0];
        if(team is null||team.Members.IsDefault||team.Members.Any(m=>m is null))return "save_pokemon_league_hof_team";
        var histories=(state.ForestTrainerHistory??[]).Where(h=>h is not null&&h.Id==team.TrainerBattleId).ToArray();
        var history=histories.Length==1?histories[0]:null;
        if(history is null||!IsPokemonLeagueChampion(new(history.TrainerId))||history.Outcome!=WildBattleOutcome.Won||history.PokemonLeagueChallenge!=team.ChallengeNumber||
            state.PokemonLeague is null||!state.PokemonLeague.Resets.Any(r=>r.Reason=="hall-of-fame"&&r.ChallengeNumber==team.ChallengeNumber)||
            team.Members.IsDefault||team.Members.Length is <1 or >6||team.Members.Select(m=>m.InstanceId).Distinct().Count()!=team.Members.Length)return "save_pokemon_league_hof_receipt";
        for(int i=0;i<team.Members.Length;i++)
        {
            var m=team.Members[i];var p=m is null?null:OwnedPokemon(state).SingleOrDefault(p=>p.InstanceId==m.InstanceId);
            if(m is null||p is null||m.PartySlot!=i||m.SpeciesOrEgg is <1 or >412||m.OriginalTrainerId!=p.Pokemon.OriginalTrainerId||
                m.Personality!=p.Pokemon.Personality||m.Level is <1 or >100||m.Level>p.Pokemon.Level||m.Nickname is null||m.NicknameBytes.IsDefault||
                m.SpeciesOrEgg!=NameRaterRules.SpeciesOrEgg(p)&&!(state.EvolutionHistory??[]).Any(e=>e.InstanceId==p.InstanceId&&e.PreviousSpeciesId==m.SpeciesOrEgg&&e.Level>=m.Level)||
                !m.NicknameBytes.SequenceEqual(PokemonLeagueNicknameBytes(m.Nickname))||!p.IsEgg&&!p.ChampionRibbon)
                return "save_pokemon_league_hof_member";
        }
        var ribbonIds=team.Members.Select(m=>m.InstanceId).ToHashSet();
        if(OwnedPokemon(state).Any(p=>p.ChampionRibbon&&!ribbonIds.Contains(p.InstanceId)))return "save_pokemon_league_hof_ribbon";
        return null;
    }
}
