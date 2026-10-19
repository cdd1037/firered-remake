using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.World;
using FireRed.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.FieldItems;
[TestClass, TestCategory("Game")]
public sealed class StoneEvolutionTests
{
    private global::Game.Content.PretCampaignCatalog catalog = default !;
    private global::Game.Application.CampaignState original = default !;
    private long generation = default !;
    private global::Game.Domain.Pokemon.CampaignPokemonContent content = default !;
    private global::Game.Domain.Pokemon.GamePokemonCatalog ownership = default !;
    private global::Game.Application.CampaignSession inspector = default !;
    private int commands = default !;
    [TestInitialize]
    public void SetUp()
    {
        catalog = CurrentCampaignFixture.Catalog;
        var checkpoint = CurrentCampaignFixture.Checkpoint;
        original = checkpoint.State;
        generation = checkpoint.Generation;
        content = catalog.PokemonContent!;
        ownership = content.Pokemon;
        inspector = CampaignSession.CreateCurrent(catalog);
        commands = 0;
    }

    private string Hash<T>(T value) => CanonicalJson.Hash(value);
    private int Quantity(CampaignState state, string item) => OpeningInventoryRules.Quantity(state.Inventory!.BagItems, new(item));
    private void Refuses(Action action, string name)
    {
        try
        {
            action();
        }
        catch (Exception e)when (e is ArgumentException or NotSupportedException)
        {
            return;
        }

        throw new InvalidOperationException(name);
    }

