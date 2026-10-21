using Game.Domain.Tutorials;

public enum TeachyTvPresentationAction { Navigate, ConfirmMenu, Acknowledge, ConfirmText, Cancel, Select, BeginDemonstration }
public enum TeachyTvButton { A, B, Select, Up, Down, Left, Right }
public sealed record TeachyTvPresentationRequest(TeachyTvPresentationAction Action, string WaitId, int Value = 0);
public readonly record struct TeachyTvHostFrame(int Image, bool FlipHorizontal);
public sealed record TeachyTvGrassFrame(int X, int Y, int Age)
{
    public int Image => Age < 40 ? 1 + Age / 10 : 0;
    public bool SplitBehindHost => Age < 10;
}

/// <summary>Read-only 60 Hz adapter for the source outer controller. The Application owns the only
/// TeachyTvPlaybackState. Every request carries its current token; external demonstrations must
/// execute and restore their own temporary transaction before Application resumes the lesson.</summary>
public sealed class TeachyTvPresentation
{
    private readonly Queue<TeachyTvPresentationRequest> _requests = new();
    private TeachyTvInstruction? _instruction;
    private string? _outstanding;
    private TeachyTvButton? _heldDirection;
    private int _repeat, _instructionFrames, _hostAnimation, _hostAnimationFrames, _entryFade;
    private double _fraction;
    private TeachyTvPresentationAction? _closingAction;
    private int _closingFrames;
    private readonly List<TeachyTvGrassFrame> _grass = [];
    private int _grassColumn, _grassRow = 3;
    public IReadOnlyList<TeachyTvGrassFrame> Grass => _grass;
    public TeachyTvCatalog? Catalog { get; private set; }
    public TeachyTvPlaybackState? State { get; private set; }
    public TeachyTvTextPresentation Text { get; } = new();
    public long Frame { get; private set; }
    public int ConfiguredTextSpeed { get; private set; } = 4;
    public int HostX { get; private set; } = 8;
    public int HostY => 56;
    public int FieldScrollX { get; private set; } = -16;
    public int FieldScrollY { get; private set; } = 40;
    public bool HostVisible { get; private set; }
    public bool StaticVisible { get; private set; } = true;
    public bool TitleVisible { get; private set; }
    public bool EndVisible { get; private set; }
    public bool OwnsOuterScreen => State is { Phase: not (TeachyTvPhase.Closed or TeachyTvPhase.AwaitingDemonstration) };
    public int FadeCoefficient => _closingAction.HasValue ? Math.Min(16, _closingFrames) : _entryFade;
    public int InstructionFrames => _instructionFrames;
    public event Action<string>? MusicRequested;
    public event Action<string>? SoundRequested;

    public void Synchronize(TeachyTvCatalog catalog, TeachyTvPlaybackState? state, int configuredTextSpeed)
    {
        if (configuredTextSpeed is not (8 or 4 or 1)) throw new ArgumentOutOfRangeException(nameof(configuredTextSpeed));
        bool newSession = state?.SessionId != State?.SessionId;
        var previous = State;
        Catalog = catalog; State = state; ConfiguredTextSpeed = configuredTextSpeed;
        if (state is null || state.Phase == TeachyTvPhase.Closed)
        { _instruction = null; _requests.Clear(); _outstanding = null; _heldDirection = null; Text.Clear(); return; }
        if (newSession)
        {
            Frame = 0; _fraction = 0; _entryFade = 16; _requests.Clear(); _outstanding = null;
            _closingAction = null; _heldDirection = null; ResetMenu(); MusicRequested?.Invoke("MUS_TEACHY_TV_MENU");
        }
        if (previous?.WaitId == state.WaitId) return;
        _outstanding = null; _instructionFrames = 0; _instruction = TeachyTvPlaybackRules.Current(catalog, state);
        if (state.Phase == TeachyTvPhase.Menu)
        {
            if (previous?.Phase != TeachyTvPhase.Menu)
            {
                ResetMenu();
                if (previous?.Phase == TeachyTvPhase.AwaitingDemonstration) { _entryFade = 16; MusicRequested?.Invoke("MUS_TEACHY_TV_MENU"); }
            }
            return;
        }
        _heldDirection = null;
        if (state.Phase == TeachyTvPhase.AwaitingDemonstration) { Text.Clear(); return; }
        if (previous?.Phase == TeachyTvPhase.AwaitingDemonstration)
        {
            _entryFade = 16; HostVisible = true; HostX = 120; SetHostAnimation(0);
            StaticVisible = TitleVisible = EndVisible = false;
            bool battle = catalog.Lessons[state.LessonIndex].DemonstrationKind == TeachyTvDemonstrationKind.PokedudeBattle;
            FieldScrollX = battle ? 32 : -16; FieldScrollY = battle ? -8 : 40;
            _grassColumn = battle ? 3 : 0; _grassRow = battle ? 0 : 3;
            SpawnGrass(120, 56, settled: true);
        }
        EnterInstruction();
    }

