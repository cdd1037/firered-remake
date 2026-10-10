using System.Text.Json;
namespace Game.Application;
public sealed record SaveEnvelope(int SchemaVersion, string RulesVersion, string ContentVersion, string GameEdition, long Generation, string PayloadHash, JsonElement Payload);
public sealed record LoadedSave<T>(T State, long Generation, string Source);
public static class SaveCodec
{
    public const int CurrentSchema = 1;
    // CanonicalJson sorts every key, including polymorphic discriminators such as
    // "kind". Decode that unchanged canonical representation in any property order.
    // Player-save admission still belongs to the supplied semantic validator.
    private static readonly JsonSerializerOptions PayloadOptions = new() { AllowOutOfOrderMetadataProperties = true };
    public static byte[] Encode<T>(T state, long generation, string rules, string content)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
        var payload = JsonSerializer.SerializeToElement(state);
        return CanonicalJson.Encode(new SaveEnvelope(CurrentSchema, rules, content, "firered", generation, CanonicalJson.Hash(payload), payload));
    }
    public static LoadedSave<T> Decode<T>(ReadOnlySpan<byte> bytes, string rules, string content, string source = "memory", Func<T, string?>? validate = null)
    {
        try { return DecodeCore(bytes, rules, content, source, validate); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        { throw new InvalidDataException("save_decode_invalid: " + ex.GetType().Name, ex); }
    }
    private static LoadedSave<T> DecodeCore<T>(ReadOnlySpan<byte> bytes, string rules, string content, string source, Func<T, string?>? validate)
    {
        ValidateEnvelopeFields(bytes);
        var envelope = JsonSerializer.Deserialize<SaveEnvelope>(bytes) ?? throw new InvalidDataException("save_empty");
        if (envelope.SchemaVersion != CurrentSchema) throw new InvalidDataException("save_schema_unsupported");
        if (envelope.RulesVersion != rules || envelope.ContentVersion != content || envelope.GameEdition != "firered") throw new InvalidDataException("save_version_mismatch");
        if (envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) throw new InvalidDataException("save_payload_empty");
        if (envelope.Generation < 0 || envelope.PayloadHash != CanonicalJson.Hash(envelope.Payload)) throw new InvalidDataException("save_checksum_mismatch");
        var state = envelope.Payload.Deserialize<T>(PayloadOptions) ?? throw new InvalidDataException("save_payload_empty");
        var error = validate?.Invoke(state);
        if (error is not null) throw new InvalidDataException("save_semantic_invalid: " + error);
        return new(state, envelope.Generation, source);
    }
    private static void ValidateEnvelopeFields(ReadOnlySpan<byte> bytes)
    {
        // Validate the envelope before constructor defaults or ignored properties can
        // hide damaged metadata. Payload fields retain their own compatibility rules.
        var reader = new Utf8JsonReader(bytes);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new InvalidDataException("save_envelope_invalid");
        int seen = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (seen != 0x7f) throw new InvalidDataException("save_envelope_field_missing");
                if (reader.Read()) throw new InvalidDataException("save_envelope_invalid");
                return;
            }
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new InvalidDataException("save_envelope_invalid");
            // ValueTextEquals compares decoded names, including JSON escapes.
            int field = reader.ValueTextEquals("SchemaVersion"u8) ? 1
                : reader.ValueTextEquals("RulesVersion"u8) ? 2
                : reader.ValueTextEquals("ContentVersion"u8) ? 4
                : reader.ValueTextEquals("GameEdition"u8) ? 8
                : reader.ValueTextEquals("Generation"u8) ? 16
                : reader.ValueTextEquals("PayloadHash"u8) ? 32
                : reader.ValueTextEquals("Payload"u8) ? 64
                : throw new InvalidDataException("save_envelope_field_unknown");
            if ((seen & field) != 0) throw new InvalidDataException("save_envelope_field_duplicate");
            seen |= field;
            if (!reader.Read()) throw new InvalidDataException("save_envelope_invalid");
            reader.Skip();
        }
        throw new InvalidDataException("save_envelope_invalid");
    }
}
/// <summary>Platform adapters provide same-directory writes and replacement. No IO in Domain or this codec.</summary>
public interface ISaveStorage
{
    byte[]? Read(string slot);
    void WriteStaged(string slot, byte[] bytes);
    void Commit(string slot, bool preservePrimary);
}
public sealed class SaveService(ISaveStorage storage)
{
    // A game has one in-process save writer; serialize all service instances and slots.
    private static readonly object Transactions = new();
    public void Save<T>(string slot, T state, long generation, string rules, string content, Func<T, string?>? validate = null)
    {
        lock (Transactions) SaveExclusive(slot, state, generation, rules, content, validate);
    }
    private void SaveExclusive<T>(string slot, T state, long generation, string rules, string content, Func<T, string?>? validate)
    {
        var error = validate?.Invoke(state);
        if (error is not null) throw new InvalidDataException("save_semantic_invalid: " + error);
        var bytes = SaveCodec.Encode(state, generation, rules, content);
        _ = SaveCodec.Decode<T>(bytes, rules, content, validate: validate);
        storage.WriteStaged(slot, bytes);
        var staged = storage.Read(slot + ".tmp") ?? throw new IOException("save_stage_missing");
        if (!staged.AsSpan().SequenceEqual(bytes)) throw new InvalidDataException("save_stage_changed");
        _ = SaveCodec.Decode<T>(staged, rules, content, validate: validate);
        // Storage backs up the last verified primary only. Keep that contract explicit.
        bool preserve = false;
        try
        {
            var current = storage.Read(slot);
            if (current is not null)
            { _ = SaveCodec.Decode<T>(current, rules, content, validate: validate); preserve = true; }
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        { /* Invalid/oversized primary data must not replace a good backup. I/O failures still abort the commit. */ }
        storage.Commit(slot, preserve);
    }
    public LoadedSave<T> Load<T>(string slot, string rules, string content, Func<T, string?>? validate = null)
    {
        lock (Transactions) return LoadExclusive(slot, rules, content, validate);
    }
    private LoadedSave<T> LoadExclusive<T>(string slot, string rules, string content, Func<T, string?>? validate)
    {
        var errors = new List<string>();
        foreach (var path in new[] { slot, slot + ".bak" })
        {
            try
            {
                var bytes = storage.Read(path);
                if (bytes is null) { errors.Add(path + ":missing"); continue; }
                return SaveCodec.Decode<T>(bytes, rules, content, path, validate);
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException) { errors.Add(path + ":" + ex.Message); }
        }
        throw new InvalidDataException("save_unrecoverable: " + string.Join(";", errors));
    }
}
