using System.Security.Cryptography;
using Game.Content;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Godot;
public partial class CampaignView
{
    private void LoadCeruleanPresentation()
    {
        if (_catalog.CeruleanPokemon is not { } source) return;
        if (_battleArt.Count != 224 || _storageIcons.Count != 56 || _route22Footprints.Count != 27)
            throw new InvalidDataException("Cerulean must extend the existing fifty-six species.");
        Image Decode(byte[] bytes, int width, int height, string sha)
        {
            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok) { image.Dispose(); throw new InvalidDataException("Invalid Cerulean PNG."); }
            image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != width || image.GetHeight() != height || Convert.ToHexStringLower(SHA256.HashData(image.GetData())) != sha)
            { image.Dispose(); throw new InvalidDataException("Cerulean source pixels differ."); }
            return image;
        }
        foreach (var sprite in source.Sprites)
        {
            using var image = Decode(source.ReadSpriteTexture(new(sprite.SpeciesId), sprite.Side, sprite.PaletteVariant == "shiny"), 64, 64, sprite.RgbaSha256);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(ImageTexture.CreateFromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var foot in source.Footprints)
        {
            using var image = Decode(source.ReadFootprintTexture(foot.SourceSpeciesId), 16, 16, foot.RgbaSha256);
            _route22Footprints.Add(foot.SourceSpeciesId, ImageTexture.CreateFromImage(image));
        }
        foreach (var icon in source.Icons)
        {
            using var image = Decode(source.ReadIconTexture(new(icon.SpeciesId)), 32, 32, icon.RgbaSha256);
            var rgba = image.GetData();
            // Match the existing storage adapter's source RGB5 display expansion.
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int value = rgba[i + c] >> 3; rgba[i + c] = (byte)((value << 3) | (value >> 2)); }
            using var display = Image.CreateFromData(32, 32, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), ImageTexture.CreateFromImage(display));
        }
        foreach(var asset in PretCeruleanTrainerArt.Load(_root))
        {
            using var image=Image.CreateFromData(64,64,false,Image.Format.Rgba8,asset.Rgba);
            _forestTrainerPortraits.Add(asset.Id,ImageTexture.CreateFromImage(image));
        }
        var machineIcon=PretCeruleanMachines.LoadProject(_root).Icon;
        using(var image=Image.CreateFromData(machineIcon.Width,machineIcon.Height,false,Image.Format.Rgba8,PretPewterMachines.IconRgba(machineIcon)))
            _earlyItemIcons.Add(machineIcon.Item,ImageTexture.CreateFromImage(image));
        if(_battleArt.Count!=260||_storageIcons.Count!=65||_route22Footprints.Count!=36)
            throw new InvalidDataException("Cerulean presentation inventory differs.");
    }
}
