using Game.Application;
using Game.Domain.Ids;
using Game.Presentation;
using Godot;

public partial class CampaignView
{
    private readonly Dictionary<Texture2D,Texture2D> _questPaletteTextures=new(ReferenceEqualityComparer.Instance);
    private CampaignResourceCache.Owner? _questPaletteOwner;
    private (CampaignQuestLogPlayback Playback,int Scene,int Revision,bool Saved,int Coefficient,string Neighbors)? _questPaletteState;

    private void PrepareQuestLogPalette(CampaignQuestLogPlayback playback,IReadOnlyList<(MapId Map,Vector2 Origin)> neighbors)
    {
        var state=(playback,playback.SceneIndex,playback.ArtRevision,playback.SavedGameVisible,playback.GrayscaleCoefficient,
            string.Join('|',neighbors.Select(neighbor=>neighbor.Map.Value).Order(StringComparer.Ordinal)));
        if(_questPaletteState==state)return;
        // Keep only the current scene revision and tone, never sixteen copies or prior maps.
        // This runs before acquiring map resources, so no evicted original remains a cache key.
        ClearQuestLogPaletteTextures();_questPaletteState=state;
    }

    private Texture2D QuestLogPaletteTexture(Texture2D original)
    {
        int coefficient=_questPaletteState?.Coefficient??0;
        if(coefficient==0)return original;
        if(_questPaletteTextures.TryGetValue(original,out var cached))return cached;
        using var image=original.GetImage();image.Convert(Image.Format.Rgba8);
        var pixels=image.GetData();QuestLogPalettePresentation.TransformRgba(pixels,coefficient);
        using var transformed=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,pixels);
        var texture=(_questPaletteOwner??=_mapResources.CreateOwner("quest-log-palette")).FromImage(transformed);
        _questPaletteTextures.Add(original,texture);return texture;
    }

    private void ClearQuestLogPaletteTextures()
    {
        _questPaletteTextures.Clear();_questPaletteOwner?.Dispose();_questPaletteOwner=null;_questPaletteState=null;
    }
}
