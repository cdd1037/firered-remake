namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class SolarPetalAnimationImportTests
{
    [TestMethod]
    public Task SolarPincerSourceClosureAndBattleBackgroundSets()=>PythonResourceCheck.Run("scripts/checks/test-solar-pincer-animation-import.py",timeout:TimeSpan.FromMinutes(2));
    [TestMethod]
    public Task PetalIceSharedFrameAffineAndRetainedCounterInputs()=>PythonResourceCheck.Run("scripts/checks/test-petal-ice-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
