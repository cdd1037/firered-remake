using Game.Application;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Egg presentation reads the owned marker and an explicit Application hatch scene.
/// The renderer never creates a Pokemon, chooses shard variants or advances gameplay RNG.</summary>
public partial class CampaignView
{
    private static string PresentedOwnedName(PartyPokemon owned) => owned.IsEgg ? "EGG" : owned.Nickname;
    private Texture2D PresentedOwnedIcon(PartyPokemon owned) => owned.IsEgg ? BreedingEggIcon() : PresentedPokemonIcon(owned.Pokemon);

    // pokemon_summary_screen.c PrintInfoPage: <=5, <=10, <=40, then >40.
    // Eggs cannot flip to skills/moves, and none of the hidden Pokemon fields is displayed.
    private static string[] EggSummaryLines(PartyPokemon owned) => owned.Pokemon.Friendship switch
    {
        <= 5 => ["It's making sounds.", "It's almost ready", "to hatch!"],
        <= 10 => ["It occasionally", "moves. It should", "hatch soon."],
        <= 40 => ["What will hatch", "from this? It will", "take some time."],
        _ => ["It looks like this", "EGG will take a", "long time to hatch."]
    };

    // pret 037335f pokemon_summary_screen.c:2789-2839,3243-3269;
    // strings.c:820-824. This is the owned-party/box branch, not the link
    // opponent's party. Validated current names contain no extended controls.
    private string[] EggOriginLines(PartyPokemon owned)
    {
        if (owned.MetLocation == "METLOC_FATEFUL_ENCOUNTER" || owned.ModernFatefulEncounter)
            return ["A wondrously peculiar POKéMON EGG", "obtained at a nice place."];
        bool currentOwner = (owned.Pokemon.OriginalTrainerId & 0xffff) == (_session.State.TrainerId & 0xffff) &&
            string.Equals(owned.OriginalTrainerName, _session.State.PlayerName, StringComparison.Ordinal);
        if (owned.MetGame is not (4 or 5) || !currentOwner)
            return ["A peculiar POKéMON EGG obtained", "in a trade."];
        return owned.MetLocation == SourceGiftEggRules.MetLocation
            ? ["An odd POKéMON EGG obtained from", "the traveling man."]
            : ["An odd POKéMON EGG found by the", "DAY-CARE couple."];
    }

