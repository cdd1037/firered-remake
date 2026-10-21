using System.Text.Json;
using Game.Application;
using Game.Domain.World;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    private sealed record CutArtBindings(Dictionary<string,int> Layouts,Dictionary<int,int>[] Groups);
    private Texture2D? _cutGrassParticle;
    private int[]? _cutGrassSine;
    private void DrawCutGrassParticles(Vector2 camera)
    {
        if(_session.State.CutEffect is not {Grass:true,Phase:FieldCutSourcePresentation.GrassCut})return;
        if(_cutGrassParticle is null)
        {
            using var image=NativeImages.LoadPng(File.ReadAllBytes(Path.Combine(_root,"content/generated/ui/field-cut/grass.png")),8,8);
            _cutGrassParticle=ImageTexture.CreateFromImage(image);
            using var source=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_root,"content/generated/battle/animations/hit/manifest.json")));
            _cutGrassSine=source.RootElement.GetProperty("sineTable").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            if(_cutGrassSine.Length!=320)throw new InvalidDataException("cut_grass_source_sine");
        }
        // Creation and init leave x2/y2 at zero; the first Run callback follows.
        int tick=Math.Clamp(FieldCutFrame-2,0,28),radius=FieldCutFrame<2?0:8;
        for(int i=0;i<tick;i++)radius+=1+(i>>2);
        var player=_session.State.World.Player;var center=new Vector2(player.X*16+8,player.Y*16+8)-_art.Player.Anchor+new Vector2(8,20)-camera;
        for(int i=0;i<8;i++)
        {
            int angle=(i*32+tick*8)&255;
            DrawTexture(_cutGrassParticle,center+new Vector2((_cutGrassSine![angle]*radius)>>8,(_cutGrassSine[angle+64]*radius)>>8)-new Vector2(4,4));
        }
    }
    private void DrawBerryPowderAmount()
    {
        if(!_session.State.BerryPowderVendor)return;
        _itemSkin.DrawStandardFrame(this,new(0,0,80,40));
        SourceText(_sourceSmall,new(8,8),"POWDER");
        SourceText(_sourceSmall,new(47,20),_session.State.BerryPowder.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(5));
    }
}
