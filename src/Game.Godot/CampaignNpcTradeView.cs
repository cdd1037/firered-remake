using Game.Application;
using Game.Content;
using Game.Domain.Capture;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.World;
using Godot;

/// <summary>Source-backed NPC wireless scene; the Application transaction is its only exchange owner.</summary>
public partial class CampaignView
{
    private int _npcTradeCursor;
    private int _npcTradeAudioFrame = -1;
    private bool _npcTradeIncomingCry;
    private NpcTradeScenePhase? _npcTradeLoggedPhase;
    private bool NpcTradeAudioComplete => _sourceAudio is null || _audioFaulted ||
        (_sourceAudio.Snapshot().Cry is not { Playing:true } && _sourceAudio.Snapshot().FanfareToken is null);
    private void ProcessNpcTradeSceneAudio()
    {
        int frame=NpcTradeSceneFrame;
        if(frame>_npcTradeAudioFrame+15)
        { frame=_npcTradeAudioFrame+15;_waitElapsed=frame/60.0; } // Preserve visible motion after a stalled host.
        int cryFrame=NpcTradeSceneTimeline.StartFrame(NpcTradeScenePhase.ReceivedMessage);
        if(frame>=cryFrame)
        {
            bool startedNow=!_npcTradeIncomingCry;
            if(startedNow)
            {
                _npcTradeIncomingCry=true;
                SourceCry(_session.State.NpcTrade!.Incoming!.Pokemon);
            }
            if(startedNow || _sourceAudio is not null && !_audioFaulted && _sourceAudio.Snapshot().Cry is { Playing:true })
            {
                // Preserve the source WAIT_FOR_MON_CRY gate before the ten-frame fanfare timer.
                frame=cryFrame; _waitElapsed=frame/60.0;
            }
        }
        int outgoingCry=NpcTradeSceneTimeline.StartFrame(NpcTradeScenePhase.SendMessage);
        if(_npcTradeAudioFrame<outgoingCry && frame>=outgoingCry)SourceCry(_session.State.NpcTrade!.Outgoing!.Pokemon);
        foreach(var cue in NpcTradeSceneTimeline.SoundCues)
        {
            if(cue.Frame<=_npcTradeAudioFrame || cue.Frame>frame)continue;
            if(cue.Symbol=="MUS_EVOLVED")SourceSound(cue.Symbol,fanfare:true);
            else if(cue.Symbol.StartsWith("MUS_",StringComparison.Ordinal))SourceMusic(cue.Symbol);
            else SourceSound(cue.Symbol);
        }
        _npcTradeAudioFrame=Math.Max(_npcTradeAudioFrame,frame);
        var phase=NpcTradeSceneTimeline.At(frame).Phase;
        if(_npcTradeLoggedPhase!=phase)
        { _npcTradeLoggedPhase=phase;Write("npcTradeScene",new{phase=phase.ToString(),frame,readyFrame=NpcTradeSceneTimeline.ReadyFrame}); }
    }

    private PretNpcTradeScene? _npcTradeSceneAssets;
    private Texture2D? _npcTradeAtlas;
    private int NpcTradeSceneFrame => Math.Max(0, (int)(_waitElapsed * 60 + 1e-9));
    private void DisposeNpcTradePresentation()
    {
        _npcTradeAudioFrame=-1;_npcTradeIncomingCry=false;_npcTradeLoggedPhase=null;
        _npcTradeAtlas?.Dispose(); _npcTradeAtlas = null; _npcTradeSceneAssets = null;
    }
    private void EnsureNpcTradeScene()
    {
        if (_npcTradeAtlas is not null) return;
        _npcTradeSceneAssets = PretNpcTradeScene.Load(_root);
        LoadSummaryDescriptions(); EnsureSourceMoveAssets();
        using var image = NativeImages.LoadPng(_npcTradeSceneAssets.Atlas,
            _npcTradeSceneAssets.Document.Width, _npcTradeSceneAssets.Document.Height);
        _npcTradeAtlas = ImageTexture.CreateFromImage(image);
    }
    private void TradeRegion(string id, Rect2 destination, Rect2? region = null, Color? color = null, bool flip = false)
    {
        EnsureNpcTradeScene();
        var a = _npcTradeSceneAssets!.Document.Assets.Single(a => a.Id == id);
        var r = region ?? new Rect2(0, 0, a.Width, a.Height);
        r.Position += new Vector2(a.X, a.Y);
        if (flip) destination.Size = -destination.Size;
        DrawTextureRectRegion(_npcTradeAtlas!, destination, r, color ?? Colors.White);
    }

