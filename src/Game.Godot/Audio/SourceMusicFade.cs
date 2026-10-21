/// <summary>m4a.c MPlayFadeOut/FadeOutBody: 64→0 by4 at the requested source-tick speed.</summary>
internal sealed class SourceMusicFade
{
    internal int Volume {get;private set;}=64;
    private readonly int _speed;
    private int _counter;
    internal SourceMusicFade(int speed=4)
    {
        if(speed<=0)throw new ArgumentOutOfRangeException(nameof(speed));
        _speed=_counter=speed;
    }
    internal bool Advance()
    {
        if(Volume==0)return false;
        if(--_counter!=0)return false;
        _counter=_speed;Volume-=4;return true;
    }
}
