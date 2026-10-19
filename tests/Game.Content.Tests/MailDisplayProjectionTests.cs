using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class MailDisplayProjectionTests
{
    private static PretMailComposer Composer => TestAssets.Cached(() => PretMailComposer.Load(TestAssets.Root, SourceTextPack.Load(null)));
    private static IReadOnlyDictionary<string, string> Battle => TestAssets.Cached(() => PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null)));
    private static PretPokedexMenu Pokemon => TestAssets.Cached(() => PretPokedexMenu.Load(TestAssets.Root, SourceTextPack.Load(null)));
    private static PretMetronomeMoveCatalog Moves => TestAssets.Cached(() => PretMetronomeMoveCatalog.Load(TestAssets.Root, SourceTextPack.Load(null)));

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void SparseWordOverrideChangesOnlyDisplayAndPreservesUnlockAndSourceOrder(bool gameClear, bool nationalDex)
    {
        using var output = TestAssets.TemporaryOutput();
        WriteGroup(output.Path, "easy-chat-words", new() { ["3613"] = "SAME", ["4128"] = "SAME", ["1113"] = "{PK}ALT" });
        var actual = PretMailComposer.Load(TestAssets.Root, SourceTextPack.Load(output.Path)).Catalog;
        var original = Composer.Catalog;
        Assert.AreEqual(original, actual with { Words = original.Words, Texts = original.Texts,
            Templates = original.Templates, ProfileDefinition = original.ProfileDefinition,
            Groups = original.Groups, Alphabetical = original.Alphabetical, NameCharacters = original.NameCharacters });
        Assert.AreEqual(original.Words.Count, actual.Words.Count);
        foreach (var (id, word) in original.Words)
        {
            string expected = id is 3613 or 4128 ? "SAME" : id == 1113 ? "{PK}ALT" : word.Text;
            Assert.AreEqual(word with { Text = expected }, actual.Words[id]);
        }
        CollectionAssert.AreEqual(original.ProfileDefinition!.DefaultWords.ToArray(), actual.ProfileDefinition!.DefaultWords.ToArray());
        CollectionAssert.AreEqual(original.ProfileDefinition!.SpecialPhrase.ToArray(), actual.ProfileDefinition!.SpecialPhrase.ToArray());
        CollectionAssert.AreEquivalent(original.NameCharacters.ToArray(), actual.NameCharacters.ToArray());
        var selection = new SourceEasyChatSelection(gameClear, nationalDex, ImmutableHashSet.Create(63, 410));
        CollectionAssert.AreEqual(SourceEasyChatRules.SelectableGroups(original, selection).Select(g => g.Id).ToArray(),
            SourceEasyChatRules.SelectableGroups(actual, selection).Select(g => g.Id).ToArray());
        for (int group = 0; group < original.Groups.Length; group++)
        {
            CollectionAssert.AreEqual(original.Groups[group].WordValues.ToArray(), actual.Groups[group].WordValues.ToArray());
            CollectionAssert.AreEqual(SourceEasyChatRules.SelectableWords(original, selection, group).Select(w => w.Value).ToArray(),
                SourceEasyChatRules.SelectableWords(actual, selection, group).Select(w => w.Value).ToArray());
        }
        for (int bucket = 0; bucket < original.Alphabetical.Length; bucket++)
        {
            CollectionAssert.AreEqual(original.Alphabetical[bucket].Alternatives.Select(a => string.Join(',', a)).ToArray(),
                actual.Alphabetical[bucket].Alternatives.Select(a => string.Join(',', a)).ToArray());
            CollectionAssert.AreEqual(SourceEasyChatRules.AlphabeticalWords(original, selection, bucket).Select(w => w.Value).ToArray(),
                SourceEasyChatRules.AlphabeticalWords(actual, selection, bucket).Select(w => w.Value).ToArray());
        }
        foreach (ushort id in original.Words.Keys)
            Assert.AreEqual(SourceEasyChatRules.IsSelectable(original, selection, id), SourceEasyChatRules.IsSelectable(actual, selection, id));
        Assert.IsNull(SourceEasyChatRules.ValidateWords(actual, Composer.Catalog.ProfileDefinition!.DefaultWords, 4, true));
        Assert.AreEqual("", SourceEasyChatRules.WordText(actual, ushort.MaxValue));
        Assert.AreEqual("???", SourceEasyChatRules.WordText(actual, 65534));
        Assert.IsFalse(Directory.Exists(Path.Combine(output.Path, "content")));
    }

    [TestMethod]
    [DataRow(0, 63, "species-names", "63")]
    [DataRow(21, 63, "species-names", "63")]
    [DataRow(18, 71, "move-names", "pret:MOVE_ABSORB")]
    [DataRow(19, 51, "move-names", "pret:MOVE_ACID")]
    public void NameWordsReuseCurrentCatalogsBySourceIdentity(int group, int index, string textGroup, string nameId)
    {
        using var output = TestAssets.TemporaryOutput();
        WriteGroup(output.Path, textGroup, new() { [nameId] = "SHARED NAME" });
        var pack = SourceTextPack.Load(output.Path);
        var pokemon = PretPokedexMenu.Load(TestAssets.Root, pack);
        var moves = PretMetronomeMoveCatalog.Load(TestAssets.Root, pack);
        var actual = Composer.WithDisplayNames(pokemon, moves);
        ushort selected = checked((ushort)((group << 9) | index));
        Assert.AreEqual("SHARED NAME", actual.Words[selected].Text);
        Assert.AreNotEqual(Composer.Catalog.Words[selected].Text, actual.Words[selected].Text);
        Assert.AreEqual(Composer.Catalog, actual with { Words = Composer.Catalog.Words });
        int projected = 0;
        foreach (var (id, word) in Composer.Catalog.Words)
        {
            Assert.AreEqual(word, actual.Words[id] with { Text = word.Text });
            if (word.Group == actual.SeenPokemonGroup || word.Group == actual.NationalPokemonGroup)
            { Assert.AreEqual(pokemon.Entries[word.Index].Name, actual.Words[id].Text); projected++; }
            else if (word.Group is 18 or 19)
            {
                var move = moves.Moves.Values.Single(m => m.SourceMoveId == word.Index);
                Assert.AreEqual(moves.MoveNames[move.Id], actual.Words[id].Text); projected++;
            }
            else Assert.AreEqual(word.Text, actual.Words[id].Text);
        }
        Assert.AreEqual(807, projected, "The generated fixture has 453 species words and 354 move words.");
    }

    [TestMethod]
    [DataRow("easy-chat-words", "65534", "UNKNOWN")]
    [DataRow("easy-chat-words", "63", "SPECIES")]
    [DataRow("easy-chat-words", "10815", "SPECIES")]
    [DataRow("easy-chat-words", "9287", "MOVE")]
    [DataRow("easy-chat-words", "9779", "MOVE")]
    [DataRow("easy-chat-words", "3613", " ")]
    [DataRow("easy-chat-words", "1113", "LOST TOKEN")]
    [DataRow("easy-chat-words", "3613", "ADDED{PK}")]
    [DataRow("easy-chat-words", "03613", "NONCANONICAL ID")]
    [DataRow("easy-chat-groups", "EC_GROUP_UNKNOWN", "LABEL")]
    [DataRow("easy-chat-groups", "alphabet:27", "LABEL")]
    [DataRow("easy-chat-groups", "alphabet:00", "LABEL")]
    [DataRow("easy-chat-groups", "alphabet:0", " ")]
    [DataRow("easy-chat-groups", "EC_GROUP_TRAINER", "LABEL{PK}")]
    public void InvalidDisplayOverridesFailAtTheRealContentBoundary(string group, string id, string text)
    {
        using var output = TestAssets.TemporaryOutput();
        WriteGroup(output.Path, group, new() { [id] = text });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => PretMailComposer.Load(TestAssets.Root, SourceTextPack.Load(output.Path)));
        StringAssert.Contains(error.Message, group + "/" + id);
    }

    [TestMethod]
    [DataRow(0, 63)]
    [DataRow(18, 71)]
    public void InvalidNameWordSourceIndexCannotSilentlyFallBackToGeneratedEnglish(int group, int index)
    {
        using var output = TestAssets.TemporaryOutput();
        const string relative = "content/generated/data/services/mail-composer/";
        TestAssets.CopyFiles(output.Path, [relative + "catalog.json", relative + "contracts.json"]);
        int oldValue = (group << 9) | index, newValue = (group << 9) | 511;
        TestAssets.EditJson(output.Path, relative + "catalog.json", document =>
        {
            var word = document["words"]!.AsArray().Single(w => w!["value"]!.GetValue<int>() == oldValue)!;
            word["index"] = 511; word["value"] = newValue;
            Replace(document["groups"]![group]!["wordValues"]!.AsArray());
            foreach (var bucket in document["alphabetical"]!.AsArray())
                foreach (var alternatives in bucket!["alternatives"]!.AsArray()) Replace(alternatives!.AsArray());
            void Replace(JsonArray values)
            {
                for (int i = 0; i < values.Count; i++) if (values[i]!.GetValue<int>() == oldValue) values[i] = newValue;
            }
        });
        var composer = PretMailComposer.Load(output.Path, SourceTextPack.Load(null));
        var error = Assert.ThrowsExactly<InvalidDataException>(() => composer.WithDisplayNames(Pokemon, Moves));
        StringAssert.Contains(error.Message, group == 0 ? "easy_chat_species_name_missing" : "easy_chat_move_name_missing");
    }

    [TestMethod]
    [DataRow("EC_GROUP_POKEMON_2")]
    [DataRow("EC_GROUP_MOVE_1")]
    [DataRow("alphabet:0")]
    public void GroupAndAlphabetHeadingsProjectWithoutReorderingWordBuckets(string key)
    {
        using var output = TestAssets.TemporaryOutput();
        WriteGroup(output.Path, "easy-chat-groups", new() { [key] = "SAME LABEL" });
        var original = Composer.Catalog;
        var actual = PretMailComposer.Load(TestAssets.Root, SourceTextPack.Load(output.Path)).Catalog;
        for (int index = 0; index < original.Groups.Length; index++)
        {
            var before = original.Groups[index]; var after = actual.Groups[index];
            Assert.AreEqual(before with { Name = before.Symbol == key ? "SAME LABEL" : before.Name },
                after with { WordValues = before.WordValues });
            CollectionAssert.AreEqual(before.WordValues.ToArray(), after.WordValues.ToArray());
        }
        for (int index = 0; index < original.Alphabetical.Length; index++)
        {
            var before = original.Alphabetical[index]; var after = actual.Alphabetical[index];
            Assert.AreEqual(before with { Name = "alphabet:" + before.Id == key ? "SAME LABEL" : before.Name },
                after with { Alternatives = before.Alternatives });
            CollectionAssert.AreEqual(before.Alternatives.Select(a => string.Join(',', a)).ToArray(),
                after.Alternatives.Select(a => string.Join(',', a)).ToArray());
        }
        foreach (var (id, word) in original.Words) Assert.AreEqual(word, actual.Words[id]);
    }

    public static IEnumerable<object[]> MailUiSymbols => new[]
    {
        "gText_From", "gText_Mailbox", "gText_DoWhatWithMail", "gText_SendMailToPC", "gText_MailSentToPC",
        "gText_PCMailboxFull", "gText_MailMessageWillBeLost", "gText_MailTakenFromPkmn",
        "gText_PkmnHoldingItemCantHoldMail", "gText_MailTransferredFromMailbox", "gText_TheresNoMailHere",
        "gText_WhatWouldYouLikeToDoWithPlayersMail", "gText_MessageWillBeLost", "gText_BagIsFull",
        "gText_MailReturnedToBagMessageErased", "gText_Read2", "gText_Take2", "gOtherText_Read",
        "gOtherText_MoveToBag", "gOtherText_Give2", "gOtherText_Exit", "gFameCheckerText_Cancel", "gText_GiveToWhichPokemon",
        "gText_CombineNineWordsOrPhrases", "gText_AndMakeAMessage", "gText_TheMailMessage", "gText_IsAsShownOkay",
        "gText_AllTextBeingEditedWill", "gText_BeDeletedThatOkay", "gText_StopGivingPkmnMail", "gText_Profile",
        "gText_CombineFourWordsOrPhrases", "gText_AndMakeYourProfile", "gText_YourProfile", "gText_QuitEditing"
    }.Select(symbol => new object[] { symbol });

    [TestMethod]
    [DynamicData(nameof(MailUiSymbols))]
    public void MailAndEditorUiReadSparseOverridesFromTheExistingBattleBank(string symbol)
    {
        using var output = TestAssets.TemporaryOutput();
        WriteGroup(output.Path, "battle", new() { [symbol] = "ALT " + Battle[symbol] });
        var actualBank = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(output.Path));
        var original = new SourceMailTextPresentation(Battle);
        var actual = new SourceMailTextPresentation(actualBank);
        Assert.AreEqual("ALT " + original.Text(symbol, "DONTAE"), actual.Text(symbol, "DONTAE"));
        Assert.AreEqual(original.Text("gText_Yes"), actual.Text("gText_Yes"));
        Assert.AreEqual(Battle.Count, actualBank.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EditorActionLabelsUseTheSourceControlLayoutEvenWhenLabelsCollide(bool replace)
    {
        using var output = TestAssets.TemporaryOutput();
        if (replace) WriteGroup(output.Path, "battle", new()
        { ["gText_DelAllCancelOk"] = "SAME{CLEAR_TO 0x57}SAME{CLEAR_TO 0xA4}SAME" });
        var presentation = new SourceMailTextPresentation(PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(output.Path)));
        CollectionAssert.AreEqual(replace ? new[] { "SAME", "SAME", "SAME" } : new[] { "DEL. ALL", "CANCEL", "OK" },
            presentation.EditorActionLabels.ToArray());
    }

    [TestMethod]
    [DataRow("DONTAE")]
    [DataRow("NAME{STR_VAR_1}\\n$")]
    [DataRow("{PAUSE_UNTIL_PRESS}{CLEAR_TO 0x57}")]
    public void AuthorIsInsertedOnceAfterSourceControlsAreDecoded(string author)
    {
        var presentation = new SourceMailTextPresentation(Battle);
        Assert.AreEqual("What would you like to do with\n" + author + "'s MAIL?",
            presentation.Text("gText_WhatWouldYouLikeToDoWithPlayersMail", author));
        Assert.AreEqual("The MAIL was sent to your PC.", presentation.Text("gText_MailSentToPC"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReaderProjectsSavedWordIdsWithoutChangingNpcMailPayload(bool replace)
    {
        using var output = TestAssets.TemporaryOutput();
        if (replace)
        {
            WriteGroup(output.Path, "easy-chat-words", new() { ["3613"] = "NOTE" });
            WriteGroup(output.Path, "species-names", new() { ["124"] = "ICE NAME" });
            WriteGroup(output.Path, "battle", new() { ["gText_From"] = "By " });
        }
        var pack = SourceTextPack.Load(output.Path);
        var catalog = PretMailComposer.Load(TestAssets.Root, pack).WithDisplayNames(PretPokedexMenu.Load(TestAssets.Root, pack), Moves);
        var npc = TestAssets.Cached(() => PretNpcTradeMail.Load(TestAssets.Root));
        var presentation = PretMailPresentation.Load(TestAssets.Root, battleText: PretBattleText.Load(TestAssets.Root, pack));
        var message = npc.Message;
        var lines = presentation.ReaderLines(message.ItemId, message.Words, word => SourceEasyChatRules.WordText(catalog, word));
        CollectionAssert.AreEqual(new[] { replace ? "NOTE A" : "THAT'S A", replace ? "HEALTHY ICE NAME" : "HEALTHY JYNX", "! BE", "KIND TO", "IT" },
            lines.Select(line => line.Text).ToArray());
        CollectionAssert.AreEqual(new[] { 35, 51, 67, 83, 99 }, lines.Select(line => line.Y).ToArray());
        Assert.IsTrue(lines.All(line => line.X == 32));
        Assert.AreEqual((replace ? "By " : "From ") + "DONTAE", presentation.SourceText.Text("gText_From") + message.AuthorName);
        Assert.AreSame(message, npc.Message);
        Assert.IsNull(SourceMailRules.ValidateMessage(message));
        Assert.IsNull(SourceEasyChatRules.ValidateWords(catalog, message.Words, true));
    }

    private static void WriteGroup(string root, string group, Dictionary<string, string> values)
    {
        string source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, group + ".json"), JsonSerializer.Serialize(values));
    }
}
