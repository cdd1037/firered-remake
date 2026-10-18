namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class PunchTraceAnimationImportTests
{
    [TestMethod]
    public Task PunchTraceNativeCommandsAndSafePolicyAdmissionRemainSeparate()=>
        PythonResourceCheck.Run("scripts/checks/test-punch-trace-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
