using Godot;

public partial class CampaignView
{
    private string? _regionalServiceNotice;
    private ulong _regionalServiceNoticeDismissedFrame=ulong.MaxValue;
    private bool TryRegionalServiceNoticeInput(InputEvent input)
    {
        if(_regionalServiceNoticeDismissedFrame==Engine.GetProcessFrames())
        {GetViewport().SetInputAsHandled();return true;}
        if(_regionalServiceNotice is null)return false;
        bool dismiss=input is InputEventKey {Pressed:true,Echo:false,Keycode:Key.Enter or Key.KpEnter or Key.Backspace or Key.Escape};
        if(input is InputEventMouseButton {Pressed:true,DoubleClick:false,ButtonIndex:MouseButton.Left or MouseButton.Right} mouse &&
            !_menuMousePressed&&!_menuRightMousePressed&&_menuMouseActionFrame!=Engine.GetProcessFrames()&&
            GetViewport().GetVisibleRect().HasPoint(mouse.Position)&&
            OpeningItemUiSkin.LogicalRect.HasPoint(GetGlobalTransformWithCanvas().AffineInverse()*mouse.Position))
        {
            if(mouse.ButtonIndex==MouseButton.Right)_menuRightMousePressed=true;else _menuMousePressed=true;
            _menuMouseActionFrame=Engine.GetProcessFrames();dismiss=true;
        }
        if(dismiss){_regionalServiceNotice=null;_regionalServiceNoticeDismissedFrame=Engine.GetProcessFrames();}
        if(input is InputEventKey {Keycode:Key.F12})return false;
        _held=null;QueueRedraw();GetViewport().SetInputAsHandled();return true;
    }
}
