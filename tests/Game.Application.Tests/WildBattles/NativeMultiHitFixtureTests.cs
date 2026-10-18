using System.Collections.Immutable;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.FirstBattle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace FireRed.Tests.Application.WildBattles;

/// <summary>
/// Portable controlled fixture: fresh lifetime hatch count zero, source-legal owned
/// Pokemon and explicitly synthetic opening/capture facts. The encounter and attack
/// under test use only public campaign commands and unchanged ordinary encounter data.
/// No external checkpoint, reflection, injected battle frame or optional default skip.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class NativeMultiHitFixtureTests
{
    private const string OutputVariable = "FIRERED_MULTIHIT_FIXTURE_DIR";
    private const string FileName = "TEST-CONTROLLED-FRESH-route1-clefairy13-double-slap.json";
    private static readonly MoveId Slap = new("pret:MOVE_DOUBLE_SLAP");
    private sealed record VerifiedRoute(CampaignState Initial, byte[] Bytes, uint Seed,
        int OpeningAcknowledgements, CampaignWildBattle Battle, SourceMultiHitEvent Summary,
        ImmutableArray<SourceHpChangedEvent> Hits);

    [TestMethod]
    public void FreshControlledClefairyUsesOrdinaryPublicEncounterAndCommitsMultipleHits()
    {
        var content = CurrentCampaignFixture.Catalog; // Catalog only: no late-game checkpoint dependency.
        var owner = CampaignSession.CreateCurrent(content, new(12345, "MULTI", "RIVAL", RngSeed: 123));
        var basis = CreateControlledField(content, owner);
        Assert.AreEqual(0, basis.GameStats!.GetValueOrDefault("GAME_STAT_HATCHED_EGGS"));
        Assert.IsNull(owner.ValidatePlayerSave(basis));
        VerifiedRoute? verified = null;
        // Deterministic fixture preparation, not a probabilistic assertion: select the
        // first bounded seed whose real public turn produces at least two hit receipts.
        for (uint seed = 0; seed < 64 && verified is null; seed++)
        {
            var initial = basis with { World = basis.World with { Rng = basis.World.Rng with { Seed = seed } } };
            Assert.IsNull(owner.ValidatePlayerSave(initial));
            var bytes = SaveCodec.Encode(initial, 0, owner.RulesVersion, content.ContentVersion);
            var loaded = SaveCodec.Decode<CampaignState>(bytes, owner.RulesVersion, content.ContentVersion,
                validate: owner.ValidatePlayerSave);
            Assert.AreEqual(CanonicalJson.Hash(initial), CanonicalJson.Hash(loaded.State));
            var route = RunPublicRoute(content, loaded.State, loaded.Generation);
            var summary = route.Battle.SourceEvents.OfType<SourceMultiHitEvent>()
                .SingleOrDefault(e => e.Actor.Value == 0 && e.Move == Slap);
            if (summary is not { ActualHits: >= 2 }) continue;
            var hits = route.Battle.SourceEvents.OfType<SourceHpChangedEvent>()
                .Where(e => e.Actor.Value == 0 && e.Target.Value == 1 && e.Move == Slap).ToImmutableArray();
            int expectedReceiptCount = summary.ActualHits;
            Assert.AreEqual(expectedReceiptCount, hits.Length);
            Assert.IsTrue(hits.All(e => e.HpBefore > e.HpAfter));
            var pp = route.Battle.SourceEvents.OfType<SourcePpCommandEvent>()
                .Single(e => e.Actor.Value == 0 && e.CurrentMove == Slap);
            Assert.AreEqual(10, pp.Before);
            Assert.AreEqual(9, pp.After);
            for (int ordinal = 0; ordinal < hits.Length; ordinal++)
            {
                int index = route.Battle.SourceEvents.IndexOf(hits[ordinal]);
                var decision = AnimationDispatchFixture.AtResult(
                    route.Battle with { EventIndex = index }, true, SourceBattleScene.On);
                Assert.IsNotNull(decision.Dispatch, decision.Reason);
                Assert.AreEqual(3, decision.Dispatch.SourceMoveId);
                Assert.AreEqual((byte)ordinal, decision.Dispatch.MoveTurn);
                Assert.AreEqual(hits[ordinal].Sequence, decision.Dispatch.ResultSequence);
                Assert.IsFalse(decision.Dispatch.AcknowledgesCurrentReceipt);
            }
            // A second normal RestoreCurrent/public route proves the bytes being
            // exported select the same encounter and committed event sequence.
            var replay = RunPublicRoute(content, loaded.State, loaded.Generation);
            Assert.AreEqual(CanonicalJson.Hash(route.Battle.SourceEvents), CanonicalJson.Hash(replay.Battle.SourceEvents));
            Assert.AreEqual(route.Acknowledgements, replay.Acknowledgements);
            verified = new(initial, bytes, seed, route.Acknowledgements, route.Battle, summary, hits);
        }
        Assert.IsNotNull(verified, "No ordinary Route1 seed in the explicit 0..63 fixture bound committed two Double Slap hits.");
        Console.WriteLine($"CONTROLLED FRESH fixture verified: seed={verified.Seed}; " +
            $"opponent={verified.Battle.State.Opponent.Pokemon.Species}; " +
            $"opening ACKs={verified.OpeningAcknowledgements}; planned={verified.Summary.PlannedHits}; " +
            $"actual={verified.Summary.ActualHits}; HP=" + string.Join(", ", verified.Hits.Select(h => $"{h.HpBefore}->{h.HpAfter}")));
        if (Environment.GetEnvironmentVariable(OutputVariable) is { Length: > 0 } output)
            WriteVerifiedFixture(output, verified, owner, content);
    }

    private static CampaignState CreateControlledField(PretCampaignCatalog content, CampaignSession owner)
    {
        var fresh = owner.State;
        var mechanics = content.PokemonContent!.Pokemon.Mechanics;
        var catalog = mechanics.Catalog;
        var move = catalog.Moves[Slap];
        var clefairyDefinition = catalog.Species[new("pret:SPECIES_CLEFAIRY")];
        Assert.IsTrue(clefairyDefinition.LevelUpMoves.Any(m => m.Level == 13 && m.Move == Slap));
        Assert.AreEqual(10, move.Pp);
        PartyPokemon Owned(string species, int level, long id, uint personality, int metLevel, string location)
        {
            var mon = SourcePokemonFactory.Create(mechanics,
                new(new("pret:SPECIES_" + species), level, fresh.TrainerId,
                    IvPolicy.Fixed(17), PersonalityPolicy.Fixed(personality)), new(12)).Pokemon;
            return new(id, mon, species, fresh.PlayerName, fresh.PlayerGender, 2,
                location, metLevel, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        }
        var starter = Owned("BULBASAUR", 5, 1, 101, 5, "MAPSEC_PALLET_TOWN");
        var lead = Owned("CLEFAIRY", 13, 2, 324468760, 8, "MAPSEC_MT_MOON");
        lead = lead with { Pokemon = lead.Pokemon with { Moves = [new(Slap, move.Pp, move.Pp)] } };
        Assert.AreEqual(43, lead.Pokemon.CurrentHp);
        var choice = content.PokemonContent.StarterChoices.Single(c => c.SourceSpeciesId == 1);
        var rival = content.BattleContent!.Trainers.Trainers.Values
            .Single(t => t.Party.Single().SourceSpeciesId == choice.RivalSourceSpeciesId).Id.Value;
        var flags = fresh.Script.Flags.SetItem("FLAG_SYS_POKEMON_GET", true)
            .SetItem("FLAG_PALLET_LADY_NOT_BLOCKING_SIGN", true).SetItem("FLAG_BEAT_RIVAL_IN_OAKS_LAB", true)
            .SetItem("FLAG_SYS_POKEDEX_GET", true).SetItem("FLAG_HIDE_POKEDEX", true);
        foreach (var item in content.PokemonContent.StarterChoices)
            flags = flags.SetItem(item.BallHideFlag, item.BallId == choice.BallId || item.BallId == choice.RivalBallId);
        var map = content.Maps[new MapId("map.route-1")];
        var definition = content.MapDefinition(map.Id);
        var cell = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y)))
            .First(p => map.GetTile(p.x, p.y) is { Blocked: false } && map.SourceEncounterTypeAt(p.x, p.y) == 1 &&
                !definition.Actors.Any(a => a.X == p.x && a.Y == p.y));
        // These opening and capture receipts are explicitly controlled fixture setup,
        // not a claim that this test naturally replayed the opening or caught Clefairy.
        // The capture species, met level and Mt. Moon origin are source-valid.
        return fresh with
        {
            World = fresh.World with { Player = new(map.Id, cell.x, cell.y, map.GetTile(cell.x, cell.y)!.Elevation, Direction.South) },
            Script = StoryState.Empty with
            {
                Flags = flags, Fame = fresh.Script.Fame,
                Variables = fresh.Script.Variables.SetItem("VAR_STARTER_MON", choice.Index)
                    .SetItem("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB", 6)
                    .SetItem("VAR_MAP_SCENE_VIRIDIAN_CITY_MART", 2).SetItem("VAR_MAP_SCENE_ROUTE22", 1)
                    .SetItem("VAR_MAP_SCENE_PALLET_TOWN_RIVALS_HOUSE", 1)
            },
            Party = [lead, starter], NextPokemonId = 3,
            SeenSpecies = ImmutableSortedSet.Create(1, 35, choice.RivalSourceSpeciesId),
            CaughtSpecies = ImmutableSortedSet.Create(1, 35),
            GcnLinkFlags = 0x31, NextBattleId = 2,
            SettledBattles = fresh.SettledBattles.Add("lab-battle:1"),
            BattledTrainers = fresh.BattledTrainers.Add(rival),
            BattleHistory = [new("lab-battle:1", rival, FirstBattleOutcome.Lost, 0, 0, null, false)],
            Exploration = fresh.Exploration! with
            {
                NextWildBattleId = 2,
                Battles = [new("wild-battle:1", 35, 8, WildBattleOutcome.Caught, 0)],
                Captured = [new(lead.InstanceId, "wild-battle:1", 35, lead.Pokemon.Personality, lead.Pokemon.Ivs, 8, "MAPSEC_MT_MOON")],
                EarlyEncounter = fresh.Exploration.EarlyEncounter! with
                {
                    PreviousMetatileBehavior = (ushort)map.SourceBehaviorAt(cell.x, cell.y)!,
                    StepsSinceLastEncounter = 21, EncounterRateBuff = ushort.MaxValue
                }
            },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag, false, false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = ImmutableSortedDictionary<int, string>.Empty.Add(0, "LOCALID_PLAYER"), SelectedObjectSlot = 0,
            EntryPrograms = [], ArrivalFacing = null, LastTalked = null, Dialogue = null,
            Motions = [], PresentationTicks = 0, PresentationEffects = [], Music = "MUS_ROUTE1", SavedMusic = "MUS_DUMMY"
        };
    }

    private static (CampaignWildBattle Battle, int Acknowledgements) RunPublicRoute(
        PretCampaignCatalog content, CampaignState initial, long generation)
    {
        var session = CampaignSession.RestoreCurrent(content, initial, generation);
        void Send(CampaignCommand command)
        {
            var result = session.Execute("native-multihit:" + session.Revision, session.Revision, command);
            Assert.AreEqual(CommandStatus.Accepted, result.Status, command.Kind + ": " + result.Error);
        }
        Send(new("turn", Direction: Direction.North));
        int acknowledgements = 0;
        while (session.State.WildBattle?.Phase != CampaignBattlePhase.ChooseMove)
        {
            Assert.IsNotNull(session.State.WildBattle, "The ordinary public turn must create a wild encounter.");
            Assert.IsTrue(acknowledgements++ < 100, "Opening acknowledgements exceeded the fixture bound.");
            Send(new("ack"));
        }
        var opening = session.State.WildBattle ?? throw new InvalidDataException("Opening battle disappeared.");
        Assert.IsNotNull(opening.Frame);
        Assert.IsNull(opening.CelioRoamer);
        Assert.AreEqual(Slap, opening.State.Player.Pokemon.Moves[0].Move);
        Send(new("battle_move", MoveSlot: 0));
        var committed = session.State.WildBattle ?? throw new InvalidDataException("Attack battle disappeared.");
        Assert.AreEqual(CampaignBattlePhase.Events, committed.Phase);
        return (committed, acknowledgements);
    }

    private static void WriteVerifiedFixture(string directory, VerifiedRoute verified,
        CampaignSession owner, PretCampaignCatalog content)
    {
        // Writing is opt-in and happens only after exact-byte decode, public execution,
        // multiple committed receipts, dispatch checks and deterministic replay passed.
        var path = Path.Combine(Path.GetFullPath(directory), FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
            CollectionAssert.AreEqual(verified.Bytes, File.ReadAllBytes(path), "Refusing to overwrite a different fixture.");
        else
        {
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            output.Write(verified.Bytes);
        }
        var saved = SaveCodec.Decode<CampaignState>(File.ReadAllBytes(path), owner.RulesVersion,
            content.ContentVersion, validate: owner.ValidatePlayerSave);
        Assert.AreEqual(CanonicalJson.Hash(verified.Initial), CanonicalJson.Hash(saved.State));
        Console.WriteLine($"VERIFIED FIXTURE {path}; generation={saved.Generation}; hatch count=0; " +
            $"Route1 ({saved.State.World.Player.X},{saved.State.World.Player.Y}) South: tap Up, " +
            $"{verified.OpeningAcknowledgements} opening ACKs, FIGHT, DOUBLESLAP slot0; " +
            $"actual hits={verified.Summary.ActualHits}. Controlled opening/capture setup; no native/audio acceptance.");
    }
}
