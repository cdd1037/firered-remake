using System.Text;
namespace Game.Presentation.BattleAnimations;

/// <summary>Presentation-only stream using the source Random() arithmetic.
/// It is intentionally independent of the original shared gameplay/VBlank RNG.</summary>
public sealed class SourceVisualRandom(uint seed)
{
    private uint _state=seed;
    public ushort Next()
    {
        _state=unchecked(_state*1103515245u+24691u);
        return (ushort)(_state>>16);
    }
}

/// <summary>Stable FNV-1a identity mixing. Strings are UTF-8 with little-endian
/// uint byte lengths; integers are fixed-width little-endian. Nullable values
/// have a one-byte presence tag. No process hash, clock or gameplay RNG is read.</summary>
public static class SourceAnimationVisualSeed
{
    public static uint For(CommittedMoveAnimation cue)
    {
        uint hash=2166136261;
        void Byte(byte value)=>hash=unchecked((hash^value)*16777619);
        void Word(uint value){for(int i=0;i<4;i++)Byte((byte)(value>>(8*i)));}
        void Text(string value){var bytes=Encoding.UTF8.GetBytes(value);Word((uint)bytes.Length);foreach(byte b in bytes)Byte(b);}
        Text("firered-source-animation-v1");Text(cue.BattleId);
        ulong sequence=unchecked((ulong)cue.Sequence);Word((uint)sequence);Word((uint)(sequence>>32));
        Word(unchecked((uint)cue.MoveId));Word(unchecked((uint)cue.Actor.Value));Word(unchecked((uint)cue.Target.Value));
        Byte(cue.MoveTurn.HasValue?(byte)1:(byte)0);if(cue.MoveTurn is {} turn)Byte(turn);
        Byte(cue.GeneralAnimationLabel is null?(byte)0:(byte)1);if(cue.GeneralAnimationLabel is {} label)Text(label);
        Byte(cue.StatAnimationArgument.HasValue?(byte)1:(byte)0);if(cue.StatAnimationArgument is {} argument)Word(unchecked((uint)argument));
        return hash;
    }
}

public sealed partial class SourceBattleAnimation
{
    private SourceVisualRandom? _visualRandom;
    private void RequireVisualSeed(SourceAnimationCommand command)
    {
        if(_validatingInvokedProgram&&_cue.VisualSeed is null)Fail(command,"Source visual randomness requires an explicit presentation seed.");
    }
    private ushort NextVisualRandom()
    {
        if(_cue.VisualSeed is not {} seed)throw new InvalidOperationException("Visual RNG callback lacks its validated presentation seed.");
        return (_visualRandom??=new SourceVisualRandom(seed)).Next();
    }
}
