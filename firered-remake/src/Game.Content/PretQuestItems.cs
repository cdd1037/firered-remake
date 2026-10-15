using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretQuestItemInput(string Path);
public sealed record PretQuestItemsDocument(int SchemaVersion, ImmutableArray<PretQuestItemInput> Inputs,
    ImmutableArray<string> Limitations, QuestItemsCatalog Catalog);

/// <summary>Loads only the complete, byte-pinned local Parcel/Poke Ball source artifact.</summary>
public static class PretQuestItems
{

    public static QuestItemsCatalog Load(string generatedJson)
    {
        var document = ContentJson.ParseStrict<PretQuestItemsDocument>(generatedJson, ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.Inputs.IsDefault ||
            document.Limitations.IsDefaultOrEmpty || document.Catalog is null)
            throw ContentJson.Error("quest_items_profile", "$", "The complete bounded Parcel/Poke Ball document is required.");
        var inputs = new Dictionary<string, PretQuestItemInput>(StringComparer.Ordinal);
        foreach (var input in document.Inputs)
            if (input is null || string.IsNullOrWhiteSpace(input.Path) || !inputs.TryAdd(input.Path, input))
                throw ContentJson.Error("quest_items_inputs", "$.inputs", "Invalid or duplicate source identity.");
        try { QuestItemRules.ValidateCatalog(document.Catalog); }
        catch (ArgumentException ex) { throw ContentJson.Error("quest_items_catalog", "$.catalog", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("quest_items_catalog", "$.catalog", ex.Message); }
        return document.Catalog;
    }
}
