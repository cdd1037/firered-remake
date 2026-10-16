using System.Globalization;
using Game.Application;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    private string? _bagTossWait;
    private int _bagTossChoice;
    // pret bag.c sWindowTemplates[0,3,7,8,9]; Berry Pouch uses the same bounds.
    private static readonly Rect2 BagTossQuantityFrame = new(184,112,56,48);
    private static readonly Rect2 BagTossConfirmFrame = new(176,112,64,48);
    private static readonly Rect2 BagTossQuantityMessage = new(40,112,144,48);
    private static readonly Rect2 BagTossConfirmMessage = new(40,112,136,48);

    private void SelectFieldBagPocket(int pocket)
    {
        if(_session.State.ItemMenu is not { Origin:"bag",View:"bag",Toss:null } menu||pocket==menu.Pocket)return;
        bool accepted = Apply(new("bag_pocket",Choice:pocket));
        if(accepted){_questPocketCursor=0;_questPocketAction=0;_itemCursor=0;_itemAction=0;}
    }

    private void OpenBagToss(ItemId item)
    {
        bool accepted = Apply(new("bag_toss_open",Text:item.Value));
        _itemPopup=false;_questPocketPopup=false;
        if(!accepted)_itemNotice="That item can't be tossed.";
    }

    private void SynchronizeBagToss()
    {
        var toss=_session.State.ItemMenu?.Toss;
        bool closed=_bagTossWait is not null&&toss is null;
        if(toss?.WaitId!=_bagTossWait){_bagTossWait=toss?.WaitId;_bagTossChoice=0;}
        if(toss is not null)
        {
            _itemPopup=false;_itemQuantityOpen=false;_questPocketPopup=false;
            _itemNotice=null;_questPocketNotice=null;
            if(toss.Pocket=="POCKET_POKE_BALLS")
                _questPocketCursor=Math.Max(0,Enumerable.Range(0,QuestPocketStacks.Length).FirstOrDefault(i=>QuestPocketStacks[i].Item==toss.Item,0));
            else _itemCursor=Math.Max(0,Enumerable.Range(0,FieldItemStacks.Length).FirstOrDefault(i=>FieldItemStacks[i].Item==toss.Item,0));
        }
        else if(closed&&_session.State.Inventory is not null)
        {
            _itemCursor=Math.Min(_itemCursor,FieldItemStacks.Length);
            _questPocketCursor=Math.Min(_questPocketCursor,QuestPocketStacks.Length);
        }
    }

    private bool BagTossInput(InputEventKey key)
    {
        if(_session.State.ItemMenu?.Toss is not {} toss||key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape)return false;
        if(!key.Pressed||key.Echo)return true;
        _held=null;
        if(toss.Phase==CampaignBagTossPhase.Result)
        {
            if(key.Keycode is Key.Enter or Key.Backspace)Apply(new("bag_toss_ack",Text:toss.WaitId));
            return true;
        }
        if(key.Keycode==Key.Backspace){Apply(new("bag_toss_back",Text:toss.WaitId));return true;}
        if(toss.Phase==CampaignBagTossPhase.Quantity)
        {
            // menu_helpers.c AdjustQuantityAccordingToDPadInput: vertical wraps;
            // horizontal advances ten and clamps. The owner validates each value.
            int quantity=key.Keycode switch {
                Key.Up=>toss.Quantity==toss.Available?1:toss.Quantity+1,
                Key.Down=>toss.Quantity==1?toss.Available:toss.Quantity-1,
                Key.Right=>Math.Min(toss.Available,toss.Quantity+10),
                Key.Left=>Math.Max(1,toss.Quantity-10),_=>toss.Quantity};
            if(quantity!=toss.Quantity)Apply(new("bag_toss_quantity",Quantity:quantity,Text:toss.WaitId));
            else if(key.Keycode==Key.Enter)Apply(new("bag_toss_select",Text:toss.WaitId));
            return true;
        }
        if(key.Keycode==Key.Up)_bagTossChoice=0;
        if(key.Keycode==Key.Down)_bagTossChoice=1;
        if(key.Keycode==Key.Enter)Apply(new("bag_toss_confirm",Choice:_bagTossChoice,Text:toss.WaitId));
        return true;
    }

    private bool DrawBagToss()
    {
        if(_session.State.ItemMenu?.Toss is not {} toss)return false;
        if(toss.Pocket=="POCKET_POKE_BALLS")DrawQuestPocket();
        else DrawItemList(_session.State.Inventory!,toss.Pocket=="POCKET_BERRY_POUCH"?"berries":"bag",_itemCursor,submenu:true);
        var frame=toss.Phase switch {
            CampaignBagTossPhase.Quantity=>BagTossQuantityMessage,
            CampaignBagTossPhase.Confirm=>BagTossConfirmMessage,
            _=>OpeningItemUiSkin.PcResultFrame};
        _itemSkin.DrawStandardFrame(this,frame);
        ItemLines(_sourceNormal,OpeningItemUiSkin.PcResultText,toss.Message);
        if(toss.Phase==CampaignBagTossPhase.Quantity)
        {
            _battleSkin.DrawStandardFrame(this,BagTossQuantityFrame);
            SourceText(_sourceSmall,new(196,130),_itemSkin.FormatText("gText_TimesStrVar1",toss.Quantity.ToString("D3",CultureInfo.InvariantCulture)));
            // The reused pocket arrow is the horizontal flip of source frame 0.
            // Undo that flip and rotate clockwise: pixel-identical to up frame 1.
            DrawSetTransform(new(212,120),Mathf.Pi/2,new(-1,1));
            DrawTexture(_itemSkin.Texture("pocket-right-arrow"),new(-8,-8));
            DrawSetTransform(new(212,152),Mathf.Pi/2);
            DrawTexture(_itemSkin.Texture("pocket-right-arrow"),new(-8,-8));
            DrawSetTransform(Vector2.Zero);
        }
        else if(toss.Phase==CampaignBagTossPhase.Confirm)
        {
            _battleSkin.DrawStandardFrame(this,BagTossConfirmFrame);
            SourceText(_sourceNormal,new(192,122),"YES");SourceText(_sourceNormal,new(192,138),"NO");
            SourceText(_sourceNormal,new(184,122+_bagTossChoice*16),_itemSkin.Text("gText_SelectorArrow2"));
        }
        return true;
    }

    private bool BagTossMouse(Vector2 point,int? direction=null)
    {
        var toss=_session.State.ItemMenu!.Toss!;
        if(toss.Phase==CampaignBagTossPhase.Confirm)
            return direction is {} move?PointerRows(point,move,ref _bagTossChoice,2,184,120,48)
                :MouseSelect(point,ref _bagTossChoice,2,184,120,48);
        if(toss.Phase==CampaignBagTossPhase.Result)
            return direction is null&&MouseConfirm(point,OpeningItemUiSkin.PcResultFrame);
        if(direction is {} delta)return delta!=0&&BagTossQuantityFrame.HasPoint(point)&&MouseNavigationKey(delta);
        if(new Rect2(204,112,16,16).HasPoint(point))return MouseKey(Key.Up);
        if(new Rect2(204,144,16,16).HasPoint(point))return MouseKey(Key.Down);
        return MouseConfirm(point,BagTossQuantityMessage)||MouseConfirm(point,BagTossQuantityFrame);
    }
}
