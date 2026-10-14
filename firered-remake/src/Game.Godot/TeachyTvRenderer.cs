using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Tutorials;
using Godot;

/// <summary>Original 240x160 screen, title, Route 1 crop and host pixels. No demonstration renderer
/// or invented fallback is installed here. The caller draws the real battle/bag owner when false.</summary>
public sealed class TeachyTvRenderer : IDisposable
{
    private const string ArtManifestHash = "b2e9452ae639b28cb55cc6d97b93161ceb437234fd07e263fed0d7146e5ff0a2";
    private readonly Dictionary<string, ImageTexture> _textures = new(StringComparer.Ordinal);
    private readonly SourceGameFont _menu, _dialogue;
    private readonly SourceTextCursor _cursor;
    private readonly Color _textBackground;
    private static readonly int[] Bounce = [0,0,0,1,1,1,1,1,2,1,1,1,1,1,0,0,0,0,0,-1,-1,-1,-1,-1,-2,-1,-1,-1,-1,-1,0,0];

    private TeachyTvRenderer(string root, string artDirectory, SourceTextCursor cursor)
    {
        _cursor = cursor;
        byte[] bytes = File.ReadAllBytes(Path.Combine(artDirectory, "teachy-tv-art.json"));
        CheckHash(bytes, ArtManifestHash, "manifest");
        using var document = JsonDocument.Parse(bytes);
        var manifest = document.RootElement;
        if (manifest.GetProperty("profile").GetString() != "firered-teachy-tv-art-v1"
            || manifest.GetProperty("sourceCommit").GetString() != SourceGameFont.SourceCommit)
            throw new InvalidDataException("teachy_tv_art_identity");
        var palette = manifest.GetProperty("contract").GetProperty("palettes");
        Color C(int index) => Color.Color8(palette[index][0].GetByte(), palette[index][1].GetByte(), palette[index][2].GetByte());
        _textBackground = C(60);
        var fonts = Path.Combine(root, "content/generated/source-font");
        _menu = SourceGameFont.Load(fonts, foreground: C(49), shadow: C(50));
        try
        {
            _dialogue = SourceGameFont.Load(fonts, foreground: C(49), shadow: C(51));
            foreach (var asset in manifest.GetProperty("assets").EnumerateArray())
            {
                string path = asset.GetProperty("path").GetString()!;
                if (Path.GetFileName(path) != path || !path.EndsWith(".png", StringComparison.Ordinal)) throw new InvalidDataException("teachy_tv_art_path");
                bytes = File.ReadAllBytes(Path.Combine(artDirectory, path));
                if (bytes.Length != asset.GetProperty("bytes").GetInt32()) throw new InvalidDataException("teachy_tv_art_length");
                CheckHash(bytes, asset.GetProperty("sha256").GetString()!, path);
                using var image = new Image();
                if (image.LoadPngFromBuffer(bytes) != Error.Ok || image.GetFormat() != Image.Format.Rgba8
                    || image.GetWidth() != asset.GetProperty("width").GetInt32() || image.GetHeight() != asset.GetProperty("height").GetInt32())
                    throw new InvalidDataException("teachy_tv_art_decode:" + path);
                string id = Path.GetFileNameWithoutExtension(path);
                _textures.Add(id, ImageTexture.CreateFromImage(image));
                if (id is "route1-background" or "static-tile" or "title-layer")
                {
                    byte[] dark = image.GetData();
                    for (int i = 0; i < dark.Length; i += 4) for (int c = 0; c < 3; c++)
                    { int v = dark[i+c] >> 3; v -= (v * 5) >> 4; dark[i+c] = (byte)((v << 3) | (v >> 2)); }
                    using var darkImage = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, dark);
                    _textures.Add(id + "-dark", ImageTexture.CreateFromImage(darkImage));
                }
            }
        }
        catch { _menu.Dispose(); _dialogue?.Dispose(); foreach (var texture in _textures.Values) texture.Dispose(); throw; }
    }
    public static TeachyTvRenderer Load(string root, SourceTextCursor sharedCursor, string? artDirectory = null) =>
        new(root, artDirectory ?? Path.Combine(root, "content/generated/teachy-tv-art"), sharedCursor);

    public bool Draw(CanvasItem canvas, TeachyTvPresentation view)
    {
        if (!view.OwnsOuterScreen) return false;
        canvas.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        canvas.DrawRect(new(0,0,240,160), Colors.Black);
        DrawField(canvas, _textures["route1-background-dark"], view.FieldScrollX, view.FieldScrollY, new(0,0,240,160));
        bool menu = view.State!.Phase == TeachyTvPhase.Menu;
        if (menu) DrawField(canvas, _textures["route1-background"], view.FieldScrollX, view.FieldScrollY, new(28,12,184,88));
        foreach (var grass in view.Grass.Where(g => g.SplitBehindHost)) DrawGrass(canvas, grass, true);
        if (view.HostVisible)
        {
            var f = view.HostFrame;
            canvas.DrawTextureRectRegion(_textures["host-sheet"], new(view.HostX-8,view.HostY-16,f.FlipHorizontal?-16:16,32), new(f.Image*16,0,16,32));
        }
        foreach (var grass in view.Grass) DrawGrass(canvas, grass, false);
        if (view.StaticVisible) DrawStatic(canvas, view.Frame, menu);
        if (view.TitleVisible) canvas.DrawTexture(_textures["title-layer-dark"], Vector2.Zero);
        canvas.DrawTexture(_textures["screen-frame"], Vector2.Zero);
        if (view.EndVisible) canvas.DrawTexture(_textures["end-graphic"], new(160,80));
        canvas.DrawRect(new(16,120,208,32), _textBackground);
        if (menu) DrawMenu(canvas, view);
        DrawText(canvas, view.Text);
        if (view.FadeCoefficient > 0) canvas.DrawRect(new(0,0,240,160), new Color(0,0,0,view.FadeCoefficient/16f));
        return true;
    }
    private void DrawMenu(CanvasItem canvas, TeachyTvPresentation view)
    {
        var state = view.State!; var entries = TeachyTvPlaybackRules.Menu(view.Catalog!, state);
        int top = state.HasTmCase ? 14 : 22, count = state.HasTmCase ? 6 : 5;
        for (int row = 0; row < count && state.ScrollOffset + row < entries.Length; row++)
            Text(canvas, _menu, new(40,top+14*row), view.Catalog!.Texts[entries[state.ScrollOffset+row].LabelSymbol].EncodedText.TrimEnd('$'));
        Text(canvas, _menu, new(32,top+14*state.SelectedRow), "▶");
        if (!state.HasTmCase) return;
        int bounce = Bounce[(int)(view.Frame % 32)];
        if (state.ScrollOffset > 0) canvas.DrawTextureRectRegion(_textures["scroll-arrows"], new(112,4+bounce,16,16), new(0,16,16,16));
        if (state.ScrollOffset < entries.Length-count) canvas.DrawTextureRectRegion(_textures["scroll-arrows"], new(112,92-bounce,16,-16), new(0,16,16,16));
    }
    private void DrawText(CanvasItem canvas, TeachyTvTextPresentation text)
    {
        if (text.Page is not {} page) return;
        foreach (var line in page.Lines)
        {
            int count = Math.Clamp(text.VisibleLength - line.CharacterStart, 0, line.Text.Length);
            if (count == 0) continue;
            int y = 121 + line.LineIndex*15 - text.ScrollPixels;
            // Source ScrollWindow clips the old top row to the original 32px window.
            if (y < 121)
            {
                DrawClippedText(canvas, new(16,y), line.Text[..count], 120,152);
            }
            else Text(canvas, _dialogue, new(16,y), line.Text[..count]);
        }
        if (text.AtPrompt)
        {
            var line = page.Lines[^1];
            int width = (int)_dialogue.Font.GetStringSize(_dialogue.PrepareText(line.Text), fontSize:_dialogue.Size).X;
            _cursor.Draw(canvas, new(16+width,121+line.LineIndex*15), text.PromptFrames);
        }
    }
    /// <summary>Original TV dialogue skin only: white source text on palette3[12].
    /// A Bag/TM owner must select its own source window/colors when that controller differs.</summary>
    public void DrawDialogue(CanvasItem canvas, TeachyTvTextPresentation text)
    {
        canvas.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        canvas.DrawTextureRectRegion(_textures["screen-frame"],new(0,112,240,48),new(0,112,240,48));
        canvas.DrawRect(new(16,120,208,32),_textBackground);
        DrawText(canvas,text);
    }
    private void DrawClippedText(CanvasItem canvas, Vector2 position, string text, int top, int bottom)
    {
        // Cached source font atlas provides exact glyph pixels; clip each region instead of a viewport allocation.
        var font = _dialogue.Font; var size = new Vector2I(_dialogue.Size,0);
        if (!_textures.TryGetValue("dialogue-atlas",out var texture))
        { using var atlas = font.GetTextureImage(0,size,0); _textures.Add("dialogue-atlas",texture=ImageTexture.CreateFromImage(atlas)); }
        float x = position.X;
        foreach (var rune in _dialogue.PrepareText(text).EnumerateRunes())
        {
            var uv=font.GetGlyphUVRect(0,size,rune.Value); var glyphSize=font.GetGlyphSize(0,size,rune.Value);
            float y0=Math.Max(position.Y,top), y1=Math.Min(position.Y+glyphSize.Y,bottom);
            if(y1>y0) canvas.DrawTextureRectRegion(texture,new(x,y0,glyphSize.X,y1-y0),new(uv.Position.X,uv.Position.Y+y0-position.Y,glyphSize.X,y1-y0));
            x+=font.GetGlyphAdvance(0,_dialogue.Size,rune.Value).X;
        }
    }
    private static void Text(CanvasItem canvas,SourceGameFont font,Vector2 point,string text) =>
        canvas.DrawString(font.Font,point+new Vector2(0,font.Baseline),font.PrepareText(text),fontSize:font.Size);
    private void DrawGrass(CanvasItem canvas, TeachyTvGrassFrame grass, bool behind)
    {
        if (behind) canvas.DrawTextureRectRegion(_textures["tall-grass-sheet"],new(grass.X-8,grass.Y-8,16,8),new(0,grass.Image*16,16,8));
        else if(grass.SplitBehindHost) canvas.DrawTextureRectRegion(_textures["tall-grass-sheet"],new(grass.X-8,grass.Y,16,8),new(0,grass.Image*16+8,16,8));
        else canvas.DrawTextureRectRegion(_textures["tall-grass-sheet"],new(grass.X-8,grass.Y-8,16,16),new(0,grass.Image*16,16,16));
    }
    private void DrawStatic(CanvasItem canvas,long frame,bool menu)
    {
        // Visual-only noise seed: never consumes the campaign's encounter or battle RNG.
        uint noise=unchecked((uint)frame*747796405u+2891336453u);
        for(int y=8;y<104;y+=8) for(int x=16;x<224;x+=8)
        {
            noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;
            bool fx=(noise&1)!=0,fy=(noise&2)!=0;
            var texture=_textures["static-tile-dark"];
            canvas.DrawTextureRectRegion(texture,new(x,y,fx?-8:8,fy?-8:8),new(0,0,8,8));
            if(menu)
            {
                int left=Math.Max(x,28),top=Math.Max(y,12),right=Math.Min(x+8,212),bottom=Math.Min(y+8,100);
                if(right>left&&bottom>top)
                {
                    int sx=fx?x+8-right:left-x,sy=fy?y+8-bottom:top-y;
                    canvas.DrawTextureRectRegion(_textures["static-tile"],new(left,top,fx?left-right:right-left,fy?top-bottom:bottom-top),new(sx,sy,right-left,bottom-top));
                }
            }
        }
    }
    private static void DrawField(CanvasItem canvas,Texture2D texture,int scrollX,int scrollY,Rect2I clip)
    {
        for(int y=clip.Position.Y;y<clip.End.Y;)
        {
            int sy=((y+scrollY)%256+256)%256,h=Math.Min(256-sy,clip.End.Y-y);
            for(int x=clip.Position.X;x<clip.End.X;)
            {
                int sx=((x+scrollX)%256+256)%256,w=Math.Min(256-sx,clip.End.X-x);
                canvas.DrawTextureRectRegion(texture,new(x,y,w,h),new(sx,sy,w,h));x+=w;
            }
            y+=h;
        }
    }
    private static void CheckHash(byte[] bytes,string expected,string label)
    {if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=expected)throw new InvalidDataException("teachy_tv_art_hash:"+label);}
    public void Dispose() { foreach(var texture in _textures.Values)texture.Dispose();_textures.Clear();_menu.Dispose();_dialogue.Dispose(); }
}
