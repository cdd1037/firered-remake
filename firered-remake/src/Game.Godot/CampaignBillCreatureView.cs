using Game.Content;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Godot;
public partial class CampaignView
{
    private void LoadBillCreaturePresentation()
    {
        if (_catalog.BillPokemon is not { } source) return;
        Image Decode(byte[] bytes, int width, int height)
        {
            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok) { image.Dispose(); throw new InvalidDataException("Invalid Bill PNG."); }
            image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != width || image.GetHeight() != height)
            { image.Dispose(); throw new InvalidDataException("Bill source pixels differ."); }
            return image;
        }
        foreach (var sprite in source.Sprites)
        {
            using var image = Decode(source.ReadSpriteTexture(new(sprite.SpeciesId), sprite.Side, sprite.PaletteVariant == "shiny"), 64, 64);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(ImageTexture.CreateFromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var foot in source.Footprints)
        {
            using var image = Decode(source.ReadFootprintTexture(foot.SourceSpeciesId), 16, 16);
            _route22Footprints.Add(foot.SourceSpeciesId, ImageTexture.CreateFromImage(image));
        }
        foreach (var icon in source.Icons)
        {
            using var image = Decode(source.ReadIconTexture(new(icon.SpeciesId)), 32, 32);
            var rgba = image.GetData();
            // Match the existing storage adapter's source RGB5 display expansion.
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int value = rgba[i + c] >> 3; rgba[i + c] = (byte)((value << 3) | (value >> 2)); }
            using var display = Image.CreateFromData(32, 32, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), ImageTexture.CreateFromImage(display));
        }
        foreach(var icon in _catalog.BillItems!.Icons.Where(i=>i.Item.Value is "pret:ITEM_NUGGET" or "pret:ITEM_FAME_CHECKER" or "pret:ITEM_SS_TICKET").Concat(PretBillMachines.LoadProject(_root).Icons.Where(i=>i.Item.Value is "pret:ITEM_TM45" or "pret:ITEM_TM43" or "pret:ITEM_TM28")))
        {
            
            _earlyItemIcons.Add(icon.Item,ItemTexture(_catalog.BillItems!.Icons.Contains(icon) ? "bill-items" : "bill-machines", icon));
        }
        var document=_questPocketUi??throw new InvalidDataException("Bill key pocket missing.");
        var position=document.Assets.Single(a=>a.Id==document.Items[0].Icon);
        foreach(var value in _catalog.BillItems.Items.Where(x=>x.Metadata.Item.Pocket=="POCKET_KEY_ITEMS"&&x.Metadata.Item.Id.Value is "pret:ITEM_FAME_CHECKER" or "pret:ITEM_SS_TICKET").Select(x=>x.Metadata))
        {
            var icon=_catalog.BillItems.Icons.Single(i=>i.Item==value.Item.Id);
            string id="bill-"+value.Item.SourceItemSymbol.ToLowerInvariant();
            
            _questPocketTextures.Add(id,ItemTexture(_catalog.BillItems!.Icons.Contains(icon) ? "bill-items" : "bill-machines", icon));
            document=document with
            {
                Items=document.Items.Add(new(value.Item.Id.Value,value.Item.SourceItemSymbol,value.Item.Name,value.Description,id,value.Item.FieldUseFunction)),
                Assets=document.Assets.Add(new(id,icon.PngPath,icon.Width,icon.Height,position.X,position.Y))
            };
        }
        _questPocketUi=document;
    }
}
