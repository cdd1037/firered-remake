using System.Collections.Immutable;
using Game.Application;
using Game.Content;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Ordinary source trainer presentation. Commands remain the sole authority for
/// combat, experience, replacement, inventory and RNG; sprites are static source frames.</summary>
public partial class CampaignView
{
    private CampaignWildBattle? PresentedWildBattle => _session.State.ForestTrainerBattle?.Presentation ?? _session.State.WildBattle;
    private TrainerDefinition VisibleOrdinaryTrainer()
    {
        var id=_session.State.ForestTrainerBattle!.Trainer;
        return _catalog.PokemonTowerTrainers?.Trainers.GetValueOrDefault(id)??_catalog.RocketHideoutTrainers?.Trainers.GetValueOrDefault(id)??_catalog.CeladonGymTrainers?.Trainers.GetValueOrDefault(id)??_catalog.CeladonApproachTrainers?.Trainers.GetValueOrDefault(id)??_catalog.RockTunnelTrainers?.Trainers.GetValueOrDefault(id)??_catalog.Route11Trainers?.Trainers.GetValueOrDefault(id)??_catalog.VermilionGymTrainers?.Trainers.GetValueOrDefault(id)??_catalog.SsAnneTrainers?.Trainers.GetValueOrDefault(id)??_catalog.VermilionTrainers?.Trainers.GetValueOrDefault(id)??_catalog.BillTrainers?.Trainers.GetValueOrDefault(id)??_catalog.CeruleanTrainers?.Trainers.GetValueOrDefault(id)??_catalog.MtMoonTrainers?.Trainers.GetValueOrDefault(id)??_catalog.Route3Trainers?.Trainers.GetValueOrDefault(id)??_catalog.Route22Trainers?.Trainers.GetValueOrDefault(id)??_catalog.PewterTrainers?.Trainers.GetValueOrDefault(id)??_catalog.ForestTrainers!.Trainers[id];
    }
    private bool ForestTrainerCombatVisible => _session.State.ForestTrainerBattle is { Stage: not CampaignForestTrainerStage.Intro };
    private readonly Dictionary<string, ImageTexture> _forestTrainerPortraits = new(StringComparer.Ordinal);
    private WildBattleEvent? WildCurrentEvent(CampaignWildBattle battle) =>
        _session.State.ForestTrainerBattle?.CurrentTrainerEvent is not null || battle.EventIndex >= battle.Events.Length
            ? null : battle.Events[battle.EventIndex];
    private void LoadForestTrainerPresentation()
    {
        if (!_catalog.ForestTrainersEnabled) return;
        foreach (var asset in PretForestTrainerArt.Load(_root).Concat(_catalog.PewterEnabled?PretPewterTrainerArt.Load(_root):[]).Concat(_catalog.Route22Enabled?PretRoute22TrainerArt.Load(_root):[]).Concat(_catalog.Route3Enabled?PretRoute3TrainerArt.Load(_root):[]))
        {
            using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, asset.Rgba);
            _forestTrainerPortraits.Add(asset.Id, ImageTexture.CreateFromImage(image));
        }
    }
    private void AddForestTrainerTextValues(Dictionary<string, string> values)
    {
        if (_session.State.ForestTrainerBattle is not { } trainer) return;
        values["B_TRAINER1_CLASS"] = VisibleOrdinaryTrainer().TrainerClass switch{"TRAINER_CLASS_CHANNELER"=>"CHANNELER","TRAINER_CLASS_BOSS"=>"BOSS","TRAINER_CLASS_BEAUTY"=>"BEAUTY","TRAINER_CLASS_COOLTRAINER"=>"COOLTRAINER","TRAINER_CLASS_BIKER"=>"BIKER","TRAINER_CLASS_POKEMANIAC"=>"POKéMANIAC","TRAINER_CLASS_GAMER"=>"GAMER","TRAINER_CLASS_ENGINEER"=>"ENGINEER","TRAINER_CLASS_SAILOR"=>"SAILOR","TRAINER_CLASS_FISHERMAN"=>"FISHERMAN","TRAINER_CLASS_GENTLEMAN"=>"GENTLEMAN","TRAINER_CLASS_RIVAL_LATE"=>"RIVAL","TRAINER_CLASS_PICNICKER"=>"PICNICKER","TRAINER_CLASS_SWIMMER_M"=>"SWIMMER","TRAINER_CLASS_RIVAL_EARLY"=>"RIVAL","TRAINER_CLASS_LEADER"=>"LEADER","TRAINER_CLASS_CAMPER"=>"CAMPER","TRAINER_CLASS_YOUNGSTER"=>"YOUNGSTER","TRAINER_CLASS_LASS"=>"LASS","TRAINER_CLASS_SUPER_NERD"=>"SUPER NERD","TRAINER_CLASS_HIKER"=>"HIKER","TRAINER_CLASS_TEAM_ROCKET"=>"TEAM ROCKET",_=>"BUG CATCHER"};
        values["B_TRAINER1_NAME"] = VisibleOrdinaryTrainer().TrainerClass is "TRAINER_CLASS_RIVAL_EARLY" or "TRAINER_CLASS_RIVAL_LATE"?_session.State.RivalName:VisibleOrdinaryTrainer().SourceTrainerName;
    }
    private bool TryForestTrainerIntroInput(InputEventKey key)
    {
        if (_session.State.ForestTrainerBattle?.Stage != CampaignForestTrainerStage.Intro ||
            key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape) return false;
        _held = null;
        if (_wildNotice is not null)
        {
            if (key.Pressed && key.Keycode is Key.Enter or Key.Backspace) _wildNotice = null;
            return true;
        }
        if (key.Pressed && !key.Echo && key.Keycode == Key.Enter &&
            _waitElapsed >= _waitDuration && _tracks.All(t => t.Time >= t.Duration)) Apply(new("ack"));
        return true;
    }
    private bool ForestTrainerEventPages(Action<string, (string Key, string Value)[]> source)
    {
        if (_session.State.ForestTrainerBattle?.CurrentTrainerEvent is not { } item) return false;
        if(item is SsAnneTrainerPlayerForcedOutEvent forcedPlayer)
        {
            source("sText_PkmnWasDraggedOut",[("B_DEF_NAME_WITH_PREFIX",_session.State.Party[forcedPlayer.PartyIndex].Nickname)]);return true;
        }
        if(item is CeruleanTrainerItemUsedEvent medicine)
        {
            string medicineName=medicine.Item switch
            {"ITEM_SUPER_POTION"=>"SUPER POTION","ITEM_HYPER_POTION" when _catalog.CeladonGymEnabled=>"HYPER POTION","ITEM_FULL_HEAL"=>"FULL HEAL",_=>throw new InvalidDataException("Unsupported visible trainer medicine: "+medicine.Item)};
            source("sText_Trainer1UsedItem",[("B_LAST_ITEM",medicineName)]);return true;
        }
        if (item is not ForestTrainerOpponentReplacedEvent replacement)
            throw new InvalidDataException("Unsupported visible trainer event: " + item.GetType().Name);
        var name=_catalog.PokemonContent!.SpeciesNames[replacement.Replacement.Pokemon.SourceSpeciesId];
        bool forced=_session.State.ForestTrainerBattle.Presentation.Events.OfType<WildBattleForcedOutEvent>()
            .Any(e=>e.TrainerBattle&&e.Target==WildBattleSide.Opponent&&e.Sequence<replacement.Sequence);
        if(forced)source("sText_PkmnWasDraggedOut",[("B_DEF_NAME_WITH_PREFIX","Foe "+name)]);
        else
        {
            if(_catalog.CeruleanEnabled&&replacement.Previous.Pokemon.CurrentHp>0)
                source("sText_Trainer1WithdrewPkmn",[("B_BUFF1",_catalog.PokemonContent!.SpeciesNames[replacement.Previous.Pokemon.SourceSpeciesId])]);
            source("sText_Trainer1SentOutPkmn2", [("B_BUFF1",name)]);
        }
        return true;
    }
    private bool ForestTrainerResultPage(CampaignWildBattle battle, Action<string, (string Key, string Value)[]> source)
    {
        if (_session.State.ForestTrainerBattle is not { } trainer) return false;
        if (trainer.Stage == CampaignForestTrainerStage.Finished && trainer.Combat.Battle.Outcome == WildBattleOutcome.Won)
        {
            if (battle.PageIndex == 0) { source("sText_PlayerDefeatedLinkTrainerTrainer1", []); return true; }
            if (battle.PageIndex == battle.Pages.Length - 1 - VermilionTrainerPayDayPageCount)
            {
                var definition = VisibleOrdinaryTrainer();
                source("sText_PlayerGotMoney", [("B_BUFF1", (4 * definition.Party[^1].Level * definition.BaseMoney).ToString())]);
                return true;
            }
        }
        if (trainer.Stage == CampaignForestTrainerStage.Finished && trainer.Combat.Battle.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw)
        {
            int page = battle.PageIndex - Route22RivalVictoryPageCount;
            if (page == 0) { source("sText_PlayerWhiteoutAgainstTrainer", []); return true; }
            if (page == 1)
            {
                if (_session.State.Money == 0) source("sText_PlayerWhiteout2", []);
                else source("sText_PlayerPanicked", [("B_BUFF1", _session.WhiteOutMoneyLoss.ToString())]);
                return true;
            }
        }
        // The application already expanded source defeat/scurry dialogue into pages.
        return false;
    }
    // These exact source text controls are presentation cues, not printable words.
    // Match only admitted controls (Erika uses the combined PLAY_BGM spelling).
    // Keep unknown controls visible for diagnosis; never erase arbitrary brace content.
    // Audio reads the untouched source pages separately; cleaning does not fire cues.
    private static string PewterDisplayText(string text)
    {
        foreach(var token in new[]{"{FONT_NORMAL}","{FONT_MALE}","{PLAY_BGM MUS_ENCOUNTER_GYM_LEADER}","{PLAY_BGM}","{MUS_ENCOUNTER_GYM_LEADER}","{PAUSE_MUSIC}","{MUS_OBTAIN_BADGE}","{PAUSE 0xFE}","{PAUSE 0x56}","{RESUME_MUSIC}"})
            text=text.Replace(token,"",StringComparison.Ordinal);
        return text;
    }
    private static IEnumerable<string> ForestTrainerPlainPages(string text)
    {
        text = PewterDisplayText(text);
        var lines = text.Split('\n');
        if (lines.Length <= 2) { yield return text; yield break; }
        // A field-return text can contain three lines. Preserve all its words by
        // scrolling one line at a time in the two-line battle message window.
        for (int i = 0; i + 1 < lines.Length; i++) yield return lines[i] + "\n" + lines[i + 1];
    }
    private bool ForestTrainerShiftPending => _session.State.ForestTrainerBattle?.Combat.Phase == ForestTrainerCombatPhase.AwaitingShiftChoice;
    private void SynchronizeForestTrainerChoice()
    {
        if (!ForestTrainerShiftPending || PresentedWildBattle!.Replacement is not null) return;
        var combat = _session.State.ForestTrainerBattle!.Combat;
        int index = combat.PendingOpponentIndex ?? throw new InvalidDataException("Trainer SHIFT opponent missing.");
        _wildPages = WildSourcePages("sText_EnemyAboutToSwitchPkmn", ("B_BUFF2", _catalog.PokemonContent!.SpeciesNames[combat.OpponentParty[index].Pokemon.SourceSpeciesId]));
        _wildMenu = "trainer-shift";
        for (int i = 0; i + 1 < _wildPages.Length; i++) _wildArrowPages.Add(i);
    }
    private bool TryForestTrainerInput(InputEventKey key, CampaignWildBattle battle)
    {
        if (_session.State.ForestTrainerBattle is null || battle.Phase != CampaignBattlePhase.ChooseMove) return false;
        if (battle.Replacement == CampaignReplacementStage.Select)
        {
            if (key.Keycode == Key.Up) _wildCursor = Math.Max(0, _wildCursor - 1);
            if (key.Keycode == Key.Down) _wildCursor = Math.Min(_session.State.Party.Length - 1, _wildCursor + 1);
            if (key.Keycode == Key.Enter) ApplyWildChoice(new("battle_switch", PartyIndex: _wildCursor));
            else if (key.Keycode == Key.Backspace && ForestTrainerShiftPending) ApplyWildChoice(new("trainer_shift", Choice: 0));
            return true;
        }
        if (!ForestTrainerShiftPending) return false;
        if (_wildPage + 1 < _wildPages.Length)
        {
            if (key.Keycode == Key.Enter && _wildElapsed > .1) { _wildPage++; _wildElapsed = 0; }
            return true;
        }
        if (key.Keycode is Key.Up or Key.Down) _wildCursor ^= 1;
        if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _wildElapsed > .1)
            ApplyWildChoice(new("trainer_shift", Choice: key.Keycode == Key.Enter && _wildCursor == 0 ? 1 : 0));
        return true;
    }
    private bool DrawForestTrainerChoice(CampaignWildBattle battle)
    {
        if (_session.State.ForestTrainerBattle is null || battle.Phase != CampaignBattlePhase.ChooseMove ||
            !ForestTrainerShiftPending && battle.Replacement is null) return false;
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        _battleSkin.DrawBackground(this); DrawTexture(CurrentWildBattleTerrain, Vector2.Zero);
        if (battle.Replacement == CampaignReplacementStage.Select)
        { DrawCaptureParty(battle, true); _battleSkin.DrawBattleMessage(this); WildLines("Choose a POKéMON."); }
        else
        {
            var mon = WildDisplayPlayer(battle); var asset = _battleArt[(mon.SourceSpeciesId, "back", mon.IsShiny)];
            DrawTexture(asset.Texture, asset.Position); DrawWildHealth(mon, WildBattleSide.Player, mon.CurrentHp);
            _battleSkin.DrawBattleMessage(this); WildLines(_wildPages[_wildPage]);
            if (_wildPage + 1 == _wildPages.Length) DrawCaptureYesNo();
            else DrawTextCursor(_battleWhite, LabBattleUiSkin.BattleMessageText, _wildPages[_wildPage].Split('\n'), 16, _wildElapsed, true);
        }
        if (_wildNotice is not null) ItemMessageBox(_wildNotice);
        return true;
    }
    private bool ForestTrainerOpening(CampaignWildBattle battle) => _session.State.ForestTrainerBattle is not null &&
        battle.Phase == CampaignBattlePhase.Events && WildCurrentEvent(battle) is WildBattleStartedEvent;
    private bool DrawForestTrainerPortrait(CampaignWildBattle battle)
    {
        if (!ForestTrainerOpening(battle) || _wildPage != 0) return false;
        DrawTexture(_forestTrainerPortraits[VisibleTrainerPortraitId], new(144, 8));
        DrawTexture(_forestTrainerPortraits[_session.State.PlayerGender == 0 ? "red-back" : "leaf-back"], new(48, 48));
        return true;
    }
    private bool DrawForestTrainerDefeatPortrait(CampaignWildBattle battle)
    {
        if (_session.State.ForestTrainerBattle is not { Stage: CampaignForestTrainerStage.Finished } trainer ||
            !(trainer.Combat.Battle.Outcome == WildBattleOutcome.Won && battle.PageIndex > 0 ||
              Route22RivalVisible && (trainer.Combat.Battle.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw) && battle.PageIndex < Route22RivalVictoryPageCount)) return false;
        DrawTexture(_forestTrainerPortraits[VisibleTrainerPortraitId], new(144, 8));
        return true;
    }
    private bool ForestTrainerHidePlayer(CampaignWildBattle battle, WildBattleSide side) =>
        ForestTrainerOpening(battle) && _wildPage == 1 && side == WildBattleSide.Player;
    private bool Route22RivalVisible => _session.State.ForestTrainerBattle is {} trainer && _catalog.Route22Trainers?.Trainers.ContainsKey(trainer.Trainer)==true;
    private int Route22RivalVictoryPageCount => Route22RivalVisible ? _catalog.TextPages(_catalog.Route22Trainers!.Battles[_session.State.ForestTrainerBattle!.Trainer].VictoryText).Length : 0;
    private string VisibleTrainerPortraitId => VisibleOrdinaryTrainer().TrainerClass switch
    {"TRAINER_CLASS_CHANNELER"=>"channeler-front","TRAINER_CLASS_BOSS"=>"giovanni-front","TRAINER_CLASS_BEAUTY"=>"beauty-front","TRAINER_CLASS_COOLTRAINER"=>"cooltrainer-f-front","TRAINER_CLASS_BIKER"=>"biker-front","TRAINER_CLASS_POKEMANIAC"=>"pokemaniac-front","TRAINER_CLASS_GAMER"=>"gamer-front","TRAINER_CLASS_ENGINEER"=>"engineer-front","TRAINER_CLASS_SAILOR"=>"sailor-front","TRAINER_CLASS_FISHERMAN"=>"fisherman-front","TRAINER_CLASS_GENTLEMAN"=>"gentleman-front","TRAINER_CLASS_RIVAL_LATE"=>"rival-late-front","TRAINER_CLASS_RIVAL_EARLY"=>"rival-early-front","TRAINER_CLASS_PICNICKER"=>"picnicker-front","TRAINER_CLASS_SWIMMER_M"=>"swimmer-m-front","TRAINER_CLASS_LEADER"=>VisibleOrdinaryTrainer().Id.Value=="pret:TRAINER_LEADER_ERIKA"?"erika-front":VisibleOrdinaryTrainer().Id==VermilionGymTrainerPartyFactory.LtSurge?"lt-surge-front":VisibleOrdinaryTrainer().Id==CeruleanTrainerPartyFactory.Misty?"misty-front":"brock-front","TRAINER_CLASS_YOUNGSTER"=>"youngster-front","TRAINER_CLASS_LASS"=>"lass-front","TRAINER_CLASS_CAMPER"=>"camper-front","TRAINER_CLASS_SUPER_NERD"=>"super-nerd-front","TRAINER_CLASS_HIKER"=>"hiker-front","TRAINER_CLASS_TEAM_ROCKET"=>_catalog.PokemonTowerTrainers?.Trainers.ContainsKey(VisibleOrdinaryTrainer().Id)==true||_catalog.RocketHideoutTrainers?.Trainers.ContainsKey(VisibleOrdinaryTrainer().Id)==true?"rocket-grunt-m-front":"rocket-m-front",_=>"bug-catcher-front"};
}
