using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.World;

namespace FireRed.Tests.Application.Engineering;

[TestClass, TestCategory("Game")]
public sealed class SavePayloadSizeTests
{
    private const string Rules = "save-projection-r1", Content = "save-projection-c1";

    [TestMethod]
    public void OnlyExactDerivedGettersAreOmittedAndCanonicalStateIsUnchanged()
    {
        var state = Fixture();
        string ordinary = JsonSerializer.Serialize(state), canonical = CanonicalJson.Hash(state);
        var bytes = Encode(state, 7);
        using var document = JsonDocument.Parse(bytes);
        var payload = document.RootElement.GetProperty("Payload");
        Assert.IsFalse(payload.GetProperty("Stats").TryGetProperty("Values", out _));
        Assert.IsFalse(payload.GetProperty("Pokemon").TryGetProperty("IsShiny", out _));
        Assert.IsFalse(payload.GetProperty("Actor").TryGetProperty("Visible", out _));
        // Get-only identifiers are authoritative; same-name unrelated fields are not projections.
        Assert.AreEqual("fixture:map", payload.GetProperty("Map").GetProperty("Value").GetString());
        Assert.AreEqual("fixture:species", payload.GetProperty("Pokemon").GetProperty("Species").GetProperty("Value").GetString());
        Assert.AreEqual(123, payload.GetProperty("Unrelated").GetProperty("Values").GetInt32());
        Assert.IsTrue(payload.GetProperty("Unrelated").GetProperty("IsShiny").GetBoolean());
        Assert.IsTrue(payload.GetProperty("Unrelated").GetProperty("Visible").GetBoolean());
        Assert.AreEqual(CanonicalJson.Hash(payload), document.RootElement.GetProperty("PayloadHash").GetString());
        Assert.AreNotEqual(canonical, document.RootElement.GetProperty("PayloadHash").GetString(),
            "The stored payload checksum must not be confused with the unchanged canonical state hash.");
        var loaded = Decode(bytes);
        Assert.AreEqual(7, loaded.Generation);
        Assert.AreEqual(canonical, CanonicalJson.Hash(loaded.State));
        Assert.AreEqual(ordinary, JsonSerializer.Serialize(state));
        Assert.AreEqual(ordinary, JsonSerializer.Serialize(loaded.State));
        Assert.IsTrue(JsonSerializer.Serialize(state).Contains("\"Values\":", StringComparison.Ordinal));
        Assert.IsTrue(bytes.Length < LegacyEncode(state, 7).Length);
    }

