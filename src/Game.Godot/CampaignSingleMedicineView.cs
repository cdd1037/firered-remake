using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Single-battle selection and presentation. The campaign command owns every item effect.</summary>
public partial class CampaignView
{
    private int _singleMedicinePartyIndex;
    private string SingleBattleItemName(ItemId item)=>_catalog.PresetDoubleItems?.Find(item)?.Name??InventoryName(item);
    private bool TryOpenSingleBattleItem(ItemId item,CampaignWildBattle battle)
    {
        if(_captureBagPocket==2)return false;
        if(item==SsAnneItemRules.BerryPouchId)
        {_capturePouchSelected=_captureBagSelected;_captureBerries=true;_wildMenu="bag";_wildCursor=0;return true;}
        var definition=_catalog.PresetDoubleItems!.Find(item);
        if(definition is null||!(definition.IsFixedEffect||definition.IsPokeFlute||definition.IsEscapeItem))
        {
            _wildNotice=definition?.Symbol=="ITEM_ENIGMA_BERRY"?"This BERRY's effect data\nis not available.":"This item can't be used\nin battle.";
            _wildNoticeReturn="bag";return true;
        }
        _wildMedicineItem=item;
        if(definition.BattleUsage==2||definition.IsPokeFlute||definition.IsEscapeItem)
            SubmitSingleMedicine(battle.ActivePartyIndex);
        else {_wildMenu="medicine-target";_wildCursor=battle.ActivePartyIndex;}
        return true;
    }
    private void SubmitSingleMedicine(int target,int moveSlot=-1)
    {
        string menu=_wildMenu;
        if(!ApplyWildChoice(new("battle_medicine",Text:_wildMedicineItem!.Value.Value,PartyIndex:target,MoveSlot:moveSlot)
            {TargetInstanceId=_session.State.Party[target].InstanceId}))
        {_wildNotice=SingleMedicineNotice(_status);_wildNoticeReturn=menu;}
    }
    private string SingleMedicineNotice(string error)=>error switch
    {
        "source_item_no_effect"=>ItemText("gText_WontHaveEffect"),
        "source_item_not_in_bag"=>"There are none left\nin the BAG.",
        "source_item_escape_trainer"=>"You can't flee from a\nTRAINER battle!",
        "source_item_escape_special_scope"=>"You can't use that here.",
        "source_item_enigma_data_required"=>"This BERRY's effect data\nis not available.",
        _=>WildCommandError()
    };
    private bool SingleMedicineInput(InputEventKey key,CampaignWildBattle battle)
    {
        if(_wildMedicineItem is not {} item||_wildMenu is not ("medicine-target" or "medicine-move"))return false;
        bool move=_wildMenu=="medicine-move";
        int count=move?_session.State.Party[_singleMedicinePartyIndex].Pokemon.Moves.Length:_session.State.Party.Length;
        if(!move)NativePartyMove(key.Keycode,ref _wildCursor,count);
        else
        {
            if(key.Keycode is Key.Up or Key.Left)_wildCursor=Math.Max(0,_wildCursor-1);
            if(key.Keycode is Key.Down or Key.Right)_wildCursor=Math.Min(count,_wildCursor+1);
        }
        if(key.Keycode==Key.Backspace||key.Keycode==Key.Enter&&_wildCursor==count)
        {_wildMenu=move?"medicine-target":"bag";_wildCursor=move?_singleMedicinePartyIndex:_captureBagSelected;return true;}
        if(key.Keycode==Key.Enter)
        {
            if(move)SubmitSingleMedicine(_singleMedicinePartyIndex,_wildCursor);
            else if(_catalog.PresetDoubleItems!.Find(item)!.EffectBytes is {Length:>4} effect&&(effect[4]&0x10)!=0)
            {_singleMedicinePartyIndex=_wildCursor;_wildMenu="medicine-move";_wildCursor=0;}
            else SubmitSingleMedicine(_wildCursor);
        }
        return true;
    }
    private bool DrawSingleMedicineMove(CampaignWildBattle battle)
    {
        if(battle.Phase!=CampaignBattlePhase.ChooseMove||_wildMenu!="medicine-move")return false;
        _battleSkin.DrawBackground(this);_battleSkin.DrawBattleMessage(this);_battleSkin.DrawStandardFrame(this,new(0,0,240,112));
        var moves=_session.State.Party[_singleMedicinePartyIndex].Pokemon.Moves;
        for(int i=0;i<moves.Length;i++)
        {SourceText(_battleMoveFont,new(24,10+16*i),MoveName(moves[i].Move));SourceText(_sourceSmall,new(167,11+16*i),$"{moves[i].CurrentPp}/{moves[i].MaximumPp}");}
        SourceText(_battleMoveFont,new(24,10+16*moves.Length),"CANCEL");SourceText(_battleMenuFont,new(10,10+16*_wildCursor),">");
        WildLines(_wildNotice??"Restore which move's PP?");return true;
    }
    private void DrawSingleBattleItemList(ImmutableArray<InventoryStack> stacks,int selected)
    {
        _itemSkin.DrawBag(this);
        string title=_captureBerries?_catalog.TextPages("gText_BerryPouch_2")[0]:_itemSkin.Text("gText_Items2");
        float width=_sourceWhite.Font.GetStringSize(_sourceWhite.PrepareText(title),fontSize:_sourceWhite.Size).X;
        SourceText(_sourceWhite,OpeningItemUiSkin.BagTitleCenter-new Vector2((int)Math.Ceiling(width/2),0),title);
        if(!_captureBerries)_itemSkin.DrawPocketRightArrow(this);
        int start=Math.Max(0,selected-5);
        for(int i=start;i<Math.Min(stacks.Length,start+6);i++)
        {
            SourceText(_sourceNormal,OpeningItemUiSkin.ListText(false,i-start),SingleBattleItemName(stacks[i].Item));
            SourceText(_sourceSmall,OpeningItemUiSkin.QuantityText(false,i-start),_itemSkin.FormatText("gText_TimesStrVar1",stacks[i].Quantity.ToString().PadLeft(3)));
        }
        if(stacks.Length<start+6)SourceText(_sourceNormal,OpeningItemUiSkin.ListText(false,stacks.Length-start),_itemSkin.Text("gFameCheckerText_Cancel"));
        SourceText(_sourceNormal,OpeningItemUiSkin.ListCursor(false,selected-start),_itemSkin.Text("gText_SelectorArrow2"));
        if(selected<stacks.Length)
        {DrawInventoryIcon(stacks[selected].Item);ItemLines(_sourceWhite,OpeningItemUiSkin.DescriptionText,EarlyItem(stacks[selected].Item) is not null?InventoryDescription(stacks[selected].Item):SingleBattleItemName(stacks[selected].Item));}
        else ItemLines(_sourceWhite,OpeningItemUiSkin.DescriptionText,_itemSkin.Text("gText_CloseBag"));
    }
    private bool SingleMedicinePages(WildBattleMedicineAppliedEvent item,Action<string,(string Key,string Value)[]> source)
    {
        if(item.Actor is null)return false;
        if (item.Actor == WildBattleSide.Opponent)
        {
            source("sText_Trainer1UsedItem", [("B_LAST_ITEM", SingleBattleItemName(item.Item))]);
            return true;
        }
        source("sText_PlayerUsedItem",[("B_LAST_ITEM",SingleBattleItemName(item.Item))]);
        string name=PresentedBattlePlayerParty.Single(p=>p.InstanceId==item.TargetInstanceId).Nickname;
        if(item.HpAfter>item.HpBefore)source("gText_PkmnHPRestoredByVar2",[("STR_VAR_1",name),("STR_VAR_2",(item.HpAfter-item.HpBefore).ToString())]);
        else if(item.StatusBefore!=item.StatusAfter&&item.StatusAfter==0)source("gText_PkmnBecameHealthy",[("STR_VAR_1",name)]);
        if(item.PpRecovery is {IsEmpty:false})source("gText_PPWasRestored",[]);
        return true;
    }
    private ForestMessage? SingleMedicineEffectMessage(WildBattleEvent value)=>value switch
    {
        WildBattleConfusionChangedEvent {Reason:"item",After:0} x=>new("gText_PkmnSnappedOutOfConfusion",("STR_VAR_1",WildName(x.Side))),
        WildBattleInfatuationChangedEvent {Reason:"item",After:null} x=>new("gText_PkmnGotOverInfatuation",("STR_VAR_1",WildName(x.Side))),
        WildBattleMtMoonVolatileEvent {Volatile:"mist",Reason:"item"} x=>new("gBattleText_MistShroud",("B_ATK_PREFIX2",x.Side==WildBattleSide.Player?"Ally":"Foe")),
        _=>null
    };
}