    private ulong _npcTradeClosedFrame = ulong.MaxValue;
    private bool NpcTradeChoosing => _session?.State.NpcTrade is { Phase: CampaignNpcTradePhase.ChoosingParty } &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.PartySelection;
    private bool NpcTradePresenting => _session?.State.NpcTrade is { Phase: CampaignNpcTradePhase.Presenting } &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.Presentation;

    private void SynchronizeNpcTradePresentation(CampaignState? before, CampaignState after)
    {
        bool wasChoosing = before?.NpcTrade is { Phase: CampaignNpcTradePhase.ChoosingParty };
        bool choosing = after.NpcTrade is { Phase: CampaignNpcTradePhase.ChoosingParty };
        bool wasPresenting = before?.NpcTrade is { Phase: CampaignNpcTradePhase.Presenting };
        bool presenting = after.NpcTrade is { Phase: CampaignNpcTradePhase.Presenting };
        if (!wasChoosing && choosing) { _npcTradeCursor = 0; _partyLastRightSlot = 1; }
        if (!wasPresenting && presenting) { _npcTradeAudioFrame=-1;_npcTradeIncomingCry=false;_npcTradeLoggedPhase=null; }
        if (wasChoosing != choosing || wasPresenting != presenting)
        {
            _held = null; _repeat = 0; ReleaseFieldTextInput();
            _fade = 16.0 / 60;
            if (wasChoosing && !choosing || wasPresenting && !presenting)
                _npcTradeClosedFrame = Engine.GetProcessFrames();
        }
    }

    private bool ConsumeNpcTradeTransitionInput(InputEvent input) => ConsumeServiceTransitionInput(input, _npcTradeClosedFrame);

