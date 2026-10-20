using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Domain;

[TestClass, TestCategory("Game")]
public sealed class PokemonNicknameRulesTests
{
    // Name Rater needs ownership identity, not a display-name catalog.
    private static PartyPokemon Owned(string? customNickname)
    {
        SpeciesId species = new("pret:SPECIES_BULBASAUR");
        MoveId move = new("pret:MOVE_TACKLE");
        var definition = new PokemonSpeciesDefinition(species, 1, new(45, 49, 49, 45, 65, 65),
            "TYPE_GRASS", "TYPE_POISON", 45, 64, SixStats.Zero, GrowthRate.MediumSlow,
            31, "ABILITY_OVERGROW", "ABILITY_NONE", 70, [new(1, move)], []);
        var attack = new PokemonMoveDefinition(move, 33, 35, 35, "TYPE_NORMAL", 95, "EFFECT_HIT", 0,
            "MOVE_TARGET_SELECTED", 0, [], false);
        var catalog = new PokemonCatalog(PokemonFactory.SourceCommit,
            ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition>.Empty.Add(species, definition),
            ImmutableDictionary<MoveId, PokemonMoveDefinition>.Empty.Add(move, attack));
        var pokemon = PokemonFactory.Create(catalog,
            new(species, 5, 42, IvPolicy.Fixed(20), PersonalityPolicy.Fixed(123)), new(0)).Pokemon;
        return new(1, pokemon, customNickname, "RED", 0, 2, "MAPSEC_PALLET_TOWN", 5, 4,
            "ITEM_POKE_BALL", "ITEM_NONE");
    }

    [TestMethod]
    [DataRow(null, "", null, false)]
    [DataRow(null, "   ", null, false)]
    [DataRow(null, "BULBASAUR", "BULBASAUR", true)]
    [DataRow(null, "Bloom", "Bloom", true)]
    [DataRow(null, "妙蛙种子", "妙蛙种子", true)]
    [DataRow("旧名", "👩🏽‍🚀", "👩🏽‍🚀", true)]
    [DataRow(null, "e\u0301", "e\u0301", true)]
    [DataRow(null, " X ", " X ", true)]
    [DataRow("BULBASAUR", "", "BULBASAUR", false)]
    [DataRow("BULBASAUR", "BULBASAUR", "BULBASAUR", false)]
    [DataRow("PETAL", "   ", "PETAL", false)]
    [DataRow("PETAL", "PETAL", "PETAL", false)]
    [DataRow("PETAL", "BULBASAUR", "BULBASAUR", true)]
    public void SubmissionPreservesNullableIntentAndEveryNonblankLiteral(string? before, string input,
        string? expected, bool changed)
    {
        Assert.AreEqual(expected, PokemonNicknameRules.Apply(before, input));
        ImmutableArray<PartyPokemon> party = [Owned(before)];
        Assert.IsNull(NameRaterRules.Begin(party, 0, 1, 42, "RED", out var target));
        Assert.IsNotNull(target);
        Assert.AreEqual(before, target.OriginalCustomNickname);

        var result = NameRaterRules.Rename(party, target, 42, "RED", input);
        Assert.IsTrue(result.Accepted, result.Error);
        Assert.AreEqual(before, result.OriginalCustomNickname);
        Assert.AreEqual(expected, result.FinalCustomNickname);
        Assert.AreEqual(expected, result.Party[0].CustomNickname);
        Assert.AreEqual(changed, result.Changed);
        Assert.AreEqual(!changed, party.Equals(result.Party));
        Assert.AreEqual(party[0] with { CustomNickname = expected }, result.Party[0]);
        Assert.AreEqual(before, party[0].CustomNickname, "the input party remains immutable");
    }

    [TestMethod]
    [DataRow(null, "BULBASAUR")]
    [DataRow("BULBASAUR", null)]
    [DataRow("PETAL", "BLOOM")]
    public void RenameDetectsChangedNullableIntentEvenForTheSameOwnedIdentity(string? before, string? current)
    {
        ImmutableArray<PartyPokemon> party = [Owned(before)];
        Assert.IsNull(NameRaterRules.Begin(party, 0, 1, 42, "RED", out var target));
        var stale = party.SetItem(0, party[0] with { CustomNickname = current });
        var result = NameRaterRules.Rename(stale, target, 42, "RED", "NEW NAME");
        Assert.AreEqual("name_rater_original_name_changed", result.Error);
        Assert.IsTrue(stale.Equals(result.Party));
    }

