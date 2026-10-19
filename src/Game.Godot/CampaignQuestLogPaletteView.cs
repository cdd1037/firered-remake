using Game.Application;
using Game.Domain.Ids;
using Game.Presentation;
using Godot;

public partial class CampaignView
{
    private readonly Dictionary<(Texture2D Source,int Blend),Texture2D> _questPaletteTextures=[];
    private CampaignResourceCache.Owner? _questPaletteOwner;
    private (object Owner,int Scene,int Revision,bool Saved,int Gray,int Background,int Object,ushort Target,string Neighbors,FieldDoorPaint? Door)? _questPaletteState;

    private void PrepareFieldSnapshotPalette(FieldSnapshot snapshot,int grayscale,int backgroundBlend,int objectBlend,IReadOnlyList<(MapId Map,Vector2 Origin)> neighbors,ushort fadeColor=0)
    {
        var state=(snapshot.Owner,snapshot.Scene,snapshot.Revision,snapshot.SavedGameVisible,grayscale,backgroundBlend,objectBlend,fadeColor,
            string.Join('|',neighbors.Select(neighbor=>neighbor.Map.Value).Order(StringComparer.Ordinal)),snapshot.Door);
        if(_questPaletteState==state)return;
        // Only this visible revision and both current bank coefficients are held.
        // Clear before acquiring map resources so an evicted original is never a key.
        ClearQuestLogPaletteTextures();_questPaletteState=state;
    }

    private Texture2D FieldSnapshotPaletteTexture(Texture2D original,bool background)
    {
        int coefficient=_questPaletteState?.Gray??0;
        int blend=background?_questPaletteState?.Background??0:_questPaletteState?.Object??0;
        if(coefficient==0&&blend==0)return original;
        if(_questPaletteTextures.TryGetValue((original,blend),out var cached))return cached;
        using var image=original.GetImage();image.Convert(Image.Format.Rgba8);
        var pixels=image.GetData();QuestLogPalettePresentation.TransformRgba(pixels,coefficient,blend,_questPaletteState?.Target??0);
        using var transformed=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,pixels);
        var texture=(_questPaletteOwner??=_mapResources.CreateOwner("field-snapshot-palette")).FromImage(transformed);
        _questPaletteTextures.Add((original,blend),texture);return texture;
    }

    private void ClearQuestLogPaletteTextures()
    {
        _questPaletteTextures.Clear();_questPaletteOwner?.Dispose();_questPaletteOwner=null;_questPaletteState=null;
    }
}
