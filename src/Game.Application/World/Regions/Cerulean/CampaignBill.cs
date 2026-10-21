using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

/// <summary>A source task's draw at a frame offset. A door frame of -1 means the original map tiles.</summary>
public sealed record BillSourceDraw(int Frame,int X,int Y,string Kind,int Value);

/// <summary>Pure presentation metadata transcribed from the pinned field_door/special_field_anim tasks.</summary>
public static class BillSourcePresentation
{
    public const string CameraActor = "LOCALID_CAMERA";
    public const string Cottage = "map.route-25-sea-cottage";
    public const int DoorAnimationTicks = 20;
    public const int DoorWaitTicks = 21;
    public static ImmutableArray<int> PcSpecies { get; } = [133,136,135,134];

    public static ImmutableArray<BillSourceDraw> DoorFrames(int x,int y,bool opening)
    {
        int[] frames=opening?[-1,0,1,2]:[2,1,0,-1];
        // AnimateDoorFrame draws at counter 0, then consumes a separate counter-4
        // task call to advance the frame without drawing. Each source duration 4
        // therefore occupies five task calls, not four.
        return frames.Select((value,index)=>new BillSourceDraw(index*5,x,y-1,"door",value)).ToImmutableArray();
    }

    public static ImmutableArray<BillSourceDraw> HousingFrames(int playerX,int playerY,int selector)
    {
        if(selector is not (0 or 1))throw new ArgumentOutOfRangeException(nameof(selector));
        int x=playerX+(selector==0?6:-1),y=playerY-5;
        var draws=ImmutableArray.CreateBuilder<BillSourceDraw>();
        for(int i=0;i<13;i++)
        {
            draws.Add(new(i*16,x,y,i%2==0?"light_yellow":"light_red",3));
            draws.Add(new(i*16,x,y+2,i%2==0?"door_half_glowing":"door_full_glowing",3));
        }
        draws.Add(new(208,x,y,"light_green",3));draws.Add(new(208,x,y+2,"door_closed",3));
        return draws.ToImmutable();
    }

    public static ImmutableArray<BillSourceDraw> CableFrames(int playerX,int playerY)
    {
        var draws=ImmutableArray.CreateBuilder<BillSourceDraw>();int x=playerX+4,y=playerY-5;
        for(int i=0;i<=4;i++)
        {
            if(i>0){draws.Add(new(i*4,x-i+1,y,"cable_top",3));draws.Add(new(i*4,x-i+1,y+1,"cable_bottom",3));}
            if(i<4){draws.Add(new(i*4,x-i,y,"cable_ball_top",3));draws.Add(new(i*4,x-i,y+1,"cable_ball_bottom",3));}
        }
        return draws.ToImmutable();
    }
}

public sealed partial class CampaignSession
{
    // Deliberate, user-approved fix to the original lose/retry Nugget duplication.
    public const string BillNuggetReceivedFlag="FLAG_REMAKE_RECEIVED_NUGGET_BRIDGE_NUGGET";
    private const string BillCottageSource="data/maps/Route25_SeaCottage/scripts.inc";
    private const string BillRoute24Source="data/maps/Route24/scripts.inc";
    private const string BillRivalSource="data/maps/CeruleanCity/scripts.inc";
    private const string BillSeparator="Route25_SeaCottage_EventScript_RunCellSeparator";
    private const string BillMenu="Route25_SeaCottage_EventScript_BillsMonList";
    private static int BillViewSpecies(string program)=>program switch
    {
        "Route25_SeaCottage_EventScript_ViewEevee"=>133,
        "Route25_SeaCottage_EventScript_ViewFlareon"=>136,
        "Route25_SeaCottage_EventScript_ViewJolteon"=>135,
        "Route25_SeaCottage_EventScript_ViewVaporeon"=>134,
        _=>0
    };
    private static bool IsBillRival(string id)=>id is "pret:TRAINER_RIVAL_CERULEAN_BULBASAUR" or
        "pret:TRAINER_RIVAL_CERULEAN_CHARMANDER" or "pret:TRAINER_RIVAL_CERULEAN_SQUIRTLE";

