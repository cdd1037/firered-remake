using System.Buffers.Binary;
using System.Security.Cryptography;
using Game.Application;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

/// <summary>New Game/Continue entry with source-backed Oak speech and optional naming keys.
/// No save file is written by menu navigation or introduction.</summary>
public partial class CampaignView
{
    private PretMainMenuSource? _entrySource;
    private LoadedSave<CampaignState>? _entrySave;
    private string _entryMode="closed",_entryPlayerName="",_entryRivalName="";
    private string? _entryNotice;
    private bool _entryLoadCanRetry;
    private int _entryCursor,_entryGender;
    private bool _entryHasExistingFiles,_entryOverwritePending,_entryOverwriteConfirmed;
    private string? _entryObservedSlot,_entryPromptSlot,_entrySaveAttemptSlot;
    private SourceGameFont? _entryHeadingFont;
    private readonly Dictionary<int,SourceGameFont> _entryGenderFonts=[];
    private SpriteAsset? _femalePlayer;
    private ulong _entrySubmitFrame=ulong.MaxValue;
    private ulong _entryActionFrame=ulong.MaxValue,_entryFreshFieldFrame=ulong.MaxValue;
    private bool PlayerEntryActive=>_entryMode!="closed";
    private string PlayerSavePath=>ProjectSettings.GlobalizePath("user://campaign-save.json");
    private SpriteAsset CurrentPlayerSprite=>_session.State.IsCycling?CityCycleSprite:_session.State.PlayerGender==0?_art.Player:_femalePlayer??throw new InvalidDataException("Female player art was not loaded.");

