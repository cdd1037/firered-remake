using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Battle.Lifecycle;

// Real admitted content exercises ownership rules; presentation catalog edits belong to Content tests.
[TestClass, TestCategory("Game")]
public sealed class OwnedNicknameLifecycleTests
{
    private static PretCampaignCatalog Catalog => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
    private static CampaignPokemonContent Content => Catalog.PokemonContent;

    private static PartyPokemon Owned(string species, int level, string? nickname, int language = 2)
    {
        var pokemon = PokemonFactory.Create(Content.Catalog,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(20), PersonalityPolicy.Fixed(123)), new(0)).Pokemon;
        return new(1, pokemon, nickname, "RED", 0, language, "MAPSEC_ROUTE_1", level, 4,
            "ITEM_POKE_BALL", "ITEM_NONE");
    }

    public static IEnumerable<object?[]> EvolutionCases()
    {
        foreach (string method in new[] { "level", "item" })
            foreach (int language in new[] { 1, 2, 3 })
                foreach (string? nickname in new[] { null, method == "level" ? "BULBASAUR" : "PIKACHU", "PETAL" })
                    yield return [method, language, nickname];
    }

    [TestMethod, DynamicData(nameof(EvolutionCases))]
    public void EvolutionPreservesNullOrLiteralIntentWithoutLanguageOrSpeciesNameGuessing(string method,
        int language, string? nickname)
    {
        var before = Owned(method == "level" ? "BULBASAUR" : "PIKACHU", 16, nickname, language);
        string saved = JsonSerializer.Serialize(before);
        PartyPokemon evolved;
        if (method == "level")
        {
            var proposal = LevelEvolutionRules.Eligibility(Content, before, 15);
            Assert.IsNotNull(proposal);
            var cancelled = LevelEvolutionRules.Cancel(Content, before, proposal);
            Assert.AreSame(before, cancelled.Pokemon);
            Assert.AreEqual(nickname, cancelled.Pokemon.CustomNickname);
            evolved = LevelEvolutionRules.Apply(Content, before, proposal).Pokemon;
            Assert.AreEqual("pret:SPECIES_IVYSAUR", evolved.Pokemon.Species.Value);
        }
        else
        {
            var proposal = ItemEvolutionRules.Eligibility(Content, before, ItemEvolutionRules.ThunderStone);
            Assert.IsNotNull(proposal);
            evolved = ItemEvolutionRules.Apply(Content, before, proposal).Pokemon;
            Assert.AreEqual("pret:SPECIES_RAICHU", evolved.Pokemon.Species.Value);
        }
        Assert.AreEqual(nickname, evolved.CustomNickname);
        Assert.AreEqual(language, evolved.Language);
        Assert.AreEqual(before.InstanceId, evolved.InstanceId);
        Assert.AreEqual(before.Pokemon.Personality, evolved.Pokemon.Personality);
        Assert.AreEqual(before.Pokemon.Ivs, evolved.Pokemon.Ivs);
        Assert.IsNull(PokemonStateValidator.ValidateParty(Content, evolved));
        Assert.AreEqual(saved, JsonSerializer.Serialize(before));
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("BULBASAUR", null)]
    [DataRow("PETAL", null)]
    [DataRow("0123456789", null)]
    [DataRow("", "party_nickname")]
    [DataRow("   ", "party_nickname")]
    [DataRow("01234567890", "party_nickname")]
    [DataRow("改名", "party_nickname")]
    public void PartyAndStorageUseTheSameStrictNullableCustomNameBoundary(string? nickname, string? expected)
    {
        var owned = Owned("BULBASAUR", 16, nickname);
        Assert.AreEqual(expected, PokemonStateValidator.ValidateParty(Content, owned));
        Assert.AreEqual(expected, PokemonStateValidator.ValidatePartyCollection(Content, [owned], 2));
        if (expected is not null) return;
        var guard = Owned("PIKACHU", 16, null) with { InstanceId = 2 };
        var deposited = PokemonStorageRules.Deposit(Content, [guard, owned], PokemonStorageState.Empty, 1, 0);
        Assert.IsTrue(deposited.Succeeded, deposited.Diagnostic?.Code);
        Assert.AreEqual(nickname, deposited.Storage.Boxes[0].Slots[0]!.CustomNickname);
        var withdrawn = PokemonStorageRules.Withdraw(Content, deposited.Party, deposited.Storage, 0, 0);
        Assert.IsTrue(withdrawn.Succeeded, withdrawn.Diagnostic?.Code);
        Assert.AreEqual(nickname, withdrawn.Party[1].CustomNickname);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)]
    [DataRow(5)] [DataRow(6)] [DataRow(7)] [DataRow(8)]
    public void FixedNpcTradeNamesAreLiteralCustomNames(int tradeId)
    {
        var created = NpcTradePokemonFactory.Create(Catalog.NpcTrades, Content, tradeId, 30, 1, new(31));
        Assert.AreEqual(Catalog.NpcTrades.Definitions[tradeId].Nickname, created.Pokemon.CustomNickname);
        Assert.IsNotNull(created.Pokemon.CustomNickname);
        Assert.IsNull(PokemonStateValidator.ValidateParty(Content, created.Pokemon));
        Assert.AreEqual(2, created.Trace.Length);
        Assert.AreEqual(2UL, created.Rng.Cursor);
    }

    [TestMethod]
    [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void GiftEggAndHatchKeepDefaultIntentIndependentOfTrainerLanguage(int language)
    {
        var trainer = new SourceEggTrainer(42, "RED", 0, language);
        var made = SourceGiftEggRules.GiveEgg(Content, Content.Catalog.BreedingData!, SourceGiftEggRules.Togepi,
            1, trainer, 4, new(31));
        Assert.IsTrue(made.Egg.IsEgg);
        Assert.AreEqual(1, made.Egg.Language);
        Assert.IsNull(made.Egg.CustomNickname);
        Assert.IsNull(PokemonStateValidator.ValidateParty(Content, made.Egg));
        Assert.AreEqual("party_nickname", PokemonStateValidator.ValidateParty(Content,
            made.Egg with { CustomNickname = "EGG" }));
        Assert.AreEqual("party_nickname", PokemonStateValidator.ValidateParty(Content,
            made.Egg with { CustomNickname = "タマゴ" }));

        var hatched = SourceEggRules.Hatch(Content, made.Egg, trainer, "MAPSEC_PALLET_TOWN", made.Rng);
        Assert.IsFalse(hatched.Pokemon.IsEgg);
        Assert.IsNull(hatched.Pokemon.CustomNickname);
        Assert.AreEqual(language, hatched.Pokemon.Language);
        Assert.IsNull(PokemonStateValidator.ValidateParty(Content, hatched.Pokemon));
        Assert.AreEqual(2, hatched.Trace.Length);
        Assert.AreEqual(made.Rng.Cursor + 2, hatched.Rng.Cursor);
    }
}
