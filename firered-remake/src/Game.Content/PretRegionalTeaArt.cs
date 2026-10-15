using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record RegionalTeaArtDocument(int SchemaVersion, QuestItemUiEntry Item, QuestItemUiAsset Asset, string TexturePath);

/// <summary>Original Tea pixels and source item metadata. Loading admits no field-use or inventory action.</summary>
public sealed class PretRegionalTeaArt
{
    public const string Profile = "firered-regional-tea-art-v1";
    public const string GeneratedDirectory = "content/generated/ui/regional-tea-art/";
    public RegionalTeaArtDocument Document { get; }
    private readonly byte[] _png;
    private PretRegionalTeaArt(RegionalTeaArtDocument document, byte[] png) { Document = document; _png = png; }
    public byte[] ReadPng() => [.. _png];

    public static PretRegionalTeaArt Load(string projectRoot, MachineItem tea)
    {
        var root = Path.GetFullPath(projectRoot);
        var document = ContentJson.ParseStrict<RegionalTeaArtDocument>(Encoding.UTF8.GetString(
            Read(root,GeneratedDirectory + "tea-art.json")), ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1,
            "Tea art scope differs.");
        var item = tea.Item;
        var ui = document.Item;
        Require(item.Id.Value == "pret:ITEM_TEA" && item.SourceItemId == 369 && item.SourceItemSymbol == "ITEM_TEA" &&
            item.Pocket == "POCKET_KEY_ITEMS" && item.Price == 0 && !item.CanToss && !item.CanSell && !item.CanGive &&
            item.FieldUseFunction == "FieldUseFunc_OakStopsYou" && ui.Id == item.Id.Value && ui.SourceSymbol == item.SourceItemSymbol &&
            ui.Name == item.Name && ui.Description == tea.Description && ui.FieldUseFunction == item.FieldUseFunction,
            "Tea presentation differs from the authoritative key item.");
        var asset = document.Asset;
        Require(asset.Id == "regional-tea" && ui.Icon == asset.Id && asset.TexturePath == "regional-tea.png" &&
            document.TexturePath == "regional-tea.png" && (asset.Width, asset.Height, asset.X, asset.Y) == (24, 24, 8, 124),
            "Tea icon layout differs.");
        var png = Read(root,GeneratedDirectory + document.TexturePath);
        return new(document, png);
    }

    private static byte[] Read(string root,string relative)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Contains('\\') && !relative.Split('/').Any(p => p is ".." or "." or ""), "Unsafe Tea art path.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        for (FileSystemInfo? current = new FileInfo(path); current is not null; current = current switch
            { FileInfo file => file.Directory, DirectoryInfo directory => directory.Parent, _ => null })
            Require(current.LinkTarget is null, "Tea art symlink rejected.");
        var bytes = File.ReadAllBytes(path);
        return bytes;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
