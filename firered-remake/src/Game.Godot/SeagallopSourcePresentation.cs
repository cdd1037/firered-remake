/// <summary>Discrete callbacks from authenticated src/seagallop.c and src/palette.c.
/// The caller supplies DMA readiness and actual music-stop state. This clock owns no game rules.</summary>
internal sealed class SeagallopSourcePresentation
{
    internal enum Stage {Setup,StartTravel,Travel,WaitFadeAndMusic,Complete}
    internal readonly record struct Cue(bool Ship,bool FadeMusic,bool Exit);
    internal readonly record struct Wake(int X,int Age,int Frame);
    internal const double FramesPerSecond=16777216d/280896d;
    internal const int MusicFadeTicks=16*4;
    internal Stage Phase {get;private set;}
    internal int Tick {get;private set;}
    internal int SetupState {get;private set;}
    internal int TravelTicks {get;private set;}
    internal int WaterX {get;private set;}
    internal int FerryX {get;private set;}
    internal bool FerryVisible {get;private set;}
    internal bool Eastbound {get;}
    internal int BackgroundFade {get;private set;}=16;
    internal int ObjectFade {get;private set;}=16;
    internal bool PaletteActive {get;private set;}
    internal IReadOnlyList<Wake> Wakes=>_wakes;
    private readonly List<Wake> _wakes=[];
    private int _ferryAge,_paletteY,_paletteTarget,_finishing=-1;
    private bool _objectToggle;
    private static readonly int[] Directions=[0x6fe,0x6fc,0x6f8,0x6f0,0x6e0,0x4c0,0x400,0x440,0x7ff,0x6e0,0];
    internal SeagallopSourcePresentation(int origin,int destination)
    {
        bool eventRoute=origin==0&&destination is 9 or 10||destination==0&&origin is 9 or 10;
        if(!eventRoute&&(origin is not (0 or 1 or 2 or 3 or 8)||destination is not (0 or 1 or 2 or 3 or 8)||origin==destination))
            throw new ArgumentOutOfRangeException(nameof(destination),"Ferry route lies outside first-Sevii scope.");
        Eastbound=((Directions[origin]>>destination)&1)!=0;FerryX=Eastbound?0:240;
    }
    internal Cue Advance(bool dmaReady,bool musicStopped)
    {
        if(Phase==Stage.Complete)return default;
        Tick++;bool ship=false,fade=false,exit=false;
        if(Phase==Stage.Setup)
        {
            if(SetupState==4&&!dmaReady)return default;
            if(SetupState==6)BeginPalette(16,0);
            if(SetupState==7){ship=true;FerryVisible=true;Phase=Stage.StartTravel;}
            SetupState++;return new(ship,false,false);
        }
        // MainCB2_SeaGallop: tasks, sprites, OAM, then palette update.
        if(Phase==Stage.StartTravel)Phase=Stage.Travel;
        else
        {
            WaterX=(WaterX+(Eastbound?6:-6))&255;
            if(Phase==Stage.Travel&&++TravelTicks==140)
            {fade=true;BeginPalette(0,16);Phase=Stage.WaitFadeAndMusic;}
            else if(Phase==Stage.WaitFadeAndMusic&&musicStopped&&!PaletteActive)
            {Phase=Stage.Complete;FerryVisible=false;_wakes.Clear();exit=true;return new(false,false,exit);}
        }
        for(int i=_wakes.Count-1;i>=0;i--)
        {
            var wake=_wakes[i];int age=wake.Age+1;
            if(age>=56)_wakes.RemoveAt(i);else _wakes[i]=new(wake.X,age,age<20?0:age<40?1:2);
        }
        if(FerryVisible)
        {
            int offset=(++_ferryAge)*3*(Eastbound?1:-1);FerryX=(Eastbound?0:240)+offset;
            if((_ferryAge-1)%5==0)_wakes.Add(new(FerryX,0,0));
            if(offset is < -300 or >300)FerryVisible=false;
        }
        UpdatePalette();return new(ship,fade,exit);
    }
    private void BeginPalette(int start,int target)
    {_paletteY=start;_paletteTarget=target;PaletteActive=true;_finishing=-1;UpdatePalette();}
    private void UpdatePalette()
    {
        if(!PaletteActive)return;
        if(_finishing>=0){if(_finishing++==4){PaletteActive=false;_finishing=-1;}return;}
        if(_objectToggle)ObjectFade=_paletteY;else BackgroundFade=_paletteY;
        _objectToggle=!_objectToggle;
        if(!_objectToggle)
        {if(_paletteY==_paletteTarget)_finishing=0;else _paletteY+=_paletteTarget>_paletteY?2:-2;}
    }
}
