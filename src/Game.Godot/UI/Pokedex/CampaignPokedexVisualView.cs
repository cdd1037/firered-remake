using Game.Content;
using Godot;

public partial class CampaignView
{
    private string PokedexPalette => PokedexAccess.NationalEnabled?"national":"kanto";
    private void DrawPokedexVisual(string id,int x,int y)
        => DrawPokedexAreaSprite(_pokedexMenu!.Document.Visuals.Sprites[id],x,y);
    private void DrawPokedexSkin(string id)=>DrawPokedexVisual(PokedexPalette+"/"+id,0,0);
    private void DrawPokedexKey(string key,int x,string label)
    {
        var sprite=_pokedexMenu!.Document.Visuals.Sprites[PokedexPalette+"/key/"+key];
        DrawPokedexAreaSprite(sprite,x,146);
        SourceText(_sourceSmall,new(x+sprite.Width,146),label);
    }
    private void DrawPokedexTypes(PokedexMenuRow row,int x,int y)
    {
        if(!row.Caught)return;
        DrawPokedexVisual(row.Type1!,x,y);
        if(row.Type2 is not null && row.Type2!=row.Type1)DrawPokedexVisual(row.Type2!,x+32,y);
    }
    private Texture2D? _pokedexAreaIcon;
    private Texture2D? ResolvePokedexIcon(int species)
    {
        var appearance=PresentedDexAppearance(species);
        Texture2D? icon=null;
        if(species==201)_unownIcons.TryGetValue(appearance.Species,out icon);
        else
        {
            var entry=_pokemonCollectionArt?.Entries.FirstOrDefault(e=>e.SourceSpeciesId==species);
            if(entry is not null)_storageIcons.TryGetValue(entry.Species,out icon);
            // Existing loaded campaign species share this catalog, even when their art
            // predates the full collection import.
            if(icon is null)
                foreach(var pair in _storageIcons)
                    if(PresentedSpeciesId(pair.Key)==species){icon=pair.Value;break;}
        }
        return icon;
    }
    private void DrawPokedexAreaIdentity(PokedexMenuRow row)
    {
        if(_pokedexAreaIcon is {} icon)DrawTextureRectRegion(icon,new(8,16,32,32),new(0,0,32,32));
        SourceText(_sourceSmall,new(40,16),row.NationalNumber.ToString("D3"));
        SourceText(_sourceNormal,new(43,28),row.DisplayName);
        DrawPokedexTypes(row,40,41);
    }
}
