using System.Buffers.Binary;
namespace FireRedAudio;

public static class MusicGapVerification
{
    public static int Run(Resources real)
    {
        int passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            Console.WriteLine("PASS " + name);
            passed++;
        }
        void Reject(Action action, string name)
        {
            try { action(); }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException)
            {
                Check(true, name);
                return;
            }
            throw new Exception("FAIL " + name);
        }

        // Independent state vectors from the audited original PCM/CGB branches.
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true, Releasing = true, Env = 255, Stage = 2,
            Instrument = pcmTone with { Release = release },
            PseudoEchoVolume = echoVolume, PseudoEchoLength = echoLength,
            Position = 12.75, Phase = 0.33
        };
        var ramp = Pcm(16, 8, 128);
        var envelope = new int[4];
        for (int n = 0; n < envelope.Length; n++)
        {
            Synth.AdvancePcmEnvelope(ramp);
            envelope[n] = ramp.Env;
        }
        Check(envelope.SequenceEqual(new[] { 127, 63, 31, 16 }) && ramp.Echoing && ramp.Active,
            "PCM release reaches absolute echo floor 127,63,31,16");
        Check(ramp.Position == 12.75 && ramp.Phase == 0.33 && ramp.PseudoEchoLength == 8,
            "echo entry preserves sample position, phase and full initial countdown");
        for (int n = 0; n < 7; n++) Synth.AdvancePseudoEcho(ramp);
        Check(ramp.Active && ramp.PseudoEchoLength == 1 && ramp.Env == 16,
            "eight-frame PCM echo retains seven subsequent frames");
        Synth.AdvancePseudoEcho(ramp);
        Check(!ramp.Active && ramp.PseudoEchoLength == 0, "eight-frame PCM echo ends on eighth countdown");
        var zeroLength = Pcm(16, 0);
        Synth.AdvancePcmEnvelope(zeroLength);
        Check(zeroLength.Active && zeroLength.Echoing && zeroLength.Env == 16,
            "zero-length PCM echo retains its entry frame");
        Synth.AdvancePseudoEcho(zeroLength);
        Check(!zeroLength.Active, "zero-length PCM echo stops on next sound frame");
        var noEcho = Pcm(0, 8);
        Synth.AdvancePcmEnvelope(noEcho);
        Check(!noEcho.Active && !noEcho.Echoing, "zero-volume PCM echo does not invent a tail");
        var decay = Pcm(12, 8);
        decay.Releasing = false;
        decay.Stage = 1;
        decay.Instrument = pcmTone with { Sustain = 0, Decay = 0 };
        Synth.AdvancePcmEnvelope(decay);
        Check(decay.Active && decay.Echoing && decay.Env == 12, "PCM sustain-zero decay enters pseudo-echo");

        var cgbTone = new Instrument(9, 60, 0, 0, 2, 0, 1, 7, 0);
        Voice Cgb(int goal, int volume, int length) => new()
        {
            Active = true, Instrument = cgbTone, Goal = goal,
            PseudoEchoVolume = volume, PseudoEchoLength = length
        };
        var cgb = Cgb(15, 8, 8);
        Synth.BeginPseudoEcho(cgb);
        Check(cgb.Active && cgb.Echoing && cgb.Env == 1, "CGB echo uses ceil(goal15 times volume8 /256)=1");
        var quietCgb = Cgb(0, 255, 8);
        Synth.BeginPseudoEcho(quietCgb);
        Check(!quietCgb.Active, "zero-goal CGB echo remains silent and stops");
        var highLength = Cgb(15, 8, 128);
        Synth.BeginPseudoEcho(highLength);
        Synth.AdvancePseudoEcho(highLength);
        Check(highLength.Active && highLength.PseudoEchoLength == 127, "CGB echo length128 decrements to signed-positive127");
        var negativeLength = Cgb(15, 8, 129);
        Synth.BeginPseudoEcho(negativeLength);
        Synth.AdvancePseudoEcho(negativeLength);
        Check(!negativeLength.Active, "CGB echo length129 decrements to signed-negative128 and ends");

        byte[] commands = { 0xcd, 8, 18, 8, 16, 9, 8, 0xcf, 60, 127, 0x81, 0xb1 };
        var fixture = Fixture(commands);
        var synth = new Synth(fixture) { EnableTrace = false };
        var player = new Player(fixture, synth, Song.Read(fixture, Resources.Base), 0);
        player.Frame();
        var track = player.Tracks[0];
        var first = synth.Voices[0];
        Check(track.PseudoEchoVolume == 16 && track.PseudoEchoLength == 8 && track.Cursor == Resources.Base + 267,
            "XCMD8/9 running status consumes exact bytes and preserves repeated xIECV");
        Check(first.PseudoEchoVolume == 16 && first.PseudoEchoLength == 8, "note snapshots track echo parameters");
        track.PseudoEchoVolume = 10;
        track.PseudoEchoLength = 0;
        synth.Note(player, track, 64, 100, -1);
        Check(first.PseudoEchoVolume == 16 && first.PseudoEchoLength == 8 && synth.Voices[1].PseudoEchoVolume == 10 && synth.Voices[1].PseudoEchoLength == 0,
            "overlapping notes keep separate echo values and countdowns");
        Check(new Track(Resources.Base + 256, 1).PseudoEchoVolume == 0 && new Track(Resources.Base + 256, 1).PseudoEchoLength == 0,
            "new track starts with zero echo state");
        var unknown = Fixture(new byte[] { 0xcd, 10, 1, 0xb1 });
        Reject(() => new Player(unknown, new Synth(unknown), Song.Read(unknown, Resources.Base), 0).Frame(),
            "unimplemented XCMD10 remains an explicit failure");
        var rawByte = Fixture(new byte[] { 0xcd, 8, 255, 9, 128, 0x81, 0xb1 });
        var rawPlayer = new Player(rawByte, new Synth(rawByte), Song.Read(rawByte, Resources.Base), 0);
        rawPlayer.Frame();
        Check(rawPlayer.Tracks[0].PseudoEchoVolume == 255 && rawPlayer.Tracks[0].PseudoEchoLength == 128,
            "XCMD echo values retain full unsigned bytes");

        foreach (byte tempo in new byte[] { 1, 125 })
        {
            var timingResources = Fixture(new byte[] { 0xbb, tempo, 0xb0, 0xb1 });
            var timing = new Synth(timingResources) { EnableTrace = false };
            timing.Start(Resources.Base, 0);
            var tail = timing.Voices[5];
            tail.Instrument = cgbTone;
            tail.Active = tail.Echoing = true;
            tail.Env = 1;
            tail.PseudoEchoLength = 8;
            for (int n = 0; n < 7; n++) timing.Frame();
            Check(tail.Active && tail.PseudoEchoLength == 1, $"echo countdown follows sound frames at tempo{tempo * 2}");
            timing.Frame();
            Check(!tail.Active && timing.GameFrames == 8, $"echo end is independent of sequence tempo{tempo * 2}");
        }

        // Exact original drum mappings, including raw MIDI identity and pan.
        var drums = new Synth(real) { EnableTrace = false };
        var drumPlayer = new Player(real, drums, Song.Read(real, 0x086e8810), 0);
        var drumTrack = new Track(drumPlayer.Song.Tracks[0], 0) { Program = 0, Key = 36, KeyShift = 12 };
        drums.Note(drumPlayer, drumTrack, 48, 100, -1);
        var kick = drums.Voices[0];
        Check(kick.MidiKey == 36 && kick.Key == 76 && kick.RhythmPan == 0 && kick.Instrument!.Type == 8 && kick.Instrument.Data == real.U32(0x0848a284),
            "rhythm36 selects original kick, sounding root64 plus transpose12, MIDI key36");
        drums.EndTie(drumPlayer, drumTrack, 76);
        Check(!kick.Releasing, "EOT does not match a drum's sounding pitch");
        drumTrack.KeyShift = -12;
        drums.EndTie(drumPlayer, drumTrack, 36);
        Check(kick.Releasing, "EOT matches original drum MIDI key despite later transpose changes");
        drumTrack.Key = 42;
        drums.Note(drumPlayer, drumTrack, 30, 100, -1);
        var snare = drums.Voices[1];
        Check(snare.MidiKey == 42 && snare.Key == 52 && snare.RhythmPan == 32 && snare.Instrument!.Data == real.U32(0x0848a2cc),
            "rhythm42 applies source child root and positive pan");
        drumTrack.Key = 57;
        drums.Note(drumPlayer, drumTrack, 45, 100, -1);
        Check(drums.Voices[2].RhythmPan == -70, "rhythm57 sign-extends child pan to minus70");
        var panTrack = new Track(Resources.Base, 0) { Volume = 120 };
        Check(Synth.ChannelVolume(panTrack, 127, 32) == (87, 148), "rhythm pan combines with track gain using original integer factors");

        var tieResources = Fixture(new byte[] { 0xbc, 12, 0xcf, 36, 127, 0x81, 0xce, 36, 0x81, 0xb1 }, 0x80);
        var tieSynth = new Synth(tieResources) { EnableTrace = false };
        var tiePlayer = new Player(tieResources, tieSynth, Song.Read(tieResources, Resources.Base), 0);
        tiePlayer.Frame();
        Check(tieSynth.Voices[0].MidiKey == 36 && tieSynth.Voices[0].Key == 76 && !tieSynth.Voices[0].Releasing,
            "sequence rhythm TIE retains distinct MIDI and sounding keys");
        tiePlayer.Frame();
        Check(tieSynth.Voices[0].Releasing, "sequence EOT36 releases transposed rhythm TIE");

        var rememberedResources = Fixture(new byte[] { 0xcf, 60, 127, 0x81, 0xce, 61, 0x81, 0xcf, 0x81, 0xb1 });
        var rememberedSynth = new Synth(rememberedResources) { EnableTrace = false };
        var rememberedPlayer = new Player(rememberedResources, rememberedSynth, Song.Read(rememberedResources, Resources.Base), 0);
        for (int frame = 0; frame < 3; frame++) rememberedPlayer.Frame();
        Check(rememberedPlayer.Tracks[0].Key == 61 && rememberedSynth.Voices[1].MidiKey == 61,
            "explicit EOT61 updates the key inherited by following omitted-key TIE");
        var duplicateResources = Fixture(new byte[] { 0xcf, 60, 127, 0xcf, 60, 127, 0x81, 0xce, 60, 0x81, 0xb1 });
        var duplicateSynth = new Synth(duplicateResources) { EnableTrace = false };
        var duplicatePlayer = new Player(duplicateResources, duplicateSynth, Song.Read(duplicateResources, Resources.Base), 0);
        duplicatePlayer.Frame();
        duplicatePlayer.Frame();
        Check(duplicateSynth.Voices[1].Releasing && !duplicateSynth.Voices[0].Releasing,
            "EOT releases only newest eligible same-key note");
        duplicateSynth.EndTie(duplicatePlayer, duplicatePlayer.Tracks[0], 60);
        Check(duplicateSynth.Voices[0].Releasing, "second EOT skips releasing newest note and releases next eligible note");

        // PCM decay-to-echo retains ENV eligibility; CGB echo clears ENV.
        var echoTie = new Synth(fixture) { EnableTrace = false };
        var echoPlayer = new Player(fixture, echoTie, Song.Read(fixture, Resources.Base), 0);
        var echoTrack = echoPlayer.Tracks[0];
        echoTie.Note(echoPlayer, echoTrack, 60, 127, -1);
        var pcmTail = echoTie.Voices[0];
        pcmTail.Instrument = pcmTone with { Sustain = 0, Decay = 0 };
        pcmTail.Env = 255;
        pcmTail.Stage = 1;
        pcmTail.PseudoEchoVolume = 12;
        pcmTail.PseudoEchoLength = 8;
        Synth.AdvancePcmEnvelope(pcmTail);
        var cgbTail = echoTie.Voices[5];
        cgbTail.Active = true;
        cgbTail.Player = echoPlayer;
        cgbTail.Track = echoTrack;
        cgbTail.MidiKey = 60;
        cgbTail.Age = pcmTail.Age + 1;
        cgbTail.Instrument = cgbTone;
        cgbTail.Goal = 15;
        cgbTail.PseudoEchoVolume = 8;
        cgbTail.PseudoEchoLength = 8;
        Synth.BeginPseudoEcho(cgbTail);
        echoTie.EndTie(echoPlayer, echoTrack, 60);
        Check(pcmTail.Echoing && pcmTail.Releasing && cgbTail.Echoing && !cgbTail.Releasing,
            "EOT accepts PCM decay echo with ENV but skips newer CGB IEC-only tail");
        pcmTail.Releasing = false;
        pcmTail.Detached = true;
        echoTie.EndTie(echoPlayer, echoTrack, 60);
        Check(!pcmTail.Releasing && !cgbTail.Releasing, "EOT excludes detached PCM echo and CGB IEC-only tail");

        var overflowCommands = new byte[] { 0xb3, 0, 0, 0, 0 };
        BinaryPrimitives.WriteUInt32LittleEndian(overflowCommands.AsSpan(1), Resources.Base + 256);
        var overflow = Fixture(overflowCommands);
        Reject(() => new Player(overflow, new Synth(overflow), Song.Read(overflow, Resources.Base), 0).Frame(),
            "actual pattern-stack overflow still fails explicitly");
        var escapedPattern = new byte[] { 0xb3, 0, 0, 0, 0 };
        BinaryPrimitives.WriteUInt32LittleEndian(escapedPattern.AsSpan(1), Resources.End + 4);
        var patternBounds = Fixture(escapedPattern);
        Reject(() => new Player(patternBounds, new Synth(patternBounds), Song.Read(patternBounds, Resources.Base), 0).Frame(),
            "pattern call outside resource bounds still fails explicitly");

        var splitResources = Fixture(new byte[] { 0xbc, 12, 0xcf, 36, 127, 0x81, 0xb1 }, 0x40);
        var splitSynth = new Synth(splitResources) { EnableTrace = false };
        new Player(splitResources, splitSynth, Song.Read(splitResources, Resources.Base), 0).Frame();
        Check(splitSynth.Voices[0].Key == 48 && splitSynth.Voices[0].RhythmPan == 0,
            "keysplit-only tone retains sequence pitch and ignores child rhythm pan");
        foreach (int type in new[] { 0x13, 0x14, 0x1b, 0x1c })
        {
            var unsupported = Fixture(new byte[] { 0xcf, 60, 127, 0x81, 0xb1 }, leafType: type);
            Reject(() => new Player(unsupported, new Synth(unsupported), Song.Read(unsupported, Resources.Base), 0).Frame(),
                $"PSG wave/noise flags0x{type:X2} remain explicitly unsupported");
        }
        var nested = Fixture(new byte[] { 0xcf, 36, 127, 0x81, 0xb1 }, 0x80, 0x40);
        Reject(() => new Player(nested, new Synth(nested), Song.Read(nested, Resources.Base), 0).Frame(),
            "nested rhythm child remains an explicit failure");
        var escaped = Fixture(new byte[] { 0xcf, 36, 127, 0x81, 0xb1 }, 0x80, invalidChildAddress: true);
        Reject(() => new Player(escaped, new Synth(escaped), Song.Read(escaped, Resources.Base), 0).Frame(),
            "rhythm child address is bounds checked");
        return passed;
    }

    static Resources Fixture(byte[] sequence, int parentType = 0, int leafType = 0, bool invalidChildAddress = false)
    {
        var bytes = new byte[Resources.End - Resources.Base];
        void Word(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        void Tone(int offset, int type, int key, int pan)
        {
            bytes[offset] = (byte)type;
            bytes[offset + 1] = (byte)key;
            bytes[offset + 3] = (byte)pan;
            Word(offset + 4, (type & 7) == 0 ? Resources.Base + 4096 : 2);
            bytes[offset + 8] = 255;
            bytes[offset + 10] = 255;
        }
        bytes[0] = 1;
        Word(4, Resources.Base + 512);
        Word(8, Resources.Base + 256);
        sequence.CopyTo(bytes, 256);
        Tone(512, leafType, 60, 0);
        if (parentType != 0)
        {
            bytes[512] = (byte)parentType;
            Word(516, invalidChildAddress ? Resources.End - 4 : Resources.Base + 1024);
            Word(520, Resources.Base + 3072);
            for (int n = 0; n < 128; n++) Tone(1024 + n * 12, leafType, 64, 0xd0);
        }
        Word(4096, 0x40000000); // looped uncompressed PCM
        Word(4100, 10512 * 1024);
        Word(4104, 0);
        Word(4108, 16);
        Array.Fill(bytes, (byte)32, 4112, 16);
        return new Resources(bytes);
    }
}
