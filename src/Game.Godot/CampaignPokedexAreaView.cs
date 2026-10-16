using Game.Content;
using Godot;

public partial class CampaignView
{
    private PokedexAreaPage? _pokedexAreaPage;

    private void PreparePokedexArea()
    {
        var state = _session.State;
        var species = _pokedexList!.Rows[_pokedexCursor.SelectedIndex].SourceSpeciesId;
        _pokedexAreaIcon=ResolvePokedexIcon(species);
        _pokedexAreaPage = _pokedexMenu!.BuildAreaPage(species, state.SeenSpecies, state.CaughtSpecies, PokedexAccess,
            new(state.Script.Flags, state.Script.Variables.GetValueOrDefault("VAR_ALTERING_CAVE_WILD_SET"),
                state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON"), _session.ActiveRoamerMapSection));
    }

    private void DrawPokedexArea()
    {
        var page = _pokedexAreaPage ?? throw new InvalidOperationException("Pokédex area page has not been prepared.");
        foreach (var region in page.Regions)
            DrawPokedexAreaSprite(region.Sprite, region.X, region.Y + page.VerticalOffset);
        // The original uses a union of OBJ-window masks. Baked source alpha colors
        // replace their underlying map pixels, so coincident markers never accumulate tint.
        foreach (var marker in page.Markers)
            DrawPokedexAreaSprite(marker.Sprite, marker.X, marker.Y + page.VerticalOffset);
        string area = _pokedexMenu!.UiTexts["gText_Area"];
        float width = _sourceSmall.Font.GetStringSize(_sourceSmall.PrepareText(area), fontSize: _sourceSmall.Size).X;
        SourceText(_sourceSmall, new((int)(184 - width / 2), 20 + page.VerticalOffset), area);
        if (page.Unknown)
        {
            var kanto = page.Regions[0];
            DrawPokedexAreaSprite(_pokedexMenu.Document.Areas.UnknownSprite, kanto.X + 4, kanto.Y + 28 + page.VerticalOffset);
            string unknown = _pokedexMenu.UiTexts["gText_AreaUnknown"];
            width = _sourceSmall.Font.GetStringSize(_sourceSmall.PrepareText(unknown), fontSize: _sourceSmall.Size).X;
            SourceText(_sourceSmall, new((int)(kanto.X + 48 - width / 2), kanto.Y + 29 + page.VerticalOffset), unknown);
        }
    }
    private void DrawPokedexAreaSprite(PokedexAreaSprite sprite, int x, int y)
        => DrawTextureRectRegion(PokedexTexture(sprite.Texture), new(x, y, sprite.Width, sprite.Height),
            new(sprite.SourceX, sprite.SourceY, sprite.Width, sprite.Height));
}
