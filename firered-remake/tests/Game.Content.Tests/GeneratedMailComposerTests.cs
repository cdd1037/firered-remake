using System.Collections.Immutable;
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
        var output = Path.Combine(root, "content/generated/mail-composer");
        Directory.CreateDirectory(output);
        try
        {
            foreach (var name in new[] { "catalog.json", "contracts.json" })
                File.Copy(Path.Combine(TestAssets.Root, "content/generated/mail-composer", name), Path.Combine(output, name));
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
}
