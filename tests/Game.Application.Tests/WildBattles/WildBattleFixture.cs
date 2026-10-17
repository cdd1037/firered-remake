using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Capture;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Application.WildBattles;
[TestClass]
public abstract class WildBattleFixture
{
    private protected global::Game.Content.PretCampaignCatalog content = default !;
    private protected global::Game.Application.CampaignSession validator = default !;
    private protected global::Game.Application.LoadedSave<global::Game.Application.CampaignState> checkpoint = default !;
    private protected global::Game.Domain.Pokemon.GamePokemonCatalog ownership = default !;
    private protected global::Game.Domain.Pokemon.SourceMechanicsCatalog mechanics = default !;
    private protected global::Game.Domain.Pokemon.PokemonCatalog catalog = default !;
    private protected int commands = default !;
    [TestInitialize]
    public void SetUp()
    {
        content = CurrentCampaignFixture.Catalog;
        validator = CampaignSession.CreateCurrent(content);
        checkpoint = CurrentCampaignFixture.Checkpoint;
        ownership = content.PokemonContent!.Pokemon;
        mechanics = ownership.Mechanics;
        catalog = mechanics.Catalog;
        commands = 0;
    }

    private protected void Send(CampaignSession session, CampaignCommand command)
    {
        string id = "canonical-wild:" + commands++;
        long revision = session.Revision;
        var before = session.State;
        var result = session.Execute(id, revision, command);
        if (result.Status != CommandStatus.Accepted)
            throw new InvalidDataException(command + " => " + result.Error);
        var stale = session.Execute(id, revision, command);
        Assert.IsTrue(stale.Status == CommandStatus.Rejected && stale.Error == "stale_revision" && ReferenceEquals(stale.State, result.State), "atomic stale command");
        if (session.State.WildBattle?.Frame is { } frame)
        {
            Assert.IsTrue(SourceBattleIdentityRules.Validate(frame)is null, "frame structural identity");
            Assert.IsTrue(frame.Party(BattleSide.Player).Members.Select(m => CanonicalJson.Hash(m.Pokemon)).SequenceEqual(session.State.Party.Select(p => CanonicalJson.Hash(p.Pokemon))), "canonical party projection");
        }
    }

    private protected void Drain(CampaignSession session, bool stopLearning = false, bool nickname = false, bool stopCapture = false)
    {
        for (int n = 0; n < 1500; n++)
        {
            if (session.State.Evolution is { } evolution)
            {
                Send(session, evolution.Stage == CampaignEvolutionStage.Proposal ? new("evolution_choice", Choice: 0) : new("ack"));
                continue;
            }

            if (session.State.WildBattle is not { } b)
                return;
            if (b.Capture is { } c)
            {
                if (stopCapture)
                    return;
                if (c.Stage == CampaignCaptureStage.Naming)
                    Send(session, new("nickname", Text: "CANON"));
                else if (c.Stage == CampaignCaptureStage.NicknameChoice)
                    Send(session, new("capture_choice", Choice: nickname ? 1 : 0));
                else
                    Send(session, new("ack"));
            }
            else if (b.Phase == CampaignBattlePhase.Events)
                Send(session, new("ack"));
            else if (b.Phase == CampaignBattlePhase.Results)
            {
                if (b.PageIndex + 1 >= b.Pages.Length && !b.PendingMoves.IsEmpty)
                {
                    if (stopLearning)
                        return;
                    Send(session, new("wild_learn", MoveSlot: -1));
                }
                else
                    Send(session, new("ack"));
            }
            else
                return;
        }

        throw new InvalidDataException("bounded presentation did not finish");
    }

