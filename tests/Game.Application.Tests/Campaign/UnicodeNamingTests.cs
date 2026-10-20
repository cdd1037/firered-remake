using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class UnicodeNamingTests
{
    [TestMethod]
    [DataRow("小智", "小茂")]
    [DataRow("𠮷𠮷𠮷𠮷𠮷𠮷𠮷", "青绿")]
    [DataRow("e\u0301e\u0301e\u0301e\u0301e\u0301e\u0301e\u0301", "👩🏽‍🚀")]
    public void NewGameNamesAndCustomBoxNamesSaveAndContinueLiterally(string player, string rival)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog, new(0xFEDCBA98, player, rival, RngSeed: 17));
        Assert.AreEqual(player, session.State.PlayerName);
        Assert.AreEqual(rival, session.State.RivalName);
        Assert.AreEqual(CampaignSession.CreateCurrent(catalog, new(0xFEDCBA98, RngSeed: 17)).State.World.Rng,
            session.State.World.Rng, "Names do not seed generation.");
        const string boxName = "伙伴👩🏽‍🚀";
        var renamed = PokemonStorageCustomizationRules.RenameBox(catalog.PokemonContent, [], session.State.Storage!, null, 0, boxName);
        Assert.IsTrue(renamed.Succeeded, renamed.Diagnostic?.Code);
        string nickname = string.Concat(Enumerable.Repeat("👩🏽‍🚀", 10));
        var state = session.State with { Storage = renamed.Storage,
            TextBuffers = session.State.TextBuffers.SetItem("STR_VAR_1", player).SetItem("STR_VAR_2", nickname) };
        Assert.IsNull(session.ValidatePlayerSave(state));
        var loaded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(state, 1, session.RulesVersion, catalog.ContentVersion),
            session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(catalog, loaded.State, loaded.Generation);
        Assert.AreEqual(CanonicalJson.Hash(state), CanonicalJson.Hash(continued.State));
        Assert.AreEqual(player, continued.State.PlayerName);
        Assert.AreEqual(rival, continued.State.RivalName);
        Assert.AreEqual(boxName, continued.State.Storage!.Boxes[0].Name);
        Assert.AreEqual(nickname, continued.State.TextBuffers["STR_VAR_2"]);
    }

    [TestMethod]
    [DataRow("一二三四五六七八")]
    [DataRow("名\t字")]
    [DataRow("\u200D")]
    public void InvalidPlayerOrRivalNamesCannotEnterANewCampaign(string invalid)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        Assert.ThrowsExactly<ArgumentException>(() => CampaignSession.CreateCurrent(catalog, new(42, invalid, "小茂")));
        Assert.ThrowsExactly<ArgumentException>(() => CampaignSession.CreateCurrent(catalog, new(42, "小智", invalid)));
    }

    [TestMethod]
    [DataRow("小智", 0u)]
    [DataRow("𠮷𠮷𠮷𠮷𠮷𠮷𠮷", uint.MaxValue)]
    [DataRow("👩🏽‍🚀", 0x12345678u)]
    public void ComposedMailCarriesActualUnicodeAuthorAndFullIdThroughMailboxAndSave(string name, uint trainerId)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog, new(trainerId, name, "小茂"));
        var content = catalog.PokemonContent!;
        var creature = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_BULBASAUR"), 5, trainerId,
            IvPolicy.Fixed(20), PersonalityPolicy.Fixed(123)), new(0)).Pokemon;
        var owned = new PartyPokemon(1, creature, "妙蛙种子", name, 0, 2, "MAPSEC_PALLET_TOWN", 5, 4,
            "ITEM_POKE_BALL", "ITEM_NONE");
        var mailCatalog = catalog.MailComposer!;
        var selection = new SourceEasyChatSelection(false, false, []);
        ushort word = mailCatalog.Words.Values.First(w => w.Group == 1 && w.Enabled).Value;
        var words = SourceEasyChatRules.BlankWords.SetItem(0, word);
        var inventory = session.State.Inventory! with { BagItems = OpeningInventoryRules.SetQuantity(
            session.State.Inventory!.BagItems, new ItemId("pret:ITEM_ORANGE_MAIL"), 1) };
        var given = SourceMailComposerRules.Give(catalog.EarlyShop!, mailCatalog, selection, inventory,
            SourceMailState.Empty, [owned], 0, 121, words, SourceMailComposerRules.CreateAuthor(name, trainerId),
            machines: catalog.Machines);
        Assert.IsTrue(given.Succeeded, given.Error);
        var message = given.State.Attached[given.Party[0].Mail]!;
        Assert.AreEqual(SourceMailOrigin.PlayerAuthored, message.Origin);
        Assert.AreEqual(name, message.AuthorName);
        Assert.AreEqual(trainerId, message.AuthorTrainerId);
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(given.Inventory.BagItems, new("pret:ITEM_ORANGE_MAIL")));
        var deposited = SourceMailRules.SendToMailbox(given.State, given.Party, 0);
        Assert.IsTrue(deposited.Succeeded, deposited.Error);
        Assert.AreSame(message, deposited.State.Mailbox[0]);
        var taken = SourceMailRules.GiveFromMailbox(deposited.State, deposited.Party, 0, 0);
        Assert.IsTrue(taken.Succeeded, taken.Error);
        Assert.AreSame(message, taken.State.Attached[taken.Party[0].Mail]);
        // The mailbox is independent of its former holder. A fresh campaign supplies the
        // remaining valid save state without a converted or fabricated late-game checkpoint.
        var state = session.State with { Mail = deposited.State };
        Assert.IsNull(session.ValidatePlayerSave(state));
        var loaded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(state, 1, session.RulesVersion, catalog.ContentVersion),
            session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave).State;
        Assert.AreEqual(CanonicalJson.Hash(state), CanonicalJson.Hash(loaded));
        Assert.AreEqual(name, loaded.Mail!.Mailbox[0]!.AuthorName);
        Assert.AreEqual(trainerId, loaded.Mail.Mailbox[0]!.AuthorTrainerId);
        foreach (var forged in new[] { message with { AuthorTrainerId = trainerId ^ 0x10000u }, message with { AuthorName = "陌生人" } })
            Assert.AreEqual("save_mail_source_invalid", session.ValidatePlayerSave(state with {
                Mail = state.Mail! with { Mailbox = state.Mail.Mailbox.SetItem(0, forged) } }));
    }

    [TestMethod]
    public void DontaeNamedPlayerCanComposeTheSameLetterWithoutForgingNpcAcquisition()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var received = catalog.NpcTradeMailMessage!;
        Assert.AreEqual(SourceMailOrigin.NpcTrade, received.Origin);
        var session = CampaignSession.CreateCurrent(catalog, new(received.AuthorTrainerId, received.AuthorName));
        var creature = PokemonFactory.Create(catalog.PokemonContent.Catalog,
            new(new("pret:SPECIES_JYNX"), 30, received.AuthorTrainerId, IvPolicy.Fixed(20), PersonalityPolicy.Fixed(123)), new(0)).Pokemon;
        var owned = new PartyPokemon(1, creature, null, received.AuthorName, 0, 2, "MAPSEC_PALLET_TOWN", 30, 4,
            "ITEM_POKE_BALL", "ITEM_NONE");
        var inventory = session.State.Inventory! with { BagItems = OpeningInventoryRules.SetQuantity(
            session.State.Inventory!.BagItems, new("pret:ITEM_FAB_MAIL"), 1) };
        var selection = new SourceEasyChatSelection(true, true, ImmutableHashSet.Create(124));
        var given = SourceMailComposerRules.Give(catalog.EarlyShop!, catalog.MailComposer!, selection, inventory,
            SourceMailState.Empty, [owned], 0, received.ItemId, received.Words,
            SourceMailComposerRules.CreateAuthor(received.AuthorName, received.AuthorTrainerId), machines: catalog.Machines);
        Assert.IsTrue(given.Succeeded, given.Error);
        var authored = given.State.Attached[given.Party[0].Mail]!;
        Assert.AreEqual(CanonicalJson.Hash(received with { Origin = SourceMailOrigin.PlayerAuthored }), CanonicalJson.Hash(authored));
        var mailbox = SourceMailRules.SendToMailbox(given.State, given.Party, 0);
        Assert.IsTrue(mailbox.Succeeded, mailbox.Error);
        var state = session.State with { Mail = mailbox.State };
        Assert.IsNull(session.ValidatePlayerSave(state), "Identical authored content does not require an NPC trade receipt.");
        Assert.AreEqual("save_mail_acquisition_missing", session.ValidatePlayerSave(state with {
            Mail = state.Mail! with { Mailbox = state.Mail.Mailbox.SetItem(0, received) } }));
        var saved = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(state, 1, session.RulesVersion, catalog.ContentVersion),
            session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave).State;
        Assert.AreEqual(SourceMailOrigin.PlayerAuthored, saved.Mail!.Mailbox[0]!.Origin);
    }

    [TestMethod]
    public void MailJsonRequiresModernIdentityAndRejectsLegacyByteAuthority()
    {
        var message = new SourceMailMessage(SourceEasyChatRules.BlankWords, "小智", 0, 1, 121, SourceMailOrigin.PlayerAuthored);
        var json = JsonNode.Parse(JsonSerializer.Serialize(message))!.AsObject();
        Assert.IsFalse(json.ContainsKey("AuthorNameBytes"));
        Assert.IsFalse(json.ContainsKey("TrainerIdBytes"));
        Assert.AreEqual(0u, JsonSerializer.Deserialize<SourceMailMessage>(json.ToJsonString())!.AuthorTrainerId);
        json.Remove("AuthorTrainerId");
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<SourceMailMessage>(json.ToJsonString()));
        json["AuthorTrainerId"] = 0;
        json.Remove("Origin");
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<SourceMailMessage>(json.ToJsonString()));
        json["Origin"] = (int)SourceMailOrigin.PlayerAuthored;
        json["TrainerIdBytes"] = new JsonArray(0, 0, 0, 0);
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<SourceMailMessage>(json.ToJsonString()));
    }
}
