using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class DriverProfileTests
{
    private FireRedAudio.Resources resources = TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.GetFullPath(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack"))));
    static void CheckFinite(double left, double right)
    {
        if (!double.IsFinite(left) || !double.IsFinite(right))
            throw new Exception("Non-finite delay output");
    }

    [TestMethod]
    public void source_PSG_absolute_output_gain()
    {
        var gain = new Synth(resources, AudioProfile.SourceGain)
        {
            EnableTrace = false
        };
        var player = new Player(resources, gain, Song.Read(resources, 0x086b5720), 1);
        gain.Note(player, new Track(player.Song.Tracks[0], 0) { Program = 88, Volume = 80 }, 94, 127, -1);
        var sample = new float[2];
        gain.Render(sample);
        Assert.IsTrue(sample[0] == 9 * 3.0 / 256 && sample[1] == sample[0], "source PSG absolute output gain");
    }

    [TestMethod]
    public void source_fractional_pitch_lookup_and_integer_Hz()
    {
        var pitch = new Synth(resources, AudioProfile.SourcePitch)
        {
            EnableTrace = false
        };
        var bgm = new Player(resources, pitch, Song.Read(resources, 0x086dd844), 0);
        pitch.Note(bgm, new Track(bgm.Song.Tracks[0], 0) { Program = 4, Tune = 16 }, 60, 100, -1);
        Assert.IsTrue(pitch.SourcePcmFrequency(pitch.Voices[0]) == 13577, "source fractional pitch lookup and integer Hz");
    }

    [TestMethod]
    public void source_delay_first_echo_geometry()
    {
        var delay = new SourceDelay();
        int firstTail = -1;
        double tailPeak = 0;
        for (int n = 0; n < 30000; n++)
        {
            var(left, right) = delay.Process(n == 0 ? 1 : 0, 0);
            CheckFinite(left, right);
            if (n > 0 && firstTail < 0 && left != 0)
                firstTail = n;
            if (n > 0)
                tailPeak = Math.Max(tailPeak, Math.Abs(left));
        }

        Assert.IsTrue(firstTail == (int)(6 * Synth.Rate * (double)Synth.FrameCycles / Synth.Cpu), "source-delay first echo geometry");
    }

    [TestMethod]
    public void source_delay_feedback_bounded()
    {
        var delay = new SourceDelay();
        int firstTail = -1;
        double tailPeak = 0;
        for (int n = 0; n < 30000; n++)
        {
            var(left, right) = delay.Process(n == 0 ? 1 : 0, 0);
            CheckFinite(left, right);
            if (n > 0 && firstTail < 0 && left != 0)
                firstTail = n;
            if (n > 0)
                tailPeak = Math.Max(tailPeak, Math.Abs(left));
        }

        Assert.IsTrue(tailPeak < 0.2, "source-delay feedback bounded");
    }

    [TestMethod]
    public void DC_blocker_removes_constant_offset()
    {
        var dc = new DcBlocker();
        double residual = 0;
        for (int n = 0; n < 10000; n++)
            residual = dc.Process(1, 1).Left;
        Assert.IsTrue(Math.Abs(residual) < 1e-8, "DC blocker removes constant offset");
    }

    [TestMethod]
    public void finite_block_independent()
    {
        foreach (var profile in Enum.GetValues<AudioProfile>())
        {
            var whole = new Synth(resources, profile)
            {
                EnableTrace = false
            };
            var blocks = new Synth(resources, profile)
            {
                EnableTrace = false
            };
            whole.Start(0x086dd844, 0);
            blocks.Start(0x086dd844, 0);
            var a = new float[2 * Synth.Rate * 2];
            var b = new float[a.Length];
            whole.Render(a);
            for (int pos = 0; pos < b.Length;)
            {
                int length = Math.Min(386, b.Length - pos);
                blocks.Render(b.AsSpan(pos, length));
                pos += length;
            }

            Assert.IsTrue(a.SequenceEqual(b) && a.All(float.IsFinite), profile + " finite/block independent");
        }
    }

    [TestMethod]
    public void PCM_only_reverb_leaves_SELECT_exactly_unchanged()
    {
        var dry = new Synth(resources, AudioProfile.SourceLinear)
        {
            EnableTrace = false
        };
        var reverb = new Synth(resources, AudioProfile.SourceReverb)
        {
            EnableTrace = false
        };
        dry.Start(0x086b5720, 1);
        reverb.Start(0x086b5720, 1);
        var x = new float[2 * Synth.Rate];
        var y = new float[x.Length];
        dry.Render(x);
        reverb.Render(y);
        Assert.IsTrue(x.SequenceEqual(y), "PCM-only reverb leaves SELECT exactly unchanged");
    }

    [TestMethod]
    public void invalid_profile_enum_rejected()
    {
        bool invalidRejected = false;
        try
        {
            _ = new Synth(resources, (AudioProfile)999);
        }
        catch (ArgumentOutOfRangeException)
        {
            invalidRejected = true;
        }

        Assert.IsTrue(invalidRejected, "invalid profile enum rejected");
    }

    [TestMethod]
    public void Ended_includes_effect_tail_state()
    {
        var dry = new Synth(resources, AudioProfile.SourceLinear)
        {
            EnableTrace = false
        };
        dry.Start(0x086b5720, 1);
        var x = new float[2 * Synth.Rate];
        dry.Render(x);
        var tailEngine = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        tailEngine.Start(0x086dd844, 0);
        tailEngine.Render(x);
        tailEngine.Stop(0);
        Assert.IsTrue(!tailEngine.Ended, "Ended includes effect tail state");
    }

    [TestMethod]
    public void effect_tails_eventually_become_silent()
    {
        var dry = new Synth(resources, AudioProfile.SourceLinear)
        {
            EnableTrace = false
        };
        dry.Start(0x086b5720, 1);
        var x = new float[2 * Synth.Rate];
        dry.Render(x);
        var tailEngine = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        tailEngine.Start(0x086dd844, 0);
        tailEngine.Render(x);
        tailEngine.Stop(0);
        for (int n = 0; n < 5; n++)
            tailEngine.Render(x);
        Assert.IsTrue(tailEngine.Ended, "effect tails eventually become silent");
    }

    [TestMethod]
    public void shadow_START_plus_STOP_before_mix_is_silent()
    {
        var dry = new Synth(resources, AudioProfile.SourceLinear)
        {
            EnableTrace = false
        };
        dry.Start(0x086b5720, 1);
        var x = new float[2 * Synth.Rate];
        dry.Render(x);
        var tailEngine = new Synth(resources, AudioProfile.SourceFiltered)
        {
            EnableTrace = false
        };
        tailEngine.Start(0x086dd844, 0);
        tailEngine.Render(x);
        tailEngine.Stop(0);
        for (int n = 0; n < 5; n++)
            tailEngine.Render(x);
        var cancelled = new Synth(resources, AudioProfile.SourceEnvelope)
        {
            EnableTrace = false
        };
        var cancelPlayer = new Player(resources, cancelled, Song.Read(resources, 0x086b5720), 1);
        var cancelTrack = new Track(cancelPlayer.Song.Tracks[0], 0)
        {
            Program = 88
        };
        cancelled.Note(cancelPlayer, cancelTrack, 94, 127, 3);
        cancelled.ReleaseTrack(cancelPlayer, cancelTrack);
        cancelled.Render(x);
        Assert.IsTrue(x.All(value => value == 0), "shadow START plus STOP before mix is silent");
    }
}