using Godot;

public partial class CampaignView
{
    private void DrawBattleBagActions(bool berries, int selected)
    {
        if (!berries)
        {
            _battleSkin.DrawStandardFrame(this, OpeningItemUiSkin.BattleBagActionFrame);
            SourceText(_sourceNormal, OpeningItemUiSkin.BattleBagActionText(0), _itemSkin.Text("gOtherText_Use"));
            SourceText(_sourceNormal, OpeningItemUiSkin.BattleBagActionText(1), _itemSkin.Text("gFameCheckerText_Cancel"));
            SourceText(_sourceNormal, OpeningItemUiSkin.BattleBagActionCursor(selected), _itemSkin.Text("gText_SelectorArrow2"));
            return;
        }
        _battleSkin.DrawStandardFrame(this, new(168,96,72,64));
        var actions = new[] { InventoryActionLabel(InventoryMenuAction.Use), InventoryActionLabel(InventoryMenuAction.Toss), InventoryActionLabel(InventoryMenuAction.Exit) };
        for (int i=0;i<actions.Length;i++) SourceText(_sourceNormal,new(184,106+16*i),actions[i]);
        SourceText(_sourceNormal,new(176,106+16*selected),_itemSkin.Text("gText_SelectorArrow2"));
    }
}
