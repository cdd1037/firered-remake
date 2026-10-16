using Game.Domain.Events;
using Godot;

public partial class CampaignView
{
    // The original waitbuttonpress (Oak rating and Route23 badge guards) is
    // distinct from text-printer/fanfare completion.
    // Enter and Backspace map to A/B; pointer confirmations reuse Enter here.
    private bool TryOakRatingButtonInput(InputEventKey key)
    {
        if (_session.State.Script.Wait?.Kind != StoryWaitKind.ButtonPress ||
            key.Keycode is not (Key.Enter or Key.Backspace)) return false;
        _held = null;
        if (key.Pressed && FieldTextComplete && _waitElapsed >= _waitDuration &&
            _tracks.All(t => t.Time >= t.Duration) && _fanfareRemaining <= 0)
            Apply(new("button_press", Text: key.Keycode == Key.Enter ? "A" : "B"));
        return true;
    }
}
