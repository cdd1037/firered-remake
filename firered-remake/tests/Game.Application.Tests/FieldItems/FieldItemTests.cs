using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Application.FieldItems;
[TestClass, TestCategory("Game")]
public sealed class FieldItemTests
{
    private global::Game.Content.PretCampaignCatalog catalog = default !;
    private global::Game.Application.CampaignSession fresh = default !;
    private byte[] originalBytes = default !;
    private global::Game.Application.LoadedSave<global::Game.Application.CampaignState> checkpoint = default !;
    private global::Game.Application.CampaignState baseFixture = default !;
    private global::Game.Domain.Pokemon.PartyPokemon lead = default !;
    private global::Game.Domain.Pokemon.PokemonInstance prepared = default !;
    private global::Game.Application.CampaignState fixture = default !;
    private global::Game.Domain.World.IFieldMap map = default !;
    private (int x, int y) cell = default !;
    [TestInitialize]
    public void SetUp()
    {
        catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        fresh = CampaignSession.CreateCurrent(catalog);
        originalBytes = File.ReadAllBytes(TestAssets.Checkpoint);
        checkpoint = SaveCodec.Decode<CampaignState>(originalBytes, CurrentGameProfile.RulesVersion, catalog.ContentVersion, validate: fresh.ValidatePlayerSave);
        baseFixture = CurrentFixturePokemon.BringToLead(catalog, checkpoint.State, 130);
        lead = baseFixture.Party[0];
        prepared = lead.Pokemon with
        {
            CurrentHp = 0,
            Moves = lead.Pokemon.Moves.Select(m => m with { CurrentPp = 0 }).ToImmutableArray()
        };
        fixture = baseFixture with
        {
            Party = baseFixture.Party.SetItem(0, lead with { Pokemon = prepared, Status = 0 })
        };
        map = catalog.Maps[new("map.pallet-town")];
        cell = Enumerable.Range(1, map.Height - 2).SelectMany(y => Enumerable.Range(1, map.Width - 3).Select(x => (x, y))).First(p => map.GetTile(p.x, p.y)is { Blocked: false } first && map.GetTile(p.x + 1, p.y)is { Blocked: false } next && first.Elevation == next.Elevation && !catalog.MapDefinition(map.Id).Actors.Any(a => a.X >= p.x && a.X <= p.x + 1 && a.Y == p.y));
        fixture = fixture with
        {
            World = fixture.World with
            {
                Player = new(map.Id, cell.x, cell.y, map.GetTile(cell.x, cell.y)!.Elevation, Direction.East)
            },
            Actors = catalog.MapDefinition(map.Id).Actors.ToImmutableSortedDictionary(a => a.LocalId, a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation, Direction.South, a.MovementType, a.ScriptId, a.HideFlag, false, false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = ImmutableSortedDictionary<int, string>.Empty.Add(0, "LOCALID_PLAYER"),
            SelectedObjectSlot = 0,
            LastTalked = null,
            MapEdits = null,
            IsSurfing = false,
            IsCycling = false
        };
        Assert.IsTrue(Environment.Version.Major == 10, "actual .NET 10");
    }

    private void Same<T>(T expected, T actual, string name) => Assert.IsTrue(CanonicalJson.Hash(expected) == CanonicalJson.Hash(actual), name);
    private ItemId Item(string symbol) => new("pret:ITEM_" + symbol);
    private int Count(CampaignState state, string symbol) => OpeningInventoryRules.Quantity(state.Inventory!.BagItems, Item(symbol));
    private CampaignState Supply(CampaignState state, params (string Symbol, int Quantity)[] supplies)
    {
        var bag = state.Inventory!.BagItems;
        foreach (var(symbol, quantity)in supplies)
        {
            var id = Item(symbol);
            bag = bag.RemoveAll(s => s.Item == id).Add(new(id, quantity));
        }

        return state with
        {
            Inventory = state.Inventory with
            {
                BagItems = bag
            }
        };
    }

    private CampaignSession Restore(CampaignState state)
    {
        Assert.IsTrue(fresh.ValidatePlayerSave(state)is null, "controlled checkpoint strictly valid: " + fresh.ValidatePlayerSave(state));
        var session = CampaignSession.RestoreCurrent(catalog, state, checkpoint.Generation);
        Same(state, session.State, "Restore preserves full state");
        return session;
    }

    private CommandOutcome<CampaignState> Run(CampaignSession session, string label, CampaignCommand command, CommandStatus expected, string? error = null)
    {
        var before = session.State;
        long revision = session.Revision;
        var outcome = session.Execute(label, revision, command);
        Assert.IsTrue(outcome.Status == expected && outcome.Error == error, label + ": expected status/error, actual " + outcome.Status + "/" + outcome.Error);
        if (expected != CommandStatus.Accepted)
        {
            Same(before, session.State, label + ": rejected/faulted state is atomic");
            Assert.IsTrue(session.Revision == revision, label + ": revision unchanged");
        }

        return outcome;
    }

    private void Accepted(CampaignSession s, string label, CampaignCommand command) => Run(s, label, command, CommandStatus.Accepted);
    private void Reject(CampaignSession s, string label, CampaignCommand command, string error) => Run(s, label, command, CommandStatus.Rejected, error);
    private void OtherPockets(CampaignState before, CampaignState after, string label)
    {
        Same(before.Inventory!.KeyItems, after.Inventory!.KeyItems, label + ": all existing key items preserved");
        Same(before.Inventory.Balls, after.Inventory.Balls, label + ": balls preserved");
        Same(before.Inventory.Machines, after.Inventory.Machines, label + ": machines preserved");
        Same(before.Inventory.Berries, after.Inventory.Berries, label + ": berries preserved");
        Same(before.Inventory.PcItems, after.Inventory.PcItems, label + ": PC preserved");
    }

    private void DrainCandy(CampaignSession session, string label)
    {
        for (int n = 0; n < 12 && session.State.RareCandy is { Stage: not CampaignRareCandyStage.LearnMove }; n++)
            Accepted(session, label + " page " + n, new("ack"));
        Assert.IsTrue(session.State.RareCandy is null or { Stage: CampaignRareCandyStage.LearnMove }, label + ": finite candy pages");
    }

    private void ProofRejected(CampaignPokemonContent content, OwnedBattlePokemonCatalog ownership, string label)
    {
        try
        {
            RareCandyRules.Apply(content, ownership, Supply(fixture, ("RARE_CANDY", 2)).Party[0], "MAPSEC_ONE_ISLAND");
        }
        catch (ArgumentException ex)when (ex.Message.Contains("rare_candy_owned_catalog_identity", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidDataException(label + ": unauthenticated identity accepted");
    }

    [TestMethod]
    [DataRow("REPEL", 100)]
    [DataRow("SUPER_REPEL", 200)]
    [DataRow("MAX_REPEL", 250)]
    public void RepelVariants(string symbol, int duration)
    {
        var start = Supply(fixture, (symbol, 2));
        var s = Restore(start);
        Accepted(s, symbol + " open bag", new("bag_open"));
        Reject(s, symbol + " unsupported item", new("field_repel", Text: Item("POTION").Value), "field_repel_context_invalid");
        Accepted(s, symbol + " request", new("field_repel", Text: Item(symbol).Value, Quantity: 2));
        Assert.IsTrue(Count(s.State, symbol) == 2 && s.RepelSteps == 0, symbol + ": sound wait defers all consumption");
        Assert.IsTrue(s.State.Repel?.Phase == CampaignRepelPhase.UseSound && s.State.PresentationEffects.Any(e => e.Opcode == "playse" && e.Args.SequenceEqual(new[] { "SE_REPEL" })), symbol + ": sound and wait exposed");
        string wait = s.State.Repel!.WaitId;
        Reject(s, symbol + " wrong acknowledgement", new("repel_presented", Text: wait + "-wrong"), "repel_sound_pending");
        Reject(s, symbol + " cannot cancel sound", new("menu_back"), "repel_sound_pending");
        Accepted(s, symbol + " matching acknowledgement", new("repel_presented", Text: wait));
        Assert.IsTrue(Count(s.State, symbol) == 1 && s.RepelSteps == duration && s.State.Repel is null, symbol + ": exactly one consumed at acknowledgement");
        Assert.IsTrue(s.State.ItemMenu?.MessageSymbol == "gText_PlayerUsedVar2" && s.State.PresentationEffects.All(e => e.Opcode != "playse" || !e.Args.SequenceEqual(new[] { "SE_REPEL" })), symbol + ": original message and sound retirement");
        OtherPockets(start, s.State, symbol);
        Reject(s, symbol + " repeated acknowledgement", new("repel_presented", Text: wait), "item_menu_active");
        Accepted(s, symbol + " dismiss used message", new("menu_ack"));
        Accepted(s, symbol + " effects linger", new("field_repel", Text: Item(symbol).Value));
        Assert.IsTrue(Count(s.State, symbol) == 1 && s.RepelSteps == duration && s.State.ItemMenu?.MessageSymbol == "gText_RepelEffectsLingered", symbol + ": lingering use preserves inventory/counter");
        Accepted(s, symbol + " dismiss lingering", new("menu_ack"));
        Accepted(s, symbol + " close bag", new("menu_back"));
        Accepted(s, symbol + " walking step", new("move", Direction.East));
        Assert.IsTrue(s.RepelSteps == duration - 1, symbol + ": one successful public walking step");
    }

    [TestMethod]
    public void RepelExpiry()
    {
        var expiring = Restore(fixture with { Script = fixture.Script with { Variables = fixture.Script.Variables.SetItem(CampaignSession.RepelCounterVariable, 1) } });
        Accepted(expiring, "repel expiry walking step", new("move", Direction.East));
        Assert.IsTrue(expiring.RepelSteps == 0 && expiring.State.Repel?.Phase == CampaignRepelPhase.WoreOff, "final repel step exposes expiry modal");
        Reject(expiring, "repel expiry cannot move", new("move", Direction.West), "repel_expiration_message_pending");
        Accepted(expiring, "repel expiry acknowledgement", new("ack"));
    }

    [TestMethod]
    public void FieldMedicine()
    {
        var medicineStart = Supply(fixture, ("REVIVE", 2), ("ETHER", 2), ("MAX_ELIXIR", 2));
        var medicine = Restore(medicineStart);
        Accepted(medicine, "medicine open bag", new("bag_open"));
        Reject(medicine, "revive invalid slot", new("field_medicine", Text: Item("REVIVE").Value, MoveSlot: 0), "medicine_invalid_slot");
        Accepted(medicine, "revive use", new("field_medicine", Text: Item("REVIVE").Value, Quantity: 2));
        Assert.IsTrue(Count(medicine.State, "REVIVE") == 1 && medicine.State.Party[0].Pokemon.CurrentHp == medicineStart.Party[0].Pokemon.Stats.MaxHp / 2, "revive consumes one and restores half HP");
        Accepted(medicine, "revive dismiss", new("menu_ack"));
        Accepted(medicine, "revive no effect", new("field_medicine", Text: Item("REVIVE").Value));
        Assert.IsTrue(Count(medicine.State, "REVIVE") == 1 && medicine.State.ItemMenu?.MessageSymbol == "gText_WontHaveEffect", "revive no effect is not consumed");
        Accepted(medicine, "revive no effect dismiss", new("menu_ack"));
        Reject(medicine, "ether omitted slot", new("field_medicine", Text: Item("ETHER").Value), "medicine_invalid_slot");
        Accepted(medicine, "ether use", new("field_medicine", Text: Item("ETHER").Value, MoveSlot: 1));
        Assert.IsTrue(Count(medicine.State, "ETHER") == 1 && medicine.State.Party[0].Pokemon.Moves[1].CurrentPp == 10, "ether restores selected depleted move");
        Accepted(medicine, "ether dismiss", new("menu_ack"));
        Accepted(medicine, "max elixir use", new("field_medicine", Text: Item("MAX_ELIXIR").Value));
        Assert.IsTrue(Count(medicine.State, "MAX_ELIXIR") == 1 && medicine.State.Party[0].Pokemon.Moves.All(m => m.CurrentPp == m.MaximumPp), "max elixir restores every move once");
        OtherPockets(medicineStart, medicine.State, "medicine");
    }

    [TestMethod]
    public void RareCandy()
    {
        var candyStart = Supply(fixture, ("RARE_CANDY", 2));
        var candy = Restore(candyStart);
        Accepted(candy, "candy open bag", new("bag_open"));
        Reject(candy, "candy quantity guard", new("field_rare_candy", Text: Item("RARE_CANDY").Value, Quantity: 2), "field_rare_candy_context_invalid");
        Reject(candy, "candy move slot guard", new("field_rare_candy", Text: Item("RARE_CANDY").Value, MoveSlot: 0), "field_rare_candy_context_invalid");
        Reject(candy, "candy party index guard", new("field_rare_candy", Text: Item("RARE_CANDY").Value, PartyIndex: 6), "item_party_index_invalid");
        Accepted(candy, "candy current source admission", new("field_rare_candy", Text: Item("RARE_CANDY").Value));
        Assert.IsTrue(OwnedBattlePokemonCatalog.For(catalog.PokemonContent!.Catalog)is not null, "candy uses the current owned catalog registry");
        Assert.IsTrue(Count(candy.State, "RARE_CANDY") == 1 && candy.State.Party[0].Pokemon.Level == lead.Pokemon.Level + 1, "current candy raises the prepared original member exactly once");
        Same(candyStart.Party[0].Pokemon.Moves, candy.State.Party[0].Pokemon.Moves, "candy preserves existing PP and moves");
        Same(candyStart.Party[0], candy.State.Party[0] with { Pokemon = candyStart.Party[0].Pokemon }, "candy preserves full owned identity");
        Same(candyStart.World.Rng, candy.State.World.Rng, "candy draws no world RNG");
        Same(candyStart.Exploration, candy.State.Exploration, "candy draws no encounter RNG");
        OtherPockets(candyStart, candy.State, "candy");
        DrainCandy(candy, "candy current source");
        Assert.IsTrue(candy.State.ItemMenu?.View == "bag" && candy.State.RareCandy is null, "candy finishes original bag UI");
    }

    [TestMethod]
    public void RareCandyProofIdentity()
    {
        var candyStart = Supply(fixture, ("RARE_CANDY", 2));
        var ownership = OwnedBattlePokemonCatalog.For(catalog.PokemonContent!.Catalog)!;
        ProofRejected(catalog.PokemonContent with { Catalog = catalog.PokemonContent!.Catalog with { } }, ownership, "candy rejects reconstructed current catalog identity");
        ProofRejected(catalog.PokemonContent, OwnedBattlePokemonCatalog.For(catalog.FiveIslandOwnership!.Catalog)!, "candy rejects wrong predecessor proof");
    }

    [TestMethod]
    public void Level100Candy()
    {
        var candyStart = Supply(fixture, ("RARE_CANDY", 2));
        var member = candyStart.Party[0];
        var pokemon = member.Pokemon;
        var species = catalog.PokemonContent!.Catalog.Species[pokemon.Species];
        var basis = species.BaseStats;
        int Other(int b, int iv, int ev, Stat stat) => StatRules.CalculateOther(b, iv, ev, 100, pokemon.Nature, stat);
        var maximum = pokemon with
        {
            Level = 100,
            Experience = GrowthRules.ExperienceAtLevel(species.GrowthRate, 100),
            StatCalculationEvs = null,
            Stats = new(StatRules.CalculateHp(basis.Hp, pokemon.Ivs.Hp, pokemon.Evs.Hp, 100), Other(basis.Attack, pokemon.Ivs.Attack, pokemon.Evs.Attack, Stat.Attack), Other(basis.Defense, pokemon.Ivs.Defense, pokemon.Evs.Defense, Stat.Defense), Other(basis.SpecialAttack, pokemon.Ivs.SpecialAttack, pokemon.Evs.SpecialAttack, Stat.SpecialAttack), Other(basis.SpecialDefense, pokemon.Ivs.SpecialDefense, pokemon.Evs.SpecialDefense, Stat.SpecialDefense), Other(basis.Speed, pokemon.Ivs.Speed, pokemon.Evs.Speed, Stat.Speed))
        };
        var cappedStart = candyStart with
        {
            Party = candyStart.Party.SetItem(0, member with { Pokemon = maximum })
        };
        var capped = Restore(cappedStart);
        Accepted(capped, "level100 open bag", new("bag_open"));
        Accepted(capped, "level100 candy no effect", new("field_rare_candy", Text: Item("RARE_CANDY").Value));
        Same(cappedStart.Party, capped.State.Party, "level100 preserves fainted HP, PP, stats and identity");
        Same(cappedStart.Inventory, capped.State.Inventory, "level100 consumes no candy");
        Assert.IsTrue(capped.State.ItemMenu?.MessageSymbol == "gText_WontHaveEffect", "level100 no-effect UI");
    }

    [TestMethod]
    public void RareCandyLearningAndHmProtection()
    {
        var candyStart = Supply(fixture, ("RARE_CANDY", 2));
        int learningIndex = Enumerable.Range(0, candyStart.Party.Length).Single(i => candyStart.Party[i].Pokemon.Species.Value == "pret:SPECIES_ARCANINE");
        var learner = candyStart.Party[learningIndex];
        var learningSpecies = catalog.PokemonContent!.Catalog.Species[learner.Pokemon.Species];
        var learnMove = learningSpecies.LevelUpMoves.First(m => m.Level > learner.Pokemon.Level && !learner.Pokemon.Moves.Any(s => s.Move == m.Move));
        learner = CurrentFixturePokemon.AtLevel(catalog.PokemonContent, learner, learnMove.Level - 1);
        var cut = new PokemonMoveSlot(new("pret:MOVE_CUT"), 7, 48, 3);
        learner = learner with
        {
            Pokemon = learner.Pokemon with
            {
                Moves = learner.Pokemon.Moves.SetItem(0, cut)
            }
        };
        var learningStart = candyStart with
        {
            Party = candyStart.Party.SetItem(1, learner).SetItem(learningIndex, candyStart.Party[1])
        };
        var hmNames = new[]
        {
            "CUT",
            "FLY",
            "SURF",
            "STRENGTH",
            "FLASH",
            "ROCK_SMASH",
            "WATERFALL",
            "DIVE"
        }.Select(n => "pret:MOVE_" + n).ToHashSet(StringComparer.Ordinal);
        int replacementSlot = Enumerable.Range(1, learner.Pokemon.Moves.Length - 1).First(i => !hmNames.Contains(learner.Pokemon.Moves[i].Move.Value));
        var learning = Restore(learningStart);
        Accepted(learning, "candy learning open bag", new("bag_open"));
        Accepted(learning, "candy reaches next source learning level", new("field_rare_candy", Text: Item("RARE_CANDY").Value, PartyIndex: 1));
        DrainCandy(learning, "candy learning");
        Assert.IsTrue(learning.State.RareCandy?.Stage == CampaignRareCandyStage.LearnMove && learning.State.RareCandy.PendingMoves[0].Move == learnMove.Move, "next source move awaits explicit replacement");
        Reject(learning, "candy learning protects HM", new("rare_candy_learn", MoveSlot: 0), "hm_move_cannot_be_forgotten");
        Accepted(learning, "candy learns source move", new("rare_candy_learn", MoveSlot: replacementSlot));
        DrainCandy(learning, "candy learned move");
        Assert.IsTrue(learning.State.Party[1].Pokemon.Moves[replacementSlot].Move == learnMove.Move && learning.State.Party[1].Pokemon.Moves[replacementSlot].CurrentPp == learning.State.Party[1].Pokemon.Moves[replacementSlot].MaximumPp, "learned move receives full PP");
        Same(cut, learning.State.Party[1].Pokemon.Moves[0], "existing HM depletion and PP Ups preserved");
        Same(learningStart.Party[1], learning.State.Party[1] with { Pokemon = learningStart.Party[1].Pokemon }, "learning preserves owned identity");
        Assert.IsTrue(Count(learning.State, "RARE_CANDY") == 1, "learning pages never consume candy again");
    }

    [TestMethod]
    public void RareCandyEvolution()
    {
        var candyStart = Supply(fixture, ("RARE_CANDY", 2));
        var evolutionStart = CurrentFixturePokemon.BringToLead(catalog, candyStart, 23);
        var evolving = Restore(evolutionStart);
        Accepted(evolving, "candy evolution open bag", new("bag_open"));
        Accepted(evolving, "candy triggers delayed Ekans evolution", new("field_rare_candy", Text: Item("RARE_CANDY").Value, PartyIndex: 0));
        DrainCandy(evolving, "candy evolution");
        Assert.IsTrue(evolving.State.Evolution?.Proposal.TargetSpecies.Value == "pret:SPECIES_ARBOK", "legal source Ekans evolution proposed");
        Accepted(evolving, "candy accept Arbok evolution", new("evolution_choice", Choice: 1));
        for (int n = 0; n < 12 && evolving.State.Evolution is { } scene; n++)
            Accepted(evolving, "candy evolution result " + n, scene.Stage == CampaignEvolutionStage.LearnMove ? new("evolution_learn", MoveSlot: -1) : new("ack"));
        Assert.IsTrue(evolving.State.Evolution is null && evolving.State.ItemMenu?.View == "bag" && evolving.State.Party[0].Pokemon.Species.Value == "pret:SPECIES_ARBOK", "candy evolution returns to bag");
        Assert.IsTrue(evolving.State.Party[0].InstanceId == evolutionStart.Party[0].InstanceId && Count(evolving.State, "RARE_CANDY") == 1, "evolution preserves identity and once-only consumption");
        Same(evolutionStart.Party[0].Pokemon.Moves, evolving.State.Party[0].Pokemon.Moves, "evolution preserves all existing PP");
        Assert.IsTrue(evolving.State.EvolutionHistory!.Value[^1].Item == Item("RARE_CANDY").Value, "evolution records original candy item");
    }

    [TestMethod]
    public void MachineTeaching()
    {
        var machineStart = fixture with
        {
            Inventory = fixture.Inventory!with
            {
                Machines = (fixture.Inventory!.Machines ?? []).RemoveAll(s => s.Item == Item("TM03")).Add(new(Item("TM03"), 1))
            }
        };
        var machine = Restore(machineStart);
        Accepted(machine, "machine open bag", new("bag_open"));
        Accepted(machine, "machine open case", new("machine_open"));
        Reject(machine, "machine unsupported ID", new("machine_teach", Text: "pret:ITEM_TM99"), "inventory_machine_unsupported");
        Reject(machine, "machine default TM39 compatibility", new("machine_teach", MoveSlot: 1), "machine_incompatible");
        Reject(machine, "machine negative replacement", new("machine_teach", Text: Item("TM03").Value, MoveSlot: -2), "machine_invalid_slot");
        Reject(machine, "machine HM protection", new("machine_teach", Text: Item("TM03").Value, MoveSlot: 0), "machine_cannot_forget_hm");
        Accepted(machine, "machine exact TM03 teach", new("machine_teach", Text: Item("TM03").Value, MoveSlot: 1));
        Assert.IsTrue(OpeningInventoryRules.Quantity(machine.State.Inventory!.Machines!.Value, Item("TM03")) == 0 && OpeningInventoryRules.Quantity(machine.State.Inventory.Machines.Value, PewterMachineRules.Tm39Id) == 1 && machine.State.Party[0].Pokemon.Moves[1].Move.Value == "pret:MOVE_WATER_PULSE", "explicit TM03 consumes precise item and replaces selected move");
        Same(checkpoint.State.Inventory!.KeyItems, machine.State.Inventory.KeyItems, "machine keeps every key item");
        Assert.IsTrue(machine.State.Party[0].InstanceId == machineStart.Party[0].InstanceId, "machine preserves owned identity");
        Assert.IsTrue(machine.State.ItemMenu?.MessageSymbol == "gText_PkmnLearnedMove3" && machine.State.ItemMenu.ReturnView == "machines", "machine result uses existing UI return");
    }
}
