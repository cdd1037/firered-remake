using Game.Domain.Items;

namespace Game.Content;

public sealed record PretOpeningItemsDocument(int SchemaVersion, OpeningItemsCatalog Catalog);

/// <summary>Loads the initial PC inventory and executable Potion definition.</summary>
public static class PretOpeningItems
{

    public static OpeningItemsCatalog Load(string generatedJson, SharedItemCatalog items)
    {
        var document = ContentJson.ParseStrict<PretOpeningItemsDocument>(generatedJson, items.CreateJsonOptions());
        if (document.SchemaVersion != 1 || document.Catalog is null)
            throw ContentJson.Error("opening_items_schema", "$", "The opening item catalog is required.");
        try { OpeningInventoryRules.ValidateCatalog(document.Catalog); }
        catch (ArgumentException ex) { throw ContentJson.Error("opening_items_catalog", "$.catalog", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("opening_items_catalog", "$.catalog", ex.Message); }
        return document.Catalog;
    }
}
