using System.Text.Json;
using Game.Content;
using Game.Domain.Slots;

/// <summary>Small source-layer compositor. Reads a supplied display snapshot; owns no wallet, RNG or timing.</summary>
internal sealed class SlotMachineScreenRenderer
{
    private readonly SlotMachinePresentationData art;
    private readonly SlotMachineRules rules;
    private readonly byte[][] icons, fairies, digits;
    private readonly byte[] help;
    private readonly Dictionary<string, byte[]> pixels = new(StringComparer.Ordinal);
    private readonly int[] affine, darken, backdrop;
    private readonly JsonElement layout;
    private (int Bet, int Buttons, int Lines, int Normal, int? Light, int Darken)? backgroundKey;
    private readonly byte[] output = new byte[240 * 160 * 4];
    private byte[]? background;

    internal SlotMachineScreenRenderer(SlotMachinePresentationData presentation, SlotMachineRules source)
    {
        art = presentation; rules = source; layout = art.Layout;
        // Decode each co-used page once through Godot; source maps and palettes are import-only.
        foreach (var page in art.Textures.Values.GroupBy(t => t.TexturePath))
        {
            using var image = NativeImages.LoadPng(page.First().Png.ToArray());
            byte[] data = image.GetData();
            int pageWidth = image.GetWidth();
            foreach (var texture in page)
            {
                var region = texture.Region;
                if (region.X < 0 || region.Y < 0 || region.X + region.Width > pageWidth || region.Y + region.Height > image.GetHeight())
                    throw new InvalidDataException("Slot image region exceeds its page.");
                var crop = new byte[texture.Width * texture.Height * 4];
                for (int y = 0; y < texture.Height; y++)
                    data.AsSpan(((region.Y + y) * pageWidth + region.X) * 4, texture.Width * 4).CopyTo(crop.AsSpan(y * texture.Width * 4));
                pixels.Add(texture.Id, crop);
            }
        }
        byte[][] Frames(string id, int height, int count)
        {
            int size = art.Textures[id].Width * height * 4;
            return Enumerable.Range(0, count).Select(i => pixels[id].AsSpan(i * size, size).ToArray()).ToArray();
        }
        icons = Frames("reel-icons", 32, 7);
        fairies = Frames("clefairy", 32, 6);
        digits = Frames("digits", 16, 10);
        help = pixels["help"];
        static int[] Values(JsonElement row) => row.EnumerateArray().Select(n => n.GetInt32()).ToArray();
        affine = Values(layout.GetProperty("scanline").GetProperty("affineParameters"));
        darken = Values(layout.GetProperty("scanline").GetProperty("darkenCoefficients"));
        backdrop = Values(layout.GetProperty("backdropRgb555"));
    }

    // HBlank register writes control the following visible scanline. The source
    // callback begins at VCOUNT43; palettes are five-bit before LCD expansion.
    private int Scanline(int y)
    {
        int row = y - layout.GetProperty("scanline").GetProperty("registerOffset").GetInt32() - 1;
        return row is >= 0 and < 84 ? row : -1;
    }
    private static byte Expand(int value) => (byte)((value << 3) | (value >> 2));
    private static byte Shade(byte value, int coefficient)
    {
        int color = value >> 3;
        return Expand(color - ((color * coefficient) >> 4));
    }
    private void Sprite(byte[] output, byte[] pixels, int width, int height, int centerX, int centerY,
        bool mirror = false, bool applyAffine = false, int paletteFade = 0)
    {
        for (int dy = 0; dy < height; dy++)
        {
            int y = centerY - height / 2 + dy;
            if (y is < 0 or >= 160) continue;
            int scan = Scanline(y), coefficient = scan < 0 ? 0 : darken[scan];
            int scale = applyAffine && scan >= 0 ? affine[scan] : 256;
            for (int dx = 0; dx < width; dx++)
            {
                int x = centerX - width / 2 + dx;
                if (x is < 0 or >= 240) continue;
                int sx = applyAffine ? (((dx - width / 2) * scale) >> 8) + width / 2 : dx;
                if ((uint)sx >= width) continue;
                if (mirror) sx = width - 1 - sx;
                int source = (dy * width + sx) * 4;
                if (pixels[source + 3] == 0) continue;
                int destination = (y * 240 + x) * 4;
                for (int c = 0; c < 3; c++) output[destination + c] = Shade(SlotMachineVisualPresentation.FadeChannel(pixels[source + c], paletteFade), coefficient);
                output[destination + 3] = 255;
            }
        }
    }
    private static void Layer(byte[] output, ReadOnlySpan<byte> pixels, int width, int offsetX = 0, int visibleWidth = 240, int paletteFade = 0, int destinationX = 0, int destinationY = 0)
    {
        for (int y = 0; y < Math.Min(160-destinationY, pixels.Length / (width * 4)); y++)
        for (int x = 0; x < Math.Min(240-destinationX, visibleWidth); x++)
        {
            int source = (y * width + (x + offsetX) % width) * 4;
            if (pixels[source + 3] == 0) continue;
            int destination = ((y+destinationY) * 240 + x+destinationX) * 4;
            for (int c = 0; c < 3; c++) output[destination + c] = SlotMachineVisualPresentation.FadeChannel(pixels[source + c], paletteFade);
            output[destination + 3] = 255;
        }
    }

