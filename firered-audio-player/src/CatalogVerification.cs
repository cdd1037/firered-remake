using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace FireRedAudio;

public static class CatalogVerification
{
    public static void Run(Resources resources, string catalogPath)
    {
        var catalog = NormalCryCatalog.FromFile(catalogPath);
        if (catalog.Count != 386) throw new Exception("Wrong catalog denominator");
        var bad = File.ReadAllBytes(catalogPath);
        bad[^1] ^= 1;
        Reject(() => NormalCryCatalog.FromBytes(bad));
        Reject(() => NormalCryCatalog.FromBytes(new byte[100]));
        var synth = new Synth(resources, AudioProfile.SourceFiltered, catalog) { EnableTrace = false };
        foreach (int invalid in new[] { -1, 0, 252, 276, 412, 413, int.MaxValue })
            Reject(() => synth.StartNormalCry(invalid));
        Reject(() => synth.StartNormalCry(1, mode: 1));
        synth.StartNormalCry(1);
        Reject(() => synth.StartNormalCry(412));
        synth.Render(new float[1024]);
        if (!synth.CryPlaying) throw new Exception("Rejected request mutated current cry");
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(catalogPath)!, ".."));
        using var baseline = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "results/phase4/render-coverage.json")));
        int count = 0;
        var timer = Stopwatch.StartNew();
        foreach (var row in baseline.RootElement.GetProperty("rows").EnumerateArray())
        {
            int species = row.GetProperty("internalId").GetInt32();
            var engine = new Synth(resources, AudioProfile.SourceFiltered, catalog) { EnableTrace = false };
            engine.PrepareNormalCry(species);
            engine.StartNormalCry(species, duckBgm: false);
            var buffer = new float[514];
            var bytes = new byte[4 * Synth.Rate * 4];
            for (int done = 0; done < 4 * Synth.Rate;)
            {
                int frames = Math.Min(257, 4 * Synth.Rate - done);
                engine.Render(buffer.AsSpan(0, frames * 2));
                for (int j = 0; j < frames * 2; j++)
                {
                    if (!float.IsFinite(buffer[j])) throw new Exception("Nonfinite catalog output");
                    short sample = (short)Math.Clamp(Math.Round(buffer[j] * 32767), short.MinValue, short.MaxValue);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan((done * 2 + j) * 2), sample);
                }
                done += frames;
            }
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (hash != row.GetProperty("hash").GetString() || !engine.Ended || engine.Clipped != 0 || engine.MaxPcm != 1)
                throw new Exception($"Public catalog render differs for {species}");
            synth.PrepareNormalCry(species);
            count++;
        }
        if (count != 386 || synth.PreparedCryCount != 386 || synth.PreparedCryBytes != 3136467)
            throw new Exception("Cry cache accounting mismatch");
        Console.WriteLine(JsonSerializer.Serialize(new { rendered = count, exactAgainstResearch = count,
            catalogBytes = NormalCryCatalog.FileBytes, cacheSampleBytes = synth.PreparedCryBytes,
            elapsedSeconds = timer.Elapsed.TotalSeconds, invalidRequestsRejected = true }));
    }

    static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException) { return; }
        throw new Exception("Invalid catalog request was accepted");
    }
}
