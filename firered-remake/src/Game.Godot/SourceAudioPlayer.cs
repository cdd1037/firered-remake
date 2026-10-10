using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;

/// <summary>Source identity is independent of rendered PCM duration. Fixture is never original audio.</summary>
public sealed record SourceAudioMetadata(string Id, string SourceCommit, string SourceSymbol,
    string SourcePath, int SourcePlayer, int Priority, int? SourceCounter, string Renderer,
    bool IsOriginalTestFixture = false);

/// <summary>Immutable authenticated PCM16 or lossy Vorbis/QOA; encoded and original PCM identities remain separate.</summary>
public sealed class VerifiedSourceAudioClip
{
    private readonly byte[] _pcm;
    private readonly byte[]? _ogg;
    private readonly byte[]? _qoa;
    public bool IsQoa => _qoa is not null;
    public bool IsVorbis => _ogg is not null;
    public string? EncodedSha256 { get; }
    public SourceAudioMetadata Metadata { get; }
    public int SampleRate { get; }
    public int Channels { get; }
    public int FrameCount { get; }
    public int? LoopStartFrame { get; }
    public int? LoopEndFrame { get; }
    public double DurationSeconds => (double)FrameCount / SampleRate;
    public string? PcmSha256 { get; }

    private VerifiedSourceAudioClip(SourceAudioMetadata metadata, byte[] pcm, int rate, int channels,
        string sha256, int? loopStart, int? loopEnd)
    {
        if (string.IsNullOrWhiteSpace(metadata.Id) || string.IsNullOrWhiteSpace(metadata.SourceCommit)
            || string.IsNullOrWhiteSpace(metadata.SourceSymbol) || string.IsNullOrWhiteSpace(metadata.SourcePath)
            || string.IsNullOrWhiteSpace(metadata.Renderer)) throw new InvalidDataException("Audio source identity is incomplete.");
        if (metadata.SourcePlayer is < 0 or > 4 || metadata.Priority is < 0 or > 255 || metadata.SourceCounter < 0)
            throw new InvalidDataException("Invalid source player, priority or counter.");
        if (rate is < 8000 or > 192000 || channels is < 1 or > 2 || pcm.Length == 0 || pcm.Length % (channels * 2) != 0)
            throw new InvalidDataException("Expected nonempty aligned mono/stereo PCM16 at 8–192 kHz.");
        if (sha256.Length != 64 || Convert.ToHexStringLower(SHA256.HashData(pcm)) != sha256)
            throw new InvalidDataException("Audio PCM digest mismatch.");
        int frames = pcm.Length / (channels * 2);
        if (loopStart.HasValue != loopEnd.HasValue || loopStart < 0 || loopEnd > frames || loopStart >= loopEnd)
            throw new InvalidDataException("Invalid exclusive sample-frame loop range.");
        Metadata = metadata; _pcm = pcm.ToArray(); SampleRate = rate; Channels = channels; FrameCount = frames;
        PcmSha256 = sha256; LoopStartFrame = loopStart; LoopEndFrame = loopEnd;
    }

    public static VerifiedSourceAudioClip FromPcm16(SourceAudioMetadata metadata, byte[] pcm,
        int sampleRate, int channels, string expectedPcmSha256, int? loopStartFrame = null, int? loopEndFrame = null)
        => new(metadata, pcm, sampleRate, channels, expectedPcmSha256, loopStartFrame, loopEndFrame);

