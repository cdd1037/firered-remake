using Game.Application;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

// src/hall_of_fame.c PC callback, distinct from recording a new League result.
// All identity/details come from the immutable archive; browsing never uses today's party.
public partial class CampaignView
{
    private HallOfFamePcPresentation? _hallPcPresentation;
    private HallOfFamePcCursor? _hallPcCursor;
    private readonly List<Texture2D> _hallPcMutedSprites = [];
    private bool _hallPcExitHook;
    private HallOfFamePcPresentation HallPcPresentation => _hallPcPresentation ??= PretHallOfFamePcPresentation.Load(_root);
    private string HallPcText(string symbol) => HallPcPresentation.Text[symbol];

    private void ClearHallPcSprites()
    {
        foreach (var texture in _hallPcMutedSprites) texture.Dispose();
        _hallPcMutedSprites.Clear();
    }

    private void SynchronizeHallPc()
    {
        var view = _session.HallOfFamePcView;
        if (view is null)
        {
            if (_hallPcCursor is not null) { _sourceAudio?.CancelCry(); ClearHallPcSprites(); }
            _hallPcCursor = null;
            return;
        }
        _ = HallPcPresentation;
        if (!_hallPcExitHook) { TreeExiting += ClearHallPcSprites; _hallPcExitHook = true; }
        if (_hallPcCursor == view.Cursor) return;
        if (_hallPcCursor?.TeamIndex != view.Cursor.TeamIndex)
        {
            ClearHallPcSprites();
            foreach (var member in view.Members)
                _hallPcMutedSprites.Add(HallPcMutedTexture(HallPokemonTexture(member.Snapshot)));
        }
        _hallPcCursor = view.Cursor;
        // Source only stops/replaces the preceding cry when the newly selected member is not an Egg.
        var selected = view.Members.FirstOrDefault(m => m.PlayCry);
        if (selected is not null) { _sourceAudio?.CancelCry(); SourceCrySymbol(selected.Snapshot.SpeciesOrEgg.ToString()); }
        Write("hallPcSelection", new { view.Cursor, view.HelpTextSymbol, view.ErrorTextSymbol,
            selectedSpecies = selected?.Snapshot.SpeciesOrEgg, archivedTeam = view.Team?.ChallengeNumber,
            sourceMusicUnchanged = true, exactPcScreenTransition = false, exactBackgroundAlpha = false });
    }

