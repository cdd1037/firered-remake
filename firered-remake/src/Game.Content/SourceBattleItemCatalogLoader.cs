using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;

namespace Game.Content;

public sealed record SourceBattleItemDocument( ImmutableArray<SourceBattleItemDefinition> Items);
public static class SourceBattleItemCatalogLoader
{
    public const string RelativePath = "content/generated/battle/items.json";
    public static SourceBattleItemCatalog LoadAuthenticated(string projectRoot) =>
        LoadAuthenticated(projectRoot, SharedItemCatalog.LoadProject(projectRoot));

    public static SourceBattleItemCatalog LoadAuthenticated(string projectRoot, SharedItemCatalog items)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(projectRoot, RelativePath));
        var document = JsonSerializer.Deserialize<SourceBattleItemDocument>(bytes, items.CreateJsonOptions())
            ?? throw new InvalidDataException("source_item_document_missing");
        return SourceBattleItemCatalog.Create(document.Items);
    }
}