    /// <summary>Strict RIFF/WAVE PCM16 reader. Loops come from the authenticated manifest, never guessed.</summary>
    public static VerifiedSourceAudioClip FromWav(SourceAudioMetadata metadata, byte[] wav,
        string expectedWavSha256, string expectedPcmSha256, int? loopStartFrame = null, int? loopEndFrame = null)
    {
        if (expectedWavSha256.Length != 64 || Convert.ToHexStringLower(SHA256.HashData(wav)) != expectedWavSha256)
            throw new InvalidDataException("Audio WAV digest mismatch.");
        var bytes = wav.AsSpan();
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != bytes.Length - 8)
            throw new InvalidDataException("Invalid complete RIFF/WAVE container.");
        int rate = 0, channels = 0; byte[]? pcm = null; bool formatSeen = false;
        for (int offset = 12; offset < bytes.Length;)
        {
            if (bytes.Length - offset < 8) throw new InvalidDataException("Truncated WAV chunk.");
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
            if (size > int.MaxValue || (long)offset + 8 + size + (size & 1) > bytes.Length)
                throw new InvalidDataException("Invalid WAV chunk length.");
            var chunk = bytes.Slice(offset + 8, (int)size);
            if (bytes.Slice(offset, 4).SequenceEqual("fmt "u8))
            {
                if (formatSeen || size < 16 || BinaryPrimitives.ReadUInt16LittleEndian(chunk) != 1
                    || BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]) != 16)
                    throw new InvalidDataException("Only one PCM16 WAV format is supported.");
                formatSeen = true; channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
                if (sampleRate > int.MaxValue) throw new InvalidDataException("Invalid WAV sample rate.");
                rate = (int)sampleRate;
                if (channels is < 1 or > 2 || rate is < 8000 or > 192000
                    || BinaryPrimitives.ReadUInt16LittleEndian(chunk[12..]) != channels * 2
                    || BinaryPrimitives.ReadUInt32LittleEndian(chunk[8..]) != rate * channels * 2)
                    throw new InvalidDataException("Inconsistent WAV frame layout.");
            }
            else if (bytes.Slice(offset, 4).SequenceEqual("data"u8))
            {
                if (pcm is not null) throw new InvalidDataException("Duplicate WAV data.");
                pcm = chunk.ToArray();
            }
            offset += checked(8 + (int)size + (int)(size & 1));
        }
        if (!formatSeen || pcm is null) throw new InvalidDataException("Missing WAV format or PCM.");
        return new(metadata, pcm, rate, channels, expectedPcmSha256, loopStartFrame, loopEndFrame);
    }

    private VerifiedSourceAudioClip(SourceAudioMetadata metadata, byte[] ogg, string encodedSha256,
        int rate, int channels, int frames, int? loopStart, int? loopEnd)
    {
        if (string.IsNullOrWhiteSpace(metadata.Id) || string.IsNullOrWhiteSpace(metadata.SourceCommit)
            || string.IsNullOrWhiteSpace(metadata.SourceSymbol) || string.IsNullOrWhiteSpace(metadata.SourcePath)
            || string.IsNullOrWhiteSpace(metadata.Renderer) || metadata.SourcePlayer is < 0 or > 4
            || metadata.Priority is < 0 or > 255 || metadata.SourceCounter < 0)
            throw new InvalidDataException("Invalid Vorbis source identity.");
        if (rate is < 8000 or > 192000 || channels is < 1 or > 2 || frames <= 0
            || loopStart.HasValue != loopEnd.HasValue || loopStart < 0 || loopStart >= loopEnd
            || (loopEnd.HasValue && loopEnd.Value != frames))
            throw new InvalidDataException("Vorbis loops require an exact exclusive file end and valid frame bounds.");
        if (encodedSha256.Length != 64 || Convert.ToHexStringLower(SHA256.HashData(ogg)) != encodedSha256)
            throw new InvalidDataException("Audio Vorbis encoded digest mismatch.");
        var b = ogg.AsSpan();
        if (b.Length < 58 || !b[..4].SequenceEqual("OggS"u8) || b[4] != 0 || b[5] != 2
            || b[26] != 1 || b[27] != 30 || !b.Slice(28, 7).SequenceEqual("\x01vorbis"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(b[35..]) != 0 || b[39] != channels
            || BinaryPrimitives.ReadUInt32LittleEndian(b[40..]) != rate)
            throw new InvalidDataException("Unsupported or mismatched Vorbis identification.");
        Metadata = metadata; _pcm = []; _ogg = ogg.ToArray(); EncodedSha256 = encodedSha256;
        SampleRate = rate; Channels = channels; FrameCount = frames; LoopStartFrame = loopStart; LoopEndFrame = loopEnd;
        using var verified = CreateOggStream();
    }

    /// <summary>Expected encoded digest and logical frames must come from the independently frozen delivery catalog.</summary>
    public static VerifiedSourceAudioClip FromOgg(SourceAudioMetadata metadata, byte[] ogg,
        string expectedEncodedSha256, int sampleRate, int channels, int logicalFrameCount,
        int? loopStartFrame = null, int? loopEndFrame = null)
        => new(metadata, ogg, expectedEncodedSha256, sampleRate, channels, logicalFrameCount, loopStartFrame, loopEndFrame);

    /// <summary>Pure QOA Data bytes, not a .res container. Expected identity and timing come from a frozen catalog.</summary>
    public static VerifiedSourceAudioClip FromQoa(SourceAudioMetadata metadata, byte[] qoa,
        string expectedEncodedSha256, int sampleRate, int channels, int logicalFrameCount,
        int? loopStartFrame = null, int? loopEndFrame = null)
        => new(metadata, qoa, expectedEncodedSha256, sampleRate, channels, logicalFrameCount,
            loopStartFrame, loopEndFrame, qoaData: true);

    private VerifiedSourceAudioClip(SourceAudioMetadata metadata, byte[] qoa, string hash,
        int rate, int channels, int frames, int? begin, int? end, bool qoaData)
    {
        ArgumentNullException.ThrowIfNull(metadata); ArgumentNullException.ThrowIfNull(qoa);
        if (!qoaData || string.IsNullOrWhiteSpace(metadata.Id) || string.IsNullOrWhiteSpace(metadata.SourceCommit)
            || string.IsNullOrWhiteSpace(metadata.SourceSymbol) || string.IsNullOrWhiteSpace(metadata.SourcePath)
            || string.IsNullOrWhiteSpace(metadata.Renderer) || metadata.SourcePlayer is < 0 or > 4
            || metadata.Priority is < 0 or > 255 || metadata.SourceCounter < 0)
            throw new InvalidDataException("Invalid QOA source identity.");
        if (rate is < 8000 or > 192000 || channels is < 1 or > 2 || frames <= 0 || frames > rate * 600
            || qoa.Length is < 32 or > 16 * 1024 * 1024 || begin.HasValue != end.HasValue
            || begin < 0 || begin >= end || end > frames)
            throw new InvalidDataException("Invalid QOA limits/loop bounds.");
        if (hash is not { Length: 64 } || Convert.ToHexStringLower(SHA256.HashData(qoa)) != hash)
            throw new InvalidDataException("QOA encoded digest mismatch.");
        var b = qoa.AsSpan();
        if (!b[..4].SequenceEqual("qoaf"u8) || BinaryPrimitives.ReadUInt32BigEndian(b[4..]) != frames)
            throw new InvalidDataException("Invalid QOA file header.");
        int offset = 8, consumed = 0;
        while (offset < b.Length)
        {
            if (b.Length - offset < 8) throw new InvalidDataException("Truncated QOA frame header.");
            var header = b.Slice(offset, 8);
            int frameRate = header[1] << 16 | header[2] << 8 | header[3];
            int samples = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
            int size = BinaryPrimitives.ReadUInt16BigEndian(header[6..]);
            if (header[0] != channels || frameRate != rate || samples <= 0 || samples != Math.Min(5120, frames - consumed)
                || size != 8 + 16 * channels + 8 * channels * ((samples + 19) / 20) || b.Length - offset < size)
                throw new InvalidDataException("Invalid QOA frame layout or size.");
            offset += size; consumed += samples;
        }
        if (offset != b.Length || consumed != frames) throw new InvalidDataException("QOA sample count mismatch.");
        Metadata = metadata; _pcm = []; _qoa = qoa.ToArray(); EncodedSha256 = hash;
        SampleRate = rate; Channels = channels; FrameCount = frames; LoopStartFrame = begin; LoopEndFrame = end;
        using var verified = CreateQoaStream();
    }

    private AudioStreamWav CreateQoaStream()
    {
        var stream = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Qoa, Data = _qoa!,
            MixRate = SampleRate, Stereo = Channels == 2,
            LoopMode = LoopStartFrame.HasValue ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = LoopStartFrame ?? 0, LoopEnd = LoopEndFrame ?? 0 };
        if (Math.Abs(stream.GetLength() - DurationSeconds) > .5 / SampleRate)
        { stream.Dispose(); throw new InvalidDataException("Godot QOA length differs from authenticated logical frames."); }
        return stream;
    }

    /// <summary>Vorbis is a delivery codec, not lossless PCM. LoopOffset remains a double, without float/millisecond rounding.</summary>
    private AudioStreamOggVorbis CreateOggStream()
    {
        var stream = AudioStreamOggVorbis.LoadFromBuffer(_ogg!);
        if (stream is null) throw new InvalidDataException("Godot could not decode authenticated Vorbis.");
        if (Math.Abs(stream.GetLength() - DurationSeconds) > .5 / SampleRate)
        { stream.Dispose(); throw new InvalidDataException("Godot Vorbis length differs from authenticated logical frames."); }
        stream.Loop = LoopStartFrame.HasValue;
        double offset = LoopStartFrame.HasValue ? (double)LoopStartFrame.Value / SampleRate : 0d;
        // Godot truncates time * sampleRate to an integer on seek. The nearest double can lie
        // one ULP below the intended frame (e.g. 781143 / 48000), adding a frame every loop.
        // Correct only that representation error; keep the authenticated integer loop unchanged.
        if (LoopStartFrame.HasValue && (long)(offset * SampleRate) < LoopStartFrame.Value)
            offset = Math.BitIncrement(offset);
        stream.LoopOffset = offset;
        return stream;
    }

    public AudioStream CreatePlaybackStream(bool stereo = true) => IsVorbis ? CreateOggStream() : IsQoa ? CreateQoaStream() : CreateStream(stereo);

    public AudioStreamWav CreateStream(bool stereo = true)
    {
        if (IsVorbis || IsQoa) throw new InvalidOperationException("Use CreatePlaybackStream for encoded audio; it is not PCM.");
        byte[] data = _pcm;
        if (!stereo && Channels == 2)
        {
            data = new byte[FrameCount * 2];
            for (int frame = 0; frame < FrameCount; frame++)
            {
                int left = BinaryPrimitives.ReadInt16LittleEndian(_pcm.AsSpan(frame * 4));
                int right = BinaryPrimitives.ReadInt16LittleEndian(_pcm.AsSpan(frame * 4 + 2));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(frame * 2), (short)((left + right) / 2));
            }
        }
        if (LoopStartFrame.HasValue)
        {
            // Godot 4.7.2's WAV decoder can decode the exclusive end frame when a mix block
            // crosses the loop boundary. Supply the next logical frame, not out-of-range data.
            // Its wrap then advances to loopBegin+1; the period remains end-begin samples.
            int frameBytes = stereo && Channels == 2 ? 4 : 2;
            var guarded = new byte[Math.Max(data.Length, (LoopEndFrame!.Value + 1) * frameBytes)];
            data.CopyTo(guarded, 0);
            data.AsSpan(LoopStartFrame.Value * frameBytes, frameBytes)
                .CopyTo(guarded.AsSpan(LoopEndFrame.Value * frameBytes, frameBytes));
            data = guarded;
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, Data = data,
            MixRate = SampleRate, Stereo = stereo && Channels == 2,
            LoopMode = LoopStartFrame.HasValue ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = LoopStartFrame ?? 0, LoopEnd = LoopEndFrame ?? 0 };
    }
}

