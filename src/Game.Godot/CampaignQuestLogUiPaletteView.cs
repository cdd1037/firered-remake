using Game.Presentation;
using Game.Presentation.BattleAnimations;
using Godot;

public partial class CampaignView
{
    // The reserved UI palette avoids Quest Log grayscale, but FadeScreen selects
    // PALETTES_ALL: these BG colors follow the same black fade as the field BG.
    // Retain at most the sixteen nonzero source fade steps until playback resets.
    private readonly ImageTexture?[] _questLogUiWindows = new ImageTexture?[17];
    private readonly SourceGameFont?[] _questLogUiFonts = new SourceGameFont?[17];

    private Color QuestLogUiColor(int index, int blackCoefficient)
    {
        ValidateQuestLogUiFade(blackCoefficient);
        var color = QuestLogColor(index);
        if (blackCoefficient == 0) return color;
        ushort original = (ushort)((color.R8 >> 3) | ((color.G8 >> 3) << 5) | ((color.B8 >> 3) << 10));
        ushort faded = SourcePaletteMath.Blend(original, blackCoefficient, 0);
        static byte Expand(int channel) => (byte)((channel << 3) | (channel >> 2));
        return Color.Color8(Expand(faded & 31), Expand((faded >> 5) & 31), Expand((faded >> 10) & 31));
    }

    private Texture2D QuestLogUiWindow(int blackCoefficient)
    {
        ValidateQuestLogUiFade(blackCoefficient);
        if (blackCoefficient == 0) return _questLogWindow!;
        if (_questLogUiWindows[blackCoefficient] is {} cached) return cached;
        using var image = _questLogWindow!.GetImage();
        image.Convert(Image.Format.Rgba8);
        var pixels = image.GetData();
        QuestLogPalettePresentation.TransformRgba(pixels, 0, blackCoefficient);
        using var transformed = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, pixels);
        return _questLogUiWindows[blackCoefficient] = ImageTexture.CreateFromImage(transformed);
    }

    private SourceGameFont QuestLogUiFont(int blackCoefficient)
    {
        ValidateQuestLogUiFade(blackCoefficient);
        if (blackCoefficient == 0) return _questLogFont!;
        return _questLogUiFonts[blackCoefficient] ??= _questLogFont!.CreateColorVariant(
            QuestLogUiColor(1, blackCoefficient), QuestLogUiColor(12, blackCoefficient));
    }

    private static void ValidateQuestLogUiFade(int coefficient)
    {
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
