using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>
/// Controlled current-checkpoint positions and encounter RNG/modifiers exercise public commands. The pre-Dex
/// branch changes the three-part gate only and is not a natural progression/save fixture.
/// Post-Dex routes, rewards and captures must satisfy the full current save contract.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class DunsparceTunnelTests : WildBattleFixture
{
    private const string Tunnel = "map.three-island-dunsparce-tunnel";
    private const string Port = "map.three-island-port";
    private const string DugOut = "LAYOUT_THREE_ISLAND_DUNSPARCE_TUNNEL_DUG_OUT";
    private const string GiftFlag = "FLAG_GOT_NUGGET_FROM_DUNSPARCE_TUNNEL";
    private const string HiddenFlag = "FLAG_HIDDEN_ITEM_THREE_ISLAND_DUNSPARCE_TUNNEL_NUGGET";
    private static readonly ItemId Nugget = new("pret:ITEM_NUGGET");

    private CampaignState AtPort(CampaignState state, int x = 16, int y = 5)
    {
        var map = content.Maps[new(Port)];
        var definition = content.MapDefinition(map.Id);
        var placed = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, Direction.North) },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag,
                    !state.Script.Flags.GetValueOrDefault(a.HideFlag), false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapEdits = null, MapLayout = null,
            FlashLevel = 0, IsSurfing = false, IsCycling = false,
            Script = state.Script with { Flags = state.Script.Flags.SetItem(GiftFlag, false).SetItem(HiddenFlag, false) }
        };
        return CampaignSession.InitializeObjectSlotsForProfileUpgrade(placed);
    }

    private CampaignSession StartPort(bool nationalDex = true, bool fullBag = false, int x = 16, int y = 5)
    {
        var state = AtPort(FieldFixture(), x, y);
        var bag = state.Inventory!.BagItems.RemoveAll(s => s.Item == Nugget);
        if (fullBag)
        {
            var missing = content.EarlyShop!.Items.Where(i => i.Pocket == "POCKET_ITEMS" && i.Id != Nugget &&
                !bag.Any(s => s.Item == i.Id)).Take(state.Inventory.Limits.BagItemSlots - bag.Length)
                .Select(i => new InventoryStack(i.Id, 1));
            bag = bag.AddRange(missing);
            Assert.AreEqual(state.Inventory.Limits.BagItemSlots, bag.Length, "fixture fills real item-slot capacity");
        }
        state = state with { Inventory = state.Inventory with { BagItems = bag } };
        Assert.IsNull(validator.ValidatePlayerSave(state), "controlled post-Dex Port fixture");
        var session = CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
        if (!nationalDex)
        {
            // Only this intentionally counterfactual branch bypasses restore validation:
            // recorded late-game acquisitions cannot be represented as a pre-Dex save.
            state = session.State with { NationalDexMagic = 0, Script = session.State.Script with {
                Flags = session.State.Script.Flags.SetItem("FLAG_SYS_NATIONAL_DEX", false),
                Variables = session.State.Script.Variables.SetItem("VAR_NATIONAL_DEX", 0) } };
            typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state,
                    session.RulesVersion, content.ContentVersion, session.Revision));
        }
        return session;
    }

    private List<string> DrainField(CampaignSession session)
    {
        var labels = new List<string>();
        for (int n = 0; n < 200; n++)
        {
            if (session.State.Dialogue is { } dialogue) labels.Add(dialogue.TextSymbol);
            if (session.State.Script.Completed && session.State.Script.Wait is null &&
                session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty) return labels;
            Assert.IsNull(session.State.WildBattle, "tunnel has no wild encounter table");
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        }
        Assert.Fail("field presentation did not finish");
        return labels;
    }

    private void Step(CampaignSession session, Direction direction)
    {
        Send(session, new("move", Direction: direction));
        DrainField(session);
    }

    private void EnterTunnel(CampaignSession session)
    {
        Step(session, Direction.North);
        Assert.AreEqual(Tunnel, session.State.World.Player.MapId.Value);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_THREE_ISLAND_DUNSPARCE_TUNNEL"));
    }

    private IFieldMap CurrentMap(CampaignSession session) => session.State.MapLayout is { } layout
        ? content.MapLayout(session.State.World.Player.MapId, layout)! : content.Maps[session.State.World.Player.MapId];

    private void WalkTo(CampaignSession session, int x, int y)
    {
        var map = CurrentMap(session);
        var start = (session.State.World.Player.X, session.State.World.Player.Y);
        var target = (x, y);
        var queue = new Queue<(int X, int Y)>();
        var seen = new HashSet<(int, int)> { start };
        var previous = new Dictionary<(int, int), ((int, int) Cell, Direction Direction)>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var cell) && cell != target)
            foreach (var (dx, dy, direction) in new[] { (0, -1, Direction.North), (1, 0, Direction.East),
                (0, 1, Direction.South), (-1, 0, Direction.West) })
            {
                var next = (cell.X + dx, cell.Y + dy);
                if (seen.Contains(next) || map.GetTile(next.Item1, next.Item2) is not { Blocked: false } ||
                    map.FieldWarps.Any(w => w.X == next.Item1 && w.Y == next.Item2) ||
                    session.State.Actors.Values.Any(a => a.Active && !a.Invisible && a.X == next.Item1 && a.Y == next.Item2)) continue;
                seen.Add(next); previous[next] = (cell, direction); queue.Enqueue(next);
            }
        Assert.IsTrue(seen.Contains(target), $"walkable path to {target}");
        var path = new Stack<Direction>();
        for (var cell = target; cell != start; cell = previous[cell].Cell) path.Push(previous[cell].Direction);
        foreach (var direction in path) Step(session, direction);
        Assert.AreEqual(target, (session.State.World.Player.X, session.State.World.Player.Y));
    }

    private void Face(CampaignSession session, Direction direction)
    {
        if (session.State.World.Player.Facing != direction) Send(session, new("turn", Direction: direction));
    }

    private List<string> TalkToProspector(CampaignSession session)
    {
        var actor = session.State.Actors.Values.Single(a => a.ScriptId == "ThreeIsland_DunsparceTunnel_EventScript_Prospector");
        if (session.NationalPokedexEnabled) { WalkTo(session, actor.X, actor.Y - 1); Face(session, Direction.South); }
        else { WalkTo(session, actor.X - 1, actor.Y); Face(session, Direction.East); }
        Send(session, new("interact"));
        return DrainField(session);
    }

    private CampaignSession Roundtrip(CampaignSession session)
    {
        Assert.IsNull(session.ValidatePlayerSave(session.State), "stable current save");
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        Assert.IsFalse(Encoding.UTF8.GetString(bytes).Contains("\"MapLayout\"", StringComparison.Ordinal), "layout selector remains transient");
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Assert.AreEqual(CanonicalJson.Hash(session.State), CanonicalJson.Hash(restored.State));
        Assert.AreEqual(session.State.MapLayout, restored.State.MapLayout, "Continue rebuilds layout from generated entry IR");
        return restored;
    }

    [TestMethod]
    public void PreDexProspectorAndWallKeepEasternTunnelClosed()
    {
        var session = StartPort(nationalDex: false);
        EnterTunnel(session);
        Assert.IsFalse(session.NationalPokedexEnabled);
        Assert.IsNull(session.State.MapLayout);
        var actor = session.State.Actors.Values.Single();
        Assert.AreEqual((6, 3), (actor.X, actor.Y));
        Assert.AreEqual((6, 3), (actor.TemplateX, actor.TemplateY));
        Assert.AreEqual("MOVEMENT_TYPE_FACE_RIGHT", actor.MovementType);
        Assert.IsTrue(CurrentMap(session).GetTile(7, 3)!.Blocked);
        var labels = TalkToProspector(session);
        Assert.IsTrue(labels.Contains("ThreeIsland_DunsparceTunnel_Text_ProspectingForGold"));
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, Nugget));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(GiftFlag));
        Step(session, Direction.East);
        Assert.AreEqual((5, 3), (session.State.World.Player.X, session.State.World.Player.Y));
        WalkTo(session, 3, 3);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Tunnel; n++) Step(session, Direction.South);
        Assert.AreEqual(Port, session.State.World.Player.MapId.Value);
    }

    [TestMethod]
    public void PostDexBothEntrancesAndExitsUseDugGeometryAndSurviveContinue()
    {
        var session = StartPort();
        EnterTunnel(session);
        Assert.AreEqual(DugOut, session.State.MapLayout);
        Assert.IsFalse(CurrentMap(session).GetTile(7, 3)!.Blocked);
        Assert.AreEqual((23, 2), (session.State.Actors.Values.Single().X, session.State.Actors.Values.Single().Y));
        WalkTo(session, 7, 3);
        session = Roundtrip(session); // This position is a wall in the base layout.
        WalkTo(session, 25, 4);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Tunnel; n++) Step(session, Direction.South);
        Assert.AreEqual(Port, session.State.World.Player.MapId.Value);
        Assert.AreEqual(38, session.State.World.Player.X);
        Assert.IsNull(session.State.MapLayout);
        session = Roundtrip(session);
        EnterTunnel(session);
        Assert.AreEqual(DugOut, session.State.MapLayout);
        WalkTo(session, 3, 3);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Tunnel; n++) Step(session, Direction.South);
        Assert.AreEqual(Port, session.State.World.Player.MapId.Value);
        Assert.AreEqual(16, session.State.World.Player.X);
        Roundtrip(session);
    }

    [TestMethod]
    [DataRow(16, 5, 18)]
    [DataRow(38, 6, 19)]
    public void EscapeRopeAfterContinueReturnsToActualTunnelEntrance(int entranceX, int entranceY, int markerX)
    {
        var session = StartPort(x: entranceX, y: entranceY);
        var state = session.State with { Inventory = session.State.Inventory! with {
            BagItems = session.State.Inventory!.BagItems.RemoveAll(s => s.Item == EscapeRopeRules.Item)
                .Add(new(EscapeRopeRules.Item, 2)) } };
        session = CampaignSession.RestoreCurrent(content, state, session.Revision);
        EnterTunnel(session);
        Assert.AreEqual(new EscapeRopeDestination(new(Port), entranceX, entranceY), session.State.EscapeDestination);
        WalkTo(session, 7, 3);
        session = Roundtrip(session);
        Send(session, new("bag_open"));
        Send(session, new("town_map_open", Text: QuestItemRules.TownMapId.Value));
        Assert.IsNotNull(session.State.TownMap);
        var atlas = PretFirstSeviiTownMap.Load(TestAssets.Root);
        Assert.AreEqual(1, atlas.RegionForMap(Tunnel));
        Assert.IsTrue(atlas.RequiresEscapeWarpForMap(Tunnel));
        Assert.IsFalse(atlas.TryPositionForMap(Tunnel, 7, 3, out _), "indoor placeholder must not become a player marker");
        var entrance = session.State.EscapeDestination!;
        Assert.IsTrue(atlas.TryPositionForMap(entrance.MapId.Value, entrance.X, entrance.Y, out var point));
        Assert.AreEqual(new TownMapPoint(markerX, 13), point, "source Port halves project the saved entrance independently");
        Send(session, new("town_map_close"));
        Send(session, new("field_escape_rope", Text: EscapeRopeRules.Item.Value));
        Assert.IsNotNull(session.State.EscapeRope);
        Assert.AreEqual(Tunnel, session.State.World.Player.MapId.Value, "used-item page precedes warp");
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, EscapeRopeRules.Item));
        Send(session, new("menu_ack")); DrainField(session);
        Assert.IsNull(session.State.EscapeRope);
        Assert.IsNull(session.State.MapLayout);
        Assert.AreEqual(new TilePosition(new(Port), entranceX, entranceY, 3, Direction.South), session.State.World.Player);
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, EscapeRopeRules.Item));
        Roundtrip(session);
    }

    [TestMethod]
    public void ProspectorFullBagCanRetryAfterPublicTossAndNeverDuplicatesNugget()
    {
        var session = StartPort(fullBag: true);
        EnterTunnel(session);
        var bagBefore = CanonicalJson.Hash(session.State.Inventory);
        var labels = TalkToProspector(session);
        Assert.IsTrue(labels.Contains("ThreeIsland_DunsparceTunnel_Text_WhoopsBagCrammedFull"));
        Assert.AreEqual(bagBefore, CanonicalJson.Hash(session.State.Inventory));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(GiftFlag));
        session = Roundtrip(session);
        var disposable = session.State.Inventory!.BagItems.First(s => s.Quantity == 1 && session.CanTossBagItem(s.Item));
        Send(session, new("bag_open"));
        Send(session, new("bag_toss_open", Text: disposable.Item.Value));
        Send(session, new("bag_toss_confirm", Text: session.State.ItemMenu!.Toss!.WaitId, Choice: 0));
        Send(session, new("bag_toss_ack", Text: session.State.ItemMenu!.Toss!.WaitId));
        Send(session, new("menu_back"));
        TalkToProspector(session);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(GiftFlag));
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, Nugget));
        session = Roundtrip(session);
        labels = TalkToProspector(session);
        Assert.IsTrue(labels.Contains("ThreeIsland_DunsparceTunnel_Text_ThatsANugget"));
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, Nugget));
        Roundtrip(session);
    }

    [TestMethod]
    public void HiddenNuggetIsIndependentOneTimePickup()
    {
        var session = StartPort();
        EnterTunnel(session);
        WalkTo(session, 20, 3); Face(session, Direction.East);
        Send(session, new("interact")); DrainField(session);
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, Nugget));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(HiddenFlag));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(GiftFlag));
        session = Roundtrip(session);
        var before = CanonicalJson.Hash(session.State);
        var again = session.Execute("repeat-hidden-nugget", session.Revision, new("interact"));
        Assert.AreEqual(CommandStatus.Rejected, again.Status);
        Assert.AreEqual("nothing_to_interact", again.Error);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State));
        TalkToProspector(session);
        Assert.AreEqual(2, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, Nugget));
        Roundtrip(session);
    }

    [TestMethod]
    [DataRow("map.seafoam-islands-b3f", "FLAG_STOPPED_SEAFOAM_B3F_CURRENT", "LAYOUT_SEAFOAM_ISLANDS_B3F_CURRENT_STOPPED")]
    [DataRow("map.seafoam-islands-b4f", "FLAG_STOPPED_SEAFOAM_B4F_CURRENT", "LAYOUT_SEAFOAM_ISLANDS_B4F_CURRENT_STOPPED")]
    public void SharedLayoutRestorePreservesStoppedSeafoamCurrent(string mapName, string stoppedFlag, string layout)
    {
        var state = FieldFixture();
        var id = new MapId(mapName);
        var map = content.MapLayout(id, layout)!;
        var definition = content.MapDefinition(id);
        var flags = state.Script.Flags.SetItem(stoppedFlag, true);
        foreach (var boulder in SeafoamFieldRules.Boulders.Where(b => b.Map == id && b.LocalId <= 2))
            flags = flags.SetItem(boulder.HideFlag, false);
        var cell = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y)))
            .First(p => map.GetTile(p.x, p.y) is { Blocked: false } && map.SourceBehaviorAt(p.x, p.y) == 8 &&
                !definition.Actors.Any(a => a.X == p.x && a.Y == p.y));
        state = state with {
            World = state.World with { Player = new(id, cell.x, cell.y, map.GetTile(cell.x, cell.y)!.Elevation, Direction.South) },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag, !flags.GetValueOrDefault(a.HideFlag),
                    false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapLayout = null,
            MapEdits = SeafoamFieldRules.OnLoadChanges(id, flags).ToImmutableSortedDictionary(c => c.X + ":" + c.Y,
                c => new FieldMetatileEdit(c.X, c.Y, c.MetatileId, c.Collision, c.Elevation)),
            Script = state.Script with { Flags = flags, Variables = state.Script.Variables
                .SetItem(SeafoamFieldRules.FallingVariable, 0).SetItem(SeafoamFieldRules.B4FSceneVariable, 0) }
        };
        state = CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
        var before = CanonicalJson.Hash(state);
        var session = CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State), "restore does not rerun persisted entry effects or RNG");
        Assert.AreEqual(layout, session.State.MapLayout);
        Assert.IsTrue(session.SeafoamCurrentStopped(id));
        Roundtrip(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PortGrassPublicTurnOrMoveCapturesDunsparceAndContinues(bool walking)
    {
        var map = content.Maps[new(Port)];
        var table = content.LandEncounters![map.Id];
        Assert.AreEqual(1, table.RawEncounterRate);
        Assert.IsTrue(table.Slots.All(slot => slot.Species == new SpeciesId("pret:SPECIES_DUNSPARCE")));
        CollectionAssert.AreEqual(new[] { 15, 15, 10, 10, 20, 20, 25, 30, 25, 30, 5, 35 },
            table.Slots.Select(slot => (int)slot.MinimumLevel).ToArray());
        Assert.IsTrue(table.Slots.All(slot => slot.MinimumLevel == slot.MaximumLevel));
        var cell = Enumerable.Range(1, map.Height - 2).SelectMany(y => Enumerable.Range(1, map.Width - 2).Select(x => (x, y)))
            .First(p => map.GetTile(p.x, p.y) is { Blocked: false } && map.SourceEncounterTypeAt(p.x, p.y) == 1 &&
                map.GetTile(p.x + 1, p.y) is { Blocked: false } && map.SourceEncounterTypeAt(p.x + 1, p.y) == 1);
        var state = AtPort(FieldFixture(), cell.x, cell.y);
        state = state with {
            World = state.World with { Rng = state.World.Rng with { Seed = 3 }, Player = state.World.Player with { Facing = Direction.South } },
            Inventory = state.Inventory! with { Balls = [new(new("pret:ITEM_MASTER_BALL"), 1)] },
            Exploration = state.Exploration! with { EarlyEncounter = new(new(0), (ushort)map.SourceBehaviorAt(cell.x, cell.y)!,
                ushort.MaxValue, 21, RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion) }
        };
        Assert.IsNull(validator.ValidatePlayerSave(state));
        var session = CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
        Send(session, new(walking ? "move" : "turn", Direction: walking ? Direction.East : Direction.North));
        Drain(session);
        Assert.IsNotNull(session.State.WildBattle, "actual Port grass must start an ordinary wild battle");
        var enemy = session.State.WildBattle.State.Opponent.Pokemon;
        Assert.AreEqual(new SpeciesId("pret:SPECIES_DUNSPARCE"), enemy.Species);
        Assert.AreEqual(206, enemy.SourceSpeciesId);
        Assert.IsTrue(table.Slots.Any(slot => slot.MinimumLevel == enemy.Level));
        long instance = session.State.NextPokemonId;
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL"));
        Drain(session);
        Assert.IsNull(session.State.WildBattle);
        var caught = session.State.Party.Concat(session.State.Storage!.Boxes.SelectMany(b => b.Slots).OfType<PartyPokemon>())
            .Single(p => p.InstanceId == instance);
        Assert.AreEqual(enemy.Species, caught.Pokemon.Species);
        Assert.AreEqual(enemy.Personality, caught.Pokemon.Personality);
        Assert.AreEqual(enemy.Ivs, caught.Pokemon.Ivs);
        Assert.AreEqual("MAPSEC_THREE_ISLE_PORT", caught.MetLocation);
        Assert.IsTrue(session.State.CaughtSpecies.Contains(206));
        Roundtrip(session);
    }
}
