using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;

namespace Game.Content;

public sealed record SourceBattleItemDocument(string SourceCommit, string SemanticSha256, ImmutableArray<SourceBattleItemDefinition> Items);
public static class SourceBattleItemCatalogLoader
{
    public const string RelativePath = "content/generated/source-battle-items/catalog.json";
    public const string ResourceSha256 = "ce2edeb1dc28481c2f3496126bca38664667ba93520f1096b20c34d85f9f93ab";
    public static SourceBattleItemCatalog LoadAuthenticated(string projectRoot)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(projectRoot, RelativePath));
        if (bytes.Length != 70297 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != ResourceSha256)
            throw new InvalidDataException("source_item_resource_fingerprint");
        var document = JsonSerializer.Deserialize<SourceBattleItemDocument>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("source_item_document_missing");
        if (document.SemanticSha256 != SourceBattleItemCatalog.SemanticSha256) throw new InvalidDataException("source_item_semantic_identity");
        return SourceBattleItemCatalog.Authenticate(document.SourceCommit, document.Items);
    }
}
