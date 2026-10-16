/// <summary>Source identity is independent of rendered PCM duration. Fixture is never original audio.</summary>
public sealed record SourceAudioMetadata(string Id, string SourceSymbol, int SourcePlayer, int Priority, int? SourceCounter, string Renderer,
    bool IsOriginalTestFixture = false);

public sealed record SourceAudioPlaybackEvent(string Kind, string Id, long Token, int SourcePlayer,
    string Reason, bool IsOriginalTestFixture, SourceAudioMetadata? Source = null);
public sealed record SourceAudioVoiceState(string Id, long Token, int SourcePlayer, int Priority,
    bool Playing, bool Paused, double PositionSeconds, float VolumeLinear);
public sealed record SourceAudioPlaybackState(SourceAudioVoiceState? Music, SourceAudioVoiceState[] Effects,
    SourceAudioVoiceState? Cry, long? FanfareToken, int? FanfareCounterRemaining,
    bool CryDucking, bool Stereo, long Generation);

/// <summary>Source driver timing and gain shared by live synthesis and presentation.</summary>
public static class SourceAudioTiming
{
    public const float CryDuckGain = 85f / 256f;
    public const double SourceFramesPerSecond = 16777216d / 280896d;
}

/// <summary>Presentation-only playback seam. Implementations never mutate campaign state or RNG.</summary>
public interface ISourceAudioBackend
{
    event Action<SourceAudioPlaybackEvent>? PlaybackEvent;
    bool AutomaticSourceClock { get; set; }
    int RegisteredClipCount { get; }
    bool HasClip(string id);
    long PlayMusic(string id, bool restart = false);
    long PlaySoundEffect(string id);
    long PlayFanfare(string id);
    long PlayCry(string id, bool duckBgm = true);
    void CancelFanfare(long? expectedToken = null, bool resumeMusic = true);
    void CancelCry(long? expectedToken = null);
    void StopSoundEffects();
    void FadeOutMusic(double seconds, bool temporarily = false);
    void FadeInMusic(double seconds);
    void ResetPlayback();
    void ShutdownPlayback();
    void SetStereo(bool stereo);
    void AdvanceSourceFrame();
    SourceAudioPlaybackState Snapshot();
}

