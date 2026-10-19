namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class InventorySourceTextTests
{
    [TestMethod]
    [DataRow("{STR_VAR_1} is\\nselected.", "{STR_VAR_2}\\p$", null, "{STR_VAR_2}\\p$ is\nselected.")]
    [DataRow("Withdrew {STR_VAR_2}\\n{STR_VAR_1}(s).", "{STR_VAR_2}", "two", "Withdrew two\n{STR_VAR_2}(s).")]
    [DataRow("There are no items.{PAUSE_UNTIL_PRESS}", null, null, "There are no items.")]
    public void SourceControlsPrecedeLiteralItemBindings(string template, string? item, string? quantity, string expected)
    {
        var source = new Dictionary<string, string> { ["text"] = template };
        Assert.AreEqual(expected, SourceInventoryText.Format(source, "text", item, quantity));
    }

    [TestMethod]
    [DataRow("{STR_VAR_1} is selected.")]
    [DataRow("Unknown {OTHER}.")]
    [DataRow("First\\pSecond")]
    public void MissingBindingsAndMultiplePagesFail(string template) =>
        Assert.ThrowsExactly<InvalidDataException>(() => SourceInventoryText.Format(
            new Dictionary<string, string> { ["text"] = template }, "text"));
}
