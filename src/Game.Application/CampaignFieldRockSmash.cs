using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Transient source field-effect presentation, owned by correlated acknowledgements.</summary>
public sealed record CampaignRockSmashEffect(int PartyIndex,long InstanceId,int SourceSpeciesId,string Nickname,
    string ActorId,TilePosition PlayerOrigin,string Phase,string WaitId);
public sealed record CampaignRockSmashReceipt(RockSmashWildOrigin Origin,RngState RngBefore,RngState RngAfter,PokemonInstance InitialPokemon);
public sealed record CampaignRockSmashPreparation(CampaignRockSmashReceipt Receipt,Route1WildPokemonResult Generated);
public static class FieldRockSmashSourcePresentation
{
    public const string ShowMon="show-mon",RockReady="rock-ready",RockBreak="rock-break",RockRemoved="rock-removed";
    public const int ShowMonCueFrames=FieldCutSourcePresentation.ShowMonCueFrames;
    public const int RockBreakCueFrames=FieldRockSmashSourceRules.RockBreakCueFrames;
}

public sealed partial class CampaignSession
{
    private const string RockSmashScriptPath="data/scripts/field_moves.inc";
    public bool CanUseFieldRockSmash(int partyIndex)=>FieldRockSmashCommandError(State,partyIndex) is null;

