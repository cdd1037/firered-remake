namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class LoreleiHouseImportTests
{
    [TestMethod]
    public Task HouseCollectionAndIceRulesMatchPinnedSource() =>
        PythonResourceCheck.Run("scripts/imports/story/import-world-field-rules.py",
            ["--raw", TestAssets.Source, "--region", "four-island", "--check"],
            timeout: TimeSpan.FromMinutes(1));
}