    [TestMethod]
    public void ExistingSchemaThreeDerivedFieldsRemainReadableButTheirBytesAreChecksummed()
    {
        var state = Fixture();
        var legacy = LegacyEncode(state, 7);
        Assert.AreEqual(CanonicalJson.Hash(state), CanonicalJson.Hash(Decode(legacy).State));
        var envelope = JsonNode.Parse(legacy)!.AsObject();
        var payload = envelope["Payload"]!.AsObject();
        payload["Stats"]!["Values"] = new JsonArray(999, 999);
        payload["Pokemon"]!["IsShiny"] = !state.Pokemon.IsShiny;
        payload["Actor"]!["Visible"] = !state.Actor.Visible;
        Assert.ThrowsExactly<InvalidDataException>(() => Decode(CanonicalJson.Encode(envelope)));
        // These getters never were load authority. Even a correctly checksummed old
        // representation reconstructs them solely from the actual stored fields.
        envelope["PayloadHash"] = CanonicalJson.Hash(payload);
        Assert.AreEqual(CanonicalJson.Hash(state), CanonicalJson.Hash(Decode(CanonicalJson.Encode(envelope)).State));
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void RawJsonConversionsPreserveEverySuppliedPayloadField(bool asNode)
    {
        var original = JsonSerializer.SerializeToElement(Fixture());
        byte[] bytes = asNode
            ? SaveCodec.Encode(JsonNode.Parse(original.GetRawText())!, 9, Rules, Content)
            : SaveCodec.Encode(original, 9, Rules, Content);
        var restored = SaveCodec.Decode<JsonElement>(bytes, Rules, Content);
        Assert.AreEqual(CanonicalJson.Hash(original), CanonicalJson.Hash(restored.State));
        Assert.IsTrue(restored.State.GetProperty("Stats").TryGetProperty("Values", out _));
        Assert.IsTrue(restored.State.GetProperty("Pokemon").TryGetProperty("IsShiny", out _));
        Assert.IsTrue(restored.State.GetProperty("Actor").TryGetProperty("Visible", out _));
    }

    [TestMethod]
    public void PrimaryBackupStagingAndCandidateFingerprintsKeepTheirByteContract()
    {
        var storage = new MemoryStorage();
        var old = Fixture();
        storage.Files["slot"] = LegacyEncode(old, 1);
        byte[] original = storage.Files["slot"].ToArray();
        var next = old with { Stats = old.Stats with { Hp = 31 } };
        var service = new SaveService(storage);
        service.Save("slot", next, 2, Rules, Content, Validate);
        CollectionAssert.AreEqual(original, storage.Files["slot.bak"]);
        Assert.IsFalse(storage.Files.ContainsKey("slot.tmp"));
        var candidate = PlayerSaveOperations.LoadVerifiedCandidate<ProjectionState>(storage, "slot", Rules, Content, Validate);
        Assert.AreEqual(CanonicalJson.Hash(next), CanonicalJson.Hash(candidate.Saved.State));
        Assert.AreEqual(storage.Files["slot"].Length + ":" +
            Convert.ToHexStringLower(SHA256.HashData(storage.Files["slot"])), candidate.Fingerprint);

        storage.Files["slot"] = Encode(next with { Stats = next.Stats with { Hp = -1 } }, 3);
        Assert.AreEqual("slot.bak", service.Load<ProjectionState>("slot", Rules, Content, Validate).Source);
        service.Save("slot", next, 4, Rules, Content, Validate);
        CollectionAssert.AreEqual(original, storage.Files["slot.bak"],
            "A checksum-valid but semantically invalid primary must not replace a good backup.");
        byte[] primary = storage.Files["slot"].ToArray();
        storage.SubstituteStage = Encode(old, 999);
        Assert.ThrowsExactly<InvalidDataException>(() => service.Save("slot", next, 5, Rules, Content, Validate));
        CollectionAssert.AreEqual(primary, storage.Files["slot"]);
        CollectionAssert.AreEqual(original, storage.Files["slot.bak"]);
    }

    [TestMethod]
    public void ActualLeagueDurableWriterUsesCanonicalStateHashAfterCompactSave()
    {
        var state = Fixture();
        var storage = new MemoryStorage();
        string requestHash = CanonicalJson.Hash(state);
        PokemonLeagueDurableSave.Write(storage, "slot", state, 17, requestHash, Rules, Content, Validate);
        var saved = Decode(storage.Files["slot"]);
        Assert.AreEqual(17, saved.Generation);
        Assert.AreEqual(requestHash, CanonicalJson.Hash(saved.State));
        using var json = JsonDocument.Parse(storage.Files["slot"]);
        Assert.AreNotEqual(requestHash, json.RootElement.GetProperty("PayloadHash").GetString());
        storage.AfterCommit = () => storage.Files["slot"] = Encode(state with { Stats = SixStats.Zero }, 18);
        Assert.ThrowsExactly<IOException>(() =>
            PokemonLeagueDurableSave.Write(storage, "slot", state, 18, requestHash, Rules, Content, Validate));
    }

    [TestMethod, DataRow("RED", "BLUE"), DataRow("小智", "小茂")]
    public void FreshCurrentCampaignSavesAndContinuesWithoutChangingState(string player, string rival)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog, new(0xfedcba98, player, rival, RngSeed: 17));
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        string before = CanonicalJson.Hash(session.State);
        var storage = new MemoryStorage();
        new SaveService(storage).Save("slot", session.State, session.Revision,
            session.RulesVersion, catalog.ContentVersion, session.ValidatePlayerSave);
        var loaded = new SaveService(storage).Load<CampaignState>("slot",
            session.RulesVersion, catalog.ContentVersion, session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(catalog, loaded.State, loaded.Generation);
        Assert.AreEqual(before, CanonicalJson.Hash(continued.State));
        Assert.AreEqual(session.State.World.Rng, continued.State.World.Rng);
        Assert.AreEqual(player, continued.State.PlayerName);
        Assert.AreEqual(rival, continued.State.RivalName);
        using var json = JsonDocument.Parse(storage.Files["slot"]);
        Assert.AreEqual(3, json.RootElement.GetProperty("SchemaVersion").GetInt32());
    }

