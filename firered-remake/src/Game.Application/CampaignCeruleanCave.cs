using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignMewtwoReceipt(RngState RngBefore,RngState RngAfter,PokemonInstance InitialPokemon);
public sealed record CampaignMewtwoPreparation(CampaignMewtwoReceipt Receipt,Route1WildPokemonResult Generated);

public sealed partial class CampaignSession
{
    private const string MewtwoMap="map.cerulean-cave-b1f";
    private const string MewtwoSource="data/maps/CeruleanCave_B1F/scripts.inc";
    private const string MewtwoScript="CeruleanCave_B1F_EventScript_Mewtwo";
    private const string MewtwoHide="FLAG_HIDE_MEWTWO";
    private const string MewtwoFought="FLAG_FOUGHT_MEWTWO";
    private static bool IsMewtwoBattle(CampaignWildBattle battle)=>
        battle.Frame.WildEncounter?.CeruleanCave?.Kind==CeruleanCaveEncounterKind.Mewtwo;
    private bool SeenByMewtwo(CampaignState s,int species)=>species==150&&
        s.Exploration?.Battles.Any(IsMewtwoHistory)==true;

    private static void ValidateCeruleanCaveCatalog(ICampaignCatalog content)
    {
        if(!content.CeruleanCaveEnabled||content.Maps.Count!=275||content.CeruleanCaveOwnership is not{} owned||
            content.CeruleanCaveOpponents is not{} opponents||content.CeruleanCaveMachines is not{} machines||
            !ReferenceEquals(CeruleanCaveOwnedPokemonCatalog.For(owned.Catalog),owned)||
            !ReferenceEquals(owned.Predecessor,content.CelioOwnership)||!ReferenceEquals(content.SevenIslandOwnership?.Predecessor,owned)||
            !ReferenceEquals(CeruleanCaveOpponentPokemonCatalog.For(owned),opponents)||
            !ReferenceEquals(opponents.Predecessor,content.CelioOpponents)||
            !ReferenceEquals(CeruleanCaveMachineCatalog.For(machines.Catalog),machines)||
            !ReferenceEquals(machines.Pokemon,owned)||!ReferenceEquals(machines.Predecessor,content.CelioMachines))
            throw new InvalidDataException("cerulean_cave_source_binding");
        var program=content.FindProgram(MewtwoScript);
        if(program?.Instructions.Count(i=>i.Opcode=="setwildbattle"&&i.Op==StoryOp.Special&&
            i.Args.SequenceEqual(new[]{"SPECIES_MEWTWO","70"})&&i.Source.Path==MewtwoSource)!=1||
            program.Instructions.Count(i=>i.Opcode=="special"&&i.Op==StoryOp.TrainerBattle&&
            i.Args.SequenceEqual(new[]{"StartLegendaryBattle"}))!=1)
            throw new InvalidDataException("cerulean_cave_mewtwo_script_binding");
    }

