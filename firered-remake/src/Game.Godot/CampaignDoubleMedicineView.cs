using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Ordinary doubles medicine selection. Only the accepted command spends an item;
/// the same owned identity and typed result drive active, partner and reserve presentation.</summary>
public partial class CampaignView
{
    private int _doubleBagPocket, _doubleBagCursor, _doublePouchCursor;
    private bool _doubleBerries;
    private ItemId? _doubleMedicineItem;
    private int _doubleMedicinePartyIndex;
    private ImmutableArray<InventoryStack> DoubleBagStacks => _session.State.Inventory is not {} inventory?[]:
        _doubleBerries?inventory.Berries??[]:_doubleBagPocket switch
        {0=>inventory.BagItems,1=>inventory.KeyItems??[],_=>inventory.Balls??[]};

    private void OpenDoubleBag()
    {
        _doubleMenu="bag";_doubleCursor=0;_doubleBagPocket=0;_doubleBagCursor=0;
        _doubleBerries=false;_doubleMedicineItem=null;
    }
    private bool DoubleMedicineAvailable(ItemId item) => _catalog.PresetDoubleItems?.Find(item) is {} definition&&
        (definition.IsFixedEffect||definition.IsPokeFlute);
    private void SubmitDoubleMedicine(int actor,int partyIndex,int moveSlot=-1)
    {
        var target=_session.State.Party[partyIndex];long revision=_session.Revision;
        Apply(new("double_medicine",PartyIndex:actor,Choice:partyIndex,Text:_doubleMedicineItem!.Value.Value,MoveSlot:moveSlot){TargetInstanceId=target.InstanceId});
        if(_session.Revision==revision)_doubleNotice=DoubleMedicineNotice(_status);
    }
    private bool DoubleMedicineInput(InputEventKey key,int actor)
    {
        if(_doubleMenu is not ("bag" or "medicine-target" or "medicine-move"))return false;
        bool back=key.Keycode is Key.Backspace or Key.B;
        bool confirm=key.Keycode is Key.Enter or Key.Space;
        if(_doubleMenu=="medicine-move")
        {
            int count=_session.State.Party[_doubleMedicinePartyIndex].Pokemon.Moves.Length;
            if(key.Keycode is Key.Up or Key.Left)_doubleCursor=Math.Max(0,_doubleCursor-1);
            if(key.Keycode is Key.Down or Key.Right)_doubleCursor=Math.Min(count,_doubleCursor+1);
            if(back||confirm&&_doubleCursor==count)
            {_doubleMenu="medicine-target";_doubleCursor=_doubleMedicinePartyIndex;return true;}
            if(confirm)SubmitDoubleMedicine(actor,_doubleMedicinePartyIndex,_doubleCursor);
            return true;
        }
        if(_doubleMenu=="medicine-target")
        {
            int count=_session.State.Party.Length;
            if(key.Keycode is Key.Up or Key.Left)_doubleCursor=Math.Max(0,_doubleCursor-1);
            if(key.Keycode is Key.Down or Key.Right)_doubleCursor=Math.Min(count,_doubleCursor+1);
            if(back||confirm&&_doubleCursor==count)
            {_doubleMenu="bag";_doubleCursor=Math.Min(_doubleBagCursor,DoubleBagStacks.Length);_doubleMedicineItem=null;return true;}
            if(confirm&&_doubleMedicineItem is {} item)
            {
                var definition=_catalog.PresetDoubleItems!.Find(item)!;
                if(definition.EffectBytes.Length>4&&(definition.EffectBytes[4]&0x10)!=0)
                {_doubleMedicinePartyIndex=_doubleCursor;_doubleMenu="medicine-move";_doubleCursor=0;}
                else SubmitDoubleMedicine(actor,_doubleCursor);
            }
            return true;
        }
        if(!_doubleBerries&&key.Keycode is Key.Left or Key.Right)
        {_doubleBagPocket=Math.Clamp(_doubleBagPocket+(key.Keycode==Key.Left?-1:1),0,2);_doubleCursor=0;return true;}
        var stacks=DoubleBagStacks;_doubleCursor=Math.Clamp(_doubleCursor,0,stacks.Length);
        if(key.Keycode==Key.Up)_doubleCursor=Math.Max(0,_doubleCursor-1);
        if(key.Keycode==Key.Down)_doubleCursor=Math.Min(stacks.Length,_doubleCursor+1);
        if(back||confirm&&_doubleCursor==stacks.Length)
        {
            if(_doubleBerries){_doubleBerries=false;_doubleCursor=Math.Min(_doublePouchCursor,DoubleBagStacks.Length);}
            else {_doubleMenu="actions";_doubleCursor=1;}
            return true;
        }
        if(confirm)
        {
            var item=stacks[_doubleCursor].Item;
            if(!_doubleBerries&&_doubleBagPocket==1&&item==SsAnneItemRules.BerryPouchId)
            {_doublePouchCursor=_doubleCursor;_doubleBerries=true;_doubleCursor=0;return true;}
            if(!DoubleMedicineAvailable(item))
            {_doubleNotice="This item's double-battle\nuse is not available yet.";return true;}
            _doubleMedicineItem=item;_doubleBagCursor=_doubleCursor;
            int member=_session.State.DoubleBattle!.Frame.Get(new(actor)).Member!.Value.Index;
            if(_catalog.PresetDoubleItems!.Find(item)!.BattleUsage==2)SubmitDoubleMedicine(actor,member);
            else {_doubleMenu="medicine-target";_doubleCursor=member;}
        }
        return true;
    }
    private string DoubleMedicineNotice(string error) => error switch
    {
        "source_item_no_effect" or "medicine_no_effect" or "potion_no_effect"=>ItemText("gText_WontHaveEffect"),
        "source_item_not_in_bag" or "medicine_not_in_bag" or "potion_not_in_bag"=>"There are none left\nin the BAG.",
        _ when error.Contains("target",StringComparison.Ordinal)=>"Choose the target again.",
        _ when error.Contains("unsupported",StringComparison.Ordinal)=>"This item's double-battle\nuse is not available yet.",
        _=>"That item can't be used\nright now."
    };
    private ImmutableArray<string> DoubleMedicinePages(SourceMedicineAppliedEvent medicine)
    {
        var target=_session.State.Party.Single(p=>p.InstanceId==medicine.TargetInstanceId);
        string symbol=medicine.HpAfter>medicine.HpBefore?"gText_PkmnHPRestoredByVar2":
            WildBattleStatus.IsSleeping(medicine.StatusBefore)?"gText_PkmnWokeUp2":ForestPresentationStatus(medicine.StatusBefore) switch
            {WildBattleStatus.Poison=>"gText_PkmnCuredOfPoison",WildBattleStatus.Burn=>"gText_PkmnBurnHealed",
             WildBattleStatus.Freeze=>"gText_PkmnThawedOut",WildBattleStatus.Paralysis=>"gText_PkmnCuredOfParalysis",
             _=>throw new InvalidDataException("Unsupported doubles medicine result message.")};
        return DoubleSource(symbol,("STR_VAR_1",target.Nickname),("STR_VAR_2",(medicine.HpAfter-medicine.HpBefore).ToString()));
    }
    private int DoubleMedicineHp(SourceMedicineAppliedEvent medicine)=>(int)Math.Round(Mathf.Lerp(medicine.HpBefore,medicine.HpAfter,(float)Math.Clamp(_doubleElapsed/.4,0,1)));
    private uint DoubleMedicineStatus(SourceMedicineAppliedEvent medicine)=>_doubleElapsed<.4?medicine.StatusBefore:medicine.StatusAfter;

