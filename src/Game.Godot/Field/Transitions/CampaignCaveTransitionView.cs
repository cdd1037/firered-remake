using Game.Application;
using Game.Content;
using Game.Presentation.BattleAnimations;
using Godot;

public partial class CampaignView
{
    private GeneratedCaveTransitionArt? _caveTransitionArt;
    private ImageTexture? _caveTransitionTexture;
    private GeneratedMapPreviewCatalog? _mapPreviewCatalog;
    private GeneratedMapPreviewCatalog MapPreviewCatalog=>_mapPreviewCatalog??=GeneratedMapPreviewCatalog.Load(Path.Combine(_root,"content/generated"));
    private SourceGameFont? _mapPreviewFont;
    private readonly Dictionary<(ushort Foreground,ushort Shadow),SourceGameFont> _mapPreviewFonts=[];
    private readonly byte[] _caveTransitionPixels=new byte[240*160*4];
    private GeneratedCaveTransitionArt CaveTransitionArt=>_caveTransitionArt??=
        GeneratedCaveTransitionArt.Load(Path.Combine(_root,"content/generated"));

    private void DrawCaveExitFrame(int elapsed)
    {
        var art=CaveTransitionArt;
        DrawCaveExit(FieldCaveTransitionPresentation.SampleExit(art.TransitionPalette,art.WhitePalette,elapsed));
    }

    private void DrawCaveEnterFrame(int elapsed,bool followsSuppressedPreview)
    {
        var art=CaveTransitionArt;
        DrawCaveExit(FieldCaveTransitionPresentation.SampleEnter(art.TransitionPalette,art.WhitePalette,art.BlackPalette,
            elapsed,followsSuppressedPreview?(ushort)0:(ushort)0x7fff));
    }

    private static Color CaveColor(ushort color)
    {
        static byte Expand(int channel)=>(byte)((channel<<3)|(channel>>2));
        return Color.Color8(Expand(color&31),Expand((color>>5)&31),Expand((color>>10)&31));
    }

    private void DrawMapPreview(MapPreviewEntry entry,FieldCaveTransitionPresentation.PreviewSample sample)
    {
        if(sample.Cave is {} cave){DrawCaveExit(cave);return;}
        if(!sample.PreviewVisible){DrawRect(new(0,0,240,160),Colors.Black);return;}
        var art=entry.Art;
        for(int y=0;y<160;y++)
            FieldCaveTransitionPresentation.CompositePreviewRgba(art.Indices.AsSpan(y*art.Width,240),art.Palette.AsSpan(),sample,
                _caveTransitionPixels.AsSpan(y*240*4,240*4));
        DrawCavePixels();
        if(!sample.NameWindowCreated)return;
        ushort Fade(int index)=>SourcePaletteMath.Blend(art.Palette[index],sample.BackgroundBlend,sample.FadeColor);
        DrawRect(new(0,0,104,16),CaveColor(Fade(17)));
        var key=(Fade(20),Fade(19));
        if(!_mapPreviewFonts.TryGetValue(key,out var font))
        {
            _mapPreviewFont??=SourceGameFont.Load(Path.Combine(_root,"content/generated/ui/font"));
            _mapPreviewFonts.Add(key,font=_mapPreviewFont.CreateColorVariant(CaveColor(key.Item1),CaveColor(key.Item2)));
        }
        int width=(int)font.Font.GetStringSize(font.PrepareText(entry.Name),fontSize:font.Size).X;
        SourceText(font,new((104-width)/2,2),entry.Name);
    }

    private void DrawCaveExit(FieldCaveTransitionPresentation.PhaseSample sample)
    {
        var art=CaveTransitionArt;
        for(int y=0;y<160;y++)
            FieldCaveTransitionPresentation.CompositeRgba(art.Indices.AsSpan(y*art.Width,240),sample,
                _caveTransitionPixels.AsSpan(y*240*4,240*4));
        DrawCavePixels();
    }

    private void DrawCavePixels()
    {
        using var image=Image.CreateFromData(240,160,false,Image.Format.Rgba8,_caveTransitionPixels);
        if(_caveTransitionTexture is null)_caveTransitionTexture=ImageTexture.CreateFromImage(image);
        else _caveTransitionTexture.Update(image);
        DrawTexture(_caveTransitionTexture,Vector2.Zero);
    }

    private void ClearCaveTransitionTexture()
    {
        ClearForestPreview();
        _caveTransitionTexture?.Dispose();_caveTransitionTexture=null;
        foreach(var font in _mapPreviewFonts.Values)font.Dispose();_mapPreviewFonts.Clear();
        _mapPreviewFont?.Dispose();_mapPreviewFont=null;
    }
}
