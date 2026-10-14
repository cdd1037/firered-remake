using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignPcView { Main, StorageMenu, Deposit, Withdraw, PlayerItems, HallOfFame, Move }
public sealed record CampaignPcState(CampaignPcView View,int StorageMenuSelection=0)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignHallOfFamePc? HallOfFame { get; init; }
    // Native cursor ownership is transient: every open PC is already excluded from player saves.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public PokemonStorageMoveCursor? MoveCursor { get; init; }
}

public sealed partial class CampaignSession
{
    private const string CenterPcScript="EventScript_PC";
    private const string CenterPcSource="data/scripts/pc.inc";

    private ImmutableArray<PartyPokemon> OwnedPokemon(CampaignState state)
    {
        var owned = _content.PokemonStorageEnabled && state.Storage is {} storage
            ? state.Party.AddRange(storage.Boxes.SelectMany(b => b.Slots).OfType<PartyPokemon>()) : state.Party;
        if (state.Route5DayCare?.Resident is {} resident) owned = owned.Add(resident.Pokemon);
        if (state.FourIslandDayCare?.First is {} first) owned = owned.Add(first.Pokemon);
        if (state.FourIslandDayCare?.Second is {} second) owned = owned.Add(second.Pokemon);
        if (state.Pc?.MoveCursor is {} cursor) owned = owned.Add(cursor.Pokemon);
        return owned;
    }

    private string? ValidateStorageState(CampaignState state)
    {
        if(!_content.PokemonStorageEnabled)return state.Storage is not null||state.Pc is not null?"save_storage_profile_missing":null;
        if(!_content.EarlyPartyEnabled)return "storage_requires_early_party_profile";
        if(state.Pc?.MoveCursor is not null&&state.Pc.View!=CampaignPcView.Move)return "storage_move_cursor_context";
        var error=PokemonStorageMoveRules.Validate(_content.PokemonContent,state.Party,state.Storage,state.Pc?.MoveCursor,state.NextPokemonId);
        if(error is not null)return error.Code;
        var owned=OwnedPokemon(state);
        if(owned.Select(p=>p.InstanceId).Distinct().Count()!=owned.Length)return "storage_duplicate_instance_id";
        if(owned.Any(p=>p.InstanceId>=state.NextPokemonId))return "storage_next_instance_id";
        if(state.Script.Variables.GetValueOrDefault("VAR_PC_BOX_TO_SEND_MON")>=PokemonStorageState.BoxCount)return "save_storage_delivery_box";
        return null;
    }