    private bool DrawDoubleMedicineOverlay(CampaignDoubleBattle battle)
    {
        if(_doubleMenu=="bag"){DrawDoubleBag();return true;}
        if(_doubleMenu=="medicine-move")
        {
            _battleSkin.DrawBackground(this);_battleSkin.DrawBattleMessage(this);
            _battleSkin.DrawStandardFrame(this,new(0,0,240,112));
            var moves=_session.State.Party[_doubleMedicinePartyIndex].Pokemon.Moves;
            for(int i=0;i<moves.Length;i++)
            {SourceText(_battleMoveFont,new(24,10+16*i),MoveName(moves[i].Move));SourceText(_sourceSmall,new(167,11+16*i),$"{moves[i].CurrentPp}/{moves[i].MaximumPp}");}
            SourceText(_battleMoveFont,new(24,10+16*moves.Length),"CANCEL");SourceText(_battleMenuFont,new(10,10+16*_doubleCursor),">");
            DoubleLines(_doubleNotice??"Restore which move's PP?");return true;
        }
        var medicine=battle.CurrentEvent as SourceMedicineAppliedEvent;
        bool playerTarget=medicine is not null&&(medicine.TargetSide??BattleSide.Player)==BattleSide.Player;
        bool partyMedicine=playerTarget&&_catalog.PresetDoubleItems?.Find(medicine!.Item)?.BattleUsage!=2;
        if(_doubleMenu!="medicine-target"&&!partyMedicine)return false;
        _battleSkin.DrawBackground(this);_battleSkin.DrawBattleMessage(this);
        _battleSkin.DrawStandardFrame(this,new(0,0,240,112));
        for(int i=0;i<_session.State.Party.Length;i++)
        {
            var owned=_session.State.Party[i];
            if(medicine is not null&&owned.InstanceId==medicine.TargetInstanceId)
                owned=owned with{Pokemon=owned.Pokemon with{CurrentHp=DoubleMedicineHp(medicine)},Status=DoubleMedicineStatus(medicine)};
            bool active=battle.Frame.Slots.Any(s=>s.Member==new PartyMemberKey(BattleSide.Player,i));
            SourceText(_battleMoveFont,new(24,7+14*i),(active?">":" ")+owned.Nickname);
            SourceText(_sourceSmall,new(122,8+14*i),$"L{owned.Pokemon.Level} {owned.Pokemon.CurrentHp}/{owned.Pokemon.Stats.MaxHp}");
            DrawForestPartyStatus(owned,new(198,10+14*i));
        }
        if(medicine is null)
        {
            SourceText(_battleMoveFont,new(24,7+14*_session.State.Party.Length),"CANCEL");
            SourceText(_battleMenuFont,new(10,7+14*_doubleCursor),">");
            DoubleLines(_doubleNotice??"Use on which POKéMON?");
        }
        else
        {
            DoubleLines(_doubleNotice??_doublePages[_doublePage]);
            if(_doubleNotice is null)DrawTextCursor(_battleWhite,LabBattleUiSkin.BattleMessageText,_doublePages[_doublePage].Split('\n'),16,_doubleElapsed,true);
        }
        return true;
    }
    private void DrawDoubleBag()
    {
        var stacks=DoubleBagStacks;int selected=Math.Clamp(_doubleCursor,0,stacks.Length);
        if(_doubleBagPocket==0||_doubleBerries)DrawItemList(_session.State.Inventory!,_doubleBerries?"berries":"bag",selected);
        else
        {
            DrawQuestPocketAsset(_doubleBagPocket==1?"bag-key-items-background":"bag-poke-balls-background");
            string title=_questPocketUi!.Texts[_doubleBagPocket==1?"gText_KeyItems2":"gText_PokeBalls2"];
            float width=_sourceWhite.Font.GetStringSize(_sourceWhite.PrepareText(title),fontSize:_sourceWhite.Size).X;
            SourceText(_sourceWhite,OpeningItemUiSkin.BagTitleCenter-new Vector2((int)Math.Ceiling(width/2),0),title);
            DrawQuestPocketAsset("pocket-left-arrow");if(_doubleBagPocket==1)_itemSkin.DrawPocketRightArrow(this);
            int start=Math.Max(0,selected-5);
            for(int i=start;i<Math.Min(stacks.Length,start+6);i++)
            {
                SourceText(_sourceNormal,OpeningItemUiSkin.ListText(false,i-start),QuestPocketItem(stacks[i]).Name);
                SourceText(_sourceSmall,OpeningItemUiSkin.QuantityText(false,i-start),_itemSkin.FormatText("gText_TimesStrVar1",stacks[i].Quantity.ToString().PadLeft(3)));
            }
            if(stacks.Length<start+6)SourceText(_sourceNormal,OpeningItemUiSkin.ListText(false,stacks.Length-start),_itemSkin.Text("gFameCheckerText_Cancel"));
            SourceText(_sourceNormal,OpeningItemUiSkin.ListCursor(false,selected-start),_itemSkin.Text("gText_SelectorArrow2"));
            if(selected<stacks.Length)
            {var item=QuestPocketItem(stacks[selected]);DrawQuestPocketAsset(item.Icon);ItemLines(_sourceWhite,OpeningItemUiSkin.DescriptionText,item.Description);}
            else ItemLines(_sourceWhite,OpeningItemUiSkin.DescriptionText,_itemSkin.Text("gText_CloseBag"));
        }
        if(_doubleNotice is not null)ItemMessageBox(_doubleNotice);
    }
}
