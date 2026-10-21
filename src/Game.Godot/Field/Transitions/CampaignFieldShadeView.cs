using Godot;

public partial class CampaignView
{
    private readonly Dictionary<Texture2D,ImageTexture> _fieldShadeTextures=[];
    private Texture2D FieldShadeTexture(Texture2D original,bool background)
    {
        // Field BG banks 0..12 use NORMAL gamma; ordinary field objects use
        // ALT gamma, which is unchanged at source index 3.
        if(!background||(_session.State.FieldWeather??_catalog.MapDefinition(_session.State.World.Player.MapId).Weather)!="WEATHER_SHADE")return original;
        if(_fieldShadeTextures.TryGetValue(original,out var cached))return cached;
        using var image=original.GetImage();image.Convert(Image.Format.Rgba8);byte[] pixels=image.GetData();
        Game.Application.FieldShadePresentation.TransformRgba(pixels);
        using var shaded=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,pixels);
        var texture=ImageTexture.CreateFromImage(shaded);_fieldShadeTextures.Add(original,texture);return texture;
    }
    private void ClearFieldShadeTextures()
    {
        foreach(var texture in _fieldShadeTextures.Values)texture.Dispose();_fieldShadeTextures.Clear();
    }
}
