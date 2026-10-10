using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FireRedAudio;
using Godot;

/// <summary>Opt-in original sequence/sample synthesis. Scene thread owns controls and PCM production.</summary>
public partial class SynthSourceAudioPlayer : Node, ISourceAudioBackend
{
    public const int BlockFrames = 512;
    public const int TargetQueuedFrames = 5120;
    public const string BackendId = "original-csharp-source-filtered-v1";
    private readonly float[] _pcm = new float[BlockFrames * 2];
    private readonly Vector2[] _frames = new Vector2[BlockFrames];
    private readonly Dictionary<string, SynthCue> _catalog;
    private readonly Dictionary<int, ActiveCue> _effects = [];
    private readonly FireRedAudio.Resources _resources;
    private readonly NormalCryCatalog _cryCatalog;
    private Synth _synth;
    private AudioStreamPlayer? _output;
    private AudioStreamGenerator? _generator;
    private AudioStreamGeneratorPlayback? _playback;
    private ActiveCue? _music, _cry;
    private SynthCue? _fanfareCue;
    private long _nextToken, _generation, _fanfareToken, _fanfareMusicToken;
    private int _fanfareCounter, _cryGrace, _capacity, _retiredSkips;
    private double _sourceRemainder, _fadeRemaining, _fadeDuration;
    private float _musicGain = 1, _fadeStartGain, _fadeTargetGain;
    private bool _fadePauseAtEnd, _musicTemporarilyPaused;
    private bool _cryDucking, _renderCryDucking, _stereo = true, _faulted;
    private long _pushedFrames, _capturedCompletionFrames;
    public bool AutomaticSourceClock { get; set; } = true;
    public event Action<SourceAudioPlaybackEvent>? PlaybackEvent;
    public int RegisteredClipCount => _catalog.Count;
    public int UnderrunSkips => _retiredSkips + (_playback?.GetSkips() ?? 0);
    public int QueuedFrames => _playback is null ? 0 : _capacity - _playback.GetFramesAvailable();
    public double QueueBoundMilliseconds => 1000d * TargetQueuedFrames / Synth.Rate;
    public long ProducedFrames => _pushedFrames;
    public long ConsumedFrames => Math.Max(0, _pushedFrames - QueuedFrames);
    public bool Faulted => _faulted;
    public double Peak => _synth.Peak;
    public long Clipped => _synth.Clipped;

    public SynthSourceAudioPlayer(string projectRoot)
    {
        string directory = Path.Combine(Path.GetFullPath(projectRoot), "content/generated/synth-audio");
        string pack = Path.Combine(directory, "firered.pack");
        if (new FileInfo(pack).Length != FireRedAudio.Resources.End - FireRedAudio.Resources.Base)
            throw new InvalidDataException("Synth resource pack length differs.");
        _resources = FireRedAudio.Resources.FromPack(pack);
        _cryCatalog = NormalCryCatalog.FromFile(Path.Combine(directory, "normal-cries.bin"));
        string manifestPath = Path.Combine(directory, "manifest.json");
        if (new FileInfo(manifestPath).Length != SynthManifestIdentity.Bytes)
            throw new InvalidDataException("Synth manifest length differs.");
        byte[] bytes = File.ReadAllBytes(manifestPath);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != SynthManifestIdentity.Sha256)
            throw new InvalidDataException("Synth manifest identity differs.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var manifest = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("Missing synth manifest.");
        if (manifest.SchemaVersion != 1 || manifest.SourceCommit != SynthManifestIdentity.SourceCommit
            || manifest.Cues.Length != SynthManifestIdentity.CueCount)
            throw new InvalidDataException("Synth manifest contract differs.");
        _catalog = new(StringComparer.Ordinal);
        foreach (var cue in manifest.Cues)
        {
            if (string.IsNullOrWhiteSpace(cue.Id) || cue.Id.Length > 100 || cue.SourcePlayer is < 0 or > 4
                || cue.Priority is < 0 or > 255 || cue.SourceCounter is < 0 or > 36000
                || cue.SourceCommit != manifest.SourceCommit || !_catalog.TryAdd(cue.Id, cue))
                throw new InvalidDataException("Invalid or duplicated synth cue.");
            if (cue.SourcePlayer == 4)
            {
                _cryCatalog.Get(cue.Species);
                if (cue.SongAddress != 0 || cue.Looping || cue.SourceCounter is not null)
                    throw new InvalidDataException("Invalid normal cry cue.");
            }
            else
            {
                var song = Song.Read(_resources, cue.SongAddress);
                if (song.Priority != cue.Priority || cue.Species != 0)
                    throw new InvalidDataException("Synth song metadata differs from authenticated resource.");
            }
        }
        _synth = NewSynth();
    }

