namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class NativePresentationImportTests
{
    [TestMethod]
    [DataRow("test-recovery-animation-import.py")]
    [DataRow("test-reaction-animation-import.py")]
    [DataRow("test-weather-animation-import.py")]
    [DataRow("test-overlay-animation-import.py")]
    [DataRow("test-impact-emission-animation-import.py")]
    [DataRow("test-orbital-particle-animation-import.py")]
    [DataRow("test-fire-seed-animation-import.py")]
    [DataRow("test-physical-animation-import.py")]
    [DataRow("test-elemental-animation-import.py")]
    [DataRow("test-impact-adapter-animation-import.py")]
    [DataRow("test-projectile-animation-import.py")]
    [DataRow("test-itemfinder-presentation.py")]
    [DataRow("test-npc-trade-scene-import.py")]
    [DataRow("test-party-screen.py")]
    [DataRow("test-summary-screen.py")]
    public Task GeneratedNativePresentationMatchesSource(string script) =>
        PythonResourceCheck.Run("scripts/checks/" + script, timeout: TimeSpan.FromMinutes(3));
}
