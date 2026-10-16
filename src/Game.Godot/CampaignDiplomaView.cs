using Game.Application;
using Game.Content;
using Godot;

public partial class CampaignView
{
    private PretDiplomaPresentation? _diplomaArt;
    private DiplomaPresentation? _diplomaPresentation;
    private readonly Dictionary<bool, Texture2D> _diplomaTextures = [];
    private readonly HashSet<Key> _diplomaConfirmKeys = [];
    private readonly HashSet<int> _diplomaConfirmJoypads = [];
    private bool _diplomaMouseConfirm;
    private ulong _diplomaClosedFrame = ulong.MaxValue;
    private bool DiplomaActive => _session?.State.Diploma is not null;

    private void LoadDiplomaPresentation()
    {
        if (_diplomaArt is not null) return;
        _diplomaArt = PretDiplomaPresentation.Load(_root);
        foreach (bool national in new[] { false, true })
        {
            using var image = NativeImages.LoadPng(_diplomaArt.ReadTexture(national), 240, 160);
            _diplomaTextures.Add(national, SceneTextures.FromImage(image));
        }
        _diplomaPresentation = new(_diplomaArt.Document.FadeFrames, _diplomaArt.Document.FanfareCounterFrames);
        _diplomaPresentation.FanfareRequested += () => SourceSound(_diplomaArt.Document.Fanfare, fanfare: true);
    }

    private void SynchronizeDiplomaPresentation(CampaignState? before, CampaignState after)
    {
        if (after.Diploma is not null && before?.Diploma is null)
        {
            LoadDiplomaPresentation();
            _diplomaConfirmKeys.Clear();
            foreach (Key key in new[] { Key.Enter, Key.KpEnter, Key.A })
                if (Input.IsKeyPressed(key)) _diplomaConfirmKeys.Add(key);
            _diplomaConfirmJoypads.Clear();
            foreach (int device in Input.GetConnectedJoypads())
                if (Input.IsJoyButtonPressed(device, JoyButton.A)) _diplomaConfirmJoypads.Add(device);
            _diplomaMouseConfirm = Input.IsMouseButtonPressed(MouseButton.Left);
            _diplomaPresentation!.Open(_diplomaConfirmKeys.Count > 0 || _diplomaConfirmJoypads.Count > 0 || _diplomaMouseConfirm);
            _held = null; _repeat = 0; ReleaseFieldTextInput(); ResetFieldMenu();
        }
        else if (after.Diploma is null && before?.Diploma is not null)
        {
            _diplomaPresentation?.Close(); ReleaseDiplomaInput();
            _diplomaClosedFrame = Engine.GetProcessFrames();
        }
    }

    private void ReleaseDiplomaInput()
    {
        _diplomaConfirmKeys.Clear(); _diplomaConfirmJoypads.Clear(); _diplomaMouseConfirm = false; _diplomaPresentation?.ReleaseInput();
    }

    private bool ProcessDiplomaPresentation(double delta)
    {
        if (!DiplomaActive) return false;
        _held = null; _repeat = 0;
        if (!_busy && !_lastCommandFault)
        {
            bool finished = _sourceAudio is null || _audioFaulted || _sourceAudio.Snapshot().FanfareToken is null;
            _diplomaPresentation!.Advance(delta, finished);
            if (_diplomaPresentation.Phase == DiplomaPhase.Complete) Apply(new("diploma_complete"));
        }
        QueueRedraw(); return true;
    }

    private bool TryDiplomaInput(InputEvent input)
    {
        bool closingFrame = _diplomaClosedFrame == Engine.GetProcessFrames();
        if (!DiplomaActive && !closingFrame) return false;
        if (input is InputEventKey { Keycode: Key.F12 }) return false;
        _held = null; _repeat = 0;
        if (DiplomaActive)
        {
            bool confirm = false, echo = false;
            if (input is InputEventKey key && key.Keycode is Key.Enter or Key.KpEnter or Key.A)
            {
                if (key.Pressed) _diplomaConfirmKeys.Add(key.Keycode); else _diplomaConfirmKeys.Remove(key.Keycode);
                confirm = true; echo = key.Echo;
            }
            else if (input is InputEventJoypadButton { ButtonIndex: JoyButton.A } joy)
            {
                if (joy.Pressed) _diplomaConfirmJoypads.Add(joy.Device); else _diplomaConfirmJoypads.Remove(joy.Device);
                confirm = true;
            }
            else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
            { _diplomaMouseConfirm = mouse.Pressed; confirm = true; }
            if (confirm) _diplomaPresentation!.Confirm(_diplomaConfirmKeys.Count > 0 || _diplomaConfirmJoypads.Count > 0 || _diplomaMouseConfirm, echo);
        }
        // B, Escape, save/load, movement and wheel input have no native-screen action.
        GetViewport().SetInputAsHandled(); QueueRedraw(); return true;
    }

    private bool DrawDiploma()
    {
        if (!DiplomaActive || _diplomaPresentation?.CertificateVisible != true) return false;
        bool national = _session.State.Diploma!.National;
        DrawTexture(_diplomaTextures[national], Vector2.Zero);
        int Width(string text) => (int)_sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(text), fontSize: _sourceNormal.Size).X;
        foreach (var line in _diplomaArt!.TextLines(_session.State.PlayerName, national, Width))
            SourceText(_sourceNormal, new(line.X, line.Y), line.Text);
        DrawDiplomaFade(); return true;
    }

    private void DrawDiplomaFieldFade()
    {
        if (DiplomaActive && _diplomaPresentation?.FieldVisible == true) DrawDiplomaFade();
    }
    private void DrawDiplomaFade()
    {
        float alpha = _diplomaPresentation!.FadeAlpha;
        if (alpha > 0) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, alpha));
    }
}