    private Synth NewSynth()
    {
        var synth = new Synth(_resources, AudioProfile.SourceFiltered, _cryCatalog) { EnableTrace = false };
        foreach (var cue in _catalog.Values)
            if (cue.SourcePlayer == 4) synth.PrepareNormalCry(cue.Species);
        return synth;
    }

    public SourceAudioMetadata Metadata(string id) => _catalog.TryGetValue(id, out var cue)
        ? cue.Metadata : throw new InvalidDataException("Unknown synth cue: " + id);
    public string? CrySymbol(int species) => _catalog.Values.FirstOrDefault(c => c.Species == species)?.SourceSymbol;
    public bool HasClip(string id) => !_faulted && _catalog.ContainsKey(id);
    public void Register(VerifiedSourceAudioClip clip) => throw new NotSupportedException("Synth admits only its frozen sequence catalog; no recorded fallback.");

    public long PlayMusic(string id, bool restart = false)
    {
        if (!TryCue(id, 0, out var cue)) return 0;
        if (!restart && _music?.Cue.Id == id && _music.EndFrame is null) return _music.Token;
        CancelFanfare(resumeMusic: false);
        CancelCry();
        _synth.Stop(0);
        _musicGain = 1; _fadeRemaining = 0; _fadePauseAtEnd = _musicTemporarilyPaused = false;
        _music = Start(cue!, "music");
        ApplyMusicGain();
        return _music.Token;
    }

    public long PlaySoundEffect(string id)
    {
        if (!TryCue(id, null, out var cue)) return 0;
        if (cue!.SourcePlayer is < 1 or > 3) throw new InvalidOperationException("Not an SE player: " + id);
        return StartEffect(cue, "effect");
    }

    public long PlayFanfare(string id)
    {
        if (!TryCue(id, 2, out var cue)) return 0;
        if (cue!.SourceCounter is null || cue.Looping)
            throw new InvalidOperationException("Fanfare requires a finite sequence and independent source counter.");
        long token = StartEffect(cue, "fanfare");
        if (token == 0) token = ++_nextToken;
        _fanfareToken = token; _fanfareCounter = cue.SourceCounter.Value;
        _fanfareCue = cue; _fanfareMusicToken = _music?.Token ?? 0;
        if (_music is not null) _synth.PausePlayer(0);
        return token;
    }

    public long PlayCry(string id)
    {
        if (!TryCue(id, 4, out var cue)) return 0;
        _synth.StartNormalCry(cue!.Species, duckBgm: false);
        _cry = new ActiveCue(cue, ++_nextToken, _pushedFrames, _synth.GameFrames);
        _cryDucking = _renderCryDucking = true; _cryGrace = 2; ApplyMusicGain();
        Emit("started", _cry, "cry");
        return _cry.Token;
    }

    public void CancelFanfare(long? expectedToken = null, bool resumeMusic = true)
    {
        if (_fanfareToken == 0 || expectedToken.HasValue && expectedToken != _fanfareToken) return;
        if (_effects.TryGetValue(2, out var cue) && cue.Token == _fanfareToken)
        { _synth.Stop(3); _effects.Remove(2); }
        FinishFanfare(resumeMusic, "cancelled");
    }

    public void CancelCry(long? expectedToken = null)
    {
        if (expectedToken.HasValue && _cry?.Token != expectedToken) return;
        _synth.Stop(2); _cry = null; _cryDucking = _renderCryDucking = false; _cryGrace = 0; ApplyMusicGain();
    }

