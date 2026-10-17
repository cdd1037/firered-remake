using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Safari;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled delivery-boundary fixtures, not a natural acquisition playthrough.</summary>
[TestClass, TestCategory("Game")]
public sealed class BoxDeliveryDialogueTests : WildBattleFixture
{
    private CampaignState Boxes(CampaignState state, bool rollover, bool renamed)
    {
        var storage = PokemonStorageState.Empty;
        var lead = state.Party[0];
        long id = state.NextPokemonId;
        var party = Enumerable.Range(0, 6).Select(_ => lead with { InstanceId = id++ }).ToImmutableArray();
        if (rollover)
        {
            // Boxed creatures require recalculated stats and restored HP/PP; use the real
            // storage conversion rather than putting a party stat-cache projection in a box.
            for (int slot = 0; slot < PokemonStorageState.SlotsPerBox; slot++)
            {
                var received = PokemonStorageRules.Receive(content.PokemonContent, party, storage, lead with { InstanceId = id++ });
                Assert.IsTrue(received.Succeeded, received.Diagnostic?.ToString());
                storage = received.Storage;
            }
        }
        if (renamed)
            storage = storage with { Boxes = storage.Boxes.SetItem(0, storage.Boxes[0] with { Name = "HOME" })
                .SetItem(1, storage.Boxes[1] with { Name = "NEW" }) };
        return state with { Party = party, Storage = storage, NextPokemonId = id,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_PC_BOX_TO_SEND_MON", 0),
                Flags = state.Script.Flags.SetItem("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE", false) } };
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public void Ordinary_and_Safari_delivery_use_saved_destination_and_previous_box_names(bool safari, bool rollover, bool renamed)
    {
        var session = StartField();
        var state = Boxes(session.State, rollover, renamed);
        var target = state.WildBattle!.Frame!.Party(Game.Domain.Battle.BattleSide.Opponent).Members[0].Pokemon;
        var origin = new CampaignWildOrigin("MAPSEC_KANTO_SAFARI_ZONE", target.Level, "CAUGHT");
        var capture = new CampaignPendingCapture(target, "ITEM_NONE", 0, origin, false, CampaignCaptureStage.NicknameChoice)
            { Ball = safari ? "ITEM_SAFARI_BALL" : "ITEM_POKE_BALL" };
        if (safari)
            state = state with { WildBattle = null, SafariBattle = new("box-dialogue-safari",
                new(target, 29, 10, 10, 0, 0, 1, SafariBattleOutcome.Caught, state.World.Rng),
                "ITEM_NONE", origin, CampaignBattlePhase.Results, [], [], 0, []) { Capture = capture } };
        else state = state with { WildBattle = state.WildBattle with { Capture = capture } };
        Assert.IsNull(PokemonStorageValidator.Validate(content.PokemonContent, state.Party, state.Storage, state.NextPokemonId),
            "controlled delivery fixture must be a valid storage owner");
        string before = CanonicalJson.Hash(state);
        var method = typeof(CampaignSession).GetMethod(safari ? "DeliverSafariCapture" : "DeliverCapturedPokemon", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var next = (CampaignState)method.Invoke(session, [state, "CAUGHT"])!;
        Assert.AreEqual(before, CanonicalJson.Hash(state), "input owners are immutable");
        Assert.AreEqual(renamed ? (rollover ? "NEW" : "HOME") : (rollover ? "BOX2" : "BOX1"), next.TextBuffers["STR_VAR_1"]);
        Assert.AreEqual(renamed ? "HOME" : "BOX1", next.TextBuffers["STR_VAR_3"]);
        Assert.AreEqual("CAUGHT", next.TextBuffers["STR_VAR_2"]);
        var delivered = safari ? next.SafariBattle!.Capture! : next.WildBattle!.Capture!;
        Assert.AreEqual(CampaignCaptureStage.SentToPc, delivered.Stage);
        Assert.AreEqual(rollover ? 1 : 0, delivered.Address!.BoxIndex);
        Assert.AreEqual(rollover, delivered.MessageSymbol!.EndsWith("BoxFull", StringComparison.Ordinal));
        Assert.AreEqual(state.NextPokemonId + 1, next.NextPokemonId);
        Assert.AreEqual(state.NextPokemonId, next.Storage!.Boxes[rollover ? 1 : 0].Slots[0]!.InstanceId);
        Assert.ThrowsExactly<TargetInvocationException>(() => method.Invoke(session, [next, "CAUGHT"]), "delivery cannot commit twice");
    }

    [TestMethod]
    [DataRow("STR_VAR_1", 1, "NEW")]
    [DataRow("STR_VAR_3", 0, "HOME")]
    public void Script_gift_box_buffers_use_saved_names(string buffer, int box, string expected)
    {
        var session = CampaignSession.CreateCurrent(content);
        var state = Boxes(checkpoint.State, true, true);
        var map = content.MapDefinition(new("map.celadon-city-condominiums-roof-room"));
        var actor = map.Actors.Single(a => a.ScriptId == "CeladonCity_Condominiums_RoofRoom_EventScript_EeveeBall");
        state = state with { World = state.World with { Player = state.World.Player with { MapId = new("map.celadon-city-condominiums-roof-room") } },
            LastTalked = actor.LocalId, Actors = state.Actors.SetItem(actor.LocalId, new(actor.LocalId, actor.SourceLocalId,
                actor.GraphicsId, actor.X, actor.Y, actor.Elevation, Game.Domain.World.Direction.South, actor.MovementType,
                actor.ScriptId, actor.HideFlag, true, false, actor.X, actor.Y)),
            GiftDelivery = new(new(1, 0), 0), EeveeGift = new(state.NextPokemonId, 7, new(1, 1, 1, 1, 1, 1)) };
        var source = content.FindProgram("EventScript_TransferredToPC")!;
        var instruction = source.Instructions.First(i => i.Opcode == "bufferboxname");
        object?[] args = [state, new StoryEffect(0, "bufferboxname", [buffer, box.ToString()], instruction.Source), null];
        var handled = (bool)typeof(CampaignSession).GetMethod("TryApplySharedScriptGiftEffect", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, args)!;
        Assert.IsTrue(handled);
        Assert.AreEqual(expected, ((CampaignState)args[2]!).TextBuffers[buffer]);
    }
}
