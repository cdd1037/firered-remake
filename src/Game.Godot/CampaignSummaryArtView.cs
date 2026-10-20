using Game.Content;
using Game.Application;
using Game.Domain.Pokemon;
using Game.Domain.Growth;
using Game.Domain.Capture;
using Game.Domain.World;
using Godot;

// One generated native Summary skin serves field, PC, and battle inspection.
// The modal state and all move mutations remain in the existing application path.
public partial class CampaignView
{
    private readonly Dictionary<string, Texture2D> _summaryTextures = [];
    private SummaryPresentation? _summaryPresentation;
    private double _summaryExitStart;
    private readonly SummaryPageTransition _summaryPageTransition = new();
    private Action? _summaryExitAction;
    private string? _summaryExitContext;
    private double _summaryClock;
    private double SummaryNow => _summaryClock * 1000;
    private string? SummaryContext => _battlePartySummaryPage >= 0 && _battlePartySelection is { } selected ? "battle:" + selected.Context + ":" + selected.InstanceId
        : _session.State.Pc?.Summary is { } pc ? "pc:" + pc.InstanceId
        : _fieldMenuMode == "summary" && _fieldPartyCursor >= 0 && _fieldPartyCursor < _session.State.Party.Length ? "field:" + _session.State.Party[_fieldPartyCursor].InstanceId
        : _session.State.DayCareInteraction is not null && _dayCareSummaryPage >= 0 && _tutorCursor >= 0 && _tutorCursor < _session.State.Party.Length
            ? "daycare:" + _session.State.Party[_tutorCursor].InstanceId : null;
    private SummaryPageKey? InteractiveSummaryPage => _battlePartySummaryPage >= 0 && _battlePartySelection is { } selected
        ? new("battle:" + selected.Context, selected.InstanceId, _battlePartySummaryPage, _summaryDetails)
        : _session.State.Pc?.Summary is { } pc ? new("pc", pc.InstanceId, (int)pc.Page, _summaryDetails)
        : _fieldMenuMode == "summary" && _fieldPartyCursor >= 0 && _fieldPartyCursor < _session.State.Party.Length
            ? new("field", _session.State.Party[_fieldPartyCursor].InstanceId, _fieldSummaryPage, _summaryDetails)
        : _session.State.DayCareInteraction is not null && _dayCareSummaryPage >= 0 && _tutorCursor >= 0 && _tutorCursor < _session.State.Party.Length
            ? new("daycare", _session.State.Party[_tutorCursor].InstanceId, _dayCareSummaryPage, _summaryDetails) : null;
    private void BeginSummaryPresentationFrame()
    {
        // Help pauses the underlying presentation rather than ending its visit.
        if (!_help.Active) _summaryPageTransition.BeginFrame();
    }
    private void ObserveSummaryPage(SummaryPageKey page)
    {
        _summaryPageTransition.Observe(page, SummaryNow);
        if (_summaryPageTransition.Outgoing is not { } outgoing) _summarySlideFrom = null;
        else if (_summaryDrawnPage is { } drawn && drawn.Key == outgoing) _summarySlideFrom = drawn;
    }
    private bool SummaryInputLocked
    {
        get
        {
            // Input may arrive before the next process/draw. Observe a preceding
            // accepted page request now, so a second edge cannot restart its slide.
            if (InteractiveSummaryPage is { } page) ObserveSummaryPage(page);
            return SummaryExitActive || _summaryPageTransition.IsEntering(SummaryNow) || _summaryPageTransition.IsSliding(SummaryNow);
        }
    }
    private void ResetSummaryPresentation()
    {
        CloseBattlePartySelection(); _moveSelectionStamp = null; ResetBattleMoveOrder();
        _summaryExitAction = null; _summaryExitContext = null; _summaryDrawnPage = null;
        _summarySlideFrom = null; _summaryClock = 0; _summaryExitStart = 0;
        _summaryPageTransition.Reset();
    }
    private bool SummaryExitActive => _summaryExitAction is not null;
    private void BeginSummaryExit(Action close)
    {
        if (SummaryExitActive) return;
        _summaryExitStart = SummaryNow;
        _summaryExitContext = SummaryContext;
        _summaryExitAction = close;
    }
    private bool ProcessSummaryPresentation(double delta)
    {
        _summaryClock += Math.Max(0, delta);
        if (InteractiveSummaryPage is { } page) ObserveSummaryPage(page);
        if (_summaryExitAction is not { } close) return false;
        if (_summaryExitContext is null || _summaryExitContext != SummaryContext)
        {
            ResetSummaryPresentation();
            return false;
        }
        _held = null; if (SummaryPageTransition.ElapsedFrames(SummaryNow, _summaryExitStart) >= 16)
        {
            _summaryExitAction = null; _summaryDrawnPage = null; _summarySlideFrom = null;
            _summaryPageTransition.Reset();
            close();
        }
        QueueRedraw();
        return true;
    }
    private void DrawSummaryFade()
    {
        double now = SummaryNow;
        float alpha = SummaryExitActive ? Math.Min(1f, (float)((now - _summaryExitStart) * 60 / 16000))
            : _summaryPageTransition.EntryAlpha(now);
        if (alpha > 0) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, alpha));
    }
    private Texture2D SummaryTexture(string name)
    {
        if (!_summaryTextures.TryGetValue(name, out var texture))
        {
            texture = SceneTextures.Texture(System.IO.Path.Combine(_root, "content/generated/ui/summary-screen", name + ".png"));
            _summaryTextures.Add(name, texture);
        }
        return texture;
    }
    private void LoadSummaryDescriptions() => _summaryPresentation ??= SummaryPresentation.Load(_root);
    private sealed record SummaryPageContent(SummaryPageKey Key, PartyPokemon Pokemon, bool Interactive, SummaryMoveSelection? Selection);
    private SummaryPageContent? _summaryDrawnPage, _summarySlideFrom;
    private void DrawNativeSummary(PartyPokemon owned, int page, bool interactive, SummaryMoveSelection? selection = null)
    {
        bool details = selection is not null;
        double now = SummaryNow;
        string owner = InteractiveSummaryPage?.Owner ?? "learning:" + _moveSelectionStamp?.Context;
        var key = new SummaryPageKey(owner, owned.InstanceId, page, details);
        ObserveSummaryPage(key);
        _summaryPageTransition.Present();
        _summaryDrawnPage = new(key, owned, interactive, selection);
        int offset = _summaryPageTransition.Offset(now), direction = _summaryPageTransition.Direction;
        if (_summarySlideFrom is { } previous && _summaryPageTransition.IsSliding(now))
        {
            DrawSetTransform(new Vector2(-offset * direction, 0));
            DrawNativeSummaryPage(previous.Pokemon, previous.Key.Page, previous.Interactive, previous.Selection);
            DrawSetTransform(new Vector2((240 - offset) * direction, 0));
            DrawNativeSummaryPage(owned, page, interactive, selection);
            DrawSetTransform(Vector2.Zero);
            DrawSummaryFade();
            QueueRedraw();
            return;
        }
        _summarySlideFrom = null;
        DrawNativeSummaryPage(owned, page, interactive, selection);
        DrawSummaryFade();
    }
    private void DrawNativeSummaryPage(PartyPokemon owned, int page, bool interactive, SummaryMoveSelection? selection)
    {
        bool details = selection is not null;
        LoadSummaryDescriptions();
        var mon = owned.Pokemon;
        int portraitFrame = _summaryPageTransition.PortraitFrame(SummaryNow);
        int portraitOffset = _summaryPresentation!.PortraitOffset(portraitFrame, mon.CurrentHp, mon.Stats.MaxHp,
            owned.IsEgg, mon.Friendship, owned.Status != 0);
        string skin = owned.IsEgg ? "egg" : details ? "moves_info" : new[] { "info", "skills", "moves" }[page];
        DrawTexture(SummaryTexture(skin + (mon.IsShiny && !owned.IsEgg ? "-shiny" : "")), Vector2.Zero);
        SourceText(_sourceNormal, new(2, 1), _summaryPartyText.PageTitle(page, owned.IsEgg));
        SourceText(_sourceSmallWhite, new(153, 2), details ? _summaryPartyText.Control(selection!) : owned.IsEgg ? "" : page == 2 && interactive ? _summaryText.Get("control.details") : _summaryPartyText.Next);
        SourceText(_sourceSmallWhite, new(209, 2), _summaryText.Get("control.back"));
        if (owned.IsEgg)
        {
            SourceText(_sourceNormal, new(167, 35), _summaryPartyText.Egg);
            DrawTexture(BreedingEggShell(), new(28 + portraitOffset, 33));
            var lines = EggSummaryLines(owned);
            for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(127, 61 + 14 * i), lines[i]);
            var origin = EggOriginLines(owned);
            for (int i = 0; i < origin.Length; i++) SourceText(_sourceSmall, new(8, 116 + i * 14), origin[i]);
            return;
        }
        var species = _catalog.PokemonContent!.Catalog.Species[mon.Species];
        bool flip = _summaryPresentation.FlipPortrait(mon.Species.Value);
        SourceText(_sourceSmall, new(4, 18), "Lv" + mon.Level);
        SourceText(_sourceNormal, new(35, 17), _catalog.PokemonDisplayName(owned));
        if (mon.Gender != PokemonGender.Genderless)
            SourceText(mon.Gender == PokemonGender.Female ? _healthFemale : _healthMale, new(107, 18), mon.Gender == PokemonGender.Female ? "♀" : "♂");
        if (details)
        {
            DrawSummaryPortrait(PresentedOwnedIcon(owned), SummaryPresentation.PortraitPlacement(true, flip));
            DrawPokedexVisual(species.Type1, 48, 35);
            if (species.Type2 != species.Type1) DrawPokedexVisual(species.Type2, 84, 35);
        }
        else if (_battleArt.TryGetValue((PresentedRenderSpecies(mon), "front", mon.IsShiny), out var art))
            DrawSummaryPortrait(art.Texture, SummaryPresentation.PortraitPlacement(false, flip, portraitOffset));
        if (!details && Enum.TryParse<CaptureBall>(FieldSymbol(owned.Ball, "ITEM_").Replace(" BALL", ""), true, out var ball))
            DrawTextureRectRegion(_captureBallAtlas, new(98, 80, 16, 16),
                new(_captureBallAssets.Column(ball), 0, 16, 16));
        if (!details && owned.Markings != 0)
            DrawTextureRectRegion(SummaryTexture("markings"), new(4, 87, 32, 8), new(0, owned.Markings * 8, 32, 8));
        if (mon.IsShiny && !details) DrawTexture(SummaryTexture("shiny-star"), new(98, 32));
        if (!details) DrawForestPartyStatus(owned, new(80, 96));
        if (page == 0)
        {
            SourceText(_sourceNormal, new(167, 21), _pokedexMenu!.Entries[mon.SourceSpeciesId].NationalNumber.ToString("D3"));
            SourceText(_sourceNormal, new(167, 35), (_catalog.SourceSpeciesName(mon.SourceSpeciesId) ?? throw new InvalidDataException("Pokemon species display name missing.")));
            DrawPokedexVisual(species.Type1, 167, 51);
            if (species.Type2 != species.Type1) DrawPokedexVisual(species.Type2, 203, 51);
            SourceText(_sourceNormal, new(167, 65), owned.OriginalTrainerName);
            SourceText(_sourceNormal, new(167, 80), (mon.OriginalTrainerId & 0xffff).ToString("D5"));
            SourceText(_sourceSmall, new(167, 95), _catalog.DisplayNames.HeldItemName(owned.HeldItem));
            bool sameOwner = (mon.OriginalTrainerId & 0xffff) == (_session.State.TrainerId & 0xffff) &&
                string.Equals(owned.OriginalTrainerName, _session.State.PlayerName, StringComparison.Ordinal);
            string[] memo = _summaryPresentation.TrainerMemo(new(_catalog.DisplayNames.NatureName(mon.Nature), owned.MetLocation,
                owned.MetLevel, owned.MetGame, owned.ModernFatefulEncounter, sameOwner)).Split('\n');
            for (int i = 0; i < memo.Length; i++) SourceText(_sourceNormal, new(8, 115 + i * 14), memo[i]);
        }
        else if (page == 1)
        {
            SourceText(_sourceNormal, new(174, 20), $"{mon.CurrentHp}/{mon.Stats.MaxHp}");
            int[] stats = [mon.Stats.Attack, mon.Stats.Defense, mon.Stats.SpecialAttack, mon.Stats.SpecialDefense, mon.Stats.Speed];
            for (int i = 0; i < stats.Length; i++) SourceText(_sourceNormal, new(210, 38 + 13 * i), stats[i].ToString());
            SourceText(_sourceNormal, new(74, 103), _summaryPartyText.Experience);
            SourceText(_sourceNormal, new(175, 103), mon.Experience.ToString());
            SourceText(_sourceNormal, new(74, 116), _summaryPartyText.NextLevel);
            uint next = mon.Level == 100 ? 0 : GrowthRules.ExperienceAtLevel(species.GrowthRate, mon.Level + 1) - mon.Experience;
            SourceText(_sourceNormal, new(175, 116), next.ToString());
            string hpColor = mon.CurrentHp * 5 <= mon.Stats.MaxHp ? "red" : mon.CurrentHp * 2 <= mon.Stats.MaxHp ? "yellow" : "green";
            DrawSummaryBar("hp-" + hpColor, 168, 32, 6, mon.CurrentHp, mon.Stats.MaxHp, true);
            uint baseExp = GrowthRules.ExperienceAtLevel(species.GrowthRate, mon.Level);
            DrawSummaryBar("exp", 152, 128, 8, mon.Level == 100 ? 0 : mon.Experience - baseExp,
                mon.Level == 100 ? 1 : GrowthRules.ExperienceAtLevel(species.GrowthRate, mon.Level + 1) - baseExp, false);
            SourceText(_sourceNormal, new(74, 129), _catalog.DisplayNames.AbilityName(mon.AbilityId));
            SourceText(_sourceNormal, new(10, 143), _summaryPresentation.Abilities.GetValueOrDefault(mon.AbilityId, ""));
        }
        else
        {
            if (selection is { Mode: SummaryInteractionMode.SelectNew } candidates)
            {
                int end = Math.Min(candidates.Candidates.Length + 1, candidates.FirstRow + 5);
                for (int i = candidates.FirstRow; i < end; i++)
                {
                    int y = 21 + (i - candidates.FirstRow) * 28;
                    if (i == candidates.Candidates.Length) SourceText(_sourceNormal, new(163, y), _summaryPartyText.Label(SummaryPartyAction.Cancel));
                    else
                    {
                        var move = _catalog.PokemonContent.Catalog.Moves[candidates.Candidates[i]];
                        DrawPokedexVisual(move.Type, 123, y);
                        SourceText(_sourceNormal, new(163, y), MoveName(move.Id));
                        SourceText(_sourceSmall, new(187, y + 12), $"{_battleSelectionText.PpLabel}{move.Pp}/{move.Pp}");
                    }
                }
            }
            else for (int i = 0; i < mon.Moves.Length; i++)
            {
                var slot = mon.Moves[i];
                var move = _catalog.PokemonContent.Catalog.Moves[slot.Move];
                DrawPokedexVisual(move.Type, 123, 21 + i * 28);
                SourceText(_sourceNormal, new(163, 21 + i * 28), MoveName(slot.Move));
                SourceText(_sourceSmall, new(187, 33 + i * 28), $"{_battleSelectionText.PpLabel}{slot.CurrentPp}/{slot.MaximumPp}");
            }
            if (selection is { } selected)
            {
                if (selected.Mode == SummaryInteractionMode.Replace && selected.NewMove is { } newMove)
                {
                    var move = _catalog.PokemonContent.Catalog.Moves[newMove];
                    int y = 21 + mon.Moves.Length * 28;
                    DrawPokedexVisual(move.Type, 123, y);
                    SourceText(_sourceNormal, new(163, y), MoveName(newMove));
                    SourceText(_sourceSmall, new(187, y + 12), $"{_battleSelectionText.PpLabel}{move.Pp}/{move.Pp}");
                }
                else if (!selected.Swapping && selected.Mode != SummaryInteractionMode.SelectNew) SourceText(_sourceNormal, new(163, 21 + mon.Moves.Length * 28), _summaryPartyText.Label(SummaryPartyAction.Cancel));
                DrawSummaryMoveCursor(selected.Cursor - selected.FirstRow, false);
                if (selected.Swapping) DrawSummaryMoveCursor(selected.SwapSource, true);
                if (selected.SelectedMove(mon.Moves) is { } moveId)
                {
                    var move = _catalog.PokemonContent.Catalog.Moves[moveId];
                    SourceText(_sourceNormal, new(57, 57), move.Power <= 1 ? "---" : move.Power.ToString());
                    SourceText(_sourceNormal, new(57, 71), move.Accuracy == 0 ? "---" : move.Accuracy.ToString());
                    string description = _summaryPresentation.Moves.GetValueOrDefault(move.Id.Value.Replace("pret:", ""), "");
                    var lines = description.Split('\n');
                    bool chinese = SourceHelpTextLayout.ContainsCjk(description);
                    if (chinese && lines.Length > 4)
                        throw new InvalidDataException("Summary move description exceeds its four-row window.");
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (chinese) SourceFittedText(_sourceNormal, new(7, 98 + i * 13), lines[i], 113);
                        else SourceText(_sourceNormal, new(7, 98 + i * 13), lines[i]);
                    }
                }
            }
        }
    }
    private void DrawSummaryPortrait(Texture2D texture, SummarySpritePlacement placement)
        => DrawTextureRectRegion(texture,
            new(placement.X, placement.Y, placement.FlipHorizontal ? -placement.Width : placement.Width, placement.Height),
            new(0, 0, placement.Width, placement.Height));
    private void DrawSummaryBar(string name, int x, int y, int segments, long value, long maximum, bool hp)
    {
        var texture = SummaryTexture(name);
        int whole = 0, partial = 0;
        if (value >= maximum) whole = segments;
        else if (value > 0)
        {
            long pointsPerTile = (maximum << 2) / segments, points = value << 2;
            while (points > pointsPerTile && whole < segments) { points -= pointsPerTile; whole++; }
            partial = (int)(points * (hp ? 6 : 8) / Math.Max(1, pointsPerTile));
        }
        for (int i = 0; i < segments + 3; i++)
        {
            int tile = i == 0 ? 9 : i == 1 ? 10 : i == segments + 2 ? 11 :
                i - 2 < whole ? 8 : i - 2 == whole ? partial : 0;
            DrawTextureRectRegion(texture, new(x + i * 8, y, 8, 8), new(Math.Clamp(tile, 0, 11) * 8, 0, 8, 8));
        }
    }
    private void DrawSummaryMoveCursor(int row, bool swap)
    {
        int y = 18 + row * 28;
        DrawTextureRectRegion(SummaryTexture("cursor-left"), new(120, y, 64, 32), new(0, swap ? 32 : 0, 64, 32));
        DrawTextureRectRegion(SummaryTexture("cursor-right"), new(184, y, 64, 32), new(0, swap ? 32 : 0, 64, 32));
    }
}
