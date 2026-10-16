using Godot;

public partial class CampaignView
{
    // Engineering screenshots keep the same meaning in every modal screen. Escape
    // stays with its existing owner because naming screens use it to cancel editing.
    private bool TrySystemScreenshotInput(InputEvent input)
    {
        if (input is not InputEventKey { Keycode: Key.F12 } key) return false;
        if (key.Pressed && !key.Echo && _evidence.Length > 0)
        {
            using var image = GetViewport().GetTexture().GetImage();
            var error = image.SavePng(System.IO.Path.ChangeExtension(_evidence, "png"));
            if (error != Error.Ok) GD.PushError("Screenshot failed: " + error);
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void ReleaseModalHeldInput()
    {
        _fameHeldDirection = null; _fameRepeat = 0;
        _fameTextHeld = _fameTextPress = false;
        _teachyTvPresentation?.ReleaseInput();
        _teachyFieldConfirm = _teachyFieldCancel = false;
        _teachyFieldText.ReleaseInput();
        _teachyBattleAHeld = false;
        _teachyBattleVoiceText.ReleaseInput();
    }
}
