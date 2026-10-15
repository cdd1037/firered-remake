using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class CampaignDiagnosticsTests
{
    [TestMethod]
    public void PlayerDiagnosticsNeverEvaluateFullStatePayload()
    {
        var diagnostics = new CampaignDiagnostics(null, null);
        Assert.IsFalse(diagnostics.Verbose);
        var payload = diagnostics.Payload(new { revision = 7, error = "rejected" },
            () => throw new InvalidOperationException("Full state must remain untouched."));
        Assert.AreEqual("{\"revision\":7,\"error\":\"rejected\"}", JsonSerializer.Serialize(payload));
        Assert.IsFalse(new CampaignDiagnostics("", "0").Verbose);
    }

    [TestMethod]
    public void EvidenceAndExplicitVerboseRetainDetailedPayload()
    {
        foreach (var diagnostics in new[] { new CampaignDiagnostics("capture", null), new CampaignDiagnostics(null, "1") })
        {
            Assert.IsTrue(diagnostics.Verbose);
            var calls = 0;
            var payload = diagnostics.Payload(new { revision = 7 }, () => { calls++; return new { state = new { revision = 7 } }; });
            Assert.AreEqual(1, calls);
            Assert.AreEqual("{\"state\":{\"revision\":7}}", JsonSerializer.Serialize(payload));
        }
    }
}
