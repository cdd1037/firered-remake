using System.Collections.Immutable;
namespace Game.Domain.Events;

public enum StoryOp
{
    End, Return, Goto, Call, SetFlag, ClearFlag, SetVar, CopyVar,
    ConditionalGoto, ConditionalCall, FlagGoto, FlagCall, Lock, Release,
    Dialog, Message, WaitMessage, CloseMessage, Delay, ApplyMovement, WaitMovement,
    Warp, WaitState, ObjectPosition, ObjectMovementType, ObjectVisible, TurnObject,
    TextColor, Music, Sound, Door, FameChecker, Respawn, FacePlayer, PokemonPicture, GivePokemon, BufferSpecies, ScreenFade, Special, TrainerBattle, QuestLogJump, Unsupported, Multichoice, WaitButtonPress
}
public sealed record StorySource(string Path, string Symbol, int Line);
public sealed record StoryInstruction(StoryOp Op, string Opcode, ImmutableArray<string> Args, StorySource Source);
public sealed record StoryProgram(string Id, ImmutableArray<StoryInstruction> Instructions);
public sealed record StoryFrame(string ProgramId, int InstructionIndex);
public enum StoryWaitKind { None, Dialog, Choice, Presentation, NativeEffect, MessagePrinting, Nickname, Battle, Inventory, MenuChoice, ButtonPress, PartySelection, SlotMachine }
public sealed record StoryWait(StoryWaitKind Kind, string CorrelationId);
public sealed record FameEntry(int PickState,int FlavorFlags);
public sealed record StoryState(ImmutableArray<StoryFrame> Stack, ImmutableSortedDictionary<string,int> Variables,
    ImmutableSortedDictionary<string,bool> Flags, bool Locked, StoryWait? Wait, long Revision, long EffectSequence, bool Completed, ImmutableSortedDictionary<int,FameEntry> Fame)
{
    public int QuestLogState{get;init;}
    public static StoryState Empty => new([],ImmutableSortedDictionary<string,int>.Empty.WithComparers(StringComparer.Ordinal),
        ImmutableSortedDictionary<string,bool>.Empty.WithComparers(StringComparer.Ordinal),false,null,0,0,true,ImmutableSortedDictionary<int,FameEntry>.Empty);
}
public sealed record StoryEffect(long Sequence, string Opcode, ImmutableArray<string> Args, StorySource Source);
public sealed record StoryTransition(TransitionStatus Status, StoryState State, ImmutableArray<StoryEffect> Effects, Diagnostic? Diagnostic);
public interface IStoryCatalog
{
    StoryProgram? FindProgram(string id);
    bool TryConstant(string symbol, out int value);
}
/// <summary>A bounded typed interpreter; unknown source operations fault, never silently no-op.</summary>
public static class StoryRules
{
    public static StoryTransition Start(StoryState state,string program,IStoryCatalog catalog,int budget=4096)
    {
        if(state.Wait is not null||!state.Completed)return new(TransitionStatus.Rejected,state,[],new("event_active","Finish the current event first."));
        if(catalog.FindProgram(program) is null)return new(TransitionStatus.Fault,state,[],new("event_program_missing",program));
        return Run(state,state with{Stack=[new(program,0)],Completed=false},catalog,budget);
    }
    public static StoryTransition Continue(StoryState state,string correlationId,IStoryCatalog catalog,int? choice=null,int budget=4096)
    {
        if(state.Wait is null||state.Wait.CorrelationId!=correlationId)return new(TransitionStatus.Rejected,state,[],new("event_wait_mismatch","Acknowledgement does not match pending event."));
        if(state.Wait.Kind==StoryWaitKind.Choice&&choice is not (0 or 1))return new(TransitionStatus.Rejected,state,[],new("event_choice_required","Yes/no choice must be 0 or 1."));
        if(state.Wait.Kind==StoryWaitKind.MenuChoice&&!ValidMenuChoice(state,catalog,choice))return new(TransitionStatus.Rejected,state,[],new("event_menu_choice_required","Source menu choice or cancellation required."));
        var next=state with{Wait=null};
        if(state.Wait.Kind is StoryWaitKind.Choice or StoryWaitKind.MenuChoice)next=next with{Variables=next.Variables.SetItem("VAR_RESULT",choice!.Value)};
        return Run(state,next,catalog,budget);
    }
    private static bool ValidMenuChoice(StoryState state,IStoryCatalog catalog,int? choice)
    {
        if(choice is null)return false;
        // Identify the suspended source instruction, not a global widening of every menu.
        // The four previews and Exit belong only to Bill's authenticated five-row menu.
        if(!state.Stack.IsEmpty)
        {
            var frame=state.Stack[^1];var program=catalog.FindProgram(frame.ProgramId);int index=frame.InstructionIndex-1;
            if(program is not null&&index>=0&&index<program.Instructions.Length)
            {
                var instruction=program.Instructions[index];
                if(GameCornerPrizeSource.MenuKind(instruction) is not null)return choice==127||choice>=0&&choice<6;
                if(GameCornerCoinSource.IsMenu(instruction))return choice is 0 or 1 or 2 or 127;
                if(DepartmentRooftopSource.Menu(instruction) is {} roofMenu)return choice==127||choice>=0&&choice<roofMenu.Rows.Length;
                if(instruction.Op==StoryOp.Multichoice&&instruction.Source.Path=="data/maps/TrainerTower_Lobby/scripts.inc"&&
                    instruction.Args.Length==4&&instruction.Args[3]=="FALSE")
                {
                    int rows=instruction.Args[2] switch{"MULTICHOICE_YES_NO_INFO"=>3,"MULTICHOICE_TRAINER_TOWER_MODE"=>5,_=>0};
                    if(rows>0)return choice==127||choice>=0&&choice<rows;
                }
                if(instruction.Op==StoryOp.Multichoice&&instruction.Source.Path=="data/maps/TrainerTower_Elevator/scripts.inc"&&
                    instruction.Args.Length==5&&instruction.Args[2]=="MULTICHOICE_ROOFTOP_B1F"&&instruction.Args[4]=="FALSE")
                    return choice is 0 or 1 or 2 or 127;
                if(instruction.Op==StoryOp.WaitState&&instruction.Opcode=="waitstate"&&instruction.Args.IsEmpty&&
                    instruction.Source.Path=="data/scripts/seagallop.inc"&&instruction.Source.Symbol is "EventScript_SeviiDestinationsPage1" or "EventScript_SeviiDestinationsPage2")
                    return choice==127||choice>=0&&choice<(instruction.Source.Symbol.EndsWith('1')?6:5);
                if(instruction.Op==StoryOp.Multichoice&&instruction.Opcode=="multichoice"&&
                    instruction.Source.Path is "data/scripts/seagallop.inc" or "data/maps/VermilionCity/scripts.inc"&&
                    instruction.Args.Length==4&&instruction.Args[0]=="19"&&instruction.Args[3]=="FALSE")
                {
                    int rows=instruction.Args[2] switch
                    {
                        "MULTICHOICE_ISLAND_23" or "MULTICHOICE_ISLAND_13" or "MULTICHOICE_ISLAND_12"=>3,
                        "MULTICHOICE_SEAGALLOP_V23" or "MULTICHOICE_SEAGALLOP_V13" or "MULTICHOICE_SEAGALLOP_V12" or "MULTICHOICE_SEAGALLOP_123"=>4,
                        _=>0
                    };
                    if(rows>0&&instruction.Args[1]==(rows==3?"6":"5"))return choice==127||choice>=0&&choice<rows;
                }
                if(instruction.Op==StoryOp.Multichoice&&instruction.Opcode=="multichoice"&&instruction.Args.Length==4&&instruction.Args[3]=="FALSE")
                {
                    int rows=(instruction.Source.Path,instruction.Source.Symbol,instruction.Args[0],instruction.Args[1],instruction.Args[2]) switch
                    {
                        ("data/maps/CinnabarIsland_PokemonLab_ExperimentRoom/scripts.inc","CinnabarIsland_PokemonLab_ExperimentRoom_EventScript_ChooseFossilHelixAmber","0","0","MULTICHOICE_HELIX_AMBER")=>3,
                        ("data/maps/CinnabarIsland_PokemonLab_ExperimentRoom/scripts.inc","CinnabarIsland_PokemonLab_ExperimentRoom_EventScript_ChooseFossilDomeAmber","0","0","MULTICHOICE_DOME_AMBER")=>3,
                        ("data/maps/VermilionCity/scripts.inc","VermilionCity_EventScript_HasMysticTicket","17","6","MULTICHOICE_SEVII_NAVEL")=>3,
                        ("data/maps/VermilionCity/scripts.inc","VermilionCity_EventScript_HasAuroraTicket","13","6","MULTICHOICE_SEVII_BIRTH")=>3,
                        ("data/maps/VermilionCity/scripts.inc","VermilionCity_EventScript_HasMysticAndAuroraTickets","13","5","MULTICHOICE_SEVII_NAVEL_BIRTH")=>4,
                        ("data/maps/NavelRock_Harbor/scripts.inc","NavelRock_Harbor_EventScript_Sailor","20","8","MULTICHOICE_SEAGALLOP_VERMILION")=>2,
                        ("data/maps/BirthIsland_Harbor/scripts.inc","BirthIsland_Harbor_EventScript_Sailor","20","8","MULTICHOICE_SEAGALLOP_VERMILION")=>2,
                        _=>0
                    };
                    if(rows>0)return choice==127||choice>=0&&choice<rows;
                }
                if(instruction.Op==StoryOp.Multichoice&&instruction.Opcode=="multichoicedefault"&&
                    instruction.Source.Path=="data/maps/CeladonCity_DepartmentStore_Elevator/scripts.inc"&&instruction.Args.Length==5&&
                    instruction.Args[2]=="MULTICHOICE_DEPT_STORE_ELEVATOR"&&instruction.Args[4]=="FALSE")
                    return choice is >=0 and <=5 or 127;
                if(instruction.Op==StoryOp.Multichoice&&instruction.Opcode=="multichoicedefault"&&
                    instruction.Source.Path=="data/maps/RocketHideout_Elevator/scripts.inc"&&instruction.Args.Length==5&&
                    instruction.Args[2]=="MULTICHOICE_ROCKET_HIDEOUT_ELEVATOR"&&instruction.Args[4]=="FALSE")
                    return choice is >=0 and <=3 or 127;
                if(instruction.Op==StoryOp.Multichoice&&instruction.Source.Path=="data/maps/Route25_SeaCottage/scripts.inc"&&
                    instruction.Source.Symbol=="Route25_SeaCottage_EventScript_BillsMonList"&&
                    instruction.Args.SequenceEqual(new[]{"0","0","MULTICHOICE_EEVEELUTIONS","FALSE"}))
                    return choice is >=0 and <=4 or 127;
            }
        }
        return choice is 0 or 1||catalog.TryConstant("SCR_MENU_CANCEL",out var cancel)&&choice==cancel;
    }
    private static StoryTransition Run(StoryState original,StoryState state,IStoryCatalog catalog,int budget)
    {
        var effects=ImmutableArray.CreateBuilder<StoryEffect>();
        StoryTransition Fault(string code,string message)=>new(TransitionStatus.Fault,original,[],new(code,message));
        int Value(string token)
        {
            if(token.StartsWith("VAR_",StringComparison.Ordinal))return state.Variables.GetValueOrDefault(token);
            if(token.StartsWith("0x",StringComparison.OrdinalIgnoreCase)&&int.TryParse(token[2..],System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out var hex))return unchecked((ushort)hex);
            if(int.TryParse(token,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var number))return unchecked((ushort)number);
            if(catalog.TryConstant(token,out var constant))return unchecked((ushort)constant);
            throw new InvalidDataException("Unresolved source value: "+token);
        }
        void Push(string id)
        {
            if(catalog.FindProgram(id) is null)throw new InvalidDataException("Missing source label: "+id);
            if(state.Stack.Length>=32)throw new InvalidDataException("Call stack limit reached.");
            state=state with{Stack=state.Stack.Add(new(id,0))};
        }
        void Jump(string id)
        {
            if(catalog.FindProgram(id) is null)throw new InvalidDataException("Missing source label: "+id);
            state=state with{Stack=state.Stack.SetItem(state.Stack.Length-1,new(id,0))};
        }
        bool Compare(string op,int a,int b)=>op switch{"eq"=>a==b,"ne"=>a!=b,"lt"=>a<b,"le"=>a<=b,"gt"=>a>b,"ge"=>a>=b,_=>throw new InvalidDataException("Unknown comparison: "+op)};
        void Effect(StoryInstruction instruction,StoryWaitKind wait=StoryWaitKind.None)
        {
            var seq=checked(state.EffectSequence+1);effects.Add(new(seq,instruction.Opcode,instruction.Args.Select((a,index)=>a.StartsWith("VAR_",StringComparison.Ordinal)&&!(instruction.Opcode=="getplayerxy"||instruction.Opcode is "specialvar" or "subvar" or "addvar" or "checkcoins"&&index==0)?Value(a).ToString(System.Globalization.CultureInfo.InvariantCulture):a).ToImmutableArray(),instruction.Source));state=state with{EffectSequence=seq};
            if(wait!=StoryWaitKind.None)state=state with{Wait=new(wait,"event:"+seq)};
        }
        try
        {
            if(budget is <1 or >100000)return Fault("event_budget_invalid","Budget must be bounded.");
            for(int step=0;step<budget;step++)
            {
                if(state.Stack.IsEmpty){state=state with{Completed=true};return new(TransitionStatus.Accepted,state with{Revision=checked(original.Revision+1)},effects.ToImmutable(),null);}
                var frame=state.Stack[^1];var program=catalog.FindProgram(frame.ProgramId);
                if(program is null||frame.InstructionIndex<0||frame.InstructionIndex>=program.Instructions.Length)return Fault("event_invalid_cursor",frame.ProgramId);
                var i=program.Instructions[frame.InstructionIndex];var a=i.Args;
                state=state with{Stack=state.Stack.SetItem(state.Stack.Length-1,frame with{InstructionIndex=frame.InstructionIndex+1})};
                switch(i.Op)
                {
                    case StoryOp.End:state=state with{Stack=[],Completed=true};break;
                    case StoryOp.Return:state=state with{Stack=state.Stack.RemoveAt(state.Stack.Length-1)};break;
                    case StoryOp.QuestLogJump:
                        if(state.QuestLogState is <0 or >3)return Fault("quest_log_state_invalid","Unknown source Quest Log state.");
                        state=state with{Variables=state.Variables.SetItem("VAR_RESULT",state.QuestLogState)};
                        if(state.QuestLogState==2)Jump(a[0]);break;
                    case StoryOp.Goto:Jump(a[0]);break;
                    case StoryOp.Call:Push(a[0]);break;
                    case StoryOp.SetFlag:state=state with{Flags=state.Flags.SetItem(a[0],true)};break;
                    case StoryOp.ClearFlag:state=state with{Flags=state.Flags.Remove(a[0])};break;
                    case StoryOp.SetVar:case StoryOp.CopyVar:state=state with{Variables=state.Variables.SetItem(a[0],Value(a[1]))};break;
                    case StoryOp.ConditionalGoto:case StoryOp.ConditionalCall:
                        if(Compare(i.Opcode.Split('_')[^1],Value(a[0]),Value(a[1]))) {if(i.Op==StoryOp.ConditionalCall)Push(a[2]);else Jump(a[2]);}break;
                    case StoryOp.FlagGoto:case StoryOp.FlagCall:
                        var set=state.Flags.GetValueOrDefault(a[0]);var expected=i.Opcode.EndsWith("_unset",StringComparison.Ordinal)?!set:set;
                        if(expected){if(i.Op==StoryOp.FlagCall)Push(a[1]);else Jump(a[1]);}break;
                    case StoryOp.Lock:state=state with{Locked=true};Effect(i);break;
                    case StoryOp.Release:state=state with{Locked=false};Effect(i);break;
                    case StoryOp.Dialog:Effect(i,a.Length>1&&a[1]=="MSGBOX_YESNO"?StoryWaitKind.Choice:StoryWaitKind.Dialog);break;
                    case StoryOp.Multichoice:Effect(i,StoryWaitKind.MenuChoice);break;
                    case StoryOp.Message:Effect(i);break;
                    case StoryOp.WaitMessage:Effect(i,StoryWaitKind.MessagePrinting);break;
                    case StoryOp.WaitButtonPress:Effect(i,StoryWaitKind.ButtonPress);break;
                    case StoryOp.Delay:if(Value(a[0])<0)return Fault("event_delay_invalid",i.Source.ToString());Effect(i,StoryWaitKind.Presentation);break;
                    case StoryOp.WaitMovement:case StoryOp.WaitState:Effect(i,StoryWaitKind.Presentation);break;
                    case StoryOp.FameChecker:
                        var fame = FameCheckerRules.ApplyMacro(state,Value(a[0]),Value(a[1]),a.Length==3?a[2]:FameCheckerRules.CollectFactFunction);
                        if(!fame.Succeeded)return Fault(fame.Error!,a.Length==3?a[2]:FameCheckerRules.CollectFactFunction);
                        state=fame.State;
                        break;
                    case StoryOp.TrainerBattle:Effect(i,StoryWaitKind.Battle);break;
                    case StoryOp.PokemonPicture:Effect(i,i.Opcode=="hidemonpic"?StoryWaitKind.Presentation:StoryWaitKind.NativeEffect);break;
                    case StoryOp.ScreenFade:Effect(i,StoryWaitKind.Presentation);break;
                    case StoryOp.GivePokemon:case StoryOp.BufferSpecies:case StoryOp.Special:Effect(i,StoryWaitKind.NativeEffect);break;
                    case StoryOp.TextColor:
                        state=state with{Variables=state.Variables.SetItem("VAR_PREV_TEXT_COLOR",state.Variables.GetValueOrDefault("VAR_TEXT_COLOR")).SetItem("VAR_TEXT_COLOR",Value(a[0]))};Effect(i);break;
                    case StoryOp.Sound:case StoryOp.Door:
                        Effect(i,i.Opcode.StartsWith("wait",StringComparison.Ordinal)?StoryWaitKind.Presentation:StoryWaitKind.None);break;
                    case StoryOp.ApplyMovement:case StoryOp.Warp:case StoryOp.ObjectPosition:case StoryOp.ObjectMovementType:
                    case StoryOp.ObjectVisible:case StoryOp.TurnObject:case StoryOp.Respawn:case StoryOp.FacePlayer:
                        Effect(i,StoryWaitKind.NativeEffect);break;
                    case StoryOp.CloseMessage:Effect(i);break;
                    case StoryOp.Music:Effect(i,i.Opcode=="fadeoutbgm"?StoryWaitKind.Presentation:StoryWaitKind.None);break;
                    case StoryOp.Unsupported:return Fault("event_opcode_unsupported",$"{i.Source.Path}:{i.Source.Line}: {i.Opcode}");
                    default:return Fault("event_ir_invalid",i.Op.ToString());
                }
                if(state.Wait is not null)return new(TransitionStatus.Accepted,state with{Revision=checked(original.Revision+1)},effects.ToImmutable(),null);
            }
            return Fault("event_instruction_budget","Event did not yield or finish within its bounded instruction budget.");
        }
        catch(Exception ex) when(ex is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException)
        {return Fault("event_invariant",ex.Message);}
    }
}
