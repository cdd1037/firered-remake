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
        Arrival=new(owner==Owner.StartMenu,palette);return true;
    }
    public void Reset(){_away=false;Suspended=false;Arrival=null;}

    public sealed class BagNavigation
    {
        private bool _fromStart;
        public bool FromStart => _fromStart;
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

    public enum MenuScreen { Bag, Party, Options, TrainerCard, Pokedex }
    public enum MenuReturnPhase { InputCallback, Departure, ReturnCallback, BlackSetup, Arrival, Complete }

    /// <summary>One Advance is one original main callback, not a rendered GPU frame.
    /// F0 includes BeginNormalPaletteFade and the initiating callback's palette calls.</summary>
    public sealed class MenuReturn
    {
        private readonly MenuScreen _screen;
        private readonly FieldArrivalPalette _weather;
        private FieldWarpPresentation.NormalPalette? _departure;
        private int _inputCallbacks;
        private bool _fadeWaitObserved;
        public MenuReturnPhase Phase {get;private set;}
        public int Frames {get;private set;}
        public int DepartureFrame {get;private set;}=-1;
        public int ReturnFrame {get;private set;}=-1;
        public int FadeRequestReturnFrame {get;}
        public Controller? Arrival {get;private set;}
        public bool Complete=>Phase==MenuReturnPhase.Complete;
        public bool ShowsDepartingMenu=>Phase is MenuReturnPhase.InputCallback or MenuReturnPhase.Departure;
        public int BagBlackWipeHeight=>_screen==MenuScreen.Bag?Math.Clamp(16*(DepartureFrame+1),0,160):0;
        public int BackgroundBlend=>ShowsDepartingMenu?_departure?.BackgroundBlend??0:Arrival?.Palette.BackgroundBlend??16;
        public int ObjectBlend=>ShowsDepartingMenu?_departure?.ObjectBlend??0:Arrival?.Palette.ObjectBlend??16;

        public MenuReturn(MenuScreen screen,int startMenuEntries,FieldArrivalPalette weather)
        {
            if(!Enum.IsDefined(screen)||!Enum.IsDefined(weather))throw new ArgumentOutOfRangeException();
            if(startMenuEntries is <1 or >8)throw new ArgumentOutOfRangeException(nameof(startMenuEntries));
            _screen=screen;_weather=weather;
            // Black setup prints two Start entries per callback.
            FadeRequestReturnFrame=9+(startMenuEntries+1)/2;
            _inputCallbacks=screen switch {MenuScreen.Party or MenuScreen.TrainerCard=>0,MenuScreen.Pokedex=>2,_=>1};
            Phase=MenuReturnPhase.InputCallback;
            if(_inputCallbacks==0)BeginDeparture();
        }
        private void BeginDeparture()
        {
            Phase=MenuReturnPhase.Departure;DepartureFrame=0;
            _departure=new(false,_screen is MenuScreen.Bag or MenuScreen.Party?-2:0);
            // Dex's close callback has no outer UpdatePaletteFade.
            if(_screen!=MenuScreen.Pokedex)_departure.Advance();
        }
        public void Advance()
        {
            if(Complete)return;
            Frames++;
            if(Phase==MenuReturnPhase.InputCallback)
            {
                if(--_inputCallbacks==0)BeginDeparture();
                return;
            }
            if(Phase==MenuReturnPhase.Departure)
            {
                DepartureFrame++;
                bool selected;
                if(_screen==MenuScreen.TrainerCard)
                {
                    // Card closing task and its outer callback both update the palette.
                    _departure!.Advance();selected=!_departure.Active;_departure.Advance();
                }
                else
                {
                    selected=!_departure!.Active;
                    if(selected&&_screen is MenuScreen.Options or MenuScreen.Pokedex)
                    {selected=_fadeWaitObserved;_fadeWaitObserved=true;}
                    _departure.Advance();
                }
                if(selected)Phase=MenuReturnPhase.ReturnCallback;
                return;
            }
            if(Phase==MenuReturnPhase.ReturnCallback)
            {
                // Selection is not invocation: R0 invokes the return callback,
                // which selects the local field loader for the following callback.
                ReturnFrame=0;Phase=MenuReturnPhase.BlackSetup;return;
            }
            ReturnFrame++;
            if(ReturnFrame==FadeRequestReturnFrame)
            {Arrival=new(true,_weather);Phase=MenuReturnPhase.Arrival;}
            // Next callback installs VBlank; only the following one ticks weather.
            if(ReturnFrame>=FadeRequestReturnFrame+2)
            {Arrival!.Advance();if(Arrival.Complete)Phase=MenuReturnPhase.Complete;}
        }
    }

    public sealed class Controller(bool startMenu,FieldArrivalPalette palette=FieldArrivalPalette.FogHorizontal)
    {
        public FieldArrivalPalettePresentation Palette {get;}=new(palette);
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
