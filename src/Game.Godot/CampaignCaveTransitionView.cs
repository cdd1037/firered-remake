using Game.Application;
using Game.Content;
using Godot;

public partial class CampaignView
{
    private GeneratedCaveTransitionArt? _caveTransitionArt;
    private ImageTexture? _caveTransitionTexture;
    private readonly byte[] _caveTransitionPixels=new byte[240*160*4];
    private GeneratedCaveTransitionArt CaveTransitionArt=>_caveTransitionArt??=
        GeneratedCaveTransitionArt.Load(Path.Combine(_root,"content/generated"));

    private void DrawCaveExitFrame(int elapsed)
    {
        var art=CaveTransitionArt;
        DrawCaveExit(FieldCaveTransitionPresentation.SampleExit(art.TransitionPalette,art.WhitePalette,elapsed));
    }

    private void DrawCaveExit(FieldCaveTransitionPresentation.PhaseSample sample)
    {
        var art=CaveTransitionArt;
        for(int y=0;y<160;y++)
            FieldCaveTransitionPresentation.CompositeRgba(art.Indices.AsSpan(y*art.Width,240),sample,
                _caveTransitionPixels.AsSpan(y*240*4,240*4));
        using var image=Image.CreateFromData(240,160,false,Image.Format.Rgba8,_caveTransitionPixels);
        if(_caveTransitionTexture is null)_caveTransitionTexture=ImageTexture.CreateFromImage(image);
        else _caveTransitionTexture.Update(image);
        DrawTexture(_caveTransitionTexture,Vector2.Zero);
    }

    private void ClearCaveTransitionTexture()
    {
        _caveTransitionTexture?.Dispose();_caveTransitionTexture=null;
    }
}
