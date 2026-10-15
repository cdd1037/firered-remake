using System.Text.Json;
using Game.Content;
using Godot;

public partial class CampaignView
{

    private void ExtendEventTicketQuestPocket(ref QuestItemUiDocument document)
    {
        if (!_catalog.EventIslandsEnabled) return;
        var catalog = _catalog.EarlyShop ?? throw new InvalidDataException("Event-ticket item catalog missing.");
        const string directory = "content/generated/event-ticket-ui";
        var bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(_root, directory, "event-ticket-ui.json"));
        using var manifest = JsonDocument.Parse(bytes);
        var root = manifest.RootElement;
        var rows = root.GetProperty("icons");
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || rows.GetArrayLength() != 2)
            throw new InvalidDataException("Event-ticket UI source contract differs.");
        string firstIcon = document.Items[0].Icon;
        var position = document.Assets.Single(asset => asset.Id == firstIcon);
        foreach (var row in rows.EnumerateArray())
        {
            string id = row.GetProperty("item").GetString()!;
            var item = catalog.Items.Single(entry => entry.Id.Value == id);
            if (item.SourceItemSymbol != row.GetProperty("sourceSymbol").GetString() ||
                item.SourceItemId != row.GetProperty("sourceItemId").GetInt32() ||
                item.Name != row.GetProperty("name").GetString() ||
                item.Description != row.GetProperty("description").GetString() ||
                item.FieldUseFunction != row.GetProperty("fieldUseFunction").GetString() ||
                item.Pocket != "POCKET_KEY_ITEMS" || document.Items.Any(entry => entry.Id == id))
                throw new InvalidDataException("Event-ticket UI differs from the authoritative item catalog: " + id);
            string file = row.GetProperty("file").GetString()!;
            string path = directory + "/" + file;
            var rgba = System.IO.File.ReadAllBytes(System.IO.Path.Combine(_root, path));
            if (row.GetProperty("width").GetInt32() != 24 || row.GetProperty("height").GetInt32() != 24 ||
                rgba.Length != 24 * 24 * 4)
                throw new InvalidDataException("Event-ticket icon pixels differ: " + id);
            string assetId = "event-ticket-" + item.SourceItemSymbol.ToLowerInvariant();
            using var image = Image.CreateFromData(24, 24, false, Image.Format.Rgba8, rgba);
            _questPocketTextures.Add(assetId, ImageTexture.CreateFromImage(image));
            document = document with
            {
                Items = document.Items.Add(new(item.Id.Value, item.SourceItemSymbol, item.Name,
                    item.Description, assetId, item.FieldUseFunction)),
                Assets = document.Assets.Add(new(assetId,path,24,24,position.X,position.Y))
            };
        }
    }
}
