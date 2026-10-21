using Game.Application;
using Game.Domain.Events;
using Godot;

public partial class CampaignView
{
    private const string DepartmentElevatorMapId="map.celadon-city-department-store-elevator";
    private static readonly string[] DepartmentElevatorRows=["gText_5F_2","gText_4F_2","gText_3F_2","gText_2F_2","gText_1F_2","gOtherText_Exit"];
    private string? _departmentMenuWait;
    private int _departmentElevatorCursor;
    private long _departmentElevatorEffect;
    private double _departmentElevatorTicks;
    private int _departmentElevatorDistance,_departmentElevatorDirection,_departmentElevatorDuration;
    private bool _departmentElevatorDing;
    private bool DepartmentElevatorMenuActive=>_session.State.World.Player.MapId.Value==DepartmentElevatorMapId&&
        _session.State.Script.Wait?.Kind==StoryWaitKind.MenuChoice&&_session.State.DepartmentElevator?.MenuDefault is not null;
    private bool DepartmentElevatorMenuVisible=>DepartmentElevatorMenuActive&&FieldTextComplete&&
        _session.State.Dialogue?.Page+1==_session.State.Dialogue?.Pages.Length;
    private int DepartmentElevatorMenuWidth=>(((int)DepartmentElevatorRows.Max(row=>
        _sourceNormal.MeasureText(SourceMenuLabelPresentation.Plain(_battleText,row)).X)+9)/8+1)*8;
    private bool DepartmentElevatorAnimating=>_departmentElevatorDistance>0&&
        _session?.State.World.Player.MapId.Value==DepartmentElevatorMapId&&_departmentElevatorTicks<_departmentElevatorDuration;
    private int DepartmentElevatorWindowPhase
    {
        get
        {
            if(_departmentElevatorDistance==0)return 0;
            int count=Math.Min(_catalog.DepartmentStoreElevatorWindowCycles(_departmentElevatorDistance),
                Math.Max(0,((int)Math.Floor(_departmentElevatorTicks)-1)/6));
            int phase=count%3;return _departmentElevatorDirection==0?phase:(3-phase)%3;
        }
    }
    private Vector2 DepartmentCameraOffset=>!DepartmentElevatorAnimating?Vector2.Zero:
        new(0,_departmentElevatorTicks<3?0:((int)Math.Floor(_departmentElevatorTicks/3)%2==0?1:-1));

