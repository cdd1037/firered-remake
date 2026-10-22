using Game.Domain.Pokemon;
using Godot;

public partial class CampaignView
{
    private void SourceFittedText(SourceGameFont font, Vector2 topLeft, string text, float width)
    {
        float scale = SourceCompactTextLayout.FitScale(font.MeasureText(text).X, width);
        if (scale == 1) { SourceText(font, topLeft, text); return; }
        // Scale only these glyph quads. The caller may own a page-slide or other
        // canvas transform, which this local label must neither replace nor reset.
        var size = new Vector2I(font.Size, 0);
        float advance = 0;
        foreach (var glyph in font.PrepareText(text).EnumerateRunes())
        {
            var uv = font.Font.GetGlyphUVRect(0, size, glyph.Value);
            var glyphSize = font.Font.GetGlyphSize(0, size, glyph.Value);
            var offset = font.Font.GetGlyphOffset(0, size, glyph.Value) + new Vector2(0, font.Baseline);
            var destination = new Rect2(topLeft + new Vector2((advance + offset.X) * scale, offset.Y),
                new Vector2(glyphSize.X * scale, glyphSize.Y));
            if (destination.HasArea()) DrawTextureRectRegion(font.GlyphTexture(glyph.Value), destination, uv);
            advance += font.Font.GetGlyphAdvance(0, font.Size, glyph.Value).X;
        }
    }

    // Action selection owns one two-row window, never an invisible extra ACK page.
    private void DrawCompactBattlePrompt(string text)
    {
        string[] lines = text.Split('\n');
        if (lines.Length > 2) throw new InvalidDataException("Action prompt exceeds its two-row window.");
        for (int i = 0; i < lines.Length; i++)
            SourceFittedText(_battleWhite, LabBattleUiSkin.BattleMessageText + new Vector2(0, i * 16), lines[i], 110);
    }

    private float SourceScrollingText(SourceGameFont font, Vector2 topLeft, string text, float width)
    {
        float measured = font.MeasureText(text).X;
        if (measured <= width) { SourceText(font, topLeft, text); return measured; }
        var clip = new Rect2(topLeft, new(width, font.GlyphHeight));
        var position = topLeft - new Vector2(SourceCompactTextLayout.ScrollOffset(measured, width, _inventoryTick), 0);
        var size = new Vector2I(font.Size, 0);
        foreach (var glyph in font.PrepareText(text).EnumerateRunes())
        {
            var uv = font.Font.GetGlyphUVRect(0, size, glyph.Value);
            var offset = font.Font.GetGlyphOffset(0, size, glyph.Value) + new Vector2(0, font.Baseline);
            var destination = new Rect2(position + offset, font.Font.GetGlyphSize(0, size, glyph.Value));
            var visible = destination.Intersection(clip);
            if (visible.HasArea())
                DrawTextureRectRegion(font.GlyphTexture(glyph.Value), visible,
                    new Rect2(uv.Position + visible.Position - destination.Position, visible.Size));
            position.X += font.Font.GetGlyphAdvance(0, font.Size, glyph.Value).X;
        }
        return width;
    }

    private void DrawHealthName(SourceGameFont font, SourceGameFont? genderFont, Vector2 origin,
        string name, PokemonGender gender, float rightEdge)
    {
        string marker = gender == PokemonGender.Female ? "♀" : "♂";
        float genderWidth = genderFont?.MeasureText(marker).X ?? 0;
        float nameWidth = SourceScrollingText(font, origin, name, rightEdge - origin.X - genderWidth);
        if (genderFont is not null) SourceText(genderFont, origin + new Vector2(nameWidth, 0), marker);
    }
}