    private void ResetMenu()
    {
        HostVisible = TitleVisible = EndVisible = false; StaticVisible = true;
        HostX = 8; FieldScrollX = -16; FieldScrollY = 40; Text.Clear();
        _grass.Clear(); _grassColumn = 0; _grassRow = 3;
        _closingAction = null; _closingFrames = 0;
    }

    private void EnterInstruction()
    {
        switch (_instruction?.SourceCommand)
        {
            case "TTVcmd_TransitionRenderBg2TeachyTvGraphicInitNpcPos":
                StaticVisible = true; TitleVisible = EndVisible = HostVisible = false; Text.Clear(); break;
            case "TTVcmd_ClearBg2TeachyTvGraphic":
                StaticVisible = false; TitleVisible = true; HostVisible = true; HostX = 8;
                SetHostAnimation(7); MusicRequested?.Invoke("MUS_FOLLOW_ME"); break;
            case "TTVcmd_NpcMoveAndSetupTextPrinter": TitleVisible = false; break;
            case "TTVcmd_TextPrinterSwitchStringByOptionChosen":
            case "TTVcmd_TextPrinterSwitchStringByOptionChosen2": BeginText(); break;
            case "TTVcmd_StartAnimNpcWalkIntoGrass": Text.Clear(); SetHostAnimation(5); break;
            case "TTVcmd_DudeMoveRight": SetHostAnimation(7); break;
            case "TTVcmd_DudeTurnLeft": Text.Clear(); SetHostAnimation(6); SpawnGrass(HostX, 56); break;
            case "TTVcmd_RenderAndRemoveBg1EndGraphic": EndVisible = true; break;
            case "TTVcmd_End":
                HostVisible = EndVisible = TitleVisible = false; StaticVisible = true; Text.Clear();
                _grass.Clear();
                MusicRequested?.Invoke("MUS_TEACHY_TV_MENU"); break;
        }
    }
    private void BeginText()
    {
        string symbol = _instruction?.TextSymbol ?? throw new InvalidOperationException("Teachy TV instruction has no source text.");
        Text.Begin(symbol, Catalog!.Texts[symbol].EncodedText, ConfiguredTextSpeed);
    }
    private void SetHostAnimation(int animation) { _hostAnimation = animation; _hostAnimationFrames = 0; }
    public TeachyTvHostFrame HostFrame
    {
        get
        {
            int phase = _hostAnimationFrames / 8 % 4;
            return _hostAnimation switch
            {
                0 => new(0, false), 1 => new(1, false), 2 => new(2, false), 3 => new(2, true),
                5 => new(new[] { 5, 1, 6, 1 }[phase], false),
                6 => new(new[] { 7, 2, 8, 2 }[phase], false),
                7 => new(new[] { 7, 2, 8, 2 }[phase], true),
                _ => throw new InvalidOperationException("Unsupported Teachy TV host animation.")
            };
        }
    }
    public bool TryTakeRequest(out TeachyTvPresentationRequest? request)
    {
        if (_requests.Count == 0) { request = null; return false; }
        request = _requests.Dequeue(); return true;
    }
    private void Request(TeachyTvPresentationAction action, int value = 0)
    {
        if (State is null || _outstanding == State.WaitId) return;
        _outstanding = State.WaitId; _requests.Enqueue(new(action, State.WaitId, value));
    }
    public bool Advance(double delta)
    {
        if (!OwnsOuterScreen) return false;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _fraction += Math.Min(delta, .25) * 60;
        int frames = (int)(_fraction + 1e-9); _fraction -= frames;
        AdvanceFrames(frames); return true;
    }
    public void AdvanceFrames(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        for (int i = 0; i < frames && OwnsOuterScreen; i++) AdvanceFrame();
    }
    private void AdvanceFrame()
    {
        Frame++; _hostAnimationFrames++;
        if (_entryFade > 0) { _entryFade--; return; }
        if (_closingAction is {} close)
        {
            if (++_closingFrames >= 16) Request(close);
            return;
        }
        if (_outstanding == State!.WaitId) return;
        Text.AdvanceFrame();
        AdvanceGrass();
        if (State.Phase == TeachyTvPhase.Menu)
        {
            if (_heldDirection is {} held && --_repeat <= 0) { Navigate(held); _repeat = 5; }
            return;
        }
        _instructionFrames++;
        switch (_instruction?.SourceCommand)
        {
            case "TTVcmd_TransitionRenderBg2TeachyTvGraphicInitNpcPos": AckAfter(64); break;
            case "TTVcmd_ClearBg2TeachyTvGraphic": AckAfter(134); break;
            case "TTVcmd_NpcMoveAndSetupTextPrinter":
                if (_instructionFrames > 35 && HostX < 120) HostX++;
                else if (_instructionFrames > 35) { SetHostAnimation(0); BeginText(); Ack(); }
                break;
            case "TTVcmd_IdleIfTextPrinterIsActive":
            case "TTVcmd_IdleIfTextPrinterIsActive2": if (Text.Complete) Ack(); break;
            case "TTVcmd_TextPrinterSwitchStringByOptionChosen":
            case "TTVcmd_TextPrinterSwitchStringByOptionChosen2":
            case "TTVcmd_StartAnimNpcWalkIntoGrass":
            case "TTVcmd_DudeTurnLeft": Ack(); break;
            case "TTVcmd_DudeMoveUp":
                FieldScrollY = 40 - Math.Min(48, _instructionFrames);
                if (_instructionFrames % 16 == 0) { _grassRow--; SpawnGrass(HostX, 56); }
                AckAfter(48); break;
            case "TTVcmd_DudeMoveRight":
                FieldScrollX = -16 + Math.Min(48, _instructionFrames);
                if (_instructionFrames % 16 == 0) _grassColumn++;
                if ((_instructionFrames + 8) % 16 == 0) SpawnGrass(HostX + 8, 56);
                if (_instructionFrames >= 48) { SetHostAnimation(3); Ack(); } break;
            case "TTVcmd_DudeMoveLeft":
                if (HostX % 16 == 0) SpawnGrass(HostX - 8, 56);
                if (HostX == 8) Ack(); else HostX--; break;
            case "TTVcmd_RenderAndRemoveBg1EndGraphic": AckAfter(127); break;
            case "TTVcmd_End": AckAfter(64); break;
            case "TTVcmd_TaskBattleOrFadeByOptionChosen": Request(TeachyTvPresentationAction.BeginDemonstration); break;
            case "TTVcmd_EraseTextWindowIfKeyPressed": break;
            default: throw new InvalidOperationException("Unsupported Teachy TV source command: " + _instruction?.SourceCommand);
        }
    }
    private void AckAfter(int frames) { if (_instructionFrames >= frames) Ack(); }
    private void Ack() => Request(TeachyTvPresentationAction.Acknowledge);

