using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretOpeningItemInput(string Path);
public sealed record PretOpeningItemsDocument(int SchemaVersion, ImmutableArray<PretOpeningItemInput> Inputs,
    ImmutableArray<string> Limitations, OpeningItemsCatalog Catalog);

/// <summary>Validates the complete reviewed UTF-8 artifact; the caller owns filesystem access.</summary>
public static class PretOpeningItems
{

    public static OpeningItemsCatalog Load(string generatedJson)
    {
        var document = ContentJson.ParseStrict<PretOpeningItemsDocument>(generatedJson, ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.Inputs.IsDefault ||
            document.Limitations.IsDefaultOrEmpty || document.Catalog is null)
            throw ContentJson.Error("opening_items_profile", "$", "The complete bounded Potion document is required.");
        var inputs = new Dictionary<string, PretOpeningItemInput>(StringComparer.Ordinal);
        foreach (var input in document.Inputs)
            if (input is null || string.IsNullOrWhiteSpace(input.Path) || !inputs.TryAdd(input.Path, input))
                throw ContentJson.Error("opening_items_inputs", "$.inputs", "Invalid or duplicate source identity.");
        try { OpeningInventoryRules.ValidateCatalog(document.Catalog); }
        catch (ArgumentException ex) { throw ContentJson.Error("opening_items_catalog", "$.catalog", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("opening_items_catalog", "$.catalog", ex.Message); }
        return document.Catalog;
    }
}
