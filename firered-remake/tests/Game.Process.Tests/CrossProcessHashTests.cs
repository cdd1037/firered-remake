using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Rules;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Processes;
[TestClass, TestCategory("Game")]
public sealed class CrossProcessHashTests
{
    [TestMethod]
    public void canonical_cold_process_hashes_match()
    {
        var parent = HashProbeScenario.HashProbe();
        var probes = Enumerable.Range(0, 3).Select(_ => StartHashProbe()).ToArray();
        Assert.IsTrue(probes.Select(p => p.ProcessId).Distinct().Count() == 3 && probes.All(p => p.ProcessId != Environment.ProcessId), "Hash test did not use distinct child processes.");
        Assert.IsTrue(probes.All(p => p.RuntimeMajor == 10 && p.StateHash == parent.StateHash && p.RoundTripHash == parent.StateHash && p.NormalizedSetHash == parent.NormalizedSetHash), "Cold processes or save round-trip changed a canonical semantic hash.");
        return;
    }

    private static HashProbeResult StartHashProbe()
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "HashProbe", "Game.Audit.HashProbe.dll"));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start the hash probe.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Cold-process hash probe timed out.");
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException("Hash probe failed: " + stderr.GetAwaiter().GetResult());
        return JsonSerializer.Deserialize<HashProbeResult>(stdout.GetAwaiter().GetResult()) ?? throw new InvalidDataException("Hash probe returned no evidence.");
    }

}
