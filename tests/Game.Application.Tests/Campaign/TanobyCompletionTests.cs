using System.Collections.Immutable;
using System.Text.Json.Nodes;
using FireRed.Tests.Application.WildBattles;
using Game.Application;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class TanobyCompletionTests : WildBattleFixture
{
    private const string UnlockFlag = "FLAG_SYS_UNLOCKED_TANOBY_RUINS";
    private static readonly ImmutableArray<int> PressOrder = [7, 1, 5, 2, 6, 3, 4];

    private CampaignState Completed()
    {
        var state = FieldFixture();
        return state with { TanobyCompletion = new(PressOrder),
            Script = state.Script with { Flags = state.Script.Flags.SetItem(UnlockFlag, true) } };
    }

    [TestMethod]
    [DataRow("b049d0009685686732941369e62a9c04d345d5325035e463c28505804b600e1b")]
    [DataRow("retired-provenance-does-not-authorize-puzzle-progress")]
    public void LegacyReceiptKeepsGameplayAndChecksumButDoesNotResaveProvenance(string retiredValue)
    {
        var state = Completed();
        Assert.IsNull(validator.ValidatePlayerSave(state));
        var payload = JsonNode.Parse(CanonicalJson.Encode(state))!.AsObject();
        payload["TanobyCompletion"]!["SourceSha256"] = retiredValue;
        var legacy = SaveCodec.Encode(payload, checkpoint.Generation, validator.RulesVersion, content.ContentVersion);
        var loaded = SaveCodec.Decode<CampaignState>(legacy, validator.RulesVersion, content.ContentVersion,
            validate: validator.ValidatePlayerSave);
        Assert.AreEqual(checkpoint.Generation, loaded.Generation);
        CollectionAssert.AreEqual(CanonicalJson.Encode(state), CanonicalJson.Encode(loaded.State));
        CollectionAssert.AreEqual(PressOrder.ToArray(), loaded.State.TanobyCompletion!.SwitchBoulderIds.ToArray());

        var resumed = CampaignSession.RestoreCurrent(content, loaded.State, loaded.Generation);
        Assert.IsTrue(resumed.CanOpenFieldMenu, "Permanent puzzle progress must not own the field callback.");
        Assert.IsTrue(resumed.CanEditOptions);
        Assert.AreEqual(resumed.State.Party.Length > 1, resumed.CanReorderFieldParty);
        var saved = SaveCodec.Encode(resumed.State, resumed.Revision, resumed.RulesVersion, content.ContentVersion);
        var rewritten = JsonNode.Parse(saved)!;
        Assert.IsFalse(rewritten["Payload"]!["TanobyCompletion"]!.AsObject().ContainsKey("SourceSha256"));
        CollectionAssert.AreEqual(CanonicalJson.Encode(state), CanonicalJson.Encode(resumed.State));

        // Even retired metadata remains covered by the checksum in an old envelope.
        var corrupt = JsonNode.Parse(legacy)!;
        corrupt["Payload"]!["TanobyCompletion"]!["SourceSha256"] = retiredValue + "changed";
        Assert.ThrowsExactly<InvalidDataException>(() => SaveCodec.Decode<CampaignState>(
            CanonicalJson.Encode(corrupt), validator.RulesVersion, content.ContentVersion,
            validate: validator.ValidatePlayerSave));
    }

    [TestMethod]
    public void CompletionStillRequiresTheUnlockFlagAndAllSevenValidSwitchReceipts()
    {
        var state = Completed();
        foreach (ImmutableArray<int> invalid in new ImmutableArray<int>[] {
            default, [], [1, 2, 3, 4, 5, 6], [0, 2, 3, 4, 5, 6, 7], [1, 2, 3, 4, 5, 6, 8]
        }) {
            var changed = state with { TanobyCompletion = new(invalid) };
            Assert.AreEqual("save_tanoby_unlock_receipt", validator.ValidatePlayerSave(changed));
        }
        Assert.AreEqual("save_tanoby_unlock_receipt", validator.ValidatePlayerSave(state with { TanobyCompletion = null }));
        Assert.AreEqual("save_tanoby_unlock_receipt", validator.ValidatePlayerSave(state with {
            Script = state.Script with { Flags = state.Script.Flags.SetItem(UnlockFlag, false) }
        }));
        var unsolved = state with { TanobyCompletion = null,
            Script = state.Script with { Flags = state.Script.Flags.SetItem(UnlockFlag, false) } };
        Assert.IsNull(validator.ValidatePlayerSave(unsolved));
    }
}
