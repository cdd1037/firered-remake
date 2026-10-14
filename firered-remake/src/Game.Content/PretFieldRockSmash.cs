using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated compact declarations, independent of regional map art and current campaign activation.</summary>
public static class PretFieldRockSmash
{
    public const string DocumentSha256 = "a28de19426ffbe058db29f552be7bbb2d94f6b46ba2caecd8d931406a2c6872a";
    public static FieldRockSmashSourceDocument LoadDocument(string root)
    {
        using var stream = File.OpenRead(Path.Combine(root, "content", "generated", "field-rock-smash", "field.json"));
        if (stream.Length is < 1 or > 262144) throw new InvalidDataException("field_rock_smash_file_size");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != DocumentSha256)
            throw new InvalidDataException("field_rock_smash_source_bytes");
        return JsonSerializer.Deserialize<FieldRockSmashSourceDocument>(bytes, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 16
        }) ?? throw new InvalidDataException("field_rock_smash_document");
    }
    public static FieldRockSmashCatalog Load(string root) => FieldRockSmashCatalog.Authenticate(LoadDocument(root));
}
