using System.Diagnostics;

namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class WaterPathHouseImportTests
{
    [TestMethod]
    public async Task BothHouseMapsDoorsAndFreshRenderedPixelsMatchPinnedSource()
    {
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("FIRERED_PYTHON") ?? (OperatingSystem.IsWindows() ? "python" : "python3"))
        { WorkingDirectory = TestAssets.Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(Path.Combine(TestAssets.Root, "scripts/checks/test-water-path-house-import.py"));
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw; }
        var output = await stdout + await stderr;
        Assert.AreEqual(0, process.ExitCode, output);
        Assert.IsFalse(string.IsNullOrWhiteSpace(output));
    }
}