public sealed record SourceAudioPlaybackEvent(string Kind, string Id, long Token, int SourcePlayer,
    string Reason, bool IsOriginalTestFixture, SourceAudioMetadata? Source = null);
public sealed record SourceAudioVoiceState(string Id, long Token, int SourcePlayer, int Priority,
    bool Playing, bool Paused, double PositionSeconds, float VolumeLinear);
public sealed record SourceAudioPlaybackState(SourceAudioVoiceState? Music, SourceAudioVoiceState[] Effects,
    SourceAudioVoiceState? Cry, long? FanfareToken, int? FanfareCounterRemaining,
    bool CryDucking, bool Stereo, long Generation);

/// <summary>
/// Authenticated PCM/Vorbis/QOA playback adapter, not an m4a synthesizer. Player0=BGM, 1/2=priority SE, 3=unprotected SE, 4=cry.
/// Godot's actual stream owns completion and looping; source task counters own fanfare resume timing.
/// All methods run on the scene thread. ResetPlayback is mandatory on debug save-load/session replacement.
/// </summary>
/// <summary>Presentation-only playback seam. Implementations never mutate campaign state or RNG.</summary>
public interface ISourceAudioBackend
{
    event Action<SourceAudioPlaybackEvent>? PlaybackEvent;
    bool AutomaticSourceClock { get; set; }
    int RegisteredClipCount { get; }
    bool HasClip(string id);
    void Register(VerifiedSourceAudioClip clip);
    long PlayMusic(string id, bool restart = false);
    long PlaySoundEffect(string id);
    long PlayFanfare(string id);
    long PlayCry(string id);
    void CancelFanfare(long? expectedToken = null, bool resumeMusic = true);
    void CancelCry(long? expectedToken = null);
    void StopSoundEffects();
    void FadeOutMusic(double seconds, bool temporarily = false);
    void FadeInMusic(double seconds);
    void ResetPlayback();
    void SetStereo(bool stereo);
    void AdvanceSourceFrame();
    SourceAudioPlaybackState Snapshot();
}

