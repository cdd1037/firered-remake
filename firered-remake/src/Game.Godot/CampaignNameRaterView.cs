using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Godot;

/// <summary>The original script owns the Name Rater; this adapter offers only occupied party slots and CANCEL.</summary>
public partial class CampaignView
{
    private int _nameRaterCursor;
    private ulong _nameRaterClosedFrame = ulong.MaxValue;
    private bool NameRaterChoosing => _session?.State.NameRater is { ChoosingParty: true } &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.PartySelection;
    private bool NameRaterNaming => _session?.State.NameRater is { ChoosingParty: false, Target: not null } &&
        _session.State.Nickname is { Submitted: false } && _session.State.Script.Wait?.Kind == StoryWaitKind.Nickname;

    private void SynchronizeNameRaterPresentation(CampaignState? before, CampaignState after)
    {
        bool wasChoosing = before?.NameRater is { ChoosingParty: true };
        bool choosing = after.NameRater is { ChoosingParty: true };
        bool wasNaming = before?.NameRater is { Target: not null } && before.Nickname is { Submitted: false } &&
            before.Script.Wait?.Kind == StoryWaitKind.Nickname;
        bool naming = after.NameRater is { Target: not null } && after.Nickname is { Submitted: false } &&
            after.Script.Wait?.Kind == StoryWaitKind.Nickname;
        if (!wasChoosing && choosing) _nameRaterCursor = 0;
        if (wasChoosing != choosing) _fade = 16.0 / 60;
        if (wasChoosing != choosing || wasNaming != naming)
        {
            _held = null; _repeat = 0; ReleaseFieldTextInput();
            if (wasChoosing && !choosing || wasNaming && !naming) _nameRaterClosedFrame = Engine.GetProcessFrames();
        }
    }

    private bool ConsumeNameRaterTransitionInput(InputEvent input)
    {
        if (_nameRaterClosedFrame != Engine.GetProcessFrames() || input is InputEventKey { Keycode: Key.F5 or Key.F9 or Key.F12 }) return false;
        GetViewport().SetInputAsHandled(); return true;
    }

    private bool NameRaterInput(InputEventKey key)
    {
        if (!NameRaterChoosing && !NameRaterNaming || key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        if (!key.Pressed || key.Echo || _fade > 0) return true;
        if (NameRaterNaming)
        {
            if (key.Keycode is Key.Enter or Key.KpEnter) SubmitNameRaterNicknameFromView(_nicknameEdit.Text);
            // B/Backspace are editor deletion. The source naming screen has no exit shortcut.
            return true;
        }
        int count = _session.State.Party.Length + 1;
        if (key.Keycode is Key.Up or Key.Left) _nameRaterCursor = (_nameRaterCursor + count - 1) % count;
        else if (key.Keycode is Key.Down or Key.Right) _nameRaterCursor = (_nameRaterCursor + 1) % count;
        else if (key.Keycode is Key.Backspace or Key.Escape) Apply(new("name_rater_cancel"));
        else if (key.Keycode is Key.Enter or Key.KpEnter)
        {
            if (_nameRaterCursor == _session.State.Party.Length) Apply(new("name_rater_cancel"));
            else if (_nameRaterCursor >= 0 && _nameRaterCursor < _session.State.Party.Length)
                Apply(new("name_rater_choose", PartyIndex: _nameRaterCursor) { TargetInstanceId = _session.State.Party[_nameRaterCursor].InstanceId });
        }
        return true;
    }

    private bool SubmitNameRaterNicknameFromView(string text)
    {
        if (!NameRaterNaming) return false;
        var target = _session.State.NameRater!.Target!;
        Apply(new("nickname", Text: text) { TargetInstanceId = target.InstanceId });
        return true;
    }

    private void NameRaterNicknameGuiInput(InputEvent input)
    {
        if (NameRaterNaming && input is InputEventKey { Pressed: true, Keycode: Key.Escape })
            _nicknameEdit.AcceptEvent();
    }

    private int NameRaterPointerRow(Vector2 point)
        => ServicePartyPointerRow(point);

    private bool NavigateNameRaterPointer(Vector2 point, int direction)
    {
        if (!NameRaterChoosing) return false;
        int row = NameRaterPointerRow(point);
        if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_nameRaterCursor == row) return false;
        _nameRaterCursor = row; return true;
    }

    private bool MouseNameRater(Vector2 point)
    {
        if (!NameRaterChoosing) return false;
        int row = NameRaterPointerRow(point);
        if (row < 0) return false;
        _nameRaterCursor = row; MouseKey(Key.Enter); return true;
    }

    private bool DrawNameRaterParty()
    {
        if (!NameRaterChoosing) return false;
        var source = _catalog.NameRaterSource ?? throw new InvalidDataException("Name Rater text content missing.");
        string prompt = NameRaterRules.ChooserPromptSymbol(_session.State.Party) == "gText_ChoosePokemon" ? source.ChoosePokemonText : source.ChoosePokemonCancelText;
        DrawServicePartyChooser(_nameRaterCursor, prompt);
        return true;
    }

    private bool DrawNameRaterNickname()
    {
        if (!NameRaterNaming) return false;
        var target = _session.State.NameRater!.Target!;
        string? error = NameRaterRules.ValidateSelection(_session.State.Party, target.PartyIndex, target.InstanceId);
        if (error is not null) throw new InvalidDataException(error);
        var mon = _session.State.Party[target.PartyIndex];
        DrawRect(new(0, 0, 240, 160), new Color("e8f0d8"));
        _battleSkin.DrawStandardFrame(this, new(8, 16, 224, 136));
        SourceText(_sourceNormal, new(20, 26), target.OriginalName + "'s nickname?");
        DrawTexture(PresentedPokemonIcon(mon.Pokemon), new(192, 24));
        SourceText(_sourceSmall, new(20, 46), _catalog.PokemonContent!.SpeciesNames[mon.Pokemon.SourceSpeciesId]);
        if (mon.Pokemon.Gender != PokemonGender.Genderless)
            SourceText(mon.Pokemon.Gender == PokemonGender.Female ? _healthFemale : _healthMale, new(168, 46), mon.Pokemon.Gender == PokemonGender.Female ? "♀" : "♂");
        SourceText(_sourceSmall, new(20, 99), "Enter: confirm. Backspace: delete.");
        SourceText(_sourceSmall, new(20, 114), "Blank keeps " + target.OriginalName + ".");
        if (_status.StartsWith("nickname_", StringComparison.Ordinal) || _status.StartsWith("name_rater_", StringComparison.Ordinal))
            SourceText(_sourceSmall, new(20, 134), _status == "nickname_length" ? "Use at most 10 characters." : "That name could not be submitted.");
        return true;
    }
}
