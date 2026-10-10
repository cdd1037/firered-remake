using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using FireRedAudio;

if (args.Length != 3)
    throw new ArgumentException("IntegrationCoverage <fixed.pack> <selection.json> <output-directory>");
var resources = Resources.FromPack(args[0]);
using var selection = JsonDocument.Parse(File.ReadAllText(args[1]));
Directory.CreateDirectory(args[2]);
var rows = new List<object>();
var runtimeSources = selection.RootElement.GetProperty("runtimeSourceSha256").Clone();
foreach (var song in selection.RootElement.GetProperty("songs").EnumerateArray())
{
    string name = song.GetProperty("name").GetString()!;
    var first = Render(song, 512, true);
    var second = Render(song, 257, false);
    bool exact = first.Error == null && second.Error == null && first.Hash == second.Hash;
    bool failurePartitionExact = first.Error != null && first.Error == second.Error && first.OutputFrames == second.OutputFrames;
    rows.Add(new { name, songId = song.GetProperty("songId").GetInt32(),
        songAddress = song.GetProperty("songAddress").GetUInt32(),
        status = first.Error == null ? "rendered" : "failed-explicitly", blockPartitionExact = exact, failurePartitionExact,
        first, second });
    Console.WriteLine(JsonSerializer.Serialize(new { name, first.Error, first.CompletedFrames, first.Notes,
        first.Gotos, first.Ended, first.Clipped, blockPartitionExact = exact }));
}
File.WriteAllText(Path.Combine(args[2], "coverage.json"), JsonSerializer.Serialize(new {
    scope = "Bounded representative runtime coverage with documented modern host timing; no all-library or bit-perfect claim",
    profile = "SourceFiltered", sampleRate = Synth.Rate, blocks = new[] { 512, 257 }, runtimeSourceSha256 = runtimeSources,
    coreAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Synth).Assembly.Location))).ToLowerInvariant(),
    runtime = Environment.Version.ToString(), rows
}, new JsonSerializerOptions { WriteIndented = true }));

RenderResult Render(JsonElement input, int block, bool trace)
{
    var synth = new Synth(resources, AudioProfile.SourceFiltered) { EnableTrace = trace };
    string name = input.GetProperty("name").GetString()!;
    uint address = input.GetProperty("songAddress").GetUInt32();
    int playerId = input.GetProperty("researchPlayer").GetInt32();
    int total = input.GetProperty("seconds").GetInt32() * Synth.Rate;
    float[] buffer = new float[block * 2];
    string[] exported = { };
    short[]? preview = trace && exported.Contains(name) ? new short[8 * Synth.Rate * 2] : null;
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    string? error = null, errorType = null;
    object? failure = null;
    int completed = 0, lastNonzero = -1, observedVoices = 0;
    long nonfinite = 0;
    var timer = Stopwatch.StartNew();
    try
    {
        synth.Start(address, playerId);
        while (completed < total)
        {
            int count = Math.Min(block, total - completed);
            var span = buffer.AsSpan(0, count * 2);
            synth.Render(span);
            for (int i = 0; i < span.Length; i++)
            {
                if (!float.IsFinite(span[i])) nonfinite++;
                if (Math.Abs(span[i]) >= 0.5 / 32767) lastNonzero = completed + i / 2;
            }
            if (nonfinite != 0) throw new ArithmeticException("Non-finite output");
            if (preview is not null && completed < 8 * Synth.Rate)
            {
                int samples = Math.Min(span.Length, preview.Length - completed * 2);
                for (int i = 0; i < samples; i++)
                    preview[completed * 2 + i] = (short)Math.Clamp(Math.Round(span[i] * 32767), short.MinValue, short.MaxValue);
            }
            hash.AppendData(MemoryMarshal.AsBytes(span));
            observedVoices = Math.Max(observedVoices, synth.Voices.Count(v => v.Active));
            completed += count;
        }
    }
    catch (Exception ex)
    {
        error = ex.Message;
        errorType = ex.GetType().Name;
        // Diagnostic access only: runtime source/API is unchanged. Before a
        // later track executes, each earlier nonended track has Wait > 0.
        var players = (Player?[])typeof(Synth).GetField("players", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(synth)!;
        var player = players[playerId];
        var track = player?.Tracks.FirstOrDefault(t => !t.Ended && t.Wait == 0);
        if (player != null && track != null)
        {
            uint topTone = player.Song.Voices + (uint)track.Program * 12;
            uint resolvedTone = topTone;
            int topType = resources.U8(topTone);
            if ((topType & 0xc0) != 0)
            {
                int index = (topType & 0x40) != 0
                    ? resources.U8(resources.U32(topTone + 8) + (uint)track.Key) : track.Key;
                resolvedTone = resources.U32(topTone + 4) + (uint)index * 12;
            }
            failure = new { trackIndex = track.Index, playerTick = player.Tick, track.Tick, track.Cursor,
                track.Program, track.Key, track.KeyShift, track.Status, topToneAddress = topTone, topType,
                resolvedToneAddress = resolvedTone, resolvedType = resources.U8(resolvedTone),
                toneDataPointer = resources.U32(resolvedTone + 4),
                commandAddress = ex.Message.StartsWith("XCMD ") ? track.Cursor - 2 : (uint?)null,
                commandBytes = ex.Message.StartsWith("XCMD ") ? new[] { resources.U8(track.Cursor - 2), resources.U8(track.Cursor - 1), resources.U8(track.Cursor) } : null,
                gameFrames = synth.GameFrames, outputFrames = synth.OutputFrames,
                trackStates = player.Tracks.Select(t => new { t.Index, t.Cursor, t.Wait, t.Ended, t.Tick, t.Program }).ToArray() };
        }
    }
    timer.Stop();
    if (error is null && preview is not null)
        WriteWav(Path.Combine(args[2], name + ".wav"), preview);
    int notes = synth.Trace.Count(line => line.Contains(" note key="));
    int gotos = synth.Trace.Count(line => line.Contains(" goto "));
    if (trace) File.WriteAllLines(Path.Combine(args[2], name + ".events.txt"), synth.Trace);
    string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    return new(error, errorType, failure, completed, synth.OutputFrames, synth.GameFrames,
        digest, nonfinite, lastNonzero, synth.Ended, synth.Peak, synth.Clipped, synth.MaxPcm,
        observedVoices, synth.Stolen, synth.Rejected, notes, gotos, timer.Elapsed.TotalSeconds);
}

static void WriteWav(string path, short[] samples)
{
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write("RIFF"u8);
    writer.Write(36 + samples.Length * 2);
    writer.Write("WAVEfmt "u8);
    writer.Write(16);
    writer.Write((short)1);
    writer.Write((short)2);
    writer.Write(Synth.Rate);
    writer.Write(Synth.Rate * 4);
    writer.Write((short)4);
    writer.Write((short)16);
    writer.Write("data"u8);
    writer.Write(samples.Length * 2);
    foreach (short sample in samples) writer.Write(sample);
}

record RenderResult(string? Error, string? ErrorType, object? Failure, int CompletedFrames,
    long OutputFrames, long GameFrames, string Hash, long Nonfinite, int LastNonzeroFrame,
    bool Ended, double Peak, long Clipped, int MaxPcm, int ObservedMaxActiveVoices,
    long Stolen, long Rejected, int Notes, int Gotos, double ElapsedSeconds);
