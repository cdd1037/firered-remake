using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Transient acknowledgement-owned field-move presentation; never a player-save checkpoint.</summary>
public sealed record CampaignCutEffect(int PartyIndex,int SourceSpeciesId,string Nickname,string ActorId,string Phase);

/// <summary>
/// Source animation parameters, not a claim about GBA task/VBlank scheduling.
/// fldeff_rocksmash.c, field_effect.c, object_event_anims.h and event_object_movement.c at 037335.
/// </summary>
public static class FieldCutSourcePresentation
{
    public const string ShowMon="show-mon",TreeReady="tree-ready",TreeCut="tree-cut";
    public static ImmutableArray<int> PlayerFrameDurations => [4,4,4,4,8];
    public const int OutdoorBannerOpenFrames=20;
    public const int MonStartX=320,MonCenterX=120,MonY=80,MonSlidePixelsPerFrame=20;
    public const int MonSlideInFrames=10,MonHoldFrames=30,MonExitThresholdX=-64,MonSlideOutFrames=11;
    public const int OutdoorBannerCloseFrames=7;
    public const int TreeAnimationFrames=4,TreeFrameDuration=6,TreeBlinkFrames=32;
    // Sum only the source visual phases. Renderer/task setup and cleanup are completion boundaries.
    public const int ShowMonCueFrames=24+OutdoorBannerOpenFrames+MonSlideInFrames+MonHoldFrames+MonSlideOutFrames+OutdoorBannerCloseFrames;
    public const int TreeCutCueFrames=TreeAnimationFrames*TreeFrameDuration+TreeBlinkFrames;
}

public sealed partial class CampaignSession
{
    private const string FieldCutSource="data/scripts/field_moves.inc";
    private const string FieldCutMap="map.vermilion-city";
    private const string FieldCutMove="pret:MOVE_CUT";
    private const string CutUsageStat="GAME_STAT_USED_CUT";

    /// <summary>The party menu can show an enabled Cut action at this admitted source tree.</summary>
    public bool CanUseFieldCut(int partyIndex)=>FieldCutCommandError(State,partyIndex) is null;

    private static bool KnowsFieldCut(PartyPokemon mon)=>!mon.IsEgg&&mon.Pokemon.Moves.Any(m=>m.Move.Value==FieldCutMove);

