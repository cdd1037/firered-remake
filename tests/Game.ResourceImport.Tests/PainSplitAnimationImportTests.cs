namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class PainSplitAnimationImportTests
{
    [TestMethod]
    public Task PainSplitRetainsFiniteFramesPairedWavesAndExactIndexedArt()=>PythonResourceCheck.Run("scripts/checks/test-pain-split-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