    public void StopSoundEffects()
    {
        CancelFanfare();
        foreach (int sourcePlayer in _effects.Keys) _synth.Stop(CoreSlot(sourcePlayer));
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

    /// <summary>Source temporary fade resumes the retained sequence; it does not start its song again.</summary>
    public void FadeInMusic(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (_music is null) return;
        _musicTemporarilyPaused = _fadePauseAtEnd = false;
        _musicGain = _fadeStartGain = 0; _fadeTargetGain = 1;
        _fadeRemaining = _fadeDuration = seconds;
        if (_fanfareToken == 0) _synth.ResumePlayer(0);
        ApplyMusicGain();
        if (seconds == 0) FinishMusicFade();
    }

    private void FinishMusicFade()
    {
        _musicGain = _fadeTargetGain; _fadeRemaining = 0; ApplyMusicGain();
        if (_music is null || _fadeTargetGain != 0) return;
        if (_fadePauseAtEnd) { _musicTemporarilyPaused = true; _synth.PausePlayer(0); }
        else { _synth.Stop(0); _music = null; _musicTemporarilyPaused = false; }
    }

    public void ResetPlayback()
    {
        _generation++;
        _fanfareToken = _fanfareMusicToken = 0; _fanfareCounter = 0; _fanfareCue = null;
        _music = _cry = null; _effects.Clear(); _cryDucking = _renderCryDucking = false; _cryGrace = 0;
        _sourceRemainder = _fadeRemaining = 0; _musicGain = 1;
        _fadePauseAtEnd = _musicTemporarilyPaused = false;
        ReleaseOutput();
        // A new mixer clears feedback tails as well as voices on session replacement.
        _synth = NewSynth();
        _pushedFrames = _capturedCompletionFrames = 0;
    }

    public void SetStereo(bool stereo) => _stereo = stereo;

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
            else if (_cry is null) { _cryDucking = false; ApplyMusicGain(); }
        }
    }

    public SourceAudioPlaybackState Snapshot() => new(State(_music),
        _effects.Values.OrderBy(c => c.Cue.SourcePlayer).Select(c => State(c)!).ToArray(), State(_cry),
        _fanfareToken == 0 ? null : _fanfareToken, _fanfareToken == 0 ? null : _fanfareCounter,
        _cryDucking, _stereo, _generation);

    public override void _Process(double delta)
    {
        if (_faulted) return;
        try
        {
            UpdateCompletions();
            if (AutomaticSourceClock)
            {
                _sourceRemainder += delta * SourceAudioPlayer.SourceFramesPerSecond;
                while (_sourceRemainder >= 1) { _sourceRemainder--; AdvanceSourceFrame(); }
            }
            if (_fadeRemaining > 0 && _music is not null)
            {
                _fadeRemaining = Math.Max(0, _fadeRemaining - delta);
                _musicGain = _fadeTargetGain + (_fadeStartGain - _fadeTargetGain) * (float)(_fadeRemaining / _fadeDuration); ApplyMusicGain();
                if (_fadeRemaining == 0) FinishMusicFade();
            }
            if (_music is not null || _cry is not null || _effects.Count != 0 || _playback is not null) Pump();
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or NotSupportedException or ArithmeticException)
        {
            _faulted = true; ResetPlayback();
            PlaybackEvent?.Invoke(new("fault", "", 0, -1, error.Message, false));
        }
    }

    private void Pump()
    {
        EnsureOutput();
        while (_playback!.GetFramesAvailable() >= BlockFrames && QueuedFrames + BlockFrames <= TargetQueuedFrames)
        {
            _synth.Render(_pcm);
            for (int n = 0; n < BlockFrames; n++)
            {
                float left = _pcm[n * 2], right = _pcm[n * 2 + 1];
                if (!float.IsFinite(left) || !float.IsFinite(right)) throw new InvalidDataException("Nonfinite synthesized PCM.");
                if (!_stereo) left = right = (left + right) * .5f;
                _frames[n] = new Vector2(left, right);
            }
            if (!_playback.PushBuffer(_frames)) throw new InvalidOperationException("PCM ring rejected a checked block.");
            _pushedFrames += BlockFrames;
            MarkEnded(_music); MarkEnded(_cry);
            foreach (var cue in _effects.Values) MarkEnded(cue);
        }
        _output!.StreamPaused = false;
    }

    private void EnsureOutput()
    {
        if (_playback is not null) return;
        _generator = new AudioStreamGenerator { MixRate = Synth.Rate, BufferLength = .1f,
            MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom };
        _output = new AudioStreamPlayer { Stream = _generator, VolumeDb = 0 };
        AddChild(_output); _output.Play(); _output.StreamPaused = true;
        _playback = (AudioStreamGeneratorPlayback)_output.GetStreamPlayback();
        _capacity = _playback.GetFramesAvailable();
        if (_capacity < TargetQueuedFrames) throw new InvalidOperationException("PCM ring capacity is below configured queue target.");
    }

    private void MarkEnded(ActiveCue? cue)
    {
        if (cue is not null && cue.EndFrame is null && !_synth.PlayerPaused(CoreSlot(cue.Cue.SourcePlayer)) && !(cue.Cue.SourcePlayer == 4 ? _synth.GameFrames <= cue.StartGameFrame || _synth.CryPlaying : _synth.PlayerPlaying(CoreSlot(cue.Cue.SourcePlayer))))
        {
            cue.EndFrame = _pushedFrames;
            if (cue.Cue.SourcePlayer == 4)
            {
                // Restore future PCM at the synthesized cry boundary; the public wait/state
                // remains active until that boundary is consumed by the output ring.
                _renderCryDucking = false;
                ApplyMusicGain();
            }
        }
    }

    private void UpdateCompletions()
    {
        _capturedCompletionFrames = ConsumedFrames;
        if (Completed(_music)) { Emit("finished", _music!, "music"); _music = null; }
        if (Completed(_cry)) { Emit("finished", _cry!, "cry"); _cry = null; }
        foreach (int key in _effects.Where(p => Completed(p.Value)).Select(p => p.Key).ToArray())
        { Emit("finished", _effects[key], "effect"); _effects.Remove(key); }
    }
    private bool Completed(ActiveCue? cue) => cue?.EndFrame is long end && _capturedCompletionFrames >= end;

    private bool TryCue(string id, int? expectedPlayer, out SynthCue? cue)
    {
        if (!IsInsideTree()) throw new InvalidOperationException("Attach synth backend before playback.");
        if (_faulted || !_catalog.TryGetValue(id, out cue))
        { cue = null; PlaybackEvent?.Invoke(new("missing", id, 0, expectedPlayer ?? -1, "No admitted synthesized cue", false)); return false; }
        if (expectedPlayer.HasValue && cue.SourcePlayer != expectedPlayer)
            throw new InvalidOperationException("Wrong source player for " + id);
        return true;
    }

    private long StartEffect(SynthCue cue, string kind)
    {
        int player = cue.SourcePlayer;
        if (player is 1 or 2 && _effects.TryGetValue(player, out var previous)
            && !Completed(previous) && previous.Cue.Priority > cue.Priority)
        {
            PlaybackEvent?.Invoke(new("rejected", cue.Id, 0, player, "active-source-priority:" + previous.Cue.Priority, false, cue.Metadata));
            return 0;
        }
        var active = Start(cue, kind); _effects[player] = active; return active.Token;
    }

    private ActiveCue Start(SynthCue cue, string kind)
    {
        _synth.Start(cue.SongAddress, CoreSlot(cue.SourcePlayer));
        var active = new ActiveCue(cue, ++_nextToken, _pushedFrames, _synth.GameFrames); Emit("started", active, kind); return active;
    }

    private void FinishFanfare(bool resume, string reason)
    {
        long token = _fanfareToken; var cue = _fanfareCue;
        if (resume && !_musicTemporarilyPaused && _music?.Token == _fanfareMusicToken) _synth.ResumePlayer(0);
        _fanfareToken = _fanfareMusicToken = 0; _fanfareCounter = 0; _fanfareCue = null;
        PlaybackEvent?.Invoke(new("fanfare-task-finished", cue?.Id ?? "", token, 2, reason, false, cue?.Metadata));
    }

    private void ApplyMusicGain() => _synth.SetPlayerGain(0, _musicGain * (_renderCryDucking ? SourceAudioPlayer.CryDuckGain : 1f));
    private SourceAudioVoiceState? State(ActiveCue? cue) => cue is null ? null : new(cue.Cue.Id, cue.Token,
        cue.Cue.SourcePlayer, cue.Cue.Priority, !Completed(cue), _synth.PlayerPaused(CoreSlot(cue.Cue.SourcePlayer)),
        (double)_synth.PlayerElapsedFrames(CoreSlot(cue.Cue.SourcePlayer)) / Synth.Rate,
        cue.Cue.SourcePlayer == 0 ? _musicGain * (_cryDucking ? SourceAudioPlayer.CryDuckGain : 1f) : 1);
    private void Emit(string kind, ActiveCue cue, string reason) => PlaybackEvent?.Invoke(new(kind,
        cue.Cue.Id, cue.Token, cue.Cue.SourcePlayer, reason, false, cue.Cue.Metadata));
    private static int CoreSlot(int player) => player switch { 0 => 0, 1 => 1, 2 => 3, 3 => 4, 4 => 2, _ => throw new ArgumentOutOfRangeException(nameof(player)) };

    private void ReleaseOutput()
    {
        if (_playback is not null) { _retiredSkips += _playback.GetSkips(); }
        if (_output is not null) { _output.Stop(); _output.Stream = null; _output.QueueFree(); _output = null; }
        _playback?.Dispose(); _playback = null;
        _generator?.Dispose(); _generator = null; _capacity = 0;
    }
    public override void _ExitTree() => ResetPlayback();

    private sealed class ActiveCue(SynthCue cue, long token, long startFrame, long startGameFrame)
    {
        public SynthCue Cue { get; } = cue;
        public long Token { get; } = token;
        public long StartFrame { get; } = startFrame;
        public long StartGameFrame { get; } = startGameFrame;
        public long? EndFrame { get; set; }
    }
    private sealed record SynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues);
    private sealed record SynthCue(string Id, string SourceCommit, string SourceSymbol, string SourcePath,
        int SourcePlayer, int Priority, int? SourceCounter, uint SongAddress, int Species, bool Looping)
    {
        public SourceAudioMetadata Metadata => new(Id, SourceCommit, SourceSymbol, SourcePath, SourcePlayer, Priority, SourceCounter, BackendId);
    }
}