    private void EnsurePlayerGenderArt()
    {
        if(_session.State.PlayerGender!=1||_femalePlayer is not null)return;
        var source=PretPlayerGenderArt.Load(_root);var asset=source.Assets.Single(a=>a.Gender==1);
        using var image=new Image();if(image.LoadPngFromBuffer(source.ReadTexture(1))!=Error.Ok)throw new InvalidDataException("Female player texture could not be decoded.");
        image.Convert(Image.Format.Rgba8);var pixels=image.GetData();
        if(image.GetWidth()!=asset.Width||image.GetHeight()!=asset.Height)throw new InvalidDataException("Female player pixels differ.");
        for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]!=0)for(int c=0;c<3;c++){int five=pixels[i+c]>>3;pixels[i+c]=(byte)((five<<3)|(five>>2));}
        using var display=Image.CreateFromData(asset.Width,asset.Height,false,Image.Format.Rgba8,pixels);
        _femalePlayer=new(ImageTexture.CreateFromImage(display),asset.FrameWidth,asset.FrameHeight,asset.FrameCount,new(asset.TileCenterAnchor.X,asset.TileCenterAnchor.Y),false);
    }

    private bool InitializePlayerEntry(string checkpoint,bool force=false)
    {
        if(!force&&(OS.GetEnvironment("FIRERED_PLAYER_ENTRY")=="0"||checkpoint.Length!=0))return false;
        _entrySource=PretMainMenuSource.Load(_root, _battleText);
        InitializeOpeningPresentation();
        _entryPlayerName=_opening!.Texts["gNameChoice_Red"];
        _entryRivalName=FieldMenuText.Get("opening.rival-initial-name");
        static Color Rgb(System.Collections.Immutable.ImmutableArray<int> c)=>Color.Color8((byte)c[0],(byte)c[1],(byte)c[2]);
        var palette=_entrySource.Palettes["sTextbox_Pal"].Rgb8;var colors=_entrySource.TextColors["sTextColor1"];
        string fontDirectory=Path.Combine(_root,"content/generated/ui/font");
        _entryHeadingFont=SourceGameFont.Load(fontDirectory,foreground:Rgb(palette[colors.Foreground]),shadow:Rgb(palette[colors.Shadow]));
        foreach(var gender in _entrySource.GenderColors)_entryGenderFonts.Add(gender.Key,SourceGameFont.Load(fontDirectory,foreground:Rgb(gender.Value.Rgb8),shadow:Rgb(palette[colors.Shadow])));
        ReadEntrySave();
        BeginIntroMovie();
        Write("playerEntryReady",new{mode=_entryMode,hasContinue=_entrySave is not null,backup=_entrySave?.Source.EndsWith(".bak",StringComparison.Ordinal)==true,
        oakSpeechImplemented=true,fullTitleIntroImplemented=false});
        return true;
    }

    private void ReadEntrySave()
    {
        ResetPlayTimeHost();
        _entrySave=null;_entryCursor=0;_entryNotice=null;_entryLoadCanRetry=false;
        var attempt=ReadPlayerSave();
        _entrySave=attempt.Saved;
        _entryHasExistingFiles=attempt.Result.Status!=SaveOperationStatus.Missing;
        if(!_entryHasExistingFiles)_entryObservedSlot="missing|missing";
        else if(!attempt.Result.Succeeded)SetEntryLoadFailure(attempt.Result);
        else if(attempt.Result.FromBackup)_entryNotice=_saveText.Get("entry.backup-available");
        _entryMode=_entryNotice is not null?"notice":"menu";
        if(_entryNotice is null&&_entrySave is null)BeginNewGameGuide();
        ApplySavedOptions(_entrySave?.State.Options??Game.Domain.Options.SourceGameOptions.Defaults);
    }

    private void ShowEntryName(string mode)
    {
        _entryMode=mode;_entryNotice=null;
        OpenNamingScreen(CurrentNamingBinding()!);QueueRedraw();
    }
    private void CancelEntryName()
    {
        bool player=_entryMode=="player-name";CloseNamingScreen();_entryNotice=null;
        if(player){_entryMode="gender";_entryCursor=_entryGender;}else BeginRivalNameChoices();QueueRedraw();
    }
    private void SubmitPlayerEntryName(string text)
    {
        if(_entryMode is not ("player-name" or "rival-name")||_entrySubmitFrame==Engine.GetProcessFrames())return;
        _entrySubmitFrame=Engine.GetProcessFrames();
        _entryActionFrame=_entrySubmitFrame;
        if(!NamingGlyphPreflight(text))return;
        if(!NameTextRules.IsValidTrainerName(text))
        {_entryNotice=FieldMenuText.Get("opening.notice.name-encoding");QueueRedraw();return;}
        bool player=_entryMode=="player-name";CloseNamingScreen();_entryNotice=null;
        if(player){_entryPlayerName=text;_entryMode="player-confirm";}
        else{_entryRivalName=text;_entryMode="rival-confirm";}
        _entryCursor=0;
        QueueRedraw();
    }
    private void StartChosenGame()
    {
        Span<byte> random=stackalloc byte[8];System.Security.Cryptography.RandomNumberGenerator.Fill(random);
        var options=new CampaignNewGameOptions(BinaryPrimitives.ReadUInt32LittleEndian(random),_entryPlayerName,_entryRivalName,_entryGender,BinaryPrimitives.ReadUInt32LittleEndian(random[4..]));
        var session=CampaignSession.CreateCurrent(_catalog,options);
        _entryOverwritePending=_entryHasExistingFiles;
        EnterPlayerSession(session);
        Write("playerNewGame",_diagnostics.Payload(new{options,_session.Revision,existingSavePreserved=_entryOverwritePending},
            ()=>new{options,hash=CanonicalJson.Hash(_session.State),existingSavePreserved=_entryOverwritePending}));
    }
    private void ContinueChosenGame()
    {
        // Re-read and validate at selection: a summary is not permission to use stale bytes.
        var attempt=ReadPlayerSave();
        if(!attempt.Result.Succeeded){_entrySave=null;_entryMode="notice";SetEntryLoadFailure(attempt.Result);return;}
        var saved=attempt.Saved!;
        EnterPlayerSession(CampaignSession.RestoreCurrent(_catalog,saved.State,saved.Generation));_entryOverwritePending=false;
        StartQuestLogPlayback();
        Write("playerContinue",_diagnostics.Payload(new{saved.Source,saved.Generation},
            ()=>new{saved.Source,saved.Generation,hash=CanonicalJson.Hash(_session.State)}));
        if(attempt.Result.FromBackup)ShowPlayerSaveResult(attempt.Result);
    }
    private void SetEntryLoadFailure(SaveOperationResult result)
    {
        _entryLoadCanRetry=true;_entryNotice=CampaignSaveFeedback.Message(_saveText, result);
        Write("playerEntryLoadRefused",new{result.Status,result.Files,error=result.DiagnosticDetail});
    }
    private void EnterPlayerSession(CampaignSession session)
    {
        ResetQuestLogPlayback();
        _entryMode="closed";_entryNotice=null;CloseNamingScreen();
        _entryFreshFieldFrame=Engine.GetProcessFrames();
        _session=session;ResetPlayTimeHost();_sequence=session.Revision;_lastFanfareSequence=0;_fanfareRemaining=0;_blockedAutomaticWait=null;_lastCommandFault=false;_waitId=null;_waitKind=null;
        ResetSummaryPresentation();ResetSourceAudio();LoadMap(session.State.World.Player.MapId);Synchronize(null,session.State);_held=null;
    }
    private void EntryKey(Key key)
    {
        if(key != Key.Escape && TryIntroMovieKey(key))return;
        if(OpeningGuideKey(key) && key != Key.Escape)return;
        if(key != Key.Escape && OpeningRivalChoiceKey(key))return;
        if(key==Key.Escape)
        {
            _openingPortraitTransition=null;_openingPortraitCompleted=null;
            if(_entryMode is "player-name" or "rival-name")CancelEntryName();
            else if(_entryMode is "gender" or "confirm" or "oak-speech" or "player-confirm" or "rival-confirm"&&_entrySave is not null){_entryMode="menu";_entryCursor=0;}
            else RequestQuit();return;
        }
        if(_entryMode=="title")
        {
            if(!_openingTitleReveal.Complete && key is Key.Enter or Key.Tab or Key.Backspace)
            { _openingTitleReveal.Skip(); _entryActionFrame=Engine.GetProcessFrames(); return; }
            if(key is Key.Enter or Key.Tab&&_entryActionFrame!=Engine.GetProcessFrames())
            {_entryActionFrame=Engine.GetProcessFrames();BeginTitleExit();}return;
        }
        if(_entryMode is "title-exit" or "oak-exit" or "oak-return" or "guide-exit")return;
        if(_entryMode=="notice")
        {
            if(_entryLoadCanRetry&&key==Key.Enter){ReadEntrySave();return;}
            if(key is Key.Enter or Key.Backspace){_entryNotice=null;_entryLoadCanRetry=false;_entryMode="menu";if(_entrySave is null)BeginNewGameGuide();}
            return;
        }
        if(_openingPortraitTransition is {Complete:false})return;
        if(_entryMode is "player-name" or "rival-name")return;
        if(_entryMode=="oak-speech")
        {
            if(key is Key.Enter or Key.Backspace && _entryActionFrame!=Engine.GetProcessFrames())
            {_entryActionFrame=Engine.GetProcessFrames();AdvanceOpeningSpeech();}
            return;
        }
        if(key is Key.Up or Key.Down)_entryCursor=_entryMode=="menu"
            ?Math.Clamp(_entryCursor+(key==Key.Up?-1:1),0,1):_entryCursor^1;
        if(key==Key.Backspace)
        {
            if(_entryMode=="rival-confirm")BeginRivalNameChoices();
            else if(_entryMode=="confirm")ShowEntryName("rival-name");
            else if(_entryMode=="player-confirm")ShowEntryName("player-name");
            else if(_entryMode=="gender"&&_entrySave is not null){_entryMode="menu";_entryCursor=0;}
            return;
        }
        if(key!=Key.Enter)return;
        if(_entryActionFrame==Engine.GetProcessFrames())return;_entryActionFrame=Engine.GetProcessFrames();
        if(_entryMode=="menu")
        {if(_entryCursor==0)ContinueChosenGame();else BeginNewGameGuide();}
        else if(_entryMode=="gender"){_entryGender=_entryCursor;_entryPlayerName=_opening!.Texts[_entryGender==0?"gNameChoice_Red":"gNameChoice_Leaf"];BeginOpeningSpeech("player-name",("YourNameWhatIsIt",OpeningPlayerPortrait));}
        else if(_entryMode=="player-confirm")
        {if(_entryCursor==0)BeginOpeningSpeech("rival-name",("WhatWasHisName","rival"));else ShowEntryName("player-name");}
        else if(_entryMode=="rival-confirm")
        {if(_entryCursor==0)BeginOpeningSpeech("start",("RememberRivalsName","rival"),("LetsGo",OpeningPlayerPortrait));else BeginRivalNameChoices();}
        else if(_entryMode=="confirm"){if(_entryCursor==0)StartChosenGame();else ShowEntryName("player-name");}
    }
    private bool TryPlayerEntryInput(InputEvent input)
    {
        if(!PlayerEntryActive)return _entryFreshFieldFrame==Engine.GetProcessFrames();
        if(input is InputEventMouseButton or InputEventMouseMotion)
        {
            var mouse=input as InputEventMouseButton;
            bool motion=input is InputEventMouseMotion;
            bool wheel=mouse?.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown;
            bool right=mouse?.ButtonIndex==MouseButton.Right;
            if(!motion&&!wheel&&!right&&mouse!.ButtonIndex!=MouseButton.Left)return true;
            if(mouse is {Pressed:false})
            {if(right)_menuRightMousePressed=false;else if(!wheel)_menuMousePressed=false;return true;}
            if(motion||wheel)
            {
                if(_menuMousePressed||_menuRightMousePressed||input is InputEventMouseMotion {ButtonMask:not 0})return true;
            }
            else
            {
                if(_menuMousePressed||_menuRightMousePressed)return true;
                if(right)_menuRightMousePressed=true;else _menuMousePressed=true;
            }
            if(mouse is {DoubleClick:true}||_menuMouseActionFrame==Engine.GetProcessFrames()||
                _entryActionFrame==Engine.GetProcessFrames()||_busy)return true;
            // Entry owns its own transition frame. The dormant field's initial
            // LoadMap fade is intentionally paused while this menu is displayed.
            Vector2 position=((InputEventMouse)input).Position;
            if(!GetViewport().GetVisibleRect().HasPoint(position))return true;
            var point=GetGlobalTransformWithCanvas().AffineInverse()*position;
            if(!OpeningItemUiSkin.LogicalRect.HasPoint(point))return true;
            if(right)
            {
                _menuMouseActionFrame=Engine.GetProcessFrames();
                if (_entryMode != "oak-speech") _entryActionFrame=_menuMouseActionFrame;
                if(_entryMode is "player-name" or "rival-name")CancelEntryName();
                else EntryKey(Key.Backspace);
                QueueRedraw();GetViewport().SetInputAsHandled();return true;
            }
            if(!motion&&!wheel&&NamingPointer(point))
            {_menuMouseActionFrame=Engine.GetProcessFrames();_entryActionFrame=_menuMouseActionFrame;GetViewport().SetInputAsHandled();return true;}
            int selected=-1;
            if(_entryMode=="menu")
            {
                foreach(var (name,index) in new[]{(PretMainMenuSource.ContinueWindow,0),(PretMainMenuSource.NewGameWindow,1)})
                {var region=_entrySource!.SelectionRegions[name];if(new Rect2(region.Left,region.Top,region.Width,region.Height).HasPoint(point)){selected=index;break;}}
            }
            else if(_entryMode=="rival-choice"&&_openingVisualFrame>=30&&RivalNameChoiceBounds.HasPoint(point))
                selected=Math.Clamp((int)((point.Y-16)/16),0,4);
            else if(_entryMode is "player-confirm" or "rival-confirm"&&new Rect2(8,8,64,48).HasPoint(point))
                selected=Math.Clamp((int)((point.Y-16)/16),0,1);
            else if(_entryMode=="gender"&&new Rect2(136,64,88,48).HasPoint(point))
                selected=Math.Clamp((int)((point.Y-72)/16),0,1);
            else if(_entryMode=="confirm"&&new Rect2(24,88,192,32).HasPoint(point))
                selected=(int)((point.Y-88)/16);
            if(motion||wheel)
            {
                // Selection only: neither path can enter a save or submit identity.
                if(selected>=0)
                {
                    if(wheel)EntryKey(mouse!.ButtonIndex==MouseButton.WheelUp?Key.Up:Key.Down);
                    else _entryCursor=selected;
                    QueueRedraw();GetViewport().SetInputAsHandled();
                }
                return true;
            }
            bool confirm=_entryMode is "intro-movie" or "title" or "controls-guide" or "pikachu-guide"||selected>=0||_entryMode is "notice" or "oak-speech"&&MouseMessage.HasPoint(point);
            if(selected>=0)_entryCursor=selected;
            if(confirm){_menuMouseActionFrame=Engine.GetProcessFrames();EntryKey(Key.Enter);}
            QueueRedraw();GetViewport().SetInputAsHandled();return true;
        }
        if(input is InputEventKey {Pressed:true,Echo:false} key)
        {
            EntryKey(key.Keycode);
            QueueRedraw();GetViewport().SetInputAsHandled();
        }
        return true;
    }
    private string EntryText(string symbol)=>_entrySource!.Texts[symbol].Text;
    private void DrawPlayerEntry()
    {
        if(DrawOpeningEntry())return;
        var source=_entrySource!;var bg=source.Palettes["sBg_Pal"].Rgb8[0];DrawRect(new(0,0,240,160),Color.Color8((byte)bg[0],(byte)bg[1],(byte)bg[2]));
        if(_entryMode=="menu"&&_entrySave is {} saved)
        {
            var continueBox=source.Windows[PretMainMenuSource.ContinueWindow];var newBox=source.Windows[PretMainMenuSource.NewGameWindow];
            void Box(PretMainMenuWindow box)=>_battleSkin.DrawUserFrame(this,new(box.FrameX,box.FrameY,box.FrameWidth,box.FrameHeight));
            Box(continueBox);Box(newBox);
            SourceText(_entryHeadingFont!,new(continueBox.PixelX+2,continueBox.PixelY+2),EntryText("gText_Continue"));
            SourceText(_entryHeadingFont!,new(newBox.PixelX+2,newBox.PixelY+2),EntryText("gText_NewGame"));
            int badges=Enumerable.Range(1,8).Count(n=>saved.State.Script.Flags.GetValueOrDefault("FLAG_BADGE"+n.ToString("00")+"_GET"));
            foreach(var (symbol,value,row) in new[]{("gText_Player",saved.State.PlayerName,18),("gText_Time",FormatPlayTime(saved.State.PlayTime),34),("gText_Pokedex",PokedexCaughtCount(saved.State).ToString(),50),("gText_Badges",badges.ToString(),66)})
            {if(symbol=="gText_Pokedex"&&!saved.State.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))continue;SourceText(_entryGenderFonts[saved.State.PlayerGender],new(continueBox.PixelX+2,continueBox.PixelY+row),EntryText(symbol));SourceText(_entryGenderFonts[saved.State.PlayerGender],new(continueBox.PixelX+62,continueBox.PixelY+row),value);}
            if(saved.State.PlayTime.SinceCheckpoint)SourceText(_sourceSmall,new(8,146),CheckpointTimeNote);
            var selected=source.SelectionRegions[_entryCursor==0?PretMainMenuSource.ContinueWindow:PretMainMenuSource.NewGameWindow];
            // Source darkens all layers except the selected rectangular window.
            float amount=source.Rules.DarkeningCoefficient/16f;
            DrawRect(new(0,0,240,selected.Top),new Color(0,0,0,amount));DrawRect(new(0,selected.Bottom,240,160-selected.Bottom),new Color(0,0,0,amount));
            DrawRect(new(0,selected.Top,selected.Left,selected.Height),new Color(0,0,0,amount));DrawRect(new(selected.Right,selected.Top,240-selected.Right,selected.Height),new Color(0,0,0,amount));
        }
        else
        {
            _battleSkin.DrawStandardFrame(this,new(8,8,224,136));SourceText(_sourceNormal,new(24,20),EntryText("gText_NewGame"));

        }
        if(_entryLoadCanRetry&&_entryMode=="notice")
        {SourceText(_sourceSmall,new(24,52),_saveText.Get("entry.control.retry"));SourceText(_sourceSmall,new(24,68),_saveText.Get("entry.control.new-game"));}
        if(_entryNotice is not null)ItemMessageBox(_entryNotice);
    }
    private bool RequestEntrySaveOverwrite()
    {
        if(_entrySource is null)return false;
        // AttemptPlayerSave owns the current-state/presentation preflight.
        string current=EntrySlotFingerprint();_entrySaveAttemptSlot=current;
        bool changed=current!=_entryObservedSlot;
        if(!_entryOverwritePending&&!changed)return false;
        if(_entryOverwriteConfirmed&&current==_entryPromptSlot)return false;
        _entryPromptSlot=current;
        _fieldMenuMode="save-overwrite";_fieldMenuNotice=null;_fieldSaveChoice=1;_held=null;QueueRedraw();return true;
    }
    private string EntrySlotFingerprint()
    {
        string One(string path)
        {
            FileStream stream;
            try{stream=new FileStream(path,FileMode.Open,System.IO.FileAccess.Read,FileShare.Read);}
            catch(FileNotFoundException){return "missing";}
            catch(DirectoryNotFoundException){return "missing";}
            using(stream)
            {
                // Saving requires a complete bounded observation. If a companion is
                // inaccessible or oversized, refuse replacement rather than assuming it is safe.
                long length=stream.Length;
                if(length>LocalSaveStorage.MaximumSaveBytes)throw new InvalidDataException("save_file_too_large");
                using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[8192];long remaining=length;
                while(remaining>0){int n=stream.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));if(n==0)throw new IOException("save_file_changed");hash.AppendData(buffer,0,n);remaining-=n;}
                if(stream.Length!=length||stream.ReadByte()!=-1)throw new IOException("save_file_changed");
                return length+":"+Convert.ToHexStringLower(hash.GetHashAndReset());
            }
        }
        return One(PlayerSavePath)+"|"+One(PlayerSavePath+".bak");
    }
    private LoadedSave<CampaignState> LoadEntryProtectedSave()
    {
        var candidate=PlayerSaveOperations.LoadVerifiedCandidate<CampaignState>(new LocalSaveStorage(),PlayerSavePath,
            _session.RulesVersion,_catalog.ContentVersion,_session.ValidatePlayerSave);
        _entryObservedSlot=null;
        string observed;
        try{observed=EntrySlotFingerprint();}
        catch(Exception error) when(SaveOperationResult.IsExpectedFailure(error))
        {
            // A verified candidate remains usable when its companion is unreadable.
            // Saving will still require the complete observation and must refuse if it fails.
            Write("playerEntryObservationUnavailable",new{candidate.Saved.Source,error=error.ToString()});
            return candidate.Saved;
        }
        string selected=observed.Split('|')[candidate.Saved.Source==PlayerSavePath?0:1];
        if(selected!=candidate.Fingerprint)throw new IOException("save_file_changed_during_load");
        _entryObservedSlot=observed;
        return candidate.Saved;
    }
    private ISaveStorage EntrySaveStorage()=>_entrySource is null?new LocalSaveStorage()
        :new EntryCheckedSaveStorage(EntrySlotFingerprint,_entrySaveAttemptSlot??throw new InvalidOperationException("save_observation_missing"));
    private void EntrySaveSucceeded()
    {
        _entryOverwritePending=false;
        if(_entrySource is null)return;
        // The staged transaction already committed. A later observation failure must
        // not report that the save failed; require fresh overwrite confirmation next time.
        _entryObservedSlot=null;
        try{_entryObservedSlot=EntrySlotFingerprint();}
        catch(Exception error) when(SaveOperationResult.IsExpectedFailure(error))
        {Write("playerSaveObservationFailed",new{saveCommitted=true,error=error.ToString()});}
    }
    private sealed class EntryCheckedSaveStorage(Func<string> fingerprint,string expected):ISaveStorage
    {
        private readonly LocalSaveStorage storage=new();
        private void Check(){if(fingerprint()!=expected)throw new IOException("save_file_changed_before_commit");}
        public byte[]? Read(string slot)=>storage.Read(slot);
        public void WriteStaged(string slot,ReadOnlySpan<byte> bytes){Check();storage.WriteStaged(slot,bytes);}
        public void Commit(string slot,bool preservePrimary){Check();storage.Commit(slot,preservePrimary);}
    }
    public override void _ExitTree()
    {
        Input.JoyConnectionChanged-=OnCampaignJoyConnectionChanged;
        ClearFieldUiPalette();DisposeFieldFogPresentation();DisposeHelpPresentation();
        // A worker may finish after this scene leaves, but has no callback or node reference.
        _startupExited=true;_startupReady=false;_catalogLoad=null;
        DisposeSourceMoveAnimation();DisposeSlotMachinePresentation();DisposeFameCheckerPresentation();DisposeTeachyTvPresentation();DisposeVsSeekerPresentation();
        _saffronGymTrainerPortraits.Clear();
        DisposeMailPresentation();_pokemonCollectionArt=null;_earlyDex.Clear();_supplementalDex.Clear();_sharedTrainerPresentation.Clear();_registeredTrainerPresentation.Clear();DisposeGiftEggPresentation();DisposeMemorialPillarPresentation();DisposeOrdinaryEvolutionPresentation();DisposeBreedingPresentation();DisposeGameCornerPresentation();DisposeStoneEvolutionPresentation();DisposeQwilfishPresentation();DisposeSeafoamPresentation();DisposeFossilLabPresentation();DisposeFightingDojoPresentation();DisposePowerPlantPresentation();DisposeEventIslandPresentation();DisposeSevenIslandPresentation();DisposeLeagueRematchPresentation();DisposeMewtwoPresentation();DisposeCelioPresentation();DisposeFiveIslandPresentation();DisposeSixIslandPresentation();DisposeFourIslandPresentation();DisposeSeviiRubyPresentation();DisposeFirstSeviiPresentation();_viridianGymPresentation=null;DisposeSilphPresentation();
        DisposeOptionsPresentation();DisposeCapturePresentation();DisposeStorageCustomizationPresentation();
        DisposePokemonTowerPresentation();DisposeCenterHealingPresentation();DisposeOldManPresentation();
        DisposeBattlePresentation();DisposeCorePresentation();
        DisposeMapResources();
        DisposeOpeningPresentation();
        _entryHeadingFont?.Dispose();foreach(var font in _entryGenderFonts.Values)font.Dispose();_entryGenderFonts.Clear();
        _femalePlayer?.Texture.Dispose();DisposeTownMapPresentation();DisposeCityServicesPresentation();
    }
}