public partial class SourceAudioPlayer : Node, ISourceAudioBackend
{
    public const float CryDuckGain = 85f / 256f;
    public const double SourceFramesPerSecond = 16777216d / 280896d;
    private readonly Dictionary<string, VerifiedSourceAudioClip> _clips = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Voice> _effects = [];
    private Voice? _music, _cry;
    private VerifiedSourceAudioClip? _fanfareClip;
    private long _nextToken, _generation, _fanfareToken, _fanfareMusicToken;
    private int _fanfareCounter, _cryGrace;
    private bool _cryDucking, _stereo = true;
    private double _sourceRemainder, _fadeRemaining, _fadeDuration;
    private float _musicGain = 1, _fadeStartGain, _fadeTargetGain;
    private bool _fadePauseAtEnd, _musicTemporarilyPaused;
    public bool AutomaticSourceClock { get; set; } = true;
    public event Action<SourceAudioPlaybackEvent>? PlaybackEvent;
    public int RegisteredClipCount => _clips.Count;
    public bool HasClip(string id) => _clips.ContainsKey(id);

    public void Register(VerifiedSourceAudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!_clips.TryAdd(clip.Metadata.Id, clip)) throw new InvalidOperationException("Audio ID already registered: " + clip.Metadata.Id);
    }

    /// <returns>Existing token for same-ID keep-position; a fresh token for a start/restart; 0 for missing input.</returns>
    public long PlayMusic(string id, bool restart = false)
    {
        if (!TryClip(id, 0, out var clip)) return 0;
        if (!restart && _music?.Clip.Metadata.Id == id && _music.Player.HasStreamPlayback()) return _music.Token;
        // An explicit new background must never be changed by an old fanfare/cry callback.
        CancelFanfare(resumeMusic: false); CancelCry();
        Retire(_music); _musicGain = 1; _fadeRemaining = 0; _fadePauseAtEnd = _musicTemporarilyPaused = false;
        _music = Start(clip!, "music"); ApplyMusicGain();
        return _music.Token;
    }

    /// <summary>SE1/SE2 protect strictly higher priority; equal priority restarts. SE3 does not protect priority.</summary>
    public long PlaySoundEffect(string id)
    {
        if (!TryClip(id, null, out var clip)) return 0;
        if (clip!.Metadata.SourcePlayer is < 1 or > 3) throw new InvalidOperationException("Not an SE player: " + id);
        return StartEffect(clip, "effect");
    }

    public long PlayFanfare(string id)
    {
        if (!TryClip(id, 2, out var clip)) return 0;
        if (clip!.Metadata.SourceCounter is null || clip.LoopStartFrame is not null)
            throw new InvalidOperationException("Fanfare requires an independent source counter and finite audio.");
        // Source sound.c starts the task and pauses BGM even if SE2 priority rejects its song.
        long token = StartEffect(clip, "fanfare");
        if (token == 0) token = ++_nextToken;
        _fanfareToken = token; _fanfareCounter = clip.Metadata.SourceCounter.Value;
        _fanfareClip = clip;
        _fanfareMusicToken = _music?.Token ?? 0;
        if (_music is not null) _music.Player.StreamPaused = true;
        return token;
    }

    public long PlayCry(string id)
    {
        if (!TryClip(id, 4, out var clip)) return 0;
        if (clip!.LoopStartFrame is not null) throw new InvalidOperationException("Normal cry must be finite.");
        Retire(_cry); _cry = Start(clip, "cry"); _cryDucking = true; _cryGrace = 2; ApplyMusicGain();
        return _cry.Token;
    }

    public void CancelFanfare(long? expectedToken = null, bool resumeMusic = true)
    {
        if (_fanfareToken == 0 || (expectedToken.HasValue && expectedToken != _fanfareToken)) return;
        if (_effects.TryGetValue(2, out var voice) && voice.Token == _fanfareToken)
        { _effects.Remove(2); Retire(voice); }
        FinishFanfare(resumeMusic, "cancelled");
    }

    public void CancelCry(long? expectedToken = null)
    {
        if (expectedToken.HasValue && _cry?.Token != expectedToken) return;
        Retire(_cry); _cry = null; _cryDucking = false; _cryGrace = 0; ApplyMusicGain();
    }

    public void StopSoundEffects()
    {
        CancelFanfare();
        foreach (var voice in _effects.Values) Retire(voice);
        _effects.Clear();
    }

    public void FadeOutMusic(double seconds, bool temporarily = false)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (_music is null) return;
        _fadeTargetGain = 0; _fadePauseAtEnd = temporarily;
        _fadeRemaining = _fadeDuration = seconds; _fadeStartGain = _musicGain;
        if (seconds == 0) FinishMusicFade();
    }

    /// <summary>Resume the retained BGM cursor and fade from zero; no replacement song or domain mutation.</summary>
    public void FadeInMusic(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (_music is null) return;
        _musicTemporarilyPaused = _fadePauseAtEnd = false;
        _musicGain = _fadeStartGain = 0; _fadeTargetGain = 1;
        _fadeRemaining = _fadeDuration = seconds;
        if (_fanfareToken == 0) _music.Player.StreamPaused = false;
        ApplyMusicGain();
        if (seconds == 0) FinishMusicFade();
    }

    private void FinishMusicFade()
    {
        _musicGain = _fadeTargetGain; _fadeRemaining = 0; ApplyMusicGain();
        if (_music is null || _fadeTargetGain != 0) return;
        if (_fadePauseAtEnd) { _musicTemporarilyPaused = true; _music.Player.StreamPaused = true; }
        else { Retire(_music); _music = null; _musicTemporarilyPaused = false; }
    }

    public void ResetPlayback()
    {
        _generation++;
        _fanfareToken = _fanfareMusicToken = 0; _fanfareCounter = 0;
        _fanfareClip = null;
        foreach (var voice in _effects.Values) Retire(voice);
        _effects.Clear(); Retire(_music); Retire(_cry); _music = _cry = null;
        _cryDucking = false; _cryGrace = 0; _sourceRemainder = _fadeRemaining = 0; _musicGain = 1;
        _fadePauseAtEnd = _musicTemporarilyPaused = false;
    }

    /// <summary>PCM downmix only; Vorbis and QOA streams are unchanged. This is not the source SOUND option.</summary>
    public void SetStereo(bool stereo)
    {
        if (_stereo == stereo) return;
        _stereo = stereo;
        foreach (var voice in AllVoices())
        {
            if (voice.Clip.IsVorbis || voice.Clip.IsQoa) continue;
            bool paused = voice.Player.StreamPaused, playing = voice.Player.HasStreamPlayback();
            float position = voice.Player.GetPlaybackPosition();
            voice.Player.Stop(); voice.Stream.Dispose();
            voice.Stream = voice.Clip.CreatePlaybackStream(stereo); voice.Player.Stream = voice.Stream;
            if (playing) { voice.Player.Play(position); voice.Player.StreamPaused = paused; }
        }
    }

    /// <summary>One source task frame: fanfare N decrements plus a separate zero-counter completion frame.</summary>
    public void AdvanceSourceFrame()
    {
        if (_fanfareToken != 0)
        {
            if (_fanfareCounter > 0) _fanfareCounter--;
            else FinishFanfare(true, "source-counter-complete");
        }
        if (_cryDucking)
        {
            if (_cryGrace > 0) _cryGrace--;
            else if (_cry is null || !_cry.Player.Playing)
            { _cryDucking = false; ApplyMusicGain(); }
        }
    }

    public SourceAudioPlaybackState Snapshot() => new(State(_music), _effects.Values.OrderBy(v => v.Clip.Metadata.SourcePlayer)
        .Select(v => State(v)!).ToArray(), State(_cry), _fanfareToken == 0 ? null : _fanfareToken,
        _fanfareToken == 0 ? null : _fanfareCounter, _cryDucking, _stereo, _generation);

    public override void _Process(double delta)
    {
        if (AutomaticSourceClock)
        {
            _sourceRemainder += delta * SourceFramesPerSecond;
            while (_sourceRemainder >= 1) { _sourceRemainder--; AdvanceSourceFrame(); }
        }
        if (_fadeRemaining > 0 && _music is not null)
        {
            _fadeRemaining = Math.Max(0, _fadeRemaining - delta);
            _musicGain = _fadeTargetGain + (_fadeStartGain - _fadeTargetGain) * (float)(_fadeRemaining / _fadeDuration); ApplyMusicGain();
            if (_fadeRemaining == 0) FinishMusicFade();
        }
    }

    public override void _ExitTree() => ResetPlayback();

    private bool TryClip(string id, int? expectedPlayer, out VerifiedSourceAudioClip? clip)
    {
        if (!IsInsideTree()) throw new InvalidOperationException("Attach SourceAudioPlayer to the scene before playback.");
        if (!_clips.TryGetValue(id, out clip))
        { PlaybackEvent?.Invoke(new("missing", id, 0, expectedPlayer ?? -1, "No verified audio registered", false)); return false; }
        if (expectedPlayer.HasValue && clip.Metadata.SourcePlayer != expectedPlayer)
            throw new InvalidOperationException("Wrong source player for " + id);
        return true;
    }

    private long StartEffect(VerifiedSourceAudioClip clip, string kind)
    {
        int player = clip.Metadata.SourcePlayer;
        if (_effects.TryGetValue(player, out var previous))
        {
            if (player is 1 or 2 && previous.Player.Playing && !previous.Player.StreamPaused
                && previous.Clip.Metadata.Priority > clip.Metadata.Priority)
            {
                PlaybackEvent?.Invoke(new("rejected", clip.Metadata.Id, 0, player,
                    "active-source-priority:" + previous.Clip.Metadata.Priority, clip.Metadata.IsOriginalTestFixture, clip.Metadata));
                return 0;
            }
            Retire(previous);
        }
        var voice = Start(clip, kind); _effects[player] = voice; return voice.Token;
    }

    private Voice Start(VerifiedSourceAudioClip clip, string kind)
    {
        long token = ++_nextToken, generation = _generation;
        var stream = clip.CreatePlaybackStream(_stereo);
        var node = new AudioStreamPlayer { Name = "Audio_" + token, Stream = stream, MaxPolyphony = 1 };
        var voice = new Voice(clip, node, stream, token);
        node.Finished += () =>
        {
            if (generation != _generation || !IsCurrent(voice)) return;
            if (ReferenceEquals(_music, voice)) _music = null;
            else if (ReferenceEquals(_cry, voice)) _cry = null;
            else _effects.Remove(clip.Metadata.SourcePlayer);
            Retire(voice);
            Emit("finished", voice, kind);
            // Neither callback resumes BGM: only the current source counter / cry task can do that.
        };
        AddChild(node); node.Play(); Emit("started", voice, kind); return voice;
    }

    private bool IsCurrent(Voice voice) => ReferenceEquals(_music, voice) || ReferenceEquals(_cry, voice)
        || (_effects.TryGetValue(voice.Clip.Metadata.SourcePlayer, out var current) && ReferenceEquals(current, voice));
    private IEnumerable<Voice> AllVoices()
    {
        if (_music is not null) yield return _music;
        if (_cry is not null) yield return _cry;
        foreach (var voice in _effects.Values) yield return voice;
    }
    private void FinishFanfare(bool resume, string reason)
    {
        long token = _fanfareToken;
        var metadata = _fanfareClip?.Metadata;
        if (resume && !_musicTemporarilyPaused && _music?.Token == _fanfareMusicToken) _music.Player.StreamPaused = false;
        _fanfareToken = _fanfareMusicToken = 0; _fanfareCounter = 0;
        _fanfareClip = null;
        PlaybackEvent?.Invoke(new("fanfare-task-finished", metadata?.Id ?? "", token, 2, reason,
            metadata?.IsOriginalTestFixture ?? false, metadata));
    }
    private void ApplyMusicGain()
    {
        if (_music is not null) _music.Player.VolumeLinear = _musicGain * (_cryDucking ? CryDuckGain : 1f);
    }
    private static SourceAudioVoiceState? State(Voice? voice) => voice is null ? null : new(voice.Clip.Metadata.Id,
        voice.Token, voice.Clip.Metadata.SourcePlayer, voice.Clip.Metadata.Priority, voice.Player.Playing,
        voice.Player.StreamPaused, voice.Player.GetPlaybackPosition(), voice.Player.VolumeLinear);
    private void Emit(string kind, Voice voice, string reason) => PlaybackEvent?.Invoke(new(kind,
        voice.Clip.Metadata.Id, voice.Token, voice.Clip.Metadata.SourcePlayer, reason, voice.Clip.Metadata.IsOriginalTestFixture, voice.Clip.Metadata));
    private static void Retire(Voice? voice)
    {
        if (voice is null || voice.Retired) return;
        voice.Retired = true; voice.Player.Stop(); voice.Player.Stream = null; voice.Stream.Dispose(); voice.Player.QueueFree();
    }
    private sealed class Voice(VerifiedSourceAudioClip clip, AudioStreamPlayer player, AudioStream stream, long token)
    {
        public VerifiedSourceAudioClip Clip { get; } = clip;
        public AudioStreamPlayer Player { get; } = player;
        public AudioStream Stream { get; set; } = stream;
        public long Token { get; } = token;
        public bool Retired { get; set; }
    }
}
