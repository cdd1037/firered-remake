using Game.Application;
using Godot;

public partial class CampaignView
{
    private SeagallopDestinationMenu? _seagallopMenu;
    private string? _seagallopMenuWaitId;
    private bool SeagallopMenuActive=>_session.State.Seagallop is null&&_session.State.SeagallopMenu is not null;
    private bool SeagallopMenuVisible=>SeagallopMenuActive&&FieldTextComplete&&
        (_session.State.Dialogue is null||_session.State.Dialogue.Page+1==_session.State.Dialogue.Pages.Length);
    private int SeagallopMenuTextWidth=>(int)(_seagallopMenu??throw new InvalidDataException("Ferry menu missing.")).Options.Max(row=>
        _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(row),fontSize:_sourceNormal.Size).X);
    private bool SeagallopMenuReady=>SeagallopMenuVisible&&_tracks.All(t=>t.Time>=t.Duration)&&_waitElapsed>=_waitDuration&&_fade<=0;
    private void SynchronizeSeagallopMenu()
    {
        if(!SeagallopMenuActive){_seagallopMenu=null;_seagallopMenuWaitId=null;return;}
        var source=_session.State.SeagallopMenu!;string? id=_session.State.Script.Wait?.CorrelationId;
        if(id is null)throw new InvalidDataException("Ferry menu has no correlated source wait.");
        if(_seagallopMenuWaitId==id&&_seagallopMenu is {} current)
        {
            if(current.SourceMenu!=source.SourceMenu||!current.Options.SequenceEqual(source.Options))throw new InvalidDataException("Ferry menu changed within one source wait.");
            return;
        }
        _seagallopMenu=new(source.SourceMenu,source.Options);_seagallopMenuWaitId=id;_held=null;}
    private bool TrySeagallopMenuInput(InputEventKey key)
    {
        if(!SeagallopMenuActive||key.Keycode is Key.Escape or Key.F12 or Key.F5 or Key.F9)return false;
        _held=null;if(!key.Pressed||!SeagallopMenuReady)return true;
        var menu=_seagallopMenu??throw new InvalidDataException("Ferry menu missing.");
        if(key.Keycode is Key.Up or Key.Down)
        {if(menu.Move(key.Keycode==Key.Up?-1:1))SourceSound("SE_SELECT");}
        else if(key.Keycode is Key.Enter or Key.Backspace)
        {
            SourceSound("SE_SELECT");Apply(new("ack",Choice:key.Keycode==Key.Enter?menu.Cursor:SeagallopDestinationMenu.Cancel));
        }
        return true;
    }
    private bool NavigateSeagallopPointer(Vector2 point,int direction)
    {
        if(!SeagallopMenuReady||_seagallopMenu is not {} menu)return false;
        int row=menu.RowAt(point.X,point.Y,SeagallopMenuTextWidth);if(row<0)return false;
        return direction!=0?MouseNavigationKey(direction):menu.Select(row);
    }
    private bool MouseSeagallopMenu(Vector2 point)
    {
        if(!SeagallopMenuReady||_seagallopMenu is not {} menu)return false;
        int row=menu.RowAt(point.X,point.Y,SeagallopMenuTextWidth);if(row<0)return false;
        menu.Select(row);return MouseKey();
    }
    private void DrawSeagallopMenu()
    {
        if(!SeagallopMenuVisible||_seagallopMenu is not {} menu)return;
        var frame=menu.Frame(SeagallopMenuTextWidth);
        _battleSkin.DrawUserFrame(this,new(frame.X,frame.Y,frame.Width,frame.Height));
        for(int i=0;i<menu.Options.Count;i++)SourceText(_sourceNormal,new(frame.X+16,frame.Y+10+i*14),menu.Options[i]);
        SourceText(_sourceNormal,new(frame.X+8,frame.Y+10+menu.Cursor*14),"▶");
    }
}
