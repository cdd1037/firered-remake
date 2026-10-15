using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FireRedAudio;
if (args.Length == 0)
{
    Console.WriteLine("render <rom-or-pack> <pallet|select|mix|cry-charmander|cry-mix> <seconds> <output.wav> [profile]");
    return;
}
try
{
    if (args[0] is not ("pack" or "benchmark" or "benchmark-cry" or "render"))
        throw new ArgumentException("Unsupported command");
    int required = args[0] switch
    {
        "pack" => 3,
        "render" => 5,
        _ => 2
    };
    if (args.Length != required && !((args[0] == "render" && args.Length == 6) || ((args[0] == "benchmark" || args[0] == "benchmark-cry") && args.Length == 3)))
        throw new ArgumentException("Incorrect argument count");
    var r = args[1].EndsWith(".pack", StringComparison.OrdinalIgnoreCase) ? Resources.FromPack(args[1]) : Resources.FromRom(args[1]);
    if (args[0] == "pack")
    {
        r.Save(args[2]);
        Console.WriteLine("External resources written, 2565436 bytes");
        return;
    }
    if (args[0] == "benchmark" || args[0] == "benchmark-cry")
    {
        Benchmark.Run(r, args.Length == 3 ? ParseProfile(args[2]) : AudioProfile.ModernDryV1, args[0] == "benchmark-cry");
        return;
    }
    string mode = args[2];
    if (mode is not ("pallet" or "select" or "mix" or "cry-charmander" or "cry-mix"))
        throw new NotSupportedException("Unsupported song/mode; only cry-charmander normal is available");
    int seconds = int.Parse(args[3]);
    if (seconds is < 1 or > 600)
        throw new ArgumentException("Seconds must be 1..600");
    AudioProfile profile = args.Length == 6 ? ParseProfile(args[5]) : AudioProfile.ModernDryV1;
    var synth = new Synth(r, profile);
    if (mode == "cry-charmander")
        synth.StartNormalCry(duckBgm: false);
    else
        synth.Start(mode == "select" ? 0x086b5720u : 0x086dd844u, mode == "select" ? 1 : 0);
    if (mode == "cry-mix")
        synth.PrepareNormalCry();
    var buffer = new float[1024 * 2];
    var pcm = new short[1024 * 2];
    using (var writer = new BinaryWriter(File.Create(args[4])))
    {
        int total = seconds * Synth.Rate;
        Wav.Header(writer, total);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (int done = 0; done < total;)
        {
            int count = Math.Min(1024, total - done);
            if (mode == "mix")
            {
                foreach (int boundary in new[] { Synth.Rate * 2, Synth.Rate * 3, Synth.Rate * 6, Synth.Rate * 7 })
                    if (done < boundary)
                        count = Math.Min(count, boundary - done);
                if (done == Synth.Rate * 2 || done == Synth.Rate * 3)
                    synth.Start(0x086b5720, 1);
                if (done == Synth.Rate * 6)
                    synth.Stop(0);
                if (done == Synth.Rate * 7)
                    synth.Start(0x086dd844, 0);
            }
            if (mode == "cry-mix")
            {
                foreach (int boundary in new[] { 2 * Synth.Rate, 3 * Synth.Rate, 31 * Synth.Rate / 10, 6 * Synth.Rate, 7 * Synth.Rate })
                    if (done < boundary)
                        count = Math.Min(count, boundary - done);
                if (done == 2 * Synth.Rate || done == 31 * Synth.Rate / 10)
                    synth.Start(0x086b5720, 1);
                if (done == 3 * Synth.Rate)
                    synth.StartNormalCry();
                if (done == 6 * Synth.Rate)
                    synth.Stop(0);
                if (done == 7 * Synth.Rate)
                    synth.Start(0x086dd844, 0);
            }
            synth.Render(buffer.AsSpan(0, count * 2));
            for (int i = 0; i < count * 2; i++)
            {
                if (!float.IsFinite(buffer[i]))
                    throw new ArithmeticException("Non-finite output");
                pcm[i] = (short)Math.Round(buffer[i] * 32767);
                writer.Write(pcm[i]);
            }
            done += count;
        }
        timer.Stop();
        var report = new
        {
            profile = profile.ToString(),
            mode,
            seconds,
            sampleRate = Synth.Rate,
            outputFrames = synth.OutputFrames,
            gameFrames = synth.GameFrames,
            synth.Peak,
            synth.Clipped,
            synth.MaxPcm,
            synth.Stolen,
            synth.Rejected,
            elapsedMs = timer.Elapsed.TotalMilliseconds,
            allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated,
            rssBytes = Process.GetCurrentProcess().WorkingSet64,
            traceEvents = synth.Trace.Count,
            sourceSha1 = Resources.SourceSha1
        };
        File.WriteAllText(args[4] + ".json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
    }
    File.WriteAllLines(args[4] + ".events.txt", synth.Trace);
    Console.WriteLine("WAV SHA256 " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[4]))));
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
    Environment.ExitCode = 1;
}
static AudioProfile ParseProfile(string text) => text switch
{
    "modern-dry" => AudioProfile.ModernDryV1,
    "source-gain" => AudioProfile.SourceGain,
    "source-linear" => AudioProfile.SourceLinear,
    "source-pitch" => AudioProfile.SourcePitch,
    "source-envelope" => AudioProfile.SourceEnvelope,
    "source-reverb" => AudioProfile.SourceReverb,
    "source-bandlimited" => AudioProfile.SourceBandlimited,
    "source-filtered" => AudioProfile.SourceFiltered,
    _ => throw new NotSupportedException("Unsupported profile")
};
static class Wav
{
    public static void Header(BinaryWriter w, int frames)
    {
        w.Write("RIFF"u8);
        w.Write(36 + frames * 4);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)2);
        w.Write(Synth.Rate);
        w.Write(Synth.Rate * 4);
        w.Write((short)4);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(frames * 4);
    }
}
static class Benchmark
{
    public static void Run(Resources r, AudioProfile profile, bool withCry = false)
    {
        var synth = new Synth(r, profile) { EnableTrace = false };
        synth.Start(0x086dd844, 0);
        if (withCry)
        {
            synth.PrepareNormalCry();
            synth.StartNormalCry();
            synth.Start(0x086b5720, 1);
        }
        var buffer = new float[1024];
        for (int n = 0; n < 9000; n++)
            synth.Render(buffer);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var timings = new double[5625];
        long before = GC.GetAllocatedBytesForCurrentThread();
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        long controlAllocation = 0;
        int nextCry = 0, nextSe = -1, cryStarts = 0;
        long begin = Stopwatch.GetTimestamp();
        for (int n = 0; n < timings.Length; n++)
        {
            int frame = n * 512;
            if (withCry && frame >= nextCry)
            {
                long a = GC.GetAllocatedBytesForCurrentThread();
                synth.StartNormalCry();
                controlAllocation += GC.GetAllocatedBytesForCurrentThread() - a;
                cryStarts++;
                nextCry += 3 * Synth.Rate;
                nextSe = frame + Synth.Rate / 10;
            }
            if (withCry && nextSe >= 0 && frame >= nextSe)
            {
                long a = GC.GetAllocatedBytesForCurrentThread();
                synth.Start(0x086b5720, 1);
                controlAllocation += GC.GetAllocatedBytesForCurrentThread() - a;
                nextSe = -1;
            }
            long t = Stopwatch.GetTimestamp();
            synth.Render(buffer);
            timings[n] = (Stopwatch.GetTimestamp() - t) * 1000000.0 / Stopwatch.Frequency;
        }
        double seconds = (Stopwatch.GetTimestamp() - begin) / (double)Stopwatch.Frequency;
        long allocation = GC.GetAllocatedBytesForCurrentThread() - before;
        Array.Sort(timings);
        var result = new
        {
            profile = profile.ToString(),
            renderedSeconds = 60,
            elapsedSeconds = seconds,
            realtimeFactor = 60 / seconds,
            blockFrames = 512,
            p50Microseconds = timings[timings.Length / 2],
            p95Microseconds = timings[(int)(timings.Length * .95)],
            p99Microseconds = timings[(int)(timings.Length * .99)],
            maxMicroseconds = timings[^1],
            allocatedBytes = allocation,
            controlAllocationBytes = controlAllocation,
            renderAllocationBytes = allocation - controlAllocation,
            cryStarts,
            gen0 = GC.CollectionCount(0) - g0,
            gen1 = GC.CollectionCount(1) - g1,
            gen2 = GC.CollectionCount(2) - g2,
            rssBytes = Process.GetCurrentProcess().WorkingSet64,
            synth.MaxPcm,
            synth.Clipped
        };
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
}
