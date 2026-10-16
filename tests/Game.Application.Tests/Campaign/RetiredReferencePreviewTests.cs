using Game.Application;

namespace FireRed.Tests.Application.Campaign;

[TestClass]
public sealed class RetiredReferencePreviewTests
{
    [TestMethod]
    public void OldPalletReferencePreviewCannotSelectRawRuntime()
    {
        var environment = new Dictionary<string, string?> { ["FIRERED_PROJECT_ROOT"] = "/generated-game" };
        var error = Assert.Throws<InvalidOperationException>(() => GameStartupSelection.Resolve(["--pallet"], environment));
        Assert.AreEqual("startup_mode_retired: --pallet", error.Message);
        Assert.AreEqual(GameStartupMode.Current, GameStartupSelection.Resolve([], environment).Mode);
    }

    [TestMethod]
    public void OriginalSourceEnvironmentCannotReenableReferencePreview()
    {
        foreach (var name in new[] { "FIRERED_REFERENCE_ROOT", "FIRERED_REFERENCE_SOURCE" })
        {
            var environment = new Dictionary<string, string?> { ["FIRERED_PROJECT_ROOT"] = "/generated-game", [name] = "/raw" };
            var error = Assert.Throws<InvalidOperationException>(() => GameStartupSelection.Resolve([], environment));
            Assert.AreEqual("startup_selector_retired: " + name, error.Message);
        }
    }
}
