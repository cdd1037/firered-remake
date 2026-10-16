namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class WorldFieldRulesImportTests
{
    [TestMethod]
    [DataRow("test-world-field-rules.py")]
    [DataRow("test-safari-features.py")]
    public Task LiveRulesDependOnOriginalSource(string script) =>
        PythonResourceCheck.Run(Path.Combine("scripts", "checks", script),
            timeout: TimeSpan.FromMinutes(1));
}
