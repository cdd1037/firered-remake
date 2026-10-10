using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FireRedAudio;

if (args.Length != 3)
    throw new ArgumentException("CryCoverage <fixed.pack> <inventory.json> <output-directory>");
var resources = Resources.FromPack(args[0]);
using var inventory = JsonDocument.Parse(File.ReadAllText(args[1]));
Directory.CreateDirectory(args[2]);
var rows = new List<object>();
int success = 0;

// Warm the same code path before measuring the batch. No cached decoded catalog.
for (int i = 0; i < 2; i++)
{
    var warm = new Synth(resources, AudioProfile.SourceFiltered) { EnableTrace = false };
    warm.StartNormalCry(duckBgm: false);
    warm.Render(new float[Synth.Rate * 2]);
}
var timer = Stopwatch.StartNew();
long allocationStart = GC.GetTotalAllocatedBytes(true);
foreach (var entry in inventory.RootElement.GetProperty("species").EnumerateArray())
{
    int id = entry.GetProperty("internalId").GetInt32();
    string name = entry.GetProperty("name").GetString()!;
    uint tone = entry.GetProperty("toneAddress").GetUInt32();
    int payload = entry.GetProperty("payloadBytes").GetInt32();
    bool guardStored = entry.GetProperty("guardStored").GetBoolean();
    bool strictDecodeAccepted;
    try
    {
        DpcmDecoder.Decode(resources, entry.GetProperty("waveAddress").GetUInt32() + 16,
            entry.GetProperty("sampleCount").GetInt32() + 1, payload);
        strictDecodeAccepted = true;
    }
    catch (InvalidDataException) { strictDecodeAccepted = false; }
    if (strictDecodeAccepted != guardStored)
        throw new InvalidDataException("Manifest guard support does not match actual decoder");
    try
    {
        var a = Render(tone, payload, guardStored, 512);
        var b = Render(tone, payload, guardStored, 257);
        if (a.Hash != b.Hash || !a.Ended || !b.Ended || a.Clipped != 0 || b.Clipped != 0 || a.Peak == 0 || b.Peak == 0)
            throw new InvalidDataException("Block mismatch, unfinished, silent or clipped render");
        // Keep only representative outputs, rather than 386 four-second recordings.
        if (new[] { "BULBASAUR", "PIKACHU", "MEWTWO", "TREECKO", "WAILORD", "DEOXYS" }.Contains(name.ToUpperInvariant()))
            WriteWav(Path.Combine(args[2], $"native-{name.ToLowerInvariant()}.wav"), a.Pcm);
        success++;
        rows.Add(new
        {
            internalId = id,
            name,
            status = "rendered",
            hash = a.Hash,
            strictPhase3GuardDecode = strictDecodeAccepted ? "supported" : "truncated-guard-rejected",
            guardPolicy = guardStored ? "stored" : "audited-repeat-last",
            blockPartitionExact = true,
            ended = a.Ended,
            clipped = a.Clipped,
            maxPcm = a.MaxPcm,
            peak = a.Peak,
            renderAllocationBytes = a.Allocated,
            secondPassRenderAllocationBytes = b.Allocated,
            lastNonzeroFrame = a.LastNonzero
        });
    }
    catch (Exception ex)
    {
        rows.Add(new
        {
            internalId = id,
            name,
            status = "failed",
            errorType = ex.GetType().Name,
            error = ex.Message
        });
    }
}
timer.Stop();
var report = new
{
    scope = "Research-only normal cry substitution; phase3 public API remains Charmander-only",
    profile = "SourceFiltered",
    secondsPerRender = 4,
    blocks = new[] { 512, 257 },
    success,
    failed = rows.Count - success,
    total = rows.Count,
    elapsedSeconds = timer.Elapsed.TotalSeconds,
    allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocationStart,
    workingSetBytes = Process.GetCurrentProcess().WorkingSet64,
    runtime = Environment.Version.ToString(),
    tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "default",
    rows
};
File.WriteAllText(Path.Combine(args[2], "render-coverage.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { success, failed = rows.Count - success, elapsedSeconds = timer.Elapsed.TotalSeconds }));
if (success != rows.Count) Environment.ExitCode = 1;

RenderResult Render(uint tone, int payload, bool guardStored, int block)
{
    var synth = new Synth(resources, AudioProfile.SourceFiltered) { EnableTrace = false };
    synth.StartResearchCry(tone, payload, guardStored);
    var buffer = new float[block * 2];
    var pcm = new short[4 * Synth.Rate * 2];
    long before = GC.GetAllocatedBytesForCurrentThread();
    int last = -1;
    for (int done = 0; done < 4 * Synth.Rate;)
    {
        int count = Math.Min(block, 4 * Synth.Rate - done);
        synth.Render(buffer.AsSpan(0, count * 2));
        for (int j = 0; j < count * 2; j++)
        {
            float value = buffer[j];
            if (!float.IsFinite(value))
                throw new InvalidDataException("Non-finite output");
            pcm[done * 2 + j] = (short)Math.Clamp(Math.Round(value * 32767), short.MinValue, short.MaxValue);
            if (pcm[done * 2 + j] != 0)
                last = done + j / 2;
        }
        done += count;
    }
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    byte[] raw = new byte[pcm.Length * 2];
    Buffer.BlockCopy(pcm, 0, raw, 0, raw.Length);
    return new(Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant(), pcm,
        synth.Ended, synth.Clipped, synth.MaxPcm, synth.Peak, allocated, last);
}
static void WriteWav(string path, short[] pcm)
{
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write("RIFF"u8);
    writer.Write(36 + pcm.Length * 2);
    writer.Write("WAVEfmt "u8);
    writer.Write(16);
    writer.Write((short)1);
    writer.Write((short)2);
    writer.Write(Synth.Rate);
    writer.Write(Synth.Rate * 4);
    writer.Write((short)4);
    writer.Write((short)16);
    writer.Write("data"u8);
    writer.Write(pcm.Length * 2);
    foreach (short sample in pcm)
        writer.Write(sample);
}
record RenderResult(string Hash, short[] Pcm, bool Ended, long Clipped, int MaxPcm, double Peak, long Allocated, int LastNonzero);
