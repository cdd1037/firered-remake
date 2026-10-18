using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Encounters;
using Game.Domain.World;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.Items;
using Game.Domain.FirstBattle;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Battle.Breeding;
[TestClass, TestCategory("Game")]
public sealed class EggFactoryTests
{
    private global::Game.Domain.Pokemon.CampaignPokemonContent baseline = default !;
    private global::Game.Domain.Pokemon.GamePokemonCatalog final = default !;
    private global::Game.Domain.Pokemon.GamePokemonCatalog ordinary = default !;
    private global::Game.Domain.Pokemon.GamePokemonCatalog owned = default !;
    private global::Game.Domain.Pokemon.PokemonBreedingData metadata = default !;
    private global::Game.Domain.Pokemon.CampaignPokemonContent content = default !;
    private global::Game.Domain.Pokemon.SourceEggTrainer trainer = default !;
    private long nextId = default !;
    private global::Game.Domain.Pokemon.PartyPokemon female = default !;
    private global::Game.Domain.Pokemon.PartyPokemon male = default !;
    private global::Game.Domain.Pokemon.FourIslandDayCareState parents = default !;
    private string before = default !;
    private global::Game.Domain.Pokemon.SourceEggCreationResult received = default !;
    private global::Game.Domain.Pokemon.PartyPokemon egg = default !;
    [TestInitialize]
    public void SetUp()
    {
        baseline = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root)).PokemonContent!;
        final = baseline.Pokemon;
        ordinary = final;
        owned = final;
        metadata = owned.Catalog.BreedingData!;
        content = baseline;
        trainer = new SourceEggTrainer(0x12345678, "RED", 0);
        nextId = 1;
        female = Make();
        male = Make(pid: 255, iv: 27);
        parents = Pair(female, male);
        before = JsonSerializer.Serialize(parents);
        received = Egg(parents);
        egg = received.Egg;
    }

    private void Throws(Action action, string label)
    {
        bool threw = false;
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            threw = true;
        }
        catch (NotSupportedException)
        {
            threw = true;
        }
        catch (OverflowException)
        {
            threw = true;
        }

        Assert.IsTrue(threw, label);
    }

    private PartyPokemon Make(string symbol = "BULBASAUR", uint pid = 0, int iv = 3, params string[] moves)
    {
        var p = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_" + symbol), 30, 42, IvPolicy.Fixed(iv), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
        if (moves.Length != 0)
            p = p with
            {
                Moves = moves.Select(symbol =>
                {
                    var m = content.Catalog.Moves[new("pret:MOVE_" + symbol)];
                    return new PokemonMoveSlot(m.Id, m.Pp, m.Pp);
                }).ToImmutableArray()
            };
        return new(nextId++, p, content.SpeciesNames[p.SourceSpeciesId], "LEAF", 1, 2, "MAPSEC_ROUTE_1", 30, 4, "ITEM_POKE_BALL", "ITEM_NONE");
    }

    private FourIslandDayCareState Pair(PartyPokemon a, PartyPokemon b, ushort low = 123) => new(new(a, 7), new(b, 19), low, 51);
    private SourceEggCreationResult Egg(FourIslandDayCareState parents, uint seed = 0) => SourceEggRules.Receive(content, metadata, parents, nextId++, trainer, "MAPSEC_FOUR_ISLAND", 4, new(seed));
    private string Moves(PartyPokemon p) => string.Join(",", p.Pokemon.Moves.Select(m => m.Move.Value[10..]));
    private BattleStats Stats(PokemonInstance p, SixStats ivs)
    {
        var b = content.Catalog.Species[p.Species].BaseStats;
        int Other(int basis, int iv, Stat s) => StatRules.CalculateOther(basis, iv, 0, 5, p.Nature, s);
        return new(StatRules.CalculateHp(b.Hp, ivs.Hp, 0, 5), Other(b.Attack, ivs.Attack, Stat.Attack), Other(b.Defense, ivs.Defense, Stat.Defense), Other(b.SpecialAttack, ivs.SpecialAttack, Stat.SpecialAttack), Other(b.SpecialDefense, ivs.SpecialDefense, Stat.SpecialDefense), Other(b.Speed, ivs.Speed, Stat.Speed));
    }

    [TestMethod]
    public void ReceiptAndValidation()
    {
        Assert.IsTrue(Environment.Version.Major == 10, "actual runtime10");
        Assert.IsTrue(metadata.Species.Keys.ToHashSet().SetEquals(content.Catalog.Species.Keys), "breeding covers every owned species");
        Assert.AreEqual(58, metadata.AllMachineMoves.Length, "all original inheritance machine slots");
        Assert.IsTrue(PokemonStateValidator.ValidatePartyCollection(content, [], 1)is null, "authenticated content names and owner");
        Assert.IsTrue(received.Trace.Length == 9 && received.Rng.Cursor == 9, "receipt exactly9 draws");
        Assert.IsTrue((egg.Pokemon.Personality & 0xffff) == 123 && egg.Pokemon.Personality >> 16 == received.Trace[0].Raw, "stored pending low and fresh high PID");
        Assert.IsTrue(egg.IsEgg && egg.Language == 1 && egg.Nickname == "タマゴ" && egg.Pokemon.Friendship == 20 && egg.MetLevel == 0, "source Japanese egg and cycle metadata");
        Assert.IsTrue(egg.Pokemon.Level == 5 && egg.Pokemon.OriginalTrainerId == trainer.TrainerId && egg.OriginalTrainerName == "RED" && egg.HeldItem == "ITEM_NONE" && egg.Ball == "ITEM_POKE_BALL", "source receipt OT level ball no item");
        Assert.IsTrue(before == JsonSerializer.Serialize(parents), "receipt leaves parents and pending state unchanged");
        Assert.IsTrue(egg.Pokemon.Stats == Stats(egg.Pokemon, egg.Pokemon.EggStatCalculationIvs!), "receipt stats retain initial random IV basis");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, egg)is null, "valid egg accepted by shared party validator");
        Assert.IsTrue(PokemonStateValidator.Validate(content.Catalog, egg.Pokemon) == "pokemon_egg_instance", "ordinary instance validator refuses egg stat marker");
        Assert.IsTrue(SourcePokemonValidator.ValidatePermanent(owned.Mechanics, egg.Pokemon) == "pokemon_egg_instance", "ordinary battle permanent validator refuses egg");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, egg with { IsEgg = false }) == "pokemon_egg_instance", "stripping party egg bit cannot relax ordinary stats");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, egg with { Pokemon = egg.Pokemon with { EggStatCalculationIvs = null } }) == "pokemon_egg_stat_basis", "egg requires explicit basis even when stats happen to match");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, egg with { Pokemon = egg.Pokemon with { EggStatCalculationIvs = new(32, 0, 0, 0, 0, 0) } }) == "pokemon_egg_stat_basis", "egg basis range checked");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, egg with { Pokemon = egg.Pokemon with { Friendship = 21 } }) == "pokemon_egg_state", "cycles bounded by authenticated species");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, egg with { Pokemon = egg.Pokemon with { Evs = new(1, 0, 0, 0, 0, 0) } }) == "pokemon_egg_state", "egg EVs forbidden");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, egg with { Nickname = "EGG" }) == "party_nickname" && PokemonStateValidator.ValidateParty(content, egg with { Language = 2 }) == "party_egg_metadata", "egg language and nickname exact");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, female with { Pokemon = female.Pokemon with { Stats = female.Pokemon.Stats with { Attack = 99 } } }) == "pokemon_stats", "ordinary stat validation remains strict");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, egg with { Status = 8 }) == "party_egg_metadata" && PokemonStateValidator.ValidateParty(content, egg with { HeldItem = "ITEM_ORAN_BERRY" }) == "party_egg_metadata", "eggs reject status and held items");
        var evolved = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_VENUSAUR"), 5, trainer.TrainerId, IvPolicy.Fixed(3), PersonalityPolicy.Fixed(123)), new(0)).Pokemon;
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, egg with { Pokemon = evolved with { EggStatCalculationIvs = evolved.Ivs, Friendship = 20 } }) == "pokemon_egg_species", "evolved species cannot forge supported egg state");
        var roundtrip = JsonSerializer.Deserialize<PartyPokemon>(JsonSerializer.Serialize(egg))!;
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, roundtrip)is null && JsonSerializer.Serialize(roundtrip) == JsonSerializer.Serialize(egg), "egg JSON roundtrip keeps hidden basis");
        Assert.IsTrue(MoveTutorRules.Eligibility(new(0, new("pret:MOVE_MEGA_PUNCH"), 5, 20, ImmutableDictionary<SpeciesId, bool>.Empty), egg) == MoveTutorEligibility.Egg, "shared tutor selector rejects egg");
    }

    [TestMethod]
    public void StorageDaycareAndHealing()
    {
        var guard = Make("PIKACHU");
        var deposit = PokemonStorageRules.Deposit(content, [guard, egg], PokemonStorageState.Empty, 1, 0);
        Assert.IsTrue(deposit.Succeeded && deposit.Storage.Boxes[0].Slots[0]!.Pokemon.EggStatCalculationIvs == egg.Pokemon.Ivs, "PC box recalculates egg from final IVs");
        Assert.IsTrue(deposit.Storage.Boxes[0].Slots[0]!.Pokemon.Stats == Stats(egg.Pokemon, egg.Pokemon.Ivs), "PC boxed egg stats follow inherited IVs");
        Assert.IsTrue(PokemonStorageValidator.Validate(content, deposit.Party, deposit.Storage, nextId)is null, "PC egg and party validation");
        var withdraw = PokemonStorageRules.Withdraw(content, deposit.Party, deposit.Storage, 0, 0);
        Assert.IsTrue(withdraw.Succeeded && withdraw.Party[1].InstanceId == egg.InstanceId && withdraw.Party[1].IsEgg, "PC withdrawal keeps egg and identity");
        Assert.IsTrue(!PokemonStorageRules.Deposit(content, [guard, egg], PokemonStorageState.Empty, 0, 0).Succeeded, "egg does not count as last usable party member");
        Assert.IsTrue(Route5DayCareRules.Deposit(content, owned, Route5DayCareState.Empty, [guard, egg], PokemonStorageState.Empty, SourceMailState.Empty, 1).Error == "daycare_egg", "shared daycare egg deposit refused");
        Assert.IsTrue(Route5DayCareRules.Validate(content, owned, new(new(egg, 0)), [guard], PokemonStorageState.Empty, SourceMailState.Empty) == "daycare_egg", "forged daycare egg resident refused");
        Assert.IsTrue(JsonSerializer.Serialize(LabBattleRewards.Heal(content.Catalog, egg)) == JsonSerializer.Serialize(egg), "shared healing retains egg cycle and stat basis");
    }

    [TestMethod]
    public void HatchCycleOrder()
    {
        var guard = Make("PIKACHU");
        var a = egg with
        {
            Pokemon = egg.Pokemon with
            {
                Friendship = 1
            }
        };
        var b = egg with
        {
            InstanceId = nextId++,
            Pokemon = egg.Pokemon with
            {
                Friendship = 0
            }
        };
        var c = egg with
        {
            InstanceId = nextId++,
            Pokemon = egg.Pokemon with
            {
                Friendship = 2
            }
        };
        var scan = SourceEggRules.AdvanceHatchCycle(content, [guard, a, b, c]);
        Assert.IsTrue(scan.HatchPartyIndex == 2 && scan.Party[1].Pokemon.Friendship == 0 && scan.Party[3].Pokemon.Friendship == 2, "cycle party-order decrements before first zero then stops");
        var one = SourceEggRules.AdvanceHatchCycle(content, [a]);
        Assert.IsTrue(one.HatchPartyIndex is null && one.Party[0].Pokemon.Friendship == 0 && SourceEggRules.AdvanceHatchCycle(content, one.Party).HatchPartyIndex == 0, "reachingzero waits another hatch tick");
    }

    [TestMethod]
    public void HatchingAndShards()
    {
        var hatch = SourceEggRules.Hatch(content, egg with { ModernFatefulEncounter = true, MetGame = 3 }, new(9001, "BLUE", 1, 3), "MAPSEC_PALLET_TOWN", received.Rng);
        Assert.IsTrue(hatch.Trace.Length == 2 && hatch.Rng.Cursor == 11 && hatch.Pokemon.InstanceId == egg.InstanceId, "hatch consumes2 discarded IV draws and keeps instance");
        Assert.IsTrue(!hatch.Pokemon.IsEgg && hatch.Pokemon.Pokemon.EggStatCalculationIvs is null && hatch.Pokemon.Pokemon.Ivs == egg.Pokemon.Ivs && hatch.Pokemon.Pokemon.Personality == egg.Pokemon.Personality, "hatch restores IVs PID and clears egg basis");
        Assert.IsTrue(hatch.Pokemon.Pokemon.Stats == Stats(egg.Pokemon, egg.Pokemon.Ivs) && hatch.Pokemon.Pokemon.Friendship == 120 && Moves(hatch.Pokemon) == Moves(egg), "hatch recalculates final stats friendship120 preserved moves");
        Assert.IsTrue(hatch.Pokemon.Pokemon.OriginalTrainerId == 9001 && hatch.Pokemon.OriginalTrainerName == "BLUE" && hatch.Pokemon.Language == 3 && hatch.Pokemon.OriginalTrainerGender == 1 && hatch.Pokemon.MetLocation == "MAPSEC_PALLET_TOWN", "hatch uses current player and location language");
        Assert.IsTrue(hatch.Pokemon.MetGame == 3 && hatch.Pokemon.ModernFatefulEncounter && hatch.Pokemon.MetLevel == 0 && hatch.Pokemon.Ball == "ITEM_POKE_BALL", "hatch preserves supported source hidden fields and metlevel0");
        Assert.IsTrue(PokemonStateValidator.ValidateParty(content, hatch.Pokemon)is null && PokemonStateValidator.Validate(content.Catalog, hatch.Pokemon.Pokemon)is null, "hatched Pokemon passes strict ordinary validators");
        var shards = SourceEggRules.CreateNormalHatchShards(hatch.Rng);
        Assert.IsTrue(shards.Trace.Length == 19 && shards.Rng.Cursor == 30 && shards.AnimationIndexes.Length == 19 && shards.AnimationIndexes.All(i => i is >= 0 and < 4), "normal hatch deterministic19 shard draws");
        var manual = received.Rng;
        for (int i = 0; i < 21; i++)
            manual = Gen3Rng.Next(manual, "independent").State;
        Assert.IsTrue(manual == shards.Rng && shards.Rng.Seed == 0x3d69ea46 && hatch.Rng.Seed == 0xfc5ecc3d && shards.AnimationIndexes.SequenceEqual(new[] { 1, 2, 1, 0, 3, 2, 0, 2, 1, 2, 0, 1, 0, 0, 3, 3, 3, 3, 1 }), "normal hatch2plus19 exact literal RNG and shard vector");
    }

    [TestMethod]
    public void InvalidRequests()
    {
        Throws(() => SourceEggRules.Receive(content, metadata, parents with { OffspringPersonality = 0 }, nextId, trainer, "MAPSEC_FOUR_ISLAND", 4, new(0)), "missing pending egg rejected");
        Throws(() => SourceEggRules.Receive(content, metadata, parents, nextId, trainer, "MAPSEC_FOUR_ISLAND", 4, new(0, ulong.MaxValue - 8)), "receipt whole RNG budget preflight");
        Throws(() => SourceEggRules.Hatch(content, egg, trainer, "MAPSEC_FOUR_ISLAND", new(0, ulong.MaxValue - 1)), "hatch whole RNG budget preflight");
        Throws(() => SourceEggRules.CreateNormalHatchShards(new(0, ulong.MaxValue - 18)), "shards whole RNG budget preflight");
        var copiedGame = GamePokemonCatalog.Create(owned.Mechanics, owned.Catalog with { }, owned.Opponents.Catalog with { }, owned.TrainerTowerOpponents.Catalog with { });
        var copied = SourceEggRules.Receive(content with { Pokemon = copiedGame }, metadata, parents, nextId, trainer, "MAPSEC_FOUR_ISLAND", 4, new(0));
        Assert.AreEqual(JsonSerializer.Serialize(received.Egg.Pokemon), JsonSerializer.Serialize(copied.Egg.Pokemon), "equivalent immutable catalog preserves egg generation");
    }

    [TestMethod]
    public void InheritanceAndSpeciesBranches()
    {
        var bulbMother = Make("BULBASAUR", 0, 3, "RAZOR_LEAF", "PETAL_DANCE", "TOXIC", "SLUDGE_BOMB");
        var bulbFather = Make("BULBASAUR", 255, 27, "RAZOR_LEAF", "PETAL_DANCE", "TOXIC", "SLUDGE_BOMB");
        Assert.IsTrue(Moves(Egg(Pair(bulbMother, bulbFather)).Egg) == "PETAL_DANCE,TOXIC,SLUDGE_BOMB,RAZOR_LEAF", "source father egg then machine then shared level move order and FIFO");
        var ditto = Make("DITTO", 255);
        var malePika = Make("PIKACHU", 255, 27, "WISH", "THUNDERBOLT");
        var maleDitto = Egg(Pair(malePika, ditto));
        Assert.IsTrue(maleDitto.Egg.Pokemon.Species.Value == "pret:SPECIES_PICHU" && Moves(maleDitto.Egg).Contains("WISH") && Moves(maleDitto.Egg).Contains("THUNDERBOLT"), "male plus Ditto supplies father egg and TM moves");
        var femalePika = Make("PIKACHU", 0, 3, "WISH", "THUNDERBOLT");
        Assert.IsTrue(!Moves(Egg(Pair(femalePika, ditto)).Egg).Contains("WISH") && !Moves(Egg(Pair(femalePika, ditto)).Egg).Contains("THUNDERBOLT"), "female plus Ditto retains move mother role");
        var psyduck = Make("PSYDUCK", 255, 27, "SURF");
        Assert.IsTrue(Moves(Egg(Pair(psyduck, ditto)).Egg).Contains("SURF"), "all58 includes inherited HM Surf without bag acquisition");
        var wob = Make("WOBBUFFET", 0);
        Assert.IsTrue(Egg(Pair(wob, ditto)).Egg.Pokemon.Species.Value == "pret:SPECIES_WOBBUFFET" && Egg(Pair(wob with { HeldItem = "ITEM_LAX_INCENSE" }, ditto)).Egg.Pokemon.Species.Value == "pret:SPECIES_WYNAUT", "Wynaut source incense requirement and fallback");
        var nidoran = Make("NIDORAN_F");
        Assert.IsTrue(Egg(Pair(nidoran, ditto, 0x8000)).Egg.Pokemon.Species.Value == "pret:SPECIES_NIDORAN_M" && Egg(Pair(nidoran, ditto, 1)).Egg.Pokemon.Species.Value == "pret:SPECIES_NIDORAN_F", "pending low PID bit selects one-way Nidoran branch");
        Assert.IsTrue(Egg(Pair(Make("NIDORAN_M", 255), ditto, 1)).Egg.Pokemon.Species.Value == "pret:SPECIES_NIDORAN_M", "Nidoran male base remains male when lowbitclear");
    }

    [TestMethod]
    public void CompiledIvVectors()
    {
        using var oracle = JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Battle.Tests/Breeding/Fixtures/iv-vectors.json", "python tests/Game.Battle.Tests/Breeding/probe_source_iv.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Breeding/Fixtures/iv-vectors.json")));
        int vectors = 0, stale = 0;
        foreach (var row in oracle.RootElement.GetProperty("rows").EnumerateArray())
        {
            var result = Egg(parents, row.GetProperty("seed").GetUInt32());
            if (!result.Egg.Pokemon.Ivs.Values.SequenceEqual(row.GetProperty("finalIvs").EnumerateArray().Select(x => x.GetInt32())) || result.Rng.Seed != row.GetProperty("finalRngSeed").GetUInt32())
                throw new InvalidDataException("compiled C IV vector mismatch");
            if (result.Egg.Pokemon.Stats != Stats(result.Egg.Pokemon, result.Egg.Pokemon.Ivs))
                stale++;
            vectors++;
        }

        Assert.IsTrue(vectors == 128 && stale > 0, "128 original compiled C inheritance/RNG vectors including stale party stats");
    }

    [TestMethod]
    public void CarriedEggBattleRewards()
    {
        var fighter = Make("RATICATE", 255, 27, "QUICK_ATTACK");
        fighter = fighter with
        {
            Pokemon = fighter.Pokemon with
            {
                CurrentHp = fighter.Pokemon.CurrentHp - 5
            }
        };
        var enemy = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_PIDGEY"), 3, 42, IvPolicy.Fixed(3), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        ImmutableArray<WildBattleCombatant> entryParty = [new(egg.Pokemon)
        {
            IsEgg = true
        }, new(fighter.Pokemon)];
        var campaign = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var mapId = new MapId("map.route-1");
        var table = campaign.LandEncounters![mapId];
        var slot = table.Slots.First(s => s.Species == enemy.Species && s.MinimumLevel <= enemy.Level && s.MaximumLevel >= enemy.Level);
        var definition = new OrdinaryLandEncounterCatalog(new Dictionary<MapId, EarlyLandEncounterTable> { [mapId] = table },
            new Dictionary<MapId, IFieldMap> { [mapId] = campaign.Maps[mapId] }, owned.Catalog)[mapId];
        var pendingEncounter = new EarlyLandEncounterState(new(0), 2, Pending: new(new(mapId, slot.Index, enemy.Species,
            (byte)enemy.Level), 2), RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
        var encounter = new SourceWildEncounterContext { OrdinaryLand = definition.BindBattle(pendingEncounter) };
        var battle = SourceWildBattlePolicy.StartOwned(owned, entryParty, 1, new(enemy), new(99), encounter);
        Assert.IsTrue(battle.Succeeded, "carried egg ordinary wild entry: " + battle.Fault);
        var frame = battle.State!;
        var mechanics = owned.Mechanics;
        Assert.IsTrue(frame.Get(new(0)).Member!.Value.Index == 1 && frame.Party(BattleSide.Player).Members[0].IsEgg && SourceFourActionTurns.ValidateCapability(mechanics.Catalog, frame)is null, "leading egg preserves party indices and valid reserve");
        Assert.IsTrue(!SourceWildBattlePolicy.StartOwned(owned, entryParty, 0, new(enemy), new(99), encounter).Succeeded && !SourceWildBattlePolicy.StartOwned(owned, [new(egg.Pokemon), new(fighter.Pokemon)], 1, new(enemy), new(99), encounter).Succeeded, "egg active and omitted flag both rejected");
        Assert.IsTrue(!SourceFourActionTurns.BeginActions(mechanics.Catalog, frame, frame.Revision, [new(frame.Get(new(0)).Occupant!.Value, SourceActionKind.Switch, Incoming: new(BattleSide.Player, 0)), new(frame.Get(new(1)).Occupant!.Value, SourceActionKind.Move, 0, new(0))]).Succeeded, "egg cannot be selected for battle switch");
        var items = SourceBattleItemCatalogLoader.LoadAuthenticated(TestAssets.Root);
        var potion = new ItemId("pret:ITEM_POTION");
        var bag = new InventoryState([new(potion, 2)], [], SourceBattleItemCatalog.Limits);
        var medicine = SourceFourMedicineRules.Select(items, mechanics, frame, frame.Revision, frame.Get(new(0)).Occupant!.Value, bag, [egg, fighter], new(potion, fighter.InstanceId));
        Assert.IsTrue(medicine.Succeeded && JsonSerializer.Serialize(medicine.Party[0]) == JsonSerializer.Serialize(egg), "battle medicine on ordinary member accepts Japanese egg reserve: " + medicine.Fault);
        Assert.IsTrue(SourceFourMedicineRules.Select(items, mechanics, frame, frame.Revision, frame.Get(new(0)).Occupant!.Value, bag, [egg, fighter], new(potion, egg.InstanceId)).Fault == "source_item_egg", "battle medicine target egg still refused");
        var facts = ImmutableArray.Create(new SourceRewardMetadata(new(BattleSide.Player, 0), egg.OriginalTrainerName, egg.MetLocation, egg.Ball, SourceRewardPokerusHistory.NeverInfected), new SourceRewardMetadata(new(BattleSide.Player, 1), fighter.OriginalTrainerName, fighter.MetLocation, fighter.Ball, SourceRewardPokerusHistory.NeverInfected));
        var rewardContext = new SourceRewardContext(SourceRewardBattleKind.OrdinaryWild, 42, "LEAF", "MAPSEC_ROUTE_1");
        frame = frame with
        {
            Rewards = new()
            {
                Context = rewardContext,
                Metadata = facts
            }
        };
        var turn = SourceFourActionTurns.BeginActions(mechanics.Catalog, frame, frame.Revision, [new(frame.Get(new(0)).Occupant!.Value, SourceActionKind.Move, 0, new(1)), new(frame.Get(new(1)).Occupant!.Value, SourceActionKind.Move, 0, new(0))]);
        Assert.IsTrue(turn.Succeeded, "ordinary battle begins with carried egg: " + turn.Fault);
        int steps = 0;
        while (turn.State.FourTurn?.Phase != SourceFourActionPhase.RewardsPending && steps++ < 16)
        {
            var f = turn.State;
            turn = SourceFourActionTurns.Advance(mechanics.Catalog, f, new(f.Revision, f.EventSequence, f.NextAction, f.FourTurn!.Phase));
            if (!turn.Succeeded)
                throw new InvalidDataException("carried egg battle action: " + turn.Fault);
        }

        Assert.IsTrue(turn.State.FourTurn?.Phase == SourceFourActionPhase.RewardsPending, "actual KO reaches shared EXP boundary");
        var defeatedCell = turn.State.Get(new(1));
        var defeat = new SourceRewardDefeat(defeatedCell.Occupant!.Value, defeatedCell.Body!, [new(BattleSide.Player, 0), new(BattleSide.Player, 1)]);
        var calculated = SourceVictoryRewards.Calculate(mechanics, turn.State.Party(BattleSide.Player), facts, defeat, rewardContext, owned);
        var only = SourceVictoryRewards.Calculate(mechanics, new(BattleSide.Player, [turn.State.Party(BattleSide.Player).Members[1]]), [facts[1] with { Key = new(BattleSide.Player, 0) }], defeat with { Participants = [new(BattleSide.Player, 0)] }, rewardContext);
        Assert.IsTrue(calculated.Members.Length == 1 && calculated.Members[0].Key.Index == 1 && JsonSerializer.Serialize(calculated.Members[0].Reward) == JsonSerializer.Serialize(only.Members[0].Reward), "eggs excluded from EXP recipients and participant denominator");
        var pending = turn.State;
        var settled = SourceFourRewards.Settle(mechanics.Catalog, pending, new(pending.Revision, pending.EventSequence, pending.NextAction, pending.FourTurn!.Phase));
        Assert.IsTrue(settled.Succeeded && settled.State.Outcome == WildBattleOutcome.Won && settled.Events.OfType<SourceRewardAppliedEvent>().Count() == 1 && settled.Events.OfType<SourceRewardAppliedEvent>().Single().Recipient.Index == 1, "actual settlement awards only ordinary active member: " + settled.Fault);
        Assert.IsTrue(JsonSerializer.Serialize(settled.State.Party(BattleSide.Player).Members[0].Pokemon) == JsonSerializer.Serialize(egg.Pokemon) && SourceFourActionTurns.ValidateCapability(mechanics.Catalog, settled.State)is null, "egg unchanged through actual move KO and EXP settlement");
    }
}