    private void SpawnGrass(int x, int y, bool settled = false)
    {
        if (x < 16 || y < 0) return;
        int column = ((x - 16) >> 4) + _grassColumn, row = (y >> 4) + _grassRow;
        bool grass = row is >= 0 and <= 4 && column is >= 2 and <= 13 || row is 7 or 8 && column is >= 8 and <= 13;
        if (grass) _grass.Add(new(x, y + 8, settled ? 40 : 0));
    }
    private void AdvanceGrass()
    {
        int dx = _instruction?.SourceCommand == "TTVcmd_DudeMoveRight" ? -1 : 0;
        int dy = _instruction?.SourceCommand == "TTVcmd_DudeMoveUp" ? 1 : 0;
        for (int i = _grass.Count - 1; i >= 0; i--)
        {
            var g = _grass[i] with { X = _grass[i].X + dx, Y = _grass[i].Y + dy, Age = Math.Min(50, _grass[i].Age + 1) };
            if (g.Age >= 50 && (g.X - HostX <= -16 || g.X - HostX >= 16 || g.Y - HostY <= -16 || g.Y - HostY >= 24)) _grass.RemoveAt(i);
            else _grass[i] = g;
        }
    }

    public void ReleaseInput()
    {
        _heldDirection = null; _repeat = 0; Text.ReleaseInput();
    }

