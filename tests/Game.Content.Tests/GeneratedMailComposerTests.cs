using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedMailComposerTests
{
    [TestMethod]
    public void VocabularyAndProgressionSelectionNeedOnlyGeneratedGameData()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-mail-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "content/generated/data/services/mail-composer");
        Directory.CreateDirectory(output);
        try
        {
            foreach (var name in new[] { "catalog.json", "contracts.json" })
                File.Copy(Path.Combine(TestAssets.Root, "content/generated/data/services/mail-composer", name), Path.Combine(output, name));
            var catalog = PretMailComposer.Load(root).Catalog;
            Assert.AreEqual(1813, catalog.Words.Count);
            Assert.AreEqual(22, catalog.Groups.Length);
            Assert.AreEqual(27, catalog.Alphabetical.Length);
            var locked = new SourceEasyChatSelection(false, false, ImmutableHashSet<int>.Empty);
            Assert.AreEqual(16, SourceEasyChatRules.SelectableGroups(catalog, locked).Length);
            var unlocked = new SourceEasyChatSelection(true, true, ImmutableHashSet.Create(63));
            Assert.AreEqual(21, SourceEasyChatRules.SelectableGroups(catalog, unlocked).Length);
            foreach (var group in catalog.Groups)
                CollectionAssert.AreEqual(group.WordValues.Where(v => SourceEasyChatRules.IsSelectable(catalog, unlocked, v)).ToArray(),
                    SourceEasyChatRules.SelectableWords(catalog, unlocked, group.Id).Select(w => w.Value).ToArray());
            foreach (var bucket in catalog.Alphabetical)
                CollectionAssert.AreEqual(bucket.Alternatives.Select(a => a.Where(v => SourceEasyChatRules.IsSelectable(catalog, unlocked, v)).Select(v => (ushort?)v).FirstOrDefault())
                    .Where(v => v.HasValue).Select(v => v!.Value).ToArray(),
                    SourceEasyChatRules.AlphabeticalWords(catalog, unlocked, bucket.Id).Select(w => w.Value).ToArray());
            Assert.IsNull(SourceEasyChatRules.ValidateWords(catalog, SourceEasyChatRules.BlankWords));
            Assert.AreEqual("mail_message_empty", SourceEasyChatRules.ValidateWords(catalog, SourceEasyChatRules.BlankWords, true));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public void RuntimeValidatesMembershipAndEncodingWithoutHistoricalInventoryCounts()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-mail-schema-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "content/generated/data/services/mail-composer");
        Directory.CreateDirectory(output);
        try
        {
            var source = Path.Combine(TestAssets.Root, "content/generated/data/services/mail-composer");
            var catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(source, "catalog.json")))!;
            var contracts = JsonNode.Parse(File.ReadAllText(Path.Combine(source, "contracts.json")))!;
            var reserved = contracts["profileDefinition"]!["defaultWords"]!.AsArray()
                .Concat(contracts["profileDefinition"]!["specialPhrase"]!.AsArray()).Select(v => v!.GetValue<int>()).ToHashSet();
            var words = catalog["words"]!.AsArray();
            var removed = words.First(w => !reserved.Contains(w!["value"]!.GetValue<int>()))!;
            int value = removed["value"]!.GetValue<int>(), count = words.Count;
            words.Remove(removed);
            foreach (var group in catalog["groups"]!.AsArray()) RemoveValue(group!["wordValues"]!.AsArray(), value);
            foreach (var bucket in catalog["alphabetical"]!.AsArray())
            {
                var alternatives = bucket!["alternatives"]!.AsArray();
                for (int i = alternatives.Count - 1; i >= 0; i--)
                {
                    RemoveValue(alternatives[i]!.AsArray(), value);
                    if (alternatives[i]!.AsArray().Count == 0) alternatives.RemoveAt(i);
                }
            }
            var characters = contracts["nameCharacters"]!.AsObject();
            foreach (var key in characters.Select(p => p.Key).Where(k => k is not (" " or "A")).ToArray()) characters.Remove(key);
            void Write()
            {
                File.WriteAllText(Path.Combine(output, "catalog.json"), catalog.ToJsonString());
                File.WriteAllText(Path.Combine(output, "contracts.json"), contracts.ToJsonString());
            }
            Write(); var loaded = PretMailComposer.Load(root).Catalog;
            Assert.AreEqual(count - 1, loaded.Words.Count); Assert.AreEqual(2, loaded.NameCharacters.Count);
            Assert.IsFalse(loaded.Words.ContainsKey((ushort)value));
            var members = catalog["groups"]![0]!["wordValues"]!.AsArray(); members.Add(value); Write();
            Assert.ThrowsExactly<InvalidDataException>(() => PretMailComposer.Load(root));
            members.RemoveAt(members.Count - 1); characters["A"] = 250; Write();
            Assert.ThrowsExactly<InvalidDataException>(() => PretMailComposer.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }

    private static void RemoveValue(JsonArray values, int value)
    {
        for (int i = values.Count - 1; i >= 0; i--) if (values[i]!.GetValue<int>() == value) values.RemoveAt(i);
    }

}
