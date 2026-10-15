using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.World;
using Godot;
using Direction=Game.Domain.World.Direction;

/// <summary>Local-only source-map integration. Does not claim original story, NPCs or map transitions.</summary>
public partial class PalletTownView:Node2D
{
    private PretWorldMap _map=null!;
    private ReferenceWorldSession _session=null!;
    private PretDialogues _dialogues=null!;
    private Font _font=null!;
    private Texture2D _bottom=null!,_middle=null!,_top=null!,_player=null!;
    private long _sequence;
    private Vector2 _visualPosition,_fromPosition;
    private double _motionTime=1,_repeat;
    private Direction? _held;
    private string _status="真新镇原图 · 门/NPC/主线尚未接入";
    private string _savePath="";
    private string _evidence="";
    public override void _Ready()
    {
        try
        {
            var root=OS.GetEnvironment("FIRERED_REFERENCE_ROOT");
            if(string.IsNullOrWhiteSpace(root))throw new InvalidOperationException("Use scripts/launch-pallet.sh to provide verified local reference data.");
            _map=PretWorldMap.Load(root);
            _font=GD.Load<FontFile>("res://Assets/ProbeFont.ttc");
            Texture2D Load(string path)=>ImageTexture.CreateFromImage(Image.LoadFromFile(path));
            _bottom=Load(_map.BottomTexturePath);_middle=Load(_map.MiddleTexturePath);_top=Load(_map.TopTexturePath);_player=Load(_map.PlayerTexturePath);
            var rawRoot=OS.GetEnvironment("FIRERED_REFERENCE_SOURCE");
            SourceRef Ref(string file,string symbol){var evidence=_map.Metadata.Files.Single(f=>f.Path==file);return new(SourceKind.Pret,file,symbol,_map.Metadata.Repository,_map.Metadata.Commit);}
            const string scripts="data/maps/PalletTown/scripts.inc", texts="data/maps/PalletTown/text.inc";
            _dialogues=PretDialogueImporter.Import(System.IO.File.ReadAllText(System.IO.Path.Combine(rawRoot,scripts)),System.IO.File.ReadAllText(System.IO.Path.Combine(rawRoot,texts)),Ref(scripts,"PalletTown_MapScripts"),Ref(texts,"PalletTown_Text"));
            var spawn=_map.TechnicalSpawn;
            _session=new(_map,new(_map.Id,spawn.X,spawn.Y,spawn.Elevation,Direction.South),PretWorldMap.FullSourceHash,Interaction,_dialogues.Programs);
            _visualPosition=new(spawn.X*16,spawn.Y*16);_fromPosition=_visualPosition;
            _savePath=ProjectSettings.GlobalizePath("user://pallet-reference-save.json");
            _evidence=OS.GetEnvironment("FIRERED_EVIDENCE");
            Write("ready",new{map=_map.Id.Value,_map.Width,_map.Height,sourceHash=PretWorldMap.FullSourceHash,supportedReadOnlyScripts=_dialogues.Programs.Count,unimplementedScripts=_dialogues.UnsupportedScripts.Length,spawn,scope="static source map, basic walking, supported read-only signs; no main story, NPCs, warps or connections"});
            QueueRedraw();
        }
        catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(19);}
    }
    private string? Interaction(TilePosition p)
    {
        var (dx,dy)=p.Facing switch{Direction.North=>(0,-1),Direction.South=>(0,1),Direction.East=>(1,0),_=>(-1,0)};
        foreach(var e in _map.Events.GetProperty("bg_events").EnumerateArray())
            if(e.GetProperty("x").GetInt32()==p.X+dx&&e.GetProperty("y").GetInt32()==p.Y+dy)
            {
                if(e.GetProperty("player_facing_dir").GetString()!="BG_EVENT_PLAYER_FACING_ANY")return "unsupported-facing-rule";
                return e.GetProperty("script").GetString();
            }
        return null;
    }
    public override void _Process(double delta)
    {
        if(_session is null)return;
        _motionTime+=delta;
        var target=new Vector2(_session.State.World.Player.X*16,_session.State.World.Player.Y*16);
        _visualPosition=_fromPosition.Lerp(target,(float)Math.Min(1,_motionTime/.1333333));
        if(_held is {} dir&&_session.State.Dialogue is null)
        {
            _repeat+=delta;
            if(_repeat>=.1333333){_repeat=0;Apply(new("move",dir));}
        }
        QueueRedraw();
    }
    public override void _Notification(int what)
    {if(what==NotificationWMWindowFocusOut){_held=null;_repeat=0;}}
    public override void _UnhandledInput(InputEvent input)
    {
        if(_session is null||input is not InputEventKey key||key.Echo)return;
        Direction? direction=key.Keycode switch{Key.Up=>Direction.North,Key.Down=>Direction.South,Key.Left=>Direction.West,Key.Right=>Direction.East,_=>null};
        if(direction is {} d)
        {
            if(key.Pressed){_held=d;_repeat=0;Apply(new("move",d));}
            else if(_held==d){_held=null;_repeat=0;}
        }
        else if(key.Pressed)
        {
            if(key.Keycode==Key.Enter){_held=null;Apply(new("interact"));}
            if(key.Keycode is Key.F5 or Key.F9)
            {
                _held=null;
                try
                {
                    if(_session.State.Dialogue is not null)throw new InvalidOperationException("对话中不能存读档");
                    var storage=new SaveService(new LocalSaveStorage());
                    if(key.Keycode==Key.F5){storage.Save(_savePath,_session.State,_sequence,ReferenceWorldSession.Rules,PretWorldMap.FullSourceHash,_session.ValidateSave);_status="存档成功";Write("saved",new{hash=CanonicalJson.Hash(_session.State)});}
                    else
                    {
                        var loaded=storage.Load<ReferenceWorldState>(_savePath,ReferenceWorldSession.Rules,PretWorldMap.FullSourceHash,_session.ValidateSave);
                        _session=new(_map,loaded.State.World.Player,PretWorldMap.FullSourceHash,Interaction,_dialogues.Programs,loaded.State);
                        _visualPosition=new(loaded.State.World.Player.X*16,loaded.State.World.Player.Y*16);_fromPosition=_visualPosition;_motionTime=1;
                        _status=loaded.Source.EndsWith(".bak",StringComparison.Ordinal)?"已从备份恢复":"读档成功";Write("loaded",new{hash=CanonicalJson.Hash(_session.State),loaded.Source});
                    }
                }
                catch(Exception ex){_status=ex.Message;Write("saveLoadRejected",new{error=ex.Message});}
            }
            if(key.Keycode==Key.F12&&_evidence.Length>0)GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.ChangeExtension(_evidence,"png"));
            if(key.Keycode==Key.Escape)GetTree().Quit();
        }
        QueueRedraw();GetViewport().SetInputAsHandled();
    }
    private void Apply(ReferenceWorldCommand command)
    {
        var before=_session.State.World.Player;
        var result=_session.Execute("pallet:"+(_sequence++),_session.Revision,command);
        var after=result.State.World.Player;
        if(after.X!=before.X||after.Y!=before.Y){_fromPosition=new(before.X*16,before.Y*16);_motionTime=0;}
        _status=result.Error switch{"unsupported-tile-behavior"=>"该地形/传送尚未接入",null=>"方向键移动 Enter调查 F5存 F9读",_=>result.Error.StartsWith("unsupported_script:",StringComparison.Ordinal)?"该剧情事件尚未接入":result.Error};
        Write("command",new{command,status=result.Status.ToString(),result.Error,result.Revision,result.StateHash,state=result.State});
    }
    public override void _Draw()
    {
        if(_session is null)return;
        var camera=new Vector2(Math.Clamp(_visualPosition.X+8-120,0,_map.Width*16-240),Math.Clamp(_visualPosition.Y+8-80,0,_map.Height*16-160));camera=camera.Floor();
        DrawTexture(_bottom,-camera);DrawTexture(_middle,-camera);
        int idle=_session.State.World.Player.Facing switch{Direction.South=>0,Direction.North=>1,_=>2};
        int frame=idle;
        if(_motionTime<.1333333)
        {
            var walk=_session.State.World.Player.Facing switch{Direction.South=>new[]{3,0,4,0},Direction.North=>new[]{5,1,6,1},_=>new[]{7,2,8,2}};
            frame=walk[(int)((Math.Max(1,_session.State.World.StepCount)-1)%4)];
        }
        var spritePosition=_visualPosition.Floor()-camera+new Vector2(0,-16);
        if(_session.State.World.Player.Facing==Direction.East)
        {
            DrawSetTransform(spritePosition+new Vector2(16,0),0,new Vector2(-1,1));
            DrawTextureRectRegion(_player,new(0,0,16,32),new(frame*16,0,16,32));
            DrawSetTransform(Vector2.Zero);
        }
        else DrawTextureRectRegion(_player,new(spritePosition,new Vector2(16,32)),new(frame*16,0,16,32));
        DrawTexture(_top,-camera);
        DrawRect(new(0,0,240,12),new Color(0.04f,0.08f,0.10f,.92f));DrawString(_font,new(3,9),"本地源地图接入验证 · 非完整剧情",fontSize:8,modulate:new Color("f4e6ad"));
        if(_session.VisibleText is {} text)
        {
            DrawRect(new(3,112,234,45),new Color("253842"));DrawRect(new(5,114,230,41),new Color("fff8dc"));
            var lines=text.Split('\n');for(var i=0;i<lines.Length;i++)DrawString(_font,new(10,126+i*11),lines[i],fontSize:9,modulate:new Color("203640"));
            DrawString(_font,new(218,152),"▼",fontSize:8,modulate:new Color("203640"));
        }
        else{DrawRect(new(0,148,240,12),new Color(0.04f,0.08f,0.1f,.92f));DrawString(_font,new(3,157),_status,fontSize:8,modulate:Colors.White);}
    }
    private void Write(string kind,object data)
    {
        var line=JsonSerializer.Serialize(new{kind,data});GD.Print("PALLET "+line);
        if(_evidence.Length>0)System.IO.File.AppendAllText(System.IO.Path.ChangeExtension(_evidence,"jsonl"),line+"\n");
    }
}
