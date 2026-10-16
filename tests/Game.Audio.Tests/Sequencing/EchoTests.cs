using System.Buffers.Binary;
using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class EchoTests : SequenceFixture
{

    [TestMethod]
    public void PCM_release_reaches_absolute_echo_floor_127_63_31_16()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var ramp = Pcm(16, 8, 128);
        var envelope = new int[4];
        for (int n = 0; n < envelope.Length; n++)
        {
            Synth.AdvancePcmEnvelope(ramp);
            envelope[n] = ramp.Env;
        }

        Assert.IsTrue(envelope.SequenceEqual(new[] { 127, 63, 31, 16 }) && ramp.Echoing && ramp.Active, "PCM release reaches absolute echo floor 127,63,31,16");
    }

    [TestMethod]
    public void echo_entry_preserves_sample_position_phase_and_full_initial_countdown()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var ramp = Pcm(16, 8, 128);
        var envelope = new int[4];
        for (int n = 0; n < envelope.Length; n++)
        {
            Synth.AdvancePcmEnvelope(ramp);
            envelope[n] = ramp.Env;
        }

        Assert.IsTrue(ramp.Position == 12.75 && ramp.Phase == 0.33 && ramp.PseudoEchoLength == 8, "echo entry preserves sample position, phase and full initial countdown");
    }

    [TestMethod]
    public void eight_frame_PCM_echo_retains_seven_subsequent_frames()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var ramp = Pcm(16, 8, 128);
        var envelope = new int[4];
        for (int n = 0; n < envelope.Length; n++)
        {
            Synth.AdvancePcmEnvelope(ramp);
            envelope[n] = ramp.Env;
        }

        for (int n = 0; n < 7; n++)
            Synth.AdvancePseudoEcho(ramp);
        Assert.IsTrue(ramp.Active && ramp.PseudoEchoLength == 1 && ramp.Env == 16, "eight-frame PCM echo retains seven subsequent frames");
    }

    [TestMethod]
    public void eight_frame_PCM_echo_ends_on_eighth_countdown()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var ramp = Pcm(16, 8, 128);
        var envelope = new int[4];
        for (int n = 0; n < envelope.Length; n++)
        {
            Synth.AdvancePcmEnvelope(ramp);
            envelope[n] = ramp.Env;
        }

        for (int n = 0; n < 7; n++)
            Synth.AdvancePseudoEcho(ramp);
        Synth.AdvancePseudoEcho(ramp);
        Assert.IsTrue(!ramp.Active && ramp.PseudoEchoLength == 0, "eight-frame PCM echo ends on eighth countdown");
    }

    [TestMethod]
    public void zero_length_PCM_echo_retains_its_entry_frame()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var zeroLength = Pcm(16, 0);
        Synth.AdvancePcmEnvelope(zeroLength);
        Assert.IsTrue(zeroLength.Active && zeroLength.Echoing && zeroLength.Env == 16, "zero-length PCM echo retains its entry frame");
    }

    [TestMethod]
    public void zero_length_PCM_echo_stops_on_next_sound_frame()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var zeroLength = Pcm(16, 0);
        Synth.AdvancePcmEnvelope(zeroLength);
        Synth.AdvancePseudoEcho(zeroLength);
        Assert.IsTrue(!zeroLength.Active, "zero-length PCM echo stops on next sound frame");
    }

    [TestMethod]
    public void zero_volume_PCM_echo_does_not_invent_a_tail()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var noEcho = Pcm(0, 8);
        Synth.AdvancePcmEnvelope(noEcho);
        Assert.IsTrue(!noEcho.Active && !noEcho.Echoing, "zero-volume PCM echo does not invent a tail");
    }

    [TestMethod]
    public void PCM_sustain_zero_decay_enters_pseudo_echo()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var decay = Pcm(12, 8);
        decay.Releasing = false;
        decay.Stage = 1;
        decay.Instrument = pcmTone with
        {
            Sustain = 0,
            Decay = 0
        };
        Synth.AdvancePcmEnvelope(decay);
        Assert.IsTrue(decay.Active && decay.Echoing && decay.Env == 12, "PCM sustain-zero decay enters pseudo-echo");
    }

    [TestMethod]
    public void CGB_echo_uses_ceil_goal15_times_volume8_256_1()
    {
        var cgbTone = new Instrument(9, 60, 0, 0, 2, 0, 1, 7, 0);
        Voice Cgb(int goal, int volume, int length) => new()
        {
            Active = true,
            Instrument = cgbTone,
            Goal = goal,
            PseudoEchoVolume = volume,
            PseudoEchoLength = length
        };
        var cgb = Cgb(15, 8, 8);
        Synth.BeginPseudoEcho(cgb);
        Assert.IsTrue(cgb.Active && cgb.Echoing && cgb.Env == 1, "CGB echo uses ceil(goal15 times volume8 /256)=1");
    }

    [TestMethod]
    public void zero_goal_CGB_echo_remains_silent_and_stops()
    {
        var cgbTone = new Instrument(9, 60, 0, 0, 2, 0, 1, 7, 0);
        Voice Cgb(int goal, int volume, int length) => new()
        {
            Active = true,
            Instrument = cgbTone,
            Goal = goal,
            PseudoEchoVolume = volume,
            PseudoEchoLength = length
        };
        var quietCgb = Cgb(0, 255, 8);
        Synth.BeginPseudoEcho(quietCgb);
        Assert.IsTrue(!quietCgb.Active, "zero-goal CGB echo remains silent and stops");
    }

    [TestMethod]
    public void CGB_echo_length128_decrements_to_signed_positive127()
    {
        var cgbTone = new Instrument(9, 60, 0, 0, 2, 0, 1, 7, 0);
        Voice Cgb(int goal, int volume, int length) => new()
        {
            Active = true,
            Instrument = cgbTone,
            Goal = goal,
            PseudoEchoVolume = volume,
            PseudoEchoLength = length
        };
        var highLength = Cgb(15, 8, 128);
        Synth.BeginPseudoEcho(highLength);
        Synth.AdvancePseudoEcho(highLength);
        Assert.IsTrue(highLength.Active && highLength.PseudoEchoLength == 127, "CGB echo length128 decrements to signed-positive127");
    }

    [TestMethod]
    public void CGB_echo_length129_decrements_to_signed_negative128_and_ends()
    {
        var cgbTone = new Instrument(9, 60, 0, 0, 2, 0, 1, 7, 0);
        Voice Cgb(int goal, int volume, int length) => new()
        {
            Active = true,
            Instrument = cgbTone,
            Goal = goal,
            PseudoEchoVolume = volume,
            PseudoEchoLength = length
        };
        var negativeLength = Cgb(15, 8, 129);
        Synth.BeginPseudoEcho(negativeLength);
        Synth.AdvancePseudoEcho(negativeLength);
        Assert.IsTrue(!negativeLength.Active, "CGB echo length129 decrements to signed-negative128 and ends");
    }

    [TestMethod]
    public void XCMD8_9_running_status_consumes_exact_bytes_and_preserves_repeated_xIECV()
    {
        byte[] commands =
        {
            0xcd,
            8,
            18,
            8,
            16,
            9,
            8,
            0xcf,
            60,
            127,
            0x81,
            0xb1
        };
        var fixture = Fixture(commands);
        var synth = new Synth(fixture)
        {
            EnableTrace = false
        };
        var player = new Player(fixture, synth, Song.Read(fixture, Resources.Base), 0);
        player.Frame();
        var track = player.Tracks[0];
        Assert.IsTrue(track.PseudoEchoVolume == 16 && track.PseudoEchoLength == 8 && track.Cursor == Resources.Base + 267, "XCMD8/9 running status consumes exact bytes and preserves repeated xIECV");
    }

    [TestMethod]
    public void note_snapshots_track_echo_parameters()
    {
        byte[] commands =
        {
            0xcd,
            8,
            18,
            8,
            16,
            9,
            8,
            0xcf,
            60,
            127,
            0x81,
            0xb1
        };
        var fixture = Fixture(commands);
        var synth = new Synth(fixture)
        {
            EnableTrace = false
        };
        var player=new Player(fixture,synth,Song.Read(fixture,Resources.Base),0);
        player.Frame();
        var first = synth.Voices[0];
        Assert.IsTrue(first.PseudoEchoVolume == 16 && first.PseudoEchoLength == 8, "note snapshots track echo parameters");
    }

    [TestMethod]
    public void overlapping_notes_keep_separate_echo_values_and_countdowns()
    {
        byte[] commands =
        {
            0xcd,
            8,
            18,
            8,
            16,
            9,
            8,
            0xcf,
            60,
            127,
            0x81,
            0xb1
        };
        var fixture = Fixture(commands);
        var synth = new Synth(fixture)
        {
            EnableTrace = false
        };
        var player = new Player(fixture, synth, Song.Read(fixture, Resources.Base), 0);
        player.Frame();
        var track = player.Tracks[0];
        var first = synth.Voices[0];
        track.PseudoEchoVolume = 10;
        track.PseudoEchoLength = 0;
        synth.Note(player, track, 64, 100, -1);
        Assert.IsTrue(first.PseudoEchoVolume == 16 && first.PseudoEchoLength == 8 && synth.Voices[1].PseudoEchoVolume == 10 && synth.Voices[1].PseudoEchoLength == 0, "overlapping notes keep separate echo values and countdowns");
    }

    [TestMethod]
    public void new_track_starts_with_zero_echo_state()
    {
        Assert.IsTrue(new Track(Resources.Base + 256, 1).PseudoEchoVolume == 0 && new Track(Resources.Base + 256, 1).PseudoEchoLength == 0, "new track starts with zero echo state");
    }

    [TestMethod]
    public void unimplemented_XCMD10_remains_an_explicit_failure()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var unknown = Fixture(new byte[] { 0xcd, 10, 1, 0xb1 });
        Reject(() => new Player(unknown, new Synth(unknown), Song.Read(unknown, Resources.Base), 0).Frame(), "unimplemented XCMD10 remains an explicit failure");
    }

    [TestMethod]
    public void XCMD_echo_values_retain_full_unsigned_bytes()
    {
        var rawByte = Fixture(new byte[] { 0xcd, 8, 255, 9, 128, 0x81, 0xb1 });
        var rawPlayer = new Player(rawByte, new Synth(rawByte), Song.Read(rawByte, Resources.Base), 0);
        rawPlayer.Frame();
        Assert.IsTrue(rawPlayer.Tracks[0].PseudoEchoVolume == 255 && rawPlayer.Tracks[0].PseudoEchoLength == 128, "XCMD echo values retain full unsigned bytes");
    }

    [TestMethod]
    public void echo_countdown_follows_sound_frames_at_tempo()
    {
        var cgbTone = new Instrument(9, 60, 0, 0, 2, 0, 1, 7, 0);
        foreach (byte tempo in new byte[]
        {
            1,
            125
        }

        )
        {
            var timingResources = Fixture(new byte[] { 0xbb, tempo, 0xb0, 0xb1 });
            var timing = new Synth(timingResources)
            {
                EnableTrace = false
            };
            timing.Start(Resources.Base, 0);
            var tail = timing.Voices[5];
            tail.Instrument = cgbTone;
            tail.Active = tail.Echoing = true;
            tail.Env = 1;
            tail.PseudoEchoLength = 8;
            for (int n = 0; n < 7; n++)
                timing.Frame();
            Assert.IsTrue(tail.Active && tail.PseudoEchoLength == 1, $"echo countdown follows sound frames at tempo{tempo * 2}");
            timing.Frame();
            Assert.IsTrue(!tail.Active && timing.GameFrames == 8, $"echo end is independent of sequence tempo{tempo * 2}");
        }
    }

    [TestMethod]
    public void EOT_accepts_PCM_decay_echo_with_ENV_but_skips_newer_CGB_IEC_only_tail()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var decay = Pcm(12, 8);
        decay.Releasing = false;
        decay.Stage = 1;
        decay.Instrument = pcmTone with
        {
            Sustain = 0,
            Decay = 0
        };
        Synth.AdvancePcmEnvelope(decay);
        var cgbTone = new Instrument(9, 60, 0, 0, 2, 0, 1, 7, 0);
        byte[] commands =
        {
            0xcd,
            8,
            18,
            8,
            16,
            9,
            8,
            0xcf,
            60,
            127,
            0x81,
            0xb1
        };
        var fixture = Fixture(commands);
        foreach (byte tempo in new byte[]
        {
            1,
            125
        }

        )
        {
            var timingResources = Fixture(new byte[] { 0xbb, tempo, 0xb0, 0xb1 });
            var timing = new Synth(timingResources)
            {
                EnableTrace = false
            };
            timing.Start(Resources.Base, 0);
            var tail = timing.Voices[5];
            tail.Instrument = cgbTone;
            tail.Active = tail.Echoing = true;
            tail.Env = 1;
            tail.PseudoEchoLength = 8;
            for (int n = 0; n < 7; n++)
                timing.Frame();
            Assert.IsTrue(tail.Active && tail.PseudoEchoLength == 1, $"echo countdown follows sound frames at tempo{tempo * 2}");
            timing.Frame();
            Assert.IsTrue(!tail.Active && timing.GameFrames == 8, $"echo end is independent of sequence tempo{tempo * 2}");
        }

        var echoTie = new Synth(fixture)
        {
            EnableTrace = false
        };
        var echoPlayer = new Player(fixture, echoTie, Song.Read(fixture, Resources.Base), 0);
        var echoTrack = echoPlayer.Tracks[0];
        echoTie.Note(echoPlayer, echoTrack, 60, 127, -1);
        var pcmTail = echoTie.Voices[0];
        pcmTail.Instrument = pcmTone with
        {
            Sustain = 0,
            Decay = 0
        };
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
        Assert.IsTrue(pcmTail.Echoing && pcmTail.Releasing && cgbTail.Echoing && !cgbTail.Releasing, "EOT accepts PCM decay echo with ENV but skips newer CGB IEC-only tail");
    }

    [TestMethod]
    public void EOT_excludes_detached_PCM_echo_and_CGB_IEC_only_tail()
    {
        var pcmTone = new Instrument(0, 60, 0, 0, 0, 255, 0, 255, 128);
        Voice Pcm(int echoVolume, int echoLength, int release = 0) => new()
        {
            Active = true,
            Releasing = true,
            Env = 255,
            Stage = 2,
            Instrument = pcmTone with
            {
                Release = release
            },
            PseudoEchoVolume = echoVolume,
            PseudoEchoLength = echoLength,
            Position = 12.75,
            Phase = 0.33
        };
        var decay = Pcm(12, 8);
        decay.Releasing = false;
        decay.Stage = 1;
        decay.Instrument = pcmTone with
        {
            Sustain = 0,
            Decay = 0
        };
        Synth.AdvancePcmEnvelope(decay);
        var cgbTone = new Instrument(9, 60, 0, 0, 2, 0, 1, 7, 0);
        byte[] commands =
        {
            0xcd,
            8,
            18,
            8,
            16,
            9,
            8,
            0xcf,
            60,
            127,
            0x81,
            0xb1
        };
        var fixture = Fixture(commands);
        foreach (byte tempo in new byte[]
        {
            1,
            125
        }

        )
        {
            var timingResources = Fixture(new byte[] { 0xbb, tempo, 0xb0, 0xb1 });
            var timing = new Synth(timingResources)
            {
                EnableTrace = false
            };
            timing.Start(Resources.Base, 0);
            var tail = timing.Voices[5];
            tail.Instrument = cgbTone;
            tail.Active = tail.Echoing = true;
            tail.Env = 1;
            tail.PseudoEchoLength = 8;
            for (int n = 0; n < 7; n++)
                timing.Frame();
            Assert.IsTrue(tail.Active && tail.PseudoEchoLength == 1, $"echo countdown follows sound frames at tempo{tempo * 2}");
            timing.Frame();
            Assert.IsTrue(!tail.Active && timing.GameFrames == 8, $"echo end is independent of sequence tempo{tempo * 2}");
        }

        var echoTie = new Synth(fixture)
        {
            EnableTrace = false
        };
        var echoPlayer = new Player(fixture, echoTie, Song.Read(fixture, Resources.Base), 0);
        var echoTrack = echoPlayer.Tracks[0];
        echoTie.Note(echoPlayer, echoTrack, 60, 127, -1);
        var pcmTail = echoTie.Voices[0];
        pcmTail.Instrument = pcmTone with
        {
            Sustain = 0,
            Decay = 0
        };
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
        pcmTail.Releasing = false;
        pcmTail.Detached = true;
        echoTie.EndTie(echoPlayer, echoTrack, 60);
        Assert.IsTrue(!pcmTail.Releasing && !cgbTail.Releasing, "EOT excludes detached PCM echo and CGB IEC-only tail");
    }
}
