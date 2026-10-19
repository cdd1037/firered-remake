namespace Game.Application;

/// <summary>Presentation ownership across ordinary full-screen menus and battles.
/// Nested screens remain one absence; Start and Save keep the field alive.</summary>
public sealed class FieldReturnPresentation
{
    public enum Owner { Field, StartMenu, FullScreenMenu, Battle }
    private bool _away;
    public Controller? Arrival {get;private set;}
    public bool Suspended {get;private set;}

    public bool Observe(Owner owner,FieldArrivalPalette palette)
    {
        if(!Enum.IsDefined(owner)||!Enum.IsDefined(palette))throw new ArgumentOutOfRangeException();
        Suspended=owner is Owner.FullScreenMenu or Owner.Battle;
        if(Suspended){_away=true;Arrival=null;return false;}
        if(!_away)return false;
        _away=false;
        // Other weather return programs retain their existing presentation until
        // their complete palette/UI branch is admitted independently.
        if(palette!=FieldArrivalPalette.FogHorizontal)return false;
        Arrival=new(owner==Owner.StartMenu);return true;
    }
    public void Reset(){_away=false;Suspended=false;Arrival=null;}

    public sealed class BagNavigation
    {
        private bool _fromStart;
        public bool Observe(string command,bool accepted,string fieldMenuMode,string? beforeOrigin,string? afterOrigin)
        {
            if(!accepted)return false;
            if(command=="bag_open"&&afterOrigin=="bag")_fromStart=fieldMenuMode=="start";
            if(beforeOrigin!="bag"||afterOrigin=="bag")return false;
            bool reopen=_fromStart&&command=="menu_back"&&afterOrigin is null;
            _fromStart=false;
            return reopen;
        }
        public void Reset()=>_fromStart=false;
    }

    public sealed class Controller(bool startMenu)
    {
        public FieldArrivalPalettePresentation Palette {get;}=new(FieldArrivalPalette.FogHorizontal);
        public int Frames {get;private set;}
        public bool Complete {get;private set;}
        public int MenuPhase {get;private set;}
        public bool StartMenu {get;}=startMenu;
        public void Advance()
        {
            if(Complete)return;
            Frames++;
            // Script return task priority10 observes weather before priority80.
            if(!StartMenu&&Palette.WeatherReady)Complete=true;
            Palette.Advance();
            // Weather was inserted before the equal-priority menu wait task.
            // Creation does not run the new task in the same traversal.
            if(StartMenu&&Palette.WeatherReady)
            {
                if(MenuPhase==2)Complete=true;
                else MenuPhase++;
            }
        }
    }
}
