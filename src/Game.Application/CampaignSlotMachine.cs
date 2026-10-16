using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Slots;
using Game.Domain.World;

namespace Game.Application;

[Flags]
public enum CampaignSlotKeys { None=0, A=1, B=2, Select=4, Start=8, Right=16, Left=32, Up=64, Down=128, R=256, L=512 }
public enum CampaignSlotPhase { Selected, Initialize, Betting, NoCoins, Help, Quit, Lose, Payout, Exit }
public enum CampaignSlotSetupKind { Dma, ShowHelp, HideHelp, EndWinFlash, FadeOut }
public sealed record CampaignSlotInput(string CallbackId, long Frame, CampaignSlotKeys NewKeys=0, CampaignSlotKeys HeldKeys=0);
public sealed record CampaignSlotSetup(CampaignSlotSetupKind Kind, int Step=0, long ReadyFrame=0);
public sealed record CampaignSlotPalette(int Y, int Target, bool ObjectHalf=false, int Finish=-1, bool Active=true)
{
    public CampaignSlotPalette Advance()
    {
        if (!Active) return this;
        if (Finish >= 0) return Finish == 4 ? this with { Active=false } : this with { Finish=Finish+1 };
        if (!ObjectHalf) return this with { ObjectHalf=true };
        return Y == Target ? this with { ObjectHalf=false, Finish=0 } :
            this with { ObjectHalf=false, Y=Y<Target?Math.Min(Target,Y+3):Math.Max(Target,Y-3) };
    }
}

/// <summary>Transient callback owner. Coins exist only in CampaignState.Coins.</summary>
public sealed record CampaignSlotMachine(int SignId, int MachineIndex, string CallbackId, CampaignSlotPhase Phase,
    SlotBiasState Bias, SlotReels Reels)
{
    public long Frame { get; init; }
    public CampaignSlotKeys PreviousKeys { get; init; }
    public int MainStep { get; init; }
    public int Counter { get; init; }
    public int Bet { get; init; }
    public int PayoutRemaining { get; init; }
    public SlotPayout? Result { get; init; }
    public int CurrentReel { get; init; }
    public int QuitChoice { get; init; }
    public int HelpWidth { get; init; }
    public int FanfareCounter { get; init; } = -1;
    public string? Fanfare { get; init; }
    public int FlashStep { get; init; } = -1;
    public long FlashDmaReady { get; init; }
    public int PressedButtons { get; init; }
    public int PlayedSpins { get; init; }
    public CampaignSlotPalette? Palette { get; init; }
    public ImmutableArray<CampaignSlotSetup?> Setup { get; init; } = [null,null,null];
}

/// <summary>One deterministic original task frame. The scene never accepts a caller-selected result or RNG.</summary>
public static class CampaignSlotMachineTransactions
{
    internal const string JackpotStat = "GAME_STAT_SLOT_JACKPOTS";
    private const CampaignSlotKeys AllKeys=(CampaignSlotKeys)1023;
    private const CampaignSlotKeys Dpad=CampaignSlotKeys.Up|CampaignSlotKeys.Down|CampaignSlotKeys.Left|CampaignSlotKeys.Right;

    public static bool OwnsCallback(CampaignState state, ICampaignCatalog content)
    {
        if (state.SlotMachine is not { Phase: not CampaignSlotPhase.Selected } slot ||
            state.Script.Wait is not {Kind:StoryWaitKind.SlotMachine} wait || wait.CorrelationId!=slot.CallbackId ||
            !AtOriginalSign(state,content,slot.SignId) || state.Script.Stack.IsEmpty) return false;
        var i=GameCornerCoinSource.Instruction(state.Script,content);
        return i is {Op:StoryOp.Special,Opcode:"playslotmachine"} && i.Source.Path==GameCornerCoinSource.Path &&
            i.Source.Symbol==SlotMachineSource.Common && i.Args.SequenceEqual(new[]{"VAR_RESULT"}) &&
            state.Script.Variables.GetValueOrDefault("VAR_RESULT",-1)==slot.MachineIndex;
    }

