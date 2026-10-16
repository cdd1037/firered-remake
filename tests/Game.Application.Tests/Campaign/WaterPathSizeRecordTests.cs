using System.Collections.Immutable;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>
/// Controlled initial party and location, followed by public interaction, selection, bag, movement,
/// save and Continue commands. Source vectors use pret 037335f4 pokemon_size_record.c and the
/// House1 script; Heracross failed gifts deliberately follow the user-approved retryable fix.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class WaterPathSizeRecordTests : WildBattleFixture
{
    private const string Water = "map.six-island-water-path";
    private const string House = "map.six-island-water-path-house1";
    private const string Prefix = "SixIsland_WaterPath_House1_Text_";
    private const string Record = "VAR_HERACROSS_SIZE_RECORD";
    private const string RewardFlag = "FLAG_GOT_NEST_BALL_FROM_WATER_PATH_HOUSE_1";
    private static readonly ItemId NestBall = new("pret:ITEM_NEST_BALL");
    private static readonly ItemId NetBall = new("pret:ITEM_NET_BALL");

    private readonly List<CampaignDayCareEggReceipt> preparedEggs = [];
    private RngState? preparedRng;
    private long? preparedNextId;

    private PartyPokemon Parent(long id, string species, uint personality)
    {
        var pokemon = SourcePokemonFactory.Create(mechanics,
            new(new("pret:SPECIES_" + species), 25, checkpoint.State.TrainerId,
                IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(12)).Pokemon;
        return new(id, pokemon, species, checkpoint.State.PlayerName, checkpoint.State.PlayerGender,
            2, "MAPSEC_FOUR_ISLAND", 25, 4, "ITEM_POKE_BALL", "ITEM_NONE");
    }

    private static CampaignEggParent ParentReceipt(PartyPokemon mon) => new(mon.InstanceId, mon.Pokemon.SourceSpeciesId,
        mon.Pokemon.Personality, mon.Pokemon.OriginalTrainerId, mon.Pokemon.Ivs,
        mon.Pokemon.Moves.Select(m => m.Move).ToImmutableArray(), mon.HeldItem);

    private PartyPokemon SizedMon(ushort hash, string species = "HERACROSS")
    {
        // Build replayable breeding/hatch receipts, rather than inventing a captured individual.
        // The source IV draws do not depend on the stored PID low half, so invert its two XOR
        // bytes to obtain this independently specified size hash without changing an owned mon.
        long parentId = preparedNextId ?? checkpoint.State.NextPokemonId;
        var female = Parent(parentId, species, 0);
        var male = Parent(parentId + 1, species, 255);
        var trainer = new SourceEggTrainer(checkpoint.State.TrainerId, checkpoint.State.PlayerName, checkpoint.State.PlayerGender);
        var rng = preparedRng ?? checkpoint.State.World.Rng;
        var pokemon = content.PokemonContent!;
        SourceEggCreationResult Receive(ushort pending) => SourceEggRules.Receive(pokemon, pokemon.Catalog.BreedingData!,
            new(new(female, 0), new(male, 0), pending, 0), parentId + 2, trainer, "MAPSEC_FOUR_ISLAND", 4, rng);
        var preview = Receive(1);
        var iv = preview.Egg.Pokemon.Ivs;
        int low = (hash >> 8) ^ (((iv.Attack & 15) ^ (iv.Defense & 15)) * (iv.Hp & 15));
        int high = (hash & 255) ^ (((iv.SpecialAttack & 15) ^ (iv.SpecialDefense & 15)) * (iv.Speed & 15));
        ushort pending = (ushort)((high << 8) | low);
        Assert.AreNotEqual((ushort)0, pending, "source requires a nonzero pending-egg PID");
        var made = Receive(pending);
        var hatched = SourceEggRules.Hatch(pokemon, made.Egg, trainer, "MAPSEC_FOUR_ISLAND", made.Rng);
        var shards = SourceEggRules.CreateNormalHatchShards(hatched.Rng);
        preparedEggs.Add(new(parentId + 2, ParentReceipt(female), ParentReceipt(male), pending,
            made.Egg.Pokemon.SourceSpeciesId, made.Egg.Pokemon.Personality, made.Egg.Pokemon.Ivs,
            "MAPSEC_FOUR_ISLAND", rng, made.Rng) { Hatch = new("MAPSEC_FOUR_ISLAND", made.Rng, shards.Rng) });
        preparedRng = shards.Rng;
        preparedNextId = parentId + 3;
        return hatched.Pokemon;
    }

    private CampaignState Fixture(params PartyPokemon[] additions)
    {
        var state = FieldFixture();
        // Keep all historic individuals and their receipts; make room by storing every non-lead.
        while (state.Party.Length > 1)
        {
            var deposited = PokemonStorageRules.Deposit(content.PokemonContent, state.Party, state.Storage!, 1, 13);
            Assert.IsTrue(deposited.Succeeded, deposited.Diagnostic?.ToString());
            state = state with { Party = deposited.Party, Storage = deposited.Storage };
        }
        state = state with {
            Party = state.Party.AddRange(additions),
            DayCareEggs = (state.DayCareEggs ?? []).AddRange(preparedEggs),
            World = state.World with { Rng = preparedRng ?? state.World.Rng },
            NextPokemonId = additions.Length == 0 ? state.NextPokemonId : Math.Max(state.NextPokemonId, additions.Max(p => p.InstanceId) + 1),
            SeenSpecies = state.SeenSpecies.Union(additions.Where(p => !p.IsEgg).Select(p => p.Pokemon.SourceSpeciesId)),
            CaughtSpecies = state.CaughtSpecies.Union(additions.Where(p => !p.IsEgg).Select(p => p.Pokemon.SourceSpeciesId)),
            Script = state.Script with { Variables = state.Script.Variables.Remove(Record), Flags = state.Script.Flags.Remove(RewardFlag) },
            Inventory = state.Inventory! with { Balls = [] }
        };
        return Place(state, House, 3, 5);
    }

    private CampaignState Place(CampaignState state, string mapName, int x, int y, Direction facing = Direction.North)
    {
        var map = content.Maps[new(mapName)];
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, facing) },
            Actors = content.MapDefinition(map.Id).Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag,
                    !state.Script.Flags.GetValueOrDefault(a.HideFlag), false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapLayout = null,
            MapEdits = null, EntryPrograms = [], Dialogue = null, Motions = [],
            IsSurfing = false, IsCycling = false, FlashLevel = 0
        };
        return CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
    }

    private CampaignSession Start(CampaignState state)
    {
        Assert.IsNull(validator.ValidatePlayerSave(state), "controlled starting state must satisfy strict current-save rules");
        return CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
    }

    private HashSet<string> DrainField(CampaignSession session, bool stopAtParty = false)
    {
        var texts = new HashSet<string>(StringComparer.Ordinal);
        for (int n = 0; n < 200; n++)
        {
            if (session.State.Dialogue is { } dialogue) texts.Add(dialogue.TextSymbol);
            if (session.State.SizeRecord?.Stage == CampaignSizeRecordStage.Party)
            {
                Assert.IsTrue(stopAtParty, "party choice must be explicit");
                return texts;
            }
            if (session.State.Script.Completed && session.State.Script.Wait is null &&
                session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty) return texts;
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        }
        Assert.Fail("field presentation did not finish");
        return texts;
    }

    private void ChooseParty(CampaignSession session)
    {
        Send(session, new("interact"));
        DrainField(session, stopAtParty: true);
        Assert.AreEqual(CampaignSizeRecordStage.Party, session.State.SizeRecord?.Stage);
        Assert.AreEqual(StoryWaitKind.PartySelection, session.State.Script.Wait?.Kind);
    }

    private HashSet<string> Measure(CampaignSession session, int index = 1, string command = "size_record_choose")
    {
        ChooseParty(session);
        Send(session, new(command, PartyIndex: index) { TargetInstanceId = session.State.Party[index].InstanceId });
        return DrainField(session);
    }

    private static int Count(CampaignSession session, ItemId item) => OpeningInventoryRules.Quantity(session.State.Inventory!.Balls ?? [], item);
    private static int RecordValue(CampaignSession session) => session.State.Script.Variables.GetValueOrDefault(Record);

    private CampaignSession Continue(CampaignSession session)
    {
        StrictRoundtrip(session);
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        return CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
    }

    private void RejectUnchanged(CampaignSession session, CampaignCommand command, string error)
    {
        string before = CanonicalJson.Hash(session.State);
        long revision = session.Revision;
        var result = session.Execute("record-rejection:" + commands++, revision, command);
        Assert.AreEqual(CommandStatus.Rejected, result.Status);
        Assert.AreEqual(error, result.Error);
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State));
    }

    private CampaignState AddHeracrossEgg(CampaignState state)
    {
        long id = state.NextPokemonId;
        var female = Parent(id, "HERACROSS", 0);
        var male = Parent(id + 1, "HERACROSS", 255);
        var trainer = new SourceEggTrainer(state.TrainerId, state.PlayerName, state.PlayerGender);
        var made = SourceEggRules.Receive(content.PokemonContent!, content.PokemonContent!.Catalog.BreedingData!,
            new(new(female, 0), new(male, 0), 123, 0), id + 2, trainer, "MAPSEC_FOUR_ISLAND", 4, state.World.Rng);
        Assert.AreEqual(214, made.Egg.Pokemon.SourceSpeciesId);
        var receipt = new CampaignDayCareEggReceipt(id + 2, ParentReceipt(female), ParentReceipt(male), 123,
            214, made.Egg.Pokemon.Personality, made.Egg.Pokemon.Ivs, "MAPSEC_FOUR_ISLAND", state.World.Rng, made.Rng);
        return state with { Party = state.Party.Add(made.Egg), NextPokemonId = id + 3,
            DayCareEggs = (state.DayCareEggs ?? []).Add(receipt), World = state.World with { Rng = made.Rng } };
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NoHeracrossOrOnlyHeracrossEggDoesNotOpenChooser(bool onlyEgg)
    {
        var state = Fixture();
        if (onlyEgg) state = AddHeracrossEgg(state);
        var session = Start(state);
        string party = CanonicalJson.Hash(session.State.Party);
        Send(session, new("interact"));
        var texts = DrainField(session);
        Assert.IsTrue(texts.Contains(Prefix + "LoveItNeedItHeracross"));
        Assert.IsNull(session.State.SizeRecord);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.IsFalse(session.State.Script.Variables.ContainsKey(Record));
        Assert.AreEqual(0, Count(session, NestBall));
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void CancelAndInvalidSelectionsAreAtomicAndLeaveTheRecordAlone()
    {
        var session = Start(Fixture(SizedMon(32710)));
        RejectUnchanged(session, new("size_record_cancel"), "size_record_not_pending");
        string party = CanonicalJson.Hash(session.State.Party);
        ChooseParty(session);
        Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.State));
        RejectUnchanged(session, new("size_record_choose", PartyIndex: 1), "size_record_target_mismatch");
        RejectUnchanged(session, new("size_record_choose", PartyIndex: 1) { TargetInstanceId = session.State.Party[0].InstanceId }, "size_record_target_mismatch");
        RejectUnchanged(session, new("size_record_choose", PartyIndex: 6) { TargetInstanceId = 1 }, "size_record_party_index");
        RejectUnchanged(session, new("move", Direction: Direction.South), "size_record_selection_required");
        Send(session, new("size_record_cancel")); DrainField(session);
        Assert.IsNull(session.State.SizeRecord);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.IsFalse(session.State.Script.Variables.ContainsKey(Record));
        Assert.AreEqual(0, Count(session, NestBall));
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SelectedWrongSpeciesOrEggDoesNotChangeRecordOrAwardBall(bool chooseEgg)
    {
        var state = Fixture(SizedMon(32710));
        if (chooseEgg) state = AddHeracrossEgg(state);
        var session = Start(state);
        string party = CanonicalJson.Hash(session.State.Party);
        var texts = Measure(session, chooseEgg ? 2 : 0);
        Assert.IsTrue(texts.Contains(Prefix + "ThisWontDo"));
        Assert.AreEqual(0, RecordValue(session));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.AreEqual(0, Count(session, NestBall));
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void FaintedHeracrossStillQualifiesAndKeepsItsIndividualData()
    {
        var mon = SizedMon(32710);
        mon = mon with { Pokemon = mon.Pokemon with { CurrentHp = 0 } };
        var session = Start(Fixture(mon));
        string identity = CanonicalJson.Hash(mon);
        var rng = session.State.World.Rng;
        var texts = Measure(session);
        Assert.IsTrue(texts.Contains(Prefix + "ItsXInchesDeserveReward"));
        Assert.AreEqual(32710, RecordValue(session));
        Assert.AreEqual(1, Count(session, NestBall));
        Assert.AreEqual(identity, CanonicalJson.Hash(session.State.Party[1]));
        Assert.AreEqual(rng, session.State.World.Rng);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(7710, 710)]
    public void FirstValidSubmissionRewardsEvenWhenTiedOrSmaller(int oldRecord, int candidate)
    {
        var state = Fixture(SizedMon((ushort)candidate));
        state = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(Record, oldRecord) } };
        var session = Start(state);
        var texts = Measure(session);
        Assert.IsTrue(texts.Contains(Prefix + "ItsXInchesDeserveReward"));
        Assert.AreEqual(oldRecord, RecordValue(session), "A first reward does not make a smaller or equal hash the size record.");
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.AreEqual(1, Count(session, NestBall));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(710, "35.4", "ItsXInchesYInchesWasBiggest", 0, 7710)]
    [DataRow(7710, "47.2", "ItsXInchesSameAsBefore", 0, 7710)]
    [DataRow(7711, "47.2", "ItsXInchesSameAsBefore", 0, 7710)]
    [DataRow(32710, "59.0", "ItsXInchesDeserveReward", 1, 32710)]
    public void EstablishedRecordComparesMeasuredSizeBeforeFormatting(int candidate, string inches, string text, int balls, int finalRecord)
    {
        var state = Fixture(SizedMon((ushort)candidate));
        state = state with { Script = state.Script with {
            Variables = state.Script.Variables.SetItem(Record, 7710), Flags = state.Script.Flags.SetItem(RewardFlag, true) } };
        var session = Start(state);
        ChooseParty(session);
        Assert.AreEqual("HERACROSS", session.State.TextBuffers["STR_VAR_1"]);
        Assert.AreEqual("47.2", session.State.TextBuffers["STR_VAR_3"]);
        Send(session, new("size_record_choose", PartyIndex: 1) { TargetInstanceId = session.State.Party[1].InstanceId });
        Assert.AreEqual(inches, session.State.TextBuffers["STR_VAR_2"]);
        var texts = DrainField(session);
        Assert.IsTrue(texts.Contains(Prefix + text));
        Assert.AreEqual(finalRecord, RecordValue(session));
        Assert.AreEqual(balls, Count(session, NestBall));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void LargerMeasurementWithIdenticalDisplayedInchesStillAwardsNewRecord()
    {
        // Source sizes 435 and 436 tenths-centimeters both truncate to 17.1 inches.
        var state = Fixture(SizedMon(1));
        state = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(Record, 0),
            Flags = state.Script.Flags.SetItem(RewardFlag, true) } };
        var session = Start(state);
        ChooseParty(session);
        Send(session, new("size_record_choose", PartyIndex: 1) { TargetInstanceId = session.State.Party[1].InstanceId });
        Assert.AreEqual("17.1", session.State.TextBuffers["STR_VAR_2"]);
        Assert.AreEqual("17.1", session.State.TextBuffers["STR_VAR_3"]);
        Assert.IsTrue(DrainField(session).Contains(Prefix + "ItsXInchesDeserveReward"));
        Assert.AreEqual(1, RecordValue(session));
        Assert.AreEqual(1, Count(session, NestBall));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void FirstRecordAndFlagCommitInTheSameTransactionAsTheAwardedBall()
    {
        var session = Start(Fixture(SizedMon(32710)));
        ChooseParty(session);
        Send(session, new("size_record_choose", PartyIndex: 1) { TargetInstanceId = session.State.Party[1].InstanceId });
        Assert.AreEqual(Prefix + "ItsXInchesDeserveReward", session.State.Dialogue?.TextSymbol);
        for (int n = 0; n < 50 && Count(session, NestBall) == 0; n++)
        {
            Assert.AreEqual(0, RecordValue(session));
            Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
            Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.State));
            Send(session, new("ack"));
        }
        Assert.AreEqual(1, Count(session, NestBall));
        Assert.AreEqual(32710, RecordValue(session));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        DrainField(session);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void MultipleHeracrossUseTheSelectedInstanceAndRepeatDoesNotDuplicateGift()
    {
        var session = Start(Fixture(SizedMon(710), SizedMon(32710)));
        string party = CanonicalJson.Hash(session.State.Party);
        Measure(session, 2);
        Assert.AreEqual(32710, RecordValue(session));
        Assert.AreEqual(1, Count(session, NestBall));
        session = Continue(session);
        Assert.IsTrue(Measure(session, 1).Contains(Prefix + "ItsXInchesYInchesWasBiggest"));
        Assert.IsTrue(Measure(session, 2).Contains(Prefix + "ItsXInchesSameAsBefore"));
        Assert.AreEqual(32710, RecordValue(session));
        Assert.AreEqual(1, Count(session, NestBall));
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        StrictRoundtrip(session);
    }

    private void TossOneBall(CampaignSession session, ItemId ball)
    {
        Send(session, new("bag_open"));
        Send(session, new("bag_pocket", Choice: 2));
        Send(session, new("bag_toss_open", Text: ball.Value));
        Send(session, new("bag_toss_select", Text: session.State.ItemMenu!.Toss!.WaitId));
        Send(session, new("bag_toss_confirm", Choice: 0, Text: session.State.ItemMenu!.Toss!.WaitId));
        Send(session, new("bag_toss_ack", Text: session.State.ItemMenu!.Toss!.WaitId));
        Send(session, new("menu_back"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FullBagRetainsPreviousRecordAndSameHeracrossCanRetryAfterContinue(bool alreadyRecorded)
    {
        var state = Fixture(SizedMon(32710));
        if (alreadyRecorded) state = state with { Script = state.Script with {
            Variables = state.Script.Variables.SetItem(Record, 7710), Flags = state.Script.Flags.SetItem(RewardFlag, true) } };
        state = state with { Inventory = state.Inventory! with { Balls = [new(NestBall, 999)] } };
        var session = Start(state);
        Assert.IsTrue(Measure(session).Contains(Prefix + "YourBagIsFull"));
        Assert.AreEqual(alreadyRecorded ? 7710 : 0, RecordValue(session));
        Assert.AreEqual(alreadyRecorded, session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.AreEqual(999, Count(session, NestBall));
        if (!alreadyRecorded) Assert.IsFalse(session.State.Script.Variables.ContainsKey(Record));
        session = Continue(session);
        TossOneBall(session, NestBall);
        Assert.AreEqual(998, Count(session, NestBall));
        Assert.IsTrue(Measure(session).Contains(Prefix + "ItsXInchesDeserveReward"));
        Assert.AreEqual(32710, RecordValue(session));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.AreEqual(999, Count(session, NestBall));
        session = Continue(session);
        Assert.IsTrue(Measure(session).Contains(Prefix + "ItsXInchesSameAsBefore"));
        Assert.AreEqual(999, Count(session, NestBall));
        StrictRoundtrip(session);
    }

    private void Step(CampaignSession session, Direction direction)
    {
        Send(session, new("move", Direction: direction));
        DrainField(session);
        Assert.IsNull(session.State.WildBattle);
        Assert.IsNull(session.State.ForestTrainerBattle);
    }

    private void Face(CampaignSession session, Direction direction)
    {
        if (session.State.World.Player.Facing != direction) Send(session, new("turn", Direction: direction));
    }

    private void WalkInside(CampaignSession session, int targetX, int targetY)
    {
        var map = content.Maps[session.State.World.Player.MapId];
        var start = (session.State.World.Player.X, session.State.World.Player.Y);
        var target = (targetX, targetY);
        var queue = new Queue<(int X, int Y)>(); queue.Enqueue(start);
        var paths = new Dictionary<(int X, int Y), Direction[]> { [start] = [] };
        (int X, int Y, Direction Direction)[] directions = [(0, -1, Direction.North), (1, 0, Direction.East), (0, 1, Direction.South), (-1, 0, Direction.West)];
        while (queue.TryDequeue(out var at) && !paths.ContainsKey(target))
        {
            foreach (var step in directions)
            {
                var next = (X: at.X + step.X, Y: at.Y + step.Y);
                if (paths.ContainsKey(next) || map.GetTile(next.X, next.Y) is not { Blocked: false } ||
                    session.State.Actors.Values.Any(a => a.Active && !a.Invisible && a.X == next.X && a.Y == next.Y) ||
                    next == (3, 7)) continue;
                paths.Add(next, [.. paths[at], step.Direction]); queue.Enqueue(next);
            }
        }
        Assert.IsTrue(paths.ContainsKey(target), "walkable interior route");
        foreach (var direction in paths[target]) Step(session, direction);
        Assert.AreEqual(target, (session.State.World.Player.X, session.State.World.Player.Y));
    }

    [TestMethod]
    public void ActualDoorEntryRecordChartContinueAndExitReturnToWaterPath()
    {
        var state = Fixture(SizedMon(32710));
        var session = Start(Place(state, Water, 5, 14));
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Water; n++) Step(session, Direction.North);
        Assert.AreEqual(House, session.State.World.Player.MapId.Value);
        Assert.AreEqual((3, 7), (session.State.World.Player.X, session.State.World.Player.Y));
        WalkInside(session, 9, 2); Face(session, Direction.North);
        Send(session, new("interact"));
        Assert.IsTrue(DrainField(session).Contains(Prefix + "BlankChartOfSomeSort"));
        WalkInside(session, 3, 5); Face(session, Direction.North);
        Measure(session); session = Continue(session);
        WalkInside(session, 9, 2); Face(session, Direction.North);
        Send(session, new("interact"));
        Assert.AreEqual("59.0", session.State.TextBuffers["STR_VAR_3"]);
        Assert.IsTrue(DrainField(session).Contains(Prefix + "BiggestHeracrossIsXInches"));
        WalkInside(session, 3, 6);
        for (int n = 0; n < 3 && session.State.World.Player.MapId.Value == House; n++) Step(session, Direction.South);
        Assert.AreEqual(Water, session.State.World.Player.MapId.Value);
        Assert.AreEqual((5, 14), (session.State.World.Player.X, session.State.World.Player.Y));
        session = Continue(session);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Water; n++) Step(session, Direction.North);
        WalkInside(session, 3, 5); Face(session, Direction.North);
        Assert.IsTrue(Measure(session).Contains(Prefix + "ItsXInchesSameAsBefore"));
        Assert.AreEqual(1, Count(session, NestBall));
        StrictRoundtrip(session);
    }

    private CampaignState MagikarpFixture()
    {
        var state = Fixture(SizedMon(32710, "MAGIKARP"));
        var rod = new ItemId("pret:ITEM_SUPER_ROD");
        state = state with { Script = state.Script with { Variables = state.Script.Variables.Remove("VAR_MAGIKARP_SIZE_RECORD"),
            Flags = state.Script.Flags.SetItem("FLAG_GOT_SUPER_ROD", true).Remove("FLAG_GOT_RECORD_SETTING_MAGIKARP") },
            Inventory = state.Inventory! with { KeyItems = (state.Inventory!.KeyItems ?? []).RemoveAll(s => s.Item == rod).Add(new(rod, 1)) } };
        return Place(state, "map.route-12-fishing-house", 4, 5);
    }

    [TestMethod]
    [DataRow("size_record_choose")]
    [DataRow("magikarp_record_choose")]
    public void SharedServiceStillAwardsOriginalMagikarpNetBallWithGenericAndLegacyCommands(string command)
    {
        var session = Start(MagikarpFixture());
        Measure(session, command: command);
        Assert.AreEqual(32710, session.State.Script.Variables["VAR_MAGIKARP_SIZE_RECORD"]);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_GOT_RECORD_SETTING_MAGIKARP"));
        Assert.AreEqual(1, Count(session, NetBall));
        Assert.AreEqual(0, Count(session, NestBall));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        session = Continue(session);
        ChooseParty(session); Send(session, new("magikarp_record_cancel")); DrainField(session);
        Assert.AreEqual(1, Count(session, NetBall));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void MagikarpFullBagStillUsesOriginalLostGiftSemantics()
    {
        var state = MagikarpFixture();
        state = state with { Inventory = state.Inventory! with { Balls = [new(NetBall, 999)] } };
        var session = Start(state);
        Assert.IsTrue(Measure(session).Contains("Route12_FishingHouse_Text_NoRoomForGift"));
        Assert.AreEqual(32710, session.State.Script.Variables["VAR_MAGIKARP_SIZE_RECORD"]);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_GOT_RECORD_SETTING_MAGIKARP"));
        session = Continue(session); TossOneBall(session, NetBall);
        Assert.IsTrue(Measure(session).Contains("Route12_FishingHouse_Text_HuhXInchesSameSizeAsLast"));
        Assert.AreEqual(998, Count(session, NetBall));
        StrictRoundtrip(session);
    }
}
