using Game.Application;
using Game.Content;
using Godot;

// Source-backed evolution staging. The Application command remains the only species mutation.
public partial class CampaignView
{
    private string? _evolutionViewKey;
    private int _evolutionMoveCursor;
    private string? _evolutionAnimationKey;
    private string? _evolutionIntroName;
    private EvolutionScenePresentation? _evolutionAnimation;
    private bool _evolutionCommitRequested, _evolutionCompletionCue, _evolutionCryStarted;
    private double _evolutionFanfare;
    private PretEvolutionScene? _evolutionSceneAssets;
    private readonly Dictionary<string, Texture2D> _evolutionSceneTextures = new(StringComparer.Ordinal);
    private readonly Dictionary<Texture2D, Texture2D> _evolutionSilhouettes = new();

    private void DisposeEvolutionScenePresentation()
    {
        foreach (var texture in _evolutionSceneTextures.Values) texture.Dispose();
        foreach (var texture in _evolutionSilhouettes.Values) texture.Dispose();
        _evolutionSceneTextures.Clear(); _evolutionSilhouettes.Clear(); _evolutionSceneAssets=null;
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

    private Texture2D EvolutionSilhouette(Texture2D texture)
    {
        if (_evolutionSilhouettes.TryGetValue(texture, out var result)) return result;
        using var image = texture.GetImage();
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
            image.SetPixel(x, y, new Color(1, 1, 1, image.GetPixel(x, y).A));
        result = ImageTexture.CreateFromImage(image); _evolutionSilhouettes.Add(texture, result); return result;
    }

    private void ProcessEvolutionPresentation(double delta)
    {
        SynchronizeEvolutionView();
        if (_session.State.Evolution is not { } scene) return;
        _held = null; _repeat = 0;
        _evolutionFanfare = Math.Max(0, _evolutionFanfare - delta);
        if (_evolutionAnimation is { Animating: true } animation && !_busy && !_lastCommandFault)
        {
            var before = animation.Phase; animation.Advance(delta);
            if (before != animation.Phase)
            {
                if (animation.Phase == EvolutionScenePhase.Spiral) { SourceMusic("MUS_EVOLUTION"); SourceSound("SE_M_MEGA_KICK"); }
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

    private void SynchronizeEvolutionView(CampaignState? before = null)
    {
        if (_session.State.Evolution is not { } scene)
        {
            _evolutionViewKey = null; _evolutionAnimationKey = null; _evolutionAnimation = null; _evolutionIntroName = null;
            if (_session.State.EggHatch is null) DisposeEvolutionScenePresentation();
            return;
        }
        string animationKey = $"{scene.BattleId}:{scene.Proposal.InstanceId}:{scene.Proposal.PreviousSpecies}:{scene.Proposal.TargetSpecies}:{scene.ProposalIndex}";
        if (_evolutionAnimationKey != animationKey)
        {
            _evolutionAnimationKey = animationKey;
            // Stone evolution already committed its new default name; the scene introduces
            // the exact pre-command nickname, including user-chosen names.
            _evolutionIntroName = before?.Party.FirstOrDefault(p => p.InstanceId == scene.Proposal.InstanceId)?.Nickname
                ?? _session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId).Nickname;
            _evolutionCommitRequested = false; _evolutionCompletionCue = false; _evolutionCryStarted = false; _evolutionFanfare = 0;
            // Ordinary saved result pages must not propose or commit evolution a second time.
            _evolutionAnimation = scene.Stage == CampaignEvolutionStage.Proposal || scene.ItemUse is not null && scene.PageIndex == 0
                ? new EvolutionScenePresentation(scene.ItemUse is null) : null;
            if (_evolutionAnimation is not null) { SourceCrySymbol(scene.Proposal.PreviousSpecies.Value.Replace("pret:", "", StringComparison.Ordinal)); }
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
        if (_evolutionAnimation is not { } a || a.Phase is EvolutionScenePhase.Intro or EvolutionScenePhase.Cancelled or EvolutionScenePhase.Complete) return;
        _evolutionSceneAssets ??= PretEvolutionScene.Load(_root);
        int palette = _evolutionSceneAssets.PaletteAt(a.BackgroundFrame);
        double angle = ((a.BackgroundFrame * 5) & 255) * Math.PI / 128;
        int dx = (int)(Math.Cos(angle) * 4), dy = (int)(Math.Sin(angle) * 4);
        DrawTexture(EvolutionSceneTexture($"background-1-{palette}"), new(-8 + dx, -16 + dy));
        DrawTexture(EvolutionSceneTexture($"background-0-{palette}"), new(-8 - dx, -16 - dy), new Color(1,1,1,.5f));
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
            DrawTextureRect(texture,rect,false);
            if (a.WhiteAmount>0) DrawTextureRect(EvolutionSilhouette(texture),rect,false,new Color(1,1,1,a.WhiteAmount));
        }
        Portrait(previous,a.PreviousScale);
        if (a.Phase is EvolutionScenePhase.Morph or EvolutionScenePhase.Circle or EvolutionScenePhase.Reveal) Portrait(target,a.TargetScale);
        DrawEvolutionSparkles(a);
        if (a.Phase == EvolutionScenePhase.Reveal)
        {
            float flash = a.PhaseFrame < 16 ? a.PhaseFrame / 16f : a.PhaseFrame < 32 ? 1 : Math.Clamp(1-(a.PhaseFrame-32)/272f,0,1);
            DrawRect(new(0,0,240,112),new Color(1,1,1,flash));
        }
    }

    private void DrawEvolutionSparkles(EvolutionScenePresentation a)
    {
        if (a.Phase is not (EvolutionScenePhase.Spiral or EvolutionScenePhase.Arc or EvolutionScenePhase.Circle)) return;
        var texture=EvolutionSceneTexture("sparkle");
        int count=a.Phase==EvolutionScenePhase.Circle?16: a.Phase==EvolutionScenePhase.Arc?9:4;
        int interval=a.Phase==EvolutionScenePhase.Spiral?8:1;
        int last=a.Phase==EvolutionScenePhase.Spiral?56:a.Phase==EvolutionScenePhase.Arc?5:32;
        for(int spawn=0;spawn<=last;spawn+=a.Phase==EvolutionScenePhase.Circle?32:interval)
        for(int i=0;i<count;i++)
        {
            int age=a.PhaseFrame-spawn;if(age<0)continue;
            double angle;float x,y;
            if(a.Phase==EvolutionScenePhase.Spiral)
            {
                y=88-age*age/80; if(y<=8)continue;
                int radius=48-age/2;angle=((spawn*2+i*64+age*4)&255)*Math.PI/128;
                x=120+(float)Math.Cos(angle)*radius;y+=(float)Math.Sin(angle)*radius/4;
            }
            else if(a.Phase==EvolutionScenePhase.Arc)
            {
                y=8+age*age/5;if(y>=88)continue;
                int radius=(int)(Math.Sin(((age*4)&255)*Math.PI/128)*40)+8;angle=i*16*Math.PI/128;
                x=120+(float)Math.Cos(angle)*radius;y+=(float)Math.Sin(angle)*radius/4;
            }
            else
            {
                int radius=120-age*(spawn==0?4:8);if(radius<=8)continue;
                angle=((i*16+age*4)&255)*Math.PI/128;
                x=120+(float)Math.Cos(angle)*radius;y=56+(float)Math.Sin(angle)*radius;
            }
            DrawTexture(texture,new(x-4,y-4));
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
        string text = _evolutionAnimation?.Animating == true ? $"What? {_evolutionIntroName ?? owned.Nickname}\nis evolving!" : scene.Stage switch
        {
            CampaignEvolutionStage.Proposal => $"What? {_evolutionIntroName ?? owned.Nickname}\nis evolving!",
            CampaignEvolutionStage.LearnMove => MoveLearningPrompt(scene.PendingMoves[0].Move),
            _ => scene.Pages[scene.PageIndex]
        };
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(10, 119 + i * 16), lines[i]);
        if (scene.Stage == CampaignEvolutionStage.Proposal)
            SourceText(_sourceSmall, new(14, 94), "B/Backspace: stop evolution");
        if (_wildNotice is not null) ItemMessageBox(_wildNotice);
    }
}
