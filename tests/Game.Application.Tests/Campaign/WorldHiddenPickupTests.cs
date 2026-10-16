using System.Collections.Immutable;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class WorldHiddenPickupTests : WildBattleFixture
{
    [TestMethod]
    public void PreviouslyUnboundHiddenPickupsCollectOnceAndSurviveContinue()
    {
        var positions = new[] {
            ("CeruleanCity",18,7), ("FuchsiaCity",33,26), ("Route13",23,14), ("Route14",9,20), ("Route14",19,53),
            ("Route3",26,9), ("Route4",43,2), ("Route4",5,4), ("Route4",67,17), ("Route6",5,5), ("Route6",19,5),
            ("VermilionCity",14,11), ("UndergroundPath_NorthSouthTunnel",5,30), ("UndergroundPath_NorthSouthTunnel",5,6),
            ("UndergroundPath_NorthSouthTunnel",2,57), ("UndergroundPath_NorthSouthTunnel",1,24), ("UndergroundPath_NorthSouthTunnel",6,53),
            ("UndergroundPath_NorthSouthTunnel",3,15), ("UndergroundPath_NorthSouthTunnel",3,39),
            ("Route24",19,4), ("Route25",14,2), ("Route25",58,6), ("Route25",33,8), ("Route25",40,3)
        }.ToHashSet();
        int checkedPickups = 0;
        foreach (var map in content.SourceMaps.Values)
        foreach (var trigger in content.MapDefinition(map.Id).BackgroundTriggers.Where(t => t.HiddenItem is not null && t.HiddenItem != "ITEM_NONE" && !t.Underfoot))
        {
            if (!positions.Contains((map.SourceMapName, trigger.X, trigger.Y))) continue;
            var pickup = content.FieldPickups.Single(p => p.Hidden && p.MapSymbol == map.SourceMapSymbol && p.X == trigger.X && p.Y == trigger.Y);
            var adjacent = new[] { (0, -1, Direction.North), (1, 0, Direction.East),
                (0, 1, Direction.South), (-1, 0, Direction.West) }
                .FirstOrDefault(d => content.ConnectedFieldMaps![map.Id].CellAt(pickup.X - d.Item1, pickup.Y - d.Item2) is { Collision: 0 } cell &&
                    (pickup.Elevation == 0 || cell.Elevation == pickup.Elevation) && !content.MapDefinition(map.Id).Actors.Any(a => a.X == pickup.X - d.Item1 && a.Y == pickup.Y - d.Item2));
            Assert.AreNotEqual(default, adjacent, pickup.Flag + " requires an accessible approach");
            var state = FieldFixture();
            var flags = state.Script.Flags.SetItem(pickup.Flag, false);
            state = state with {
                World = state.World with { Player = new(map.Id, pickup.X - adjacent.Item1, pickup.Y - adjacent.Item2, content.ConnectedFieldMaps![map.Id].CellAt(pickup.X - adjacent.Item1, pickup.Y - adjacent.Item2)!.Elevation, adjacent.Item3) },
                Actors = content.MapDefinition(map.Id).Actors.ToImmutableSortedDictionary(a => a.LocalId,
                    a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                        Direction.South, a.MovementType, a.ScriptId, a.HideFlag, false, false, a.X, a.Y), StringComparer.Ordinal),
                ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapEdits = null, MapLayout = null,
                FlashLevel = 0, IsSurfing = false, IsCycling = false, Music = content.MapDefinition(map.Id).Music,
                Inventory = state.Inventory! with { BagItems = [], Balls = [], Berries = [] },
                Script = state.Script with { Flags = flags }
            };
            state = CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
            if (map.SourceMapName == "CeruleanCity")
            {
                var fullBag = content.EarlyShop!.Items.Where(i => i.Pocket == "POCKET_ITEMS" && i.Id != pickup.Item)
                    .Take(state.Inventory!.Limits.BagItemSlots).Select(i => new InventoryStack(i.Id, 1)).ToImmutableArray();
                Assert.AreEqual(state.Inventory.Limits.BagItemSlots, fullBag.Length);
                var blocked = CampaignSession.RestoreCurrent(content, state with { Inventory = state.Inventory with { BagItems = fullBag } }, checkpoint.Generation);
                Send(blocked, new("interact"));
                DrainPickup(blocked, pickup.Flag);
                Assert.IsFalse(blocked.State.Script.Flags.GetValueOrDefault(pickup.Flag));
                CollectionAssert.AreEqual(fullBag.ToArray(), blocked.State.Inventory!.BagItems.ToArray());
                StrictRoundtrip(blocked);
            }
            var session = CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
            Send(session, new("interact"));
            DrainPickup(session, pickup.Flag);
            Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(pickup.Flag), pickup.Flag);
            var bag = pickup.Pocket switch { "POCKET_BERRY_POUCH" => session.State.Inventory!.Berries!.Value,
                "POCKET_POKE_BALLS" => session.State.Inventory!.Balls!.Value, _ => session.State.Inventory!.BagItems };
            Assert.AreEqual(pickup.Quantity, OpeningInventoryRules.Quantity(bag, pickup.Item), pickup.Flag);
            StrictRoundtrip(session);
            var repeat = session.Execute("repeat-hidden:" + pickup.Flag, session.Revision, new("interact"));
            Assert.AreEqual(CommandStatus.Rejected, repeat.Status, pickup.Flag);
            Assert.AreEqual("nothing_to_interact", repeat.Error, pickup.Flag);
            checkedPickups++;
        }
        Assert.AreEqual(positions.Count, checkedPickups);
    }
    private void DrainPickup(CampaignSession session, string flag)
    {
        for (int n = 0; !session.State.Script.Completed || session.State.Script.Wait is not null || session.State.Dialogue is not null; n++)
        {
            Assert.IsTrue(n < 100, flag);
            Send(session, session.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress ? new("button_press", Text: "A") : new("ack"));
        }
    }

}