    private void RequireMewtwoCaller(CampaignState s,StoryEffect e)
    {
        if(!_content.CeruleanCaveEnabled||s.World.Player.MapId.Value!=MewtwoMap||!CelioCompleted(s)||
            e.Source.Path!=MewtwoSource||e.Source.Symbol!=MewtwoScript||
            s.LastTalked is not{} actorId||!s.Actors.TryGetValue(actorId,out var actor)||actor.SourceLocalId!=3||
            actor.ScriptId!=MewtwoScript||s.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED")!=3)
            throw new InvalidDataException("mewtwo_script_context");
    }

    private bool TryApplyMewtwoEffect(CampaignState s,StoryEffect e,out CampaignState next)
    {
        next=s;
        if(!_content.CeruleanCaveEnabled||e.Source.Path!=MewtwoSource)return false;
        var a=e.Args;bool caller=e.Source.Symbol==MewtwoScript;
        if(e.Opcode=="specialvar"&&a.SequenceEqual(new[]{"VAR_RESULT","GetBattleOutcome"}))
        {
            if(s.World.Player.MapId.Value!=MewtwoMap||s.MewtwoReturn is not{} outcome||
                e.Source.Symbol is not(MewtwoScript or "CeruleanCave_B1F_EventScript_TryRemoveMewtwo"))
                throw new InvalidDataException("mewtwo_outcome_context");
            // The source OnResume and the suspended event both read gBattleOutcome.
            next=s with{MewtwoReturn=caller?null:s.MewtwoReturn,
                Script=s.Script with{Variables=s.Script.Variables.SetItem("VAR_RESULT",outcome)}};
            return true;
        }
        if(!caller)return false;
        if(e.Opcode=="special"&&a.SequenceEqual(new[]{"QuestLog_CutRecording"}))
        {RequireMewtwoCaller(s,e);return true;}
        if(e.Opcode=="waitbuttonpress")
        {
            RequireMewtwoCaller(s,e);
            if(a.Length!=0||s.MewtwoPreparation is not null)throw new InvalidDataException("mewtwo_button_context");
            return true;
        }
        if(e.Opcode=="playmoncry")
        {
            RequireMewtwoCaller(s,e);
            if(!a.SequenceEqual(new[]{"SPECIES_MEWTWO","CRY_MODE_ENCOUNTER"})||s.MewtwoPreparation is not null)
                throw new InvalidDataException("mewtwo_encounter_cry_context");
            return true;
        }
        if(e.Opcode=="setwildbattle")
        {
            RequireMewtwoCaller(s,e);
            if(!a.SequenceEqual(new[]{"SPECIES_MEWTWO","70"})||s.MewtwoPreparation is not null||s.MewtwoReturn is not null||
                s.Script.Flags.GetValueOrDefault(MewtwoFought)||s.Script.Flags.GetValueOrDefault(MewtwoHide)||s.Script.Flags.GetValueOrDefault(SpecialWildFlag))
                throw new InvalidDataException("mewtwo_preparation_context");
            var made=CeruleanCaveWildPokemonFactory.GenerateMewtwo(_content.CeruleanCaveOwnership!,s.World.Rng,s.TrainerId);
            next=s with{MewtwoPreparation=new(new(s.World.Rng,made.GlobalRng,made.Pokemon),made),World=s.World with{Rng=made.GlobalRng}};
            return true;
        }
        if(e.Opcode=="special"&&a.SequenceEqual(new[]{"StartLegendaryBattle"}))
        {
            RequireMewtwoCaller(s,e);
            if(s.MewtwoPreparation is not{} pending||s.Script.Wait?.Kind!=StoryWaitKind.Battle||
                !s.Script.Flags.GetValueOrDefault(SpecialWildFlag)||s.World.Rng!=pending.Generated.GlobalRng)
                throw new InvalidDataException("mewtwo_battle_start_context");
            next=BeginWildBattle(s with{MewtwoPreparation=null},ceruleanCaveOrigin:CeruleanCaveWildBattleContract.MewtwoOrigin,
                scriptedPokemon:pending.Generated,mewtwoReceipt:pending.Receipt);
            return true;
        }
        return false;
    }

    private bool MewtwoButtonWait(CampaignState state)=>_content.CeruleanCaveEnabled&&
        state.World.Player.MapId.Value==MewtwoMap&&state.Script.Wait?.Kind==StoryWaitKind.ButtonPress&&
        !state.Script.Stack.IsEmpty&&state.Script.Stack[^1].ProgramId==MewtwoScript;

    private Decision<CampaignState> DecideMewtwoButton(CampaignState state,CampaignCommand command)
    {
        if(!MewtwoButtonWait(state))return new(CommandStatus.Rejected,state,"mewtwo_button_not_pending");
        if(command.Text is not("A" or "B"))return new(CommandStatus.Rejected,state,"button_press_requires_a_or_b");
        var next=state with{PresentationEffects=[],PresentationTicks=0,Motions=[]};
        return new(CommandStatus.Accepted,Pump(Advance(next,StoryRules.Continue(next.Script,next.Script.Wait!.CorrelationId,_content))));
    }

    private CampaignState ReturnFromMewtwoBattle(CampaignState state,CampaignWildBattle battle)
    {
        if(state.World.Player.MapId.Value!=MewtwoMap||state.Script.Wait is not{Kind:StoryWaitKind.Battle} wait||
            !state.Script.Flags.GetValueOrDefault(SpecialWildFlag)||battle.MewtwoReceipt is null)
            throw new InvalidDataException("mewtwo_return_context");
        var caller=state.Script;
        var next=state with{WildBattle=null,Dialogue=null,Music=FieldTravelMusic(state),Exploration=ResetEncounterModifiers(state.Exploration!),
            MewtwoReturn=SnorlaxSourceOutcome(battle),Script=caller with{Stack=[],Wait=null,Completed=true}};
        if(_content.MapDefinition(state.World.Player.MapId).Programs.OnResume!="CeruleanCave_B1F_OnResume")
            throw new InvalidDataException("mewtwo_resume_source");
        next=StartStory(next,"CeruleanCave_B1F_OnResume");
        for(int i=0;i<8&&next.Script.Wait is{Kind:StoryWaitKind.NativeEffect} effect;i++)
            next=Advance(next,StoryRules.Continue(next.Script,effect.CorrelationId,_content));
        if(!next.Script.Completed||next.Script.Wait is not null||!next.Script.Stack.IsEmpty||
            battle.Frame.Outcome==WildBattleOutcome.Caught&&next.Actors[state.LastTalked!].Active)
            throw new InvalidDataException("mewtwo_resume_incomplete");
        next=next with{Script=next.Script with{Stack=caller.Stack,Wait=caller.Wait,Completed=caller.Completed,Locked=caller.Locked}};
        return Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content)));
    }

    private bool IsMewtwoHistory(CampaignWildHistory history)
    {
        if(history.CeruleanCaveOrigin!=CeruleanCaveWildBattleContract.MewtwoOrigin||history.MewtwoReceipt is not{} receipt||
            history.SourceSpeciesId!=150||history.Level!=70||history.Outcome==WildBattleOutcome.Ongoing||
            history.Outcome!=WildBattleOutcome.Won&&history.ExperienceGained!=0||
            receipt.RngBefore.AlgorithmId!=Gen3Rng.AlgorithmId||receipt.RngBefore.Cursor>ulong.MaxValue-4||
            receipt.InitialPokemon is null||!CeruleanCaveWildPokemonFactory.SourceInitialInstance(
                _content.CeruleanCaveOwnership!,CeruleanCaveWildBattleContract.MewtwoOrigin,receipt.InitialPokemon))return false;
        var made=CeruleanCaveWildPokemonFactory.GenerateMewtwo(_content.CeruleanCaveOwnership!,receipt.RngBefore,receipt.InitialPokemon.OriginalTrainerId);
        return made.GlobalRng==receipt.RngAfter&&SameSourceOwnedPokemon(made.Pokemon,receipt.InitialPokemon);
    }

    private string? ValidateCeruleanCaveProgress(CampaignState state)
    {
        if(state.MewtwoPreparation is not null||state.MewtwoReturn is not null)return "save_mewtwo_event_active";
        var battles=state.Exploration?.Battles??[];
        var attempts=battles.Where(b=>b.CeruleanCaveOrigin?.Kind==CeruleanCaveEncounterKind.Mewtwo).ToArray();
        bool fought=state.Script.Flags.GetValueOrDefault(MewtwoFought),hidden=state.Script.Flags.GetValueOrDefault(MewtwoHide);
        if((CeruleanCaveEncounterTables.IsCave(state.World.Player.MapId)||attempts.Length>0||fought||hidden||
            state.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_CERULEAN_CAVE_1F"))&&!CelioCompleted(state))return "save_cerulean_cave_locked";
        if(attempts.Any(b=>!IsMewtwoHistory(b)||b.MewtwoReceipt!.InitialPokemon.OriginalTrainerId!=state.TrainerId||
            b.MewtwoReceipt.RngAfter.Cursor>state.World.Rng.Cursor))return "save_mewtwo_history";
        var terminal=attempts.Where(b=>b.Outcome is WildBattleOutcome.Won or WildBattleOutcome.Caught).ToArray();
        if(terminal.Length>1||fought!=(terminal.Length==1)||fought&&(!hidden||attempts[^1]!=terminal[0]))return "save_mewtwo_completion";
        if(hidden&&!fought&&!attempts.Any(b=>b.Outcome==WildBattleOutcome.Escaped))return "save_mewtwo_hide_receipt";
        if(battles.Any(b=>b.MewtwoReceipt is not null&&b.CeruleanCaveOrigin?.Kind!=CeruleanCaveEncounterKind.Mewtwo))return "save_mewtwo_origin";
        if(state.World.Player.MapId.Value==MewtwoMap&&state.Actors.Values.Any(a=>a.ScriptId==MewtwoScript&&a.Active==hidden))return "save_mewtwo_actor";
        return null;
    }
}