    private byte[] PreparedBackground(int bet, int buttons, int lines, int normalLines, int? light, int darken)
    {
        if (bet is < 0 or > 3 || buttons is < 0 or > 7 || lines is < 0 or > 31 || normalLines is < 0 or > 31 || light is < 0 or > 2 || darken is < 0 or > 8)
            throw new ArgumentOutOfRangeException(nameof(bet), "Slot display state outside source ranges.");
        byte[] result = background ?? new byte[240 * 160 * 4];
        pixels["background"].CopyTo(result, 0);
        void Patch(string id, string? geometry = null)
        {
            int width = art.Textures[id].Width;
            // Rectangle tuples: source x/y, destination x/y, width/height.
            foreach (var rect in layout.GetProperty("preparedPatches").GetProperty(geometry ?? id).EnumerateArray())
            {
                int sx = rect[0].GetInt32(), sy = rect[1].GetInt32();
                int x = rect[2].GetInt32(), y = rect[3].GetInt32();
                int w = rect[4].GetInt32(), h = rect[5].GetInt32();
                for (int row = 0; row < h; row++)
                    pixels[id].AsSpan(((sy + row) * width + sx) * 4, w * 4).CopyTo(result.AsSpan(((y + row) * 240 + x) * 4));
            }
        }
        if (light is int frame) Patch("light-" + frame);
        foreach (var line in layout.GetProperty("matchLines").EnumerateArray())
        {
            int id = line.GetProperty("id").GetInt32();
            bool winning = (lines & (1 << id)) != 0;
            bool active = (normalLines & (1 << id)) == 0 && bet >= line.GetProperty("minimumBet").GetInt32();
            if (winning) Patch($"win-{id}-{darken}", $"line-{id}-5");
            else Patch($"line-{id}-{(active ? 5 : 4)}");
        }
        for (int reel = 0; reel < 3; reel++) if ((buttons & (1 << reel)) != 0) Patch("button-" + reel);
        return result;
    }

    internal byte[] Render(SlotReels reels, int coins, int payout, int bet, int pressedButtons,
        int winningLines, int? payoutLightFrame, string fairyAnimation, int animationTicks,
        int helpWidth = 0, int backgroundFade = 0, int objectFade = 0, int lineDarken = 0, int normalLines = 0, string? message = null)
    {
        if (reels.Reels.IsDefault || reels.Reels.Length != 3 || reels.Reels.Any(r => r.Position is < 0 or >= 21 || r.Subpixel is < 0 or > 2) ||
            coins is < 0 or > 9999 || payout is < 0 or > 9999 || animationTicks < 0 || helpWidth is < 0 or > 256 || backgroundFade is < 0 or > 16 || objectFade is < 0 or > 16)
            throw new ArgumentException("slot_display_snapshot_invalid");
        for (int y = 0; y < 160; y++)
        {
            int scan = Scanline(y), coefficient = scan < 0 ? 0 : darken[scan];
            for (int x = 0; x < 240; x++)
            {
                int p = (y * 240 + x) * 4;
                for (int c = 0; c < 3; c++) output[p + c] = Shade(SlotMachineVisualPresentation.FadeChannel(Expand(backdrop[c]), backgroundFade), coefficient);
                output[p + 3] = 255;
            }
        }
        var centers = layout.GetProperty("reels").GetProperty("centers");
        for (int reel = 2; reel >= 0; reel--)
        for (int slot = 4; slot >= 0; slot--)
        {
            var center = centers[reel * 5 + slot]; var state = reels.Reels[reel];
            int icon = rules.Reels[reel][(state.Position + slot) % 21];
            Sprite(output, icons[icon], 32, 32, center[0].GetInt32(), center[1].GetInt32() + state.Subpixel * 8, applyAffine: true, paletteFade: objectFade);
        }
        var key = (bet, pressedButtons, winningLines, normalLines, payoutLightFrame, lineDarken);
        if (backgroundKey != key) { background = PreparedBackground(bet, pressedButtons, winningLines, normalLines, payoutLightFrame, lineDarken); backgroundKey = key; }
        Layer(output, background!, 240, paletteFade: backgroundFade);
        var animation = layout.GetProperty("clefairy").GetProperty("animations").GetProperty(fairyAnimation);
        int fairy = SlotMachineVisualPresentation.AnimationTile(animation, animationTicks) / 16;
        var fairyCenters = layout.GetProperty("clefairy").GetProperty("centers");
        for (int i = 0; i < 2; i++) Sprite(output, fairies[fairy], 32, 32, fairyCenters[i][0].GetInt32(), fairyCenters[i][1].GetInt32(), mirror: i == 1, paletteFade: objectFade);
        void Number(int number, string positions)
        {
            string text = number.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            var positionsArray = layout.GetProperty("digits").GetProperty(positions);
            for (int i = 0; i < 4; i++) Sprite(output, digits[text[i] - '0'], 8, 16, positionsArray[i][0].GetInt32(), positionsArray[i][1].GetInt32(), paletteFade: objectFade);
        }
        Number(coins, "creditCenters"); Number(payout, "payoutCenters");
        if (helpWidth > 0) Layer(output, help, 256, 256 - helpWidth, helpWidth, backgroundFade);
        Layer(output, pixels["controls"].AsSpan(), art.Textures["controls"].Width, paletteFade: backgroundFade);
        if(message is not null) Layer(output,pixels[message],176,visibleWidth:176,paletteFade:backgroundFade,destinationX:32,destinationY:112);
        return output;
    }
}