    private void DrawEggSummary(PartyPokemon owned)
    {
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
        SourceText(_sourceNormal, new(8, 8), "POKéMON INFO");
        SourceText(_sourceNormal, new(82, 30), "EGG");
        DrawTexture(BreedingEggShell(), new(8, 38));
        var lines = EggSummaryLines(owned);
        for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(82, 51 + 15 * i), lines[i]);
        var origin = EggOriginLines(owned);
        SourceText(_sourceSmall, new(8, 111), origin[0]);
        SourceText(_sourceSmall, new(8, 125), origin[1]);
        SourceText(_sourceSmall, new(172, 143), "BACK");
    }

    private double _eggHatchElapsed;
    private int _eggHatchCueFrame = -1;
    private bool _eggHatchCryStarted;
    private ulong _eggHatchTransitionFrame = ulong.MaxValue;
    private const int EggHatchRevealFrame = 256, EggHatchEndFrame = 298;
    // daycare.c SpriteCB_Egg_0..5: fade/31 idle,21 shake,30+21 shake,
    //30+39 shake,51 idle,white flash,42 reveal. Real VBlank parity is not asserted.
    private static readonly int[] EggShardSpawnFrames = [62,164,164,239,239,239,239,240,240,240,240,241,241,241,241,242,242,242,242];
    private static readonly (int X, int Y)[] EggShardVelocity =
    [(-384,-960),(-1280,-768),(896,-768),(-1024,-960),(512,-384),(-128,-1728),(1280,-576),
     (-384,-960),(1152,-384),(-256,-1728),(1024,-576),(-896,-960),(256,-384),(-900,-1728),
     (1152,-576),(-128,-1920),(256,-1152),(-640,-576),(640,-1920)];
    private int EggHatchFrame => (int)Math.Min(EggHatchEndFrame, Math.Floor(_eggHatchElapsed * 60));
    private bool EggHatchNaming => _session?.State.EggHatch?.Stage == CampaignEggHatchStage.Naming;
    private bool EggHatchCryComplete => _sourceAudio is null || _audioFaulted || _sourceAudio.Snapshot().Cry is not { Playing: true };
    private bool EggHatchFanfareComplete => _fanfareRemaining <= 0 && (_sourceAudio is null || _audioFaulted || _sourceAudio.Snapshot().FanfareToken is null);

    private void SynchronizeEggHatchPresentation(CampaignState? before, CampaignState after)
    {
        if (before is null || before.EggHatch?.InstanceId != after.EggHatch?.InstanceId)
        { _eggHatchElapsed = 0; _eggHatchCueFrame = -1; _eggHatchCryStarted = false; }
        if (before?.EggHatch?.Stage == after.EggHatch?.Stage && before?.EggHatch?.InstanceId == after.EggHatch?.InstanceId) return;
        _held = null; _repeat = 0; ReleaseFieldTextInput();
        _eggHatchTransitionFrame = Engine.GetProcessFrames();
        if (after.EggHatch is not null) ResetFieldMenu();
        if (after.EggHatch?.Stage == CampaignEggHatchStage.Hatched)
        {
            SourceSound("MUS_EVOLVED", fanfare: true);
            _fanfareRemaining = (_cues.CounterFor("MUS_EVOLVED") + 1) / 60.0;
        }
    }

    private bool ProcessEggHatchPresentation(double delta)
    {
        if (_session.State.EggHatch is not { } hatch) return false;
        _held = null; _repeat = 0;
        _waitElapsed += delta;
        if (hatch.Stage == CampaignEggHatchStage.Animation && !_busy && !_lastCommandFault)
        {
            _eggHatchElapsed += delta;
            int frame = EggHatchFrame;
            if (_eggHatchCueFrame < 61 && frame >= 61) SourceMusic("MUS_EVOLUTION");
            foreach (int tick in new[] { 62, 113, 164, 179 })
                if (_eggHatchCueFrame < tick && frame >= tick) SourceSound("SE_BALL");
            _eggHatchCueFrame = frame;
            if (frame >= EggHatchEndFrame)
            {
                if (!_eggHatchCryStarted)
                { _eggHatchCryStarted = true; SourceCry(_session.State.Party[hatch.PartyIndex].Pokemon); }
                else if (EggHatchCryComplete) Apply(new("ack"));
            }
        }
        else if (hatch.Stage == CampaignEggHatchStage.Hatched && FieldTextComplete && EggHatchFanfareComplete && !_busy && !_lastCommandFault)
            Apply(new("ack"));
        else if (hatch.Stage == CampaignEggHatchStage.Naming && _session.State.Nickname?.Submitted == true && !_busy && !_lastCommandFault)
            Apply(new("ack"));
        QueueRedraw(); return true;
    }

    private bool EggHatchInput(InputEvent input)
    {
        if (input is InputEventKey { Keycode: Key.F5 or Key.F9 or Key.F12 }) return false;
        if (_eggHatchTransitionFrame == Engine.GetProcessFrames()) { GetViewport().SetInputAsHandled(); return true; }
        if (_session?.State.EggHatch is not { } hatch) return false;
        _held = null; _repeat = 0;
        if (input is InputEventMouseButton { Pressed: true } mouse)
        {
            if (mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
            {
                Vector2 point = GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position;
                if (hatch.Stage == CampaignEggHatchStage.NicknameChoice && new Rect2(160,64,64,48).HasPoint(point))
                    _choiceYes = point.Y < 88;
                using var key = new InputEventKey { Pressed = true, Keycode = mouse.ButtonIndex == MouseButton.Right ? Key.Backspace : Key.Enter };
                EggHatchInput(key);
            }
            GetViewport().SetInputAsHandled(); return true;
        }
        if (input is not InputEventKey keyInput) return true;
        if (!keyInput.Pressed || keyInput.Echo) { if (!keyInput.Pressed) ReleaseFieldTextInput(); return true; }
        if (hatch.Stage == CampaignEggHatchStage.Animation) return true;
        if (hatch.Stage == CampaignEggHatchStage.Naming)
        {
            if (keyInput.Keycode is Key.Enter or Key.KpEnter) SubmitCampaignNickname(_nicknameEdit.Text);
            else if (keyInput.Keycode == Key.Escape) SubmitCampaignNickname("");
            return true;
        }
        bool confirm = keyInput.Keycode is Key.Enter or Key.KpEnter or Key.A;
        bool cancel = keyInput.Keycode is Key.Backspace or Key.Escape or Key.B;
        if (!FieldTextComplete)
        {
            if (confirm || cancel) { _fieldTextHeld = true; _fieldTextNewPress = true; }
            return true;
        }
        if (hatch.Stage == CampaignEggHatchStage.Hatched && !EggHatchFanfareComplete) return true;
        if (hatch.Stage == CampaignEggHatchStage.NicknameChoice)
        {
            if (confirm || cancel || keyInput.Keycode is Key.Up or Key.Down) SourceSound("SE_SELECT");
            if (keyInput.Keycode == Key.Up) _choiceYes = true;
            if (keyInput.Keycode == Key.Down) _choiceYes = false;
            if (confirm || cancel) Apply(new("ack", Choice: !cancel && _choiceYes ? 1 : 0));
        }
        else if (confirm || cancel) Apply(new("ack"));
        QueueRedraw(); GetViewport().SetInputAsHandled(); return true;
    }

    private bool DrawEggHatch()
    {
        if (_session.State.EggHatch is not { } hatch) return false;
        var owned = _session.State.Party[hatch.PartyIndex];
        if (owned.InstanceId != hatch.InstanceId || owned.IsEgg || owned.Pokemon.SourceSpeciesId != hatch.SourceSpeciesId)
            throw new InvalidDataException("Hatch presentation identity differs from its Application scene.");
        // daycare.c loads the shared trade shadow map using gba_pal2.
        DrawTexture(EvolutionSceneTexture("hatch"), Vector2.Zero);
        if (hatch.Stage == CampaignEggHatchStage.Naming) return DrawNamingScreen();
        int frame = hatch.Stage == CampaignEggHatchStage.Animation ? EggHatchFrame : EggHatchEndFrame;
        if (frame < EggHatchRevealFrame)
        {
            int shell = frame < 62 ? 0 : frame < 113 ? 1 : 2;
            int shake = frame is >= 47 and < 68 ? frame-46 : frame is >= 98 and < 119 ? frame-97+20 : frame is >= 149 and < 188 ? frame-148+40 : 0;
            int offset = shake == 0 ? 0 : (int)Math.Round(Math.Sin(((shake*20)&255)*Math.PI/128) * (frame < 68 ? 1 : 2));
            DrawTexture(BreedingEggHatchFrame(shell), new(104 + offset,59));
        }
        else
        {
            var texture = _battleArt[(PresentedRenderSpecies(owned.Pokemon),"front",owned.Pokemon.IsShiny)].Texture;
            int rise = Math.Min(10,frame-EggHatchRevealFrame+1);
            DrawTexture(texture,new(88,38-rise));
        }
        if (hatch.ShardAnimationIndexes.Length != EggShardSpawnFrames.Length)
            throw new InvalidDataException("Hatch shard preview must contain the original nineteen variants.");
        for (int i=0;i<EggShardSpawnFrames.Length;i++)
        {
            int age=frame-EggShardSpawnFrames[i]; if(age<0)continue;
            int n=age+1, x=n*EggShardVelocity[i].X/256;
            int y=(n*EggShardVelocity[i].Y+100*n*(n-1)/2)/256;
            if(y>20 && EggShardVelocity[i].Y+100*n>0)continue;
            DrawTexture(BreedingEggShard(hatch.ShardAnimationIndexes[i]),new(116+x,56+y));
        }
        if (frame is >= 239 and < 281)
        {
            float white = frame < 256 ? (frame-239)/16f : frame < 264 ? 1 : 1-(frame-264)/16f;
            DrawRect(new(0,0,240,160),new Color(1,1,1,Math.Clamp(white,0,1)));
        }
        if (hatch.Stage != CampaignEggHatchStage.Animation && PresentedFieldText is { } text)
        {
            _battleSkin.DrawDialogue(this);
            string[] lines=FieldRenderedText(text).Split('\n');
            for(int i=0;i<lines.Length;i++) SourceText(_sourceNormal,LabBattleUiSkin.DialogueText+new Vector2(0,i*15),lines[i]);
            if(FieldTextComplete && hatch.Stage==CampaignEggHatchStage.NicknameChoice)
            {
                _battleSkin.DrawUserFrame(this,new(160,64,64,48));
                SourceText(_sourceNormal,new(176,74),"YES"); SourceText(_sourceNormal,new(176,88),"NO");
                SourceText(_sourceNormal,new(168,_choiceYes?74:88),"▶");
            }
            else if(FieldTextComplete) DrawTextCursor(_sourceNormal,LabBattleUiSkin.DialogueText,text.Split('\n'),15,_waitElapsed);
        }
        if (_fade > 0) DrawRect(new(0,0,240,160),new Color(0,0,0,(float)Math.Clamp(_fade/.16,0,1)));
        return true;
    }
}
