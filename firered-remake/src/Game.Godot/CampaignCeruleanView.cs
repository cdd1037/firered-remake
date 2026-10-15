using Game.Content;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Godot;
public partial class CampaignView
{
    private void LoadCeruleanPresentation()
    {
        if (_catalog.CeruleanPokemon is not { } source) return;
        Image Decode(byte[] bytes, int width, int height)
        {
            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok) { image.Dispose(); throw new InvalidDataException("Invalid Cerulean PNG."); }
            image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != width || image.GetHeight() != height)
            { image.Dispose(); throw new InvalidDataException("Cerulean source pixels differ."); }
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
        foreach(var asset in PretCeruleanTrainerArt.Load(_root))
        {
            using var image=NativeImages.LoadPng(asset.Png, 64, 64);
            _forestTrainerPortraits.Add(asset.Id,ImageTexture.CreateFromImage(image));
        }
        var machineIcon=PretCeruleanMachines.LoadProject(_root).Icon;
        _earlyItemIcons.Add(machineIcon.Item,ItemTexture("cerulean-machines", machineIcon));
    }
}
