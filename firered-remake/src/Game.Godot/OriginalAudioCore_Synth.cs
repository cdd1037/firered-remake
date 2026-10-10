namespace FireRedAudio;

public sealed record Instrument(int Type, int Root, int Length, int Sweep, uint Data, int Attack, int Decay, int Sustain, int Release);
public sealed class Voice
{
    public bool Active, Releasing, Detached, ReleaseStarted, PsgStarted, Echoing;
    public Player? Player;
    public Track? Track;
    public Instrument? Instrument;
    public int Key, Velocity, Gate, Priority, Env, Goal, SustainGoal, Stage, EnvelopeClock, Length, Loop;
    public int MidiKey, RhythmPan, PseudoEchoVolume, PseudoEchoLength;
    public uint Samples;
    public sbyte[]? DecodedSamples;
    public byte[]? WaveSamples;
    public int NoiseState, NoiseOutput;
    public bool Looping;
    public double Position, Phase, Frequency;
    public int SweepRegister;
    public double SweepClock;
    public long Age;
}
public sealed partial class Synth
{
    public const int Rate = 48000;
    public const long Cpu = 16777216, FrameCycles = 280896;
    public readonly Resources Resources;
    public readonly AudioProfile Profile;
    readonly SourceDelay? sourceDelay;
    readonly DcBlocker? dcBlocker;
    public readonly Voice[] Voices = Enumerable.Range(0, 9).Select(_ => new Voice()).ToArray();
    // Host slots retain the original prototype API: 0=BGM, 1=SE1, 2=cry.
    // The remaining original song players are 3=SE2 and 4=SE3. All share 9 voices.
    readonly Player?[] players = new Player[5];
    readonly bool[] pausedPlayers = new bool[5];
    readonly long[] playerFrames = new long[5];
    readonly double[] playerGains = { 1, 1, 1, 1, 1 };
    static readonly int[] PlayerOrder = { 0, 1, 3, 4, 2 };
    sbyte[]? charmanderSamples;
    readonly NormalCryCatalog? cryCatalog;
    readonly Dictionary<uint, sbyte[]> catalogSamples = new();
    public int PreparedCryCount => catalogSamples.Count;
    public int PreparedCryBytes => catalogSamples.Values.Sum(samples => samples.Length);
    int duckFrames;
    bool duckActive;
    int reverbAmount;
    internal int ReverbAmount => reverbAmount;
    public bool CryPlaying => Voices.Any(v => v.Active && v.Player?.Id == 2);
    public bool BgmDucked => duckActive;
    readonly Dictionary<uint, Instrument> instruments = new();
    public readonly List<string> Trace = new();
    public bool EnableTrace = true;
    public long OutputFrames, GameFrames;
    long clock;
    long ages;
    long envelopeClock;
    int driverEnvelopePhase;
    readonly uint[] sourceFrequencyTable = new uint[12];
    public long Stolen, Rejected, Clipped;
    public int MaxPcm;
    public double Peak;
    public bool Ended => players.All(p => p == null || p.Tracks.All(t => t.Ended)) && !Voices.Any(v => v.Active) && (reverbAmount == 0 || sourceDelay?.IsSilent == true || sourceDelay == null) && (dcBlocker?.IsSilent ?? true);
    public Synth(Resources resources, AudioProfile profile = AudioProfile.ModernDryV1, NormalCryCatalog? cryCatalog = null)
    {
        if (!Enum.IsDefined(profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        Resources = resources;
        this.cryCatalog = cryCatalog;
        Profile = profile;
        for (int n = 0; n < 12; n++)
            sourceFrequencyTable[n] = resources.U32(0x08489abc + (uint)n * 4);
        if (profile >= AudioProfile.SourceReverb)
            sourceDelay = new SourceDelay();
        if (profile == AudioProfile.SourceFiltered)
            dcBlocker = new DcBlocker();
    }
    // Control calls and Render have one owner thread; this prototype has no command queue.
    public void Start(uint song, int player)
    {
        if (player is not (0 or 1 or 3 or 4))
            throw new ArgumentOutOfRangeException(nameof(player));
        // A rejected header must not discard the currently playing song.
        var next = new Player(Resources, this, Song.Read(Resources, song), player);
        int headerReverb = Resources.U8(song + 3);
        Stop(player);
        players[player] = next;
        if ((headerReverb & 128) != 0)
            reverbAmount = headerReverb & 127;
        if (player == 0 && duckActive)
            SetBgmExternalVolume(21);
    }
    public void Stop(int player)
    {
        ValidatePlayer(player);
        foreach (var v in Voices)
            if (v.Player?.Id == player)
                v.Active = false;
        players[player] = null;
        pausedPlayers[player] = false;
        playerFrames[player] = 0;
    }

    static void ValidatePlayer(int player)
    {
        if (player is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(player));
    }

    public bool HasPlayer(int player)
    {
        ValidatePlayer(player);
        return players[player] != null;
    }

    static bool HasSequence(Player player)
    {
        foreach (var track in player.Tracks)
            if (!track.Ended) return true;
        return false;
    }

    // This includes detached release tails, even when their sequence is paused/ended.
    // It excludes the shared reverb/DC-filter history, which has no individual owner.
    public bool PlayerPlaying(int player)
    {
        ValidatePlayer(player);
        if (players[player] is { } p && !pausedPlayers[player] && HasSequence(p))
            return true;
        foreach (var voice in Voices)
            if (voice.Active && voice.Player?.Id == player) return true;
        return false;
    }

    public bool PlayerPaused(int player)
    {
        ValidatePlayer(player);
        return pausedPlayers[player];
    }

    // 48 kHz rendered frame position, frozen while paused or finished; Start/Stop reset it.
    public long PlayerElapsedFrames(int player)
    {
        ValidatePlayer(player);
        return playerFrames[player];
    }

    public double GetPlayerGain(int player)
    {
        ValidatePlayer(player);
        return playerGains[player];
    }

    // Host gain is separate from track VOL and cry ducking, and persists across Start/Stop.
    // It scales new contributions to the shared mix, not already-buffered reverb history.
    public void SetPlayerGain(int player, double gain)
    {
        ValidatePlayer(player);
        if (!double.IsFinite(gain) || gain is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(gain));
        playerGains[player] = gain;
    }

    // Original m4aMPlayStop + TrackStop: retain the sequencer, immediately silence
    // linked channels, and leave already-detached FINE/release tails to decay.
    // This is deliberately not a waveform freeze or a pause of the shared mixer.
    public void PausePlayer(int player)
    {
        ValidatePlayer(player);
        if (players[player] == null) return;
        pausedPlayers[player] = true;
        foreach (var voice in Voices)
            if (voice.Active && !voice.Detached && voice.Player?.Id == player)
            {
                voice.Active = false;
                voice.Detached = true;
            }
    }

    // Original MPlayContinue clears PAUSE only: old voices are not resurrected.
    public void ResumePlayer(int player)
    {
        ValidatePlayer(player);
        pausedPlayers[player] = false;
    }
    public void StartNormalCry(int species = 4, int mode = 0, bool duckBgm = true)
    {
        if (mode != 0) throw new NotSupportedException("Only normal cry mode is supported");
        var (song, sequence) = cryCatalog is null
            ? NormalCharmanderCry.Build(Resources, species, mode)
            : NormalCharmanderCry.BuildForTone(Resources, cryCatalog.Get(species).ToneAddress);
        PrepareNormalCry(species);
        Stop(2);
        players[2] = new Player(Resources, this, song, 2, sequence);
        if (duckBgm)
        {
            duckActive = true;
            duckFrames = 2;
            SetBgmExternalVolume(21);
            if (EnableTrace)
                Trace.Add($"frame={GameFrames} sample={OutputFrames} duck=on externalVolume=21");
        }
    }

    public void PrepareNormalCry(int species = 4)
    {
        if (cryCatalog is not null)
        {
            PrepareCatalogCry(cryCatalog.Get(species));
            return;
        }
        if (species != 4) throw new NotSupportedException("No ordinary-cry catalog loaded");
        if (charmanderSamples != null)
            return;
        uint wave = NormalCharmanderCry.WaveAddress;
        var tone = ReadInstrument(NormalCharmanderCry.ToneAddress);
        if (tone.Type != 0x20 || tone.Root != 60 || tone.Data != wave || tone.Attack != 255 || tone.Decay != 0 || tone.Sustain != 255 || tone.Release != 0)
            throw new InvalidDataException("Unexpected fixed Charmander tone");
        if (Resources.U16(wave) != 1 || Resources.U16(wave + 2) != 0 || Resources.U32(wave + 4) != 10764288 || Resources.U32(wave + 8) != 0 || Resources.U32(wave + 12) != NormalCharmanderCry.DecodedLength)
            throw new InvalidDataException("Unexpected fixed Charmander sample header");
        charmanderSamples = DpcmDecoder.Decode(Resources, wave + 16, NormalCharmanderCry.DecodedLength + 1, NormalCharmanderCry.EncodedPayloadLength);
    }

    void PrepareCatalogCry(NormalCryEntry entry)
    {
        var tone = ReadInstrument(entry.ToneAddress);
        uint wave = tone.Data;
        if (catalogSamples.ContainsKey(wave)) return;
        if (tone.Type != 0x20 || tone.Root != 60 || tone.Attack != 255 || tone.Decay != 0 || tone.Sustain != 255 || tone.Release != 0 ||
            Resources.U16(wave) != 1 || Resources.U16(wave + 2) != 0 || Resources.U32(wave + 4) != 10764288 ||
            Resources.U32(wave + 8) != 0 || Resources.U32(wave + 12) != entry.SampleCount)
            throw new InvalidDataException("Unexpected fixed normal cry metadata");
        var samples = DpcmDecoder.Decode(Resources, wave + 16,
            entry.SampleCount + (entry.GuardStored ? 1 : 0), entry.PayloadBytes);
        if (!entry.GuardStored)
        {
            // Independently verified fixed-asset omission; never read adjacent bytes.
            Array.Resize(ref samples, entry.SampleCount + 1);
            samples[entry.SampleCount] = samples[entry.SampleCount - 1];
        }
        catalogSamples.Add(wave, samples);
    }

    void SetBgmExternalVolume(int volume)
    {
        if (players[0] is { } bgm)
        foreach (var track in bgm.Tracks)
            track.ExternalVolume = volume;
    }

    void AdvanceCryDucking()
    {
        if (!duckActive)
            return;
        if (duckFrames > 0)
        {
            duckFrames--;
            return;
        }
        if (CryPlaying)
            return;
        duckActive = false;
        SetBgmExternalVolume(64);
        if (EnableTrace)
            Trace.Add($"frame={GameFrames} sample={OutputFrames} duck=off externalVolume=64");
    }

    public void Log(Player p, Track t, string text)
    {
        if (EnableTrace)
            Trace.Add($"frame={GameFrames} sample={OutputFrames} player={p.Id} track={t.Index} tick={p.Tick} {text}");
    }
    Instrument ReadInstrument(uint p)
    {
        if (instruments.TryGetValue(p, out var cached))
            return cached;
        var i = new Instrument(Resources.U8(p), Resources.U8(p + 1), Resources.U8(p + 2), Resources.U8(p + 3), Resources.U32(p + 4), Resources.U8(p + 8), Resources.U8(p + 9), Resources.U8(p + 10), Resources.U8(p + 11));
        instruments.Add(p, i);
        return i;
    }
    public void Note(Player player, Track track, int key, int velocity, int duration)
    {
        int midiKey = key - track.KeyShift;
        if (midiKey is < 0 or > 127)
            throw new InvalidDataException("Note key outside MIDI range");
        if (track.PseudoEchoVolume is < 0 or > 255 || track.PseudoEchoLength is < 0 or > 255)
            throw new InvalidDataException("Pseudo-echo parameter outside byte range");
        int rhythmPan = 0;
        var i = ReadInstrument(player.Song.Voices + (uint)track.Program * 12);
        if ((i.Type & 0xc0) != 0)
        {
            int parentType = i.Type;
            uint voiceAddress = i.Data;
            int index = midiKey;
            if ((parentType & 0x40) != 0)
            {
                uint map = Resources.U32(player.Song.Voices + (uint)track.Program * 12 + 8);
                index = Resources.U8(map + (uint)midiKey);
            }
            i = ReadInstrument(voiceAddress + (uint)index * 12);
            if ((i.Type & 0xc0) != 0)
                throw new NotSupportedException("Nested composite voices");
            if ((parentType & 0x80) != 0)
            {
                key = i.Root + track.KeyShift;
                if ((i.Sweep & 0x80) != 0)
                {
                    rhythmPan = unchecked((sbyte)((i.Sweep - 0xc0) * 2));
                    // A rhythm-pan byte is not a square-channel sweep setting.
                    if ((i.Type & 7) != 0)
                        i = i with { Sweep = 8 };
                }
            }
        }
        key = Math.Max(0, key);
        if (track.ReleaseOverride >= 0 && track.ReleaseOverride != i.Release)
            i = i with
            {
                Release = track.ReleaseOverride
            };
        if (i.Type is not (0 or 8 or 1 or 2 or 3 or 4 or 9 or 10 or 11 or 12 or 0x20))
            throw new NotSupportedException($"Unsupported instrument flags {i.Type:X2}");
        int kind = i.Type & 7;
        if (kind is < 0 or > 4)
            throw new NotSupportedException($"Voice type {i.Type}");
        // Validate immutable asset metadata before selecting or mutating a live voice.
        int sampleLength = 0, loopStart = 0;
        uint sampleAddress = 0;
        bool looping = false;
        double sampleFrequency = 0;
        sbyte[]? decoded = null;
        byte[]? wave = null;
        if (kind == 0)
        {
            bool compressed = (i.Type & 0x20) != 0;
            if (compressed)
            {
                if (catalogSamples.TryGetValue(i.Data, out var cached))
                    decoded = cached;
                else
#if CRY_COVERAGE_RESEARCH
                if (researchSamples.TryGetValue(i.Data, out var prepared))
                    decoded = prepared;
                else
#endif
                {
                    if (i.Data != NormalCharmanderCry.WaveAddress)
                        throw new NotSupportedException("Only the fixed Charmander compressed sample is supported");
                    PrepareNormalCry();
                    decoded = catalogSamples.TryGetValue(i.Data, out var preparedCharmander) ? preparedCharmander : charmanderSamples;
                }
            }
            else if (Resources.U16(i.Data) != 0)
                throw new NotSupportedException("Unsupported PCM header type");
            sampleLength = checked((int)Resources.U32(i.Data + 12));
            loopStart = checked((int)Resources.U32(i.Data + 8));
            looping = (Resources.U16(i.Data + 2) & 0x4000) != 0;
            sampleAddress = i.Data + 16;
            if (decoded == null)
                Resources.Offset(sampleAddress, sampleLength);
            if (sampleLength == 0 || (looping && (loopStart < 0 || loopStart >= sampleLength)))
                throw new InvalidDataException("Invalid sample loop/length");
            sampleFrequency = Resources.U32(i.Data + 4) / 1024.0;
        }
        else if (kind is 1 or 2 && i.Data > 3)
            throw new InvalidDataException("Invalid square duty");
        else if (kind == 3)
        {
            if (i.Length != 0)
                throw new NotSupportedException("Programmable-wave hardware length counters are not implemented");
            wave = PrepareWave(i.Data);
        }
        else if (kind == 4)
        {
            if (i.Length != 0)
                throw new NotSupportedException("Noise hardware length counters are not implemented");
            if (i.Data > 1)
                throw new InvalidDataException("Invalid noise width selector");
        }
        int priority = Math.Min(255, player.Song.Priority + track.Priority);
        // Five PCM slots are separate from the four CGB slots. Stable source track order
        // reproduces priority ties without emulating the original RAM addresses.
        Voice? target = null;
        if (kind == 0)
        {
            for (int j = 0; j < 5; j++)
                if (!Voices[j].Active)
                {
                    target = Voices[j];
                    break;
                }
            if (target == null)
            {
                for (int j = 0; j < 5; j++)
                {
                    var old = Voices[j];
                    if (!old.Releasing && (old.Priority > priority || (old.Priority == priority && Owner(old) < Owner(player, track))))
                        continue;
                    if (target == null || (old.Releasing && !target.Releasing) || (old.Releasing == target.Releasing && (old.Priority < target.Priority || (old.Priority == target.Priority && Owner(old) >= Owner(target)))))
                        target = old;
                }
                if (target == null)
                {
                    Rejected++;
                    return;
                }
                Stolen++;
            }
        }
        else
        {
            target = Voices[4 + kind];
            if (target.Active && !target.Releasing && (target.Priority > priority || (target.Priority == priority && Owner(target) < Owner(player, track))))
            {
                Rejected++;
                return;
            }
            if (target.Active)
                Stolen++;
        }
        var v = target;
        v.Active = true;
        v.Releasing = false;
        v.Detached = false;
        v.ReleaseStarted = false;
        v.PsgStarted = false;
        v.Echoing = false;
        v.Player = player;
        v.Track = track;
        v.Instrument = i;
        v.DecodedSamples = decoded;
        v.WaveSamples = wave;
        v.NoiseState = kind == 4 ? (i.Data == 1 ? 0x40 : 0x4000) : 0;
        v.NoiseOutput = 0;
        v.Key = key;
        v.MidiKey = midiKey;
        v.RhythmPan = rhythmPan;
        v.PseudoEchoVolume = track.PseudoEchoVolume;
        v.PseudoEchoLength = track.PseudoEchoLength;
        v.Velocity = velocity;
        v.Gate = duration;
        v.Priority = priority;
        v.Env = 0;
        v.Stage = 0;
        v.Position = 0;
        v.Phase = 0;
        v.Age = ages++;
        v.EnvelopeClock = 0;
        v.SweepClock = 0;
        if (kind == 0)
        {
            v.Length = sampleLength;
            v.Loop = loopStart;
            v.Looping = looping;
            v.Samples = sampleAddress;
            v.Frequency = sampleFrequency;
        }
        else
        {
            v.Frequency = 440 * Math.Pow(2, (key - 69) / 12.0);
            v.SweepRegister = SquareRegister(key, track, i);
            var (left, right) = ChannelVolume(track, velocity, rhythmPan);
            v.Goal = Math.Min(15, (left + right) / 16);
            v.SustainGoal = (v.Goal * i.Sustain + 15) >> 4;
            v.Env = i.Attack == 0 ? v.Goal : 0;
            v.Stage = i.Attack == 0 ? 1 : 0;
        }
        if (EnableTrace)
            Log(player, track, $"note key={key} velocity={velocity} gate={duration} program={track.Program} kind={kind} priority={priority}");
        int count = 0;
        for (int n = 0; n < 5; n++)
            if (Voices[n].Active)
                count++;
        MaxPcm = Math.Max(MaxPcm, count);
    }
    internal static (int Left, int Right) ChannelVolume(Track t, int velocity, int rhythmPan = 0)
    {
        int x = t.Volume * t.ExternalVolume >> 5, y = Math.Clamp(t.Pan * 2, -128, 127);
        int right = (y + 128) * x >> 8, left = (127 - y) * x >> 8;
        return (Math.Min(255, (127 - rhythmPan) * velocity * left >> 14), Math.Min(255, (128 + rhythmPan) * velocity * right >> 14));
    }
    static readonly int[] SquareTable = { -2004, -1891, -1785, -1685, -1591, -1501, -1417, -1337, -1262, -1192, -1125, -1062 };
    static int SquareRegister(int key, Track t, Instrument i)
    {
        int fixedKey = key * 256 + (t.Tune + t.Bend * t.BendRange) * 4 + (int)(t.PitchMod * 256);
        int k = fixedKey >> 8, fine = fixedKey & 255;
        int index = k <= 35 ? 0 : k - 36;
        if (k <= 35)
            fine = 0;
        if (index > 130)
        {
            index = 130;
            fine = 255;
        }
        int a = SquareTable[index % 12] >> (index / 12), b = SquareTable[(index + 1) % 12] >> ((index + 1) / 12);
        int register = 2048 + a + ((b - a) * fine >> 8);
        if ((i.Type & 8) != 0)
            register = (register + 1) & 0x7fe;
        return Math.Clamp(register, 0, 2047);
    }
    static uint Owner(Player p, Track t) => (p.Id switch
    {
        0 => 0x030020c8u,
        1 => 0x030023e8u,
        2 => 0x03007180u,
        3 => 0x030024d8u,
        4 => 0x030027a8u,
        _ => throw new ArgumentOutOfRangeException(nameof(p), "Unknown player slot")
    }) + (uint)t.Index * 0x50;
    static uint Owner(Voice v) => v.Detached ? 0 : Owner(v.Player!, v.Track!);
    public void GateTick(Player p, Track t)
    {
        foreach (var v in Voices)
            if (v.Active && v.Player == p && v.Track == t && !v.Detached && v.Gate > 0 && --v.Gate == 0)
                v.Releasing = true;
    }
    public void ReleaseTrack(Player p, Track t)
    {
        foreach (var v in Voices)
            if (v.Active && v.Player == p && v.Track == t)
            {
                v.Releasing = true;
                v.Detached = true;
            }
    }
    public void EndTie(Player p, Track t, int key)
    {
        Voice? newest = null;
        foreach (var v in Voices)
        {
            if (!v.Active || v.Detached || v.Releasing || v.Player != p || v.Track != t || v.MidiKey != key)
                continue;
            // CGB IEC-only tails no longer carry START/ENV eligibility. PCM
            // sustain-zero IEC retains its envelope bits until STOP is set.
            if (v.Echoing && (v.Instrument!.Type & 7) != 0)
                continue;
            if (newest == null || v.Age > newest.Age)
                newest = v;
        }
        if (newest != null)
            newest.Releasing = true;
    }
    // Sequencer ticks are tempo-dependent; PCM envelopes advance once per GBA frame.
    internal void Frame()
    {
        AdvanceCryDucking();
        foreach (int slot in PlayerOrder)
            if (!pausedPlayers[slot])
                players[slot]?.Frame();
        foreach (var v in Voices)
        {
            if (!v.Active)
                continue;
            var i = v.Instrument!;
            if (v.Echoing)
            {
                AdvancePseudoEcho(v);
                continue;
            }
            if ((i.Type & 7) == 0)
                AdvancePcmEnvelope(v);
        }
        if (Profile == AudioProfile.SourceEnvelope)
            PsgDriverEnvelope();
        GameFrames++;
    }
    internal static void BeginPseudoEcho(Voice v)
    {
        bool cgb = (v.Instrument!.Type & 7) != 0;
        v.Env = cgb ? (v.Goal * v.PseudoEchoVolume + 255) >> 8 : v.PseudoEchoVolume;
        v.Echoing = v.Env != 0;
        v.Active = v.Echoing;
    }
    internal static void AdvancePseudoEcho(Voice v)
    {
        int previous = v.PseudoEchoLength;
        v.PseudoEchoLength = (previous - 1) & 255;
        bool cgb = (v.Instrument!.Type & 7) != 0;
        if (cgb ? unchecked((sbyte)v.PseudoEchoLength) <= 0 : previous <= 1)
            v.Active = false;
    }
    internal static void AdvancePcmEnvelope(Voice v)
    {
        var i = v.Instrument!;
        if (v.Releasing)
        {
            v.Env = v.Env * i.Release / 256;
            if (v.Env <= v.PseudoEchoVolume)
                BeginPseudoEcho(v);
        }
        else if (v.Stage == 0)
        {
            v.Env += i.Attack;
            if (v.Env >= 255)
            {
                v.Env = 255;
                v.Stage = 1;
            }
        }
        else if (v.Stage == 1)
        {
            v.Env = v.Env * i.Decay / 256;
            if (v.Env <= i.Sustain)
            {
                v.Env = i.Sustain;
                v.Stage = 2;
                if (v.Env == 0)
                    BeginPseudoEcho(v);
            }
        }
    }
    // Driver shadow-envelope candidate. This is not an emulated APU envelope timer.
    void PsgDriverEnvelope()
    {
        driverEnvelopePhase = driverEnvelopePhase == 0 ? 14 : driverEnvelopePhase - 1;
        for (int slot = 5; slot < Voices.Length; slot++)
        {
            var v = Voices[slot];
            if (!v.Active || v.Echoing)
                continue;
            var i = v.Instrument!;
            if (!v.PsgStarted)
            {
                if (v.Releasing)
                {
                    v.Active = false;
                    continue;
                }
                v.PsgStarted = true;
                v.Stage = i.Attack > 0 ? 0 : i.Decay > 0 ? 1 : 2;
                v.Env = i.Attack > 0 ? 0 : v.Stage == 1 ? v.Goal : v.SustainGoal;
                v.EnvelopeClock = v.Stage == 0 ? i.Attack : v.Stage == 1 ? i.Decay : 7;
            }
            else if (v.Releasing && !v.ReleaseStarted)
            {
                v.ReleaseStarted = true;
                v.EnvelopeClock = i.Release;
                if (i.Release == 0)
                {
                    BeginPseudoEcho(v);
                    continue;
                }
            }
            else if (v.EnvelopeClock == 0)
                PsgDriverStep(v);
            if (!v.Active || v.Echoing)
                continue;
            v.EnvelopeClock--;
            if (driverEnvelopePhase == 0)
            {
                if (v.EnvelopeClock == 0)
                    PsgDriverStep(v);
                if (v.Active && !v.Echoing)
                    v.EnvelopeClock--;
            }
            if (v.Stage > 0 && v.Env <= 0)
                BeginPseudoEcho(v);
        }
    }

    void PsgDriverStep(Voice v)
    {
        var i = v.Instrument!;
        var (left, right) = ChannelVolume(v.Track!, v.Velocity, v.RhythmPan);
        v.Goal = Math.Min(15, (left + right) / 16);
        v.SustainGoal = (v.Goal * i.Sustain + 15) >> 4;
        if (v.Releasing)
        {
            if (--v.Env <= 0)
            {
                BeginPseudoEcho(v);
                return;
            }
            v.EnvelopeClock = i.Release;
        }
        else if (v.Stage == 0)
        {
            if (++v.Env >= v.Goal)
            {
                v.Env = v.Goal;
                v.Stage = i.Decay == 0 ? 2 : 1;
            }
            v.EnvelopeClock = v.Stage == 0 ? i.Attack : v.Stage == 1 ? i.Decay : 7;
        }
        else if (v.Stage == 1)
        {
            if (--v.Env <= v.SustainGoal)
            {
                v.Env = v.SustainGoal;
                v.Stage = 2;
            }
            v.EnvelopeClock = v.Stage == 1 ? i.Decay : 7;
        }
        else
        {
            v.Env = v.SustainGoal;
            v.EnvelopeClock = 7;
        }
    }
    // Continuous 64 Hz is the modern profile approximation to the original driver/APU.
    void PsgEnvelope()
    {
        for (int j = 5; j < Voices.Length; j++)
        {
            var v = Voices[j];
            if (!v.Active || v.Echoing)
                continue;
            var i = v.Instrument!;
            int period = v.Releasing ? i.Release : v.Stage == 0 ? i.Attack : v.Stage == 1 ? i.Decay : 7;
            if (period > 0 && ++v.EnvelopeClock < period)
                continue;
            v.EnvelopeClock = 0;
            var (left, right) = ChannelVolume(v.Track!, v.Velocity, v.RhythmPan);
            v.Goal = Math.Min(15, (left + right) / 16);
            v.SustainGoal = (v.Goal * i.Sustain + 15) >> 4;
            if (v.Releasing)
            {
                if (period == 0 || --v.Env <= 0)
                    BeginPseudoEcho(v);
            }
            else if (v.Stage == 0)
            {
                if (period == 0 || ++v.Env >= v.Goal)
                {
                    v.Env = v.Goal;
                    v.Stage = 1;
                }
            }
            else if (v.Stage == 1)
            {
                if (period == 0 || --v.Env <= v.SustainGoal)
                {
                    v.Env = v.SustainGoal;
                    v.Stage = 2;
                }
            }
            else
                v.Env = v.SustainGoal;
            if (v.Stage > 0 && v.Env == 0)
                BeginPseudoEcho(v);
        }
    }
    double ReadSample(Voice voice, int index) => voice.DecodedSamples is { } decoded ? decoded[index] : Resources.I8(voice.Samples + (uint)index);

    // Original asset-table pitch mapping differs slightly from continuous equal temperament.
    // Integer Hz is musically close but prevents cumulative phase drift in long source comparisons.
    internal double SourcePcmFrequency(Voice voice)
    {
        var track = voice.Track!;
        int fixedKey = Math.Clamp(voice.Key * 256 + (track.Tune + track.Bend * track.BendRange) * 4 + (int)(track.PitchMod * 256), 0, 178 * 256 + 255);
        int key = fixedKey >> 8, fine = fixedKey & 255;
        ulong a = sourceFrequencyTable[key % 12] >> (14 - key / 12);
        ulong b = sourceFrequencyTable[(key + 1) % 12] >> (14 - (key + 1) / 12);
        ulong scale = a + ((b - a) * (uint)fine >> 8);
        ulong frequencyWord = (ulong)(voice.Frequency * 1024);
        return (frequencyWord * scale) >> 32;
    }

    // Analytic edge smoothing (polyBLEP): remove the discontinuity's folded high-frequency energy.
    // This is a modern oscillator option, not emulation of the GBA DAC or blip filter.
    static double SmoothStepEdge(double phase, double step)
    {
        if (phase < step)
        {
            double x = phase / step;
            return 2 * x - x * x - 1;
        }
        if (phase > 1 - step)
        {
            double x = (phase - 1) / step;
            return x * x + 2 * x + 1;
        }
        return 0;
    }
    // Caller-owned interleaved stereo. The integer phase accumulator survives every block.
    public void Render(Span<float> stereo)
    {
        if (stereo.Length % 2 != 0)
            throw new ArgumentException("Stereo buffer must have an even length");
        for (int s = 0; s < stereo.Length; s += 2)
        {
            if (OutputFrames == 0)
                Frame();
            // Count elapsed host frames independently of tempo and render block size.
            for (int slot = 0; slot < players.Length; slot++)
                if (players[slot] != null && !pausedPlayers[slot] && PlayerPlaying(slot))
                    playerFrames[slot]++;
            double left = 0, right = 0;
            double pcmLeft = 0, pcmRight = 0;
            foreach (var v in Voices)
            {
                if (!v.Active)
                    continue;
                var i = v.Instrument!;
                var t = v.Track!;
                double pitch = t.Bend * t.BendRange / 64.0 + t.Tune / 64.0 + t.PitchMod;
                double value;
                if ((i.Type & 7) == 0)
                {
                    int index = (int)v.Position;
                    if (index >= v.Length)
                    {
                        if (v.Looping)
                        {
                            v.Position = v.Loop + (v.Position - v.Loop) % (v.Length - v.Loop);
                            index = (int)v.Position;
                        }
                        else
                        {
                            if (v.Player?.Id == 2 && EnableTrace)
                                Log(v.Player, v.Track!, "cry-sample-end");
                            v.Active = false;
                            continue;
                        }
                    }
                    double sample = ReadSample(v, index);
                    if (Profile >= AudioProfile.SourceLinear)
                    {
                        int next = index + 1;
                        if (next >= v.Length)
                            next = v.Looping ? v.Loop : v.DecodedSamples is not null && v.DecodedSamples.Length > v.Length ? v.Length : index;
                        sample += (ReadSample(v, next) - sample) * (v.Position - index);
                    }
                    value = sample / 128.0 * v.Env / 255.0;
                    double frequency = (i.Type & 8) != 0 ? 224.0 * Cpu / FrameCycles : Profile >= AudioProfile.SourcePitch ? SourcePcmFrequency(v) : v.Frequency * Math.Pow(2, (v.Key - 60 + pitch) / 12.0);
                    v.Position += frequency / Rate;
                }
                else if ((i.Type & 7) == 3)
                {
                    double nibbleClock = WaveClock(v.Key, t, i);
                    value = RenderWave(v, nibbleClock / Rate) * WaveGain(v.Env) * 0.35;
                }
                else if ((i.Type & 7) == 4)
                {
                    int register = NoiseRegister(v.Key, t, (int)i.Data);
                    value = RenderNoise(v, NoiseClock(register) / Rate) * v.Env / 15.0 * 0.35;
                }
                else
                {
                    double frequency = 131072.0 / (2048 - SquareRegister(v.Key, t, i));
                    if ((i.Type & 7) == 1 && ((i.Sweep >> 4) & 7) != 0 && (i.Sweep & 7) != 0)
                    {
                        v.SweepClock += 128.0 / Rate;
                        if (v.SweepClock >= ((i.Sweep >> 4) & 7))
                        {
                            v.SweepClock -= ((i.Sweep >> 4) & 7);
                            int delta = v.SweepRegister >> (i.Sweep & 7);
                            v.SweepRegister += ((i.Sweep & 8) != 0 ? -delta : delta);
                            if (v.SweepRegister >= 2048)
                            {
                                v.Active = false;
                                continue;
                            }
                        }
                        frequency = 131072.0 / (2048 - v.SweepRegister);
                    }
                    double duty = i.Data switch
                    {
                        0 => 0.125,
                        1 => 0.25,
                        2 => 0.5,
                        3 => 0.75,
                        _ => throw new InvalidDataException()
                    };
                    double square = Profile == AudioProfile.ModernDryV1 ? (v.Phase < duty ? 1 : -1) : 2 * ((v.Phase < duty ? 1 : 0) - duty);
                    if (Profile >= AudioProfile.SourceBandlimited)
                    {
                        double step = frequency / Rate;
                        square += SmoothStepEdge(v.Phase, step) - SmoothStepEdge((v.Phase - duty + 1) % 1, step);
                    }
                    value = square * v.Env / 15.0 * 0.35;
                    v.Phase = (v.Phase + frequency / Rate) % 1;
                }
                double hostGain = v.Player is { } owner ? playerGains[owner.Id] : 1;
                if (hostGain != 1)
                    value *= hostGain;
                if (Profile == AudioProfile.ModernDryV1)
                {
                    double gain = (i.Type & 7) == 0 ? v.Velocity / 127.0 * t.Volume / 127.0 * 0.22 * (t.ExternalVolume / 64.0) : 0.22;
                    left += value * gain * (64 - Math.Clamp(t.Pan, -64, 63)) / 64.0 * ((127 - v.RhythmPan) / 127.0);
                    right += value * gain * (64 + Math.Clamp(t.Pan, -64, 63)) / 64.0 * ((128 + v.RhythmPan) / 128.0);
                }
                else if ((i.Type & 7) == 0)
                {
                    var (channelLeft, channelRight) = ChannelVolume(t, v.Velocity, v.RhythmPan);
                    int envelope = (13 * v.Env) >> 4;
                    int gainLeft = (envelope * channelLeft) >> 8, gainRight = (envelope * channelRight) >> 8;
                    // Original gain-byte arithmetic; retain float summation in this host-rate profile.
                    double unscaled = v.Env == 0 ? 0 : value * 255.0 / v.Env;
                    pcmLeft += unscaled * gainLeft / 256.0 * 0.75;
                    pcmRight += unscaled * gainRight / 256.0 * 0.75;
                }
                else
                {
                    var (channelLeft, channelRight) = ChannelVolume(t, v.Velocity, v.RhythmPan);
                    bool onlyRight = channelRight / 2 >= channelLeft, onlyLeft = channelLeft / 2 >= channelRight;
                    double normalized = value / 0.35 * (45.0 / 256);
                    if (!onlyRight)
                        left += normalized;
                    if (!onlyLeft)
                        right += normalized;
                }

            }
            if (sourceDelay != null)
                (pcmLeft, pcmRight) = sourceDelay.Process(pcmLeft, pcmRight, reverbAmount);
            left += pcmLeft;
            right += pcmRight;
            if (dcBlocker != null)
                (left, right) = dcBlocker.Process(left, right);
            if (!double.IsFinite(left) || !double.IsFinite(right))
                throw new ArithmeticException("Non-finite mix");
            Peak = Math.Max(Peak, Math.Max(Math.Abs(left), Math.Abs(right)));
            if (Math.Abs(left) > 1 || Math.Abs(right) > 1)
                Clipped++;
            stereo[s] = (float)Math.Clamp(left, -1, 1);
            stereo[s + 1] = (float)Math.Clamp(right, -1, 1);
            OutputFrames++;
            envelopeClock += 64;
            if (envelopeClock >= Rate)
            {
                envelopeClock -= Rate;
                if (Profile != AudioProfile.SourceEnvelope)
                    PsgEnvelope();
            }
            clock += Cpu;
            if (clock >= Rate * FrameCycles)
            {
                clock -= Rate * FrameCycles;
                Frame();
            }
        }
    }
}
