using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class ServiceTextProjectionTests
{
    private static IReadOnlyDictionary<string, string> English => TestAssets.Cached(() =>
        PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null)));
    private static PretCampaignCatalog Current => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
    private static PretCurrentWorld World => TestAssets.Cached(() => PretCurrentWorld.Load(TestAssets.Root));
    private static PokemonCatalog Owned => Current.PokemonContent.Catalog;

    [TestMethod]
    [DataRow("storage", "gPCText_Forest", "FOREST", "GROVE")]
    [DataRow("storage", "gPCText_Scenery1", "SCENERY", "LANDSCAPE")]
    [DataRow("tutor", "gText_Able2", "ABLE", "READY")]
    [DataRow("reminder", "gText_TeachWhichMoveToMon", "Teach", "Offer")]
    [DataRow("deleter", "gText_PokeSum_PageName_KnownMoves", "KNOWN MOVES", "MOVE LIST")]
    [DataRow("name-rater", "gText_ChoosePokemon", "Choose", "Pick")]
    [DataRow("trade", "gText_XWillBeSentToY", "will be", "is being")]
    [DataRow("hall", "gText_HOFCorrupted", "corrupted", "damaged")]
    [DataRow("diploma", "gText_Diploma_ThisDocument", "document", "certificate")]
    [DataRow("prizes", "gText_Abra_180Coins", "ABRA", "PRIZE")]
    [DataRow("prizes", "gText_NoThanks_2", "NO THANKS", "LEAVE")]
    public void ActualServiceConsumersUseOneSparseBankAndKeepRules(string family, string symbol, string before, string after)
    {
        using var output = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        string replacement = English[symbol].Replace(before, after, StringComparison.Ordinal);
        Assert.AreNotEqual(English[symbol], replacement);
        File.WriteAllText(Path.Combine(output.Path, "source/battle.json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [symbol] = replacement }));
        var bank = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(output.Path));
        var original = Capture(family, English);
        var actual = Capture(family, bank);
        Assert.AreEqual(original.Rules, actual.Rules, "Display projection cannot change identities, rules, layouts or preset names.");
        Assert.AreEqual(original.Texts.Count, actual.Texts.Count);
        Assert.IsTrue(original.Texts.ContainsKey(symbol), "Exercise an actual consumed source ID.");
        foreach (var (id, text) in original.Texts)
            Assert.AreEqual(id == symbol ? text.Replace(before, after, StringComparison.Ordinal) : text, actual.Texts[id], id);
    }

    private static (IReadOnlyDictionary<string, string> Texts, string Rules) Capture(string family, IReadOnlyDictionary<string, string> bank)
    {
        string Rules(object value) => JsonSerializer.Serialize(value);
        switch (family)
        {
            case "storage":
                var storage = PretStorageCustomization.Load(TestAssets.Root, bank);
                foreach (var wallpaper in storage.Wallpapers) Assert.AreEqual(storage.Texts[wallpaper.NameTextSymbol], wallpaper.Name);
                foreach (var category in storage.Categories) Assert.AreEqual(storage.Texts[category.NameTextSymbol], category.Name);
                return (storage.Texts, Rules(new { storage.Assets,
                    Wallpapers = storage.Wallpapers.Select(row => row with { Name = "" }),
                    Categories = storage.Categories.Select(row => row with { Name = "" }), storage.Document }));
            case "tutor":
                var tutor = PretMoveTutors.Load(TestAssets.Root, Owned, bank).Catalog;
                Assert.AreSame(Owned, tutor.Pokemon);
                return (tutor.Texts, Rules(tutor.Definitions.Values.OrderBy(row => row.Index).Select(row => new {
                    row.Index, Move = row.Move.Value, row.SourceMoveId, row.Pp,
                    Compatibility = row.Compatibility.OrderBy(pair => pair.Key.Value).Select(pair => new { Species = pair.Key.Value, pair.Value }) })));
            case "reminder":
                var reminder = PretMoveReminderService.Load(TestAssets.Root, Owned, bank).Catalog;
                Assert.AreSame(Owned, reminder.Pokemon);
                return (reminder.Texts, "");
            case "deleter":
                return (PretMoveDeleterTexts.LoadProject(TestAssets.Root, bank), "");
            case "name-rater":
                var name = PretNameRaterContract.Load(TestAssets.Root, bank).Source;
                return (new Dictionary<string, string> { ["gText_ChoosePokemon"] = name.ChoosePokemonText,
                    ["gText_ChoosePokemonCancel"] = name.ChoosePokemonCancelText }, "");
            case "trade":
                var trade = PretNpcTrades.Load(World, Owned, bank);
                return (trade.Texts, Rules(trade.Trades.Definitions.OrderBy(pair => pair.Key)));
            case "hall":
                var hall = PretHallOfFamePcPresentation.Load(TestAssets.Root, bank);
                return (hall.Text, Rules(hall with { Text = ImmutableDictionary<string, string>.Empty }));
            case "diploma":
                var diploma = PretDiplomaPresentation.Load(TestAssets.Root, bank).Document;
                return (new Dictionary<string, string> { ["gText_Diploma_Player"] = diploma.PlayerTemplate,
                    ["gText_Diploma_ThisDocument"] = diploma.BodyTemplate, ["gText_Diploma_Kanto"] = diploma.Kanto,
                    ["gText_Diploma_National"] = diploma.National, ["gText_Diploma_GameFreak"] = diploma.Signature },
                    Rules(diploma with { PlayerTemplate = "", BodyTemplate = "", Kanto = "", National = "", Signature = "" }));
            case "prizes":
                var shared = SharedMachineCatalog.LoadProject(TestAssets.Root, Current.Items);
                var prizes = PretGameCornerPrizes.LoadCatalog(TestAssets.Root, Current.GamePokemon,
                    Current.EarlyShop!, Current.Machines, shared, bank);
                var rows = PretGameCornerPrizes.LoadSourceContract(TestAssets.Root, shared).GetProperty("counters");
                var texts = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int index = 0; index < prizes.Counters.Length; index++)
                {
                    var symbols = rows[index].GetProperty("menuTextSymbols").EnumerateArray().Select(row => row.GetString()!).ToArray();
                    var counter = prizes.Counters[index];
                    for (int row = 0; row < symbols.Length; row++)
                    {
                        if (texts.TryGetValue(symbols[row], out string? existing)) Assert.AreEqual(existing, counter.MenuLabels[row]);
                        texts[symbols[row]] = counter.MenuLabels[row];
                    }
                }
                return (texts, Rules(prizes.Counters.Select(row => row with { MenuLabels = [] })));
            default: throw new ArgumentOutOfRangeException(nameof(family));
        }
    }
}
