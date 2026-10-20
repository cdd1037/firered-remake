using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class SharedFieldScriptPrimitiveTests
{
    private static CampaignSession At(string mapSymbol, int x, int y, Direction facing, int gender = 0, int money = 3000)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog); var state = session.State;
        var id = catalog.MapFromSourceSymbol(mapSymbol);
        state = state with {
            World = state.World with { Player = new(id, x, y, catalog.Maps[id].GetTile(x, y)!.Elevation, facing) },
            Actors = catalog.MapDefinition(id).Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag,
                    !state.Script.Flags.GetValueOrDefault(a.HideFlag), false, a.X, a.Y), StringComparer.Ordinal),
            Script = StoryState.Empty with { Flags = state.Script.Flags.Remove("FLAG_BEAT_RIVAL_IN_OAKS_LAB"),
                Variables = state.Script.Variables.SetItem("VAR_MAP_SCENE_PALLET_TOWN_OAK", 1).SetItem("VAR_FACING", 99) },
            PlayerGender = gender, Money = money, EntryPrograms = [], ArrivalFacing = null,
            Dialogue = null, Motions = [], PresentationTicks = 0, PresentationEffects = [],
            ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapLayout = null, MapEdits = null
        };
        SetState(session, state);
        return session;
    }
    private static void SetState(CampaignSession session, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion, session.Revision));
    private static void Send(CampaignSession session, CampaignCommand command)
    {
        var result = session.Execute("shared-field:" + session.Revision, session.Revision, command);
        Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
    }
    private static void Finish(CampaignSession session, int choice = 1)
    {
        for (int i = 0; i < 100 && !session.State.Script.Completed; i++) Send(session, new("ack", Choice: choice));
        Assert.IsTrue(session.State.Script.Completed, "bounded script must finish");
        Assert.IsNull(session.State.Script.Wait);
    }
    private static (bool Handled, CampaignState State) Effect(CampaignSession session, string method, string opcode,
        string[] args, string path = "data/maps/PalletTown/scripts.inc")
    {
        object?[] parameters = [session.State, new StoryEffect(1, opcode, [..args], new(path, "ControlledPrimitive", 1)), null];
        bool handled = (bool)typeof(CampaignSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(session, parameters)!;
        return (handled, (CampaignState)parameters[2]!);
    }

    [TestMethod]
    [DataRow(0, "AllBoysLeaveOakLookingForYou")]
    [DataRow(1, "AllGirlsLeaveOakLookingForYou")]
    public void MomDialogueUsesPlayerGenderAndFinishes(int gender, string text)
    {
        var session = At("MAP_PALLET_TOWN_PLAYERS_HOUSE_1F", 8, 5, Direction.North, gender);
        Send(session, new("interact"));
        Assert.AreEqual("PalletTown_PlayersHouse_1F_Text_" + text, session.State.Dialogue?.TextSymbol);
        Assert.AreEqual(gender, session.State.Script.Variables["VAR_RESULT"]);
        Finish(session);
        Assert.AreEqual(Direction.West, session.State.Actors["LOCALID_MOM"].Facing);
    }

    [TestMethod]
    [DataRow(0, "MovieOnTVFourBoysOnRailroad")]
    [DataRow(1, "MovieOnTVGirlOnBrickRoad")]
    public void TelevisionInitializesFacingWithoutAnEarlierNpcInteraction(int gender, string text)
    {
        var session = At("MAP_PALLET_TOWN_PLAYERS_HOUSE_1F", 6, 2, Direction.North, gender);
        Send(session, new("interact"));
        Assert.AreEqual(2, session.State.Script.Variables["VAR_FACING"]);
        Assert.AreEqual("PalletTown_PlayersHouse_1F_Text_" + text, session.State.Dialogue?.TextSymbol);
        Finish(session);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)]
    public void TelevisionWrongSideRetainsItsSourceBranch(int gender)
    {
        var session = At("MAP_PALLET_TOWN_PLAYERS_HOUSE_1F", 5, 1, Direction.East, gender);
        Send(session, new("interact"));
        Assert.AreEqual(4, session.State.Script.Variables["VAR_FACING"]);
        Assert.AreEqual("PalletTown_PlayersHouse_1F_Text_OopsWrongSide", session.State.Dialogue?.TextSymbol);
        Finish(session);
    }

    [TestMethod]
    [DataRow(Direction.East, 4, 6)] [DataRow(Direction.West, 6, 4)]
    public void PalletSignLadyCopiesHerMovedCoordinates(Direction facing, int playerX, int expectedX)
    {
        var session = At("MAP_PALLET_TOWN", playerX, 15, facing);
        var actor = session.State.Actors["LOCALID_PALLET_SIGN_LADY"];
        SetState(session, session.State with { Actors = session.State.Actors.SetItem(actor.LocalId,
            actor with { X = 5, Y = 15, TemplateX = 5, TemplateY = 15, Active = true }) });
        Send(session, new("interact")); Finish(session);
        actor = session.State.Actors[actor.LocalId];
        Assert.AreEqual((expectedX, 15), (actor.X, actor.Y));
        Assert.AreEqual((actor.X, actor.Y), (actor.TemplateX, actor.TemplateY));
        Assert.IsTrue(session.State.Script.Flags["FLAG_TEMP_2"]);
    }

    [TestMethod]
    [DataRow(3000, 1, 2950, 1)] [DataRow(49, 1, 49, 0)] [DataRow(3000, 0, 3000, 0)]
    public void MuseumEntranceKeepsTicketPaymentAndRefusalBranches(int money, int choice, int expectedMoney, int admitted)
    {
        var session = At("MAP_PEWTER_CITY_MUSEUM_1F", 12, 6, Direction.North, money: money);
        Send(session, new("move", Direction.North));
        for (int i = 0; i < 20 && session.State.MoneyBox is null; i++) Send(session, new("ack"));
        Assert.AreEqual(money, session.State.MoneyBox?.Amount);
        Finish(session, choice);
        Assert.AreEqual(expectedMoney, session.State.Money);
        Assert.AreEqual(admitted, session.State.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PEWTER_CITY_MUSEUM_1F"));
        Assert.IsNull(session.State.MoneyBox);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)]
    public void SharedGenderPrimitiveHasNoNpcOwnership(int gender)
    {
        var session = At("MAP_PALLET_TOWN_PLAYERS_HOUSE_1F", 6, 2, Direction.North, gender);
        var result = Effect(session, "TryApplySharedFieldScriptEffect", "checkplayergender", []);
        Assert.IsTrue(result.Handled); Assert.AreEqual(gender, result.State.Script.Variables["VAR_RESULT"]);
        Assert.AreEqual(session.State.World, result.State.World);
    }

    [TestMethod]
    [DataRow(-1)] [DataRow(2)]
    public void SharedGenderRejectsInvalidData(int gender)
    {
        var session = At("MAP_PALLET_TOWN_PLAYERS_HOUSE_1F", 6, 2, Direction.North, gender);
        var error = Assert.ThrowsExactly<TargetInvocationException>(() => Effect(session, "TryApplySharedFieldScriptEffect", "checkplayergender", []));
        Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
    }

    [TestMethod]
    [DataRow(true)] [DataRow(false)]
    public void SharedActorCopyUsesOnlyLiveCoordinates(bool active)
    {
        var session = At("MAP_PALLET_TOWN", 4, 15, Direction.East);
        var actor = session.State.Actors["LOCALID_PALLET_SIGN_LADY"] with { X = 8, Y = 9, Active = active };
        SetState(session, session.State with { Actors = session.State.Actors.SetItem(actor.LocalId, actor) });
        var result = Effect(session, "TryApplySharedFieldScriptEffect", "copyobjectxytoperm", [actor.SourceLocalId.ToString()]);
        var copied = result.State.Actors[actor.LocalId];
        Assert.AreEqual(active ? (8, 9) : (actor.TemplateX, actor.TemplateY), (copied.TemplateX, copied.TemplateY));
    }

    [TestMethod]
    public void SharedMoneyRemovalSaturatesLikeTheSource()
    {
        var session = At("MAP_PEWTER_CITY_MUSEUM_1F", 12, 6, Direction.North, money: 49);
        Assert.AreEqual(0, Effect(session, "TryApplySharedFieldScriptEffect", "removemoney", ["50"]).State.Money);
        Assert.AreEqual(49, Effect(session, "TryApplySharedFieldScriptEffect", "removemoney", ["50", "1"]).State.Money);
    }

    [TestMethod]
    [DataRow("checkplayergender")] [DataRow("showmoneybox")]
    public void MagikarpDoesNotClaimAnotherScriptsSharedPrimitive(string opcode)
    {
        var session = At("MAP_PALLET_TOWN_PLAYERS_HOUSE_1F", 6, 2, Direction.North);
        Assert.IsFalse(Effect(session, "TryApplyMagikarpEffect", opcode, opcode == "showmoneybox" ? ["0", "0"] : []).Handled);
    }

    [TestMethod]
    public void MtMoonDoesNotClaimAnotherScriptsActorCopy()
    {
        var session = At("MAP_PALLET_TOWN", 4, 15, Direction.East);
        Assert.IsFalse(Effect(session, "TryApplyMtMoonEffect", "copyobjectxytoperm", ["LOCALID_PALLET_SIGN_LADY"]).Handled);
    }
    [TestMethod]
    [DataRow("checkmoney", "499", "magikarp_price_invalid")]
    [DataRow("removemoney", "500", "magikarp_payment_context_invalid")]
    public void SalesmanRetainsItsPriceAndDeliveredGiftGuards(string opcode, string amount, string expected)
    {
        var session = At("MAP_ROUTE4_POKEMON_CENTER_1F", 5, 5, Direction.North);
        var salesman = session.State.Actors.Values.Single(a => a.ScriptId == "Route4_PokemonCenter_1F_EventScript_MagikarpSalesman");
        SetState(session, session.State with { LastTalked = salesman.LocalId });
        var error = Assert.ThrowsExactly<TargetInvocationException>(() => Effect(session, "TryApplyMagikarpEffect", opcode,
            [amount], "data/maps/Route4_PokemonCenter_1F/scripts.inc"));
        Assert.AreEqual(expected, error.InnerException?.Message);
    }

    [TestMethod]
    public void SharedGenderRejectsUnexpectedOperands()
    {
        var session = At("MAP_PALLET_TOWN_PLAYERS_HOUSE_1F", 6, 2, Direction.North);
        var error = Assert.ThrowsExactly<TargetInvocationException>(() => Effect(session, "TryApplySharedFieldScriptEffect", "checkplayergender", ["0"]));
        Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
    }
    [TestMethod]
    [DataRow(7, 4, Direction.West, 0x86)]
    [DataRow(5, 4, Direction.East, 0x86)]
    [DataRow(6, 3, Direction.South, 0x86)]
    [DataRow(4, 2, Direction.North, 0x81)]
    [DataRow(3, 2, Direction.North, 0x8b)]
    public void BedroomSolidFurnitureBumpsWithoutFaultDialogueStepOrRng(int x, int y, Direction direction, int behavior)
    {
        var session = At("MAP_PALLET_TOWN_PLAYERS_HOUSE_2F", x, y, direction);
        var (dx, dy) = direction switch { Direction.North => (0, -1), Direction.South => (0, 1), Direction.East => (1, 0), _ => (-1, 0) };
        var map = CurrentCampaignFixture.Catalog.Maps[session.State.World.Player.MapId];
        Assert.AreEqual(behavior, map.SourceBehaviorAt(x + dx, y + dy));
        Assert.IsTrue(map.GetTile(x + dx, y + dy)!.Blocked);
        var before = session.State;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            Send(session, new("move", direction));
            Assert.AreEqual(before.World.Player, session.State.World.Player);
            Assert.AreEqual(before.World.StepCount, session.State.World.StepCount);
            Assert.AreEqual(before.World.Rng, session.State.World.Rng);
            Assert.IsNull(session.State.Dialogue); Assert.IsTrue(session.State.Script.Completed);
            Assert.IsNull(session.State.Script.Wait);
        }
        var retreat = direction switch { Direction.North => Direction.South, Direction.South => Direction.North,
            Direction.East => Direction.West, _ => Direction.East };
        Send(session, new("move", retreat));
        Assert.AreEqual((x - dx, y - dy), (session.State.World.Player.X, session.State.World.Player.Y));
        Assert.AreEqual(before.World.StepCount + 1, session.State.World.StepCount);
    }
}
