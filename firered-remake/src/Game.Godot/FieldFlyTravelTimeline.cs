/// <summary>
/// Presentation-only projection of pret 037335f4 field_effect.c Fly tasks and bird callbacks.
/// Tasks run before sprite callbacks. Completion is a callback/fade boundary, never a total-duration timer.
/// The shared show-mon renderer supplies its existing visual duration; this is not a GBA scheduler emulator.
/// </summary>
public sealed class FieldFlyTravelTimeline
{
    public enum PlayerPose { MapNormal, MapWest, Summon, MapRide, ScreenRide, Hidden }
    private enum BirdCallback { None, LeaveBall, Swoop, WithPlayer, ReturnToBall }
    private readonly bool _outgoing, _female;
    private readonly int _showMonFrames, _summonFrames;
    private BirdCallback _callback;
    private int _angle, _speed, _acceleration, _distance, _birdTicks;
    private bool _birdAttached, _birdDone;
    public string Stage { get; private set; } = "fade-in";
    public int StageFrame { get; private set; }
    public int Ticks { get; private set; }
    public int BirdX { get; private set; }
    public int BirdY { get; private set; }
    public int BirdFrame { get; private set; }
    public int BirdScale { get; private set; } = 256;
    public int BirdRotation { get; private set; }
    public bool BirdVisible { get; private set; }
    public bool BirdCompleted => _birdDone;
    public int PlayerX { get; private set; } = 120;
    public int PlayerY { get; private set; } = 64;
    public int PlayerYOffset { get; private set; }
    public bool SwoopSound { get; private set; }
    public bool Complete => Stage == "done";
    public int? ShowMonFrame => Stage == "show-mon" ? StageFrame : null;
    public int FadeLevel => Stage == "fade-in" ? Math.Max(0, 16 - StageFrame) : Stage == "fade-out" || (_outgoing && Complete) ? Math.Min(16, StageFrame) : 0;
    public int SummonFrame => Math.Min(4, (Stage == "show-mon" || Stage == "return-pose" ? StageFrame : _summonFrames) / 4);
    public PlayerPose Pose => Stage switch
    {
        "fade-in" => _outgoing ? PlayerPose.MapNormal : PlayerPose.Hidden,
        "show-mon" or "bird-leave" or "return-pose" or "bird-return" or "hold" => PlayerPose.Summon,
        "face-left" or "swoop" => PlayerPose.MapWest,
        "jump-on" => PlayerPose.MapRide,
        "fly-off" or "fade-out" or "ride-in" or "jump-off" or "wait-bird" => PlayerPose.ScreenRide,
        "done" when _outgoing => PlayerPose.ScreenRide,
        _ => PlayerPose.MapNormal
    };
    private static readonly int[] JumpOff = [-2,-4,-5,-6,-7,-8,-8,-8,-7,-7,-6,-5,-3,-2,0,2,4,8];
    private static readonly int[] JumpHigh = [-4,-6,-8,-10,-11,-12,-12,-12,-11,-10,-9,-8,-6,-4,0,0];
    // Exact Q8.8 first quadrant of the pinned gSineTable; remaining quadrants are symmetric.
    private static readonly int[] SineQuarter = [0,6,12,18,25,31,37,43,49,56,62,68,74,80,86,92,97,103,109,115,120,126,131,136,142,147,152,157,162,167,171,176,181,185,189,193,197,201,205,209,212,216,219,222,225,228,231,234,236,238,241,243,244,246,248,249,251,252,253,254,254,255,255,255,256];
    public static int Sin(int angle, int amplitude)
    {
        angle &= 255;
        int value = angle <= 64 ? SineQuarter[angle] : angle <= 128 ? SineQuarter[128-angle] : angle <= 192 ? -SineQuarter[angle-128] : -SineQuarter[256-angle];
        return value * amplitude >> 8;
    }
    public static int Cos(int angle, int amplitude) => Sin(angle + 64, amplitude);

