using FireRed.Tests;

namespace FireRed.Tests.ResourceImport;
[TestClass, TestCategory("ResourceImport")]
public sealed class ResourceImportTests
{
    [TestMethod]
    public Task ScriptLayout() => Run("test-script-layout.py");
    [TestMethod]
    public async Task SharedTrainerImporters()
    {
        await Run("test-trainer-import.py");
        await Run("test-trainer-portraits.py");
        await Run("test-trainer-display-import.py");
    }
    [TestMethod]
    public Task SharedPokemonPresentation() => Run("test-pokemon-import.py");
    [TestMethod]
    public Task StoryMenus() => Run("test-story-menus.py");
    [TestMethod]
    public Task SingleFileImportPublishing() => Run("test-single-file-imports.py");
    [TestMethod]
    public Task ImageIo() => Run("test-image-io.py");
    [TestMethod]
    public Task ChineseFontMasks() => Run("test-cjk-font.py");
    [TestMethod]
    public async Task ChineseSourceText()
    {
        await Run("test-source-text-collector.py");
        await Run("test-chinese-catalogs.py");
    }
    [TestMethod]
    public Task ImporterPngSemantics() => Run("test-importer-png-semantics.py");
    [TestMethod]
    public Task SharedItemUiImports() => Run("test-item-ui-library-imports.py");
    [TestMethod]
    public Task ExplicitImportInputs() => Run("test-import-input-closure.py");
    [TestMethod]
    public Task SuppliedItemSources() => Run("test-item-source-callers.py");
    [TestMethod]
    public Task MagikarpRuntimeReferences() => Run("test-magikarp-contract-import.py");
    [TestMethod]
    public Task SpecialPresentationImports() => Run("test-special-presentation-importers.py");
    [TestMethod]
    public Task MapAtlases() => Run("test-map-atlases.py");
    [TestMethod]
    public Task WorldSourceSemantics() => Run("test-world-pipeline-oracle.py");
    [TestMethod]
    public Task Route15GateSourceAndRegeneration() => Run("test-route15-gate-import.py");
    [TestMethod]
    public Task SharedMapImporters() => Run("test-map-consolidation.py");
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
    public Task CutGrassRegeneration() => Run("test-cut-grass-import.py");
    [TestMethod]
    public Task FameChecker() => Run("test-fame-checker-importer.py", "--git-dir", TestAssets.Source);
    [TestMethod]
    public Task SlotPresentation() => Run("test-slot-machine-presentation.py");
    [TestMethod]
    public Task NationalDex() => Run("test-national-dex-source.py");
    [TestMethod]
    public Task Learnsets() => Run("test-learnset-import.py");
    [TestMethod]
    public Task SnorlaxSharedCatalog() => Run("verify-snorlax-catalog.py");
    [TestMethod]
    public async Task PokemonCollection()
    {
        await Run("test-game-pokemon.py");
        await Run("test-pokemon-collection-presentation.py");
    }
    [TestMethod]
    public Task CompactUiJson() => Run("test-compact-ui-json.py");
    [TestMethod]
    public Task SharedRegionalItems() => Run("test-regional-item-effects.py");
    [TestMethod]
    public Task ItemCatalog() => Run("test-item-catalog-import.py");

    [TestMethod]
    public Task RuntimeProofRetirement() => Run("test-runtime-proof-retirement.py");
    [TestMethod]
    public Task SharedPresentationPixels() => Run("test-presentation-pixels.py");
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

    private Task Run(string script, params string[] args) =>
        PythonResourceCheck.Run(Path.Combine("scripts", "checks", script), args, context: TestContext);
}