    private PartyPokemon Factory(string species, int level)
    {
        var mon = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(17), PersonalityPolicy.Fixed(324468760)), new RngState(0)).Pokemon;
        return new(91, mon, null, "RED", 0, 2, "MAPSEC_ROUTE_1", level, 4, "ITEM_POKE_BALL", "ITEM_NONE");
    }

    private CampaignState Fixture(int species, string item)
    {
        var address = original.Storage!.Boxes.SelectMany((box, b) => box.Slots.Select((mon, s) => new { mon, b, s })).Single(x => x.mon?.Pokemon.SourceSpeciesId == (species == 44 ? 43 : species));
        var deposit = PokemonStorageRules.Deposit(content, original.Party, original.Storage, 0, 13);
        Assert.IsTrue(deposit.Succeeded, "controlled source deposit for " + species);
        var withdrawal = PokemonStorageRules.Withdraw(content, deposit.Party, deposit.Storage, address.b, address.s);
        Assert.IsTrue(withdrawal.Succeeded, "controlled withdrawal of existing owned " + species);
        var bag = original.Inventory!.BagItems;
        if (!bag.Any(x => x.Item.Value == item))
            bag = bag.Add(new(new(item), 2));
        var fixture = original with
        {
            Party = withdrawal.Party,
            Storage = withdrawal.Storage,
            Inventory = original.Inventory with
            {
                BagItems = bag
            }
        };
        Assert.IsTrue(inspector.ValidatePlayerSave(fixture)is null, "strict controlled fixture " + species);
        if (species == 44)
        {
            // Evolve the actual stored Oddish through public Candy/evolution commands.
            var candy = new ItemId("pret:ITEM_RARE_CANDY");
            fixture = fixture with
            {
                Inventory = fixture.Inventory with
                {
                    BagItems = fixture.Inventory.BagItems.RemoveAll(s => s.Item == candy).Add(new(candy, 1))
                }
            };
            var session = Session(fixture);
            Send(session, new("bag_open"));
            Send(session, new("field_rare_candy", Text: candy.Value, PartyIndex: fixture.Party.Length - 1));
            for (int i = 0; i < 40 && (session.State.RareCandy is not null || session.State.Evolution is not null); i++)
                Send(session, session.State.Evolution is { Stage: CampaignEvolutionStage.Proposal } ? new("evolution_choice", Choice: 1) : session.State.Evolution is { Stage: CampaignEvolutionStage.LearnMove } ? new("evolution_learn", MoveSlot: -1) : session.State.RareCandy is { Stage: CampaignRareCandyStage.LearnMove } ? new("rare_candy_learn", MoveSlot: -1) : new("ack"));
            Assert.IsTrue(session.State.Party[^1].Pokemon.SourceSpeciesId == 44 && session.State.Evolution is null, "existing Oddish publicly evolves to Gloom");
            Send(session, new("menu_back"));
            fixture = session.State;
        }

        return fixture;
    }

    private CampaignSession Session(CampaignState state)
    {
        var validationError = inspector.ValidatePlayerSave(state);
        Assert.IsNull(validationError, "strict player boundary before test: " + validationError);
        return CampaignSession.RestoreCurrent(catalog, state, generation);
    }

    private void Send(CampaignSession session, CampaignCommand command)
    {
        var revision = session.Revision;
        var id = "stone:" + commands++;
        var result = session.Execute(id, revision, command);
        Assert.IsTrue(result.Status == CommandStatus.Accepted, "public " + command.Kind + " accepted: " + result.Error);
        var retry = session.Execute(id, revision, command);
        Assert.IsTrue(retry.Status == CommandStatus.Rejected && retry.Error == "stale_revision" && retry.Revision == result.Revision && ReferenceEquals(retry.State, result.State), "stale retry atomic " + command.Kind);
    }

    private void Reject(CampaignSession session, CampaignCommand command, string error)
    {
        var before = session.State;
        var revision = session.Revision;
        var result = session.Execute("negative:" + commands++, revision, command);
        Assert.IsTrue(result.Status == CommandStatus.Rejected && result.Error == error && result.Revision == revision && ReferenceEquals(before, result.State), "atomic refusal " + error);
    }

    private void Drain(CampaignSession session)
    {
        for (int i = 0; i < 25 && session.State.Evolution is not null; i++)
        {
            var scene = session.State.Evolution;
            if (scene.Stage == CampaignEvolutionStage.LearnMove)
            {
                var member = session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId);
                var candidate = scene.PendingMoves[0].Move;
                Send(session, new("evolution_learn", MoveSlot: -1));
                GrowthResultTextFixture.AssertPages(catalog, session.State.Evolution!.Pages, "sText_DidNotLearnMove",
                    ("B_BUFF1", catalog.PokemonDisplayName(member)), ("B_BUFF2", catalog.BattleContent.MoveNames[candidate.Value]));
            }
            else Send(session, new("ack"));
        }
        Assert.IsTrue(session.State.Evolution is null && session.State.ItemMenu is { Origin: "bag", View: "bag" }, "evolution returns to original bag");
        Send(session, new("menu_back"));
    }

    private void Save(CampaignSession session, string name)
    {
        var validationError = session.ValidatePlayerSave(session.State);
        Assert.IsNull(validationError, name + " strict stable save: " + validationError);
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(catalog, decoded.State, decoded.Generation);
        Assert.IsTrue(restored.Revision == session.Revision && Hash(restored.State) == Hash(session.State), name + " exact strict codec and Restore");
    }

    private CampaignState Evolve(CampaignState fixture, string item, int target, string name, bool hm = false, bool learn = false)
    {
        var session = Session(fixture);
        int index = fixture.Party.Length - 1;
        var before = fixture.Party[index];
        Send(session, new("bag_open"));
        Reject(session, new("field_evolution", Text: item, PartyIndex: index, Quantity: 2), "evolution_item_command_invalid");
        Send(session, new("field_evolution", Text: item, PartyIndex: index));
        var after = session.State.Party[index];
        Assert.IsTrue(after.Pokemon.SourceSpeciesId == target && session.State.Evolution?.ItemUse is not null, name + " actual public stone evolution");
        var expectedEvolution = GrowthResultTextFixture.Pages(catalog, "gText_CongratsPkmnEvolved",
            ("STR_VAR_1", catalog.PokemonDisplayName(before)), ("STR_VAR_2", catalog.SourceSpeciesName(target)!));
        Assert.AreSequenceEqual(expectedEvolution, session.State.Evolution!.Pages.Take(expectedEvolution.Length));
        Assert.IsTrue(after.InstanceId == before.InstanceId && after.Pokemon.Personality == before.Pokemon.Personality && after.Pokemon.Ivs == before.Pokemon.Ivs && after.Pokemon.OriginalTrainerId == before.Pokemon.OriginalTrainerId && after.MetLevel == before.MetLevel && after.MetLocation == before.MetLocation, name + " original owned identity retained");
        Assert.IsTrue(Hash(after.Pokemon.Moves) == Hash(before.Pokemon.Moves), name + " existing PP, PP Ups and moves retained");
        Assert.IsTrue(before.Pokemon.CurrentHp == 0 ? after.Pokemon.CurrentHp == 0 : after.Pokemon.Stats.MaxHp - after.Pokemon.CurrentHp == before.Pokemon.Stats.MaxHp - before.Pokemon.CurrentHp, name + " living HP deficit or fainting retained");
        Assert.IsTrue(Quantity(session.State, item) == Quantity(fixture, item) - 1, name + " exactly one stone consumed");
        Assert.IsTrue(Hash(session.State.Exploration) == Hash(fixture.Exploration) && Hash(session.State.Storage) == Hash(fixture.Storage) && session.State.World.Rng == fixture.World.Rng, name + " capture ledger, storage and world RNG unchanged");
        Assert.IsTrue(session.State.EvolutionHistory!.Value[^1] is { BattleId: null } history && history.InstanceId == before.InstanceId && history.Item == item && history.Level == before.Pokemon.Level && history.LevelBeforeBattle == history.Level, name + " same-level stone history without invented battle");
        Assert.IsTrue(session.ValidatePlayerSave(session.State) == "save_event_active", name + " modal save refused");
        Reject(session, new("evolution_choice", Choice: 0), "evolution_decision_required");
        Reject(session, new("field_evolution", Text: item, PartyIndex: index), "evolution_decision_required");
        if (hm)
        {
            for (int i = 0; i < 10 && session.State.Evolution!.Stage != CampaignEvolutionStage.LearnMove; i++)
                Send(session, new("ack"));
            Assert.IsTrue(session.State.Evolution?.Stage == CampaignEvolutionStage.LearnMove, "Sun Stone exact-level move reaches decision");
            Reject(session, new("evolution_learn", MoveSlot: 0), "hm_move_cannot_be_forgotten");
            if (learn)
            {
                var candidate = session.State.Evolution!.PendingMoves[0].Move;
                Send(session, new("evolution_learn", MoveSlot: 1));
                GrowthResultTextFixture.AssertPages(catalog, session.State.Evolution!.Pages, "sText_PkmnLearnedMove",
                    ("B_BUFF1", catalog.PokemonDisplayName(session.State.Party[index])),
                    ("B_BUFF2", catalog.BattleContent.MoveNames[candidate.Value]));
                Assert.AreEqual(candidate, session.State.Party[index].Pokemon.Moves[1].Move);
            }
        }

        Drain(session);
        Save(session, name);
        var historyEntries = session.State.EvolutionHistory!.Value;
        Assert.IsTrue(inspector.ValidatePlayerSave(session.State with { EvolutionHistory = historyEntries.RemoveAt(historyEntries.Length - 1) })is not null, name + " missing evolution lineage rejected");
        Assert.IsTrue(inspector.ValidatePlayerSave(session.State with { EvolutionHistory = historyEntries.SetItem(historyEntries.Length - 1, historyEntries[^1] with { Item = item == "pret:ITEM_SUN_STONE" ? "pret:ITEM_MOON_STONE" : "pret:ITEM_SUN_STONE" }) })is not null, name + " wrong stone lineage rejected");
        return session.State;
    }

    [TestMethod]
    [DataRow("NIDORINA", "NIDOQUEEN")]
    [DataRow("NIDORINO", "NIDOKING")]
    [DataRow("CLEFAIRY", "CLEFABLE")]
    [DataRow("JIGGLYPUFF", "WIGGLYTUFF")]
    public void MoonStoneFamilies(string before, string after)
    {
        var mon = Factory(before, 22);
        var proposal = ItemEvolutionRules.Eligibility(content, mon)!;
        Assert.IsTrue(proposal.TargetSpecies.Value == "pret:SPECIES_" + after && !proposal.CanCancel, "source Moon Stone family " + before);
        var result = ItemEvolutionRules.Apply(content, mon, proposal);
        Assert.IsTrue(result.Pokemon.Pokemon.Species == proposal.TargetSpecies && result.Pokemon.InstanceId == mon.InstanceId, "apply exact owned family " + before);
    }

    [TestMethod]
    public void NidokingMoveAndIdentityGuards()
    {
        var nidorino = Factory("NIDORINO", 22);
        var full = nidorino.Pokemon.Moves;
        var cut = content.Catalog.Moves[new("pret:MOVE_CUT")];
        full = full.SetItem(0, new(cut.Id, 7, cut.Pp * 6 / 5, 1));
        nidorino = nidorino with
        {
            CustomNickname = "ROYAL",
            Pokemon = nidorino.Pokemon with
            {
                CurrentHp = nidorino.Pokemon.Stats.MaxHp - 9,
                Moves = full
            }
        };
        var nidProposal = ItemEvolutionRules.Eligibility(content, nidorino)!;
        var nidResult = ItemEvolutionRules.Apply(content, nidorino, nidProposal);
        Assert.IsTrue(nidResult.Pokemon.CustomNickname == "ROYAL" && Hash(nidResult.Pokemon.Pokemon.Moves) == Hash(full), "Nidorino keeps nickname, full HM moves and depleted PP Ups");
        Assert.IsTrue(nidResult.PendingMoves.Any(m => m.Move.Value == "pret:MOVE_THRASH") && nidResult.Pokemon.Pokemon.Stats.MaxHp - nidResult.Pokemon.Pokemon.CurrentHp == 9, "Nidoking exact level22 Thrash decision and HP deficit");
        Refuses(() => ItemEvolutionRules.Apply(content, nidorino with { InstanceId = 92 }, nidProposal), "stale different-member proposal refused");
        Refuses(() => ItemEvolutionRules.Apply(content, nidorino, nidProposal with { TargetSpecies = new("pret:SPECIES_CLEFABLE") }), "forged target refused");
        var copy = GamePokemonCatalog.Create(ownership.Mechanics, ownership.Catalog with { }, ownership.Opponents.Catalog with { }, ownership.TrainerTowerOpponents.Catalog with { });
        Assert.AreEqual(nidProposal, ItemEvolutionRules.Eligibility(content with { Pokemon = copy }, nidorino), "equivalent definitions preserve proposal");
        Refuses(() => ItemEvolutionRules.Eligibility(content, nidorino with { Pokemon = nidorino.Pokemon with { Species = new("pret:SPECIES_SUNKERN"), SourceSpeciesId = 191 } }), "opponent-only species cannot borrow ownership");
        Refuses(() => ItemEvolutionRules.Eligibility(content, nidorino, "ITEM_KINGS_ROCK"), "trade evolution item refused");
        Refuses(() => ItemEvolutionRules.SourceEvolution(content, new("pret:SPECIES_SUNKERN"), ItemEvolutionRules.SunStone), "trainer-only Sunkern cannot use source edge as ownership");
        var edge = SixIslandItemEvolutionRules.EvolutionEdge(catalog.GamePokemon!);
        Assert.AreEqual("EVO_ITEM", edge.Method);
        Assert.AreEqual("ITEM_SUN_STONE", edge.Parameter);
        Assert.AreEqual("pret:SPECIES_BELLOSSOM", edge.Target.Value);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MoonStonePublicEvolution(bool translated)
    {
        catalog = GrowthResultTextFixture.WithTranslations(catalog, translated);
        var moon = Fixture(35, "pret:ITEM_MOON_STONE");
        Evolve(moon, "pret:ITEM_MOON_STONE", 36, "moon-clefable");
    }

    [TestMethod]
    public void SunStonePublicEvolution()
    {
        var sun = Fixture(44, "pret:ITEM_SUN_STONE");
        Evolve(sun, "pret:ITEM_SUN_STONE", 182, "sun-bellossom");
    }

    [TestMethod]
    [DataRow(35, "pret:ITEM_MOON_STONE")]
    [DataRow(44, "pret:ITEM_SUN_STONE")]
    public void EverstoneNoEffect(int species, string item)
    {
        var fixture = Fixture(species, item);
        int index = fixture.Party.Length - 1;
        var mon = fixture.Party[index] with
        {
            HeldItem = "ITEM_EVERSTONE"
        };
        var held = fixture with
        {
            Party = fixture.Party.SetItem(index, mon)
        };
        Assert.IsTrue(ItemEvolutionRules.PartyMenuTarget(content, mon, item[5..])is not null && ItemEvolutionRules.Eligibility(content, mon, item[5..])is null, item + " Everstone menu compatibility differs from actual use");
        var session = Session(held);
        Send(session, new("bag_open"));
        var before = session.State;
        Send(session, new("field_evolution", Text: item, PartyIndex: index));
        Assert.IsTrue(session.State.Evolution is null && session.State.ItemMenu?.MessageSymbol == "gText_WontHaveEffect" && Hash(session.State with { ItemMenu = before.ItemMenu }) == Hash(before), item + " public Everstone no effect and no consumption");
    }

    [TestMethod]
    public void NicknameWoundsAndPpUps()
    {
        var sun = Fixture(44, "pret:ITEM_SUN_STONE");
        var wounded = sun.Party[^1];
        var pp = content.Catalog.Moves[wounded.Pokemon.Moves[0].Move].Pp;
        wounded = wounded with
        {
            CustomNickname = "PETAL",
            Pokemon = wounded.Pokemon with
            {
                CurrentHp = wounded.Pokemon.Stats.MaxHp - 11,
                Moves = wounded.Pokemon.Moves.SetItem(0, wounded.Pokemon.Moves[0] with { PpUps = 1, MaximumPp = pp * 6 / 5, CurrentPp = 3 })
            }
        };
        var named = Evolve(sun with { Party = sun.Party.SetItem(sun.Party.Length - 1, wounded) }, "pret:ITEM_SUN_STONE", 182, "sun-named-wounded");
        Assert.IsTrue(named.Party[^1].CustomNickname == "PETAL", "public custom nickname retained");
    }

    [TestMethod]
    public void FaintedEvolution()
    {
        var sun = Fixture(44, "pret:ITEM_SUN_STONE");
        Evolve(sun with { Party = sun.Party.SetItem(sun.Party.Length - 1, sun.Party[^1] with { Pokemon = sun.Party[^1].Pokemon with { CurrentHp = 0 } }) }, "pret:ITEM_SUN_STONE", 182, "sun-fainted");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void HmLearningDecision(bool translated, bool learn)
    {
        catalog = GrowthResultTextFixture.WithTranslations(catalog, translated);
        var sun = Fixture(44, "pret:ITEM_SUN_STONE");
        var sourceGloom = sun.Party[^1];
        var bellossom = content.Catalog.Species[new("pret:SPECIES_BELLOSSOM")];
        int moveLevel = bellossom.LevelUpMoves.First(m => m.Level >= sourceGloom.MetLevel && m.Level > 1).Level;
        var raised = CurrentFixturePokemon.AtLevel(content, sourceGloom, moveLevel).Pokemon;
        Assert.IsTrue(raised.Ivs == sourceGloom.Pokemon.Ivs, "controlled raised Gloom keeps capture IVs");
        var hmMoves = new[]
        {
            "CUT",
            "TACKLE",
            "GROWL",
            "POUND"
        }.Select(symbol => content.Catalog.Moves[new("pret:MOVE_" + symbol)]).Select(m => new PokemonMoveSlot(m.Id, m.Pp, m.Pp)).ToImmutableArray();
        var hmGloom = sourceGloom with
        {
            Pokemon = raised with
            {
                Moves = hmMoves
            }
        };
        Evolve(sun with { Party = sun.Party.SetItem(sun.Party.Length - 1, hmGloom) }, "pret:ITEM_SUN_STONE", 182, "sun-hm-decision", hm: true, learn: learn);
    }
}