    public FieldFlyTravelTimeline(bool outgoing, bool female, int showMonFrames, int summonFrames)
    {
        if (showMonFrames <= 0 || summonFrames <= 0) throw new ArgumentOutOfRangeException(nameof(showMonFrames));
        _outgoing = outgoing; _female = female; _showMonFrames = showMonFrames; _summonFrames = summonFrames;
    }
    private void Enter(string stage) { Stage = stage; StageFrame = 0; }
    private void StartBird(BirdCallback callback)
    {
        _callback = callback; _birdTicks = 0; _birdDone = false; _angle = _distance = 0; _speed = _acceleration = 0;
        _birdAttached = false; BirdScale = 256; BirdRotation = 0; BirdFrame = 0; BirdVisible = true;
    }
    public void Tick()
    {
        SwoopSound = false;
        if (Complete) return;
        Ticks++; StageFrame++;
        switch (Stage)
        {
            case "fade-in" when StageFrame >= 16:
                if (_outgoing) Enter("show-mon");
                else
                {
                    StartBird(BirdCallback.WithPlayer); _birdAttached = true; BirdFrame = (_female ? 2 : 0) + 2;
                    Enter("ride-in");
                }
                break;
            case "show-mon" when StageFrame >= _showMonFrames:
                StartBird(BirdCallback.LeaveBall); Enter("bird-leave"); break;
            case "bird-leave" when _birdDone: Enter("face-left"); break;
            case "face-left" when StageFrame >= 16:
                StartBird(BirdCallback.Swoop); SwoopSound = true; Enter("swoop"); break;
            case "swoop" when StageFrame >= 8:
                PlayerYOffset = JumpHigh[0]; Enter("jump-on"); break;
            case "jump-on":
                if (StageFrame >= 10)
                {
                    _callback = BirdCallback.WithPlayer; _birdTicks = 0; _birdAttached = true;
                    BirdFrame = (_female ? 2 : 0) + 1; BirdScale = 256; Enter("fly-off");
                }
                else PlayerYOffset = JumpHigh[StageFrame];
                break;
            case "fly-off" when _birdDone: Enter("fade-out"); break;
            case "fade-out" when StageFrame >= 16: Stage = "done"; break;
            case "ride-in":
                // TryChangeBirdSprite observes the previous sprite update's reciprocal matrix.
                // 256 + 512 followed by 32 * -16 gives unit scale at the 33-update gate.
                if (BirdScale == 256) { _callback = BirdCallback.Swoop; BirdFrame = 0; }
                if (StageFrame >= 33) { _birdAttached = false; PlayerYOffset = 0; Enter("jump-off"); }
                break;
            case "jump-off":
                PlayerYOffset = JumpOff[StageFrame - 1];
                if (StageFrame >= JumpOff.Length) Enter("wait-bird");
                break;
            case "wait-bird" when _birdDone:
                PlayerYOffset = 0; Enter("return-pose"); break;
            case "return-pose" when StageFrame >= _summonFrames:
                StartBird(BirdCallback.ReturnToBall); Enter("bird-return"); break;
            case "bird-return" when _birdDone:
                BirdVisible = false; _callback = BirdCallback.None; Enter("hold"); break;
            case "hold" when StageFrame >= 16: Stage = "done"; break;
        }
        if (!Complete) UpdateBird();
    }
    private void UpdateBird()
    {
        if (_callback == BirdCallback.None) return;
        switch (_callback)
        {
            case BirdCallback.LeaveBall:
                if (_birdDone) break;
                if (_birdTicks == 0) { _angle = 64; _speed = 256; BirdScale = 8; BirdRotation = -30; }
                _angle += _speed >> 8;
                BirdX = (_female ? 118 : 128) + Cos(_angle, 120); BirdY = -48 + Sin(_angle, 120);
                if (_speed < 2048) _speed += 96;
                if (_birdTicks > 0 && _birdTicks <= 30) BirdScale += 28;
                if (_angle > 129) { _birdDone = true; BirdScale = 256; BirdRotation = 0; }
                break;
            case BirdCallback.Swoop:
            case BirdCallback.WithPlayer:
                BirdX = 120 + Cos(_angle, _callback == BirdCallback.Swoop ? 140 : 180); BirdY = Sin(_angle, 72);
                _angle = (_angle + (_callback == BirdCallback.Swoop ? 4 : 2)) & 255;
                if (_birdAttached) { PlayerX = BirdX; PlayerY = BirdY - 8; PlayerYOffset = 0; }
                if (_callback == BirdCallback.WithPlayer)
                    BirdScale = _outgoing ? BirdScale + 24 : _birdTicks == 0 ? 768 : BirdScale - 16;
                if (_angle >= 128) { _birdDone = true; BirdScale = 256; }
                break;
            case BirdCallback.ReturnToBall:
                if (_birdDone) break;
                if (_birdTicks == 0) { _angle = 240; _speed = 2048; _acceleration = 128; BirdRotation = 64; BirdScale = 256; }
                _angle = (_angle + (_speed >> 8)) & 255; _distance += _speed >> 8;
                BirdX = (_female ? 100 : 112) + Cos(_angle, 32); BirdY = -32 + Sin(_angle, 120);
                if (_speed > 256) _speed -= _acceleration;
                if (_acceleration < 256) _acceleration += 24;
                if (_speed < 256) _speed = 256;
                if (_birdTicks > 0 && _birdTicks <= 22) BirdScale -= 10;
                if (_distance >= 60) { _birdDone = true; BirdVisible = false; BirdScale = 256; BirdRotation = 0; }
                break;
        }
        _birdTicks++;
    }
}
