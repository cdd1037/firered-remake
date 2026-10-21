using Game.Application;
using Game.Content;
using Godot;

public partial class CampaignView
{
    private readonly Dictionary<string, (int Species, Texture2D Texture)> _namedPokemonPictures = new(StringComparer.Ordinal);

    private void LoadMuseumPicturesPresentation()
    {
        var pictures = PretMuseumPictures.Load(_root);
        foreach (var asset in pictures.Assets.Values)
        {
            using var image = NativeImages.LoadPng(pictures.ReadTexture(asset.Id), asset.Width, asset.Height);
            _namedPokemonPictures.Add(asset.Id, (asset.SourceSpeciesId, SceneTextures.FromImage(image)));
        }
    }

    private bool DrawNamedPokemonPicture(CampaignMonPicture picture)
    {
        if (picture.AssetId is not { } id) return false;
        if (!_namedPokemonPictures.TryGetValue(id, out var asset) || asset.Species != picture.SourceSpeciesId)
            throw new InvalidDataException("Named picture identity is not registered: " + id);
        var box = new Vector2(picture.X * 8, picture.Y * 8);
        // CreateWindowFromRect adds one tile inside the frame. The source 64px
        // sprite is centered at (left * 8 + 40, top * 8 + 40).
        _battleSkin.DrawUserFrame(this, new(box, new Vector2(80, 80)));
        DrawTexture(asset.Texture, box + new Vector2(8, 8));
        return true;
    }
}
