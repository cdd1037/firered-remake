using System.Text.Json;
using Game.Content;
using Godot;

public partial class CampaignView
{
    private readonly PresentationDexIndex _earlyDex = new();

    private void RegisterPokemonSprites(PokemonPresentationPackage source)
    {
        _earlyDex.AddPackage(source);
        foreach (var sprite in source.Sprites)
        {
            var texture = PresentationTexture(source.ReadSpriteTexture(new(sprite.SpeciesId), sprite.Side,
                sprite.PaletteVariant == "shiny"), 64, 64);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(texture, new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var footprint in source.Footprints)
            _route22Footprints.Add(footprint.SourceSpeciesId,
                PresentationTexture(source.ReadFootprintTexture(footprint.SourceSpeciesId), 16, 16));
    }

    private void RegisterPokemonIcons(PokemonPresentationPackage source)
    {
        foreach (var icon in source.Icons)
            _storageIcons.Add(new(icon.SpeciesId),
                PresentationTexture(source.ReadIconTexture(new(icon.SpeciesId)), 32, 32, replicatePalette: true));
    }

    private void RegisterPokemonArt(IEnumerable<ForestPokemonSprite> sprites, IEnumerable<StorageArtIcon> icons,
        IEnumerable<Route3PokemonFootprint> footprints, IReadOnlyDictionary<string, byte[]> files)
    {
        foreach (var sprite in sprites)
        {
            var texture = PresentationTexture(files[sprite.TexturePath], sprite.Width, sprite.Height);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(texture, new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
            if (sprite.Side == "front" && sprite.PaletteVariant == "normal")
                _portraits.Add(sprite.SourceSpeciesId, texture);
        }
        foreach (var icon in icons)
            _storageIcons.Add(new(icon.SpeciesId),
                PresentationTexture(files[icon.TexturePath], icon.Width, icon.Height, replicatePalette: true));
        foreach (var footprint in footprints)
            _route22Footprints.Add(footprint.SourceSpeciesId,
                PresentationTexture(files[footprint.TexturePath], footprint.Width, footprint.Height));
    }

    // Decode, validate and upload once. The decoded CPU image never outlives registration;
    // the scene owner retains the texture until the existing presentation teardown.
    private Texture2D PresentationTexture(IReadOnlyDictionary<string, byte[]> files, JsonElement row,
        bool replicatePalette = false) => PresentationTexture(files[row.GetProperty("texturePath").GetString()!],
            row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32(), replicatePalette);

    private Texture2D PresentationTexture(byte[] png, int width, int height, bool replicatePalette = false)
    {
        using var image = NativeImages.LoadPng(png, width, height);
        if (replicatePalette)
        {
            var rgba = image.GetData();
            for (int i = 0; i < rgba.Length; i += 4)
                if (rgba[i + 3] != 0)
                    for (int c = 0; c < 3; c++)
                    {
                        int value = rgba[i + c] >> 3;
                        rgba[i + c] = (byte)((value << 3) | (value >> 2));
                    }
            image.SetData(width, height, false, Image.Format.Rgba8, rgba);
        }
        return SceneTextures.FromImage(image);
    }
}