    private CampaignActor? AdjacentFieldCutTree(CampaignState state)
    {
        var p=state.World.Player;
        if(p.MapId.Value!=FieldCutMap&&!(_content.Route11Enabled&&p.MapId.Value=="map.route-2")&&!(_content.CeladonGymEnabled&&p.MapId.Value=="map.celadon-city-gym")&&!(_content.CeladonApproachEnabled&&p.MapId.Value is "map.route-8" or "map.celadon-city")&&!(_content.RockTunnelEnabled&&p.MapId.Value is "map.route-9" or "map.route-10")&&!(_content.FuchsiaRouteEnabled&&p.MapId.Value is "map.route-12" or "map.route-13" or "map.route-14" or "map.fuchsia-city"))return null;
        bool Admitted(CampaignActor actor)=>p.MapId.Value==FieldCutMap
            ? actor.SourceLocalId==7&&actor.X==19&&actor.Y==24&&actor.Elevation==3&&actor.HideFlag=="FLAG_TEMP_12"
            : p.MapId.Value=="map.celadon-city-gym" ? (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag) is (9,6,8,3,"FLAG_TEMP_12") or (10,3,5,3,"FLAG_TEMP_13") or (11,9,6,3,"FLAG_TEMP_14")
            : p.MapId.Value=="map.route-8" ? (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag) is (10,33,15,3,"FLAG_TEMP_12") or (11,47,12,3,"FLAG_TEMP_13")
            : p.MapId.Value=="map.celadon-city" ? (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag) is (10,52,22,3,"FLAG_TEMP_14") or (11,40,35,0,"FLAG_TEMP_13")
            : p.MapId.Value=="map.route-9" ? (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag)==(10,2,8,3,"FLAG_TEMP_12")
            : p.MapId.Value=="map.route-10" ? (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag) is
                (7,10,20,0,"FLAG_TEMP_12") or (8,10,22,0,"FLAG_TEMP_13") or (9,10,24,0,"FLAG_TEMP_14") or (10,10,26,0,"FLAG_TEMP_15")
            : p.MapId.Value=="map.route-12" ? (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag) is
                (9,11,100,3,"FLAG_TEMP_12") or (14,12,111,3,"FLAG_TEMP_13")
            : p.MapId.Value=="map.route-13" ? (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag)==(11,44,5,3,"FLAG_TEMP_12")
            : p.MapId.Value=="map.route-14" ? (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag) is
                (11,7,26,3,"FLAG_TEMP_12") or (12,12,35,3,"FLAG_TEMP_13") or (13,1,47,3,"FLAG_TEMP_14")
            : p.MapId.Value=="map.fuchsia-city" ? (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag) is
                (11,30,10,3,"FLAG_TEMP_12") or (12,21,13,3,"FLAG_TEMP_13") or (13,32,16,3,"FLAG_TEMP_14") or (14,24,22,3,"FLAG_TEMP_15")
            : (actor.SourceLocalId,actor.X,actor.Y,actor.Elevation,actor.HideFlag) is
                (1,16,62,3,"FLAG_TEMP_14") or (2,15,69,3,"FLAG_TEMP_13") or
                (3,11,13,3,"FLAG_TEMP_15") or (4,18,26,3,"FLAG_TEMP_16");
        var (dx,dy)=Delta(p.Facing);
        return state.Actors.Values.SingleOrDefault(a=>Admitted(a)&&a.Active&&!a.Invisible&&
            a.GraphicsId=="OBJ_EVENT_GFX_CUT_TREE"&&a.ScriptId=="EventScript_CutTree"&&
            a.X==p.X+dx&&a.Y==p.Y+dy&&(p.Elevation==0||a.Elevation==0||p.Elevation==a.Elevation)&&
            !state.Script.Flags.GetValueOrDefault(a.HideFlag));
    }

    private string? FieldCutCommandError(CampaignState state,int partyIndex)
    {
        if(!_content.VermilionGymEnabled)return "field_cut_not_enabled";
        if(!IsStableFieldForParty(state)||state.CutEffect is not null||state.Pc is not null||state.TownMap is not null||state.Repel is not null)
            return "field_cut_event_active";
        if(partyIndex<0||partyIndex>=state.Party.Length)return "field_cut_party_index_invalid";
        if(!KnowsFieldCut(state.Party[partyIndex]))return "field_cut_move_missing";
        if(!state.Script.Flags.GetValueOrDefault("FLAG_BADGE02_GET"))return "field_cut_badge_required";
        return AdjacentFieldCutTree(state) is null?"field_cut_nothing_to_cut":null;
    }

    private Decision<CampaignState> DecideFieldCut(CampaignState state,CampaignCommand command)
    {
        var error=FieldCutCommandError(state,command.PartyIndex);
        if(error is not null)return new(CommandStatus.Rejected,state,error);
        var tree=AdjacentFieldCutTree(state)!;
        // FieldCallback_CutTree stores the selected menu member, then starts this entry.
        // Unlike direct interaction, this source path has no Yes/No or used-move textbox.
        // CheckObjectGraphicsInFrontOfPlayer changes LastTalked, not gSelectedObjectEventId.
        var context=state with
        {
            LastTalked=tree.LocalId,
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_LAST_TALKED",tree.SourceLocalId)},
            FieldEffectArguments=ImmutableSortedDictionary<int,int>.Empty.Add(0,command.PartyIndex)
        };
        return new(CommandStatus.Accepted,Pump(StartStory(context,"EventScript_FldEffCut")));
    }

