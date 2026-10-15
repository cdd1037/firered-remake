using System.Buffers.Binary;
using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class SynthesizerTests
{
    private FireRedAudio.Resources real = TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.GetFullPath(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack"))));
    [TestMethod]
    public void unsupported_opcode_fails()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception e)when (e is InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        Resources Fake(byte[] sequence)
        {
            var b = new byte[Resources.End - Resources.Base];
            b[0] = 1;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), Resources.Base + 512);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8), Resources.Base + 256);
            sequence.CopyTo(b, 256);
            return new Resources(b);
        }

        void PlayBad(byte[] sequence)
        {
            var r = Fake(sequence);
            var s = new Synth(r);
            s.Start(Resources.Base, 0);
            s.Render(new float[2]);
        }

        Reject(() => PlayBad(new byte[] { 0xb9 }), "unsupported opcode fails");
    }

    [TestMethod]
    public void missing_running_status_fails()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception e)when (e is InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        Resources Fake(byte[] sequence)
        {
            var b = new byte[Resources.End - Resources.Base];
            b[0] = 1;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), Resources.Base + 512);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8), Resources.Base + 256);
            sequence.CopyTo(b, 256);
            return new Resources(b);
        }

        void PlayBad(byte[] sequence)
        {
            var r = Fake(sequence);
            var s = new Synth(r);
            s.Start(Resources.Base, 0);
            s.Render(new float[2]);
        }

        Reject(() => PlayBad(new byte[] { 0x42 }), "missing running status fails");
    }

    [TestMethod]
    public void original_depth_zero_PEND_is_an_inline_pattern_no_op()
    {
        Resources Fake(byte[] sequence)
        {
            var b = new byte[Resources.End - Resources.Base];
            b[0] = 1;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), Resources.Base + 512);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8), Resources.Base + 256);
            sequence.CopyTo(b, 256);
            return new Resources(b);
        }

        var inlinePattern = Fake(new byte[] { 0xb4, 0xb1 });
        var inlinePlayer = new Synth(inlinePattern);
        inlinePlayer.Start(Resources.Base, 0);
        inlinePlayer.Render(new float[2]);
        Assert.IsTrue(inlinePlayer.Ended, "original depth-zero PEND is an inline-pattern no-op");
    }

    [TestMethod]
    public void zero_wait_infinite_loop_bounded()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception e)when (e is InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        Resources Fake(byte[] sequence)
        {
            var b = new byte[Resources.End - Resources.Base];
            b[0] = 1;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), Resources.Base + 512);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8), Resources.Base + 256);
            sequence.CopyTo(b, 256);
            return new Resources(b);
        }

        void PlayBad(byte[] sequence)
        {
            var r = Fake(sequence);
            var s = new Synth(r);
            s.Start(Resources.Base, 0);
            s.Render(new float[2]);
        }

        var loop = new byte[5];
        loop[0] = 0xb2;
        BinaryPrimitives.WriteUInt32LittleEndian(loop.AsSpan(1), Resources.Base + 256);
        Reject(() => PlayBad(loop), "zero-wait infinite loop bounded");
    }

    [TestMethod]
    public void five_slots_occupied()
    {
        var synth = new Synth(real)
        {
            EnableTrace = false
        };
        var song = Song.Read(real, 0x086dd844);
        var p = new Player(real, synth, song, 0);
        for (int i = 0; i < 5; i++)
            synth.Note(p, new Track(song.Tracks[0], i) { Program = 4 }, 60 + i, 100, -1);
        Assert.IsTrue(synth.Voices.Take(5).All(v => v.Active), "five slots occupied");
    }

    [TestMethod]
    public void equal_priority_steals_greatest_track_address()
    {
        var synth = new Synth(real)
        {
            EnableTrace = false
        };
        var song = Song.Read(real, 0x086dd844);
        var p = new Player(real, synth, song, 0);
        for (int i = 0; i < 5; i++)
            synth.Note(p, new Track(song.Tracks[0], i) { Program = 4 }, 60 + i, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 4 }, 72, 100, -1);
        Assert.IsTrue(synth.Voices[4].Key == 72, "equal priority steals greatest track address");
    }

    [TestMethod]
    public void later_equal_priority_track_rejected()
    {
        var synth = new Synth(real)
        {
            EnableTrace = false
        };
        var song = Song.Read(real, 0x086dd844);
        var p = new Player(real, synth, song, 0);
        for (int i = 0; i < 5; i++)
            synth.Note(p, new Track(song.Tracks[0], i) { Program = 4 }, 60 + i, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 4 }, 72, 100, -1);
        long rejected = synth.Rejected;
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 74, 100, -1);
        Assert.IsTrue(synth.Rejected == rejected + 1, "later equal-priority track rejected");
    }

    [TestMethod]
    public void release_tail_replaceable_despite_higher_priority()
    {
        var synth = new Synth(real)
        {
            EnableTrace = false
        };
        var song = Song.Read(real, 0x086dd844);
        var p = new Player(real, synth, song, 0);
        for (int i = 0; i < 5; i++)
            synth.Note(p, new Track(song.Tracks[0], i) { Program = 4 }, 60 + i, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 4 }, 72, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 74, 100, -1);
        synth.Voices[1].Releasing = true;
        synth.Voices[1].Priority = 200;
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 76, 100, -1);
        Assert.IsTrue(synth.Voices[1].Key == 76, "release tail replaceable despite higher priority");
    }

    [TestMethod]
    public void SE_steals_square1_only_PCM_and_square2_intact()
    {
        var synth = new Synth(real)
        {
            EnableTrace = false
        };
        var song = Song.Read(real, 0x086dd844);
        var p = new Player(real, synth, song, 0);
        for (int i = 0; i < 5; i++)
            synth.Note(p, new Track(song.Tracks[0], i) { Program = 4 }, 60 + i, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 4 }, 72, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 74, 100, -1);
        synth.Voices[1].Releasing = true;
        synth.Voices[1].Priority = 200;
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 76, 100, -1);
        var se = new Player(real, synth, Song.Read(real, 0x086b5720), 1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 80 }, 70, 100, 12);
        synth.Note(p, new Track(song.Tracks[0], 3) { Program = 81 }, 70, 100, 12);
        var pcmBeforeSelect = synth.Voices.Take(5).Select(v => (v.Key, v.Age, v.Player)).ToArray();
        long square2Age = synth.Voices[6].Age;
        synth.Note(se, new Track(se.Song.Tracks[0], 0) { Program = 87, Bend = 13 }, 94, 68, 3);
        Assert.IsTrue(synth.Voices[5].Player == se && synth.Voices[6].Age == square2Age && synth.Voices[6].Player == p && pcmBeforeSelect.SequenceEqual(synth.Voices.Take(5).Select(v => (v.Key, v.Age, v.Player))), "SE steals square1 only; PCM and square2 intact");
    }

    [TestMethod]
    public void BGM_cannot_steal_active_SE_or_alter_PCM_voices()
    {
        var synth = new Synth(real)
        {
            EnableTrace = false
        };
        var song = Song.Read(real, 0x086dd844);
        var p = new Player(real, synth, song, 0);
        for (int i = 0; i < 5; i++)
            synth.Note(p, new Track(song.Tracks[0], i) { Program = 4 }, 60 + i, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 4 }, 72, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 74, 100, -1);
        synth.Voices[1].Releasing = true;
        synth.Voices[1].Priority = 200;
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 76, 100, -1);
        var se = new Player(real, synth, Song.Read(real, 0x086b5720), 1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 80 }, 70, 100, 12);
        synth.Note(p, new Track(song.Tracks[0], 3) { Program = 81 }, 70, 100, 12);
        synth.Note(se, new Track(se.Song.Tracks[0], 0) { Program = 87, Bend = 13 }, 94, 68, 3);
        var before = synth.Voices.Take(5).Select(v => v.Key).ToArray();
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 80 }, 72, 100, 12);
        Assert.IsTrue(synth.Voices[5].Player == se && before.SequenceEqual(synth.Voices.Take(5).Select(v => v.Key)), "BGM cannot steal active SE or alter PCM voices");
    }

    [TestMethod]
    public void sustained_PSG_follows_live_VOL()
    {
        var volumeSynth = new Synth(real)
        {
            EnableTrace = false
        };
        var volumePlayer = new Player(real, volumeSynth, Song.Read(real, 0x086b5720), 1);
        var volumeTrack = new Track(volumePlayer.Song.Tracks[0], 0)
        {
            Program = 88,
            Volume = 80
        };
        volumeSynth.Note(volumePlayer, volumeTrack, 94, 127, -1);
        var scratch = new float[48000];
        volumeSynth.Render(scratch);
        int loud = volumeSynth.Voices[5].Env;
        volumeTrack.Volume = 25;
        volumeSynth.Render(scratch);
        Assert.IsTrue(volumeSynth.Voices[5].Env < loud, "sustained PSG follows live VOL");
    }

    [TestMethod]
    public void FINE_detaches_while_releasing()
    {
        var volumeSynth = new Synth(real)
        {
            EnableTrace = false
        };
        var volumePlayer = new Player(real, volumeSynth, Song.Read(real, 0x086b5720), 1);
        var volumeTrack = new Track(volumePlayer.Song.Tracks[0], 0)
        {
            Program = 88,
            Volume = 80
        };
        volumeSynth.Note(volumePlayer, volumeTrack, 94, 127, -1);
        var scratch = new float[48000];
        volumeSynth.Render(scratch);
        volumeTrack.Volume = 25;
        volumeSynth.Render(scratch);
        volumeSynth.ReleaseTrack(volumePlayer, volumeTrack);
        Assert.IsTrue(volumeSynth.Voices[5].Detached && volumeSynth.Voices[5].Releasing, "FINE detaches while releasing");
    }

    [TestMethod]
    public void same_owner_tie_steals_highest_slot()
    {
        var synth = new Synth(real)
        {
            EnableTrace = false
        };
        var song = Song.Read(real, 0x086dd844);
        var p = new Player(real, synth, song, 0);
        for (int i = 0; i < 5; i++)
            synth.Note(p, new Track(song.Tracks[0], i) { Program = 4 }, 60 + i, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 4 }, 72, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 74, 100, -1);
        synth.Voices[1].Releasing = true;
        synth.Voices[1].Priority = 200;
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 76, 100, -1);
        var se = new Player(real, synth, Song.Read(real, 0x086b5720), 1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 80 }, 70, 100, 12);
        synth.Note(p, new Track(song.Tracks[0], 3) { Program = 81 }, 70, 100, 12);
        synth.Note(se, new Track(se.Song.Tracks[0], 0) { Program = 87, Bend = 13 }, 94, 68, 3);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 80 }, 72, 100, 12);
        var sameOwner = new Synth(real)
        {
            EnableTrace = false
        };
        var samePlayer = new Player(real, sameOwner, song, 0);
        var sameTrack = new Track(song.Tracks[0], 0)
        {
            Program = 4
        };
        for (int n = 0; n < 5; n++)
            sameOwner.Note(samePlayer, sameTrack, 60 + n, 100, -1);
        sameOwner.Note(samePlayer, sameTrack, 75, 100, -1);
        Assert.IsTrue(sameOwner.Voices[4].Key == 75, "same-owner tie steals highest slot");
    }

    [TestMethod]
    public void free_slot_precedes_release_candidate()
    {
        var synth = new Synth(real)
        {
            EnableTrace = false
        };
        var song = Song.Read(real, 0x086dd844);
        var p = new Player(real, synth, song, 0);
        for (int i = 0; i < 5; i++)
            synth.Note(p, new Track(song.Tracks[0], i) { Program = 4 }, 60 + i, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 4 }, 72, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 74, 100, -1);
        synth.Voices[1].Releasing = true;
        synth.Voices[1].Priority = 200;
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 76, 100, -1);
        var se = new Player(real, synth, Song.Read(real, 0x086b5720), 1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 80 }, 70, 100, 12);
        synth.Note(p, new Track(song.Tracks[0], 3) { Program = 81 }, 70, 100, 12);
        synth.Note(se, new Track(se.Song.Tracks[0], 0) { Program = 87, Bend = 13 }, 94, 68, 3);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 80 }, 72, 100, 12);
        var sameOwner = new Synth(real)
        {
            EnableTrace = false
        };
        var samePlayer = new Player(real, sameOwner, song, 0);
        var sameTrack = new Track(song.Tracks[0], 0)
        {
            Program = 4
        };
        for (int n = 0; n < 5; n++)
            sameOwner.Note(samePlayer, sameTrack, 60 + n, 100, -1);
        sameOwner.Note(samePlayer, sameTrack, 75, 100, -1);
        sameOwner.Voices[0].Releasing = true;
        sameOwner.Voices[3].Active = false;
        sameOwner.Note(samePlayer, sameTrack, 76, 100, -1);
        Assert.IsTrue(sameOwner.Voices[3].Key == 76 && sameOwner.Voices[0].Releasing, "free slot precedes release candidate");
    }

    [TestMethod]
    public void multiple_releases_rank_lowest_priority_first()
    {
        var synth = new Synth(real)
        {
            EnableTrace = false
        };
        var song = Song.Read(real, 0x086dd844);
        var p = new Player(real, synth, song, 0);
        for (int i = 0; i < 5; i++)
            synth.Note(p, new Track(song.Tracks[0], i) { Program = 4 }, 60 + i, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 4 }, 72, 100, -1);
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 74, 100, -1);
        synth.Voices[1].Releasing = true;
        synth.Voices[1].Priority = 200;
        synth.Note(p, new Track(song.Tracks[0], 9) { Program = 4 }, 76, 100, -1);
        var se = new Player(real, synth, Song.Read(real, 0x086b5720), 1);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 80 }, 70, 100, 12);
        synth.Note(p, new Track(song.Tracks[0], 3) { Program = 81 }, 70, 100, 12);
        synth.Note(se, new Track(se.Song.Tracks[0], 0) { Program = 87, Bend = 13 }, 94, 68, 3);
        synth.Note(p, new Track(song.Tracks[0], 0) { Program = 80 }, 72, 100, 12);
        var sameOwner = new Synth(real)
        {
            EnableTrace = false
        };
        var samePlayer = new Player(real, sameOwner, song, 0);
        var sameTrack = new Track(song.Tracks[0], 0)
        {
            Program = 4
        };
        for (int n = 0; n < 5; n++)
            sameOwner.Note(samePlayer, sameTrack, 60 + n, 100, -1);
        sameOwner.Note(samePlayer, sameTrack, 75, 100, -1);
        sameOwner.Voices[0].Releasing = true;
        sameOwner.Voices[3].Active = false;
        sameOwner.Note(samePlayer, sameTrack, 76, 100, -1);
        sameOwner.Voices[1].Releasing = true;
        sameOwner.Voices[1].Priority = 2;
        sameOwner.Voices[0].Priority = 3;
        sameOwner.Note(samePlayer, sameTrack, 77, 100, -1);
        Assert.IsTrue(sameOwner.Voices[1].Key == 77, "multiple releases rank lowest priority first");
    }

    [TestMethod]
    public void cross_loop_SFX_stop_restart_block_independence()
    {
        float[] Timeline(int block)
        {
            var engine = new Synth(real)
            {
                EnableTrace = true
            };
            engine.Start(0x086dd844, 0);
            int total = 55 * Synth.Rate;
            var output = new float[total * 2];
            int[] edges =
            {
                2 * Synth.Rate,
                3 * Synth.Rate,
                6 * Synth.Rate,
                7 * Synth.Rate,
                total
            };
            int edge = 0;
            for (int pos = 0; pos < total;)
            {
                if (pos == edges[edge])
                {
                    if (edge < 2)
                        engine.Start(0x086b5720, 1);
                    else if (edge == 2)
                        engine.Stop(0);
                    else if (edge == 3)
                        engine.Start(0x086dd844, 0);
                    edge++;
                }

                int frames = Math.Min(block, edges[edge] - pos);
                engine.Render(output.AsSpan(pos * 2, frames * 2));
                pos += frames;
            }

            Assert.IsTrue(engine.Trace.Any(x => x.Contains("tick=1536 goto")), "timeline crosses restarted loop");
            return output;
        }

        Assert.IsTrue(Timeline(int.MaxValue).SequenceEqual(Timeline(257)), "cross-loop/SFX/stop/restart block independence");
    }
}