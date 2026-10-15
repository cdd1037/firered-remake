using System.Buffers.Binary;
using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class PanningTests
{
    private FireRedAudio.Resources real = TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.GetFullPath(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack"))));
    internal static Resources Fixture()
    {
        var b = new byte[Resources.End - Resources.Base];
        void W(int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o), v);
        void Header(int o, params int[] tracks)
        {
            b[o] = (byte)tracks.Length;
            W(o + 4, Resources.Base + 2048);
            for (int n = 0; n < tracks.Length; n++)
                W(o + 8 + 4 * n, Resources.Base + (uint)tracks[n]);
        }

        Header(0, 1024, 1024, 1024, 1024);
        Header(64, 1024);
        Header(128, 1152);
        Header(192, 1280);
        Header(256, 1408);
        new byte[]
        {
            0xbd,
            0,
            0xcf,
            60,
            127,
            0xb0,
            0xb2,
            0,
            0,
            0,
            0
        }.CopyTo(b, 1024);
        W(1031, Resources.Base + 1029);
        new byte[]
        {
            0xbd,
            1,
            0xcf,
            60,
            127,
            0xb0,
            0xb2,
            0,
            0,
            0,
            0
        }.CopyTo(b, 1152);
        W(1159, Resources.Base + 1157);
        new byte[]
        {
            0xbf,
            96,
            0xcf,
            60,
            127,
            0x82,
            0xbf,
            32,
            0xb0,
            0xb2,
            0,
            0,
            0,
            0
        }.CopyTo(b, 1280);
        W(1290, Resources.Base + 1288);
        new byte[]
        {
            0xcf,
            60,
            127,
            0x81,
            0xb1
        }.CopyTo(b, 1408);
        b[2049] = 60;
        W(2052, Resources.Base + 4096);
        b[2056] = b[2057] = b[2058] = 255;
        b[2059] = 224;
        new byte[]
        {
            1,
            60,
            0,
            8,
            2,
            0,
            0,
            0,
            0,
            0,
            15,
            4
        }.CopyTo(b, 2060);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(4098), 0x4000);
        W(4100, 16384 * 1024);
        W(4108, 64);
        for (int n = 0; n < 64; n++)
            b[4112 + n] = unchecked((byte)(sbyte)(n < 32 ? 80 : -80));
        for (int n = 0; n < 12; n++)
            W((int)(0x08489abc - Resources.Base) + 4 * n, 0x80000000);
        return new Resources(b);
    }

    [TestMethod]
    public void absent_player_is_not_created()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        var absent = New();
        Assert.IsTrue(absent.SetPlayerPan(1, 63) == 0 && !absent.HasPlayer(1), "absent player is not created");
    }

    [TestMethod]
    public void invalid_player_rejected()
    {
        void Reject(Action action, string message)
        {
            try
            {
                action();
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            throw new InvalidOperationException("FAIL panX: " + message);
        }

        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        var absent = New();
        Reject(() => absent.SetPlayerPan(-1, 0), "invalid player rejected");
    }

    [TestMethod]
    public void upper_invalid_player_rejected()
    {
        void Reject(Action action, string message)
        {
            try
            {
                action();
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            throw new InvalidOperationException("FAIL panX: " + message);
        }

        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        var absent = New();
        Reject(() => absent.SetPlayerPan(5, 0), "upper invalid player rejected");
    }

    [TestMethod]
    public void pan_below_signed_byte_rejected()
    {
        void Reject(Action action, string message)
        {
            try
            {
                action();
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            throw new InvalidOperationException("FAIL panX: " + message);
        }

        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        var absent = New();
        Reject(() => absent.SetPlayerPan(1, -129), "pan below signed byte rejected");
    }

    [TestMethod]
    public void pan_above_signed_byte_rejected()
    {
        void Reject(Action action, string message)
        {
            try
            {
                action();
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            throw new InvalidOperationException("FAIL panX: " + message);
        }

        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        var absent = New();
        Reject(() => absent.SetPlayerPan(1, 128), "pan above signed byte rejected");
    }

    [TestMethod]
    public void track_mask_selects_zero_based_existing_tracks()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var selection = New();
        selection.Start(Resources.Base, 1);
        selection.Frame();
        var tracks = Tracks(selection, 1);
        Assert.IsTrue(selection.SetPlayerPan(1, -64, 5) == 2 && tracks.Select(t => t.ExternalPan).SequenceEqual(new[] { -64, 0, -64, 0 }), "track mask selects zero-based existing tracks");
    }

    [TestMethod]
    public void empty_and_out_of_range_masks_are_no_ops()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var selection = New();
        selection.Start(Resources.Base, 1);
        selection.Frame();
        var tracks = Tracks(selection, 1);
        selection.SetPlayerPan(1,-64,5);
        Assert.IsTrue(selection.SetPlayerPan(1, 63, 0) == 0 && selection.SetPlayerPan(1, 63, 0x8000) == 0 && tracks[0].ExternalPan == -64, "empty and out-of-range masks are no-ops");
    }

    [TestMethod]
    public void FINE_track_is_excluded()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var selection = New();
        selection.Start(Resources.Base, 1);
        selection.Frame();
        var tracks = Tracks(selection, 1);
        tracks[1].Ended = true;
        Assert.IsTrue(selection.SetPlayerPan(1, 127) == 3 && tracks[1].ExternalPan == 0, "FINE track is excluded");
    }

    [TestMethod]
    public void pause_retains_existing_track_pan_without_resuming()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var selection = New();
        selection.Start(Resources.Base, 1);
        selection.Frame();
        var tracks = Tracks(selection, 1);
        tracks[1].Ended = true;
        selection.PausePlayer(1);
        Assert.IsTrue(selection.SetPlayerPan(1, -128, 1) == 1 && tracks[0].ExternalPan == -128 && selection.PlayerPaused(1), "pause retains existing track pan without resuming");
    }

    [TestMethod]
    public void hard_stop_prevents_stale_track_mutation()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var selection = New();
        selection.Start(Resources.Base, 1);
        selection.Frame();
        var tracks = Tracks(selection, 1);
        tracks[1].Ended = true;
        selection.PausePlayer(1);
        selection.SetPlayerPan(1,-128,1);
        selection.Stop(1);
        Assert.IsTrue(selection.SetPlayerPan(1, 63) == 0 && tracks[0].ExternalPan == -128, "hard stop prevents stale track mutation");
    }

    [TestMethod]
    public void restart_resets_panX()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var selection = New();
        selection.Start(Resources.Base, 1);
        selection.Frame();
        selection.PausePlayer(1);
        selection.Stop(1);
        selection.Start(Resources.Base, 1);
        selection.Frame();
        Assert.IsTrue(Tracks(selection, 1).All(t => t.ExternalPan == 0), "restart resets panX");
    }

    [TestMethod]
    public void left_right_center_direction()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        double[] Energy(Synth s)
        {
            var pcm = new float[8192];
            s.Render(pcm);
            if (pcm.Any(x => !float.IsFinite(x)))
                throw new InvalidDataException("Nonfinite pan PCM");
            double[] e = [0, 0];
            for (int n = 0; n < pcm.Length; n++)
                e[n % 2] += pcm[n] * pcm[n];
            return e;
        }

        foreach (AudioProfile profile in Enum.GetValues<AudioProfile>())
        {
            foreach (uint offset in new uint[]
            {
                64,
                128
            }

            )
            {
                var left = New(profile);
                left.Start(Resources.Base + offset, 1);
                left.SetPlayerPan(1, -128);
                var right = New(profile);
                right.Start(Resources.Base + offset, 1);
                right.SetPlayerPan(1, 127);
                var center = New(profile);
                center.Start(Resources.Base + offset, 1);
                var l = Energy(left);
                var r = Energy(right);
                var c = Energy(center);
                Assert.IsTrue(l[0] > l[1] * 4 && r[1] > r[0] * 4 && c.All(x => x > 0) && Math.Max(c[0], c[1]) < 1.15 * Math.Min(c[0], c[1]), $"{profile} {(offset == 64 ? "PCM" : "PSG")} left/right/center direction");
                left.SetPlayerPan(1, 127);
                var reversed = Energy(left);
                Assert.IsTrue(reversed[1] > reversed[0] * 4, $"{profile} {(offset == 64 ? "PCM" : "PSG")} live pan reversal");
            }
        }
    }

    [TestMethod]
    public void sequence_PAN_and_host_panX_coexist_on_first_frame()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var coexist = New();
        coexist.Start(Resources.Base + 192, 1);
        coexist.SetPlayerPan(1, -64);
        coexist.Frame();
        var ct = Tracks(coexist, 1)[0];
        Assert.IsTrue(ct.Pan == 32 && ct.ExternalPan == -64, "sequence PAN and host panX coexist on first frame");
    }

    [TestMethod]
    public void opposite_sequence_PAN_and_panX_cancel()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var coexist = New();
        coexist.Start(Resources.Base + 192, 1);
        coexist.SetPlayerPan(1, -64);
        coexist.Frame();
        var ct = Tracks(coexist, 1)[0];
        var first = Synth.ChannelVolume(ct, 127);
        Assert.IsTrue(Math.Abs(first.Left - first.Right) <= 2, "opposite sequence PAN and panX cancel");
    }

    [TestMethod]
    public void later_sequence_PAN_retains_external_panX_and_clamps_sum()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var coexist = New();
        coexist.Start(Resources.Base + 192, 1);
        coexist.SetPlayerPan(1, -64);
        coexist.Frame();
        var ct = Tracks(coexist, 1)[0];
        coexist.Frame();
        coexist.Frame();
        Assert.IsTrue(ct.Pan == -32 && ct.ExternalPan == -64 && Synth.ChannelVolume(ct, 127).Right == 0, "later sequence PAN retains external panX and clamps sum");
    }

    [TestMethod]
    public void selected_player_leaves_BGM_and_unrelated_SE_untouched()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        var independent = New();
        independent.Start(Resources.Base + 64, 0);
        independent.Start(Resources.Base + 64, 1);
        independent.Start(Resources.Base + 64, 3);
        independent.Frame();
        Assert.IsTrue(independent.SetPlayerPan(1, -64) == 1 && Tracks(independent, 0)[0].ExternalPan == 0 && Tracks(independent, 3)[0].ExternalPan == 0, "selected player leaves BGM and unrelated SE untouched");
    }

    [TestMethod]
    public void naturally_FINE_track_cannot_be_repanned()
    {
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p)
        {
            EnableTrace = false
        };
        var ended = New();
        ended.Start(Resources.Base + 256, 1);
        ended.SetPlayerPan(1, -64);
        ended.Frame();
        ended.Frame();
        Assert.IsTrue(ended.SetPlayerPan(1, 63) == 0, "naturally FINE track cannot be repanned");
    }

    [TestMethod]
    public void explicit_zero_pan_equals_default_PCM()
    {
        foreach (AudioProfile profile in new[]
        {
            AudioProfile.ModernDryV1,
            AudioProfile.SourceFiltered
        }

        )
        {
            var plain = new Synth(real, profile)
            {
                EnableTrace = false
            };
            var zero = new Synth(real, profile)
            {
                EnableTrace = false
            };
            plain.Start(0x086b5720, 1);
            zero.Start(0x086b5720, 1);
            zero.SetPlayerPan(1, 0);
            var a = new float[16384];
            var b = new float[a.Length];
            plain.Render(a);
            zero.Render(b);
            Assert.IsTrue(a.Any(x => x != 0) && a.SequenceEqual(b), $"{profile} explicit zero pan equals default PCM");
        }
    }

}
