using System.Diagnostics.CodeAnalysis;
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
    private readonly bool _supportsAnimationCries;
    private Synth _synth;
    private AudioStreamPlayer? _output;
    private AudioStreamGenerator? _generator;
    private AudioStreamGeneratorPlayback? _playback;
    private ActiveCue? _music, _cry;
    private SynthCue? _fanfareCue;
    private long _nextToken, _generation, _fanfareToken, _fanfareMusicToken;
    private int _fanfareCounter, _cryGrace, _capacity, _retiredSkips;
    private double _sourceRemainder, _fadeRemaining, _fadeDuration;
    private float _helpGain = 1;
    private float _musicGain = 1, _fadeStartGain, _fadeTargetGain;
    private bool _fadePauseAtEnd, _musicTemporarilyPaused;
    private bool _cryDucking, _renderCryDucking, _stereo = true, _faulted, _shutdown;
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
        string directory = Path.Combine(Path.GetFullPath(projectRoot), "content/generated/audio");
        _resources = FireRedAudio.Resources.FromPack(Path.Combine(directory, "synth/firered.pack"));
        _cryCatalog = NormalCryCatalog.FromFile(Path.Combine(directory, "synth/normal-cries.bin"));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true };
        var manifest = JsonSerializer.Deserialize<SynthManifest>(
            File.ReadAllBytes(Path.Combine(directory, "cues.json")), options)
            ?? throw new InvalidDataException("Missing audio cue catalog.");
        if (manifest.SchemaVersion != 1 || manifest.Cues.Length == 0)
            throw new InvalidDataException("Audio cue catalog contract differs.");
        var cues = manifest.Cues;
        _catalog = new(StringComparer.Ordinal);
        foreach (var cue in cues)
        {
            if (string.IsNullOrWhiteSpace(cue.Id) || cue.Id.Length > 100 || cue.SourcePlayer is < 0 or > 4
                || cue.Priority is < 0 or > 255 || cue.SourceCounter is < 0 or > 36000
|| !_catalog.TryAdd(cue.Id, cue))
                throw new InvalidDataException("Invalid or duplicated synth cue.");
            if (cue.SourcePlayer == 4)
            {
                _cryCatalog.Get(cue.Species);
                if (cue.SongAddress != 0 || cue.Looping || cue.SourceCounter is not null ||
                    cue.CryMode is not (0 or SourceEncounterCry.Mode) || cue.Priority != 10 ||
                    !cue.SourceSymbol.StartsWith("SPECIES_", StringComparison.Ordinal) ||
                    cue.Id != CryId(cue.SourceSymbol, cue.CryMode))
                    throw new InvalidDataException("Invalid source cry cue.");
            }
            else
            {
                var song = Song.Read(_resources, cue.SongAddress);
                if (song.Priority != cue.Priority || cue.Species != 0 || cue.CryMode != 0)
                    throw new InvalidDataException("Synth song metadata differs from authenticated resource.");
            }
        }
        _synth = NewSynth();
        _supportsAnimationCries = _catalog.Values.Any(cue => cue.SourcePlayer == 4 && cue.CryMode == 0);
    }

    private Synth NewSynth()
    {
        var synth = new Synth(_resources, AudioProfile.SourceFiltered, _cryCatalog) { EnableTrace = false };
        foreach (var cue in _catalog.Values)
            if (cue.SourcePlayer == 4) synth.PrepareAnimationCry(cue.Species);
        return synth;
    }

    private static string CryId(string symbol, int mode) =>
        "CRY_" + symbol["SPECIES_".Length..] + (mode switch
        {
            0 => "_NORMAL", SourceDoublesCry.Mode => "_DOUBLES", SourceEncounterCry.Mode => "_ENCOUNTER",
            _ => throw new NotSupportedException("Unsupported source cry mode")
        });

    public string? CryCueId(string symbol, int mode) =>
        mode is 0 or SourceDoublesCry.Mode or SourceEncounterCry.Mode && symbol.StartsWith("SPECIES_", StringComparison.Ordinal) &&
        TryFindCue(CryId(symbol, mode), out var cue) && cue!.SourcePlayer == 4 && cue.CryMode == mode
            ? cue.Id : null;

    // Mode presets reuse the existing species binding; asking for a short cry must
    // neither duplicate the stored catalog nor mutate its registered entries.
    private bool TryFindCue(string id, [NotNullWhen(true)] out SynthCue? cue)
    {
        if (_catalog.TryGetValue(id, out cue)) return true;
        const string suffix = "_DOUBLES";
        if (id.StartsWith("CRY_", StringComparison.Ordinal) && id.EndsWith(suffix, StringComparison.Ordinal) &&
            _catalog.TryGetValue(id[..^suffix.Length] + "_NORMAL", out var normal) && normal.SourcePlayer == 4 && normal.CryMode == 0)
        {
            cue = normal with { Id = id, CryMode = SourceDoublesCry.Mode };
            return true;
        }
        return false;
    }

    public SourceAudioMetadata Metadata(string id) => TryFindCue(id, out var cue)
        ? cue!.Metadata : throw new InvalidDataException("Unknown synth cue: " + id);
    public string? CrySymbol(int species) => _catalog.Values.FirstOrDefault(c => c.Species == species)?.SourceSymbol;
    public bool HasClip(string id) => !_faulted && !_shutdown && TryFindCue(id, out _);

    public long PlayMusic(string id, bool restart = false)
    {
        if (!TryCue(id, 0, out var cue)) return 0;
        if (!restart && _music?.Cue.Id == id && _music.EndFrame is null) return _music.Token;
        CancelFanfare(resumeMusic: false);
        CancelCry();
        _synth.Stop(0);
        _seagallopMusicFade=null;
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

    public long PlayCry(string id, bool duckBgm = true)
    {
        if (!TryCue(id, 4, out var cue)) return 0;
        return StartCry(cue!, duckBgm);
    }

    // PlayCry_ByMode(mode 1) preserves BGM volume and accepts the source signed pan.
    public long PlayDoublesCry(string id, int pan = 0)
    {
        if (!TryCue(id, 4, out var cue)) return 0;
        if (cue!.CryMode != SourceDoublesCry.Mode) throw new InvalidOperationException("Not a doubles cry: " + id);
        return StartCry(cue, duckBgm: false, pan);
    }

    // Supported presets share the canonical species binding. Both source tone
    // tables and decoded waves were validated/prepared during composition.
    public bool SupportsBattleAnimationCries => _supportsAnimationCries && !_faulted && !_shutdown;
    public bool IsBattleAnimationCryPlaying => _cry is not null && !Completed(_cry);

    public long PlayBattleAnimationCry(int species, int mode, int pan)
    {
        if (!SourceAnimationCryPreset.IsSupported(mode)) throw new NotSupportedException("Unsupported animation cry mode");
        if (pan is < -128 or > 127) throw new ArgumentOutOfRangeException(nameof(pan));
        string symbol = CrySymbol(species) ?? throw new NotSupportedException("Species has no source cry binding");
        if (!TryCue(CryId(symbol, 0), 4, out var normal)) return 0;
        string suffix = mode switch
        {
            SourceHighPitchCry.Mode => "HIGH_PITCH", SourceAnimationCryPreset.EchoStart => "ECHO_START",
            SourceAnimationCryPreset.EchoEnd => "ECHO_END", SourceAnimationCryPreset.Growl1 => "GROWL_1",
            SourceAnimationCryPreset.Roar1 => "ROAR_1", SourceAnimationCryPreset.Roar2 => "ROAR_2",
            SourceAnimationCryPreset.Growl2 => "GROWL_2", _ => throw new NotSupportedException("Unsupported animation cry mode")
        };
        var cue = normal! with { Id = "CRY_" + symbol["SPECIES_".Length..] + "_" + suffix, CryMode = mode };
        return StartCry(cue, duckBgm: true, pan);
    }

    private long StartCry(SynthCue cue, bool duckBgm, int pan = 0)
    {
        if (SourceAnimationCryPreset.IsSupported(cue.CryMode)) _synth.StartAnimationCry(cue.Species, cue.CryMode, pan, duckBgm: false);
        else if (cue.CryMode == SourceDoublesCry.Mode) _synth.StartDoublesCry(cue.Species, pan, duckBgm: false);
        else if (cue.CryMode == SourceEncounterCry.Mode) _synth.StartEncounterCry(cue.Species, duckBgm: false);
        else _synth.StartNormalCry(cue.Species, duckBgm: false);
        _cry = new ActiveCue(cue, ++_nextToken, _pushedFrames, _synth.GameFrames);
        _cryDucking = _renderCryDucking = duckBgm; _cryGrace = duckBgm ? 2 : 0; ApplyMusicGain();
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
        _seagallopMusicFade=null;
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (_music is null) return;
        _fadeTargetGain = 0; _fadePauseAtEnd = temporarily;
        _fadeRemaining = _fadeDuration = seconds; _fadeStartGain = _musicGain;
        if (seconds == 0) FinishMusicFade();
    }

    /// <summary>Source temporary fade resumes the retained sequence; it does not start its song again.</summary>
    public void FadeInMusic(double seconds)
    {
        _seagallopMusicFade=null;
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
        if (_shutdown) return;
        ClearPlayback();
        // Feedback/voice state is fresh; immutable decoded samples remain scene-owned.
        _synth = _synth.CreateFreshPlayback();
    }

    public void ShutdownPlayback()
    {
        if (_shutdown) return;
        _shutdown = true;
        SetProcess(false);
        ClearPlayback();
    }

    private void ClearPlayback()
    {
        _seagallopMusicFade=null;
        _generation++;
        _fanfareToken = _fanfareMusicToken = 0; _fanfareCounter = 0; _fanfareCue = null;
        _music = _cry = null; _effects.Clear(); _cryDucking = _renderCryDucking = false; _cryGrace = 0;
        _sourceRemainder = _fadeRemaining = 0; _musicGain = _helpGain = 1;
        _fadePauseAtEnd = _musicTemporarilyPaused = false;
        ReleaseOutput();
        _pushedFrames = _capturedCompletionFrames = 0;
    }

    public void SetHelpDucking(bool enabled) { if (_shutdown) return; _helpGain = enabled ? .5f : 1f; ApplyMusicGain(); }

    public void SetStereo(bool stereo) => _stereo = stereo;

    public void AdvanceSourceFrame()
    {
        AdvanceSeagallopMusicFade();
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
        if (_faulted || _shutdown) return;
        try
        {
            UpdateCompletions();
            if (AutomaticSourceClock)
            {
                _sourceRemainder += delta * SourceAudioTiming.SourceFramesPerSecond;
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
        if (_faulted || _shutdown || !TryFindCue(id, out cue))
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

    private void ApplyMusicGain() => _synth.SetPlayerGain(0, _helpGain * _musicGain * (_renderCryDucking ? SourceAudioTiming.CryDuckGain : 1f));
    private SourceAudioVoiceState? State(ActiveCue? cue) => cue is null ? null : new(cue.Cue.Id, cue.Token,
        cue.Cue.SourcePlayer, cue.Cue.Priority, !Completed(cue), _synth.PlayerPaused(CoreSlot(cue.Cue.SourcePlayer)),
        (double)_synth.PlayerElapsedFrames(CoreSlot(cue.Cue.SourcePlayer)) / Synth.Rate,
        cue.Cue.SourcePlayer == 0 ? _helpGain * _musicGain * (_cryDucking ? SourceAudioTiming.CryDuckGain : 1f) : 1);
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
    public override void _ExitTree() => ShutdownPlayback();

    private sealed class ActiveCue(SynthCue cue, long token, long startFrame, long startGameFrame)
    {
        public SynthCue Cue { get; } = cue;
        public long Token { get; } = token;
        public long StartFrame { get; } = startFrame;
        public long StartGameFrame { get; } = startGameFrame;
        public long? EndFrame { get; set; }
    }
    private sealed record SynthManifest(int SchemaVersion, SynthCue[] Cues);
    private sealed record SynthCue(string Id, string SourceSymbol,
        int SourcePlayer, int Priority, int? SourceCounter, uint SongAddress, int Species, bool Looping, int CryMode = 0)
    {
        public SourceAudioMetadata Metadata => new(Id, SourceSymbol, SourcePlayer, Priority, SourceCounter, BackendId);
    }
}
