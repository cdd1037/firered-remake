using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.FirstBattle;
using Game.Domain.Items;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Godot;

public partial class CampaignView
{
    private sealed record BattleArt(Texture2D Texture,Vector2 Position);
    private readonly Dictionary<(int Species,string Side,bool Shiny),BattleArt> _battleArt=[],_battleDarkArt=[];
    private LabBattleUiSkin _battleSkin=null!,_battleDarkSkin=null!;
    private SourceGameFont _battleWhite=null!,_battleMenuFont=null!,_battleMoveFont=null!,_healthFont=null!,_healthDarkFont=null!,_healthMale=null!,_healthFemale=null!,_healthDarkMale=null!,_healthDarkFemale=null!;
    private readonly SourceGameFont[] _battlePpFont=new SourceGameFont[4],_battlePpSmallFont=new SourceGameFont[4];
    private readonly HashSet<int> _battleOakPages=[],_battleArrowPages=[];
    private int _lastMoveCursor;
    private bool _oakNotice,_battleStatsTotals;
    private ImmutableSortedDictionary<string,string> _battleText=null!;
    private string? _battlePresentationKey;
    private ImmutableArray<string> _battlePages=[];
    private int _battlePage,_battleCursor;
    private double _battleElapsed;
    private string _battleMenu="actions";
    private string? _battleNotice;
    private string? _battleNoticeReturn;
    private void LoadBattlePresentation()
    {
        _battleText=PretBattleText.Load(_root);
        var ui=System.IO.Path.Combine(_root,"content/generated/lab-battle-ui");_battleSkin=LabBattleUiSkin.Load(ui);_battleDarkSkin=LabBattleUiSkin.Load(ui,8);
        var fonts=System.IO.Path.Combine(_root,"content/generated/source-font");
        SourceGameFont Font(Color fore,Color shadow,bool small=false)=>SourceGameFont.Load(fonts,small?"small":"normal",fore,shadow);
        _battleWhite=Font(LabBattleUiSkin.BattleTextColor,LabBattleUiSkin.BattleTextShadow);
        _battleMenuFont=Font(LabBattleUiSkin.MenuTextColor,LabBattleUiSkin.MenuTextShadow);_battleMoveFont=Font(LabBattleUiSkin.MenuTextColor,LabBattleUiSkin.MenuTextShadow,true);
        for(int i=0;i<4;i++){var colors=_uiCorrections.PpColors(i);_battlePpFont[i]=Font(colors.Foreground,colors.Shadow);_battlePpSmallFont[i]=Font(colors.Foreground,colors.Shadow,true);}
        var hf=LabBattleUiSkin.HealthboxTextColor;var hs=LabBattleUiSkin.HealthboxTextShadow;var male=Color.Color8(66,206,255);var female=Color.Color8(255,156,148);
        _healthFont=Font(hf,hs,true);_healthDarkFont=Font(LabBattleUiSkin.DarkenColor(hf),LabBattleUiSkin.DarkenColor(hs),true);
        _healthMale=Font(male,hs,true);_healthFemale=Font(female,hs,true);_healthDarkMale=Font(LabBattleUiSkin.DarkenColor(male),LabBattleUiSkin.DarkenColor(hs),true);_healthDarkFemale=Font(LabBattleUiSkin.DarkenColor(female),LabBattleUiSkin.DarkenColor(hs),true);
        var folder=System.IO.Path.Combine(_root,"content/generated/lab-battle-art");var bytes=System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,"lab-battle-art.json"));
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!="d1516447a3e856a257c63e7293e8ebc8c7c555d65dbd49adca1eaa7dca7a547d")throw new InvalidDataException("Battle art catalog checksum mismatch.");
        using var json=JsonDocument.Parse(bytes);
        foreach(var sprite in json.RootElement.GetProperty("sprites").EnumerateArray())
        {
            var file=sprite.GetProperty("texturePath").GetString()!;if(System.IO.Path.GetFileName(file)!=file)throw new InvalidDataException("Battle art path invalid.");
            var path=System.IO.Path.Combine(folder,file);
            if(Convert.ToHexStringLower(SHA256.HashData(System.IO.File.ReadAllBytes(path)))!=sprite.GetProperty("sha256").GetString())throw new InvalidDataException("Battle art bytes differ.");
            var position=sprite.GetProperty("singleBattleTopLeft");var texture=Texture(path);
            if(texture.GetWidth()!=64||texture.GetHeight()!=64)throw new InvalidDataException("Battle sprite dimensions invalid.");
            _battleArt.Add((sprite.GetProperty("sourceSpeciesId").GetInt32(),sprite.GetProperty("side").GetString()!,sprite.GetProperty("paletteVariant").GetString()=="shiny"),new(texture,new(position.GetProperty("x").GetInt32(),position.GetProperty("y").GetInt32())));
        }
        foreach(var pair in _battleArt)
        {
            using var image=pair.Value.Texture.GetImage();var darkPixels=image.GetData();
            for(int i=0;i<darkPixels.Length;i+=4)if(darkPixels[i+3]!=0)for(int c=0;c<3;c++){int v=darkPixels[i+c]>>3;v+=(-v*8)>>4;darkPixels[i+c]=(byte)((v<<3)|(v>>2));}
            using var display=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,darkPixels);
            _battleDarkArt.Add(pair.Key,new(ImageTexture.CreateFromImage(display),pair.Value.Position));
        }
        if(_battleArt.Count!=12)throw new InvalidDataException("Battle sprite set incomplete.");
    }
    private string BattleName(FirstBattleSide side)=>side==FirstBattleSide.Player?_session.State.Battle!.OwnedBefore.Nickname:_catalog.PokemonContent!.SpeciesNames[_session.State.Battle!.State.Opponent.Pokemon.SourceSpeciesId];
    private ImmutableArray<string> BattleSourcePages(string symbol,params (string Key,string Value)[] extra)
    {
        var b=_session.State.Battle!;
        var values=new Dictionary<string,string>(StringComparer.Ordinal){["B_PLAYER_NAME"]=_session.State.PlayerName,["PLAYER"]=_session.State.PlayerName,["B_TRAINER1_CLASS"]="RIVAL",["B_TRAINER1_NAME"]=_session.State.RivalName,["B_PLAYER_MON1_NAME"]=b.OwnedBefore.Nickname,["B_OPPONENT_MON1_NAME"]=BattleName(FirstBattleSide.Opponent)};
        foreach(var (key,value) in extra)values[key]=value;
        return PretBattleText.Pages(_battleText,symbol,values);
    }
    private string PrefixedName(FirstBattleSide side)=>(side==FirstBattleSide.Opponent?"Foe ":"")+BattleName(side);
    private string MoveName(Game.Domain.Ids.MoveId move)=>_catalog.BattleContent!.MoveNames[move.Value];
    private void SynchronizeBattle()
    {
        if(_session.State.Battle is not {} b){_battlePresentationKey=null;_battleNotice=null;_battleMenu="actions";return;}
        var key=$"{b.Id}:{b.Phase}:{b.State.EventSequence}:{b.EventIndex}:{b.PageIndex}";
        if(_battlePresentationKey==key)return;
        _battlePresentationKey=key;_battleElapsed=0;_battlePage=0;_battlePages=[];_battleOakPages.Clear();_battleArrowPages.Clear();_battleNotice=null;_oakNotice=false;_battleStatsTotals=false;
        if(b.Phase==CampaignBattlePhase.ChooseMove)return;
        var pages=ImmutableArray.CreateBuilder<string>();
        void Source(string symbol,params (string Key,string Value)[] values)
        {
            var added=BattleSourcePages(symbol,values);
            bool oak=symbol is "gText_ForPetesSake" or "gText_TheTrainerThat" or "gText_TryBattling" or "gText_InflictingDamageIsKey" or "gText_LoweringStats" or "gText_KeepAnEyeOnHP" or "gText_WinEarnsPrizeMoney" or "gText_HowDissapointing";
            if(oak)for(int i=0;i<added.Length;i++)_battleOakPages.Add(pages.Count+i);
            bool finalWait=_battleText[symbol].EndsWith("\\p",StringComparison.Ordinal)||_battleText[symbol].EndsWith("\\l",StringComparison.Ordinal)||_battleText[symbol].Contains("{PAUSE_UNTIL_PRESS}",StringComparison.Ordinal);
            for(int i=0;i<added.Length;i++)if(i+1<added.Length||finalWait)_battleArrowPages.Add(pages.Count+i);
            pages.AddRange(added);
        }
        if(b.Phase==CampaignBattlePhase.Events)
        {
            var e=b.Events[b.EventIndex];
            switch(e)
            {
                case FirstBattleStartedEvent:
                    Source("sText_Trainer1WantsToBattle");Source("sText_Trainer1SentOutPkmn");Source("sText_GoPkmn");Source("gText_ForPetesSake");Source("gText_TheTrainerThat");Source("gText_TryBattling");break;
                case FirstBattleMoveUsedEvent used:Source("sText_AttackerUsedX",("B_ATK_NAME_WITH_PREFIX",PrefixedName(used.Actor)),("B_BUFF2",MoveName(used.Move)+"!"));break;
                case FirstBattleMoveMissedEvent missed:Source("sText_AttackMissed",("B_ATK_NAME_WITH_PREFIX",PrefixedName(missed.Actor)));break;
                case FirstBattlePotionRecoveredEvent potion:Source("gText_PkmnHPRestoredByVar2",("STR_VAR_1",BattleName(potion.Side)),("STR_VAR_2",potion.HpRestored.ToString()));break;
                case FirstBattleHpChangedEvent:break;
                case FirstBattleRecoilEvent recoil:Source("sText_PkmnHitWithRecoil",("B_ATK_NAME_WITH_PREFIX",PrefixedName(recoil.Actor)));break;
                case FirstBattleCriticalHitEvent:Source("sText_CriticalHit");break;
                case FirstBattleStatChangedEvent stat:Source("sText_DefendersStatFell",("B_DEF_NAME_WITH_PREFIX",PrefixedName(stat.Target)),("B_BUFF1",stat.Stat==FirstBattleStat.Attack?"ATTACK":"DEFENSE"),("B_BUFF2","fell!"));break;
                case FirstBattleStatAtMinimumEvent stat:Source("sText_StatsWontDecrease",("B_DEF_NAME_WITH_PREFIX",PrefixedName(stat.Target)),("B_BUFF1",stat.Stat==FirstBattleStat.Attack?"ATTACK":"DEFENSE"));break;
                case FirstBattleTutorialEvent tutorial:
                    if(tutorial.Flag==FirstBattleTutorialFlags.Damage)Source("gText_InflictingDamageIsKey");
                    else if(tutorial.Flag==FirstBattleTutorialFlags.StatChange)Source("gText_LoweringStats");
                    else if(tutorial.Flag==FirstBattleTutorialFlags.HpRestore)Source("gText_KeepAnEyeOnHP");
                    else if(tutorial.Flag==FirstBattleTutorialFlags.PartyMenu){Source("gText_OakImportantToGetToKnowPokemonThroughly");Source("gText_OakThisIsListOfPokemon");}
                    else throw new InvalidDataException("Unsupported battle tutorial presentation.");break;
                case FirstBattleFaintedEvent fainted:Source("sText_TargetFainted",("B_DEF_NAME_WITH_PREFIX",PrefixedName(fainted.Side)));break;
                default:throw new InvalidDataException("Unsupported battle presentation event: "+e.GetType().Name);
            }
        }
        else
        {
            var page=b.Pages[b.PageIndex];
            switch(page.Reward)
            {
                case LabBattleExperienceEvent xp:Source("sText_PkmnGainedEXP",("B_BUFF1",b.OwnedBefore.Nickname),("B_BUFF2",""),("B_BUFF3",xp.Gained.ToString()));break;
                case LabBattleLevelEvent level when page.Kind=="level":Source("sText_PkmnGrewToLv",("B_BUFF1",b.OwnedBefore.Nickname),("B_BUFF2",level.After.ToString()));break;
                case LabBattleLevelEvent:break;
                case LabBattleMoneyEvent money:Source("sText_PlayerGotMoney",("B_BUFF1",money.Awarded.ToString()));break;
                default:pages.Add(page.Text);break;
            }
            if(b.PageIndex==b.Pages.Length-1)Source(b.State.Outcome==FirstBattleOutcome.Won?"gText_WinEarnsPrizeMoney":"gText_HowDissapointing");
        }
        _battlePages=pages.ToImmutable();
    }
    private void ProcessBattle(double delta)
    {
        _battleElapsed+=delta;
        if(_session.State.Battle is {Phase:CampaignBattlePhase.Events} b&&_battlePages.IsEmpty&&b.Events[b.EventIndex] is FirstBattleHpChangedEvent&&_battleElapsed>=.45)Apply(new("ack"));
    }
    private bool BattleInput(InputEventKey key)
    {
        if(key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape)return false;
        if(!key.Pressed)return true;_held=null;
        var b=_session.State.Battle!;
        if(_battleNotice is not null){if(key.Keycode is Key.Enter or Key.Backspace){_battleNotice=null;if(_battleNoticeReturn is {} target){_battleMenu=target;_battleCursor=0;_battleNoticeReturn=null;}}return true;}
        if(b.Phase!=CampaignBattlePhase.ChooseMove)
        {
            if(key.Keycode==Key.Enter&&_battleElapsed>.10)
            {
                if(b.Phase==CampaignBattlePhase.Results&&b.Pages[b.PageIndex].Kind=="stats"&&!_battleStatsTotals){_battleStatsTotals=true;_battleElapsed=0;Write("battleStatsPage",new{page=2});return true;}
                if(_battlePage+1<_battlePages.Length){_battlePage++;_battleElapsed=0;Write("battlePage",new{key=_battlePresentationKey,page=_battlePage});}else if(!_battlePages.IsEmpty||b.Phase==CampaignBattlePhase.Results)Apply(new("ack"));}
            return true;
        }
        if(_battleMenu=="bag")
        {
            int entries=(_session.State.Inventory?.BagItems.Length??0)+1;
            if(key.Keycode==Key.Up)_battleCursor=Math.Max(0,_battleCursor-1);
            if(key.Keycode==Key.Down)_battleCursor=Math.Min(entries-1,_battleCursor+1);
            if(key.Keycode==Key.Backspace||key.Keycode==Key.Enter&&_battleCursor==entries-1){_battleMenu="actions";_battleCursor=0;}
            else if(key.Keycode==Key.Enter){if(_session.State.Inventory!.BagItems[_battleCursor].Item!=Game.Domain.Items.OpeningInventoryRules.PotionId){_battleNotice="This item's battle use is\nnot implemented yet.";return true;}_battleMenu="bag-actions";_battleCursor=0;}
            return true;
        }
        if(_battleMenu=="bag-actions")
        {
            if(key.Keycode is Key.Up or Key.Down)_battleCursor^=1;
            if(key.Keycode==Key.Backspace||key.Keycode==Key.Enter&&_battleCursor==1){_battleMenu="bag";_battleCursor=0;}
            else if(key.Keycode==Key.Enter){_battleMenu="medicine-target";_battleCursor=0;Apply(new("battle_menu",Text:"party"));}
            return true;
        }
        if(_battleMenu=="medicine-target")
        {
            if(key.Keycode==Key.Backspace){_battleMenu="bag";_battleCursor=0;}
            else if(key.Keycode==Key.Enter)
            {
               bool accepted = Apply(new("battle_potion",PartyIndex:0));
                if(accepted){_battleMenu="actions";_battleCursor=0;}
                else{_battleNotice=_status=="potion_no_effect"?ItemText("gText_WontHaveEffect"):_status;_battleNoticeReturn="bag";}
            }
            return true;
        }
        if(_battleMenu=="party")
        {if(key.Keycode is Key.Backspace or Key.Enter){_battleMenu="actions";_battleCursor=0;}return true;}
        if(key.Keycode==Key.Backspace){_battleMenu="actions";_battleCursor=0;return true;}
        int count=_battleMenu=="actions"?4:FirstBattleRules.RequiresStruggle(b.State.Player)?1:b.State.Player.Pokemon.Moves.Length;
        if(_battleMenu=="actions"&&key.Keycode is Key.Left or Key.Right)_battleCursor^=1;
        else if(_battleMenu=="actions"&&key.Keycode is Key.Up or Key.Down)_battleCursor^=2;
        else if(_battleMenu=="moves"&&key.Keycode is Key.Left or Key.Right){int candidate=_battleCursor^1;if(candidate<count)_battleCursor=candidate;}
        else if(_battleMenu=="moves"&&key.Keycode is Key.Up or Key.Down){int candidate=_battleCursor^2;if(candidate<count)_battleCursor=candidate;}
        else if(key.Keycode==Key.Enter)
        {
            if(_battleMenu=="moves")
            {_lastMoveCursor=_battleCursor;bool accepted = Apply(new("battle_move",MoveSlot:FirstBattleRules.RequiresStruggle(b.State.Player)?-1:_battleCursor));if(accepted){_battleMenu="actions";_battleCursor=0;}else _battleNotice=_lastCommandFault?"The battle step was not committed.":"This move has no PP left.";}
            else switch(_battleCursor)
            {
                case 0:_battleMenu="moves";_battleCursor=Math.Min(_lastMoveCursor,Math.Max(0,b.State.Player.Pokemon.Moves.Length-1));break;
                case 1:_battleMenu="bag";_battleCursor=0;Apply(new("battle_menu",Text:"bag"));break;
                case 2:_battleMenu="party";Apply(new("battle_menu",Text:"party"));break;
                case 3:Apply(new("battle_menu",Text:"run"));_oakNotice=true;_battleNotice=string.Join("\n",BattleSourcePages("gText_OakNoRunningFromATrainer"));break;
            }
        }
        return true;
    }
    private void BattleBox(Rect2 rectangle)
    {DrawRect(rectangle,new Color("324451"));DrawRect(rectangle.Grow(-2),new Color("fffbe7"));}
    private void DrawBattle()
    {
        var b=_session.State.Battle!;
        if(b.Phase==CampaignBattlePhase.ChooseMove&&_battleMenu is "bag" or "bag-actions"&&_session.State.Inventory is {} inventory)
        {DrawItemList(inventory,"bag",_battleMenu=="bag-actions"?0:_battleCursor,_battleNotice);if(_battleMenu=="bag-actions")DrawItemActionPopup(false,[_itemSkin.Text("gOtherText_Use"),_itemSkin.Text("gFameCheckerText_Cancel")],_battleCursor);return;}
        bool oak=_battleNotice is not null?_oakNotice:_battleOakPages.Contains(_battlePage);
        DrawRect(new(0,0,240,160),Colors.Black);var skin=oak?_battleDarkSkin:_battleSkin;skin.DrawBackground(this);
        foreach(var (side,mon) in new[]{(FirstBattleSide.Opponent,b.DisplayOpponent),(FirstBattleSide.Player,b.DisplayPlayer)})
        {
            bool faintShown=mon.CurrentHp==0&&(b.Phase==CampaignBattlePhase.Results||b.Phase==CampaignBattlePhase.Events&&b.Events.Take(b.EventIndex+1).OfType<FirstBattleFaintedEvent>().Any(f=>f.Side==side));
            if(faintShown)continue;
            var asset=(oak?_battleDarkArt:_battleArt)[(mon.SourceSpeciesId,side==FirstBattleSide.Player?"back":"front",mon.IsShiny)];
            if(mon.CurrentHp>0||(b.Phase==CampaignBattlePhase.Events&&!b.Events.Take(b.EventIndex).OfType<FirstBattleFaintedEvent>().Any(f=>f.Side==side)))DrawTexture(asset.Texture,asset.Position);
            int hp=mon.CurrentHp;
            if(b.Phase==CampaignBattlePhase.Events&&b.Events[b.EventIndex] is FirstBattleHpChangedEvent change&&change.Target==side)hp=(int)Math.Round(Mathf.Lerp(change.HpBefore,change.HpAfter,(float)Math.Clamp(_battleElapsed/.4,0,1)));
            bool highlight=oak&&side==FirstBattleSide.Opponent&&b.Phase==CampaignBattlePhase.Events&&b.Events[b.EventIndex] is FirstBattleTutorialEvent{Flag:FirstBattleTutorialFlags.Damage};
            DrawBattleHealth(mon,side,hp,oak&&!highlight);
        }
        bool selecting=b.Phase==CampaignBattlePhase.ChooseMove&&_battleNotice is null&&_battleMenu is "actions" or "moves";
        if(oak)_battleSkin.DrawDialogue(this);else if(selecting)_uiCorrections.ClearBattleMenu(this);else _battleSkin.DrawBattleMessage(this);
        void Lines(string text)
        {
            var lines=text.Split('\n');var origin=oak?LabBattleUiSkin.DialogueText:LabBattleUiSkin.BattleMessageText;
            for(int i=0;i<lines.Length&&i<2;i++)SourceText(oak?_sourceNormal:_battleWhite,origin+new Vector2(0,i*(oak?15:16)),lines[i]);
        }
        bool partyPresentation=_battleMenu=="medicine-target"||b.Phase==CampaignBattlePhase.Events&&b.Events[b.EventIndex] is FirstBattlePotionRecoveredEvent or FirstBattleTutorialEvent{Flag:FirstBattleTutorialFlags.PartyMenu};
        if(partyPresentation||_battleMenu=="party")
        {
            // Temporary party adapter; its original full-screen art is a separate presentation gate.
            _battleSkin.DrawStandardFrame(this,new(0,0,240,112));var p=b.DisplayPlayer;
            SourceText(_sourceNormal,new(12,8),$"{b.OwnedBefore.Nickname} {p.CurrentHp}/{p.Stats.MaxHp} HP");
            for(int i=0;i<p.Moves.Length;i++)SourceText(_sourceNormal,new(12,29+i*16),$"{MoveName(p.Moves[i].Move)} {p.Moves[i].CurrentPp}/{p.Moves[i].MaximumPp}");
        }
        if(_battleNotice is not null)Lines(_battleNotice);
        else if(b.Phase==CampaignBattlePhase.Results&&b.Pages[b.PageIndex] is {Kind:"stats",Reward:LabBattleLevelEvent level})
        {
            Lines(string.Join('\n',BattleSourcePages("sText_PkmnGrewToLv",("B_BUFF1",b.OwnedBefore.Nickname),("B_BUFF2",level.After.ToString()))));
            _battleSkin.DrawStandardFrame(this,new(144,56,96,104));
            var stats=new[]{("gText_LevelUp_MaxHP",level.StatsBefore.MaxHp,level.StatsAfter.MaxHp),("gText_LevelUp_Attack",level.StatsBefore.Attack,level.StatsAfter.Attack),("gText_LevelUp_Defense",level.StatsBefore.Defense,level.StatsAfter.Defense),("gText_LevelUp_SpAtk",level.StatsBefore.SpecialAttack,level.StatsAfter.SpecialAttack),("gText_LevelUp_SpDef",level.StatsBefore.SpecialDefense,level.StatsAfter.SpecialDefense),("gText_LevelUp_Speed",level.StatsBefore.Speed,level.StatsAfter.Speed)};
            for(int i=0;i<stats.Length;i++)
            {
                var (symbol,before,after)=stats[i];DrawStatLabel(new(152,64+i*15),_battleText[symbol]);
                if(_battleStatsTotals)SourceText(_battleMenuFont,new(208+6*(4-after.ToString().Length),64+i*15),after.ToString());
                else{int diff=after-before;DrawStatLabel(new(208,64+i*15),_battleText[diff>=0?"gText_LevelUp_Plus":"gText_LevelUp_Minus"]);SourceText(_battleMenuFont,new(208+(Math.Abs(diff)<10?12:6),64+i*15)," "+Math.Abs(diff));}
            }
        }
        else if(!_battlePages.IsEmpty)
        {
            Lines(_battlePages[_battlePage]);
            if(_battleArrowPages.Contains(_battlePage))DrawTextCursor(oak?_sourceNormal:_battleWhite,oak?LabBattleUiSkin.DialogueText:LabBattleUiSkin.BattleMessageText,_battlePages[_battlePage].Split('\n'),oak?15:16,_battleElapsed,!oak);
        }
        else if(b.Phase==CampaignBattlePhase.ChooseMove)
        {
            if(_battleMenu=="medicine-target")Lines("Use on which POKéMON?");
            else if(_battleMenu=="party")Lines("Choose a POKéMON.");
            else if(_battleMenu=="actions")
            {
                _battleSkin.DrawActionMenu(this);Lines("What will\n"+b.OwnedBefore.Nickname+" do?");
                var labels=new[]{"FIGHT","BAG","POKéMON","RUN"};for(int i=0;i<4;i++)SourceText(_battleMenuFont,LabBattleUiSkin.ActionText(i),labels[i]);
                _uiCorrections.DrawSelectionCursor(this,false,_battleCursor);
            }
            else if(FirstBattleRules.RequiresStruggle(b.State.Player)){_battleSkin.DrawMoveMenu(this);SourceText(_battleMoveFont,LabBattleUiSkin.MoveText(0),"STRUGGLE");_uiCorrections.DrawSelectionCursor(this,true,0);}
            else
            {
                _battleSkin.DrawMoveMenu(this);var moves=b.State.Player.Pokemon.Moves;
                for(int i=0;i<4;i++)SourceText(_battleMoveFont,LabBattleUiSkin.MoveText(i),i<moves.Length?MoveName(moves[i].Move):"-");
                _uiCorrections.DrawSelectionCursor(this,true,_battleCursor);
                var move=moves[_battleCursor];int ppState=SourceUiCorrections.PpPaletteState(move.CurrentPp,move.MaximumPp);SourceText(_battlePpSmallFont[ppState],new(168,122),"PP");SourceText(_battlePpFont[ppState],new(202,122),$"{move.CurrentPp,2}/{move.MaximumPp,2}");
                SourceText(_battleMoveFont,new(168,138),"TYPE/");
                // MoveSelectionDisplayMoveType switches to FONT_NORMAL after the small TYPE/ prefix.
                float typePrefixWidth=_battleMoveFont.Font.GetStringSize(_battleMoveFont.PrepareText("TYPE/"),fontSize:_battleMoveFont.Size).X;
                SourceText(_battleMenuFont,new(168+typePrefixWidth,138),_catalog.PokemonContent!.Catalog.Moves[move.Move].Type.Replace("TYPE_","",StringComparison.Ordinal));
            }
        }
    }
    private void DrawStatLabel(Vector2 origin,string source)
    {
        var font=_battleMenuFont;
        foreach(var segment in System.Text.RegularExpressions.Regex.Split(source,@"(\{[^}]*\})"))
        {
            if(segment=="{FONT_SMALL}"){font=_battleMoveFont;continue;}
            if(segment=="{FONT_NORMAL}"){font=_battleMenuFont;continue;}
            var text=segment=="{PLUS}"?"+":segment;
            if(text.Contains('{')||text.Contains('}'))throw new InvalidDataException("Unsupported level-stat text control.");
            SourceText(font,origin,text);origin.X+=font.Font.GetStringSize(font.PrepareText(text),fontSize:font.Size).X;
        }
    }
    private void DrawBattleHealth(PokemonInstance mon,FirstBattleSide side,int hp,bool dark)
    {
        bool player=side==FirstBattleSide.Player;var skin=dark?_battleDarkSkin:_battleSkin;var font=dark?_healthDarkFont:_healthFont;
        var growth=_catalog.PokemonContent!.Catalog.Species[mon.Species].GrowthRate;
        uint min=GrowthRules.ExperienceAtLevel(growth,mon.Level),max=GrowthRules.ExperienceAtLevel(growth,Math.Min(100,mon.Level+1));
        int exp=max==min?0:(int)Math.Clamp((long)(mon.Experience-min)*64/(max-min),0,64);
        skin.DrawHealthbox(this,player,hp,mon.Stats.MaxHp,exp);
        var name=player?LabBattleUiSkin.PlayerName:LabBattleUiSkin.OpponentName;
        SourceText(font,name,BattleName(side));
        if(mon.Gender!=PokemonGender.Genderless)
        {
            var genderFont=mon.Gender==PokemonGender.Female?(dark?_healthDarkFemale:_healthFemale):(dark?_healthDarkMale:_healthMale);
            float width=font.Font.GetStringSize(font.PrepareText(BattleName(side)),fontSize:font.Size).X;
            SourceText(genderFont,name+new Vector2(width,0),mon.Gender==PokemonGender.Female?"♀":"♂");
        }
        var levelOrigin=(player?LabBattleUiSkin.PlayerLevelWindow:LabBattleUiSkin.OpponentLevelWindow)+new Vector2(5*(3-mon.Level.ToString().Length),0);
        SourceText(font,levelOrigin,font.LevelPrefix+mon.Level);
        if(player)
        {
            SourceText(font,LabBattleUiSkin.PlayerCurrentHpText,hp.ToString().PadLeft(3)+"/");
            SourceText(font,LabBattleUiSkin.PlayerMaximumHpText,mon.Stats.MaxHp.ToString().PadLeft(3));
        }
    }
}