    public bool Input(TeachyTvButton button, bool pressed)
    {
        if (!OwnsOuterScreen) return false;
        if (!pressed)
        {
            if (button == TeachyTvButton.A) Text.Confirm(false);
            if (button == _heldDirection) _heldDirection = null;
            return true;
        }
        if (_entryFade > 0 || _closingAction.HasValue) return true;
        if (button == TeachyTvButton.B)
        {
            // The outer task checks new B before ANY command or text prompt.
            // Also allow escape after an Application request was rejected. Discard
            // only this view's not-yet-applied request, never an active external demo.
            _requests.Clear(); _outstanding = null;
            if (State!.Phase == TeachyTvPhase.Menu) StartClose(TeachyTvPresentationAction.Cancel);
            else if (State.Phase == TeachyTvPhase.Script) Request(TeachyTvPresentationAction.Cancel);
            return true;
        }
        if (_outstanding == State!.WaitId) return true;
        if (State.Phase == TeachyTvPhase.ReturningToMenu) return true;
        if (State.Phase == TeachyTvPhase.Menu)
        {
            switch (button)
            {
                case TeachyTvButton.A:
                    if (TeachyTvPlaybackRules.Menu(Catalog!, State)[State.ScrollOffset + State.SelectedRow].SourceIndex == -2)
                        StartClose(TeachyTvPresentationAction.ConfirmMenu);
                    else { SoundRequested?.Invoke("SE_SELECT"); Request(TeachyTvPresentationAction.ConfirmMenu); }
                    break;
                case TeachyTvButton.Select:
                    if (State.Origin == TeachyTvOrigin.RegisteredItem) StartClose(TeachyTvPresentationAction.Select);
                    break;
                case TeachyTvButton.Up: case TeachyTvButton.Down: case TeachyTvButton.Left: case TeachyTvButton.Right:
                    Navigate(button); _heldDirection = button; _repeat = 20; break;
            }
        }
        else if (button == TeachyTvButton.A)
        {
            if (_instruction?.Kind == TeachyTvInstructionKind.Confirm)
            { Text.Clear(); Request(TeachyTvPresentationAction.ConfirmText); }
            else Text.Confirm(true);
        }
        return true;
    }
    private void StartClose(TeachyTvPresentationAction action)
    { SoundRequested?.Invoke("SE_SELECT"); _closingAction = action; _closingFrames = 0; _heldDirection = null; }
    private void Navigate(TeachyTvButton button)
    {
        int previous = State!.ScrollOffset + State.SelectedRow;
        int delta = button is TeachyTvButton.Up or TeachyTvButton.Left ? -1 : 1;
        if (button is TeachyTvButton.Left or TeachyTvButton.Right) delta *= State.HasTmCase ? 6 : 5;
        int next = Math.Clamp(previous + delta, 0, TeachyTvPlaybackRules.Menu(Catalog!, State).Length - 1);
        if (next == previous) return;
        SoundRequested?.Invoke("SE_SELECT"); Request(TeachyTvPresentationAction.Navigate, next);
    }
}
