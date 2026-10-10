using Game.Application;
using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    private int _itemCursor,_itemAction,_itemQuantity=1;
    private bool _itemPopup,_itemQuantityOpen;
    private string? _itemNotice;
    private string? _itemMenuKey;
    private string ItemText(string symbol)=>string.Join("\n",_catalog.TextPages(symbol)).Replace("{PLAYER}",_session.State.PlayerName,StringComparison.Ordinal).Replace("{STR_VAR_1}",_catalog.OpeningItems!.Potion.Name,StringComparison.Ordinal);
    private void SynchronizeItems()
    {
        var menu=_session.State.ItemMenu;var key=menu is null?null:$"{menu.Origin}:{menu.View}";
        if(key!=_itemMenuKey){ResetQuestPocketPresentation();_itemMenuKey=key;_itemCursor=0;_itemAction=0;_itemQuantity=1;_itemPopup=false;_itemQuantityOpen=false;_itemNotice=null;_fieldMedicineTargetOpen=false;_mtMoonMoveSlotOpen=false;}
        if(menu is not null)_held=null;
    }
    private bool ItemInput(InputEventKey key)
    {
        if(key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape)return false;
        if(!key.Pressed)return true;
        var menu=_session.State.ItemMenu!;
        if(_itemNotice is not null){if(key.Keycode is Key.Enter or Key.Backspace)_itemNotice=null;return true;}
        if(_fieldMedicineTargetOpen)return MtMoonTargetActive?MtMoonTargetInput(key):FieldMedicineTargetInput(key);
        if(menu.View=="message"){if(key.Keycode is Key.Enter or Key.Backspace)Apply(new("menu_ack"));return true;}
        if(_itemQuantityOpen)
        {
            var stacks=menu.View=="withdraw"?_session.State.Inventory!.PcItems:FieldItemStacks;int max=stacks[_itemCursor].Quantity;
            if(key.Keycode is Key.Up or Key.Right)_itemQuantity=Math.Min(max,_itemQuantity+1);
            if(key.Keycode is Key.Down or Key.Left)_itemQuantity=Math.Max(1,_itemQuantity-1);
            if(key.Keycode==Key.Backspace)_itemQuantityOpen=false;
            if(key.Keycode==Key.Enter)TransferItem(menu.View);
            return true;
        }
        if(_itemPopup)
        {
            int count=menu.View=="withdraw"?3:2;
            if(key.Keycode==Key.Up)_itemAction=Math.Max(0,_itemAction-1);
            if(key.Keycode==Key.Down)_itemAction=Math.Min(count-1,_itemAction+1);
            if(key.Keycode==Key.Backspace){_itemPopup=false;return true;}
            if(key.Keycode==Key.Enter)
            {
                if(_itemAction==count-1){_itemPopup=false;return true;}
                if(menu.View=="withdraw"&&_itemAction==1)
                {_itemPopup=false;Apply(new("pc_give",Text:OpeningInventoryRules.PotionId.Value));if(_session.State.ItemMenu?.View!="message")_itemNotice="Held-item transfer is not available yet.";return true;}
                if(menu.Origin=="bag"){_itemPopup=false;if(_catalog.EarlyShop is not null){var item=FieldItemStacks[_itemCursor].Item;if(TryOpenMtMoonItem(item))return true;if(_catalog.MtMoonEnabled&&item==EscapeRopeRules.Item){var revision=_session.Revision;Apply(new("field_escape_rope",Text:item.Value));if(_session.Revision==revision)_itemNotice=FieldConsumableNotice(_status);return true;}if(_catalog.RepelEnabled&&(item==EarlyRepelRules.Item||_catalog.CeladonApproachEnabled&&item==LavenderItemRules.SuperRepelId)){var revision=_session.Revision;Apply(new("field_repel",Text:item.Value));if(_session.Revision==revision)_itemNotice=_status;return true;}if(EarlyItem(item)?.UseSupport is not (EarlyShopUseSupport.Potion or EarlyShopUseSupport.StatusCure)&&!(_catalog.EarlyStarterEnabled&&EarlyItem(item)?.UseSupport==EarlyShopUseSupport.DeferredSleep)){_itemNotice="This item's use is not\nimplemented yet.";return true;}_fieldMedicineTarget=0;_fieldMedicineTargetOpen=true;return true;}Apply(new("field_potion"));if(_session.State.ItemMenu?.View!="message")_itemNotice=_status;return true;}
                var stacks=menu.View=="withdraw"?_session.State.Inventory!.PcItems:FieldItemStacks;
                if(stacks[_itemCursor].Item!=OpeningInventoryRules.PotionId){_itemPopup=false;_itemNotice="PC storage for this item is\nnot implemented yet.";return true;}
                if(stacks[_itemCursor].Quantity>1){_itemQuantity=1;_itemQuantityOpen=true;_itemPopup=false;}else TransferItem(menu.View);
            }
            return true;
        }
        if(key.Keycode==Key.Backspace){Apply(new("menu_back"));return true;}
        string[] choices=menu.View switch{"top"=>["storage","mailbox","off"],"storage"=>["withdraw","deposit","cancel"],_=>[]};
        var inventory=_session.State.Inventory!;int countChoices=choices.Length>0?choices.Length:(menu.View=="withdraw"?inventory.PcItems:FieldItemStacks).Length+1;
        if(key.Keycode==Key.Up)_itemCursor=Math.Max(0,_itemCursor-1);
        if(key.Keycode==Key.Down)_itemCursor=Math.Min(countChoices-1,_itemCursor+1);
        if(key.Keycode==Key.Enter)
        {
            if(choices.Length>0)Apply(new("menu_select",Text:choices[_itemCursor]));
            else if(_itemCursor==countChoices-1)Apply(new("menu_back"));
            else{_itemPopup=true;_itemAction=0;}
        }
        return true;
    }
    private void TransferItem(string view)
    {
        var revision=_session.Revision;Apply(new(view=="withdraw"?"pc_withdraw":"pc_deposit",Text:(view=="withdraw"?_session.State.Inventory!.PcItems:FieldItemStacks)[_itemCursor].Item.Value,Quantity:_itemQuantity));
        _itemPopup=false;_itemQuantityOpen=false;if(_session.Revision==revision)_itemNotice=_status;
    }
    private void ItemLabel(Vector2 position,string text,int size=9,Color? color=null)=>SourceText(color==Colors.White?_sourceWhite:size<9?_sourceSmall:_sourceNormal,position,text);
    private void ItemMessageBox(string text)
    {
        _battleSkin.DrawDialogue(this);var lines=text.Split('\n');for(int i=0;i<Math.Min(2,lines.Length);i++)ItemLabel(LabBattleUiSkin.DialogueText+new Vector2(0,i*15),lines[i]);
    }
    private void DrawItemMenu()
    {
        var menu=_session.State.ItemMenu!;var view=menu.View=="message"?menu.ReturnView:menu.View;
        if(view is "top" or "storage")
        {
            var symbols=view=="top"?new[]{"gText_ItemStorage","gText_Mailbox","gText_TurnOff"}:new[]{"gText_WithdrawItem2","gText_DepositItem2","gFameCheckerText_Cancel"};
            _battleSkin.DrawUserFrame(this,new(0,0,view=="top"?120:128,64));
            for(int i=0;i<symbols.Length;i++)ItemLabel(new(16,10+i*16),ItemText(symbols[i]));ItemLabel(new(8,10+_itemCursor*16),"▶");
            string prompt=view=="top"?ItemText("gText_WhatWouldYouLikeToDo"):ItemText(_itemCursor switch{0=>"gText_TakeOutItemsFromThePC",1=>"gText_StoreItemsInThePC",_=>"gText_GoBackToThePreviousMenu"});
            ItemMessageBox(menu.View=="message"?_session.VisibleItemMessage!:prompt);
        }
        else DrawItemList(menu.DisplayInventory??_session.State.Inventory!,view,_itemCursor,menu.View=="message"?_session.VisibleItemMessage:null,_itemPopup||_itemQuantityOpen);
        if(_itemPopup)
        {
            string[] labels=view=="withdraw"?[_itemSkin.Text("gText_Withdraw"),_itemSkin.Text("gOtherText_Give"),_itemSkin.Text("gFameCheckerText_Cancel")]:[view=="deposit"?"DEPOSIT":_itemSkin.Text("gOtherText_Use"),_itemSkin.Text("gFameCheckerText_Cancel")];
            DrawItemActionPopup(view=="withdraw",labels,_itemAction,InventoryName((view=="withdraw"?_session.State.Inventory!.PcItems:FieldItemStacks)[_itemCursor].Item));
        }
        if(_itemQuantityOpen)ItemMessageBox(ItemText(view=="withdraw"?"gText_WithdrawHowMany":"gText_DepositHowManyStrVars1")+"   × "+_itemQuantity);
        if(_fieldMedicineTargetOpen){if(MtMoonTargetActive)DrawMtMoonTargets();else DrawFieldMedicineTargets();}
        if(_itemNotice is not null)ItemMessageBox(_itemNotice);
        NotePokemonTowerFluteMessage();
    }
    private void DrawItemActionPopup(bool pc,string[] labels,int selected,string? itemName=null)
    {
        var frame=pc?OpeningItemUiSkin.PcActionFrame:OpeningItemUiSkin.BattleBagActionFrame;
        _battleSkin.DrawStandardFrame(this,frame);
        for(int i=0;i<labels.Length;i++)SourceText(_sourceNormal,pc?OpeningItemUiSkin.PcActionText(i):OpeningItemUiSkin.BattleBagActionText(i),labels[i]);
        SourceText(_sourceNormal,pc?OpeningItemUiSkin.PcActionCursor(selected):OpeningItemUiSkin.BattleBagActionCursor(selected),_itemSkin.Text("gText_SelectorArrow2"));
        _itemSkin.DrawStandardFrame(this,OpeningItemUiSkin.PcSelectedFrame);
        ItemLines(_sourceNormal,OpeningItemUiSkin.PcSelectedText,_itemSkin.FormatText("gText_Var1IsSelected",itemName??_itemSkin.PotionName));
    }
    private void ItemLines(SourceGameFont font,Vector2 origin,string text,int step=14)
    {var lines=text.Split('\n');for(int i=0;i<lines.Length;i++)SourceText(font,origin+new Vector2(0,i*step),lines[i]);}
    private void DrawItemList(InventoryState inventory,string view,int cursor,string? message=null,bool submenu=false)
    {
        bool pc=view=="withdraw";
        bool pcSubmenu=pc&&(submenu||message is not null);
        if(pcSubmenu)_uiCorrections.DrawPcSubmenuBackground(this);else if(pc)_itemSkin.DrawPcWithdraw(this);else if(view=="deposit")_itemSkin.DrawPcDeposit(this);else _itemSkin.DrawBag(this);
        if(pc)ItemLines(_sourceSmallWhite,OpeningItemUiSkin.PcTitleText,_itemSkin.Text("gText_WithdrawItem"));
        else
        {
            var title=view=="berries"?_catalog.TextPages("gText_BerryPouch_2")[0]:_itemSkin.Text(view=="deposit"?"gText_DepositItem":"gText_Items2");var center=view=="deposit"?OpeningItemUiSkin.DepositTitleCenter:OpeningItemUiSkin.BagTitleCenter;
            var font=view=="deposit"?_sourceSmall:_sourceWhite;float width=font.Font.GetStringSize(font.PrepareText(title),fontSize:font.Size).X;SourceText(font,center-new Vector2((int)Math.Ceiling(width/2),0),title);
            if(view is not ("deposit" or "berries"))_itemSkin.DrawPocketRightArrow(this);
        }
        var stacks=pc?inventory.PcItems:view=="berries"?inventory.Berries??[]:inventory.BagItems;
        cursor=Math.Clamp(cursor,0,stacks.Length);int start=Math.Max(0,cursor-5);
        for(int i=start;i<Math.Min(stacks.Length,start+6);i++)
        {
            SourceText(_sourceNormal,OpeningItemUiSkin.ListText(pc,i-start),InventoryName(stacks[i].Item));
            SourceText(_sourceSmall,OpeningItemUiSkin.QuantityText(pc,i-start),_itemSkin.FormatText("gText_TimesStrVar1",stacks[i].Quantity.ToString().PadLeft(3)));
        }
        if(stacks.Length<start+6)SourceText(_sourceNormal,OpeningItemUiSkin.ListText(pc,stacks.Length-start),_itemSkin.Text("gFameCheckerText_Cancel"));
        var cursorPosition=OpeningItemUiSkin.ListCursor(pc,cursor-start);
        SourceText(_sourceNormal,cursorPosition,_itemSkin.Text("gText_SelectorArrow2"));
        // item_pc.c prints the palette-2 cursor at window x=0 over the existing list cursor at x=1.
        if(pcSubmenu)SourceText(_sourceInactiveCursor,cursorPosition-new Vector2(1,0),_itemSkin.Text("gText_SelectorArrow2"));
        if(cursor<stacks.Length)DrawInventoryIcon(stacks[cursor].Item);
        if(!pcSubmenu)
        {
            if(cursor<stacks.Length)ItemLines(_sourceWhite,OpeningItemUiSkin.DescriptionText,InventoryDescription(stacks[cursor].Item));
            else ItemLines(_sourceWhite,OpeningItemUiSkin.DescriptionText,_itemSkin.Text(pc?"gText_ReturnToPC":"gText_CloseBag"));
        }
        if(message is not null){_itemSkin.DrawStandardFrame(this,OpeningItemUiSkin.PcResultFrame);ItemLines(_sourceNormal,OpeningItemUiSkin.PcResultText,message);}
    }
}
