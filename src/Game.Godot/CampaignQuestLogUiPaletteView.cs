using Game.Presentation;
using Game.Presentation.BattleAnimations;
using Godot;

public partial class CampaignView
{
    // The reserved UI palette avoids Quest Log grayscale, but FadeScreen selects
    // PALETTES_ALL: UI colors follow the same black/white target as the field BG.
    // Cache the bounded source coefficients for both targets until playback resets.
    private readonly ImageTexture?[] _questLogUiWindows = new ImageTexture?[34];
    private readonly SourceGameFont?[] _questLogUiFonts = new SourceGameFont?[34];

    private Color QuestLogUiColor(int index, int blackCoefficient,ushort fadeColor=0)
    {
        ValidateQuestLogUiFade(blackCoefficient,fadeColor);
        var color = QuestLogColor(index);
        if (blackCoefficient == 0) return color;
        ushort original = (ushort)((color.R8 >> 3) | ((color.G8 >> 3) << 5) | ((color.B8 >> 3) << 10));
        ushort faded = SourcePaletteMath.Blend(original, blackCoefficient, fadeColor);
        static byte Expand(int channel) => (byte)((channel << 3) | (channel >> 2));
        return Color.Color8(Expand(faded & 31), Expand((faded >> 5) & 31), Expand((faded >> 10) & 31));
    }

    private Texture2D QuestLogUiWindow(int blackCoefficient,ushort fadeColor=0)
    {
        ValidateQuestLogUiFade(blackCoefficient,fadeColor);
        if (blackCoefficient == 0) return _questLogWindow!;
        int cache=blackCoefficient+(fadeColor==0?0:17);
        if (_questLogUiWindows[cache] is {} cached) return cached;
        using var image = _questLogWindow!.GetImage();
        image.Convert(Image.Format.Rgba8);
        var pixels = image.GetData();
        QuestLogPalettePresentation.TransformRgba(pixels, 0, blackCoefficient,fadeColor);
        using var transformed = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, pixels);
        return _questLogUiWindows[cache] = ImageTexture.CreateFromImage(transformed);
    }

    private SourceGameFont QuestLogUiFont(int blackCoefficient,ushort fadeColor=0)
    {
        ValidateQuestLogUiFade(blackCoefficient,fadeColor);
        if (blackCoefficient == 0) return _questLogFont!;
        int cache=blackCoefficient+(fadeColor==0?0:17);
        return _questLogUiFonts[cache] ??= _questLogFont!.CreateColorVariant(
            QuestLogUiColor(1, blackCoefficient,fadeColor), QuestLogUiColor(12, blackCoefficient,fadeColor));
    }

    private static void ValidateQuestLogUiFade(int coefficient,ushort fadeColor)
    {
        if(fadeColor is not (0 or 0x7fff))throw new ArgumentOutOfRangeException(nameof(fadeColor));
        if (coefficient is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(coefficient));
    }

    private void ClearQuestLogUiPalette()
    {
        foreach (var texture in _questLogUiWindows) texture?.Dispose();
        foreach (var font in _questLogUiFonts) font?.Dispose();
        Array.Clear(_questLogUiWindows);
        Array.Clear(_questLogUiFonts);
    }
}
