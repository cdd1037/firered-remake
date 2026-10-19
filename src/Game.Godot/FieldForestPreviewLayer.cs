using Godot;

/// <summary>One source BG0 layer: picture and name window share the same
/// RGB555 alpha operation against the already drawn field.</summary>
internal sealed partial class FieldForestPreviewLayer : Node2D
{
    public Texture2D? Picture {get;set;}
    public SourceGameFont? TextFont {get;set;}
    public string Label {get;set;}="";
    public Color WindowColor {get;set;}
    public bool ShowName {get;set;}
    public override void _Draw()
    {
        if(Picture is null)return;
        DrawTexture(Picture,Vector2.Zero);
        if(!ShowName||TextFont is not {} font)return;
        DrawRect(new(0,0,104,16),WindowColor);
        string text=font.PrepareText(Label);
        int width=(int)font.Font.GetStringSize(text,fontSize:font.Size).X;
        DrawString(font.Font,new((104-width)/2,2+font.Baseline),text,fontSize:font.Size);
    }
}
