using System.Diagnostics;
using FireRed.Tests;

namespace FireRed.Tests.ResourceImport;
[TestClass, TestCategory("ResourceImport")]
public sealed class ResourceImportTests
{
    [TestMethod]
    public Task ScriptLayout() => Run("test-script-layout.py");
    [TestMethod]
    public Task StoryMenus() => Run("test-story-menus.py");
    [TestMethod]
    public Task SingleFileImportPublishing() => Run("test-single-file-imports.py");
    [TestMethod]
    public Task ImageIo() => Run("test-image-io.py");
    [TestMethod]
    public Task ImporterPngSemantics() => Run("test-importer-png-semantics.py");
    [TestMethod]
    public Task MapAtlases() => Run("test-map-atlases.py");
    [TestMethod]
    public Task Route15GateSourceAndRegeneration() => Run("test-route15-gate-import.py");
    [TestMethod]
    public Task MapImporterUnification() => Run("verify-map-importer-unification.py");
    [TestMethod]
    public Task CeladonApproachEncounters() => Run("test-celadon-approach-encounters.py");
    [TestMethod]
    public Task RegionalEncounters() => Run("test-regional-encounter-imports.py");
    [TestMethod]
    public Task MtEmberSummitEncounters() => Run("test-mt-ember-summit-encounters.py");
    [TestMethod]
    public Task FieldTilesetRegeneration() => Run("test-field-tilesets.py");
    [TestMethod]
    public Task FameChecker() => Run("test-fame-checker-importer.py", "--git-dir", TestAssets.Source);
    [TestMethod]
    public Task NationalDex() => Run("test-national-dex-source.py");
    [TestMethod]
    public Task Learnsets() => Run("test-learnset-import.py");
    [TestMethod]
    public async Task PokemonCollection()
    {
        await Run("test-game-pokemon.py");
        await Run("test-pokemon-collection-presentation.py");
    }
    [TestMethod]
    public Task CompactUiJson() => Run("test-compact-ui-json.py");
    [TestMethod]
    public Task ItemCatalog() => Run("test-item-catalog-import.py");

    [TestMethod]
    public Task RuntimeProofRetirement() => Run("test-runtime-proof-retirement.py");
    [TestMethod]
    public Task PresentationImportMetadata() => Run("test-presentation-import-metadata.py");
    [TestMethod]
    public Task RuntimeJsonEncoding() => Run("test-runtime-json.py");
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
    public async Task SourceHit()
    {
        using var output = TestAssets.TemporaryOutput();
        await Run("test-source-hit-import.py", Path.Combine(output.Path, "results"));
    }
    [TestMethod]
    public Task TeachyTv() => Run("test-teachy-tv-importer.py", "--git-dir", TestAssets.Source);
    [TestMethod]
    public async Task TrainerFanClub()
    {
        using var output = TestAssets.TemporaryOutput();
        await Run("test-trainer-fan-club-importer.py", "--project-root", TestAssets.Root, "--dependency-root", TestAssets.Root, "--reference-root", TestAssets.Source, "--output", Path.Combine(output.Path, "result.json"));
    }
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
        info.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        info.ArgumentList.Add(Path.Combine(TestAssets.Root, "scripts", "checks", script));
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
