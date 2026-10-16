using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Rules;
using Game.Domain.World;
using Godot;
using GridMap = Game.Domain.World.GridMap;
using Direction = Game.Domain.World.Direction;

/// <summary>Original test arena. This is an integration harness, not a FireRed gameplay milestone.</summary>
public partial class EngineeringHarness : Node2D
{
    private Font _font = null!;
    private EngineeringSession _session = null!;
    private GridMap _map = null!;
    private BattleSnapshot _battle = null!;
    private readonly HarnessBattleContent _content = new();
    private long _sequence;
    private string _status = "方向键移动 B对战 Enter奖励 F5存 F9读";
    private string _path = "";
    public override void _Ready()
    {
        _font = GD.Load<FontFile>("res://Assets/ProbeFont.ttc");
        _map = new(new("fixture:lab"), 12, 8, Enumerable.Range(0, 96).Select(i => new TileCell(i%12==0||i%12==11||i/12==0||i/12==7)).ToImmutableArray());
        CombatantSnapshot actor(string id, BattleSide side, int attack) => new(new(id),new("fixture:creature"),side,5,new(22,attack,10,10,10,side==BattleSide.Player?12:9),ElementType.Normal,null,22,[new(new("fixture:strike"),35,35)]);
        _battle = BattleSnapshot.Create(new("fixture:battle"),EngineeringSession.Content,actor("fixture:player",BattleSide.Player,13),actor("fixture:opponent",BattleSide.Opponent,10),12345);
        _session = new(_map,_content,_battle);
        _path = ProjectSettings.GlobalizePath("user://engineering-save.json");
        GD.Print("HARNESS_READY original-fixture-v1");
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(0,0,240,160),new Color("122835"));
        DrawString(_font,new(8,14),"工程沙盒 · 非原版地图",fontSize:10,modulate:new Color("f5d986"));
        if(_session.State.Battle is {} battle)
        {
            DrawRect(new(14,24,212,89),new Color("e3eac8"));
            var foe=battle.Battlers.Single(b=>b.Side==BattleSide.Opponent);var player=battle.Battlers.Single(b=>b.Side==BattleSide.Player);
            DrawRect(new(163,44,20,22),new Color("bb7583"));DrawRect(new(48,72,24,24),new Color("559ba4"));
            DrawString(_font,new(25,40),$"对手 HP {foe.Hp}/{foe.Stats.MaxHp}",fontSize:10,modulate:new Color("273c3b"));
            DrawString(_font,new(105,98),$"我方 {player.Hp}/{player.Stats.MaxHp}",fontSize:10,modulate:new Color("273c3b"));
            DrawString(_font,new(20,126),"Enter：使用普通攻击",fontSize:10);
        }
        else
        {
            for(var y=0;y<_map.Height;y++)for(var x=0;x<_map.Width;x++)
            {
                var blocked=_map.GetTile(x,y)!.Blocked;
                DrawRect(new(24+x*16,23+y*12,16,12),new Color(blocked?"42725c":((x+y)%2==0?"a7bf83":"9eb87c")));
                if(blocked)DrawRect(new(28+x*16,25+y*12,8,7),new Color("2b5947"));
            }
            var p=_session.State.World.Player;DrawRect(new(29+p.X*16,24+p.Y*12,7,10),new Color("d95751"));
            DrawRect(new(28+p.X*16,24+p.Y*12,9,3),new Color("fff0c5"));
            DrawString(_font,new(9,129),$"测试币 {_session.State.Money}  步数 {_session.State.World.StepCount}",fontSize:9);
        }
        DrawRect(new(0,134,240,26),new Color("122835"));
        DrawString(_font,new(5,144),_session.State.Message,fontSize:8);
        DrawString(_font,new(5,156),_status,fontSize:8);
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(input is not InputEventKey{Pressed:true,Echo:false} key)return;
        EngineeringCommand? command=key.Keycode switch
        {
            Key.Up=>new("move",Direction.North),Key.Down=>new("move",Direction.South),Key.Left=>new("move",Direction.West),Key.Right=>new("move",Direction.East),
            Key.B=>new("battle"),Key.Enter=>new(_session.State.Battle is null?"reward":"attack"),_=>null
        };
        try
        {
            if(command is not null)
            {
                var result=_session.Execute("ui:"+(_sequence++),_session.Revision,command);
                _status=result.Error??(result.Status==CommandStatus.Accepted?"B对战 Enter攻击/奖励 F5存 F9读":"拒绝");
                WriteEvidence("command",new{command,status=result.Status.ToString(),result.Error,result.Revision,StateHash=CanonicalJson.Hash(result.State),state=result.State});
            }
            if(key.Keycode is Key.F5 or Key.F9 && _session.State.Battle is not null)
            {
                _status="战斗中不能存读档";WriteEvidence("saveLoadRejected",new{reason="battle_active",key=key.Keycode.ToString()});
                QueueRedraw();GetViewport().SetInputAsHandled();return;
            }
            if(key.Keycode==Key.F5)
            {
                new SaveService(new LocalSaveStorage()).Save(_path,_session.State,_sequence,EngineeringSession.Rules,EngineeringSession.Content,s => EngineeringSession.ValidatePlayerSave(s,_map,_content));
                _status="存档成功";WriteEvidence("saved",new{hash=CanonicalJson.Hash(_session.State)});
            }
            if(key.Keycode==Key.F9)
            {
                var loaded=new SaveService(new LocalSaveStorage()).Load<EngineeringState>(_path,EngineeringSession.Rules,EngineeringSession.Content,s => EngineeringSession.ValidatePlayerSave(s,_map,_content));
                _session=new(_map,_content,_battle,loaded.State);_status=loaded.Source.EndsWith(".bak",StringComparison.Ordinal)?"已从备份恢复":"读档成功";
                WriteEvidence("loaded",new{hash=CanonicalJson.Hash(_session.State),loaded.Source});
            }
            if(key.Keycode==Key.F12)
            {
                var root=OS.GetEnvironment("FIRERED_EVIDENCE");if(root.Length>0)GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.ChangeExtension(root,"harness.png"));
            }
        }
        catch(Exception ex){_status="错误: "+ex.Message;WriteEvidence("fault",new{error=ex.ToString()});}
        if(key.Keycode==Key.Escape)GetTree().Quit();
        QueueRedraw();GetViewport().SetInputAsHandled();
    }
    private static void WriteEvidence(string kind,object data)
    {
        var root=OS.GetEnvironment("FIRERED_EVIDENCE");var line=System.Text.Json.JsonSerializer.Serialize(new{kind,data});
        GD.Print("HARNESS "+line);
        if(root.Length>0)System.IO.File.AppendAllText(System.IO.Path.ChangeExtension(root,"harness.jsonl"),line+"\n");
    }
    private sealed class HarnessBattleContent:IBattleContent {public MoveDefinition? FindMove(MoveId id)=>id.Value=="fixture:strike"?new(id,ElementType.Normal,40,100,35):null;}
}
public sealed class LocalSaveStorage : ISaveStorage
{
    public const int MaximumSaveBytes = 16 * 1024 * 1024;
    public byte[]? Read(string slot)
    {
        System.IO.FileStream stream;
        try{stream=new(slot,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.Read);}
        catch(System.IO.FileNotFoundException){return null;}
        catch(System.IO.DirectoryNotFoundException){return null;}
        // File.Exists also returns false for access/IO failures. Preserve those
        // errors so callers can distinguish unreadable files and try a valid backup.
        using(stream)
        {
            long length=stream.Length;
            if(length>MaximumSaveBytes)throw new System.IO.InvalidDataException("save_file_too_large");
            byte[] bytes=new byte[(int)length];
            stream.ReadExactly(bytes);
            if(stream.Length!=length||stream.ReadByte()!=-1)throw new System.IO.IOException("save_file_changed_during_read");
            return bytes;
        }
    }
    public void WriteStaged(string slot,ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length>MaximumSaveBytes)throw new System.IO.InvalidDataException("save_file_too_large");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(slot)!);
        using var stream=new System.IO.FileStream(slot+".tmp",System.IO.FileMode.Create,System.IO.FileAccess.Write,System.IO.FileShare.None);
        stream.Write(bytes);stream.Flush(true);
    }
    public void Commit(string slot,bool preservePrimary)
    {
        if(preservePrimary)System.IO.File.Copy(slot,slot+".bak",true);
        System.IO.File.Move(slot+".tmp",slot,true);
        // Same-directory rename plus flush is tested here; directory fsync/power-loss guarantees remain platform work.
    }
}
