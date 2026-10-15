using System.Text.Json;
using Game.Domain.Capture;
using Game.Domain.Growth;
using Game.Domain.Randomness;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Domain;
[TestClass]
public abstract class GrowthCaptureFixture
{
    private protected JsonDocument Fixture(string name) => JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Fixtures("Game.Domain.Tests/GrowthCapture"), name)));
    private protected CaptureInput Input(int hp = 100, int rate = 45, CaptureBall ball = CaptureBall.Poke, CaptureStatus status = CaptureStatus.None) => new(100, hp, rate, ball, status);
    private protected void NoDraw(CaptureResult result, RngState before, CaptureOutcome outcome, string code)
    {
        Assert.AreEqual(outcome, result.Outcome);
        Assert.AreEqual(code, result.Diagnostic);
        Assert.AreEqual(before, result.Rng);
        Assert.AreEqual(0, result.RandomTrace.Length);
        Assert.AreEqual(0, result.ChecksPassed);
        Assert.IsTrue(result.Calculation is null && result.Branch is null && result.AnimationCode is null, "Rejected/unsupported requests must not fabricate a resolved calculation.");
    }
}
