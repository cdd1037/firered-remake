namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class FocusPunchAnimationImportTests
{
    [TestMethod]
    public Task FocusPunchClosesHitRootWithoutNewArtOrSetupClaim()=>PythonResourceCheck.Run("scripts/checks/test-focus-punch-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
