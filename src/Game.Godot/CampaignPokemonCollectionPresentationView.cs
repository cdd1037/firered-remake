using Game.Content;
using Godot;

public partial class CampaignView
{
    private PretPokemonCollectionPresentation? _pokemonCollectionArt;

    private void LoadPokemonCollectionPresentation()
    {
        if (_catalog.PokemonContent is not { } content || _pokedexMenu is null) return;
        var art = PretPokemonCollectionPresentation.Load(_root, content.Pokemon, _pokedexMenu);
        Texture2D Pixels(PokedexMenuTexture texture)
        {
            using var image = NativeImages.LoadPng(art.Textures[texture.TexturePath], texture.Width, texture.Height);
            return SceneTextures.FromImage(image);
        }
        foreach (var sprite in art.Sprites)
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.Shiny),
                new(Pixels(sprite.Texture), new(sprite.X, sprite.Y)));
        foreach (var entry in art.Entries)
        {
            if (!_battleArt.TryGetValue((entry.SourceSpeciesId, "front", false), out var front))
                throw new InvalidDataException("Owned collection battle portrait is missing.");
            _portraits.TryAdd(entry.SourceSpeciesId, front.Texture);
            _storageIcons.Add(entry.Species, Pixels(entry.Icon));
            _route22Footprints.Add(entry.SourceSpeciesId, Pixels(entry.Footprint));
        }
        _pokemonCollectionArt = art;
    }
}
