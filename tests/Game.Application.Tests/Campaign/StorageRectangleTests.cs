using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class StorageRectangleTests
{
    private CampaignSession session = default!;
    private CampaignPokemonContent content = default!;
    private PartyPokemon first = default!, second = default!, third = default!;
    [TestInitialize]
    public void Initialize()
    {
        session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        content = CurrentCampaignFixture.Catalog.PokemonContent!;
        PartyPokemon Mon(long id)
        {
            var made = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_BULBASAUR"), 5, session.State.TrainerId,
                IvPolicy.Fixed(3), PersonalityPolicy.Fixed((uint)id)), new(0)).Pokemon;
            return new(id, made, "BULBASAUR", session.State.PlayerName, session.State.PlayerGender, 2, "MAPSEC_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_ORAN_BERRY") { Markings = (int)id };
        }
        first = Mon(2); second = Mon(3); third = Mon(4);
    }
    private static PokemonStorageState Put(PokemonStorageState storage, int box, int slot, PartyPokemon mon) =>
        storage with { Boxes = storage.Boxes.SetItem(box, storage.Boxes[box] with { Slots = storage.Boxes[box].Slots.SetItem(slot, mon) }) };
    private PokemonStorageState Source() => Put(Put(PokemonStorageState.Empty, 0, 0, first), 0, 7, second);
    private PokemonStorageGroupResult Grab(PokemonStorageState storage) => PokemonStorageRectangleRules.Grab(content, [], storage, null, new(0, 7, 0), [2, null, null, 3]);

    [TestMethod]
    public void Reversed_rectangle_preserves_holes_and_full_creature_identity_across_boxes()
    {
        var grabbed = Grab(Source());
        Assert.IsTrue(grabbed.Succeeded, grabbed.Diagnostic?.Code);
        Assert.IsNull(grabbed.Storage.Boxes[0].Slots[0]);
        Assert.AreSame(first, grabbed.Cursor!.Cells[0]);
        var destination = Put(grabbed.Storage with { CurrentBox = 1 }, 1, 15, third);
        var placed = PokemonStorageRectangleRules.Place(content, [], destination, grabbed.Cursor, new(1, 14), [2, null, null, 3]);
        Assert.IsTrue(placed.Succeeded, placed.Diagnostic?.Code);
        Assert.IsNull(placed.Cursor);
        Assert.AreSame(first, placed.Storage.Boxes[1].Slots[14]);
        Assert.AreSame(second, placed.Storage.Boxes[1].Slots[21]);
        Assert.AreSame(third, placed.Storage.Boxes[1].Slots[15], "A payload hole must never erase a destination occupant.");
        Assert.AreEqual("ITEM_ORAN_BERRY", placed.Storage.Boxes[1].Slots[21]!.HeldItem);
    }

    [TestMethod]
    public void Occupied_destination_bounds_and_stale_identity_reject_atomically()
    {
        var grabbed = Grab(Source());
        var storage = Put(grabbed.Storage, 0, 20, third);
        foreach (var slot in new[] { -1, 5, 29, 30, 20 })
        {
            var failed = PokemonStorageRectangleRules.Place(content, [], storage, grabbed.Cursor, new(0, slot), [2, null, null, 3]);
            Assert.IsFalse(failed.Succeeded, $"slot {slot}");
            Assert.AreSame(storage, failed.Storage);
            Assert.AreSame(grabbed.Cursor, failed.Cursor);
        }
        var stale = PokemonStorageRectangleRules.Place(content, [], storage, grabbed.Cursor, new(0, 8), [3, null, null, 2]);
        Assert.AreEqual("storage_move_held_identity", stale.Diagnostic!.Code);
        Assert.AreSame(storage, stale.Storage);
        var source = Source();
        var changed = PokemonStorageRectangleRules.Grab(content, [], source, null, new(0, 0, 7), [2, 4, null, 3]);
        Assert.AreSame(source, changed.Storage);
        Assert.AreEqual("storage_move_target_identity", changed.Diagnostic!.Code);
        foreach (int slot in new[] { -1, 30, int.MaxValue })
            Assert.IsFalse(PokemonStorageRectangleRules.Grab(content, [], source, null, new(0, slot, 0), []).Succeeded);
        Assert.IsFalse(PokemonStorageRectangleRules.Grab(content, [], source, null, new(0, 0, 7), default).Succeeded);
        Assert.IsNotNull(PokemonStorageRectangleRules.Validate(content, [], grabbed.Storage, new(new(0, 0, 7), default)));
        Assert.IsNotNull(PokemonStorageRectangleRules.Validate(content, [], grabbed.Storage, new(new(0, 0, 7), [null, null, null, null])));
    }

    [TestMethod]
    public void Full_box_has_no_partial_placement_and_full_rectangle_can_return_to_source()
    {
        var grabbed = Grab(Source());
        var full = grabbed.Storage with { CurrentBox = 1 };
        for (int i = 0; i < 30; i++) full = Put(full, 1, i, third with { InstanceId = 10 + i });
        var blocked = PokemonStorageRectangleRules.Place(content, [], full, grabbed.Cursor, new(1, 0), [2, null, null, 3]);
        Assert.AreSame(full, blocked.Storage);
        Assert.AreEqual("storage_move_slot_occupied", blocked.Diagnostic!.Code);
        var original = Source();
        var ids = original.Boxes[0].Slots.Select(p => p?.InstanceId).ToImmutableArray();
        var all = PokemonStorageRectangleRules.Grab(content, [], original, null, new(0, 0, 29), ids);
        Assert.IsTrue(all.Succeeded);
        var restored = PokemonStorageRectangleRules.Place(content, [], all.Storage, all.Cursor, new(0, 0), ids);
        Assert.IsTrue(restored.Succeeded);
        CollectionAssert.AreEqual(original.Boxes[0].Slots.ToArray(), restored.Storage.Boxes[0].Slots.ToArray());
    }

    [TestMethod]
    public void Held_group_is_owned_blocks_cancel_exit_and_saves_and_can_place()
    {
        var basis = CurrentCampaignFixture.Checkpoint.State;
        var source = basis.Storage! with { CurrentBox = 0 };
        var rectangle = new PokemonStorageRectangle(0, 0, 7);
        var ids = Enumerable.Range(0, 4).Select(i => source.Boxes[0].Slots[rectangle.Slot(i)]?.InstanceId).ToImmutableArray();
        var grabbed = PokemonStorageRectangleRules.Grab(content, basis.Party, source, null, rectangle, ids);
        Assert.IsTrue(grabbed.Succeeded, grabbed.Diagnostic?.Code);
        var state = basis with { Storage = grabbed.Storage with { CurrentBox = 2 },
            Pc = new(CampaignPcView.Move) { GroupCursor = grabbed.Cursor } };
        object? Invoke(string name, params object[] args) => typeof(CampaignSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, args);
        var owned = (ImmutableArray<PartyPokemon>)Invoke("OwnedPokemon", state)!;
        foreach (var id in ids.OfType<long>()) Assert.AreEqual(1, owned.Count(p => p.InstanceId == id));
        Assert.AreEqual("save_event_active", session.ValidatePlayerSave(state));
        foreach (var kind in new[] { "pc_back", "storage_move_close", "storage_move_hide_party", "storage_release_prompt", "storage_group_cancel" })
        {
            var decision = (Decision<CampaignState>)Invoke("DecideStorageGroupMove", state, new CampaignCommand(kind))!;
            Assert.AreEqual(CommandStatus.Rejected, decision.Status, decision.Error);
            Assert.AreSame(state, decision.State);
        }
        var placed = (Decision<CampaignState>)Invoke("DecideStorageGroupMove", state, new CampaignCommand("storage_group_place") {
            StorageLocation = new(2, 14), StorageIdentities = ids })!;
        Assert.AreEqual(CommandStatus.Accepted, placed.Status, placed.Error);
        Assert.IsNull(placed.State.Pc!.GroupCursor);
        Assert.AreSame(grabbed.Cursor!.Cells[0], placed.State.Storage!.Boxes[2].Slots[14]);
        Assert.IsNotNull(Invoke("ValidateStorageState", state with { Party = [grabbed.Cursor!.Cells[0]!] }), "Held IDs cannot also belong to party.");
        Assert.IsNotNull(Invoke("ValidateStorageState", state with { Pc = state.Pc with { View = CampaignPcView.StorageMenu } }));
    }
    [TestMethod]
    public void Public_pc_commands_move_group_then_close_and_strict_save_continue()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var catalog = CurrentCampaignFixture.Catalog;
        var basis = CurrentCampaignFixture.Checkpoint.State;
        var map = new Game.Domain.Ids.MapId("map.viridian-pokemon-center-1f");
        basis = basis with {
            World = basis.World with { Player = new(map, 7, 5, catalog.Maps[map].GetTile(7, 5)!.Elevation, Game.Domain.World.Direction.North) },
            Script = Game.Domain.Events.StoryState.Empty with { Flags = basis.Script.Flags, Variables = basis.Script.Variables, EffectSequence = basis.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [], PresentationTicks = 0, PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null
        };
        CampaignState Call(string method, params object?[] args) => (CampaignState)typeof(CampaignSession).GetMethod(method, flags)!.Invoke(session, args)!;
        void Install(CampaignState state) => typeof(CampaignSession).GetField("_commands", flags)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, catalog.ContentVersion));
        void Send(CampaignCommand command)
        {
            var revision = session.Revision;
            var result = session.Execute("rectangle:" + revision, revision, command);
            Assert.AreEqual(CommandStatus.Accepted, result.Status, command.Kind + ": " + result.Error);
        }
        void DrainUntil(Func<bool> done)
        {
            for (int i = 0; i < 40 && !done(); i++) Send(new("ack"));
            Assert.IsTrue(done(), "Native PC callback did not reach expected state.");
        }
        Install(Call("Pump", Call("EnterMap", basis, null, Game.Domain.Roaming.RoamerTransition.Warp)));
        DrainUntil(() => session.State.Script.Completed && session.State.EntryPrograms.IsEmpty);
        Assert.IsNull(session.ValidatePlayerSave(session.State), "controlled pre-PC save");
        // Only entry setup invokes the actual native script; every menu/move/exit is a public command.
        Install(Call("Pump", Call("StartStory", session.State, "EventScript_PC")));
        DrainUntil(() => session.State.Pc?.View == CampaignPcView.Main);
        Send(new("pc_select", Choice: 0));
        DrainUntil(() => session.State.Pc?.View == CampaignPcView.StorageMenu);
        Send(new("storage_select", Choice: 2));
        Send(new("storage_box", Choice: 0));
        var rectangle = new PokemonStorageRectangle(0, 0, 7);
        Assert.IsNotNull(session.State.Storage!.Boxes[0].Slots[0], "fixture needs occupied anchor");
        var ids = Enumerable.Range(0, 4).Select(i => session.State.Storage.Boxes[0].Slots[rectangle.Slot(i)]?.InstanceId).ToImmutableArray();
        var originals = Enumerable.Range(0, 4).Select(i => session.State.Storage.Boxes[0].Slots[rectangle.Slot(i)]).ToArray();
        Send(new("storage_group_grab") { StorageRectangle = rectangle, StorageIdentities = ids });
        var holding = session.State;
        Assert.AreEqual("save_event_active", session.ValidatePlayerSave(holding));
        var cancel = session.Execute("rectangle:cancel", session.Revision, new("pc_back"));
        Assert.AreEqual(CommandStatus.Rejected, cancel.Status);
        Assert.AreSame(holding, session.State);
        Send(new("storage_box", Choice: 2));
        Send(new("storage_group_place") { StorageLocation = new(2, 14), StorageIdentities = ids });
        for (int i = 0; i < 4; i++) Assert.AreEqual(originals[i], session.State.Storage!.Boxes[2].Slots[14 + i / 2 * 6 + i % 2]);
        Send(new("storage_move_close"));
        Send(new("pc_back"));
        DrainUntil(() => session.State.Pc?.View == CampaignPcView.Main);
        Send(new("pc_back"));
        DrainUntil(() => session.State.Pc is null && session.State.Script.Completed);
        Assert.IsNull(session.ValidatePlayerSave(session.State), "strict save after native PC exit");
        var restored = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion),
            session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(catalog, restored.State, restored.Generation);
        Assert.IsNull(continued.State.Pc);
        Assert.AreEqual(ids[0], continued.State.Storage!.Boxes[2].Slots[14]!.InstanceId);
    }

}
