using Game.Domain.Items;

namespace Game.Content;

public sealed record PretQuestItemsDocument(int SchemaVersion, QuestItemsCatalog Catalog);

/// <summary>Loads quest item references and their inventory limits.</summary>
public static class PretQuestItems
{

    public static QuestItemsCatalog Load(string generatedJson, SharedItemCatalog items)
    {
        var document = ContentJson.ParseStrict<PretQuestItemsDocument>(generatedJson, items.CreateJsonOptions());
        if (document.SchemaVersion != 1 || document.Catalog is null)
            throw ContentJson.Error("quest_items_schema", "$", "The quest item catalog is required.");
        try { QuestItemRules.ValidateCatalog(document.Catalog); }
        catch (ArgumentException ex) { throw ContentJson.Error("quest_items_catalog", "$.catalog", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("quest_items_catalog", "$.catalog", ex.Message); }
        return document.Catalog;
    }
}
