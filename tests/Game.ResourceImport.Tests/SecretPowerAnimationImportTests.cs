namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class SecretPowerAnimationImportTests
{
    [TestMethod]
    public Task SecretPowerUsesSourceAuthenticatedBoundedProgramTails()=>PythonResourceCheck.Run("scripts/checks/test-secret-power-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
