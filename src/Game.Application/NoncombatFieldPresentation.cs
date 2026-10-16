using Game.Domain.World;

namespace Game.Application;

/// <summary>Source field-effect motion, expressed as frame ticks for all renderers.</summary>
public sealed class FieldReturnTimeline
{
    private readonly bool _dig;
    private readonly Direction _original;
    private int _turnDelay, _turns, _age, _speed = 1, _speedDelay = 8;
    public string Stage { get; private set; }
    public Direction Facing { get; private set; }
    public int OffsetY { get; private set; }
    public int Fade { get; private set; }
    public string? Sound { get; private set; }
    public bool Complete => Stage == "done";
    public FieldReturnTimeline(string move, bool outgoing, Direction facing, int arrivalHeight = 96)
    {
        if (move is not (NoncombatFieldMoveRules.Dig or NoncombatFieldMoveRules.Teleport)) throw new ArgumentException("return_move", nameof(move));
        _dig = move == NoncombatFieldMoveRules.Dig; _original = Facing = facing;
        Stage = outgoing ? "spin" : "fade-in";
        if (!outgoing) { OffsetY = -(_dig ? 88 : arrivalHeight); Fade = 16; _speed = 8; }
    }
    private static Direction Spin(Direction direction) => direction switch
    { Direction.South => Direction.West, Direction.West => Direction.North, Direction.North => Direction.East, _ => Direction.South };
    private void Turn(int delay)
    {
        if (_turnDelay > 0 && --_turnDelay > 0) return;
        Facing = Spin(Facing); _turns++; _turnDelay = delay;
    }
    private void Enter(string stage) { Stage = stage; _age = 0; }
    public void Tick()
    {
        Sound = null;
        if (Complete) return;
        _age++;
        switch (Stage)
        {
            case "spin":
                Turn(_dig ? Math.Max(1, 12 >> Math.Min(_turns + 1, 12)) : 8);
                if (_dig && _age == 20) Sound = "SE_WARP_IN";
                if (_dig ? _age >= 60 : _turns > 7 && Facing == _original)
                { Enter("rise"); _turnDelay = 4; if (!_dig) Sound = "SE_WARP_IN"; }
                break;
            case "rise":
                Turn(_dig ? 1 : 4);
                OffsetY -= _dig ? 8 : _speed;
                if (!_dig && --_speedDelay <= 0) { _speedDelay = 4; _speed = Math.Min(8, _speed * 2); }
                if (OffsetY <= (_dig ? -88 : -168)) Enter("fade-out");
                break;
            case "fade-out":
                Fade = Math.Min(16, _age * 2);
                if (Fade == 16) Enter("done");
                break;
            case "fade-in":
                Fade = Math.Max(0, 16 - _age * 2);
                if (Fade == 0) { Enter("fall"); Sound = _dig ? "SE_WARP_OUT" : "SE_WARP_IN"; _turnDelay = 0; _turns = 0; }
                break;
            case "fall":
                OffsetY = Math.Min(0, OffsetY + (_dig ? 4 : _speed));
                if (_dig) { if (_age > 8) Turn(Math.Max(1, 12 >> Math.Min(_turns + 1, 12))); }
                else { Turn(4); if (OffsetY >= -48 && _speed > 1 && (OffsetY & 1) == 0) _speed--; }
                if (OffsetY == 0)
                { if (_dig) Sound = "SE_CLICK"; Enter("settle"); _turnDelay = 0; _turns = 0; }
                break;
            case "settle":
                Turn(_dig ? 1 : 8);
                if ((_dig ? _age >= 28 : _turns > 4) && Facing == _original) Enter("done");
                break;
        }
    }
}

/// <summary>palette.c delay=4, deltaY=2, alternating BG/OBJ, five final drain frames,
/// followed by fldeff_sweetscent.c's separate 64-frame wait.</summary>
public sealed class SweetScentTimeline
{
    private readonly bool _reverse;
    private int _delay, _coefficient, _finishing, _hold;
    private bool _objectPhase = true, _finishedFade;
    public int BackgroundCoefficient { get; private set; }
    public int ObjectCoefficient { get; private set; }
    public bool Complete { get; private set; }
    public SweetScentTimeline(bool reverse)
    {
        _reverse = reverse; _coefficient = reverse ? 8 : 0;
        BackgroundCoefficient = ObjectCoefficient = _coefficient;
        // BeginNormalPaletteFade immediately performs the first BG update.
    }
    public void Tick()
    {
        if (Complete) return;
        if (_finishedFade)
        {
            if (_finishing++ < 4) return;
            if (_reverse || _hold++ == 64) Complete = true;
            return;
        }
        if (!_objectPhase && _delay++ < 4) return;
        if (_objectPhase) ObjectCoefficient = _coefficient;
        else { BackgroundCoefficient = _coefficient; _delay = 0; }
        _objectPhase = !_objectPhase;
        if (!_objectPhase)
        {
            if (_coefficient == (_reverse ? 0 : 8)) _finishedFade = true;
            else _coefficient += _reverse ? -2 : 2;
        }
    }
}
