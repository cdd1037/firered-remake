using Game.Content;
using Godot;

public partial class CampaignView
{
    private PokedexHabitat[] _pokedexHabitats=[];
    private int _pokedexContentsCursor,_pokedexHabitatIndex,_pokedexHabitatPage,_pokedexHabitatSlot;
    private string _pokedexDetailReturn="pokedex",_pokedexHabitatReturn="pokedex-contents";
    private PokedexMenuList? _pokedexBeforeHabitatList;
    private PokedexMenuCursor _pokedexBeforeHabitatCursor;

    private void OpenPokedexHabitat(int category,string returnMode,int species=0)
    {
        if(_pokedexHabitats[category].Pages.IsEmpty)return;
        _pokedexHabitatIndex=category;_pokedexHabitatReturn=returnMode;
        _pokedexBeforeHabitatList=_pokedexList;_pokedexBeforeHabitatCursor=_pokedexCursor;
        _pokedexHabitatPage=0;_pokedexHabitatSlot=0;
        if(species!=0)
            for(int p=0;p<_pokedexHabitats[category].Pages.Length;p++)
                for(int i=0;i<_pokedexHabitats[category].Pages[p].Rows.Length;i++)
                    if(_pokedexHabitats[category].Pages[p].Rows[i].SourceSpeciesId==species)
                    {_pokedexHabitatPage=p;_pokedexHabitatSlot=i;}
        SetPokedexHabitatPage();_fieldMenuMode="pokedex-habitat";
    }
    private void OpenPokedexHabitatForSpecies(int species)
    {
        int category=Array.FindIndex(_pokedexHabitats,h=>h.Pages.Any(p=>p.Rows.Any(r=>r.SourceSpeciesId==species)));
        if(category>=0)OpenPokedexHabitat(category,"pokedex",species);
    }
    private void SetPokedexHabitatPage()
    {
        var rows=_pokedexHabitats[_pokedexHabitatIndex].Pages[_pokedexHabitatPage].Rows;
        _pokedexHabitatSlot=Math.Clamp(_pokedexHabitatSlot,0,rows.Length-1);
        _pokedexList=new(rows,_pokedexBeforeHabitatList?.SeenCount??0,_pokedexBeforeHabitatList?.CaughtCount??0,PokedexAccess.NationalEnabled);
        _pokedexCursor=new(0,_pokedexHabitatSlot);
    }
    private bool PokedexExtraInput(InputEventKey key)
    {
        if(_fieldMenuMode is not ("pokedex-contents" or "pokedex-habitat" or "pokedex-area"))return false;
        if(!key.Pressed)return true;
        if(_fieldMenuMode=="pokedex-area")
        {
            if(key.Keycode==Key.Tab)TryPokedexCryStart(true);
            else if(key.Keycode==Key.Backspace)_fieldMenuMode="pokedex-entry";
            else if(key.Keycode==Key.Enter){CancelPokedexCry();_fieldMenuMode=_pokedexDetailReturn;}
            return true;
        }
        if(_fieldMenuMode=="pokedex-contents")
        {
            if(key.Keycode==Key.Backspace)_fieldMenuMode="start";
            else if(key.Keycode==Key.Up)_pokedexContentsCursor=Math.Max(0,_pokedexContentsCursor-1);
            else if(key.Keycode==Key.Down)_pokedexContentsCursor=Math.Min(_pokedexHabitats.Length,_pokedexContentsCursor+1);
            else if(key.Keycode==Key.R)OpenPokedexOrderMenu();
            else if(key.Keycode==Key.Enter)
            {
                if(_pokedexContentsCursor==_pokedexHabitats.Length)OpenPokedexOrderMenu();
                else OpenPokedexHabitat(_pokedexContentsCursor,"pokedex-contents");
            }
            return true;
        }
        if(key.Keycode==Key.Backspace)
        {_pokedexList=_pokedexBeforeHabitatList;_pokedexCursor=_pokedexBeforeHabitatCursor;_fieldMenuMode=_pokedexHabitatReturn;return true;}
        if(key.Keycode==Key.Enter)
        {_pokedexDetailReturn="pokedex-habitat";_fieldMenuMode="pokedex-entry";PlayPokedexCry();return true;}
        if(key.Keycode is Key.Left or Key.Right)
        {
            int direction=key.Keycode==Key.Left?-1:1;
            int next=_pokedexHabitatSlot+direction;
            if(next>=0&&next<_pokedexList!.Rows.Length)_pokedexHabitatSlot=next;
            else
            {
                int page=_pokedexHabitatPage+direction;
                if(page<0||page>=_pokedexHabitats[_pokedexHabitatIndex].Pages.Length)return true;
                _pokedexHabitatPage=page;
                _pokedexHabitatSlot=direction<0?_pokedexHabitats[_pokedexHabitatIndex].Pages[page].Rows.Length-1:0;
            }
            SetPokedexHabitatPage();
        }
        return true;
    }
    private bool MousePokedexExtra(Vector2 point)
    {
        if(_fieldMenuMode=="pokedex-contents")
        {
            if(new Rect2(176,144,64,16).HasPoint(point))return MouseKey(Key.Backspace);
            return MouseSelect(point,ref _pokedexContentsCursor,_pokedexHabitats.Length+1,16,20,208,12);
        }
        if(_fieldMenuMode=="pokedex-area")
        {
            if(point.Y>=144)return MouseKey(point.X<80?Key.Tab:point.X<176?Key.Enter:Key.Backspace);
            return true;
        }
        if(_fieldMenuMode!="pokedex-habitat")return false;
        if(point.Y>=144)return MouseKey(point.X<56?Key.Left:point.X<112?Key.Right:Key.Backspace);
        int count=_pokedexList!.Rows.Length;
        for(int i=0;i<count;i++)
            if(new Rect2(_pokedexMenu!.Document.HabitatLayouts[count-1][i].PortraitX,_pokedexMenu.Document.HabitatLayouts[count-1][i].PortraitY,64,64).HasPoint(point)||
               new Rect2(_pokedexMenu.Document.HabitatLayouts[count-1][i].InfoX,_pokedexMenu.Document.HabitatLayouts[count-1][i].InfoY,64,32).HasPoint(point))
            {_pokedexHabitatSlot=i;SetPokedexHabitatPage();return MouseKey(Key.Enter);}
        return true;
    }
    private bool NavigatePokedexExtraPointer(Vector2 point,int direction)
    {
        if(_fieldMenuMode=="pokedex-contents")return PointerRows(point,direction,ref _pokedexContentsCursor,_pokedexHabitats.Length+1,16,20,208,12);
        return false;
    }
    private bool DrawPokedexExtra()
    {
        if(_fieldMenuMode=="pokedex-area"){DrawPokedexAreaPage();return true;}
        if(_fieldMenuMode is not ("pokedex-contents" or "pokedex-habitat"))return false;
        _battleSkin.DrawStandardFrame(this,new(0,0,240,160));
        if(_fieldMenuMode=="pokedex-contents")
        {
            SourceText(_sourceSmall,new(12,2),_pokedexMenu!.UiTexts["gText_PokedexTableOfContents"]);
            for(int i=0;i<_pokedexHabitats.Length;i++)SourceText(_sourceSmall,new(24,20+i*12),
                _pokedexHabitats[i].Pages.IsEmpty?"-----":_pokedexHabitats[i].Title);
            SourceText(_sourceSmall,new(24,20+_pokedexHabitats.Length*12),"SEARCH / NUMERICAL");
            SourceText(_sourceSmall,new(16,20+_pokedexContentsCursor*12),"▶");
            SourceText(_sourceSmall,new(8,146),"ENTER SELECT");SourceText(_sourceSmall,new(184,146),"BACK");return true;
        }
        var habitat=_pokedexHabitats[_pokedexHabitatIndex];
        SourceText(_sourceSmall,new(12,2),habitat.Title);
        SourceText(_sourceSmall,new(168,2),$"{_pokedexHabitatPage+1}/{habitat.Pages.Length}");
        for(int i=0;i<_pokedexList!.Rows.Length;i++)
        {
            var row=_pokedexList.Rows[i];var page=CachedPokedexPage(row.SourceSpeciesId);
            var slot=_pokedexMenu!.Document.HabitatLayouts[_pokedexList.Rows.Length-1][i];
            if(page.Portrait is {} portrait)DrawTexture(PokedexTexture(portrait),new(slot.PortraitX,slot.PortraitY));
            SourceText(_sourceSmall,new(slot.InfoX+12,slot.InfoY),row.NationalNumber.ToString("D3"));
            SourceText(_sourceSmall,new(slot.InfoX+2,slot.InfoY+13),row.DisplayName);
            if(row.Caught)DrawTextureRect(PokedexTexture(_pokedexMenu.OwnedMarker),new(slot.InfoX+2,slot.InfoY+3,8,8),false);
            if(i==_pokedexHabitatSlot)DrawRect(new(slot.InfoX,slot.InfoY,64,32),new Color("c82828"),false);
        }
        SourceText(_sourceSmall,new(8,146),"PREV");SourceText(_sourceSmall,new(64,146),"NEXT");SourceText(_sourceSmall,new(184,146),"BACK");return true;
    }
    private void DrawPokedexAreaPage()
    {
        _battleSkin.DrawStandardFrame(this,new(0,0,240,160));
        var row=_pokedexList!.Rows[_pokedexCursor.SelectedIndex];
        SourceText(_sourceSmall,new(8,2),$"{row.NationalNumber:D3} {row.DisplayName}");
        SourceText(_sourceSmall,new(28,64),_pokedexMenu!.UiTexts["gText_Size"]);
        DrawPokedexSize(row.SourceSpeciesId);
        DrawPokedexArea();
        SourceText(_sourceSmall,new(8,146),"Tab CRY");SourceText(_sourceSmall,new(88,146),"ENTER EXIT");SourceText(_sourceSmall,new(184,146),"DATA");
    }
    private readonly Dictionary<int,PokedexMenuPage> _pokedexPresentedPages=[];
    private PokedexSizeTransform? _pokedexSizeComparison;
    private PokedexMenuPage CachedPokedexPage(int species)
    {
        if(!_pokedexPresentedPages.TryGetValue(species,out var page))
        {_pokedexPresentedPages.Add(species,page=PresentedPokedexPage(species));}
        return page;
    }
    private void OpenPokedexArea()
    {
        var species=_pokedexList!.Rows[_pokedexCursor.SelectedIndex].SourceSpeciesId;
        _pokedexSizeComparison=_pokedexMenu!.BuildSizeComparison(species,_session.State.SeenSpecies,_session.State.CaughtSpecies,PokedexAccess);
        PreparePokedexArea();_fieldMenuMode="pokedex-area";
    }
    private void DrawPokedexSize(int species)
    {
        var transform=_pokedexSizeComparison;
        if(transform is null)return;
        var page=CachedPokedexPage(species);
        if(page.Portrait is {} portrait)DrawPokedexSilhouette(PokedexTexture(portrait),new(40,104+transform.PokemonOffset),transform.PokemonScale);
        var trainer=_leagueTextures[_session.State.PlayerGender==0?"red-front":"leaf-front"];
        DrawPokedexSilhouette(trainer,new(80,104+transform.TrainerOffset),transform.TrainerScale);
    }
    private readonly Dictionary<ulong,Texture2D> _pokedexSilhouettes=[];
    private void DrawPokedexSilhouette(Texture2D texture,Vector2 center,int inverseScale)
    {
        // OAM affine NORMAL samples within a 64x64 sprite box; coefficients are inverse 8.8 scale.
        float scale=256f/inverseScale;float size=64*scale;
        var destination=new Rect2(center-new Vector2(size/2,size/2),new(size,size));
        var clipped=destination.Intersection(new Rect2(center-new Vector2(32,32),new(64,64)));
        var source=new Rect2((clipped.Position-destination.Position)/scale,clipped.Size/scale);
        if(!_pokedexSilhouettes.TryGetValue(texture.GetInstanceId(),out var silhouette))
        {
            using var image=texture.GetImage();image.Convert(Image.Format.Rgba8);var pixels=image.GetData();
            var rgb=_pokedexMenu!.Document.SilhouetteRgb;
            for(int i=0;i<pixels.Length;i+=4)for(int c=0;c<3;c++)pixels[i+c]=(byte)rgb[c];
            using var converted=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,pixels);
            silhouette=SceneTextures.FromImage(converted);_pokedexSilhouettes.Add(texture.GetInstanceId(),silhouette);
        }
        DrawTextureRectRegion(silhouette,clipped,source);
    }
}
