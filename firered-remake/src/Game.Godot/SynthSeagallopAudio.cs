/// <summary>Small source-tick music-fade adapter for the original Seagallop callback.</summary>
public partial class SynthSourceAudioPlayer
{
    private SeagallopSourceMusicFade? _seagallopMusicFade;
    public void BeginSeagallopMusicFade()
    {
        _fadeRemaining=0;_fadePauseAtEnd=false;
        _seagallopMusicFade=_music is null?null:new();
    }
    private void AdvanceSeagallopMusicFade()
    {
        if(_seagallopMusicFade is not {} fade)return;
        if(_music is null){_seagallopMusicFade=null;return;}
        if(!fade.Advance())return;
        _musicGain=fade.Volume/64f;ApplyMusicGain();
        if(fade.Volume==0)
        {_synth.Stop(0);_music=null;_musicTemporarilyPaused=false;_seagallopMusicFade=null;}
    }
}