    private bool IsBillButton(CampaignState state)=>state.Script.Wait?.Kind==StoryWaitKind.ButtonPress&&
        !state.Script.Stack.IsEmpty&&(state.World.Player.MapId.Value=="map.route-24"&&state.Script.Stack[^1].ProgramId=="Route24_EventScript_BattleRocket"||
        state.World.Player.MapId.Value==BillSourcePresentation.Cottage&&BillViewSpecies(state.Script.Stack[^1].ProgramId)!=0);

    private Decision<CampaignState> DecideBillButton(CampaignState state,CampaignCommand command)
    {
        if(!IsBillButton(state)||command.Text is not ("A" or "B"))return new(CommandStatus.Rejected,state,"bill_button_invalid");
        var next=state with{PresentationEffects=[],PresentationTicks=0,Motions=[]};
        return new(CommandStatus.Accepted,Pump(Advance(next,StoryRules.Continue(next.Script,next.Script.Wait!.CorrelationId,_content))));
    }

    private bool TryApplyBillEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;        var a=effect.Args;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):
            int.TryParse(token,NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)?n:
            _content.TryConstant(token,out n)?n:throw new InvalidDataException("bill_operand_missing: "+token);
        CampaignState Result(int value)=>state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",value)}};
        void Cottage(string? symbol=null)
        {
            if(effect.Source.Path!=BillCottageSource||state.World.Player.MapId.Value!=BillSourcePresentation.Cottage||
                symbol is not null&&effect.Source.Symbol!=symbol)throw new InvalidDataException("bill_cottage_source_context");
        }
        CampaignState Metadata(CampaignState changed,string opcode,params string[] args)=>changed with
        {PresentationEffects=changed.PresentationEffects.Add(new(effect.Sequence,opcode,state.World.Player.MapId,args.ToImmutableArray()))};
        string Number(int value)=>value.ToString(CultureInfo.InvariantCulture);

        if(effect.Opcode=="checkplayergender"&&effect.Source.Path==BillCottageSource)
        {
            Cottage();if(a.Length!=0||state.PlayerGender is <0 or >1)throw new InvalidDataException("bill_gender_invalid");
            next=Result(state.PlayerGender);return true;
        }
        if(effect.Opcode=="waitbuttonpress"&&(effect.Source.Path==BillCottageSource||effect.Source.Path==BillRoute24Source))
        {
            if(a.Length!=0||!IsBillButton(state))throw new InvalidDataException("bill_button_source_context");
            int species=BillViewSpecies(effect.Source.Symbol);
            if(species!=0){Cottage();if(state.MonPicture?.SourceSpeciesId!=species)throw new InvalidDataException("bill_picture_button_context");}
            else if(state.Dialogue?.TextSymbol!="Route24_Text_JoinTeamRocket")throw new InvalidDataException("bill_rocket_button_context");
            return true;
        }
        if(effect.Opcode=="multichoice"&&effect.Source.Path==BillCottageSource)
        {
            Cottage(BillMenu);
            if(!a.SequenceEqual(new[]{"0","0","MULTICHOICE_EEVEELUTIONS","FALSE"})||state.Script.Wait?.Kind!=StoryWaitKind.MenuChoice||
                !state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET")||!state.Script.Flags.GetValueOrDefault("FLAG_TEMP_3"))
                throw new InvalidDataException("bill_menu_context");
            return true;
        }
        if(effect.Opcode is "showmonpic" or "hidemonpic"&&effect.Source.Path==BillCottageSource)
        {
            Cottage();int species=BillViewSpecies(effect.Source.Symbol);
            if(species==0||!state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET")||!state.Script.Flags.GetValueOrDefault("FLAG_TEMP_3"))
                throw new InvalidDataException("bill_picture_context");
            if(effect.Opcode=="showmonpic")
            {
                if(a.Length!=3||Value(a[0])!=species||Value(a[1])!=10||Value(a[2])!=3||state.MonPicture is not null)
                    throw new InvalidDataException("bill_picture_source_invalid");
                // Pictures are authenticated separately from the combat-species catalog.
                next=state with{MonPicture=new(species,10,3)};
            }
            else
            {
                if(a.Length!=0||state.MonPicture?.SourceSpeciesId!=species)throw new InvalidDataException("bill_hide_picture_context");
                next=state with{MonPicture=null,PresentationTicks=Math.Max(state.PresentationTicks,1)};
            }
            return true;
        }
        if(effect.Opcode is "opendoor" or "closedoor" or "waitdooranim"&&effect.Source.Path==BillCottageSource)
        {
            Cottage();bool separator=effect.Source.Symbol==BillSeparator;
            if(!separator&&effect.Source.Symbol!="Route25_SeaCottage_EventScript_BillAskForHelp")throw new InvalidDataException("bill_door_source_context");
            if(effect.Opcode=="waitdooranim")
            {
                if(a.Length!=0||!state.PresentationEffects.Any(p=>p.Opcode=="bill_teleporter_door"))throw new InvalidDataException("bill_door_wait_context");
                // RunTasks destroys the door on zero-based frame 19. The script
                // runs before tasks; frame 20's native poll switches to bytecode
                // and returns, so the next source instruction executes at 21.
                next=state with{PresentationTicks=Math.Max(state.PresentationTicks,BillSourcePresentation.DoorWaitTicks)};
            }
            else
            {
                if(a.Length!=2||Value(a[0])!=(separator?3:10)||Value(a[1])!=3)throw new InvalidDataException("bill_door_coordinates");
                next=Metadata(state,"bill_teleporter_door",Number(Value(a[0])),"3",effect.Opcode=="opendoor"?"open":"close",Number(BillSourcePresentation.DoorAnimationTicks));
            }
            return true;
        }
        if(effect.Opcode=="fadeinbgm"&&effect.Source.Path==BillCottageSource)
        {
            Cottage(BillSeparator);if(a.Length!=1||Value(a[0])!=0)throw new InvalidDataException("bill_bgm_fade_source");
            // ScrCmd_fadeinbgm returns FALSE: enqueue the audio fade without blocking the script.
            return true;
        }
        if(effect.Opcode=="applymovement"&&effect.Source.Path==BillCottageSource&&a.Length>0&&a[0]==BillSourcePresentation.CameraActor)
        {
            Cottage(BillSeparator);
            if(a.Length!=2||a[1] is not ("Route25_SeaCottage_Movement_CameraPanToTeleporters" or "Route25_SeaCottage_Movement_CameraPanBackFromTeleporters")||
                !state.Actors.TryGetValue(BillSourcePresentation.CameraActor,out var camera)||!camera.Active||!camera.Invisible)
                throw new InvalidDataException("bill_camera_movement_context");
            next=ApplyMovement(state,BillSourcePresentation.CameraActor,a[1]);return true;
        }
        if(effect.Opcode=="special"&&effect.Source.Path==BillCottageSource)
        {
            Cottage();if(a.Length!=1)throw new InvalidDataException("bill_special_arity");
            if(a[0]=="SetSeenMon")
            {
                int species=BillViewSpecies(effect.Source.Symbol);
                if(species==0||state.Script.Variables.GetValueOrDefault("VAR_0x8004")!=species||state.MonPicture is not null||
                    !state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET")||!state.Script.Flags.GetValueOrDefault("FLAG_TEMP_3"))
                    throw new InvalidDataException("bill_seen_context");
                next=state with{SeenSpecies=state.SeenSpecies.Add(species)};return true;
            }
            Cottage(BillSeparator);
            if(!state.Script.Flags.GetValueOrDefault("FLAG_HELPED_BILL_IN_SEA_COTTAGE")||state.Script.Flags.GetValueOrDefault("FLAG_TEMP_2"))
                throw new InvalidDataException("bill_separator_flag_context");
            var player=state.World.Player;
            switch(a[0])
            {
                case "SpawnCameraObject":
                    if(state.Actors.ContainsKey(BillSourcePresentation.CameraActor))throw new InvalidDataException("bill_camera_already_spawned");
                    int slot=state.ObjectSlots is null?1:Enumerable.Range(1,15).FirstOrDefault(i=>!state.ObjectSlots.ContainsKey(i));
                    if(slot==0)throw new InvalidDataException("bill_camera_object_pool_full");
                    var camera=new CampaignActor(BillSourcePresentation.CameraActor,127,"OBJ_EVENT_GFX_YOUNGSTER",player.X,player.Y,3,Direction.South,
                        "MOVEMENT_TYPE_FACE_DOWN","NULL","0",true,true,player.X,player.Y);
                    next=state with{Actors=state.Actors.Add(camera.LocalId,camera),ObjectSlots=state.ObjectSlots?.Add(slot,camera.LocalId)};
                    return true;
                case "RemoveCameraObject":
                    if(!state.Actors.TryGetValue(BillSourcePresentation.CameraActor,out var removed)||!removed.Active||!removed.Invisible)
                        throw new InvalidDataException("bill_camera_missing");
                    next=state with{Actors=state.Actors.Remove(removed.LocalId),ObjectSlots=state.ObjectSlots?.RemoveRange(state.ObjectSlots.Where(x=>x.Value==removed.LocalId).Select(x=>x.Key))};
                    return true;
                case "AnimateTeleporterHousing":
                    if(!state.Actors.ContainsKey(BillSourcePresentation.CameraActor))throw new InvalidDataException("bill_camera_missing");
                    int selector=state.Script.Variables.GetValueOrDefault("VAR_0x8004");
                    if(selector is not (0 or 1))throw new InvalidDataException("bill_housing_selector");
                    next=Metadata(state,"bill_teleporter_housing",Number(selector),Number(player.X+(selector==0?6:-1)),Number(player.Y-5),"13","16");return true;
                case "AnimateTeleporterCable":
                    if(!state.Actors.ContainsKey(BillSourcePresentation.CameraActor))throw new InvalidDataException("bill_camera_missing");
                    next=Metadata(state,"bill_teleporter_cable",Number(player.X+4),Number(player.Y-5),"4","4");return true;
                default:throw new InvalidDataException("bill_special_unsupported: "+a[0]);
            }
        }
        if(effect.Opcode is "release" or "releaseall"&&effect.Source.Path is BillCottageSource or BillRoute24Source or BillRivalSource)
        {
            // Received-item macros keep a transient message pointer; the field event is now closed.
            next=state with{Dialogue=null,SignMessage=null,MessagePointer=null,GiftDelivery=null};return true;
        }
        if(effect.Opcode is not ("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural"))return false;
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(a.Length<(buffer?2:1))throw new InvalidDataException("bill_item_arity");
        int sourceId=Value(a[buffer?1:0]);
        var item=ShopCatalog.Items.SingleOrDefault(i=>i.SourceItemId==sourceId&&i.Id is var id&&
            (id==BillItemRules.NuggetId||id==BillItemRules.FameCheckerId||id==BillItemRules.SsTicketId));
        if(item is null)return false;
        bool nugget=item.Id==BillItemRules.NuggetId,ticket=item.Id==BillItemRules.SsTicketId;
        string map=nugget?"map.route-24":ticket?BillSourcePresentation.Cottage:"map.cerulean-city";
        string path=nugget?BillRoute24Source:ticket?BillCottageSource:BillRivalSource;
        // A shared item (for example a Nugget) does not identify its giver. This
        // handler owns only Bill/Bridge/Rival source callers, including their macros.
        bool caller = effect.Source.Path == path || state.Script.Stack.Any(frame =>
            _content.FindProgram(frame.ProgramId)?.Instructions.Any(i => i.Source.Path == path) == true);
        if (!caller) return false;
        if(state.World.Player.MapId.Value!=map||effect.Source.Path!=path&&effect.Source.Path is not ("data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("bill_reward_source_context");
        if(buffer)
        {
            if(a[0]!="STR_VAR_2"||a.Length!=(effect.Opcode=="bufferitemnameplural"?3:2)||a.Length==3&&Value(a[2])!=1)
                throw new InvalidDataException("bill_reward_buffer_context");
            next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],item.Name)};return true;
        }
        if(effect.Opcode=="checkitemtype"){if(a.Length!=1)throw new InvalidDataException("bill_item_arity");next=Result(Value(item.Pocket));return true;}
        int quantity=a.Length==1?1:Value(a[1]);
        if(a.Length>2||quantity!=1)throw new InvalidDataException("bill_reward_quantity");
        if(nugget)
        {
            if(state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE24")!=0||effect.Source.Path!=BillRoute24Source||
                effect.Opcode=="additem"&&effect.Source.Symbol!="Route24_EventScript_BattleRocket")throw new InvalidDataException("bill_nugget_context");
            if(state.Script.Flags.GetValueOrDefault(BillNuggetReceivedFlag))
            {
                next=Result(1);
                if(effect.Opcode=="additem")next=SkipRepeatedBillNuggetMessage(next,effect);
                return true;
            }
        }
        else if(ticket)
        {
            if(!state.Script.Flags.GetValueOrDefault("FLAG_HELPED_BILL_IN_SEA_COTTAGE")||state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET")||
                effect.Source.Path!=BillCottageSource||effect.Source.Symbol!="Route25_SeaCottage_EventScript_BillGiveSSTicket")throw new InvalidDataException("bill_ticket_context");
        }
        else if(!state.Script.Flags.GetValueOrDefault("FLAG_GOT_FAME_CHECKER")||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CERULEAN_CITY_RIVAL")!=1||
            !state.BattledTrainers.Any(IsBillRival))throw new InvalidDataException("bill_fame_checker_context");
        var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
        var invalid=ValidateCampaignInventory(inventory);if(invalid is not null)throw new InvalidDataException(invalid);
        if(!nugget&&OpeningInventoryRules.Quantity(inventory.KeyItems??[],item.Id)!=0)throw new InvalidDataException("bill_key_item_duplicate");
        var view=inventory;
        // The narrowed shop view omits Town Map. Capacity counts the full original key pocket.
        bool keyFull=!nugget&&(inventory.KeyItems?.Length??0)>=ShopCatalog.QuestLimits.KeyItemSlots;
        var result=keyFull?new InventoryTransactionResult(view,false,"inventory_destination_full"):
            effect.Opcode=="additem"?EarlyInventoryAddRules.Add(ShopCatalog,view,item.Id,1):EarlyInventoryAddRules.CheckSpace(ShopCatalog,view,item.Id,1);
        if(!result.Succeeded&&result.Error is not ("inventory_stack_full" or "inventory_destination_full"))throw new InvalidDataException(result.Error);
        next=Result(result.Succeeded?1:0);
        if(result.Succeeded&&effect.Opcode=="additem")
        {
            next=next with{Inventory=nugget?result.State:inventory with{KeyItems=(inventory.KeyItems??[]).Add(new(item.Id,1))}};
            if(nugget)next=next with{Script=next.Script with{Flags=next.Script.Flags.SetItem(BillNuggetReceivedFlag,true)}};
        }
        return true;
    }

    private CampaignState SkipRepeatedBillNuggetMessage(CampaignState state,StoryEffect effect)
    {
        if(state.Script.Stack.IsEmpty||state.Script.Stack[^1].ProgramId!=effect.Source.Symbol)throw new InvalidDataException("bill_nugget_retry_cursor");
        var program=_content.FindProgram(effect.Source.Symbol)??throw new InvalidDataException("bill_nugget_retry_program");
        int cursor=state.Script.Stack[^1].InstructionIndex;
        // Skip only the authenticated giveitem_msg expansion, then execute the original next instruction.
        string[] expected=["loadword","setorcopyvar","setorcopyvar","setorcopyvar","call"];
        if(cursor+expected.Length>=program.Instructions.Length||
            !program.Instructions.Skip(cursor).Take(expected.Length).Select(i=>i.Opcode).SequenceEqual(expected)||
            program.Instructions[cursor].Args[1]!="Route24_Text_ReceivedNuggetFromMysteryTrainer"||
            program.Instructions[cursor+4].Args[0]!="Std_ReceivedItem"||
            program.Instructions[cursor+5].Opcode!="message"||program.Instructions[cursor+5].Args[0]!="Route24_Text_JoinTeamRocket")
            throw new InvalidDataException("bill_nugget_retry_macro_changed");
        return state with{Script=state.Script with{Stack=state.Script.Stack.SetItem(state.Script.Stack.Length-1,new(effect.Source.Symbol,cursor+5))}};
    }

    private string? ValidateBillProgress(CampaignState state)
    {
        bool received=state.Script.Flags.GetValueOrDefault(BillNuggetReceivedFlag);
        if(state.Actors.ContainsKey(BillSourcePresentation.CameraActor)||state.ObjectSlots?.Values.Contains(BillSourcePresentation.CameraActor)==true)
            return "save_bill_camera_active";
        var history=state.ForestTrainerHistory??[];
        var rivalHistory=history.Where(h=>IsBillRival(h.TrainerId)).ToArray();
        bool rivalWon=rivalHistory.Any(h=>h.Outcome==WildBattleOutcome.Won);
        int rivalScene=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CERULEAN_CITY_RIVAL");
        if(rivalScene is <0 or >1||rivalWon!=(rivalScene==1)||state.Script.Flags.GetValueOrDefault("FLAG_GOT_FAME_CHECKER")!=rivalWon)
            return "save_bill_rival_progress";
        bool rocketWon=history.Any(h=>h.TrainerId=="pret:TRAINER_TEAM_ROCKET_GRUNT_6"&&h.Outcome==WildBattleOutcome.Won);
        int bridgeScene=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE24");
        if(bridgeScene is <0 or >1||rocketWon!=(bridgeScene==1)||rocketWon&&!received||
            history.Any(h=>h.TrainerId=="pret:TRAINER_TEAM_ROCKET_GRUNT_6")&&!received)return "save_bill_bridge_progress";
        var inventory=state.Inventory;
        // Nuggets are fungible, sellable rewards from multiple source events.
        // Bridge progress is validated by its receipt and battle flags above;
        // shared inventory validation owns stack/pocket limits, not this giver.
        int fame=OpeningInventoryRules.Quantity(inventory?.KeyItems??[],BillItemRules.FameCheckerId);
        // The original rival sets its flag before giveitem. A full key pocket can leave zero items.
        if(fame>1||fame>0&&!rivalWon)return "save_bill_fame_checker_reward";
        bool helped=state.Script.Flags.GetValueOrDefault("FLAG_HELPED_BILL_IN_SEA_COTTAGE"),ticket=state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET");
        int tickets=OpeningInventoryRules.Quantity(inventory?.KeyItems??[],BillItemRules.SsTicketId);
        if(ticket&&!helped||tickets!=(ticket?1:0)||state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET_DUP")!=ticket||
            state.Script.Flags.GetValueOrDefault("FLAG_HIDE_NUGGET_BRIDGE_ROCKET")!=ticket||state.Script.Flags.GetValueOrDefault("FLAG_SYS_NOT_SOMEONES_PC")!=ticket)
            return "save_bill_ticket_progress";
        if(state.World.Player.MapId.Value==BillSourcePresentation.Cottage)
        {
            bool teleporter=state.Script.Flags.GetValueOrDefault("FLAG_TEMP_2"),returned=state.Script.Flags.GetValueOrDefault("FLAG_TEMP_3");
            if(teleporter&&helped||returned&&!ticket||
                state.Script.Flags.GetValueOrDefault("FLAG_HIDE_BILL_CLEFAIRY")!=(helped||teleporter)||
                state.Script.Flags.GetValueOrDefault("FLAG_HIDE_BILL_HUMAN_SEA_COTTAGE")==helped)
                return "save_bill_cottage_flags";
            if(!state.Actors.TryGetValue("LOCALID_BILL_CLEFAIRY",out var clefairy)||!state.Actors.TryGetValue("LOCALID_BILL_HUMAN",out var human)||
                (helped||teleporter)&&clefairy.Active||!helped&&human.Active)return "save_bill_cottage_actors";
        }
        return null;
    }
}
