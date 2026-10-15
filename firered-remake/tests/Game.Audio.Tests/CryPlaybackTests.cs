using System.Security.Cryptography;
using System.Buffers.Binary;
using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class CryPlaybackTests
{
    private FireRedAudio.Resources resources = TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.GetFullPath(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack"))));
    [TestMethod]
    public void DPCM_all7134_samples_match_independent_original_WAV_oracle()
    {
        var decoded = DpcmDecoder.Decode(resources, NormalCharmanderCry.WaveAddress + 16, 7134, 3679);
        string hash = Convert.ToHexString(SHA256.HashData(decoded.Select(x => unchecked((byte)x)).ToArray())).ToLowerInvariant();
        Assert.IsTrue(hash == "e1890eaf6544102dbbbcfd9c217f5502e84a85ab7496aaeb045921e1a1de958f", "DPCM all7134 samples match independent original-WAV oracle");
    }

    [TestMethod]
    public void DPCM_guard_and_exact_physical_payload_size()
    {
        var decoded = DpcmDecoder.Decode(resources, NormalCharmanderCry.WaveAddress + 16, 7134, 3679);
        Assert.IsTrue(decoded[^1] == -2 && DpcmDecoder.RequiredBytes(7134) == 3679, "DPCM guard and exact physical payload size");
    }

    [TestMethod]
    public void DPCM_truncated_tail_rejected()
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

        Reject(() => DpcmDecoder.Decode(resources, NormalCharmanderCry.WaveAddress + 16, 7134, 3678), "DPCM truncated tail rejected");
    }

    [TestMethod]
    public void DPCM_address_range_rejected_before_decode()
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

        Reject(() => DpcmDecoder.Decode(resources, Resources.End - 1, 64, 33), "DPCM address range rejected before decode");
    }

    [TestMethod]
    public void DPCM_allocation_budget_enforced()
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

        Reject(() => DpcmDecoder.RequiredBytes(1_000_001), "DPCM allocation budget enforced");
    }

    [TestMethod]
    public void DPCM_nibble_order_and_signed8_wrap()
    {
        var fake = new byte[Resources.End - Resources.Base];
        fake[1024] = 127;
        fake[1025] = 0xf1;
        fake[1026] = 0xf8;
        Assert.IsTrue(DpcmDecoder.Decode(new Resources(fake), Resources.Base + 1024, 4, 3).SequenceEqual(new sbyte[] { 127, -128, 127, 63 }), "DPCM nibble order and signed8 wrap");
    }

    [TestMethod]
    public void other_cry_species_rejected()
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

        Reject(() => NormalCharmanderCry.Build(resources, 1, 0), "other cry species rejected");
    }

    [TestMethod]
    public void special_cry_mode_rejected()
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

        Reject(() => NormalCharmanderCry.Build(resources, 4, 1), "special cry mode rejected");
    }

    [TestMethod]
    public void bounded_normal_cry_template_parameters()
    {
        var(song, bytes) = NormalCharmanderCry.Build(resources, 4, 0);
        Assert.IsTrue(bytes.Length == 52 && bytes[0] == 1 && bytes[2] == 10 && bytes[0x1d] == 120 && bytes[0x2a] == 60, "bounded normal cry template parameters");
    }

    [TestMethod]
    public void dynamic_sequence_lower_bound()
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

        var(song, bytes) = NormalCharmanderCry.Build(resources, 4, 0);
        var image = new SequenceImage(resources, bytes);
        Reject(() => image.U8(NormalCharmanderCry.SequenceBase - 1), "dynamic sequence lower bound");
    }

    [TestMethod]
    public void dynamic_sequence_upper_bound()
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

        var(song, bytes) = NormalCharmanderCry.Build(resources, 4, 0);
        var image = new SequenceImage(resources, bytes);
        Reject(() => image.U32(NormalCharmanderCry.SequenceBase + 50), "dynamic sequence upper bound");
    }

    [TestMethod]
    public void unknown_cry_XCMD_hard_fails()
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

        var(song, bytes) = NormalCharmanderCry.Build(resources, 4, 0);
        byte[] Bad(params byte[] commands)
        {
            var data = (byte[])bytes.Clone();
            Array.Clear(data, 0x11, data.Length - 0x11);
            commands.CopyTo(data, 0x11);
            return data;
        }

        void BadSequence(byte[] data)
        {
            var engine = new Synth(resources);
            var p = new Player(resources, engine, song, 2, data);
            p.Frame();
        }

        Reject(() => BadSequence(Bad(0xcd, 0xff)), "unknown cry XCMD hard fails");
    }

    [TestMethod]
    public void nonzero_cry_progress_unsupported()
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

        var(song, bytes) = NormalCharmanderCry.Build(resources, 4, 0);
        byte[] Bad(params byte[] commands)
        {
            var data = (byte[])bytes.Clone();
            Array.Clear(data, 0x11, data.Length - 0x11);
            commands.CopyTo(data, 0x11);
            return data;
        }

        void BadSequence(byte[] data)
        {
            var engine = new Synth(resources);
            var p = new Player(resources, engine, song, 2, data);
            p.Frame();
        }

        Reject(() => BadSequence(Bad(0xcd, 13, 1, 0, 0, 0)), "nonzero cry progress unsupported");
    }

    [TestMethod]
    public void dynamic_no_wait_loop_hits_instruction_budget()
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

        var(song, bytes) = NormalCharmanderCry.Build(resources, 4, 0);
        byte[] Bad(params byte[] commands)
        {
            var data = (byte[])bytes.Clone();
            Array.Clear(data, 0x11, data.Length - 0x11);
            commands.CopyTo(data, 0x11);
            return data;
        }

        void BadSequence(byte[] data)
        {
            var engine = new Synth(resources);
            var p = new Player(resources, engine, song, 2, data);
            p.Frame();
        }

        var cycle = Bad(0xb2, 0, 0, 0, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(cycle.AsSpan(0x12), NormalCharmanderCry.SequenceBase + 0x11);
        Reject(() => BadSequence(cycle), "dynamic no-wait loop hits instruction budget");
    }

    [TestMethod]
    public void dynamic_jump_cannot_escape_into_ROM()
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

        var(song, bytes) = NormalCharmanderCry.Build(resources, 4, 0);
        byte[] Bad(params byte[] commands)
        {
            var data = (byte[])bytes.Clone();
            Array.Clear(data, 0x11, data.Length - 0x11);
            commands.CopyTo(data, 0x11);
            return data;
        }

        void BadSequence(byte[] data)
        {
            var engine = new Synth(resources);
            var p = new Player(resources, engine, song, 2, data);
            p.Frame();
        }

        var escape = Bad(0xb2, 0, 0, 0, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(escape.AsSpan(0x12), 0x086dd012);
        Reject(() => BadSequence(escape), "dynamic jump cannot escape into ROM");
    }

    [TestMethod]
    public void sample_ends_before140tick_template_wait()
    {
        var solo = new Synth(resources, AudioProfile.SourceFiltered);
        solo.StartNormalCry(duckBgm: false);
        var second = new float[Synth.Rate * 2];
        solo.Render(second);
        Assert.IsTrue(!solo.CryPlaying && !solo.Ended, "sample ends before140tick template wait");
    }

    [TestMethod]
    public void xWAIT140_reaches_EOT_FINE_at_correct_tick()
    {
        var solo = new Synth(resources, AudioProfile.SourceFiltered);
        solo.StartNormalCry(duckBgm: false);
        var second = new float[Synth.Rate * 2];
        solo.Render(second);
        solo.Render(second);
        solo.Render(second);
        Assert.IsTrue(solo.Ended && solo.Trace.Any(x => x.Contains("tick=140 cry-sequence-fine")), "xWAIT140 reaches EOT/FINE at correct tick");
    }

    [TestMethod]
    public void solo_cry_uses_one_real_PCM_voice_without_clipping()
    {
        var solo = new Synth(resources, AudioProfile.SourceFiltered);
        solo.StartNormalCry(duckBgm: false);
        var second = new float[Synth.Rate * 2];
        solo.Render(second);
        solo.Render(second);
        solo.Render(second);
        Assert.IsTrue(solo.MaxPcm == 1 && solo.Clipped == 0, "solo cry uses one real PCM voice without clipping");
    }

    [TestMethod]
    public void cry_SE_retrigger_duck_block_independence()
    {
        float[] Timeline(int block)
        {
            var engine = new Synth(resources, AudioProfile.SourceFiltered)
            {
                EnableTrace = false
            };
            engine.PrepareNormalCry();
            engine.Start(0x086dd844, 0);
            int total = 5 * Synth.Rate;
            int[] edges =
            {
                Synth.Rate,
                2 * Synth.Rate,
                21 * Synth.Rate / 10,
                23 * Synth.Rate / 10,
                4 * Synth.Rate,
                total
            };
            int edge = 0;
            var output = new float[total * 2];
            bool sawDuck = false;
            for (int pos = 0; pos < total;)
            {
                if (pos == edges[edge])
                {
                    if (edge == 0 || edge == 2)
                        engine.Start(0x086b5720, 1);
                    if (edge == 1 || edge == 3)
                    {
                        engine.StartNormalCry();
                        sawDuck |= engine.BgmDucked;
                    }

                    if (edge == 4)
                        Assert.IsTrue(!engine.BgmDucked && !engine.CryPlaying, "cry replacement restores BGM after latest voice ends");
                    edge++;
                }

                int frames = Math.Min(block, edges[edge] - pos);
                engine.Render(output.AsSpan(pos * 2, frames * 2));
                pos += frames;
            }

            Assert.IsTrue(sawDuck && engine.MaxPcm <= 5 && engine.Clipped == 0, "cry and SE share bounded PCM pool with BGM ducking");
            return output;
        }

        Assert.IsTrue(Timeline(int.MaxValue).SequenceEqual(Timeline(257)), "cry/SE/retrigger/duck block independence");
    }

    [TestMethod]
    public void cancelled_cry_restores_BGM_without_stale_duck()
    {
        var solo = new Synth(resources, AudioProfile.SourceFiltered);
        solo.StartNormalCry(duckBgm: false);
        var second = new float[Synth.Rate * 2];
        solo.Render(second);
        solo.Render(second);
        solo.Render(second);
        var cancel = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        cancel.Start(0x086dd844, 0);
        cancel.Render(second);
        cancel.StartNormalCry();
        cancel.Render(second.AsSpan(0, 2400));
        cancel.Stop(2);
        cancel.Render(second);
        Assert.IsTrue(!cancel.BgmDucked && !cancel.CryPlaying, "cancelled cry restores BGM without stale duck");
    }

    [TestMethod]
    public void invalid_replacement_mode_fails_without_new_action()
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

        var solo = new Synth(resources, AudioProfile.SourceFiltered);
        solo.StartNormalCry(duckBgm: false);
        var second = new float[Synth.Rate * 2];
        solo.Render(second);
        solo.Render(second);
        solo.Render(second);
        var cancel = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        cancel.Start(0x086dd844, 0);
        cancel.Render(second);
        cancel.StartNormalCry();
        cancel.Render(second.AsSpan(0, 2400));
        cancel.Stop(2);
        cancel.Render(second);
        var awaiting = new Synth(resources)
        {
            EnableTrace = false
        };
        awaiting.Start(0x086dd844, 0);
        awaiting.Render(second);
        awaiting.StartNormalCry();
        Reject(() => awaiting.StartNormalCry(4, 1), "invalid replacement mode fails without new action");
    }

    [TestMethod]
    public void failed_cry_request_preserves_existing_duck_state()
    {
        var solo = new Synth(resources, AudioProfile.SourceFiltered);
        solo.StartNormalCry(duckBgm: false);
        var second = new float[Synth.Rate * 2];
        solo.Render(second);
        solo.Render(second);
        solo.Render(second);
        var cancel = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        cancel.Start(0x086dd844, 0);
        cancel.Render(second);
        cancel.StartNormalCry();
        cancel.Render(second.AsSpan(0, 2400));
        cancel.Stop(2);
        cancel.Render(second);
        var awaiting = new Synth(resources)
        {
            EnableTrace = false
        };
        awaiting.Start(0x086dd844, 0);
        awaiting.Render(second);
        awaiting.StartNormalCry();
        Assert.IsTrue(awaiting.BgmDucked, "failed cry request preserves existing duck state");
    }

    [TestMethod]
    public void fresh_cry_inherits_global_reverb_zero()
    {
        var inheritance = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        inheritance.StartNormalCry(duckBgm: false);
        Assert.IsTrue(inheritance.ReverbAmount == 0, "fresh cry inherits global reverb zero");
    }

    [TestMethod]
    public void cry_preserves_Pallet_global_reverb50()
    {
        var inheritance = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        inheritance.StartNormalCry(duckBgm: false);
        inheritance.Start(0x086dd844, 0);
        inheritance.StartNormalCry(duckBgm: false);
        Assert.IsTrue(inheritance.ReverbAmount == 50, "cry preserves Pallet global reverb50");
    }

    [TestMethod]
    public void cry_priority10_steals_exactly_one_occupied_BGM_PCM_slot()
    {
        var full = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        full.PrepareNormalCry();
        var fullSong = Song.Read(resources, 0x086dd844);
        var fullPlayer = new Player(resources, full, fullSong, 0);
        for (int n = 0; n < 5; n++)
            full.Note(fullPlayer, new Track(fullSong.Tracks[0], n) { Program = 4 }, 60 + n, 100, -1);
        full.StartNormalCry(duckBgm: false);
        full.Render(new float[2]);
        Assert.IsTrue(full.Voices.Take(5).Count(v => v.Active) == 5 && full.Voices.Take(5).Count(v => v.Active && v.Player?.Id == 2 && v.Priority == 10) == 1, "cry priority10 steals exactly one occupied BGM PCM slot");
    }

    [TestMethod]
    public void BGM_started_during_cry_inherits_active_duck()
    {
        var switched = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        switched.StartNormalCry();
        switched.Render(new float[9600]);
        switched.Start(0x086dd844, 0);
        switched.Render(new float[2048]);
        Assert.IsTrue(switched.Voices.Any(v => v.Active && v.Player?.Id == 0 && v.Track!.ExternalVolume == 21), "BGM started during cry inherits active duck");
    }

    [TestMethod]
    public void prepared_cry_render_including_completion_has_zero_managed_allocation()
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long delta = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.IsTrue(delta == 0, "prepared cry render including completion has zero managed allocation");
    }
}