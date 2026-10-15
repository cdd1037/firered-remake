using System.Diagnostics;
using FireRed.Tests;

namespace FireRed.Tests.ResourceImport;
[TestClass, TestCategory("ResourceImport")]
public sealed class ResourceImportTests
{
    [TestMethod]
    public Task FameChecker() => Run("test-fame-checker-importer.py", "--git-dir", TestAssets.Source);
    [TestMethod]
    public Task NationalDex() => Run("test-national-dex-source.py");
    [TestMethod]
    public Task Learnsets() => Run("test-learnset-import.py");
    [TestMethod]
    public Task CompactUiJson() => Run("test-compact-ui-json.py");
    [TestMethod]
    public Task ItemCatalog() => Run("test-item-catalog-import.py");

    [TestMethod]
    public Task CurrentItems() => Run("test-current-items-import.py");
    [TestMethod]
    public async Task MachineCatalog()
    {
        await Run("test-machine-catalog-import.py");
        await Run("test-machine-importers.py");
        await Run("test-late-machine-importers.py");
        await Run("test-shop-machine-importers.py");
    }
    [TestMethod]
    public Task SourceHit() => Run("test-source-hit-import.py", Path.Combine(TestAssets.Output(), "results"));
    [TestMethod]
    public Task TeachyTv() => Run("test-teachy-tv-importer.py", "--git-dir", TestAssets.Source);
    [TestMethod]
    public Task TrainerFanClub() => Run("test-trainer-fan-club-importer.py", "--project-root", TestAssets.Root, "--dependency-root", TestAssets.Root, "--reference-root", TestAssets.Source, "--output", Path.Combine(TestAssets.Output(), "result.json"));
    [TestMethod]
    public Task VsSeeker() => Run("test-vs-seeker-importer.py", "--git-dir", TestAssets.Source);
    public TestContext TestContext { get; set; } = null !;

    private async Task Run(string script, params string[] args)
    {
        var interpreter = Environment.GetEnvironmentVariable("FIRERED_PYTHON")
            ?? (OperatingSystem.IsWindows() ? "python" : "python3");
        var info = new ProcessStartInfo(interpreter)
        {
            WorkingDirectory = TestAssets.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        info.Environment["PYTHONIOENCODING"] = "utf-8";
        info.ArgumentList.Add(Path.Combine(TestAssets.Root, "scripts", script));
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        using var p = Process.Start(info) ?? throw new InvalidOperationException("Python could not start");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        try
        {
            await p.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            p.Kill(true);
            await p.WaitForExitAsync();
            Assert.Fail("Resource check timed out: " + script);
        }

        var output = await stdout + await stderr;
        TestContext.WriteLine(output);
        Assert.AreEqual(0, p.ExitCode, script + "\n" + output);
        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Empty resource check output");
    }
}
