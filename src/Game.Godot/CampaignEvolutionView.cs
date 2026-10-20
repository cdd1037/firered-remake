using Game.Application;
using Game.Content;
using Game.Domain.Options;
using Game.Domain.Events;
using Game.Presentation.BattleAnimations;
using Game.Domain.World;
using Godot;

// Source-backed evolution staging. The Application command remains the only species mutation.
public partial class CampaignView
{
    private string? _evolutionViewKey;
    private int _evolutionMoveCursor;
    private string? _evolutionAnimationKey;
    private string? _evolutionIntroName;
    private string EvolutionIntroText(string name) => string.Join('\n', SourceBattleText.Pages(_battleText,
        "gText_PkmnIsEvolving", new Dictionary<string, string>(StringComparer.Ordinal) { ["STR_VAR_1"] = name }));
    private EvolutionScenePresentation? _evolutionAnimation;
    private bool _evolutionCommitRequested, _evolutionCompletionCue, _evolutionCryStarted;
    private double _evolutionFanfare;
    private bool _evolutionIntroCueAvailable, _evolutionIntroTextPressed;
    private string EvolutionSourceMusic => _evolutionAnimation is
        { Phase: EvolutionScenePhase.Intro, IntroStage: not EvolutionIntroStage.BackgroundFade }
        ? "MUS_NONE" : "MUS_EVOLUTION";
    private PretEvolutionScene? _evolutionSceneAssets;
    private readonly Dictionary<string, Texture2D> _evolutionSceneTextures = new(StringComparer.Ordinal);
    private readonly Dictionary<Texture2D, SourcePixelAsset> _evolutionSourcePixels = new();
    private readonly Dictionary<(Texture2D Source, int Coefficient), Texture2D> _evolutionWhiteTextures = new();
    private ImageTexture? _evolutionBackgroundTexture;
    private (int Palette, int Angle)? _evolutionBackgroundFrame;

    private void DisposeEvolutionScenePresentation()
    {
        foreach (var texture in _evolutionSceneTextures.Values) texture.Dispose();
        foreach (var texture in _evolutionWhiteTextures.Values) texture.Dispose();
        _evolutionBackgroundTexture?.Dispose(); _evolutionBackgroundTexture = null; _evolutionBackgroundFrame = null;
        _evolutionSceneTextures.Clear(); _evolutionWhiteTextures.Clear(); _evolutionSourcePixels.Clear(); _evolutionSceneAssets=null;
        _evolutionAnimation=null; _evolutionAnimationKey=null; _evolutionViewKey=null;
    }

    private Texture2D EvolutionSceneTexture(string id)
    {
        if (_evolutionSceneTextures.TryGetValue(id, out var texture)) return texture;
        _evolutionSceneAssets ??= PretEvolutionScene.Load(_root);
        if (id.StartsWith("background-",StringComparison.Ordinal) && id.Count(c=>c=='-')==2)
        {
            var parts=id.Split('-'); int palette=int.Parse(parts[2],System.Globalization.CultureInfo.InvariantCulture);
            if(palette<0 || palette>=_evolutionSceneAssets.Document.PaletteCount)throw new InvalidDataException("Invalid scene palette.");
            int columns=_evolutionSceneAssets.Document.AtlasColumns;
            texture=new AtlasTexture { Atlas=EvolutionSceneTexture("background-"+parts[1]),Region=new Rect2((palette%columns)*256,(palette/columns)*256,256,256) };
            _evolutionSceneTextures.Add(id,texture);return texture;
        }
        var asset = _evolutionSceneAssets.Document.Assets.Single(a => a.Id == id);
        using var image = NativeImages.LoadPng(_evolutionSceneAssets.Texture(id), asset.Width, asset.Height);
        texture = ImageTexture.CreateFromImage(image); _evolutionSceneTextures.Add(id, texture);
        return texture;
    }

