using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class AudioMathTests
{
    private FireRedAudio.Resources r = TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.GetFullPath(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack"))));
    [TestMethod]
    public void bounds_end()
    {
        void Reject(Action a, string name)
        {
            try
            {
                a();
            }
            catch (InvalidDataException)
            {
                Console.WriteLine("PASS " + name);
                return;
            }

            throw new Exception("FAIL " + name);
        }

        Reject(() => r.U32(Resources.End - 3), "bounds end");
    }

    [TestMethod]
    public void bounds_start()
    {
        void Reject(Action a, string name)
        {
            try
            {
                a();
            }
            catch (InvalidDataException)
            {
                Console.WriteLine("PASS " + name);
                return;
            }

            throw new Exception("FAIL " + name);
        }

        Reject(() => r.U8(Resources.Base - 1), "bounds start");
    }

    [TestMethod]
    public void address_overflow()
    {
        void Reject(Action a, string name)
        {
            try
            {
                a();
            }
            catch (InvalidDataException)
            {
                Console.WriteLine("PASS " + name);
                return;
            }

            throw new Exception("FAIL " + name);
        }

        Reject(() => r.Offset(uint.MaxValue, 4), "address overflow");
    }

    [TestMethod]
    public void negative_length()
    {
        void Reject(Action a, string name)
        {
            try
            {
                a();
            }
            catch (InvalidDataException)
            {
                Console.WriteLine("PASS " + name);
                return;
            }

            throw new Exception("FAIL " + name);
        }

        Reject(() => r.Offset(Resources.Base, -1), "negative length");
    }

    [TestMethod]
    public void block_size_independent_exact_output()
    {
        var a = new Synth(r)
        {
            EnableTrace = false
        };
        var b = new Synth(r)
        {
            EnableTrace = false
        };
        a.Start(0x086dd844, 0);
        b.Start(0x086dd844, 0);
        var x = new float[48000 * 2];
        a.Render(x);
        var y = new float[x.Length];
        for (int pos = 0; pos < y.Length;)
        {
            int n = Math.Min(514, y.Length - pos);
            b.Render(y.AsSpan(pos, n));
            pos += n;
        }

        Assert.IsTrue(x.SequenceEqual(y), "block-size independent exact output");
    }

    [TestMethod]
    public void finite_samples()
    {
        var a = new Synth(r)
        {
            EnableTrace = false
        };
        a.Start(0x086dd844, 0);
        var x = new float[48000 * 2];
        a.Render(x);
        Assert.IsTrue(x.All(float.IsFinite), "finite samples");
    }

    [TestMethod]
    public void real_nonzero_synthesis()
    {
        var a = new Synth(r)
        {
            EnableTrace = false
        };
        a.Start(0x086dd844, 0);
        var x = new float[48000 * 2];
        a.Render(x);
        Assert.IsTrue(x.Any(v => v != 0), "real nonzero synthesis");
    }

    [TestMethod]
    public void five_PCM_cap()
    {
        var a = new Synth(r)
        {
            EnableTrace = false
        };
        a.Start(0x086dd844, 0);
        var x = new float[48000 * 2];
        a.Render(x);
        Assert.IsTrue(a.MaxPcm <= 5, "five PCM cap");
    }

    [TestMethod]
    public void fractional_frame_clock()
    {
        var a = new Synth(r)
        {
            EnableTrace = false
        };
        a.Start(0x086dd844, 0);
        var x = new float[48000 * 2];
        a.Render(x);
        Assert.IsTrue(a.GameFrames == 1 + 48000L * Synth.Cpu / (Synth.Rate * Synth.FrameCycles), "fractional frame clock");
    }

    [TestMethod]
    public void hard_stop_clears_voices()
    {
        var a = new Synth(r)
        {
            EnableTrace = false
        };
        a.Start(0x086dd844, 0);
        var x = new float[48000 * 2];
        a.Render(x);
        a.Stop(0);
        a.Render(x);
        Assert.IsTrue(x.All(v => v == 0), "hard stop clears voices");
    }

    [TestMethod]
    public void SE_synthesis()
    {
        var a = new Synth(r)
        {
            EnableTrace = false
        };
        a.Start(0x086dd844, 0);
        var x = new float[48000 * 2];
        a.Render(x);
        a.Stop(0);
        a.Render(x);
        a.Start(0x086b5720, 1);
        a.Render(x);
        Assert.IsTrue(x.Any(v => v != 0), "SE synthesis");
    }

    [TestMethod]
    public void SE_ends()
    {
        var a = new Synth(r)
        {
            EnableTrace = false
        };
        a.Start(0x086dd844, 0);
        var x = new float[48000 * 2];
        a.Render(x);
        a.Stop(0);
        a.Render(x);
        a.Start(0x086b5720, 1);
        a.Render(x);
        a.Render(x);
        Assert.IsTrue(a.Ended, "SE ends");
    }
}