    // Native menu rows are indices into the visible list. The source script deliberately uses
    // its Oak/Hall flag guards to interpret early log-off rows, so never remap these results.
    public ImmutableArray<string> PcMenuTextSymbols
    {
        get
        {
            var rows=ImmutableArray.CreateBuilder<string>();
            bool clear=State.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR");
            rows.Add(State.Script.Flags.GetValueOrDefault("FLAG_SYS_NOT_SOMEONES_PC")?"gText_BillSPc":"gText_SomeoneSPc");
            rows.Add("gText_SPc");
            if(clear||State.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))rows.Add("gText_ProfOakSPc");
            if(clear)rows.Add("gText_HallOfFame_2");
            rows.Add("gText_LogOff");
            return rows.ToImmutable();
        }
    }

    private bool TryApplyPcEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(!_content.PokemonStorageEnabled||effect.Opcode!="special"||effect.Source.Path!=CenterPcSource)return false;
        if(!IsSupportedHealingCenter(state.World.Player.MapId))throw new InvalidDataException("pc_source_map_unsupported");
        var error=ValidateStorageState(state);if(error is not null)throw new InvalidDataException(error);
        next=effect.Args[0] switch
        {
            "Script_SetHelpContext" when state.Script.Variables.GetValueOrDefault("VAR_0x8004")==27=>state with{HelpContext="HELPCONTEXT_PC"},
            "AnimatePcTurnOn"=>state with{PcPoweredOn=true},
            "AnimatePcTurnOff"=>state with{PcPoweredOn=false,Pc=null},
            "SetHelpContextForMap"=>state with{HelpContext="map"},
            "CreatePCMenu"=>state with{Pc=new(CampaignPcView.Main)},
            "ShowPokemonStorageSystemPC"=>state with{Pc=new(CampaignPcView.StorageMenu),Dialogue=null},
            "PlayerPC"=>state with{Pc=new(CampaignPcView.PlayerItems),ItemMenu=new("pc","top"),Dialogue=null},
            _=>throw new InvalidDataException("pc_native_special_unsupported: "+effect.Args[0])
        };
        return true;
    }

    private Decision<CampaignState> DecidePc(CampaignState state,CampaignCommand command)
    {
        Decision<CampaignState> No(string error)=>new(CommandStatus.Rejected,state,error);
        if(!_content.PokemonStorageEnabled||state.Storage is null||!state.PcPoweredOn||state.Pc is not {} pc||
            state.Script.Wait is not {Kind:StoryWaitKind.Presentation})return No("pc_callback_context_invalid");
        Decision<CampaignState> View(CampaignPcView view,int? selection=null)=>new(CommandStatus.Accepted,
            state with{Pc=pc with{View=view,StorageMenuSelection=selection??pc.StorageMenuSelection}});
        if(pc.View==CampaignPcView.HallOfFame)return DecideHallOfFamePc(state,command);
        if(pc.View==CampaignPcView.Move||pc.MoveCursor is not null)return DecideStorageMove(state,command);
        if(command.Kind=="pc_back")
        {
            if(pc.View is CampaignPcView.Deposit or CampaignPcView.Withdraw)return View(CampaignPcView.StorageMenu);
            if(pc.View==CampaignPcView.StorageMenu)return ResumePcScript(state);
            if(pc.View==CampaignPcView.Main)return ResumePcScript(state,127);
        }
        if(command.Kind=="pc_select"&&pc.View==CampaignPcView.Main)
        {
            bool dex=state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"),clear=state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR");
            int count=clear?5:dex?4:3;
            if(command.Choice is not {} row||row<0||row>=count)return No("pc_menu_index_invalid");
            // Unsupported native screens must leave the recoverable source menu untouched.
            if(row==2&&dex)
            {
                if(!_content.OakRatingEnabled)return No("pc_oaks_rating_not_implemented");
                if(!_preflightingStory)PreflightOakPcEntry(state,row);
            }
            if(row==3&&clear&&!HallOfFamePcEnabled)return No("pc_hall_of_fame_not_enabled");
            return ResumePcScript(state,row);
        }
        if(command.Kind=="storage_select"&&pc.View==CampaignPcView.StorageMenu)
        {
            return command.Choice switch
            {
                0 when state.Party.Length>=6=>No("storage_party_full"),
                0=>View(CampaignPcView.Withdraw,0),
                1 when state.Party.Length<=1=>No("storage_last_usable"),
                1=>View(CampaignPcView.Deposit,1),
                2=>View(CampaignPcView.Move,2),
                3=>No("storage_move_items_not_implemented"),
                4=>ResumePcScript(state),
                _=>No("storage_menu_index_invalid")
            };
        }
        if(pc.View is CampaignPcView.Deposit or CampaignPcView.Withdraw)
        {
            PokemonStorageResult? result=command.Kind switch
            {
                "storage_box" when command.Choice is {} box=>PokemonStorageRules.SelectBox(_content.PokemonContent,state.Party,state.Storage,box),
                "storage_deposit" when pc.View==CampaignPcView.Deposit=>PokemonStorageRules.Deposit(_content.PokemonContent,state.Party,state.Storage,command.PartyIndex,state.Storage.CurrentBox),
                "storage_withdraw" when pc.View==CampaignPcView.Withdraw=>PokemonStorageRules.Withdraw(_content.PokemonContent,state.Party,state.Storage,state.Storage.CurrentBox,command.MoveSlot),
                _=>null
            };
            if(result is not null)return result.Succeeded
                ?new(CommandStatus.Accepted,state with{Party=result.Party,Storage=result.Storage})
                :No(result.Diagnostic!.Code);
        }
        return No("pc_menu_active");
    }

    private Decision<CampaignState> ResumePcScript(CampaignState state,int? result=null)
    {
        if(state.Pc?.MoveCursor is not null)return new(CommandStatus.Rejected,state,"storage_move_holding");
        if(state.Pc is null||state.Script.Wait is not {} wait||wait.Kind is not (StoryWaitKind.Presentation or StoryWaitKind.Inventory))
            return new(CommandStatus.Fault,state,"pc_callback_context_invalid");
        var next=state with{Pc=state.Pc.HallOfFame is {ReturnPending:true}?state.Pc:null,ItemMenu=null,Dialogue=null,Motions=[],PresentationTicks=0,PresentationEffects=[]};
        if(result is {} value)next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",value)}};
        return new(CommandStatus.Accepted,Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content))));
    }

    private string? CaptureCapacityError(CampaignState state)
    {
        if(!_content.PokemonStorageEnabled)return state.Party.Length>=6?"storage_delivery_unsupported":null;
        var error=ValidateStorageState(state);if(error is not null)return error;
        return state.Party.Length>=6&&state.Storage!.Boxes.All(b=>b.Slots.All(p=>p is not null))?"storage_full":null;
    }
}
