using System.Diagnostics;

namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class LoreleiHouseImportTests
{
    [TestMethod]
    public async Task HouseCollectionAndIceRulesMatchPinnedSource()
    {
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("FIRERED_PYTHON") ?? (OperatingSystem.IsWindows() ? "python" : "python3"))
        { WorkingDirectory = TestAssets.Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(Path.Combine(TestAssets.Root, "scripts/imports/story/import-world-field-rules.py"));
        info.ArgumentList.Add("--raw");
        info.ArgumentList.Add(Path.Combine(TestAssets.Root, "content/raw/pokefirered/037335f4"));
        info.ArgumentList.Add("--region");
        info.ArgumentList.Add("four-island");
        info.ArgumentList.Add("--check");
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