    private bool TryApplyFieldCutEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(!_content.VermilionGymEnabled||effect.Source.Path!=FieldCutSource||effect.Source.Symbol is not
            ("EventScript_CutTree" or "EventScript_FldEffCut" or "EventScript_CutTreeDown" or "EventScript_CantCutTree" or "EventScript_DontCutTree"))return false;
        var a=effect.Args;
        int Value(string token)=>int.TryParse(token,out var literal)?literal:state.Script.Variables.TryGetValue(token,out var value)?value:
            _content.TryConstant(token,out var constant)?constant:throw new InvalidDataException("field_cut_value_missing: "+token);
        bool Is(string token,string symbol)=>token==symbol||_content.TryConstant(symbol,out var expected)&&Value(token)==expected;
        void RequireTree()
        {
            var tree=AdjacentFieldCutTree(state);
            if(tree is null||state.LastTalked!=tree.LocalId||state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED")!=tree.SourceLocalId)
                throw new InvalidDataException("field_cut_tree_context_invalid");
        }
        void RequireBuffer(string target)
        {if(target is not ("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3"))throw new InvalidDataException("field_cut_buffer_invalid");}
        switch(effect.Opcode)
        {
            case "checkpartymove":
                RequireTree();if(!Is(a[0],"MOVE_CUT"))throw new InvalidDataException("field_cut_move_unsupported");
                int selected=Enumerable.Range(0,state.Party.Length).FirstOrDefault(i=>KnowsFieldCut(state.Party[i]),6);
                var variables=state.Script.Variables.SetItem("VAR_RESULT",selected);
                // ScrCmd_checkpartymove leaves 0x8004 untouched when no member knows it.
                if(selected!=6)variables=variables.SetItem("VAR_0x8004",state.Party[selected].Pokemon.SourceSpeciesId);
                next=state with{Script=state.Script with{Variables=variables}};return true;
            case "setfieldeffectargument":
                RequireTree();if(Value(a[0])!=0)throw new InvalidDataException("field_cut_argument_unsupported");
                int argument=unchecked((short)Value(a[1]));
                if(argument<0||argument>=state.Party.Length||!KnowsFieldCut(state.Party[argument]))throw new InvalidDataException("field_cut_party_argument_invalid");
                next=state with{FieldEffectArguments=(state.FieldEffectArguments??ImmutableSortedDictionary<int,int>.Empty).SetItem(0,argument)};return true;
            case "bufferpartymonnick":
                RequireTree();RequireBuffer(a[0]);int index=Value(a[1]);
                if(index<0||index>=state.Party.Length)throw new InvalidDataException("field_cut_nickname_index_invalid");
                next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],state.Party[index].Nickname)};return true;
            case "buffermovename":
                RequireTree();RequireBuffer(a[0]);if(!Is(a[1],"MOVE_CUT"))throw new InvalidDataException("field_cut_move_unsupported");
                next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],_content.BattleContent!.MoveNames[FieldCutMove])};return true;
            case "dofieldeffect":
                RequireTree();
                if(!Is(a[0],"FLDEFF_USE_CUT_ON_TREE")||state.CutEffect is not null||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE02_GET")||
                    state.FieldEffectArguments is not {} args||!args.TryGetValue(0,out int partyIndex)||partyIndex<0||partyIndex>=state.Party.Length||!KnowsFieldCut(state.Party[partyIndex]))
                    throw new InvalidDataException("field_cut_effect_invalid");
                var mon=state.Party[partyIndex];var stats=state.GameStats??ImmutableSortedDictionary<string,int>.Empty.WithComparers(StringComparer.Ordinal);
                next=state with
                {
                    CutEffect=new(partyIndex,mon.Pokemon.SourceSpeciesId,mon.Nickname,state.LastTalked!,FieldCutSourcePresentation.ShowMon),
                    GameStats=stats.SetItem(CutUsageStat,Math.Min(0xffffff,stats.GetValueOrDefault(CutUsageStat)+1)),
                    PresentationTicks=FieldCutSourcePresentation.ShowMonCueFrames
                };return true;
            case "waitstate":
                RequireTree();if(state.CutEffect?.Phase!=FieldCutSourcePresentation.ShowMon)throw new InvalidDataException("field_cut_wait_without_effect");return true;
            case "applymovement":
                RequireTree();
                if(a.Length!=2||Value(a[0])!=AdjacentFieldCutTree(state)!.SourceLocalId||a[1]!="Movement_CutTreeDown"||state.CutEffect?.Phase!=FieldCutSourcePresentation.TreeReady||
                    !_content.Movement(a[1]).SequenceEqual(new[]{"cut_tree","step_end"}))throw new InvalidDataException("field_cut_movement_invalid");
                var actor=state.Actors[state.CutEffect.ActorId];var position=new TilePosition(state.World.Player.MapId,actor.X,actor.Y,actor.Elevation,actor.Facing);
                next=state with
                {
                    CutEffect=state.CutEffect with{Phase=FieldCutSourcePresentation.TreeCut},
                    Motions=state.Motions.Add(new(actor.LocalId,[position],_content.Movement(a[1]))),
                    PresentationTicks=FieldCutSourcePresentation.TreeCutCueFrames
                };return true;
            case "waitmovement":
                RequireTree();if(a.Length!=1||Value(a[0])!=0||state.CutEffect?.Phase!=FieldCutSourcePresentation.TreeCut)throw new InvalidDataException("field_cut_movement_wait_invalid");return true;
            case "releaseall":
                // Field-effect arguments are temporary source RAM, not player-save fields.
                next=state with{FieldEffectArguments=null,CutEffect=null,Dialogue=null,SignMessage=null,MessagePointer=null,GiftDelivery=null};return true;
            default:return false;
        }
    }

    /// <summary>Call after generic ack cleanup and before StoryRules.Continue.</summary>
    private CampaignState CompleteFieldCutPresentation(CampaignState before,CampaignState next)
    {
        if(before.CutEffect is not {} cut||before.Script.Wait?.Kind!=StoryWaitKind.Presentation)return next;
        if(cut.Phase==FieldCutSourcePresentation.ShowMon&&before.PresentationEffects.Any(e=>e.Opcode=="waitstate"))
        {
            // FieldMoveCallback_CutTree: SE_M_CUT, remove active effect, enable script.
            long sequence=checked(next.Script.EffectSequence+1);int facing=SourceDirectionValue(before.World.Player.Facing);
            return next with
            {
                CutEffect=cut with{Phase=FieldCutSourcePresentation.TreeReady},
                FieldEffectArguments=ImmutableSortedDictionary<int,int>.Empty.Add(0,cut.SourceSpeciesId).Add(1,facing).Add(2,facing-1),
                Script=next.Script with{EffectSequence=sequence},
                PresentationEffects=next.PresentationEffects.Add(new(sequence,"playse",before.World.Player.MapId,["SE_M_CUT"]))
            };
        }
        if(cut.Phase==FieldCutSourcePresentation.TreeCut&&before.PresentationEffects.Any(e=>e.Opcode=="waitmovement"))
        {
            var actor=next.Actors[cut.ActorId];
            // The 32nd blink finishes invisible; source removeobject runs only after this acknowledgement.
            return next with{Actors=next.Actors.SetItem(actor.LocalId,actor with{Invisible=true}),CutEffect=null,FieldEffectArguments=null};
        }
        throw new InvalidDataException("field_cut_presentation_wait_invalid");
    }

    private string? ValidateFieldCutState(CampaignState state)
    {
        if(state.CutEffect is not null||state.FieldEffectArguments is not null)return "save_field_cut_active";
        if(!_content.VermilionGymEnabled&&state.GameStats?.ContainsKey(CutUsageStat)==true)return "save_field_cut_profile_missing";
        return null;
    }
}