    private SourcePixelAsset EvolutionSourcePixels(Texture2D texture, int width, int height)
    {
        // Capture each immutable original/crop once, never once per draw or fade step.
        if (!_evolutionSourcePixels.TryGetValue(texture, out var source))
        { source = SourcePixels(texture, width, height); _evolutionSourcePixels.Add(texture, source); }
        return source;
    }

    private Texture2D EvolutionWhiteTexture(Texture2D texture, int width, int height, int coefficient)
    {
        // Zero restores the existing RGB8 texture exactly, without quantizing it again.
        if (coefficient == 0) return texture;
        var key = (texture, coefficient);
        if (_evolutionWhiteTextures.TryGetValue(key, out var result)) return result;
        var pixels = EvolutionScenePixels.Whiten(EvolutionSourcePixels(texture, width, height), coefficient);
        using var image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, SourcePixelRgba(pixels));
        result = ImageTexture.CreateFromImage(image); _evolutionWhiteTextures.Add(key, result); return result;
    }

    // The trade screen also uses the all-white portrait endpoint.
    private Texture2D EvolutionSilhouette(Texture2D texture) => EvolutionWhiteTexture(texture, 64, 64, 16);

    private void ProcessEvolutionPresentation(double delta)
    {
        SynchronizeEvolutionView();
        if (_session.State.Evolution is not { } scene) return;
        _held = null; _evolutionFanfare = Math.Max(0, _evolutionFanfare - delta);
        if (_evolutionAnimation is { Animating: true } animation && !_busy && !_lastCommandFault)
        {
            var before = animation.Phase;
            bool held = Input.IsKeyPressed(Key.Enter) || Input.IsKeyPressed(Key.A) ||
                Input.IsKeyPressed(Key.Backspace) || Input.IsKeyPressed(Key.B);
            if (animation.Advance(delta, ReadEvolutionIntroSignals, DispatchEvolutionIntroCue, held, _evolutionIntroTextPressed) > 0)
                _evolutionIntroTextPressed = false;
            if (before != animation.Phase)
            {
                if (animation.Phase == EvolutionScenePhase.Spiral) SourceSound("SE_M_MEGA_KICK");
                if (animation.Phase == EvolutionScenePhase.Arc) SourceSound("SE_M_BUBBLE_BEAM2");
                if (animation.Phase == EvolutionScenePhase.Circle) SourceSound("SE_SHINY");
                if (animation.Phase == EvolutionScenePhase.Reveal) SourceSound("SE_M_PETAL_DANCE");
            }
        }
        if (_evolutionAnimation?.Phase == EvolutionScenePhase.Complete && !_busy && !_lastCommandFault)
        {
            if (scene.Stage == CampaignEvolutionStage.Proposal && !_evolutionCommitRequested)
            { _evolutionCommitRequested = true; Apply(new("evolution_choice", Choice: 1)); }
            else if (scene.Stage == CampaignEvolutionStage.Results && !_evolutionCompletionCue)
            {
                if (!_evolutionCryStarted)
                {
                    _evolutionCryStarted=true;
                    SourceCry(_session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId).Pokemon);
                }
                else if (_sourceAudio is null || _audioFaulted || _sourceAudio.Snapshot().Cry is not { Playing:true })
                {
                    _evolutionCompletionCue = true;
                    SourceSound("MUS_EVOLVED", fanfare: true);
                    _evolutionFanfare = (_cues.CounterFor("MUS_EVOLVED") + 1) / 60.0;
                }
            }
        }
        QueueRedraw();
    }

    private EvolutionIntroSignals ReadEvolutionIntroSignals()
    {
        if (_sourceAudio is null || _audioFaulted || !_evolutionIntroCueAvailable)
            return EvolutionIntroSignals.Unavailable;
        var audio = _sourceAudio.Snapshot();
        // sound.c:IsSEPlaying observes both ordinary SE players, not just this cue.
        return new(true, audio.Cry is { Playing: true },
            audio.Effects.Any(effect => effect.SourcePlayer is 1 or 2 && effect.Playing));
    }

    private void DispatchEvolutionIntroCue(EvolutionIntroCue cue)
    {
        switch (cue)
        {
            case EvolutionIntroCue.Cry:
                SourceCrySymbol(_session.State.Evolution!.Proposal.PreviousSpecies.Value.Replace("pret:", "", StringComparison.Ordinal));
                // A missing/disabled/faulted cry must not strand the cosmetic scene.
                // SourceCrySymbol/EnsureAudio retain their existing fault/missing evidence.
                _evolutionIntroCueAvailable = _sourceAudio is not null && !_audioFaulted &&
                    _sourceAudio.Snapshot().Cry is { Playing: true };
                break;
            case EvolutionIntroCue.Sound:
                _evolutionIntroCueAvailable = EnsureAudio("MUS_EVOLUTION_INTRO");
                if (_evolutionIntroCueAvailable) _sourceAudio!.PlaySoundEffect("MUS_EVOLUTION_INTRO");
                break;
            case EvolutionIntroCue.Music:
                SourceMusic("MUS_EVOLUTION");
                break;
        }
    }

    private void SynchronizeEvolutionView(CampaignState? before = null)
    {
        if (_session.State.Evolution is not { } scene)
        {
            _evolutionViewKey = null; _evolutionAnimationKey = null; _evolutionAnimation = null; _evolutionIntroName = null;
            // NPC trade borrows the all-white portrait endpoint from this cache.
            // Keep it for that presentation owner; its exit synchronizes here too.
            if (_session.State.EggHatch is null && !NpcTradePresenting) DisposeEvolutionScenePresentation();
            return;
        }
        string animationKey = $"{scene.BattleId}:{scene.Proposal.InstanceId}:{scene.Proposal.PreviousSpecies}:{scene.Proposal.TargetSpecies}:{scene.ProposalIndex}";
        if (_evolutionAnimationKey != animationKey)
        {
            _evolutionAnimationKey = animationKey;
            // Stone evolution already committed its new species; resolve the intro
            // from the previous owned snapshot, preserving any explicit custom name.
            var introPokemon = before?.Party.FirstOrDefault(p => p.InstanceId == scene.Proposal.InstanceId)
                ?? _session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId);
            _evolutionIntroName = _catalog.PokemonDisplayName(introPokemon);
            _evolutionCommitRequested = false; _evolutionCompletionCue = false; _evolutionCryStarted = false; _evolutionFanfare = 0;
            _evolutionIntroCueAvailable = true; _evolutionIntroTextPressed = false;
            // Ordinary saved result pages must not propose or commit evolution a second time.
            _evolutionAnimation = scene.Stage == CampaignEvolutionStage.Proposal || scene.ItemUse is not null && scene.PageIndex == 0
                ? new EvolutionScenePresentation(scene.ItemUse is null, EvolutionIntroText(_evolutionIntroName),
                    _session.EffectiveOptions.TextSpeed switch { SourceTextSpeed.Slow => 8, SourceTextSpeed.Mid => 4, _ => 1 }) : null;
        }
        string key = $"{animationKey}:{scene.ProposalIndex}:{scene.Stage}:{scene.PageIndex}:{scene.PendingMoves.Length}";
        if (_evolutionViewKey == key) return;
        _moveSelectionStamp = null;
        _evolutionViewKey = key; _evolutionMoveCursor = 0; _held = null;
        if (scene is { Stage: CampaignEvolutionStage.LearnMove } && MoveLearningUnavailable(scene.PendingMoves[0].Move))
            _evolutionMoveCursor = _session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId).Pokemon.Moves.Length;
    }

    private bool TryEvolutionInput(InputEventKey key)
    {
        if (_session.State.Evolution is not { } scene) return false;
        if (key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape) return false;
        if (!key.Pressed || key.Echo) return true;
        SynchronizeEvolutionView(); _held = null;
        bool confirm = key.Keycode is Key.Enter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.B;
        if ((confirm || cancel) && _evolutionAnimation is { IntroTextComplete: false })
            _evolutionIntroTextPressed = true;
        if (_wildNotice is not null)
        {
            if (confirm || cancel) _wildNotice = null;
            return true;
        }
        if (scene.Stage == CampaignEvolutionStage.Proposal)
        {
            if (cancel && _evolutionAnimation?.Cancel() == true)
                Apply(new("evolution_choice", Choice: 0));
        }
        else if (scene.Stage == CampaignEvolutionStage.Results)
        {
            if (confirm && _evolutionAnimation?.Animating != true &&
                (_evolutionAnimation?.Phase != EvolutionScenePhase.Complete || _evolutionCompletionCue) && _evolutionFanfare <= 0 &&
                (_sourceAudio is null || _audioFaulted || _sourceAudio.Snapshot().FanfareToken is null)) Apply(new("ack"));
        }
        else
        {
            var owned = _session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId);
            if (!MoveSelectionCurrent("evolution", owned, scene.PendingMoves[0].Move))
            { _evolutionMoveCursor = 0; _wildNotice = SummaryChangedNotice; return true; }
            if (key.Keycode == Key.Up) _evolutionMoveCursor = Math.Max(0, _evolutionMoveCursor - 1);
            if (key.Keycode == Key.Down) _evolutionMoveCursor = Math.Min(owned.Pokemon.Moves.Length, _evolutionMoveCursor + 1);
            if (confirm || cancel)
            {
                if (confirm && _evolutionMoveCursor < owned.Pokemon.Moves.Length && MoveLearningUnavailable(scene.PendingMoves[0].Move)) return true;
                       bool accepted = Apply(new("evolution_learn", MoveSlot: cancel || _evolutionMoveCursor == owned.Pokemon.Moves.Length ? -1 : _evolutionMoveCursor));
                if (!accepted) _wildNotice = MoveLearningError();
            }
        }
        return true;
    }

    private void DrawEvolutionBackground()
    {
        if (_evolutionAnimation is not { } a || !a.MovingBackgroundVisible) return;
        _evolutionSceneAssets ??= PretEvolutionScene.Load(_root);
        int palette = _evolutionSceneAssets.PaletteAt(a.BackgroundFrame);
        int angle = (a.BackgroundFrame * 5) & 255;
        int dx = _captureBallAssets.Sine[angle + 64] * 4 >> 8, dy = _captureBallAssets.Sine[angle] * 4 >> 8;
        int outerAngle = (angle + 128) & 255;
        int outerX = _captureBallAssets.Sine[outerAngle + 64] * 4 >> 8, outerY = _captureBallAssets.Sine[outerAngle] * 4 >> 8;
        if (_evolutionBackgroundFrame != (palette, angle))
        {
            var inner = EvolutionSourcePixels(EvolutionSceneTexture($"background-0-{palette}"), 256, 256);
            var outer = EvolutionSourcePixels(EvolutionSceneTexture($"background-1-{palette}"), 256, 256);
            var pixels = EvolutionScenePixels.MovingBackground(inner, outer, 8 + dx, 16 + dy, 8 + outerX, 16 + outerY);
            using var image = Image.CreateFromData(240, 112, false, Image.Format.Rgba8, SourcePixelRgba(pixels));
            if (_evolutionBackgroundTexture is null) _evolutionBackgroundTexture = ImageTexture.CreateFromImage(image);
            else _evolutionBackgroundTexture.Update(image);
            _evolutionBackgroundFrame = (palette, angle);
        }
        DrawTexture(_evolutionBackgroundTexture!, Vector2.Zero);
    }

    private void DrawEvolutionPokemon(CampaignEvolution scene, Game.Domain.Pokemon.PartyPokemon owned)
    {
        var a = _evolutionAnimation;
        if (a is null || !a.Animating)
        { DrawTexture(_battleArt[(PresentedRenderSpecies(owned.Pokemon), "front", owned.Pokemon.IsShiny)].Texture, new(88,32)); return; }
        var previous = _battleArt[(_catalog.PokemonContent!.Catalog.Species[scene.Proposal.PreviousSpecies].SourceSpeciesId,"front",owned.Pokemon.IsShiny)].Texture;
        var target = _battleArt[(_catalog.PokemonContent.Catalog.Species[scene.Proposal.TargetSpecies].SourceSpeciesId,"front",owned.Pokemon.IsShiny)].Texture;
        void Portrait(Texture2D texture, int scale)
        {
            if (scale <= 0) return;
            float size=64*scale/256f;
            var rect=new Rect2(120-size/2,64-size/2,size,size);
            DrawTextureRect(EvolutionWhiteTexture(texture,64,64,a.PortraitWhiteCoefficient),rect,false);
        }
        Portrait(previous,a.PreviousScale);
        if (a.Phase is EvolutionScenePhase.Morph or EvolutionScenePhase.Circle or EvolutionScenePhase.Reveal) Portrait(target,a.TargetScale);
        DrawEvolutionSparkles(a);
    }

    private void DrawEvolutionSparkles(EvolutionScenePresentation a)
    {
        var particles = a.Sparkles(_captureBallAssets.Sine);
        if (particles.IsEmpty) return;
        var texture = EvolutionWhiteTexture(EvolutionSceneTexture("sparkle"),8,8,a.SparkleWhiteCoefficient);
        foreach (var particle in particles.OrderByDescending(p => p.Subpriority))
        {
            float size = 8 * particle.Scale;
            DrawTextureRect(texture, new(particle.X - size / 2, particle.Y - size / 2, size, size), false);
        }
    }

    private bool MouseEvolutionLearning(Vector2 point) => _wildNotice is not null ? MouseConfirm(point, MouseMessage)
        : MouseMoveSelectionSummary(point, ref _evolutionMoveCursor,
            _session.State.Party.Single(p => p.InstanceId == _session.State.Evolution!.Proposal.InstanceId).Pokemon.Moves.Length);
    private bool NavigateEvolutionLearning(Vector2 point, int direction) => _wildNotice is null &&
        NavigateMoveSelectionSummary(point, direction, ref _evolutionMoveCursor,
            _session.State.Party.Single(p => p.InstanceId == _session.State.Evolution!.Proposal.InstanceId).Pokemon.Moves.Length);

    private void DrawEvolution()
    {
        SynchronizeEvolutionView();
        var scene = _session.State.Evolution!;
        var owned = _session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId);
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        DrawEvolutionBackground();
        if (_evolutionAnimation is { Phase: EvolutionScenePhase.Reveal } flash)
        {
            // Both moving banks were blacked before this fade. Every visible field
            // sample is opaque, so identical faded banks blend to this RGB555 gray.
            int channel = SourcePaletteMath.Blend(0x8000,flash.BackgroundWhiteCoefficient,0x7fff) & 31;
            float gray = (channel * 255 / 31) / 255f;
            DrawRect(new(0, 0, 240, 112), new Color(gray,gray,gray,1));
        }
        if (scene.Stage == CampaignEvolutionStage.LearnMove)
        {
            DrawMoveSelectionSummary("evolution", owned, scene.PendingMoves[0].Move, _evolutionMoveCursor);
            if (_wildNotice is not null) ItemMessageBox(_wildNotice);
            return;
        }
        else
        {
            DrawEvolutionPokemon(scene, owned);
        }
        _battleSkin.DrawStandardFrame(this, new(0, 112, 240, 48));
        string text = _evolutionAnimation?.Animating == true ? _evolutionAnimation.IntroVisibleText : scene.Stage switch
        {
            CampaignEvolutionStage.Proposal => EvolutionIntroText(_evolutionIntroName ?? _catalog.PokemonDisplayName(owned)),
            CampaignEvolutionStage.LearnMove => MoveLearningPrompt(scene.PendingMoves[0].Move),
            _ => scene.Pages[scene.PageIndex]
        };
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(10, 119 + i * 16), lines[i]);
        if (scene.Stage == CampaignEvolutionStage.Proposal && _evolutionAnimation?.CanCancel == true)
            SourceText(_sourceSmall, new(14, 94), _growthText.EvolutionCancel);
        if (_wildNotice is not null) ItemMessageBox(_wildNotice);
    }
}
