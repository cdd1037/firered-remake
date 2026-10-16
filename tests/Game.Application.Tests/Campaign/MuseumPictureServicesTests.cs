using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled museum positions followed by the real public interaction and acknowledgement commands.</summary>
[TestClass, TestCategory("Game")]
public sealed class MuseumPictureServicesTests : WildBattleFixture
{
    private CampaignState AtExhibit(int y)
    {
        var state = FieldFixture();
        var map = content.Maps[new("map.pewter-museum-1f")];
        Assert.IsFalse(map.GetTile(4, y)!.Blocked);
        state = state with {
            World = state.World with { Player = new(map.Id, 4, y, map.GetTile(4, y)!.Elevation, Direction.North) },
            Actors = content.MapDefinition(map.Id).Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag,
                    !state.Script.Flags.GetValueOrDefault(a.HideFlag), false, a.X, a.Y), StringComparer.Ordinal),
            Script = StoryState.Empty with { Flags = state.Script.Flags,
                Variables = state.Script.Variables, EffectSequence = state.Script.EffectSequence },
            ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapLayout = null,
            MapEdits = null, EntryPrograms = [], Dialogue = null, Motions = [],
            IsSurfing = false, IsCycling = false, FlashLevel = 0
        };
        return CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
    }

    [TestMethod]
    [DataRow(5, 142, "aerodactyl-fossil", "AerodactylFossil")]
    [DataRow(8, 141, "kabutops-fossil", "KabutopsFossil")]
    public void OriginalExhibitOpensClosesAndReopensWithoutRegisteringPokemon(int y, int species, string asset, string text)
    {
        var state = AtExhibit(y);
        Assert.IsNull(validator.ValidatePlayerSave(state), "controlled museum entry save");
        var session = CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
        var seen = session.State.SeenSpecies;
        var caught = session.State.CaughtSpecies;
        var rng = session.State.World.Rng;
        string party = CanonicalJson.Hash(session.State.Party);
        string inventory = CanonicalJson.Hash(session.State.Inventory);
        for (int repeat = 0; repeat < 2; repeat++)
        {
            Send(session, new("interact"));
            Assert.AreEqual(new CampaignMonPicture(species, 10, 3) { AssetId = asset }, session.State.MonPicture);
            Assert.IsTrue(session.State.Script.Locked);
            Assert.AreEqual("PewterCity_Museum_1F_Text_" + text, session.State.Dialogue?.TextSymbol);
            Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.State));
            string busy = CanonicalJson.Hash(session.State);
            var rejected = session.Execute("museum-walk:" + repeat, session.Revision, new("move", Direction: Direction.East));
            Assert.AreEqual(CommandStatus.Rejected, rejected.Status);
            Assert.AreEqual(busy, CanonicalJson.Hash(session.State));
            for (int n = 0; n < 20 && !session.State.Script.Completed; n++) Send(session, new("ack"));
            Assert.IsTrue(session.State.Script.Completed);
            Assert.IsFalse(session.State.Script.Locked);
            Assert.IsNull(session.State.MonPicture);
            Assert.IsNull(session.State.Dialogue);
            Assert.IsNull(session.State.Script.Wait);
            Assert.IsTrue(seen.SetEquals(session.State.SeenSpecies), "fossil artwork does not register seen");
            Assert.IsTrue(caught.SetEquals(session.State.CaughtSpecies), "fossil artwork does not register caught");
            Assert.AreEqual(rng, session.State.World.Rng);
            Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
            Assert.AreEqual(inventory, CanonicalJson.Hash(session.State.Inventory));
            StrictRoundtrip(session);
        }
        Send(session, new("turn", Direction: Direction.East));
        Assert.AreEqual(Direction.East, session.State.World.Player.Facing);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(25, 10, 3)]
    [DataRow(142, -1, 3)]
    [DataRow(141, 10, 11)]
    public void InvalidNativePictureOperandsFailExplicitly(int species, int x, int y)
    {
        var state = AtExhibit(5);
        state = state with { Script = state.Script with { Variables = state.Script.Variables
            .SetItem("VAR_0x8004", species).SetItem("VAR_0x8005", x).SetItem("VAR_0x8006", y) } };
        var method = typeof(CampaignSession).GetMethod("TryApplyMuseumPictureEffect", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var error = Assert.ThrowsExactly<TargetInvocationException>(() => method.Invoke(validator,
            [state, new StoryEffect(1, "special", ["OpenMuseumFossilPic"], new("controlled-native-test", "Open", 1)), null]));
        Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
        Assert.IsNull(state.MonPicture);
    }
}
