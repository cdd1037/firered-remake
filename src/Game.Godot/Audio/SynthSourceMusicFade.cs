/// <summary>Shared source-tick music-fade adapter for field transitions.</summary>
public partial class SynthSourceAudioPlayer
{
    private SourceMusicFade? _sourceMusicFade;
    public void BeginSourceMusicFade(int speed=4)
    {
        _fadeRemaining=0;_fadePauseAtEnd=false;
        _sourceMusicFade=_music is null?null:new(speed);
    }
    private void AdvanceSourceMusicFade()
    {
        if(_sourceMusicFade is not {} fade)return;
        if(_music is null){_sourceMusicFade=null;return;}
        if(!fade.Advance())return;
        _musicGain=fade.Volume/64f;ApplyMusicGain();
        if(fade.Volume==0)
        {_synth.Stop(0);_music=null;_musicTemporarilyPaused=false;_sourceMusicFade=null;}
    }
}