    private void SynchronizeDepartmentPresentation(CampaignState? before,CampaignState after)
    {
        if(before is null||before.World.Player.MapId!=after.World.Player.MapId)
        {
            _departmentElevatorDistance=0;_departmentElevatorTicks=0;_departmentElevatorDuration=0;
            _departmentElevatorDing=false;_departmentElevatorEffect=before is null?after.Script.EffectSequence:0;
        }
        string? wait=DepartmentElevatorMenuActive?after.Script.Wait!.CorrelationId:null;
        if(wait!=_departmentMenuWait){_departmentMenuWait=wait;_departmentElevatorCursor=after.DepartmentElevator?.MenuDefault??0;}
        foreach(var effect in after.PresentationEffects.Where(e=>e.Sequence>_departmentElevatorEffect&&
            e.MapId.Value==DepartmentElevatorMapId&&e.Opcode=="special"&&e.Args.SequenceEqual(new[]{"AnimateElevator"})))
        {
            _departmentElevatorEffect=effect.Sequence;
            int from=after.Script.Variables.GetValueOrDefault("VAR_0x8005"),to=after.Script.Variables.GetValueOrDefault("VAR_0x8006");
            _departmentElevatorDistance=Math.Abs(from-to);_departmentElevatorDirection=from>to?1:0;
            _departmentElevatorTicks=0;_departmentElevatorDing=false;
            _departmentElevatorDuration=_catalog.DepartmentStoreElevatorShakeFrames(from,to);
            if(_departmentElevatorDistance is <1 or >4||after.PresentationTicks!=_departmentElevatorDuration)
                throw new InvalidDataException("Department elevator source presentation differs.");
            SourceSound("SE_ELEVATOR");
        }
    }
    private void ProcessDepartmentPresentation(double delta)
    {
        if(_session.State.World.Player.MapId.Value!=DepartmentElevatorMapId||_departmentElevatorDistance==0)return;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        _departmentElevatorTicks=Math.Min(_departmentElevatorDuration,_departmentElevatorTicks+delta*60);
        if(!_departmentElevatorDing&&_departmentElevatorTicks+1e-8>=_departmentElevatorDuration)
        {_departmentElevatorDing=true;SourceSound("SE_DING_DONG");}
    }
    private bool DrawDepartmentElevatorLayer(Texture2D original,Vector2 camera,int layer, Rid? target = null)
    {
        if(_session.State.World.Player.MapId.Value!=DepartmentElevatorMapId||_departmentElevatorDistance==0)return false;
        // Both source elevators use the same SilphCo window cells and authenticated atlas.
        int phase=DepartmentElevatorWindowPhase;
        for(int y=0;y<_art.Source.Height;y++)for(int x=0;x<_art.Source.Width;x++)
        {
            var destination=new Rect2(new Vector2(x*16,y*16)-camera,new(16,16));
            if(x is >=1 and <=3&&y is >=0 and <=2)
            {
                string symbol="METATILE_SilphCo_ElevatorWindow_"+(y==0?"Top":y==1?"Mid":"Bottom")+phase;
                var tile=_rocketTiles[("silph_co",_rocketTileSymbols[symbol])];
                DrawMapTextureRegion(_rocketTileLayers[tile.Tileset][layer],destination,new(tile.Region.Position,tile.Region.Size), target);
            }
            else DrawMapTextureRegion(original,destination,new(x*16,y*16,16,16), target);
        }
        return true;
    }
    private bool TryDepartmentInput(InputEventKey key)
    {
        if(!DepartmentElevatorMenuActive||key.Keycode is not(Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Backspace))return false;
        _held=null;
        if(!key.Pressed||!DepartmentElevatorMenuVisible||_tracks.Any(t=>t.Time<t.Duration)||_waitElapsed<_waitDuration)return true;
        if(key.Keycode is Key.Up or Key.Down or Key.Enter or Key.Backspace)SourceSound("SE_SELECT");
        if(key.Keycode==Key.Up)_departmentElevatorCursor=(_departmentElevatorCursor+DepartmentElevatorRows.Length-1)%DepartmentElevatorRows.Length;
        else if(key.Keycode==Key.Down)_departmentElevatorCursor=(_departmentElevatorCursor+1)%DepartmentElevatorRows.Length;
        else if(key.Keycode==Key.Enter)Apply(new("ack",Choice:_departmentElevatorCursor));
        else if(key.Keycode==Key.Backspace)Apply(new("ack",Choice:127));
        return true;
    }
    private bool NavigateDepartmentPointer(Vector2 point,int direction)=>DepartmentElevatorMenuVisible&&
        PointerRows(point,direction,ref _departmentElevatorCursor,DepartmentElevatorRows.Length,8,8,DepartmentElevatorMenuWidth,14);
    private bool MouseDepartmentElevator(Vector2 point)=>DepartmentElevatorMenuVisible&&
        MouseSelect(point,ref _departmentElevatorCursor,DepartmentElevatorRows.Length,8,8,DepartmentElevatorMenuWidth,14);
    private void DrawDepartmentElevatorMenu()
    {
        if(_session.State.World.Player.MapId.Value!=DepartmentElevatorMapId)return;
        if(_session.State.DepartmentElevator?.FloorWindowVisible==true)
        {
            _battleSkin.DrawUserFrame(this,new(168,0,72,48));
            SourceText(_sourceNormal,new(176,10),SourceMenuLabelPresentation.Plain(_battleText,"gText_NowOn"));
            int sourceFloor=_session.State.Script.Variables.GetValueOrDefault("VAR_0x8005");
            var floorDefinition=_catalog.DepartmentStoreElevatorFloors.Single(f=>f.SourceFloorValue==sourceFloor);
            string floor=SourceMenuLabelPresentation.Plain(_battleText,"gText_"+(5-floorDefinition.MenuIndex)+"F");
            float width=_sourceNormal.MeasureText(floor).X;
            SourceText(_sourceNormal,new(232-width,24),floor);
        }
        if(!DepartmentElevatorMenuVisible)return;
        int frameHeight=((DepartmentElevatorRows.Length*14+16+7)/8)*8;
        _battleSkin.DrawUserFrame(this,new(0,0,DepartmentElevatorMenuWidth+16,frameHeight));
        for(int row=0;row<DepartmentElevatorRows.Length;row++)SourceText(_sourceNormal,new(16,10+row*14),SourceMenuLabelPresentation.Plain(_battleText,DepartmentElevatorRows[row]));
        SourceText(_sourceNormal,new(8,10+_departmentElevatorCursor*14),"▶");
    }
}