    private bool NpcTradeInput(InputEventKey key)
    {
        if ((!NpcTradeChoosing && !NpcTradePresenting) || key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        // Source final A acknowledgement is allowed only after the whole visual sequence.
        // No key can cancel, finish early, select a second row or reach the field.
        if (NpcTradePresenting)
        {
            if (key.Pressed && !key.Echo && key.Keycode is Key.Enter or Key.KpEnter or Key.A &&
                NpcTradeSceneFrame >= NpcTradeSceneTimeline.ReadyFrame && NpcTradeAudioComplete) Apply(new("ack"));
            return true;
        }
        if (!key.Pressed || key.Echo || _fade > 0) return true;
        int count = _session.State.Party.Length + 1;
        if (NativePartyMove(key.Keycode, ref _npcTradeCursor, count - 1)) { }
        else if (key.Keycode is Key.Backspace or Key.Escape or Key.B) Apply(new("npc_trade_cancel"));
        else if (key.Keycode is Key.Enter or Key.KpEnter or Key.A)
        {
            if (_npcTradeCursor == _session.State.Party.Length) Apply(new("npc_trade_cancel"));
            else if (_npcTradeCursor >= 0 && _npcTradeCursor < _session.State.Party.Length)
                Apply(new("npc_trade_choose", PartyIndex: _npcTradeCursor)
                    { TargetInstanceId = _session.State.Party[_npcTradeCursor].InstanceId });
        }
        return true;
    }

    private bool NavigateNpcTradePointer(Vector2 point, int direction)
    {
        if (!NpcTradeChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_npcTradeCursor == row) return false;
        _npcTradeCursor = row; return true;
    }

    private bool MouseNpcTrade(Vector2 point)
    {
        if (NpcTradePresenting) { MouseKey(Key.Enter); return true; }
        if (!NpcTradeChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        _npcTradeCursor = row; MouseKey(Key.Enter); return true;
    }

    private string NpcTradeSourceText(string symbol) =>
        _catalog.NpcTradeContent?.Texts.GetValueOrDefault(symbol)
        ?? throw new InvalidDataException("NPC trade presentation text missing: " + symbol);

    private string NpcTradePresentationText(int stage)
    {
        var trade = _session.State.NpcTrade!;
        var outgoing = trade.Outgoing ?? throw new InvalidDataException("NPC trade outgoing instance missing.");
        var incoming = trade.Incoming ?? throw new InvalidDataException("NPC trade incoming instance missing.");
        string symbol = stage switch
        {
            0 => "gText_XWillBeSentToY", 1 => "gText_ByeByeVar1",
            2 => "gText_XSentOverY", _ => "gText_TakeGoodCareOfX"
        };
        // trade_scene.c TradeBufferOTnameAndNicknames uses these exact bindings.
        string text = NpcTradeSourceText(symbol)
            .Replace("{STR_VAR_1}", incoming.OriginalTrainerName, StringComparison.Ordinal)
            .Replace("{STR_VAR_2}", _catalog.PokemonDisplayName(outgoing), StringComparison.Ordinal)
            .Replace("{STR_VAR_3}", _catalog.PokemonDisplayName(incoming), StringComparison.Ordinal);
        if (text.Contains('{') || text.Contains('}')) throw new InvalidDataException("Unrendered NPC trade source control token.");
        return text;
    }

    private bool DrawNpcTrade()
    {
        if (NpcTradeChoosing)
        {
            DrawServicePartyChooser(_npcTradeCursor,
                NpcTradeSourceText(NameRaterRules.ChooserPromptSymbol(_session.State.Party)), NpcTradeSourceText("gFameCheckerText_Cancel"));
            return true;
        }
        if (!NpcTradePresenting) return false;
        EnsureNpcTradeScene();
        var trade = _session.State.NpcTrade!;
        var outgoing = trade.Outgoing ?? throw new InvalidDataException("NPC trade outgoing instance missing.");
        var incoming = trade.Incoming ?? throw new InvalidDataException("NPC trade incoming instance missing.");
        var (phase, frame) = NpcTradeSceneTimeline.At(NpcTradeSceneFrame);
        void Portrait(bool received, float x, float y, float scale = 1, bool flip = false, bool white = false)
        {
            if (scale <= 0) return;
            var appearance = PresentedAppearance((received ? incoming : outgoing).Pokemon);
            var texture = _battleArt[(appearance.Species, "front", appearance.Shiny)].Texture;
            float offset = _sourceBattlerGeometry[(appearance.Species,"front")].YOffset;
            if (phase == NpcTradeScenePhase.CrossingPortraits && received || phase == NpcTradeScenePhase.Recall && frame >= 21) offset=0;
            if (phase == NpcTradeScenePhase.Release) offset*=Math.Clamp((frame-21)/32f,0,1);
            y += offset;
            if (flip) flip = _summaryPresentation!.FlipPortrait(outgoing.Pokemon.Species.Value);
            if (white) texture = EvolutionSilhouette(texture);
            var rect = new Rect2(x - 32 * scale, y - 32 * scale, 64 * scale, 64 * scale);
            if (flip) rect.Size = new(-rect.Size.X,rect.Size.Y);
            DrawTextureRect(texture, rect, false);
        }
        void Background(string id, int y = 0)
        {
            if(id=="crossing")DrawRect(new(0,0,240,160),Colors.Black); // bank 3 is black before the source window opens
            else TradeRegion(id,new(0,0,240,160),new Rect2(0,y,240,160));
        }
        void Console(int y) => Background("console",y);
        void Ball(int y, int spin, float width = 16) => TradeRegion("pokeball", new(120-width/2,y-8,width,16), new Rect2(0,Math.Clamp(spin,0,11)*16,16,16));
        void CommonBall(int y, int tile)
        {
            DrawTextureRectRegion(_captureBallAtlas,new Rect2(112,y-8,16,16),
                new Rect2(_captureBallAssets.Column(CaptureBall.Poke),tile*16,16,16));
        }
        void Particles(int age, int y)
        {
            for(int i=0;i<16;i++)
            {
                int t=age-i-1;if(t is <0 or >=25)continue;
                int angle=i%8*32;
                int x=120+((_captureBallAssets.Sine[angle]*t*2)>>8);
                int py=y-5+((_captureBallAssets.Sine[angle+64]*t*2)>>8);
                int[] frames=[0,1,2,0,2,1];
                DrawSetTransform(new(x,py),0,new((t+1)%6==3?-1:1,1));
                DrawTextureRectRegion(_captureParticleAtlas,new(-4,-4,8,8),new(0,frames[(t+1)%6]*8,8,8));
                DrawSetTransform(Vector2.Zero);
            }
        }
        void Glow(float x, float y, bool small = true)
        {
            float size = 32 * (256 - 10 * (frame%10 < 5 ? frame%10 : 10-frame%10))/256f;
            if(small)TradeRegion("link_mon_glow",new(x-size/2,y-size/2,size,size),color:new Color(1,1,1,.75f));
            TradeRegion("link_mon_shadow",new(x-8,y-16,16,32),new Rect2(0,small?32:0,16,32),
                frame%12/3%2==1 ? Colors.White : new Color(.7f,.85f,1),true);
        }
        void Signal(bool receive, int age, int offset)
        {
            int palette=16;
            foreach(var step in _npcTradeSceneAssets!.Document.SignalParameters)
            {
                if(step[1]<0)break;
                if(age<step[1]+1){palette=step[0];break;}
                age-=step[1]+1;
            }
            if(palette==16)return;
            int y = ((256-offset%256)%256);
            for(int copy=0;copy<2;copy++)
                TradeRegion(receive?"signal_receive":"signal_send",new(56,y-copy*256,128,128),new Rect2(palette%4*128,palette/4*128,128,128));
        }
        void WhiteColumn(int halfWidth) => DrawRect(new(120-halfWidth,0,halfWidth*2,160),Colors.White);
        void ScreenFlash()
        {
            int[] frames = [0,1,2,3,2,1,0];
            TradeRegion("gba_screen",new(88,64,64,32),new Rect2(0,frames[(frame/2)%7]*32,64,32),flip:true);
        }
        void Zoom(bool inward)
        {
            float zoom = inward ? Math.Min(1024,128+frame*52) : Math.Max(256,1024-frame*52);
            if(phase is NpcTradeScenePhase.SendFadeIn or NpcTradeScenePhase.FadeToPokemon)zoom=1024;
            float scale = zoom/128;
            TradeRegion("affine",new(120-64*scale,80-92*scale,128*scale,128*scale));
        }
        DrawRect(new(0,0,240,160),Colors.Black);
        switch (phase)
        {
            case NpcTradeScenePhase.SlideIn:
                TradeRegion("shadow",new(0,0,240,160),new Rect2(180-frame*3,0,240,160));
                Portrait(false,-60+frame*3,60); break;
            case NpcTradeScenePhase.SendMessage: Background("shadow"); Portrait(false,120,60); break;
            case NpcTradeScenePhase.Recall:
                Background("shadow");
                int shrink=Math.Max(0,frame-21);
                float scale=frame<21?1:Math.Max(0,256-Math.Min(18,shrink)*2-Math.Max(0,shrink-18)*16)/256f;
                Portrait(false,120,60-shrink*.375f,scale,white:frame>=21);
                CommonBall(32,frame<21?0:frame<54?1:2);Particles(frame-21,32);break;
            case NpcTradeScenePhase.DepartBounce:
            case NpcTradeScenePhase.DepartLaunch:
                Background("shadow");
                int departure=frame+(phase==NpcTradeScenePhase.DepartLaunch?44:0);
                Ball(NpcTradeSceneTimeline.DepartBallY(departure,_npcTradeSceneAssets!.Document.BallVelocity),Math.Min(24,departure)/3%12,
                    departure<=64?16:16*256f/(256-8*Math.Min(20,departure-64))); break;
            case NpcTradeScenePhase.FadeToSend:
                Background("shadow"); DrawRect(new(0,0,240,160),new Color(0,0,0,frame/16f)); break;
            case NpcTradeScenePhase.SendFadeIn:
                Zoom(false); DrawRect(new(0,0,240,160),new Color(0,0,0,1-frame/16f)); break;
            case NpcTradeScenePhase.ZoomOut: Zoom(false); break;
            case NpcTradeScenePhase.SendFlashDelay: Console(348); break;
            case NpcTradeScenePhase.SendFlash: Console(348); ScreenFlash(); break;
            case NpcTradeScenePhase.SendSignal: Console(348);Signal(false,frame,80);break;
            case NpcTradeScenePhase.PanAway:
                Console(348-frame);break;
            case NpcTradeScenePhase.TravelOut: Console(316-frame*3); Glow(120,80); break;
            case NpcTradeScenePhase.TravelOffscreen: Console(166); Glow(120,80-frame*2); break;
            case NpcTradeScenePhase.FadeToCrossing: Console(166); DrawRect(new(0,0,240,160),new Color(0,0,0,frame/16f)); break;
            case NpcTradeScenePhase.CrossingEnter:
                Background("crossing"); Glow(111,170-frame*3,false); Glow(129,-10+frame*3,false);
                if(frame<17)DrawRect(new(0,0,240,160),new Color(0,0,0,1-frame/16f)); break;
            case NpcTradeScenePhase.CrossingFlash:
                Background("crossing"); Glow(111,80,false); Glow(129,80,false);
                WhiteColumn(Math.Min(40,frame*5)); break;
            case NpcTradeScenePhase.CrossingPortraits:
                Background("crossing");WhiteColumn(Math.Min(40,(frame+4)*5)); Glow(111,80,false); Glow(129,80,false);
                Portrait(false,40,192-frame*3,flip:true); Portrait(true,200,-32+frame*3); break;
            case NpcTradeScenePhase.CrossingExit:
                Background("crossing");WhiteColumn(Math.Max(0,40-frame*5)); Glow(111,80-frame*3,false); Glow(129,80+frame*3,false); break;
            case NpcTradeScenePhase.FadeToReceive:
                Background("crossing"); DrawRect(new(0,0,240,160),new Color(0,0,0,frame/16f)); break;
            case NpcTradeScenePhase.ReceiveFadeIn:
                Console(166); Glow(120,-20); DrawRect(new(0,0,240,160),new Color(0,0,0,1-frame/16f)); break;
            case NpcTradeScenePhase.TravelIn: Console(166); Glow(120,-20+frame*4); break;
            case NpcTradeScenePhase.PanToConsole:
                Console(Math.Min(316,166+frame*3)); Glow(120,64);
                if(frame>=10)Signal(true,frame-10,(412+frame*3)%512);break;
            case NpcTradeScenePhase.ReceiveSignal: Console(316);Signal(true,frame+41,53);break;
            case NpcTradeScenePhase.ArrivedDelay: Console(316); break;
            case NpcTradeScenePhase.CenterConsole:
                Console(Math.Min(348,316+frame));break;
            case NpcTradeScenePhase.ReceiveFlash: Console(348); ScreenFlash(); break;
            case NpcTradeScenePhase.ZoomIn: Zoom(true); break;
            case NpcTradeScenePhase.FadeToPokemon:
                Zoom(true); DrawRect(new(0,0,240,160),new Color(0,0,0,frame/16f)); break;
            case NpcTradeScenePhase.PokemonFadeIn:
                Background("shadow"); DrawRect(new(0,0,240,160),new Color(0,0,0,1-frame/16f)); break;
            case NpcTradeScenePhase.ArriveBounce:
                Background("shadow");Ball(NpcTradeSceneTimeline.ArriveBallY(frame,_npcTradeSceneAssets!.Document.BallVelocity),(frame/3)%12); break;
            case NpcTradeScenePhase.Release:
                Background("shadow");
                if(frame<21) CommonBall(84,0);
                else
                {
                    int age=frame-21;float t=Math.Clamp(age/32f,0,1);
                    float arc=-_captureBallAssets.Sine[Math.Min(128,age*4)]/8;
                    Portrait(true,120+arc,84-24*t+arc,Math.Min(256,40+18*age)/256f,white:age<16);
                    if(age<10)CommonBall(84,age<5?1:2);Particles(age,84);
                }
                break;
            default: Background("shadow");Portrait(true,120,60);break;
        }
        int stage=NpcTradeSceneTimeline.TextStage(phase);
        if(stage>=0)
        {
            _battleSkin.DrawBattleMessage(this);
            var lines=NpcTradePresentationText(stage).Split('\n');
            for(int i=0;i<lines.Length;i++)SourceText(_sourceNormal,new(16,122+i*16),lines[i]);
        }
        if(_fade>0)DrawRect(new(0,0,240,160),new Color(0,0,0,(float)Math.Clamp(_fade/(16.0/60),0,1)));
        return true;
    }
}