    [TestMethod, DataRow(1), DataRow(60), DataRow(420)]
    public void PopulatedStorageSerializationVectorRetainsEverySlotAndIndividual(int occupied)
    {
        // A controlled serialization-capacity vector, not a fabricated campaign or
        // a claim that these individuals were obtained by gameplay.
        var prototype = Fixture().Pokemon;
        var boxes = PokemonStorageState.Empty.Boxes;
        for (int index = 0; index < occupied; index++)
        {
            var pokemon = new PartyPokemon(index + 1, prototype with { Personality = (uint)(index * 71) },
                index % 2 == 0 ? null : "伙伴", "小智", 0, 2, "MAPSEC_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE");
            int box = index / PokemonStorageState.SlotsPerBox, slot = index % PokemonStorageState.SlotsPerBox;
            boxes = boxes.SetItem(box, boxes[box] with { Slots = boxes[box].Slots.SetItem(slot, pokemon) });
        }
        var state = new PokemonStorageState(0, boxes);
        var ordinary = JsonSerializer.SerializeToElement(state);
        var legacy = CanonicalJson.Encode(new SaveEnvelope(3, Rules, Content, "firered", 0, CanonicalJson.Hash(ordinary), ordinary));
        var compact = SaveCodec.Encode(state, 0, Rules, Content);
        var loaded = SaveCodec.Decode<PokemonStorageState>(compact, Rules, Content);
        Assert.AreEqual(CanonicalJson.Hash(state), CanonicalJson.Hash(loaded.State));
        Assert.AreEqual(occupied, loaded.State.Boxes.Sum(box => box.Slots.Count(p => p is not null)));
        Assert.AreEqual(420, loaded.State.Boxes.Sum(box => box.Slots.Length));
        Assert.IsTrue(compact.Length < legacy.Length);
    }

    private static ProjectionState Fixture()
    {
        var stats = new SixStats(20, 21, 22, 23, 24, 25);
        var pokemon = new PokemonInstance(new("fixture:species"), 1, 5, 135, 123, 456, Nature.Hardy,
            stats, SixStats.Zero, new(22, 11, 12, 13, 14, 15), 22, PokemonGender.Male, 0,
            "ABILITY_OVERGROW", 70, [new(new("fixture:move"), 35, 35)]);
        var actor = new CampaignActor("local:1", 1, "fixture:graphics", 3, 4, 3, Direction.South,
            "fixture:movement", "fixture:script", "FLAG_FIXTURE", true, false, 3, 4);
        return new(stats, pokemon, actor, new("fixture:map"), new(123, true, true));
    }
    private static string? Validate(ProjectionState state) => state.Stats.Hp < 0 ? "negative_hp" : null;
    private static byte[] Encode(ProjectionState state, long generation) => SaveCodec.Encode(state, generation, Rules, Content);
    private static LoadedSave<ProjectionState> Decode(byte[] bytes) => SaveCodec.Decode<ProjectionState>(bytes, Rules, Content);
    private static byte[] LegacyEncode(ProjectionState state, long generation)
    {
        var payload = JsonSerializer.SerializeToElement(state);
        return CanonicalJson.Encode(new SaveEnvelope(SaveCodec.CurrentSchema, Rules, Content, "firered",
            generation, CanonicalJson.Hash(payload), payload));
    }
    public sealed record ProjectionState(SixStats Stats, PokemonInstance Pokemon, CampaignActor Actor, MapId Map, SameNamedFields Unrelated);
    public sealed record SameNamedFields(int Values, bool IsShiny, bool Visible);
    private sealed class MemoryStorage : ISaveStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public byte[]? SubstituteStage { get; set; }
        public Action? AfterCommit { get; set; }
        public byte[]? Read(string slot) => Files.GetValueOrDefault(slot);
        public void WriteStaged(string slot, ReadOnlySpan<byte> bytes) => Files[slot + ".tmp"] = SubstituteStage ?? bytes.ToArray();
        public void Commit(string slot, bool preservePrimary)
        {
            if (preservePrimary) Files[slot + ".bak"] = Files[slot];
            Files[slot] = Files[slot + ".tmp"];
            Files.Remove(slot + ".tmp");
            AfterCommit?.Invoke();
        }
    }
}
