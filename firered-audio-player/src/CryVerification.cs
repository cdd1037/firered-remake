using System.Security.Cryptography;
using System.Buffers.Binary;
namespace FireRedAudio;

public static class CryVerification
{
    public static int Run(Resources resources)
    {
        int passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new Exception("FAIL " + name);
            Console.WriteLine("PASS " + name);
            passed++;
        }
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException)
            {
                Check(true, name);
                return;
            }
            throw new Exception("FAIL " + name);
        }
        var decoded = DpcmDecoder.Decode(resources, NormalCharmanderCry.WaveAddress + 16, 7134, 3679);
        string hash = Convert.ToHexString(SHA256.HashData(decoded.Select(x => unchecked((byte)x)).ToArray())).ToLowerInvariant();
        Check(hash == "e1890eaf6544102dbbbcfd9c217f5502e84a85ab7496aaeb045921e1a1de958f", "DPCM all7134 samples match independent original-WAV oracle");
        Check(decoded[^1] == -2 && DpcmDecoder.RequiredBytes(7134) == 3679, "DPCM guard and exact physical payload size");
        Reject(() => DpcmDecoder.Decode(resources, NormalCharmanderCry.WaveAddress + 16, 7134, 3678), "DPCM truncated tail rejected");
        Reject(() => DpcmDecoder.Decode(resources, Resources.End - 1, 64, 33), "DPCM address range rejected before decode");
        Reject(() => DpcmDecoder.RequiredBytes(1_000_001), "DPCM allocation budget enforced");
        var fake = new byte[Resources.End - Resources.Base];
        fake[1024] = 127;
        fake[1025] = 0xf1;
        fake[1026] = 0xf8;
        Check(DpcmDecoder.Decode(new Resources(fake), Resources.Base + 1024, 4, 3).SequenceEqual(new sbyte[] { 127, -128, 127, 63 }), "DPCM nibble order and signed8 wrap");
        Reject(() => NormalCharmanderCry.Build(resources, 1, 0), "other cry species rejected");
        Reject(() => NormalCharmanderCry.Build(resources, 4, 1), "special cry mode rejected");
        var (song, bytes) = NormalCharmanderCry.Build(resources, 4, 0);
        Check(bytes.Length == 52 && bytes[0] == 1 && bytes[2] == 10 && bytes[0x1d] == 120 && bytes[0x2a] == 60, "bounded normal cry template parameters");
        var image = new SequenceImage(resources, bytes);
        Reject(() => image.U8(NormalCharmanderCry.SequenceBase - 1), "dynamic sequence lower bound");
        Reject(() => image.U32(NormalCharmanderCry.SequenceBase + 50), "dynamic sequence upper bound");
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
        Reject(() => BadSequence(Bad(0xcd, 13, 1, 0, 0, 0)), "nonzero cry progress unsupported");
        var cycle = Bad(0xb2, 0, 0, 0, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(cycle.AsSpan(0x12), NormalCharmanderCry.SequenceBase + 0x11);
        Reject(() => BadSequence(cycle), "dynamic no-wait loop hits instruction budget");
        var escape = Bad(0xb2, 0, 0, 0, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(escape.AsSpan(0x12), 0x086dd012);
        Reject(() => BadSequence(escape), "dynamic jump cannot escape into ROM");

        var solo = new Synth(resources, AudioProfile.SourceFiltered);
        solo.StartNormalCry(duckBgm: false);
        var second = new float[Synth.Rate * 2];
        solo.Render(second);
        Check(!solo.CryPlaying && !solo.Ended, "sample ends before140tick template wait");
        solo.Render(second);
        solo.Render(second);
        Check(solo.Ended && solo.Trace.Any(x => x.Contains("tick=140 cry-sequence-fine")), "xWAIT140 reaches EOT/FINE at correct tick");
        Check(solo.MaxPcm == 1 && solo.Clipped == 0, "solo cry uses one real PCM voice without clipping");

        float[] Timeline(int block)
        {
            var engine = new Synth(resources, AudioProfile.SourceFiltered) { EnableTrace = false };
            engine.PrepareNormalCry();
            engine.Start(0x086dd844, 0);
            int total = 5 * Synth.Rate;
            int[] edges = { Synth.Rate, 2 * Synth.Rate, 21 * Synth.Rate / 10, 23 * Synth.Rate / 10, 4 * Synth.Rate, total };
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
                        Check(!engine.BgmDucked && !engine.CryPlaying, "cry replacement restores BGM after latest voice ends");
                    edge++;
                }
                int frames = Math.Min(block, edges[edge] - pos);
                engine.Render(output.AsSpan(pos * 2, frames * 2));
                pos += frames;
            }
            Check(sawDuck && engine.MaxPcm <= 5 && engine.Clipped == 0, "cry and SE share bounded PCM pool with BGM ducking");
            return output;
        }
        Check(Timeline(int.MaxValue).SequenceEqual(Timeline(257)), "cry/SE/retrigger/duck block independence");
        var cancel = new Synth(resources, AudioProfile.SourceFiltered) { EnableTrace = false };
        cancel.Start(0x086dd844, 0);
        cancel.Render(second);
        cancel.StartNormalCry();
        cancel.Render(second.AsSpan(0, 2400));
        cancel.Stop(2);
        cancel.Render(second);
        Check(!cancel.BgmDucked && !cancel.CryPlaying, "cancelled cry restores BGM without stale duck");
        var awaiting = new Synth(resources) { EnableTrace = false };
        awaiting.Start(0x086dd844, 0);
        awaiting.Render(second);
        awaiting.StartNormalCry();
        Reject(() => awaiting.StartNormalCry(4, 1), "invalid replacement mode fails without new action");
        Check(awaiting.BgmDucked, "failed cry request preserves existing duck state");
        var inheritance = new Synth(resources, AudioProfile.SourceFiltered) { EnableTrace = false };
        inheritance.StartNormalCry(duckBgm: false);
        Check(inheritance.ReverbAmount == 0, "fresh cry inherits global reverb zero");
        inheritance.Start(0x086dd844, 0);
        inheritance.StartNormalCry(duckBgm: false);
        Check(inheritance.ReverbAmount == 50, "cry preserves Pallet global reverb50");
        var full = new Synth(resources, AudioProfile.SourceFiltered) { EnableTrace = false };
        full.PrepareNormalCry();
        var fullSong = Song.Read(resources, 0x086dd844);
        var fullPlayer = new Player(resources, full, fullSong, 0);
        for (int n = 0; n < 5; n++)
            full.Note(fullPlayer, new Track(fullSong.Tracks[0], n) { Program = 4 }, 60 + n, 100, -1);
        full.StartNormalCry(duckBgm: false);
        full.Render(new float[2]);
        Check(full.Voices.Take(5).Count(v => v.Active) == 5 && full.Voices.Take(5).Count(v => v.Active && v.Player?.Id == 2 && v.Priority == 10) == 1, "cry priority10 steals exactly one occupied BGM PCM slot");
        var switched = new Synth(resources, AudioProfile.SourceFiltered) { EnableTrace = false };
        switched.StartNormalCry();
        switched.Render(new float[9600]);
        switched.Start(0x086dd844, 0);
        switched.Render(new float[2048]);
        Check(switched.Voices.Any(v => v.Active && v.Player?.Id == 0 && v.Track!.ExternalVolume == 21), "BGM started during cry inherits active duck");
        var prepared = new Synth(resources, AudioProfile.SourceFiltered) { EnableTrace = false };
        prepared.PrepareNormalCry();
        prepared.StartNormalCry();
        var warm = new float[2048];
        prepared.Render(warm);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int n = 0; n < 50; n++)
            prepared.Render(warm);
        long delta = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(delta == 0, "prepared cry render including completion has zero managed allocation");
        return passed;
    }
}