    private static Texture2D HallPcMutedTexture(Texture2D original)
    {
        using var image = original.GetImage(); image.Convert(Image.Format.Rgba8);
        var pixels = image.GetData();
        // BlendPalettesUnfaded(...,12,RGB(22,24,29)), in source RGB555 precision.
        int[] target = [22, 24, 29];
        for (int offset = 0; offset < pixels.Length; offset += 4)
            if (pixels[offset + 3] != 0)
                for (int channel = 0; channel < 3; channel++)
                {
                    int color = pixels[offset + channel] >> 3;
                    color += ((target[channel] - color) * 12) >> 4;
                    pixels[offset + channel] = (byte)((color << 3) | (color >> 2));
                }
        using var faded = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, pixels);
        return ImageTexture.CreateFromImage(faded);
    }

    private bool HallPcInput(InputEventKey key)
    {
        if (!key.Pressed || key.Echo) return true;
        int buttons = key.Keycode switch { Key.Enter => 1, Key.Backspace => 2, Key.Up => 64, Key.Down => 128, _ => 0 };
        if (buttons != 0 && _session.HallOfFamePcView is not null) Apply(new("pc_hall_input", Choice: buttons));
        return true;
    }

    private bool MouseHallPc(Vector2 point)
    {
        var view = _session.HallOfFamePcView;
        if (view is null) return false;
        if (view.Cursor.Corrupted) return new Rect2(0, 0, 240, 16).HasPoint(point) && MouseKey(Key.Enter);
        if (new Rect2(184, 0, 56, 16).HasPoint(point)) return MouseKey(Key.Backspace);
        if (new Rect2(144, 0, 40, 16).HasPoint(point)) return MouseKey(Key.Enter);
        // A click uses the same non-wrapping member transitions as the source D-pad.
        foreach (var member in view.Members.Reverse())
        {
            if (!new Rect2(member.Position.X - 32, member.Position.Y - 32, 64, 64).HasPoint(point)) continue;
            int target = member.Snapshot.PartySlot;
            while (_session.HallOfFamePcView is { } current && current.Cursor.MemberIndex != target)
            {
                Apply(new("pc_hall_input", Choice: current.Cursor.MemberIndex < target ? 128 : 64));
                if (_session.HallOfFamePcView?.Cursor.MemberIndex == current.Cursor.MemberIndex) break;
            }
            return true;
        }
        return false;
    }

    private string HallPcHeader(CampaignHallOfFamePcView view) => HallPcText("gText_HOFNumber")
        .Replace("{STR_VAR_1}", view.Cursor.HallNumber.ToString(), StringComparison.Ordinal);
    private string HallPcHelp(CampaignHallOfFamePcView view) => HallPcText(view.HelpTextSymbol)
        // The shared atlas lacks the source multi-cell A/B/D-pad glyphs. Retain source words.
        .Replace("{DPAD_UPDOWN}", "↑↓", StringComparison.Ordinal)
        .Replace("{A_BUTTON}", "A ", StringComparison.Ordinal).Replace("{B_BUTTON}", "B ", StringComparison.Ordinal);
    private static float HallPcTextWidth(SourceGameFont font, string text) =>
        font.Font.GetStringSize(font.PrepareText(text), fontSize: font.Size).X;
    private (string Dex, string Nickname, string Species, string Level, string TrainerId) HallPcDetails(CampaignHallOfFamePcMember member)
    {
        var snapshot = member.Snapshot;
        string speciesName = _catalog.SourceSpeciesName(snapshot.SpeciesOrEgg)
            ?? throw new InvalidDataException("Archived Hall Pokemon species display name missing.");
        string name = snapshot.CustomNickname ?? speciesName;
        if (!member.ShowPokemonDetails) return ("", name, "", "", "");
        var entry = _pokedexMenu?.Entries.GetValueOrDefault(snapshot.SpeciesOrEgg)
            ?? throw new InvalidDataException("Archived Hall Pokemon National Dex entry missing.");
        var definition = _catalog.PokemonContent!.Catalog.Species.Values.Single(s => s.SourceSpeciesId == snapshot.SpeciesOrEgg);
        var gender = snapshot.SpeciesOrEgg is 29 or 32 ? PokemonGender.Genderless
            : PokemonFactory.GenderFromPersonality(definition.GenderRatio, snapshot.Personality);
        string suffix = gender switch { PokemonGender.Male => "♂", PokemonGender.Female => "♀", _ => " " };
        return (HallPcText("gText_Number") + entry.NationalNumber.ToString("D3"), name,
            "/" + speciesName + suffix, HallPcText("gText_Level") + snapshot.Level,
            HallPcText("gText_IDNumber") + member.DisplayTrainerId.ToString("D5"));
    }

    private void DrawHallPc()
    {
        DrawHallBackground();
        var view = _session.HallOfFamePcView;
        if (view is null) return;
        // Source top bar is a 30x2-tile window, stdpal_2 index15; no menu frame.
        DrawRect(new(0, 0, 240, 16), Color.Color8(0, 123, 198));
        string help = HallPcHelp(view);
        SourceText(_sourceSmallWhite, new(236 - HallPcTextWidth(_sourceSmallWhite, help), 1), help);
        if (view.Cursor.Corrupted)
        {
            _battleSkin.DrawDialogue(this);
            string[] lines = HallPcText(view.ErrorTextSymbol!).Split('\n');
            for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(16, 121 + i * 15), lines[i]);
            return;
        }
        SourceText(_sourceWhite, new(4, 1), HallPcHeader(view));
        // Source OAM priority: the selected member is in front of every unselected member.
        foreach (var member in view.Members.Where(m => !m.Selected))
            DrawTexture(_hallPcMutedSprites[member.Snapshot.PartySlot], new(member.Position.X - 32, member.Position.Y - 32));
        var selected = view.Members.Single(m => m.Selected);
        DrawTexture(HallPokemonTexture(selected.Snapshot), new(selected.Position.X - 32, selected.Position.Y - 32));
        var details = HallPcDetails(selected);
        float width = HallPcTextWidth(_sourceWhite, details.Nickname);
        SourceText(_sourceWhite, new(144 - (selected.ShowPokemonDetails ? width : width / 2), 121), details.Nickname);
        if (!selected.ShowPokemonDetails) return;
        SourceText(_sourceWhite, new(32, 121), details.Dex);
        SourceText(_sourceWhite, new(144, 121), details.Species);
        SourceText(_sourceWhite, new(48, 137), details.Level);
        SourceText(_sourceWhite, new(112, 137), details.TrainerId);
    }
}
