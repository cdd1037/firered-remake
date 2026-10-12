/// <summary>m4a.c MPlayFadeOut/FadeOutBody with the ferry's speed4: 64→0 by4 every4 source ticks.</summary>
internal sealed class SeagallopSourceMusicFade
{
    internal int Volume {get;private set;}=64;
    private int _counter=4;
    internal bool Advance()
    {
        if(Volume==0)return false;
        if(--_counter!=0)return false;
        _counter=4;Volume-=4;return true;
    }
}