    private protected CampaignState FieldFixture()
    {
        var original = checkpoint.State;
        // Retain the recorded individual's identity and level; use its naturally learned low-power Confusion.
        var lead = original.Party[0];
        var move = catalog.Moves[new("pret:MOVE_CONFUSION")];
        Assert.IsTrue(catalog.Species[lead.Pokemon.Species].LevelUpMoves.Any(m => m.Move == move.Id && m.Level <= lead.Pokemon.Level), "natural controlled roamer damage move");
        original = original with
        {
            Party = original.Party.SetItem(0, lead with { Status = 0, Pokemon = lead.Pokemon with { CurrentHp = lead.Pokemon.Stats.MaxHp, Moves = [new(move.Id, move.Pp, move.Pp)] } })
        };
        var map = content.Maps[new MapId("map.route-1")];
        var definition = content.MapDefinition(map.Id);
        var cell = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y))).First(p => map.GetTile(p.x, p.y)is { Blocked: false } && map.SourceEncounterTypeAt(p.x, p.y) == 1 && !definition.Actors.Any(a => a.X == p.x && a.Y == p.y));
        var tile = map.GetTile(cell.x, cell.y)!;
        return original with
        {
            World = original.World with
            {
                Player = new(map.Id, cell.x, cell.y, tile.Elevation, Direction.South)
            },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId, a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation, Direction.South, a.MovementType, a.ScriptId, a.HideFlag, false, false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = ImmutableSortedDictionary<int, string>.Empty.Add(0, "LOCALID_PLAYER"),
            SelectedObjectSlot = 0,
            LastTalked = null,
            MapEdits = null,
            IsSurfing = false,
            IsCycling = false,
            Roamer = original.Roamer!with
            {
                Runtime = original.Roamer.Runtime with
                {
                    Location = content.CelioRoamerMaps![new("map.route-2")]
                }
            },
            Exploration = original.Exploration!with
            {
                EarlyEncounter = original.Exploration.EarlyEncounter!with
                {
                    PreviousMetatileBehavior = (ushort)map.SourceBehaviorAt(cell.x, cell.y)!,
                    StepsSinceLastEncounter = 21,
                    EncounterRateBuff = ushort.MaxValue
                }
            },
            Script = original.Script with
            {
                Variables = original.Script.Variables.SetItem("VAR_REPEL_STEP_COUNT", 0)
            },
            Inventory = original.Inventory!with
            {
                Balls = [new(new ItemId("pret:ITEM_ULTRA_BALL"), 20)]
            }
        };
    }

    private protected CampaignSession StartField()
    {
        var fixture = FieldFixture();
        var validationError = validator.ValidatePlayerSave(fixture);
        Assert.IsNull(validationError, "controlled current field fixture: " + validationError);
        var session = CampaignSession.RestoreCurrent(content, fixture, checkpoint.Generation);
        Send(session, new("turn", Direction: Direction.North));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle is { Frame: not null, CelioRoamer: null, Phase: CampaignBattlePhase.ChooseMove }, "public field starts canonical ordinary battle");
        return session;
    }

    private protected void StrictRoundtrip(CampaignSession session)
    {
        var validationError = session.ValidatePlayerSave(session.State);
        Assert.IsNull(validationError, "strict current save: " + validationError);
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Assert.IsTrue(CanonicalJson.Hash(restored.State) == CanonicalJson.Hash(session.State), "strict save Continue preserves entire payload");
    }

    private protected PartyPokemon Owned(string species, int level, long id, params string[] moves)
    {
        var mon = SourcePokemonFactory.Create(mechanics, new(new("pret:SPECIES_" + species), level, checkpoint.State.TrainerId, IvPolicy.Fixed(31), PersonalityPolicy.Fixed((uint)id)), new(12)).Pokemon;
        if (moves.Length != 0)
            mon = mon with
            {
                Moves = moves.Select(name =>
                {
                    var move = catalog.Moves[new("pret:MOVE_" + name)];
                    return new PokemonMoveSlot(move.Id, move.Pp, move.Pp);
                }).ToImmutableArray()
            };
        return checkpoint.State.Party[0] with
        {
            InstanceId = id,
            Pokemon = mon,
            Nickname = species,
            HeldItem = "ITEM_NONE",
            Status = 0,
            MetLevel = level,
            MetLocation = "MAPSEC_ROUTE_1"
        };
    }

    private protected CampaignSession Controlled(ImmutableArray<PartyPokemon> party, PartyPokemon enemy, uint seed = 123)
    {
        var session = CampaignSession.CreateCurrent(content);
        var basis = FieldFixture()with
        {
            Party = party
        };
        basis = basis with
        {
            Inventory = basis.Inventory!with
            {
                BagItems = basis.Inventory.BagItems.Add(new(new("pret:ITEM_POTION"), 1))
            }
        };
        // Explicit test content supplies this controlled opponent's level. Admit its natural
        // initial instance first, then apply the test's HP/status/move fixture to the live frame.
        // This is lifecycle coverage, not a claim that Route 1 naturally contains level-30 Pidgey.
        var mapId = basis.World.Player.MapId;
        var table = content.LandEncounters![mapId] with { Slots = content.LandEncounters[mapId].Slots.Select(s => s with {
            Species = enemy.Pokemon.Species, MinimumLevel = (byte)enemy.Pokemon.Level,
            MaximumLevel = (byte)enemy.Pokemon.Level }).ToImmutableArray() };
        var definition = new OrdinaryLandEncounterCatalog(new Dictionary<MapId, EarlyLandEncounterTable> { [mapId] = table },
            new Dictionary<MapId, IFieldMap> { [mapId] = content.Maps[mapId] }, ownership.Catalog)[mapId];
        var pending = new EarlyLandEncounterState(new(0), 2, Pending: new(new(mapId, 0, enemy.Pokemon.Species,
            (byte)enemy.Pokemon.Level), 2), RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
        var initialEnemy = enemy.Pokemon with { CurrentHp = enemy.Pokemon.Stats.MaxHp,
            Moves = PokemonMoveRules.InitialMoves(ownership.Catalog, ownership.Catalog.Species[enemy.Pokemon.Species], enemy.Pokemon.Level) };
        var entry = SourceWildBattlePolicy.StartOwned(ownership,
            party.Select(p => new WildBattleCombatant(p.Pokemon, HeldItem: p.HeldItem, Status: p.Status)).ToImmutableArray(),
            0, new(initialEnemy), new(seed), new() { OrdinaryLand = definition.BindBattle(pending) });
        Assert.IsTrue(entry.Succeeded, "controlled source admission: " + entry.Fault);
        var entered = entry.State!;
        var enemyCell = entered.Get(new(1));
        entered = entered with {
            Slots = entered.Slots.SetItem(1, enemyCell with { Body = enemyCell.Body! with {
                Pokemon = enemy.Pokemon, HeldItem = enemy.HeldItem, Status = enemy.Status } }),
            Parties = entered.Parties.SetItem(1, entered.Party(BattleSide.Opponent) with {
                Members = [new(enemy.Pokemon, enemy.HeldItem, enemy.Status)] })
        };
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(catalog, entered));
        var prepare = typeof(CampaignSession).GetMethod("PrepareCanonicalWildFrame", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var frame = (SourceBattleFrame)prepare.Invoke(session, [basis, entered])!;
        var view = CampaignWildReadProjection.From(frame);
        var battle = new CampaignWildBattle("wild-battle:" + basis.Exploration!.NextWildBattleId, party[0], frame, entry.Events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), 0, view.Player.Pokemon, view.Opponent.Pokemon, CampaignBattlePhase.Events, [], 0, entry.Trace)
        {
            SourceEvents = entry.Events,
            DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(entry.EntrySlots.SetItem(1,
                entry.EntrySlots[1] with { Body = entry.EntrySlots[1].Body! with {
                    Pokemon = enemy.Pokemon, HeldItem = enemy.HeldItem, Status = enemy.Status } }))),
            EntryLevels = party.Select(p => p.Pokemon.Level).ToImmutableArray(),
            ActivePartyIndex = 0,
            Origin = new("MAPSEC_ROUTE_1", enemy.Pokemon.Level, enemy.Nickname),
            FaintedMembers = ImmutableSortedSet<long>.Empty,
            OpponentHpAtLastSwitch = enemy.Pokemon.CurrentHp
        };
        var state = basis with
        {
            WildBattle = battle,
            World = basis.World with
            {
                Rng = frame.Rng
            },
            Exploration = basis.Exploration with
            {
                NextWildBattleId = basis.Exploration.NextWildBattleId + 1
            }
        };
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, checkpoint.Generation));
        Drain(session);
        return session;
    }

    private protected CampaignSession Scene(string mapName, string script, bool scope = true)
    {
        var session = CampaignSession.CreateCurrent(content);
        var basis = FieldFixture();
        var map = new MapId(mapName);
        var defs = content.MapDefinition(map);
        var target = defs.Actors.FirstOrDefault(a => a.ScriptId == script);
        var party = ImmutableArray.Create(Owned("SNORLAX", 100, 1, "STRENGTH"));
        var flags = basis.Script.Flags.SetItem("FLAG_RESCUED_LOSTELLE", false).SetItem("FLAG_HIDE_LOSTELLE_IN_BERRY_FOREST", false).SetItem("FLAG_HIDE_ROUTE_12_SNORLAX", false).SetItem("FLAG_SYS_SPECIAL_WILD_BATTLE", false);
        var keys = basis.Inventory!.KeyItems!.Value;
        if (!scope)
            keys = keys.RemoveAll(i => i.Item == RocketHideoutItemRules.SilphScopeId);
        var state = basis with
        {
            World = basis.World with
            {
                Player = new(map, target?.X ?? 11, target is null ? 15 : target.Y + 1, target?.Elevation ?? 3, Direction.North)
            },
            Actors = defs.Actors.ToImmutableSortedDictionary(a => a.LocalId, a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation, Direction.South, a.MovementType, a.ScriptId, a.HideFlag, true, false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = null,
            LastTalked = target?.LocalId,
            EntryPrograms = [],
            Dialogue = null,
            Motions = [],
            PresentationTicks = 0,
            PresentationEffects = [],
            Script = StoryState.Empty with
            {
                Variables = basis.Script.Variables.SetItem("VAR_LAST_TALKED", target?.SourceLocalId ?? 0).SetItem("VAR_MAP_SCENE_THREE_ISLAND", 3).SetItem("VAR_MAP_SCENE_POKEMON_TOWER_6F", 0),
                Flags = flags
            },
            Party = party,
            NextPokemonId = 2,
            Storage = PokemonStorageState.Empty,
            Exploration = basis.Exploration!with
            {
                Battles = [],
                Captured = [],
                NextWildBattleId = 1
            },
            Inventory = basis.Inventory with
            {
                KeyItems = keys,
                Balls = [new(new("pret:ITEM_MASTER_BALL"), 1)]
            }
        };
        var start = typeof(CampaignSession).GetMethod("StartStory", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)pump.Invoke(session, [start.Invoke(session, [state, script])])!;
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, checkpoint.Generation));
        for (int i = 0; i < 250 && session.State.WildBattle is null; i++)
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        Assert.IsTrue(session.State.WildBattle?.Frame is not null, "authenticated source scene entered canonical battle: " + script);
        Drain(session);
        return session;
    }
}
