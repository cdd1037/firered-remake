using Game.Domain.Pokemon;

namespace FireRed.Tests.Domain;

[TestClass, TestCategory("Game")]
public sealed class NameTextRulesTests
{
    [TestMethod]
    [DataRow("小智")]
    [DataRow("𠮷")]
    [DataRow("e\u0301")]
    [DataRow("👩🏽‍🚀")]
    public void EveryLimitCountsWholeVisibleTextElements(string element)
    {
        int elements = NameTextRules.TextElementCount(element);
        string Repeat(int count) => string.Concat(Enumerable.Repeat(element, count));
        // The Chinese two-character case deliberately exercises the boundary with mixed text.
        string At(int limit) => Repeat(limit / elements) + new string('名', limit % elements);
        Assert.IsTrue(NameTextRules.IsValidTrainerName(At(7)));
        Assert.IsFalse(NameTextRules.IsValidTrainerName(At(7) + element));
        Assert.IsNull(PokemonNicknameRules.Validate(At(10)));
        Assert.AreEqual("nickname_length", PokemonNicknameRules.Validate(At(10) + element));
        Assert.IsNull(PokemonStorageCustomizationRules.ValidateNameInput(At(8)));
        Assert.AreEqual("storage_box_name_length", PokemonStorageCustomizationRules.ValidateNameInput(At(8) + element));
        Assert.AreEqual(At(7), NameTextRules.Truncate(At(7) + element, 7));
    }

    [TestMethod]
    [DataRow("A\nB")]
    [DataRow("A\0B")]
    [DataRow("A\u2028B")]
    [DataRow("A\u202EB")]
    [DataRow("{PLAYER}")]
    [DataRow("A\\n")]
    [DataRow("A$")]
    [DataRow("\u200D")]
    [DataRow("\uFE0F")]
    public void ControlsAndSourceCommandsAreNeverNames(string name)
    {
        Assert.IsFalse(NameTextRules.IsValidTrainerName(name));
        Assert.AreEqual("nickname_character_unsupported", PokemonNicknameRules.Validate(name));
        Assert.AreEqual("storage_box_name_character", PokemonStorageCustomizationRules.ValidateNameInput(name));
    }

    [TestMethod]
    public void InvalidSurrogatesAreRejectedAndLiteralSpellingIsNotNormalized()
    {
        foreach (string invalid in new[] { "\uD800", "\uDC00", "名\uD800字" })
        {
            Assert.IsFalse(NameTextRules.HasValidCharacters(invalid));
            Assert.IsFalse(PokemonNicknameRules.IsValidCustomNickname(invalid));
        }
        Assert.AreEqual(" e\u0301 ", PokemonNicknameRules.Apply(null, " e\u0301 "));
        Assert.AreNotEqual("é", PokemonNicknameRules.Apply(null, "e\u0301"));
        Assert.IsFalse(NameTextRules.IsValidTrainerName("\u3000"));
        Assert.AreEqual("旧名", PokemonNicknameRules.Apply("旧名", "\u3000"));
    }
    [TestMethod]
    public void PathologicalCombiningSequencesHaveOneExplicitSharedResourceBound()
    {
        string allowed = "a" + new string('\u0301', NameTextRules.MaximumScalarCount - 1);
        Assert.AreEqual(1, NameTextRules.TextElementCount(allowed));
        Assert.IsTrue(NameTextRules.IsValidTrainerName(allowed));
        Assert.IsNull(PokemonNicknameRules.Validate(allowed));
        Assert.IsNull(PokemonStorageCustomizationRules.ValidateNameInput(allowed));
        string oversized = allowed + "\u0301";
        Assert.IsFalse(NameTextRules.IsValidTrainerName(oversized));
        Assert.AreEqual("nickname_length", PokemonNicknameRules.Validate(oversized));
        Assert.AreEqual("storage_box_name_length", PokemonStorageCustomizationRules.ValidateNameInput(oversized));
    }

}