    public static bool AtOriginalSign(CampaignState state, ICampaignCatalog content, int signId)
    {
        if (content.GameCornerSlots is null ||
            state.World.Player.MapId.Value!=GameCornerCoinSource.Map || !state.Script.Locked || state.Script.Completed ||
            !state.Script.Flags.GetValueOrDefault(GameCornerCoinSource.CaseFlag) ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],GameCornerCoinSource.CoinCase)!=1 ||
            state.Coins is <0 or >GameCornerCoinSource.Maximum ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8004",-1)!=signId) return false;
        var sign=SlotMachineSource.Signs.SingleOrDefault(x=>x.Id==signId); if(sign is null)return false;
        var player=state.World.Player; int dx=sign.Facing==Direction.East?1:-1;
        return player.Facing==sign.Facing && player.X+dx==sign.X && player.Y==sign.Y &&
            content.MapDefinition(player.MapId).BackgroundTriggers.Any(x=>x.X==sign.X&&x.Y==sign.Y&&
                x.Elevation==0&&x.PlayerFacing==sign.Facing&&x.ScriptId==sign.Script&&x.HiddenItem is null);
    }

    public static Decision<CampaignState> Tick(CampaignState state, CampaignCommand command, ICampaignCatalog content)
    {
        Decision<CampaignState> Reject(string e)=>new(CommandStatus.Rejected,state,e);
        if(state.SlotMachine is not {} s)return Reject("slot_not_pending");
        if(!OwnsCallback(state,content))return Reject("slot_callback_context");
        if(command.Kind!="slot_tick"||command.SlotInput is not {} input)return Reject("slot_event_active");
        if(input.CallbackId!=s.CallbackId||input.Frame!=checked(s.Frame+1))return Reject("slot_stale_frame_or_callback");
        if(((input.NewKeys|input.HeldKeys)&~AllKeys)!=0||input.NewKeys!=(input.HeldKeys&~s.PreviousKeys))
            return Reject("slot_input_edges");
        content.GameCornerSlots!.Validate();
        var initial=state;
        s=s with {Frame=input.Frame,PreviousKeys=input.HeldKeys};
        state=state with {PresentationEffects=[]};
        bool New(CampaignSlotKeys keys)=>(input.NewKeys&keys)!=0;
        bool Held(CampaignSlotKeys keys)=>(input.HeldKeys&keys)!=0;
        bool Idle(params int[] indices)=>indices.All(i=>s.Setup[i] is null);
        void Phase(CampaignSlotPhase phase)=>s=s with {Phase=phase,MainStep=0};
        void Step()=>s=s with {MainStep=s.MainStep+1};
        void Dma(int index)=>s=s with {Setup=s.Setup.SetItem(index,new(CampaignSlotSetupKind.Dma,ReadyFrame:s.Frame+1))};
        void Emit(string opcode, params string[] args)
        {
            long sequence=checked(state.Script.EffectSequence+1);
            state=state with {Script=state.Script with {EffectSequence=sequence},
                PresentationEffects=state.PresentationEffects.Add(new(sequence,opcode,state.World.Player.MapId,args.ToImmutableArray()))};
        }
        void Sound(string id)=>Emit("playse",id);
        void ResetLights(int line,int button)
        {
            Dma(line);Dma(button);s=s with {PressedButtons=0};
        }
        void Credit(bool all)
        {
            var award=SlotMachineCore.CollectPayout(state.Coins,s.PayoutRemaining,all);
            state=state with {Coins=award.Coins};s=s with {PayoutRemaining=award.PayoutRemaining};
        }
        try
        {
            switch(s.Phase)
            {
                case CampaignSlotPhase.Initialize:
                    // CB2_Init calls setup before its main switch. Asset decode/copy is admitted
                    // under a fixed next-frame DMA contract; no renderer acknowledgements decide input.
                    if(s.MainStep==0)Step();
                    else if(s.MainStep==1){Dma(0);Step();}
                    else if(s.MainStep==2&&(Idle(0)||s.Setup[0] is {Kind:CampaignSlotSetupKind.Dma} initCopy&&s.Frame>=initCopy.ReadyFrame))
                    {
                        // Init's RunTasks precedes the main callback, unlike the gameplay loop.
                        s=s with {Setup=s.Setup.SetItem(0,null),Palette=new CampaignSlotPalette(16,0).Advance()};Step();
                    }
                    else if(s.MainStep==3)
                    {
                        s=s with {Palette=s.Palette!.Advance()};
                        if(!s.Palette.Active)Phase(CampaignSlotPhase.Betting);
                    }
                    break;
                case CampaignSlotPhase.Betting:
                    switch(s.MainStep)
                    {
                        case 0:
                            if(state.Coins==0)Phase(CampaignSlotPhase.NoCoins);
                            else if(New(CampaignSlotKeys.Down)||New(CampaignSlotKeys.R))
                            {
                                var wager=SlotMachineCore.InsertCoins(state.Coins,s.Bet,!New(CampaignSlotKeys.Down));
                                state=state with {Coins=wager.Coins};s=s with {Bet=wager.Bet,MainStep=1};
                                Sound("SE_RS_SHOP");Dma(1);
                            }
                            else if(New(CampaignSlotKeys.A)&&s.Bet!=0)s=s with {MainStep=2};
                            else if(New(CampaignSlotKeys.B))Phase(CampaignSlotPhase.Quit);
                            else if(New(CampaignSlotKeys.Right))Phase(CampaignSlotPhase.Help);
                            break;
                        case 1:
                            if(Idle(0,1))s=s with {MainStep=s.Bet==3||state.Coins==0?2:0};
                            break;
                        case 2:
                            var bias=SlotMachineCore.ChooseBias(content.GameCornerSlots.Rules,s.MachineIndex,s.Bias,state.World.Rng);
                            state=state with {World=state.World with {Rng=bias.Rng}};
                            s=s with {Bias=bias.Bias,Reels=SlotMachineCore.StartReels(s.Reels),CurrentReel=0,MainStep=3,PlayedSpins=checked(s.PlayedSpins+1),Result=null};
                            Emit("slot_played");
                            break;
                        case 3:
                            if(Idle(0)&&New(CampaignSlotKeys.A))
                            {
                                var stop=SlotMachineCore.StopCurrent(content.GameCornerSlots.Rules,s.Reels,s.Bias,state.World.Rng);
                                state=state with {World=state.World with {Rng=stop.Rng}};
                                s=s with {Reels=stop.Reels,MainStep=4,PressedButtons=s.PressedButtons|(1<<s.CurrentReel)};
                                Sound("SE_CONTEST_PLACE");Dma(0);
                            }
                            break;
                        case 4:
                            if(!s.Reels.Reels[s.CurrentReel].Spinning&&Idle(0))
                            {
                                int reel=s.CurrentReel+1;
                                if(reel<3)s=s with {CurrentReel=reel,MainStep=3};
                                else
                                {
                                    var result=SlotMachineCore.Evaluate(content.GameCornerSlots.Rules,s.Bet,s.Reels.Reels.Select(x=>x.Position).ToImmutableArray());
                                    s=s with {Result=result,PayoutRemaining=result.Coins,Bet=0,CurrentReel=0,Bias=SlotMachineCore.AfterPayout(s.Bias,result)};
                                    if(result.BestReward==SlotReward.Seven)
                                    {
                                        var stats=state.GameStats??ImmutableSortedDictionary<string,int>.Empty;
                                        int count=stats.GetValueOrDefault(JackpotStat);
                                        state=state with {GameStats=stats.SetItem(JackpotStat,Math.Min(0xffffff,checked(count+1)))};
                                    }
                                    Phase(result.BestReward==SlotReward.None?CampaignSlotPhase.Lose:CampaignSlotPhase.Payout);
                                }
                            }
                            break;
                    }
                    break;
                case CampaignSlotPhase.NoCoins:
                    if(s.MainStep==0){Dma(0);Step();}
                    else if(s.MainStep==1&&Idle(0))Step();
                    else if(s.MainStep==2&&New(CampaignSlotKeys.A|CampaignSlotKeys.B|Dpad))Phase(CampaignSlotPhase.Exit);
                    break;
                case CampaignSlotPhase.Help:
                    if(s.MainStep==0)
                    {
                        s=s with {HelpWidth=0,Setup=s.Setup.SetItem(0,new(CampaignSlotSetupKind.ShowHelp))};Sound("SE_WIN_OPEN");Step();
                    }
                    else if(s.MainStep==1&&Idle(0))Step();
                    else if(s.MainStep==2&&New(CampaignSlotKeys.Left))
                    {
                        // HideHelp's state0 immediately falls through into its first -16 step.
                        s=s with {HelpWidth=240,Setup=s.Setup.SetItem(0,new(CampaignSlotSetupKind.HideHelp))};Sound("SE_WIN_OPEN");Step();
                    }
                    else if(s.MainStep==3&&Idle(0))Phase(CampaignSlotPhase.Betting);
                    break;
                case CampaignSlotPhase.Quit:
                    if(s.MainStep==0){s=s with {QuitChoice=0};Dma(0);Step();}
                    else if(s.MainStep==1&&Idle(0))Step();
                    else if(s.MainStep==2)
                    {
                        // Source Menu_ProcessInputNoWrap checks A/B before cursor movement.
                        if(New(CampaignSlotKeys.A|CampaignSlotKeys.B))
                        {
                            if(New(CampaignSlotKeys.A))Sound("SE_SELECT");
                            if(New(CampaignSlotKeys.A)&&s.QuitChoice==0)
                            {
                                state=state with {Coins=SlotMachineCore.RefundUnplayedBet(state.Coins,s.Bet)};
                                s=s with {Bet=0,MainStep=3};
                            }
                            else{s=s with {MainStep=4};Dma(0);}
                        }
                        else if(New(CampaignSlotKeys.Up)){if(s.QuitChoice!=0)Sound("SE_SELECT");s=s with {QuitChoice=0};}
                        else if(New(CampaignSlotKeys.Down)){if(s.QuitChoice!=1)Sound("SE_SELECT");s=s with {QuitChoice=1};}
                    }
                    else if(s.MainStep==3&&Idle(0))Phase(CampaignSlotPhase.Exit);
                    else if(s.MainStep==4&&Idle(0))Phase(CampaignSlotPhase.Betting);
                    break;
                case CampaignSlotPhase.Lose:
                    if(s.MainStep==0){s=s with {Counter=0};Step();}
                    else if(s.MainStep==1)
                    {
                        s=s with {Counter=s.Counter+1};
                        if(s.Counter>60){ResetLights(1,2);Step();}
                    }
                    else if(s.MainStep==2&&Idle(0,1,2))Phase(CampaignSlotPhase.Betting);
                    break;
                case CampaignSlotPhase.Payout:
                    if(s.MainStep==0)
                    {
                        bool jackpot=s.Result!.BestReward is SlotReward.Rocket or SlotReward.Seven;
                        s=s with {Counter=8,FanfareCounter=jackpot?250:150,Fanfare=jackpot?"MUS_SLOTS_JACKPOT":"MUS_SLOTS_WIN",FlashStep=0};
                        Emit("playfanfare",s.Fanfare);Step();
                    }
                    else if(s.MainStep==1)
                    {
                        s=s with {Counter=s.Counter+1};
                        if(s.Counter>120){s=s with {Counter=Held(CampaignSlotKeys.A)?2:8};Step();}
                    }
                    else if(s.MainStep==2&&Idle(0))
                    {
                        if(s.FanfareCounter<0&&New(CampaignSlotKeys.Start))Credit(true);
                        else
                        {
                            s=s with {Counter=s.Counter-1};
                            if(s.Counter==0)
                            {
                                if(s.FanfareCounter<0)Sound("SE_PIN");
                                if(s.PayoutRemaining!=0)Credit(false);
                                s=s with {Counter=Held(CampaignSlotKeys.A)?2:8};
                            }
                        }
                        if(s.PayoutRemaining==0)Step();
                    }
                    else if(s.MainStep==3&&s.FanfareCounter<0&&Idle(0))
                    {
                        s=s with {FlashStep=2,Setup=s.Setup.SetItem(0,new(CampaignSlotSetupKind.EndWinFlash))};Step();
                    }
                    else if(s.MainStep==4&&Idle(0)){ResetLights(0,1);Step();}
                    else if(s.MainStep==5&&Idle(0,1))Phase(CampaignSlotPhase.Betting);
                    break;
                case CampaignSlotPhase.Exit:
                    if(s.MainStep==0)
                    {
                        s=s with {Palette=new CampaignSlotPalette(0,16).Advance(),Setup=s.Setup.SetItem(0,new(CampaignSlotSetupKind.FadeOut))};Step();
                    }
                    else if(s.MainStep==1&&Idle(0))
                    {
                        if(s.Bet!=0||s.PayoutRemaining!=0||s.Reels.Reels.Any(x=>x.Spinning))return Reject("slot_unsettled_exit");
                        return new(CommandStatus.Accepted,state with {SlotMachine=null,PresentationEffects=[],PresentationTicks=0,ScreenIsBlack=false,Dialogue=null});
                    }
                    break;
                default:return Reject("slot_phase_unsupported");
            }
            // Exact priority order: main0, reels1, setup2, winning-line3, fanfare80, palette callback.
            if(s.Phase!=CampaignSlotPhase.Initialize)s=s with {Reels=SlotMachineCore.AdvanceReels(s.Reels)};
            for(int i=0;i<s.Setup.Length;i++)
            {
                if(s.Setup[i] is not {} setup)continue;
                switch(setup.Kind)
                {
                    case CampaignSlotSetupKind.Dma:if(s.Frame>=setup.ReadyFrame)setup=null;break;
                    case CampaignSlotSetupKind.ShowHelp:
                        if(setup.Step==1)setup=null;
                        else{s=s with {HelpWidth=Math.Min(256,s.HelpWidth+16)};if(s.HelpWidth==256)setup=setup with {Step=1};}
                        break;
                    case CampaignSlotSetupKind.HideHelp:
                        if(setup.Step==2)setup=null;
                        else if(setup.Step==1)setup=setup with {Step=2};
                        else{s=s with {HelpWidth=Math.Max(0,s.HelpWidth-16)};if(s.HelpWidth==0)setup=setup with {Step=1};}
                        break;
                    case CampaignSlotSetupKind.EndWinFlash:if(s.FlashStep<0)setup=null;break;
                    case CampaignSlotSetupKind.FadeOut:if(s.Palette?.Active==false)setup=null;break;
                }
                s=s with {Setup=s.Setup.SetItem(i,setup)};
            }
            if(s.FlashStep==0)s=s with {FlashStep=1};
            else if(s.FlashStep==2)s=s with {FlashStep=3,FlashDmaReady=s.Frame+1};
            else if(s.FlashStep==3&&s.Frame>=s.FlashDmaReady)s=s with {FlashStep=-1};
            if(s.FanfareCounter>=0)s=s with {FanfareCounter=s.FanfareCounter-1};
            if(s.Phase!=CampaignSlotPhase.Initialize&&s.Palette?.Active==true)s=s with {Palette=s.Palette.Advance()};
            return new(CommandStatus.Accepted,state with {SlotMachine=s});
        }
        catch(NotSupportedException ex){return new(CommandStatus.Rejected,initial,ex.Message);}
    }
}