    [TestMethod]
    [DataRow(null, true)]
    [DataRow("BULBASAUR", true)]
    [DataRow("PETAL", true)]
    [DataRow("0123456789", true)]
    [DataRow(" X ", true)]
    [DataRow("", false)]
    [DataRow("          ", false)]
    [DataRow("01234567890", false)]
    [DataRow("改名", true)]
    [DataRow("A\nB", false)]
    public void StoredCustomNameValidationIsSeparateFromDefaultDisplayNames(string? name, bool valid)
    {
        Assert.AreEqual(valid, PokemonNicknameRules.IsValidCustomNickname(name));
        var error = NameRaterRules.Begin([Owned(name)], 0, 1, 42, "RED", out var target);
        Assert.AreEqual(valid ? null : "name_rater_original_name_unsupported", error);
        Assert.AreEqual(valid, target is not null);
    }

    [TestMethod]
    [DataRow(null, "nickname_text_required")]
    [DataRow("01234567890", "nickname_length")]
    [DataRow("名{字}", "nickname_character_unsupported")]
    [DataRow("A\tB", "nickname_character_unsupported")]
    public void RejectedInputCannotChangeNamingIntent(string? input, string expectedError)
    {
        ImmutableArray<PartyPokemon> party = [Owned(null)];
        Assert.IsNull(NameRaterRules.Begin(party, 0, 1, 42, "RED", out var target));
        var result = NameRaterRules.Rename(party, target, 42, "RED", input);
        Assert.AreEqual(expectedError, result.Error);
        Assert.IsTrue(party.Equals(result.Party));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("BULBASAUR")]
    [DataRow("PETAL")]
    [DataRow("妙蛙种子")]
    [DataRow("👩🏽‍🚀")]
    public void SerializationRequiresExplicitNullableIntentAndNeverInfersOldNames(string? name)
    {
        var owned = Owned(name);
        string json = JsonSerializer.Serialize(owned);
        var document = JsonNode.Parse(json)!.AsObject();
        Assert.IsTrue(document.ContainsKey("CustomNickname"));
        Assert.IsFalse(document.ContainsKey("Nickname"));
        var roundtrip = JsonSerializer.Deserialize<PartyPokemon>(json)!;
        Assert.AreEqual(name, roundtrip.CustomNickname);
        Assert.AreEqual(json, JsonSerializer.Serialize(roundtrip));

        document.Remove("CustomNickname");
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PartyPokemon>(document.ToJsonString()));
        document["Nickname"] = name ?? "BULBASAUR";
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PartyPokemon>(document.ToJsonString()));
    }

    [TestMethod]
    [DataRow("egg", "name_rater_egg")]
    [DataRow("trainer-id", "name_rater_foreign_id")]
    [DataRow("trainer-name", "name_rater_foreign_name")]
    public void NullableNamesDoNotRelaxNameRaterOwnershipRules(string change, string expectedError)
    {
        var owned = Owned(null);
        owned = change switch
        {
            "egg" => owned with { IsEgg = true },
            "trainer-id" => owned with { Pokemon = owned.Pokemon with { OriginalTrainerId = 43 } },
            "trainer-name" => owned with { OriginalTrainerName = "BLUE" },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.AreEqual(expectedError, NameRaterRules.Begin([owned], 0, 1, 42, "RED", out var target));
        Assert.IsNull(target);
    }
    [TestMethod]
    [DataRow("小智")]
    [DataRow("𠮷")]
    [DataRow("e\u0301")]
    [DataRow("👩🏽‍🚀")]
    public void UnicodeOtUsesActualLiteralNameAndFullTrainerId(string trainer)
    {
        var owned = Owned(null) with { OriginalTrainerName = trainer };
        Assert.IsNull(NameRaterRules.Begin([owned], 0, 1, 42, trainer, out var target));
        var renamed = NameRaterRules.Rename([owned], target, 42, trainer, "新名字");
        Assert.IsTrue(renamed.Accepted, renamed.Error);
        Assert.AreEqual(trainer, renamed.Party[0].OriginalTrainerName);
        Assert.AreEqual("name_rater_foreign_id", NameRaterRules.Begin([owned], 0, 1, 42 + 65536, trainer, out _));
        Assert.AreEqual("name_rater_foreign_name", NameRaterRules.Begin([owned], 0, 1, 42, trainer + "甲", out _));
        Assert.IsFalse(Game.Domain.Battle.SourceObedienceRules.IsOtherTrainer(42, trainer, 42, trainer + "甲"),
            "The source obedience rule intentionally compares an OT prefix.");
        Assert.IsTrue(Game.Domain.Battle.SourceObedienceRules.IsOtherTrainer(42, trainer, 42 + 65536, trainer));
    }

}
