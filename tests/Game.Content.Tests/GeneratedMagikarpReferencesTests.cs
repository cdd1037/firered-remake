using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedMagikarpReferencesTests
{
    private const string DirectoryPath = "content/generated/data/services/magikarp-gift";
    private const string DocumentName = "magikarp-contract.json";

    [TestMethod]
    public void SaleReferencesLoadWithoutManifestAndResolveToCompiledStory()
    {
        WithDocument(document =>
        {
            CollectionAssert.AreEquivalent(new[] { "schemaVersion", "commonScripts", "commonTexts" },
                document.Select(property => property.Key).ToArray());
        }, root =>
        {
            var references = PretMagikarpContract.Load(root);
            var story = PretStoryRuntime.Load(TestAssets.Root);
            CollectionAssert.AreEqual(new[]
            {
                "EventScript_GetGiftMonPartySlot", "EventScript_TransferredToPC",
                "EventScript_TransferredSomeonesPC", "EventScript_TransferredSomeonesPCBoxFull",
                "EventScript_TransferredBillsPC", "EventScript_TransferredBillsPCBoxFull",
                "Std_MsgboxDefault", "Std_MsgboxYesNo"
            }, references.RequiredCommonScriptLabels.ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "Text_GiveNicknameToThisMon", "Text_MonSentToBoxInSomeonesPC",
                "Text_MonSentToBoxInBillsPC", "Text_MonSentToBoxSomeonesBoxFull",
                "Text_MonSentToBoxBillsBoxFull"
            }, references.RequiredCommonTextLabels.ToArray());
            foreach (var label in references.RequiredCommonScriptLabels)
                Assert.IsNotNull(story.Compilation.FindProgram(label), label);
            foreach (var label in references.RequiredCommonTextLabels)
                Assert.IsTrue(story.TextBank.ContainsKey(label), label);
            Assert.IsFalse(File.Exists(Path.Combine(root, DirectoryPath, "manifest.json")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        });
    }

    [TestMethod]
    public void HistoricalManifestCountsAreNotRuntimeInputs()
    {
        WithDocument(_ => { }, root =>
        {
            var expected = PretMagikarpContract.Load(root);
            var manifest = Path.Combine(root, DirectoryPath, "manifest.json");
            foreach (var legacy in new[]
            {
                "{\"schemaVersion\":1,\"scriptBlockCount\":0,\"sourceFunctionCount\":0}",
                "{\"schemaVersion\":1,\"scriptBlockCount\":999,\"sourceFunctionCount\":999}",
                "This obsolete receipt is not runtime JSON."
            })
            {
                File.WriteAllText(manifest, legacy);
                var actual = PretMagikarpContract.Load(root);
                CollectionAssert.AreEqual(expected.RequiredCommonScriptLabels.ToArray(), actual.RequiredCommonScriptLabels.ToArray());
                CollectionAssert.AreEqual(expected.RequiredCommonTextLabels.ToArray(), actual.RequiredCommonTextLabels.ToArray());
            }
        });
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("empty")]
    [DataRow("duplicate")]
    [DataRow("blank")]
    [DataRow("non-string")]
    [DataRow("invalid-symbol")]
    [DataRow("overlapping")]
    [DataRow("schema")]
    public void MalformedReferenceContractsFail(string mutation)
    {
        WithDocument(document =>
        {
            var scripts = document["commonScripts"]!.AsArray();
            switch (mutation)
            {
                case "missing": document.Remove("commonTexts"); break;
                case "empty": document["commonTexts"] = new JsonArray(); break;
                case "duplicate": scripts.Add(scripts[0]!.DeepClone()); break;
                case "blank": scripts[0] = ""; break;
                case "non-string": scripts[0] = 1; break;
                case "invalid-symbol": scripts[0] = "not a story label"; break;
                case "overlapping": document["commonTexts"]![0] = scripts[0]!.DeepClone(); break;
                case "schema": document["schemaVersion"] = 0; break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        }, root => Assert.ThrowsExactly<InvalidDataException>(() => PretMagikarpContract.Load(root)));
    }

    private static void WithDocument(Action<JsonObject> mutate, Action<string> check)
    {
        var root = TestAssets.Output();
        var directory = Path.Combine(root, DirectoryPath);
        Directory.CreateDirectory(directory);
        try
        {
            var document = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, DirectoryPath, DocumentName)))!.AsObject();
            mutate(document);
            File.WriteAllText(Path.Combine(directory, DocumentName), document.ToJsonString());
            check(root);
        }
        finally { Directory.Delete(root, true); }
    }
}
