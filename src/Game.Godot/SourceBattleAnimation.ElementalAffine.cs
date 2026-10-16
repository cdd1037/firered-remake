using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public sealed record SourceSpriteAffineCommand(string Op,int X,int Y,int Rotation,int Duration,int Target);

/// <summary>Actual FRAME/JUMP/END data and sprite.c affine timing; no source files at runtime.</summary>
public sealed class SourceSpriteAffineProgram
{
    public static SourceSpriteAffineCommand[][] Read(JsonElement programs)=>programs.EnumerateArray().Select(p=>p.EnumerateArray().Select(c=>new SourceSpriteAffineCommand(c.GetProperty("op").GetString()!,c.GetProperty("x").GetInt32(),c.GetProperty("y").GetInt32(),c.GetProperty("rotation").GetInt32(),c.GetProperty("duration").GetInt32(),c.GetProperty("target").GetInt32())).ToArray()).ToArray();
    private readonly SourceSpriteAffineCommand[] _commands;
    private int _pc,_delay,_x=256,_y=256,_rotation;
    public bool Ended{get;private set;}
    public int XScale=>_x;
    public int YScale=>_y;
    public int Rotation=>_rotation;
    public SourceAffineMatrix Matrix=>_x==0||_y==0?throw new InvalidDataException("Zero-scale source affine requires reviewed division semantics."):SourceAffineMatrix.FromScaleRotation(65536/_x,65536/_y,_rotation);
    public SourceSpriteAffineProgram(SourceSpriteAffineCommand[] commands)
    {
        if(commands.Length is <1 or >64)throw new InvalidDataException("Unbounded affine program.");
        foreach(var c in commands)
        {
            if(c.Op is not ("frame" or "jump" or "end")||c.X is < -32768 or >32767||c.Y is < -32768 or >32767||c.Rotation is <0 or >255||c.Duration is <0 or >255||c.Op=="jump"&&(c.Target<0||c.Target>=commands.Length))throw new InvalidDataException("Invalid source affine command.");
        }
        _commands=commands;ApplyCommand();
    }
    private void ApplyRelative(SourceSpriteAffineCommand c)
    {_x=(short)(_x+c.X);_y=(short)(_y+c.Y);_rotation=(ushort)(_rotation+(c.Rotation<<8));}
    private void ApplyCommand()
    {
        var c=_commands[_pc];
        if(c.Op=="end"){Ended=true;return;}
        if(c.Op=="jump")
        {
            _pc=c.Target;c=_commands[_pc];
            if(c.Op!="frame")throw new InvalidDataException("Affine jump must target a frame.");
        }
        if(c.Duration==0){_x=c.X;_y=c.Y;_rotation=(ushort)(c.Rotation<<8);_delay=0;}
        else{ApplyRelative(c);_delay=c.Duration-1;}
    }
    public void Step()
    {
        if(Ended)return;
        if(_delay>0){_delay--;ApplyRelative(_commands[_pc]);return;}
        if(++_pc>=_commands.Length)throw new InvalidDataException("Affine program fell through.");
        ApplyCommand();
    }
}
