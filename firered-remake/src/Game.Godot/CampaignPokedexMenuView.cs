using Game.Application;
using Game.Content;
using Godot;

// Read-only projection of the persistent seen/caught sets. Browsing never grants a dex flag.
public partial class CampaignView
{
    private PretPokedexMenu? _pokedexMenu;
    private PokedexMenuList? _pokedexList;
    private PokedexMenuCursor _pokedexCursor;
    private readonly Dictionary<string,Texture2D> _pokedexTextures=new(StringComparer.Ordinal);
    // Read the actual source grant. Loading the successor profile or importing metadata
    // never grants National mode or adds seen/caught species.
    private static PokedexMenuAccess PokedexAccessFor(CampaignState state)=>new(
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"),
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_NATIONAL_DEX"),
        state.NationalDexMagic,state.Script.Variables.GetValueOrDefault("VAR_NATIONAL_DEX"));
    private PokedexMenuAccess PokedexAccess=>PokedexAccessFor(_session.State);
    private int PokedexCaughtCount(CampaignState state)=>PretTrainerCard.CountCaught(
        state.CaughtSpecies.Select(id=>_pokedexMenu?.Entries[id].NationalNumber??id),PokedexAccessFor(state).NationalEnabled);
    private void LoadPokedexMenuPresentation()
    {
        if(_catalog.PokemonStorageEnabled)_pokedexMenu=_catalog.PokedexMenu?.ForProjectRoot(_root)??PretPokedexMenu.Load(_root);
    }
    private void OpenPokedexMenu()
    {
        if(_pokedexMenu is null||!_pokedexMenu.CanOpen(PokedexAccess,_session.State.SeenSpecies))
        {_fieldMenuNotice="The POKéDEX cannot be opened\nhere.";return;}
        _pokedexList=_pokedexMenu.BuildNumericalList(_session.State.SeenSpecies,_session.State.CaughtSpecies,PokedexAccess,nationalOrder:_session.NationalPokedexEnabled);
        _pokedexCursor=PretPokedexMenu.CenterDetailCursor(_pokedexList.Rows.Length,Math.Clamp(_pokedexCursor.SelectedIndex,0,_pokedexList.Rows.Length-1));
        _fieldMenuMode="pokedex";
    }
    private void OpenPokedexEntry()
    {
        if(_pokedexList!.Rows[_pokedexCursor.SelectedIndex].CanOpen)_fieldMenuMode="pokedex-entry";
    }
    private bool PokedexMenuInput(InputEventKey key)
    {
        if(_fieldMenuMode is not ("pokedex" or "pokedex-entry"))return false;
        if(!key.Pressed)return true;
        if(key.Keycode==Key.Backspace){_fieldMenuMode=_fieldMenuMode=="pokedex-entry"?"pokedex":"start";return true;}
        if(key.Keycode is Key.Up or Key.Down)
            _pokedexCursor=_fieldMenuMode=="pokedex-entry"?_pokedexMenu!.MoveDetail(_pokedexList!,_pokedexCursor,key.Keycode==Key.Up?-1:1)
                :_pokedexMenu!.MoveList(_pokedexList!,_pokedexCursor,key.Keycode==Key.Up?-1:1);
        if(_fieldMenuMode=="pokedex"&&key.Keycode is Key.Left or Key.Right)
            _pokedexCursor=_pokedexMenu!.MoveList(_pokedexList!,_pokedexCursor,key.Keycode==Key.Left?-1:1,page:true);
        if(_fieldMenuMode=="pokedex"&&key.Keycode==Key.Enter)OpenPokedexEntry();
        return true;
    }
    private bool MousePokedexMenu(Vector2 point)
    {
        if(_fieldMenuMode is not ("pokedex" or "pokedex-entry"))return false;
        if(new Rect2(176,144,64,16).HasPoint(point))return MouseKey(Key.Backspace);
        if(_fieldMenuMode=="pokedex-entry")
        {
            if(new Rect2(0,144,64,16).HasPoint(point))return MouseKey(Key.Up);
            if(new Rect2(80,144,72,16).HasPoint(point))return MouseKey(Key.Down);
            return false;
        }
        if(new Rect2(0,144,64,16).HasPoint(point))return MouseKey(Key.Left);
        if(new Rect2(80,144,72,16).HasPoint(point))return MouseKey(Key.Right);
        var layout=_pokedexMenu!.ListLayout;int rows=Math.Min(layout.VisibleRows,_pokedexList!.Rows.Length-_pokedexCursor.ScrollOffset);
        if(new Rect2(layout.X,layout.Y+layout.TopY,layout.WindowWidth,rows*layout.RowHeight).HasPoint(point))
        {
            int row=(int)((point.Y-layout.Y-layout.TopY)/layout.RowHeight);
            _pokedexCursor=new(_pokedexCursor.ScrollOffset,row);return MouseKey();
        }
        return false;
    }
    private Texture2D PokedexTexture(PokedexMenuTexture asset)
    {
        if(_pokedexTextures.TryGetValue(asset.TexturePath,out var cached))return cached;
        using var image=new Image();
        if(image.LoadPngFromBuffer(ReadPokedexTexture(asset))!=Error.Ok||image.GetWidth()!=asset.Width||image.GetHeight()!=asset.Height)throw new InvalidDataException("Pokédex texture dimensions differ.");
        image.Convert(Image.Format.Rgba8);var pixels=image.GetData();
        for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]!=0)for(int c=0;c<3;c++){int v=pixels[i+c]>>3;pixels[i+c]=(byte)((v<<3)|(v>>2));}
        using var display=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,pixels);
        var texture=SceneTextures.FromImage(display);_pokedexTextures.Add(asset.TexturePath,texture);return texture;
    }
    private void DrawPokedexMenu()
    {
        if(_fieldMenuMode=="pokedex-entry"){DrawPokedexEntry();return;}
        var list=_pokedexList!;var layout=_pokedexMenu!.ListLayout;
        _battleSkin.DrawStandardFrame(this,new(0,0,240,160));
        SourceText(_sourceSmall,new(12,2),$"{(list.NationalOrder?"NATIONAL":"KANTO")}  Seen:{list.SeenCount}  Owned:{list.CaughtCount}");
        for(int i=0;i<Math.Min(layout.VisibleRows,list.Rows.Length-_pokedexCursor.ScrollOffset);i++)
        {
            var row=list.Rows[_pokedexCursor.ScrollOffset+i];int y=layout.Y+layout.TopY+i*layout.RowHeight;
            SourceText(_sourceSmall,new(layout.X+layout.NumberX,y),row.NationalNumber.ToString("D3"));
            SourceText(_sourceSmall,new(layout.X+layout.NameX,y),row.DisplayName);
            if(row.Caught)DrawTexture(PokedexTexture(_pokedexMenu.OwnedMarker),new(layout.X+layout.CaughtX,y));
            if(row.Caught)
            {
                static string Type(string? value){var text=(value??"").Replace("TYPE_","",StringComparison.Ordinal);return text[..Math.Min(3,text.Length)];}
                SourceText(_sourceSmall,new(layout.X+layout.Type1X,y),Type(row.Type1));
                if(row.Type2!=row.Type1)SourceText(_sourceSmall,new(layout.X+layout.Type2X,y),Type(row.Type2));
            }
        }
        SourceText(_sourceSmall,new(layout.X+layout.CursorX,layout.Y+layout.TopY+_pokedexCursor.Row*layout.RowHeight),"▶");
        SourceText(_sourceSmall,new(8,146),"PREV");SourceText(_sourceSmall,new(88,146),"NEXT");SourceText(_sourceSmall,new(184,146),"BACK");
    }
    private void DrawPokedexEntry()
    {
        var row=_pokedexList!.Rows[_pokedexCursor.SelectedIndex];
        var page=PresentedPokedexPage(row.SourceSpeciesId);
        var layout=_pokedexMenu!.EntryLayout;var background=_captureDex!.Assets.Single(x=>x.Id=="capture-dex-background");
        DrawTexture(_captureTextures[background.Id],new(background.X,background.Y));
        if(page.Portrait is {} portrait)DrawTexture(PokedexTexture(portrait),new(layout.FrontX,layout.FrontY));
        if(page.Footprint is {} footprint)DrawTexture(PokedexTexture(footprint),new(layout.FootprintX,layout.FootprintY));
        const string title="POKéMON DATA";float titleWidth=_captureDexTitle.Font.GetStringSize(_captureDexTitle.PrepareText(title),fontSize:_captureDexTitle.Size).X;
        SourceText(_captureDexTitle,new((int)((layout.HeaderCenterWidth-titleWidth)/2),layout.HeaderY),title);
        SourceText(_captureDexSmall,new(layout.NumberX,layout.NumberY),page.NationalNumber.ToString("D3"));
        SourceText(_captureDexNormal,new(layout.NameX,layout.NameY),page.Name,layout.NameLetterSpacing);
        SourceText(_captureDexSmall,new(layout.CategoryX,layout.CategoryY),page.DisplayCategory+(page.Caught?layout.CategorySuffix:""),layout.StatsLetterSpacing);
        SourceText(_captureDexSmall,new(layout.HeightX,layout.HeightY),layout.HeightLabel);SourceText(_captureDexSmall,new(layout.HeightX+layout.MeasureValueOffsetX,layout.HeightY),page.HeightDisplay);
        SourceText(_captureDexSmall,new(layout.WeightX,layout.WeightY),layout.WeightLabel);SourceText(_captureDexSmall,new(layout.WeightX+layout.MeasureValueOffsetX,layout.WeightY),page.WeightDisplay);
        var lines=page.Description.Split('\n');float width=lines.Max(line=>_captureDexDescription.Font.GetStringSize(_captureDexDescription.PrepareText(line),fontSize:_captureDexDescription.Size).X);
        for(int i=0;i<lines.Length;i++)SourceText(_captureDexDescription,new((int)((layout.DescriptionCenterWidth-width)/2),layout.DescriptionY+i*(layout.DescriptionLineHeight+layout.DescriptionLineSpacing)),lines[i],layout.DescriptionLetterSpacing);
        SourceText(_captureDexFooter,new(8,layout.FooterY),"PREV");SourceText(_captureDexFooter,new(88,layout.FooterY),"NEXT");SourceText(_captureDexFooter,new(184,layout.FooterY),"BACK");
    }
}
