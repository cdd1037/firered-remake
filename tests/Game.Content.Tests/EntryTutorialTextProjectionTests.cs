using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Tutorials;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class EntryTutorialTextProjectionTests
{
    private static IReadOnlyDictionary<string, string> Battle => TestAssets.Cached(() =>
        PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null)));
    private static string TeachyJson => File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/services/teachy-tv.json"));

    private static SourceTextPack Pack(string directory, string group, string id, string value)
    {
        string source = Path.Combine(directory, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, group + ".json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [id] = value }));
        return SourceTextPack.Load(directory);
    }

    private static string Display(string group, string id, SourceTextPack pack) => group switch
    {
        "help" => HelpCatalog.Load(TestAssets.Root, pack, Battle).CancelLabel,
        "opening" or "opening-names" => PretOpeningPresentation.Load(TestAssets.Root, pack, Battle).Texts[id],
        "teachy-tv" => PretTeachyTv.Load(TeachyJson, pack).Texts[id].EncodedText,
        _ => throw new ArgumentOutOfRangeException(nameof(group))
    };

    [TestMethod]
    [DataRow("help", "Help_Text_Cancel", "RETURN")]
    [DataRow("opening", "gOakSpeech_Text_ThisWorld", "This realm…")]
    [DataRow("opening-names", "gNameChoice_Green", "AZURE")]
    [DataRow("teachy-tv", "gTeachyTvString_TeachBattle", "Study battling.")]
    public void SparseProjectionChangesItsRealConsumerAndRejectsUnknownEmptyOrInjectedControls(string group, string id, string value)
    {
        using var output = TestAssets.TemporaryOutput();
        string english = Display(group, id, SourceTextPack.Load(null));
        Assert.AreNotEqual(value, english);
        Assert.AreEqual(value, Display(group, id, Pack(output.Path, group, id, value)));
        Assert.AreEqual(english, Display(group, id, SourceTextPack.Load(null)));
        foreach (var (key, replacement) in new[] { ("Unknown_Entry", value), (id, ""), (id, value + "\f") })
            Assert.ThrowsExactly<InvalidDataException>(() => Display(group, id, Pack(output.Path, group, key, replacement)));
        Assert.IsFalse(Directory.Exists(Path.Combine(output.Path, "content")), "Only a sparse override is written.");
    }

    [TestMethod]
    public void OpeningKeepsSourcePageControlsAndBindsCustomNamesOnlyAfterPagination()
    {
        using var output = TestAssets.TemporaryOutput();
        const string id = "gOakSpeech_Text_LetsGo";
        var original = PretOpeningPresentation.Load(TestAssets.Root, SourceTextPack.Load(null), Battle);
        string text = original.Texts[id].Replace("Your very own", "Your personal", StringComparison.Ordinal);
        var actual = PretOpeningPresentation.Load(TestAssets.Root, Pack(output.Path, "opening", id, text), Battle);
        const string player = "{RIVAL}\f\\p";
        Assert.AreSequenceEqual(new[] { player + "!", "Your personal POKéMON legend is\nabout to unfold!",
            "A world of dreams and adventures\nwith POKéMON awaits! Let's go!" }, actual.Pages("LetsGo", player, "CUSTOM"));
        Assert.AreEqual(original.Texts["gOakSpeech_Text_ThisWorld"], actual.Texts["gOakSpeech_Text_ThisWorld"]);
        foreach (string malformed in new[] { text.Replace('\f', '\n'), text.Replace("\f", ""), text + "\f", text.Replace("{PLAYER}", "{RIVAL}") })
            Assert.ThrowsExactly<InvalidDataException>(() => PretOpeningPresentation.Load(TestAssets.Root,
                Pack(output.Path, "opening", id, malformed), Battle));
    }

    [TestMethod]
    public void PresetSuggestionsUseSourceIdsWhileTitleProjectionKeepsNamingRulesAndLiteralNames()
    {
        using var output = TestAssets.TemporaryOutput();
        var pack = Pack(output.Path, "opening-names", "gNameChoice_Green", "AZURE");
        var original = PretOpeningPresentation.Load(TestAssets.Root, SourceTextPack.Load(null), Battle);
        var bank = Battle.ToImmutableDictionary().SetItem("gText_PkmnsNickname", " name?");
        var actual = PretOpeningPresentation.Load(TestAssets.Root, pack, bank);
        Assert.AreSequenceEqual(original.RivalNameSymbols, actual.RivalNameSymbols);
        Assert.AreEqual("gNameChoice_Green", actual.RivalNameSymbols[0]);
        Assert.AreSequenceEqual(new[] { "AZURE", "GARY", "KAZ", "TORU" }, actual.RivalNames);
        Assert.AreEqual("…Er, was it GREEN?", actual.Pages("ConfirmRivalName", "RED", "GREEN")[0]);
        foreach (var (id, template) in original.NamingTemplates)
        {
            string expected = template.TitleSymbol == "gText_PkmnsNickname" ? " name?" : template.Title;
            Assert.AreEqual(template with { Title = expected }, actual.NamingTemplates[id]);
        }
        Assert.AreEqual(original.NamingLayout, actual.NamingLayout);
        foreach (string invalid in new[] { "EIGHTLET", "BAD\\n", "{RIVAL}" })
            Assert.ThrowsExactly<InvalidDataException>(() => PretOpeningPresentation.Load(TestAssets.Root,
                Pack(output.Path, "opening-names", "gNameChoice_Green", invalid), Battle));
    }

    [TestMethod]
    [DataRow("小茂")]
    [DataRow("𠮷𠮷𠮷𠮷𠮷𠮷𠮷")]
    [DataRow("e\u0301e\u0301e\u0301e\u0301e\u0301e\u0301e\u0301")]
    public void PresetSuggestionsAcceptUnicodeTextElementNames(string name)
    {
        using var output = TestAssets.TemporaryOutput();
        var actual = PretOpeningPresentation.Load(TestAssets.Root,
            Pack(output.Path, "opening-names", "gNameChoice_Green", name), Battle);
        Assert.AreEqual(name, actual.RivalNames[0]);
        Assert.IsTrue(Game.Domain.Pokemon.NameTextRules.IsValidTrainerName(actual.RivalNames[0]));
        Assert.ThrowsExactly<InvalidDataException>(() => PretOpeningPresentation.Load(TestAssets.Root,
            Pack(output.Path, "opening-names", "gNameChoice_Green", "一二三四五六七八"), Battle));
    }

    [TestMethod]
    public void HelpTextProjectionKeepsProgressionAndBindsPlayerAndPcNamesOnce()
    {
        using var output = TestAssets.TemporaryOutput();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, HelpCatalog.ResourcePath)));
        var originals = document.RootElement.GetProperty("texts").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        string id = originals.First(p => p.Value == " Using “{PLAYER}”").Key;
        var pack = Pack(output.Path, "help", id, " Select “{PLAYER}”");
        var bank = Battle.ToImmutableDictionary().SetItem("gString_Bill", "BILLIE").SetItem("gString_Someone", "ANOTHER");
        var original = HelpCatalog.Load(TestAssets.Root, SourceTextPack.Load(null), bank);
        var actual = HelpCatalog.Load(TestAssets.Root, pack, bank);
        var progress = new HelpProgress(ImmutableHashSet.Create("FLAG_SYS_POKEMON_GET", "FLAG_SYS_NOT_SOMEONES_PC", "FLAG_DEFEATED_BROCK"),
            ImmutableHashSet.Create("ITEM_TOWN_MAP"), 151, "{STR_VAR_1}");
        int changed = 0;
        foreach (string context in original.Contexts)
        {
            var before = original.Topics(context, progress);
            var after = actual.Topics(context, progress);
            Assert.AreSequenceEqual(before.Select(t => t.Id), after.Select(t => t.Id));
            for (int index = 0; index < before.Length; index++)
            {
                Assert.AreSequenceEqual(before[index].Entries.Select(e => e.Id), after[index].Entries.Select(e => e.Id));
                foreach (var entry in after[index].Entries)
                {
                    if (entry.Question == " Select “{STR_VAR_1}”") changed++;
                    Assert.IsFalse(entry.Question.Contains(" Select “BILLIE”", StringComparison.Ordinal));
                }
            }
        }
        Assert.IsTrue(changed > 0);
    }

    [TestMethod]
    public void TeachyTvProjectionLeavesLessonCommandsMenuPositionsAndSelectionIntact()
    {
        using var output = TestAssets.TemporaryOutput();
        const string id = "gTeachyTvString_TeachBattle";
        var original = PretTeachyTv.Load(TeachyJson, SourceTextPack.Load(null));
        var actual = PretTeachyTv.Load(TeachyJson, Pack(output.Path, "teachy-tv", id, "Study battling."));
        Assert.AreEqual(JsonSerializer.Serialize(original with { Texts = actual.Texts }), JsonSerializer.Serialize(actual));
        foreach (var (symbol, source) in original.Texts)
            Assert.AreEqual(symbol == id ? "Study battling." : source.EncodedText, actual.Texts[symbol].EncodedText);
        foreach (bool hasTmCase in new[] { false, true })
        {
            var state = TeachyTvPlaybackRules.Open("projection-test", TeachyTvOrigin.Bag, true, hasTmCase);
            Assert.AreEqual(TeachyTvPlaybackRules.ConfirmMenu(original, state), TeachyTvPlaybackRules.ConfirmMenu(actual, state));
        }
    }

    [TestMethod]
    [DataRow("gText_Continue", "RESUME")]
    [DataRow("gText_NewGame", "BEGIN")]
    [DataRow("gText_Player", "TRAINER")]
    [DataRow("gText_Time", "CLOCK")]
    [DataRow("gText_Pokedex", "DEX")]
    [DataRow("gText_Badges", "AWARDS")]
    public void MainMenuUsesSharedBattleSymbolsWithoutChangingEntryRules(string id, string text)
    {
        using var output = TestAssets.TemporaryOutput();
        var original = PretMainMenuSource.Load(TestAssets.Root, Battle);
        var bank = PretBattleText.Load(TestAssets.Root, Pack(output.Path, "battle", id, text));
        var actual = PretMainMenuSource.Load(TestAssets.Root, bank);
        foreach (var (symbol, source) in original.Texts)
            Assert.AreEqual(symbol == id ? source with { Text = text } : source, actual.Texts[symbol]);
        Assert.AreEqual(JsonSerializer.Serialize(original.Rules), JsonSerializer.Serialize(actual.Rules));
        Assert.AreEqual(JsonSerializer.Serialize(original.TextPlacements), JsonSerializer.Serialize(actual.TextPlacements));
    }

    [TestMethod]
    [DataRow("sText_WildPkmnAppearedPause", "Wild", "A wild")]
    [DataRow("gText_WhatWillOldManDo", "What will", "What shall")]
    [DataRow("sText_OldManUsedItem", "The old man used", "The elder used")]
    [DataRow("sText_GotchaPkmnCaught2", "Gotcha!", "Caught!")]
    public void OldManProjectsAllFourSharedSymbolsAndRetainsTutorialRulesAndControls(string id, string from, string to)
    {
        using var output = TestAssets.TemporaryOutput();
        var canonical = TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root));
        string route = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/pokemon/route1/catalog.json"));
        string json = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/services/old-man-tutorial.json"));
        var original = PretOldManTutorial.LoadContent(canonical, route, json, Battle);
        string replacement = Battle[id].Replace(from, to, StringComparison.Ordinal);
        var bank = PretBattleText.Load(TestAssets.Root, Pack(output.Path, "battle", id, replacement));
        var actual = PretOldManTutorial.LoadContent(canonical, route, json, bank);
        foreach (var (symbol, source) in original.Texts)
            Assert.AreEqual(symbol == id ? source with { Text = replacement.Replace("\\n", "\n", StringComparison.Ordinal) } : source, actual.Texts[symbol]);
        Assert.AreSequenceEqual(original.Catalog.Species.Keys.OrderBy(key => key.Value), actual.Catalog.Species.Keys.OrderBy(key => key.Value));
        Assert.AreSequenceEqual(original.Catalog.Moves.Keys.OrderBy(key => key.Value), actual.Catalog.Moves.Keys.OrderBy(key => key.Value));
        foreach (var (key, species) in original.Catalog.Species)
            Assert.AreEqual(JsonSerializer.Serialize(species), JsonSerializer.Serialize(actual.Catalog.Species[key]));
        foreach (var (key, move) in original.Catalog.Moves)
            Assert.AreEqual(move, actual.Catalog.Moves[key]);
        Assert.AreSequenceEqual(original.Assets, actual.Assets);
        Assert.AreEqual(original.TeachyTv, actual.TeachyTv);
        Assert.AreSequenceEqual(original.OldManThrowFrames, actual.OldManThrowFrames);
        Assert.AreEqual(original.OldManThrowSource, actual.OldManThrowSource);
        Assert.AreSequenceEqual(original.Layouts, actual.Layouts);
    }
}
