namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class SludgeSparkAnimationImportTests
{
    [TestMethod]
    public Task SludgeSparkSourceDataAndApprovedPolicyBoundaryRemainSeparate()=>
        PythonResourceCheck.Run("scripts/checks/test-sludge-spark-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