    private CampaignActor? AdjacentFieldRockSmashRock(CampaignState state)
    {
        if(_content.FieldRockSmash is not {} catalog)return null;
        var definition=_content.MapDefinition(state.World.Player.MapId);
        var live=state.Actors.Values.Select(a=>new FieldRockSmashActorState(
            new(a.LocalId,a.SourceLocalId,a.GraphicsId,a.X,a.Y,a.Elevation,a.MovementType,a.ScriptId,a.HideFlag),
            a.TemplateX,a.TemplateY,a.Active,a.Invisible)).ToImmutableArray();
        var source=catalog.FindFacingRock(definition,state.World.Player,live,state.Script.Flags);
        return source is null?null:state.Actors.Values.Single(a=>a.SourceLocalId==source.SourceLocalId);
    }
    private string? FieldRockSmashCommandError(CampaignState state,int index)
    {
        if(_content.FieldRockSmash is null)return "field_rock_smash_not_enabled";
        if(!IsStableFieldForParty(state)||state.RockSmashEffect is not null||state.RockSmashPreparation is not null||
            state.FieldEffectArguments is not null||state.Pc is not null||state.TownMap is not null||state.Repel is not null)
            return "field_rock_smash_event_active";
        if(index<0||index>=state.Party.Length)return "field_rock_smash_party_index_invalid";
        if(!FieldRockSmashSourceRules.Knows(state.Party[index]))return "field_rock_smash_move_missing";
        if(!state.Script.Flags.GetValueOrDefault(FieldRockSmashSourceRules.Badge))return "field_rock_smash_badge_required";
        return AdjacentFieldRockSmashRock(state) is null?"field_rock_smash_nothing_to_smash":null;
    }
    private Decision<CampaignState> DecideFieldRockSmash(CampaignState state,CampaignCommand command)
    {
        if(FieldRockSmashCommandError(state,command.PartyIndex) is {} error)return new(CommandStatus.Rejected,state,error);
        var rock=AdjacentFieldRockSmashRock(state)!;
        var next=state with{LastTalked=rock.LocalId,
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_LAST_TALKED",rock.SourceLocalId)},
            FieldEffectArguments=ImmutableSortedDictionary<int,int>.Empty.Add(0,command.PartyIndex)};
        return new(CommandStatus.Accepted,Pump(StartStory(next,FieldRockSmashSourceRules.PartyScript)));
    }
    private bool TryApplyFieldRockSmashEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(_content.FieldRockSmash is null||effect.Source.Path!=RockSmashScriptPath||effect.Source.Symbol is not
            ("EventScript_RockSmash" or "EventScript_FldEffRockSmash" or "EventScript_UseRockSmash" or
             "EventScript_RockSmashNoEncounter" or "EventScript_CantSmashRock" or "EventScript_DontSmashRock"))return false;
        var a=effect.Args;
        int Value(string token)=>int.TryParse(token,out var literal)?literal:state.Script.Variables.TryGetValue(token,out var value)?value:
            _content.TryConstant(token,out var constant)?constant:throw new InvalidDataException("field_rock_smash_value_missing:"+token);
        bool Is(string token,string symbol)=>token==symbol||_content.TryConstant(symbol,out var expected)&&Value(token)==expected;
        CampaignActor Rock()
        {
            var rock=AdjacentFieldRockSmashRock(state);
            if(rock is null||state.LastTalked!=rock.LocalId||state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED")!=rock.SourceLocalId)
                throw new InvalidDataException("field_rock_smash_source_actor");
            return rock;
        }
        switch(effect.Opcode)
        {
            case "lockall":Rock();return true;
            case "checkpartymove":
                Rock();if(a.Length!=1||!Is(a[0],"MOVE_ROCK_SMASH"))throw new InvalidDataException("field_rock_smash_move");
                int selected=FieldRockSmashSourceRules.FirstPartyMember(state.Party);
                var vars=state.Script.Variables.SetItem("VAR_RESULT",selected);
                if(selected!=6)vars=vars.SetItem("VAR_0x8004",state.Party[selected].Pokemon.SourceSpeciesId);
                next=state with{Script=state.Script with{Variables=vars}};return true;
            case "setfieldeffectargument":
                Rock();if(a.Length!=2||Value(a[0])!=0)throw new InvalidDataException("field_rock_smash_argument");
                int argument=unchecked((short)Value(a[1]));
                if(argument<0||argument>=state.Party.Length||!FieldRockSmashSourceRules.Knows(state.Party[argument]))throw new InvalidDataException("field_rock_smash_party_argument");
                next=state with{FieldEffectArguments=ImmutableSortedDictionary<int,int>.Empty.Add(0,argument)};return true;
            case "bufferpartymonnick":
                Rock();if(a.Length!=2||a[0]!="STR_VAR_1"||Value(a[1]) is <0 or >=6||Value(a[1])>=state.Party.Length)throw new InvalidDataException("field_rock_smash_nickname");
                next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],PokemonName(state.Party[Value(a[1])]))};return true;
            case "buffermovename":
                Rock();if(a.Length!=2||a[0]!="STR_VAR_2"||!Is(a[1],"MOVE_ROCK_SMASH"))throw new InvalidDataException("field_rock_smash_move_buffer");
                next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],_content.BattleContent!.MoveNames[FieldRockSmashSourceRules.Move])};return true;
            case "dofieldeffect":
                var rock=Rock();
                if(a.Length!=1||!Is(a[0],"FLDEFF_USE_ROCK_SMASH")||state.RockSmashEffect is not null||state.RockSmashPreparation is not null||
                    !state.Script.Flags.GetValueOrDefault(FieldRockSmashSourceRules.Badge)||state.FieldEffectArguments is not {} arguments||
                    !arguments.TryGetValue(0,out int index)||index<0||index>=state.Party.Length||!FieldRockSmashSourceRules.Knows(state.Party[index]))
                    throw new InvalidDataException("field_rock_smash_effect");
                var mon=state.Party[index];var stats=state.GameStats??ImmutableSortedDictionary<string,int>.Empty.WithComparers(StringComparer.Ordinal);
                next=state with{RockSmashEffect=new(index,mon.InstanceId,mon.Pokemon.SourceSpeciesId,PokemonName(mon),rock.LocalId,state.World.Player,FieldRockSmashSourcePresentation.ShowMon,""),
                    GameStats=stats.SetItem(FieldRockSmashSourceRules.UsageStat,Math.Min(0xffffff,stats.GetValueOrDefault(FieldRockSmashSourceRules.UsageStat)+1)),
                    PresentationTicks=FieldRockSmashSourcePresentation.ShowMonCueFrames};return true;
            case "waitstate":
                if(a.Length!=0||state.Script.Wait is not {Kind:StoryWaitKind.Presentation} wait)throw new InvalidDataException("field_rock_smash_wait");
                if(state.RockSmashPreparation is {} pending)
                {
                    if(effect.Source.Symbol!=FieldRockSmashSourceRules.RemovalScript||state.World.Rng!=pending.Generated.GlobalRng||state.RockSmashEffect is not null)
                        throw new InvalidDataException("field_rock_smash_battle_wait");
                    next=BeginWildBattle(state with{RockSmashPreparation=null,Script=state.Script with{Wait=wait with{Kind=StoryWaitKind.Battle}}},
                        scriptedPokemon:pending.Generated,rockSmashReceipt:pending.Receipt);return true;
                }
                Rock();if(state.RockSmashEffect is not {Phase:FieldRockSmashSourcePresentation.ShowMon} show)throw new InvalidDataException("field_rock_smash_show_wait");
                next=state with{RockSmashEffect=show with{WaitId=wait.CorrelationId}};return true;
            case "applymovement":
                var actor=Rock();
                if(a.Length!=2||Value(a[0])!=actor.SourceLocalId||a[1]!="Movement_BreakRock"||state.RockSmashEffect is not {Phase:FieldRockSmashSourcePresentation.RockReady} ready||
                    !_content.Movement(a[1]).SequenceEqual(new[]{"rock_smash_break","step_end"}))throw new InvalidDataException("field_rock_smash_movement");
                next=state with{RockSmashEffect=ready with{Phase=FieldRockSmashSourcePresentation.RockBreak,WaitId=""},
                    Motions=state.Motions.Add(new(actor.LocalId,[new(state.World.Player.MapId,actor.X,actor.Y,actor.Elevation,actor.Facing)],_content.Movement(a[1]))),
                    PresentationTicks=FieldRockSmashSourcePresentation.RockBreakCueFrames};return true;
            case "waitmovement":
                Rock();if(a.Length!=1||Value(a[0])!=0||state.RockSmashEffect is not {Phase:FieldRockSmashSourcePresentation.RockBreak} breaking||state.Script.Wait is not {Kind:StoryWaitKind.Presentation} movementWait)
                    throw new InvalidDataException("field_rock_smash_break_wait");
                next=state with{RockSmashEffect=breaking with{WaitId=movementWait.CorrelationId}};return true;
            case "special" when a.SequenceEqual(new[]{"RockSmashWildEncounter"}):
                if(effect.Source.Symbol!=FieldRockSmashSourceRules.RemovalScript||state.RockSmashEffect is not {Phase:FieldRockSmashSourcePresentation.RockRemoved} removed||
                    state.World.Player!=removed.PlayerOrigin||!state.Actors.TryGetValue(removed.ActorId,out var hidden)||hidden.Active||
                    hidden.HideFlag!="0"&&!state.Script.Flags.GetValueOrDefault(hidden.HideFlag)||state.RockSmashPreparation is not null)
                    throw new InvalidDataException("field_rock_smash_encounter_context");
                next=CheckRockSmashEncounter(state with{RockSmashEffect=null,FieldEffectArguments=null});return true;
            case "releaseall":
                next=state with{RockSmashEffect=null,RockSmashPreparation=null,FieldEffectArguments=null,Dialogue=null,SignMessage=null,MessagePointer=null};return true;
            default:return false;
        }
    }
    private CampaignState CompleteFieldRockSmashPresentation(CampaignState before,CampaignState next)
    {
        if(before.RockSmashEffect is not {} effect)
        {
            // The source MSGBOX_SIGN macro releases controls; the compact interpreter exposes its text wait.
            if(before.Script.Wait?.Kind==StoryWaitKind.Dialog&&before.Dialogue is {TextSymbol:"Text_MonMaySmashRock",CloseOnCompletion:true}&&
                !before.Script.Stack.IsEmpty&&before.Script.Stack[^1].ProgramId=="EventScript_CantSmashRock")
                return next with{Script=next.Script with{Locked=false},Dialogue=null,SignMessage=null,MessagePointer=null,FieldEffectArguments=null};
            return next;
        }
        if(before.Script.Wait is not {Kind:StoryWaitKind.Presentation} wait||wait.CorrelationId!=effect.WaitId||
            before.World.Player!=effect.PlayerOrigin||effect.PartyIndex<0||effect.PartyIndex>=before.Party.Length||
            before.Party[effect.PartyIndex].InstanceId!=effect.InstanceId||!FieldRockSmashSourceRules.Knows(before.Party[effect.PartyIndex])||
            AdjacentFieldRockSmashRock(before)?.LocalId!=effect.ActorId)throw new InvalidDataException("field_rock_smash_presentation_context");
        if(effect.Phase==FieldRockSmashSourcePresentation.ShowMon&&before.PresentationEffects.Any(e=>e.Opcode=="waitstate"))
        {
            long sequence=checked(next.Script.EffectSequence+1);int facing=SourceDirectionValue(before.World.Player.Facing);
            return next with{RockSmashEffect=effect with{Phase=FieldRockSmashSourcePresentation.RockReady,WaitId=""},
                FieldEffectArguments=ImmutableSortedDictionary<int,int>.Empty.Add(0,effect.SourceSpeciesId).Add(1,facing).Add(2,facing-1),
                Script=next.Script with{EffectSequence=sequence},PresentationEffects=next.PresentationEffects.Add(new(sequence,"playse",before.World.Player.MapId,[FieldRockSmashSourceRules.EffectSound]))};
        }
        if(effect.Phase==FieldRockSmashSourcePresentation.RockBreak&&before.PresentationEffects.Any(e=>e.Opcode=="waitmovement"))
        {
            var actor=next.Actors[effect.ActorId];
            return next with{Actors=next.Actors.SetItem(actor.LocalId,actor with{Invisible=true}),
                RockSmashEffect=effect with{Phase=FieldRockSmashSourcePresentation.RockRemoved,WaitId=""}};
        }
        throw new InvalidDataException("field_rock_smash_presentation_wait");
    }
    private CampaignState CheckRockSmashEncounter(CampaignState state)
    {
        var early=state.Exploration?.EarlyEncounter??throw new InvalidDataException("field_rock_smash_encounter_missing");
        if(early.Pending is not null)throw new InvalidDataException("field_rock_smash_walk_pending");
        var result=RockSmashEncounterRules.Check(_content.FieldRockSmash!,state.World.Player.MapId,
            new(early.RateRng,early.EncounterRateBuff,state.IsCycling,CachedLeadHasCleanseTag:early.CachedLeadHasCleanseTag),state.Party,
            checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)),state.World.Rng);
        var next=state with{World=state.World with{Rng=result.GlobalRng},Exploration=state.Exploration! with{EarlyEncounter=early with{RateRng=result.Context.RateRng}},
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",result.Outcome==RockSmashEncounterOutcome.CandidateReady?1:0)}};
        if(result.Candidate is not {} candidate)return next;
        var origin=new RockSmashWildOrigin(candidate.Map,candidate.Slot);
        var made=RockSmashWildBattleContract.Generate(_binding.Ownership,_content.FieldRockSmash!,origin,candidate.Level,result.GlobalRng,state.TrainerId);
        return next with{World=next.World with{Rng=made.GlobalRng},RockSmashPreparation=new(new(origin,result.GlobalRng,made.GlobalRng,made.Pokemon),made)};
    }
    private CampaignState ReturnFromRockSmashBattle(CampaignState state,CampaignWildBattle battle)
    {
        if(state.Script.Wait is not {Kind:StoryWaitKind.Battle} wait||state.Script.Stack.IsEmpty||
            state.Script.Stack[^1].ProgramId!=FieldRockSmashSourceRules.RemovalScript||battle.RockSmashReceipt?.Origin!=battle.Frame.WildEncounter?.RockSmash)
            throw new InvalidDataException("field_rock_smash_return_context");
        var next=state with{WildBattle=null,Dialogue=null,Music=FieldTravelMusic(state),Exploration=ResetEncounterModifiers(state.Exploration!)};
        return Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content)));
    }
    private string? ValidateFieldRockSmashState(CampaignState state)=>state.RockSmashEffect is not null||state.RockSmashPreparation is not null
        ?"save_field_rock_smash_active":_content.FieldRockSmash is null&&state.GameStats?.ContainsKey(FieldRockSmashSourceRules.UsageStat)==true?"save_field_rock_smash_profile":null;
}
