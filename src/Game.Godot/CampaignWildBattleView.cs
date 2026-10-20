using Game.Domain.Events;
using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Capture;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private Texture2D _wildTerrain=null!;
    private Texture2D? _mtMoonTerrain;
    private Texture2D? _gymTerrain, _leaderTerrain;
    private PretCityBattleTerrain? _cityBattleTerrain;
    private readonly Dictionary<string,Texture2D> _cityBattleScenes = [];
    private Texture2D WildBattleTerrainForMap(MapId mapId)
    {
        if (SeafoamBattleTerrain(mapId) is {} seafoamTerrain) return seafoamTerrain;
        if (FiveIslandBattleTerrain(mapId) is {} fiveIslandTerrain) return fiveIslandTerrain;
        if (SixIslandBattleTerrain(mapId) is {} sixIslandTerrain) return sixIslandTerrain;
        if (FourIslandBattleTerrain(mapId) is {} fourIslandTerrain) return fourIslandTerrain;
        if (IsVictoryRoadCave(mapId))
            return _mtMoonTerrain ?? throw new InvalidDataException("Victory Road cave terrain was not loaded.");
        if (PresentedWildBattle?.State.Terrain is WildBattleTerrain.Water or WildBattleTerrain.Pond)
            return _cityBattleScenes[PresentedWildBattle.State.Terrain == WildBattleTerrain.Water ? "water" : "pond"];
        if(PokemonTowerPresentationModel.UsesTowerTerrain(mapId.Value))return _towerTerrain??throw new InvalidDataException("Tower terrain missing.");
        if ((_catalog.RocketHideoutRegion?.NewMaps.ContainsKey(mapId)==true||_catalog.PowerPlantRegion?.NewMaps.ContainsKey(mapId)==true)&&_catalog.SourceMaps[mapId].Events.GetProperty("battle_scene").GetString()=="MAP_BATTLE_SCENE_INDOOR_2")
            return _rocketIndoorTerrain??throw new InvalidDataException("Rocket Indoor2 terrain was not loaded.");
        if (_catalog.SeviiRubyRegion?.NewMaps.ContainsKey(mapId) == true || _catalog.FuchsiaRouteRegion?.NewMaps.ContainsKey(mapId) == true || _catalog.RocketHideoutRegion?.NewMaps.ContainsKey(mapId) == true || _catalog.CeladonApproachRegion?.NewMaps.ContainsKey(mapId) == true || _catalog.RockTunnelRegion?.NewMaps.ContainsKey(mapId) == true || _catalog.Route11Region?.NewMaps.ContainsKey(mapId) == true)
        {
            if (mapId.Value is "map.rock-tunnel-1f" or "map.rock-tunnel-b1f" or "map.digletts-cave-south-entrance" or "map.digletts-cave-b1f" or "map.digletts-cave-north-entrance")
                return _mtMoonTerrain ?? throw new InvalidDataException("Diglett cave terrain was not loaded.");
            // The transaction commits terrain on entry. Rendering does not reclassify it from the player's later tile.
            if (PresentedWildBattle is {} battle)
                return battle.State.Terrain switch
                {
                    WildBattleTerrain.Grass => _wildTerrain,
                    WildBattleTerrain.Cave when _catalog.SeviiRubyRegion?.NewMaps.ContainsKey(mapId) == true => _mtMoonTerrain ?? throw new InvalidDataException("Sevii Ruby cave terrain missing."),
                    WildBattleTerrain.Mountain when _catalog.SeviiRubyRegion?.NewMaps.ContainsKey(mapId) == true => _seviiRubyMountain ?? throw new InvalidDataException("Sevii Ruby mountain terrain missing."),
                    WildBattleTerrain.Plain => _cityBattleScenes["plain"],
                    WildBattleTerrain.Sand => _cityBattleScenes["sand"],
                    WildBattleTerrain.Building => _cityBattleScenes["building"],
                    _ => throw new InvalidDataException("Unsupported committed Route11 terrain.")
                };
        }
        if (mapId.Value == "map.viridian-city-gym" ||
            mapId.Value == "map.fuchsia-city-gym" ||
            mapId.Value == "map.celadon-city-gym" ||
            PretGymBattleTerrain.AppliesToMap(mapId))
        {
            bool leader = _session.State.ForestTrainerBattle?.Trainer.Value is
                "pret:TRAINER_LEADER_BROCK" or "pret:TRAINER_LEADER_MISTY" or "pret:TRAINER_LEADER_ERIKA" or "pret:TRAINER_LEADER_KOGA" or "pret:TRAINER_LEADER_GIOVANNI";
            return (leader ? _leaderTerrain : _gymTerrain) ?? throw new InvalidDataException("Gym terrain was not loaded.");
        }
        if (_cityBattleTerrain is not null && _session.State.World.Player is var player && player.MapId == mapId)
        {
            int? behavior = _session.State.MapEdits?.TryGetValue(player.X+":"+player.Y,out var edit)==true
                ? _catalog.MetatileDefinition(mapId,edit.SourceMetatileId)?.Behavior
                : _catalog.SourceMaps[mapId].SourceBehaviorAt(player.X,player.Y);
            if (_cityBattleTerrain.TryGetScene(mapId,behavior) is {} scene)
                return _cityBattleScenes[scene.Terrain];
        }
        return PretMtMoonBattleTerrain.AppliesToMap(mapId)
            ? _mtMoonTerrain ?? throw new InvalidDataException("MtMoon cave terrain was not loaded.")
            : _wildTerrain;
    }
    private Texture2D CurrentWildBattleTerrain => TeachyBattle is not null ? _wildTerrain : WildBattleTerrainForMap(_session.State.World.Player.MapId);
    private string? _wildPresentationKey, _wildNoticeReturn;
    private string _wildMenu = "actions";
    private ImmutableArray<string> _wildPages = [];
    private readonly HashSet<int> _wildArrowPages = [];
    private int _wildPage, _wildCursor, _wildLastMoveCursor, _wildLearningSlot;
    private double _wildElapsed;
    private bool _wildStatsTotals;
    private ItemId? _wildMedicineItem;

    // Called after the existing, independently verified twelve-sprite lab catalog is loaded.
    private void LoadWildBattlePresentation()
    {
        var terrain=System.IO.Path.Combine(_root,"content/generated/battle/terrain/route1/background-scene.png");
        _wildTerrain=Texture(terrain);
        if(_wildTerrain.GetWidth()!=240||_wildTerrain.GetHeight()!=112)throw new InvalidDataException("Wild terrain size invalid.");
        if (_catalog.MtMoonRegion is not null)
        {
            var cave = PretMtMoonBattleTerrain.Load(_root);
            _mtMoonTerrain = SceneTextures.Texture(cave.SceneTexturePath);
            if (_mtMoonTerrain.GetWidth() != 240 || _mtMoonTerrain.GetHeight() != 112)
                throw new InvalidDataException("MtMoon cave terrain size invalid.");
        }
        {
            var gym = PretGymBattleTerrain.Load(_root);
            _gymTerrain = SceneTextures.Texture(gym.GymSceneTexturePath);
            _leaderTerrain = SceneTextures.Texture(gym.LeaderSceneTexturePath);
            _sourceTerrainIds[_gymTerrain.GetInstanceId()]=gym.GymSourceTerrainId;
            _sourceTerrainIds[_leaderTerrain.GetInstanceId()]=gym.LeaderSourceTerrainId;
            if (_gymTerrain.GetWidth() != 240 || _gymTerrain.GetHeight() != 112 || _leaderTerrain.GetWidth() != 240 || _leaderTerrain.GetHeight() != 112)
                throw new InvalidDataException("Gym terrain size invalid.");
        }
        {
            _cityBattleTerrain = PretCityBattleTerrain.Load(_root);
            foreach (var scene in new[]{_cityBattleTerrain.PlainScene,_cityBattleTerrain.SandScene,_cityBattleTerrain.BuildingScene})
            {
                var texture = SceneTextures.Texture(scene.TexturePath);
                if (texture.GetWidth()!=240 || texture.GetHeight()!=112)throw new InvalidDataException("City battle terrain size invalid.");
                _cityBattleScenes.Add(scene.Terrain,texture);
            }
        }
        var folder = System.IO.Path.Combine(_root, "content/generated/battle/art/route1-battle-art");
        var bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder, "route1-battle-art.json"));
        using var json = JsonDocument.Parse(bytes);
        var sprites = json.RootElement.GetProperty("sprites");
        foreach (var sprite in sprites.EnumerateArray())
        {
            var file = sprite.GetProperty("texturePath").GetString()!;
            var species = sprite.GetProperty("sourceSpeciesId").GetInt32();
            var side = sprite.GetProperty("side").GetString()!;
            var variant = sprite.GetProperty("paletteVariant").GetString()!;
            if (System.IO.Path.GetFileName(file) != file || species is not (16 or 19) || side != "front" || variant is not ("normal" or "shiny"))
                throw new InvalidDataException("Wild battle art identity invalid.");
            var path = System.IO.Path.Combine(folder, file);
            var texture = Texture(path);
            if (texture.GetWidth() != 64 || texture.GetHeight() != 64) throw new InvalidDataException("Wild battle sprite dimensions invalid.");
            var position = sprite.GetProperty("singleBattleTopLeft");
            _battleArt.Add((species, side, variant == "shiny"), new(texture, new(position.GetProperty("x").GetInt32(), position.GetProperty("y").GetInt32())));
        }
        foreach (var species in new[] { 16, 19 })
            foreach (var shiny in new[] { false, true })
                if (!_battleArt.ContainsKey((species, "front", shiny))) throw new InvalidDataException("Wild battle sprite variant missing.");
        LoadCapturePresentation();
        LoadForestBattlePresentation();
    }

    private static bool SingleBattlerFaintShown(CampaignWildBattle battle,PokemonInstance mon,WildBattleSide side)=>
        mon.CurrentHp==0&&(battle.Phase==CampaignBattlePhase.Results||battle.Phase==CampaignBattlePhase.Events&&
            battle.Events.Take(battle.EventIndex+1).OfType<WildBattleFaintedEvent>().Any(e=>e.Side==side));
    private bool WildWithdrawing(CampaignWildBattle b) => b.Phase == CampaignBattlePhase.Events &&
        WildCurrentEvent(b) is WildBattlePlayerSwitchedEvent && !SourcePlayerBatonSwitch(b) && _wildPage == 0;
    private PokemonInstance WildDisplayPlayer(CampaignWildBattle b) => WildWithdrawing(b)
        ? ((WildBattlePlayerSwitchedEvent)WildCurrentEvent(b)!).Previous.Pokemon : b.DisplayPlayer;
    private PartyPokemon WildRewardRecipient(CampaignWildBattle b) => PresentedBattlePlayerParty[b.RewardPartyIndex];
    private string WildName(WildBattleSide side) => side == WildBattleSide.Player
        ? WildWithdrawing(PresentedWildBattle!) ? _catalog.PokemonDisplayName(PresentedWildBattle!.LastSwitch!.Outgoing) : SsAnnePendingPlayerForceOut(PresentedWildBattle!) is {} forced ? _catalog.PokemonDisplayName(_session.State.Party[forced.PreviousIndex]) : _catalog.PokemonDisplayName(PresentedWildBattle!.OwnedBefore)
        : TowerOpponentAppearance()?.Name ?? PresentedBattleSpeciesName(PresentedWildBattle!.DisplayOpponent.SourceSpeciesId);
    private string WildPrefixedName(WildBattleSide side) => (side == WildBattleSide.Opponent ? _session.State.ForestTrainerBattle is not null ? _battleSelectionText.FoePrefix : _battleSelectionText.WildPrefix : "") + WildName(side);
    private string WildStatName(WildBattleStat stat) => SourceTrainerPresentationModel.StatName(_battleText,stat);
    private ImmutableArray<string> WildSourcePages(string symbol, params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["B_PLAYER_NAME"] = _session.State.PlayerName, ["PLAYER"] = _session.State.PlayerName,
            ["B_PLAYER_MON1_NAME"] = WildName(WildBattleSide.Player), ["B_OPPONENT_MON1_NAME"] = WildName(WildBattleSide.Opponent),
            // Sound requests and exact sound timing remain a separate presentation gate.
            ["PLAY_SE SE_FLEE"] = "", ["PAUSE 32"] = "", ["PLAY_BGM MUS_CAUGHT"] = ""
        };
        AddForestTrainerTextValues(values);
        foreach (var (key, value) in extra) values[key] = value;
        return SourceBattleText.Pages(_battleText, symbol, values);
    }
    private static bool WildStatsPage(CampaignWildBattle b) => b.Phase == CampaignBattlePhase.Results &&
        b.PageIndex < b.Pages.Length && b.Pages[b.PageIndex] is { Text: "", Reward: WildRewardLevelEvent };

    private void SynchronizeWildBattle()
    {
        if (PresentedWildBattle is not { } b || _session.State.ForestTrainerBattle?.Stage == CampaignForestTrainerStage.Intro)
        {
            _wildPresentationKey = null; _wildNotice = null; _wildNoticeReturn = null; _wildMenu = "actions";
            _wildCursor = 0; _wildLastMoveCursor = 0; _wildPages = []; ResetCaptureBag(); return;
        }
        _held = null;
        var key = $"{b.Id}:{b.Phase}:{b.State.EventSequence}:{b.EventIndex}:{b.PageIndex}:{b.PendingMoves.Length}:{b.Capture?.Stage}:{b.Replacement}:{b.ActivePartyIndex}:{b.RewardPartyIndex}:{_session.State.ForestTrainerBattle?.Stage}:{_session.State.ForestTrainerBattle?.TrainerEventIndex}";
        if (_wildPresentationKey == key) return;
        _wildPresentationKey = key; _wildElapsed = 0; _wildPage = 0; _wildPages = []; _wildArrowPages.Clear(); _wildSourceContinuations.Clear();
        _wildNotice = null; _wildNoticeReturn = null; _wildStatsTotals = false; _wildMenu = "actions"; _wildCursor = 0;_wildMedicineItem=null;
        if (b.Capture is not null) return;
        if (b.Phase == CampaignBattlePhase.ChooseMove) { if(b.Replacement is null && !ForestTrainerShiftPending && SourceBattleForcedMoveSlot(b) is not null)SelectWildMoveMenu(b); SynchronizeForestTrainerChoice(); return; }
        var pages = ImmutableArray.CreateBuilder<string>();
        void Source(string symbol, params (string Key, string Value)[] values)
        {
            var added = WildSourcePages(symbol, values);
            if(symbol=="sText_TheGhostWas")added=added.Where(p=>!string.IsNullOrWhiteSpace(p)).ToImmutableArray();
            bool finalWait = _battleText[symbol].EndsWith("\\p", StringComparison.Ordinal) ||
                _battleText[symbol].EndsWith("\\l", StringComparison.Ordinal) || _battleText[symbol].Contains("{PAUSE_UNTIL_PRESS}", StringComparison.Ordinal);
            for (int i = 0; i < added.Length; i++) if (i + 1 < added.Length || finalWait) _wildArrowPages.Add(pages.Count + i);
            AppendBattleSourcePages(pages, added, _wildSourceContinuations);
        }
        if (b.Phase == CampaignBattlePhase.Events)
        {
            if (ForestTrainerEventPages(Source)) { _wildPages = pages.ToImmutable(); return; }
            var current = WildCurrentEvent(b) ?? throw new InvalidDataException("Battle event missing.");
            if (SingleMedicineEffectMessage(current) is {} itemEffect) { Source(itemEffect.Symbol,itemEffect.Values); _wildPages=pages.ToImmutable(); return; }
            if (SourceTrainerEventPages(b, Source, pages)) { _wildPages = pages.ToImmutable(); return; }
            if(current is WildBattleInfatuationBlockedEvent love) Source("sText_PkmnInLove",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(love.Side)),("B_SCR_ACTIVE_NAME_WITH_PREFIX",WildPrefixedName(love.Source)));
            var forestMessage = FuchsiaRouteWildMessage(current) ?? SnorlaxWildMessage(current) ?? PokemonTowerWildMessage(current) ?? RocketHideoutWildMessage(current) ?? CeladonGymWildMessage(current) ?? RockTunnelWildMessage(current) ?? Route11WildMessage(b,current) ?? SsAnneWildMessage(current) ?? VermilionWildMessage(current) ?? MtMoonWildMessage(b,current) ?? Route3WildMessage(b,current) ?? ForestWildMessage(current);
            if (forestMessage is { } message)
            {
                if (message.Symbol.Length > 0) Source(message.Symbol, message.Values);
            }
            else switch (current)
            {
                case WildBattleStartedEvent:
                    if (PokemonTowerStartedPages(b,Source)) break;
                    if (_session.State.ForestTrainerBattle is not null) { Source("sText_Trainer1WantsToBattle"); Source("sText_Trainer1SentOutPkmn"); }
                    else Source("sText_WildPkmnAppeared");
                    Source("sText_GoPkmn"); break;
                case WildBattleMoveUsedEvent used:
                    Source("sText_AttackerUsedX", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(used.Actor)), ("B_BUFF2", MoveName(used.Move) + "!")); break;
                case WildBattleMoveMissedEvent missed:
                    Source("sText_AttackMissed", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(missed.Actor))); break;
                case WildBattleHpChangedEvent: break;
                case WildBattleRecoveredEvent recovered:
                    Source("sText_PkmnRegainedHealth", ("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(recovered.Side)));break;
                case WildBattlePotionRecoveredEvent potion:
                    Source("gText_PkmnHPRestoredByVar2", ("STR_VAR_1", WildName(potion.Side)), ("STR_VAR_2", potion.HpRestored.ToString())); break;
                case WildBattleMedicineAppliedEvent medicine when SingleMedicinePages(medicine,Source): break;
                case WildBattleMedicineAppliedEvent medicine:
                    string targetName=_catalog.PokemonDisplayName(PresentedBattlePlayerParty.Single(p=>p.InstanceId==medicine.TargetInstanceId));
                    string medicineText=medicine.HpAfter>medicine.HpBefore?"gText_PkmnHPRestoredByVar2":
                        WildBattleStatus.IsSleeping(medicine.StatusBefore)?"gText_PkmnWokeUp2":ForestPresentationStatus(medicine.StatusBefore) switch
                        {WildBattleStatus.Poison=>"gText_PkmnCuredOfPoison",WildBattleStatus.Burn=>"gText_PkmnBurnHealed",WildBattleStatus.Freeze=>"gText_PkmnThawedOut",WildBattleStatus.Paralysis=>"gText_PkmnCuredOfParalysis",_=>throw new InvalidDataException("Unsupported medicine result message.")};
                    Source(medicineText,("STR_VAR_1",targetName),("STR_VAR_2",(medicine.HpAfter-medicine.HpBefore).ToString()));break;
                case WildBattleRecoilEvent recoil:
                    Source("sText_PkmnHitWithRecoil", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(recoil.Actor))); break;
                case WildBattleCriticalHitEvent: Source("sText_CriticalHit"); break;
                case WildBattleStatChangedEvent stat:
                    Source("sText_DefendersStatFell", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat)), ("B_BUFF2", SourceTrainerPresentationModel.StatChangeText(_battleText,false,false))); break;
                case WildBattleStatAtMinimumEvent stat:
                    Source("sText_StatsWontDecrease", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat))); break;
                case WildBattleAbilityPreventedEvent ability when ability.Ability == "ABILITY_KEEN_EYE":
                    Source("sText_PkmnsXPreventsYLoss", ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_SCR_ACTIVE_ABILITY", _catalog.DisplayNames.AbilityName(ability.Ability)), ("B_BUFF1", "accuracy")); break;
                case WildBattleFaintedEvent fainted:
                    Source("sText_TargetFainted", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(fainted.Side))); break;
                case WildBattleRunAttemptEvent run:
                    if (run.Escaped && run.EscapingItem is { } escapingItem)
                        Source("sText_PkmnFledUsingIts", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(WildBattleSide.Player)),
                            ("B_LAST_ITEM", escapingItem == "ITEM_SMOKE_BALL" ? _catalog.DisplayNames.ItemName(escapingItem) : throw new InvalidDataException("Unadmitted escape item: " + escapingItem)));
                    else Source(run.Escaped ? "sText_GotAwaySafely" : "sText_CantEscape");
                    break;
                case WildBattleBallThrownEvent thrown:
                    Source("sText_PlayerUsedItem", ("B_LAST_ITEM", CaptureBallName(thrown.Ball))); break;
                case WildBattleCaptureResultEvent captured:
                    // Source animation code 4 is three-shake success. These are committed
                    // result pages, not an invented four-shake animation or another roll.
                    Source(captured.Outcome == CaptureOutcome.Caught ? "sText_GotchaPkmnCaught" : captured.AnimationCode switch
                    {
                        0 => "sText_PkmnBrokeFree", 1 => "sText_ItAppearedCaught",
                        2 => "sText_AarghAlmostHadIt", 3 => "sText_ShootSoClose",
                        _ => throw new InvalidDataException("Invalid capture result animation code.")
                    }); break;
                case WildBattleReplacementRequiredEvent: Source("sText_UseNextPkmn"); break;
                case WildBattlePlayerReplacedEvent: Source("sText_GoPkmn"); break;
                case WildBattlePlayerSwitchedEvent:
                    if (SourcePlayerBatonSwitch(b))
                    { Source(SourceBatonSendOutText(b), ("B_BUFF1", _catalog.PokemonDisplayName(b.OwnedBefore))); break; }
                    var change = b.LastSwitch ?? throw new InvalidDataException("Voluntary switch presentation is missing.");
                    Source(change.WithdrawalTextSymbol, ("B_BUFF1", _catalog.PokemonDisplayName(change.Outgoing)));
                    Source(change.SendOutTextSymbol, ("B_BUFF1", _catalog.PokemonDisplayName(b.OwnedBefore)));
                    break;
                default:
                    pages.Add("This battle event is not\nsupported yet.");
                    Write("unsupportedBattlePresentation",new{type=current.GetType().Name,current.Sequence});break;
            }
        }
        else if (b.PageIndex < b.Pages.Length)
        {
            var page = b.Pages[b.PageIndex];
            switch (page.Reward)
            {
                case WildRewardExperienceEvent xp:
                    Source("sText_PkmnGainedEXP", ("B_BUFF1", _catalog.PokemonDisplayName(WildRewardRecipient(b))), ("B_BUFF2", ""), ("B_BUFF3", xp.Gained.ToString())); break;
                case WildRewardLevelEvent level when page.Text.Length > 0:
                    Source("sText_PkmnGrewToLv", ("B_BUFF1", _catalog.PokemonDisplayName(WildRewardRecipient(b))), ("B_BUFF2", level.After.ToString())); break;
                case WildRewardLevelEvent: break;
                case WildRewardMoveLearnedEvent learned:
                    Source("sText_PkmnLearnedMove", ("B_BUFF1", _catalog.PokemonDisplayName(WildRewardRecipient(b))), ("B_BUFF2", MoveName(learned.Move))); break;
                default:
                    if (VermilionPayDayResultPage(b, Source) || ForestTrainerResultPage(b, Source)) break;
                    if (_session.State.ForestTrainerBattle is null && b.State.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw) Source("sText_PlayerWhiteout");
                    else if (_session.State.ForestTrainerBattle is not null)
                    {
                        var added = SourceTrainerPresentationModel.ScrollResultPages(TrainerTowerBattleSpeech(b.PageIndex) ?? page.Text).ToArray();
                        for (int i = 0; i + 1 < added.Length; i++) _wildArrowPages.Add(pages.Count + i);
                        AppendBattleSourcePages(pages, added, _wildSourceContinuations);
                    }
                    else pages.Add(page.Text);
                    break;
            }
        }
        _wildPages = pages.ToImmutable();
        if (b.Phase == CampaignBattlePhase.Results && b.Pages.IsEmpty && !b.PendingMoves.IsEmpty) BeginWildMoveLearning();
    }

    private void ProcessWildBattle(double delta)
    {
        if(SourceAnimationBlocking)return;
        _wildElapsed += delta;
        if(PresentedWildBattle is {} captureBattle)ProcessWildCaptureBall(captureBattle);
        if(PresentedWildBattle is {Phase:CampaignBattlePhase.Events} fluteBattle && ProcessPokemonTowerFluteBattle(fluteBattle))return;
        if (_wildNotice is null && PresentedWildBattle is { Phase: CampaignBattlePhase.Events } b &&
            _wildPages.IsEmpty && _wildElapsed >= .45 && (WildCurrentEvent(b) is
                WildBattleHpChangedEvent or WildBattleStatusChangedEvent { Reason: "faint" } or WildBattleAbilityActivatedEvent or WildBattleDisableChangedEvent or WildBattleRolloutChangedEvent or WildBattleInfatuationChangedEvent || SourceTrainerSilentEvent(b) || SnorlaxSilentEvent(b) || PokemonTowerSilentEvent(b) || CeladonGymSilentEvent(b) || RockTunnelSilentEvent(b) || Route11SilentEvent(b) || MtMoonSilentEvent(b) || VermilionSilentEvent(b))) Apply(new("ack"));
    }
    private string WildCommandError() => _lastCommandFault ? _summaryText.Get("notice.battle-step-not-committed") : _status switch
    {
        "hm_move_cannot_be_forgotten" => HmCannotForgetText,
        "pokemon_learning_effect_unsupported" => UnsupportedLearningText,
        "wild_battle_switch_rooted" or "wild_battle_switch_mean_look" or "celadon_gym_trainer_switch_rooted" or "wild_battle_switch_binding" or "pewter_trainer_switch_binding" => string.Join('\n', WildSourcePages("gText_PkmnCantSwitchOut", ("STR_VAR_1", WildName(WildBattleSide.Player)))),
        "wild_battle_switch_arena_trap" or "route11_trainer_switch_arena_trap" => string.Join('\n',WildSourcePages("gText_PkmnsXPreventsSwitching",("B_BUFF1",WildName(WildBattleSide.Opponent)),("B_LAST_ABILITY",_catalog.DisplayNames.AbilityName("ABILITY_ARENA_TRAP")))),
        "wild_battle_run_arena_trap" => string.Join('\n',WildSourcePages("sText_PreventsEscape",("B_SCR_ACTIVE_NAME_WITH_PREFIX",WildPrefixedName(WildBattleSide.Opponent)),("B_SCR_ACTIVE_ABILITY",_catalog.DisplayNames.AbilityName("ABILITY_ARENA_TRAP")))),
        "wild_battle_run_rooted" or "wild_battle_run_binding" or "wild_battle_run_mean_look" => string.Join('\n',WildSourcePages("sText_CantEscape2")),
        "trainer_battle_cannot_run" => string.Join('\n', WildSourcePages("sText_NoRunningFromTrainers")),
        "trainer_battle_cannot_capture" => string.Join('\n', WildSourcePages("sText_DontBeAThief")),
        "wild_battle_player_move_pp_empty" => _summaryText.Get("notice.battle-move-no-pp"),
        "wild_battle_player_move_disabled" => _summaryText.Get("notice.battle-move-disabled"),
        "wild_battle_player_rollout_locked" => _summaryText.Get("notice.battle-move-must-finish"),
        "wild_battle_player_move_effect_unsupported" => _summaryText.Get("notice.battle-move-effect-unsupported"),
        "wild_battle_opponent_move_effect_unsupported" => _summaryText.Get("notice.battle-opponent-move-effect-unsupported"),
        "potion_no_effect" or "medicine_no_effect" => ItemText("gText_WontHaveEffect"),
        "medicine_not_in_bag" => _inventoryText.Get("notice.none-left"),
        "battle_medicine_target_identity" => _inventoryText.Get("notice.choose-target-again"),
        "potion_not_in_bag" => _inventoryText.Get("notice.no-potions"),
        "capture_no_poke_ball" or "capture_ball_not_in_bag" or "ball_not_in_bag" or "poke_ball_not_in_bag" => _inventoryText.Get("notice.no-poke-balls"),
        "storage_full" => PcText("gText_BoxFull"),
        "storage_delivery_unsupported" => _storageText.Get("notice.capture-delivery-unavailable"),
        "capture_profile_unsupported" or "capture_not_enabled" or "capture_ball_unsupported" or "wild_capture_unsupported" => _inventoryText.Get("notice.capture-unavailable"),
        "replacement_member_invalid" or "wild_replacement_fainted" or "wild_replacement_invalid" or "wild_replacement_target_invalid" => _summaryText.Get("notice.battle-replacement-required"),
        "nickname_length" => FieldMenuText.Get("naming.notice.length", ("limit", "10")),
        "nickname_character_unsupported" => FieldMenuText.Get("naming.notice.character-unavailable"),
        _ => _summaryText.Get("notice.battle-choice-unavailable")
    };
    private bool ApplyWildChoice(CampaignCommand command)
    {
        bool accepted = Apply(command);
        if (accepted) { _wildMenu = "actions"; _wildCursor = 0; return true; }
        _wildNotice = WildCommandError(); return false;
    }
    private void BeginWildMoveLearning()
    {
        var b = PresentedWildBattle!;
        if (b.PendingMoves.IsEmpty) return;
        CaptureMoveSelection("wild:" + b.Id, WildRewardRecipient(b), b.PendingMoves[0].Move);
        if (MoveLearningUnavailable(b.PendingMoves[0].Move))
        {
            _wildPages = []; _wildPage = 0; _wildElapsed = 0; _wildMenu = "learn-moves";
            _wildCursor = WildRewardRecipient(b).Pokemon.Moves.Length; return;
        }
        var values = new[] { ("B_BUFF1", _catalog.PokemonDisplayName(WildRewardRecipient(b))), ("B_BUFF2", MoveName(b.PendingMoves[0].Move)) };
        _wildPages = JoinBattleSourcePages(_wildSourceContinuations, WildSourcePages("sText_TryToLearnMove1", values), WildSourcePages("sText_TryToLearnMove2", values), WildSourcePages("sText_TryToLearnMove3", values));
        _wildPage = 0; _wildElapsed = 0; _wildMenu = "learn-intro"; _wildCursor = 0;
    }
    private bool WildLearningInput(InputEventKey key)
    {
        var b = PresentedWildBattle!;
        if (_wildMenu == "learn-intro")
        {
            if (key.Keycode == Key.Enter && _wildElapsed > .1)
            {
                if (TryAdvanceWildVisualMessage()) return true;
                if (_wildPage + 1 < _wildPages.Length) { _wildPage++; _wildElapsed = 0; }
                else { _wildMenu = "learn-moves"; _wildCursor = 0; _wildPages = []; }
            }
            else if (key.Keycode == Key.Backspace) { _wildMenu = "learn-confirm"; _wildLearningSlot = -1; _wildCursor = 1; }
            return true;
        }
        if (_wildMenu == "learn-moves")
        {
            if (!MoveSelectionCurrent("wild:" + b.Id, WildRewardRecipient(b), b.PendingMoves[0].Move))
            { _wildCursor = 0; _wildNotice = SummaryChangedNotice; return true; }
            if (key.Keycode == Key.Up) _wildCursor = Math.Max(0, _wildCursor - 1);
            if (key.Keycode == Key.Down) _wildCursor = Math.Min(WildRewardRecipient(b).Pokemon.Moves.Length, _wildCursor + 1);
            if (key.Keycode is Key.Enter or Key.Backspace)
            {
                _wildLearningSlot = key.Keycode == Key.Backspace || _wildCursor == WildRewardRecipient(b).Pokemon.Moves.Length ? -1 : _wildCursor;
                if (_wildLearningSlot >= 0 && MoveLearningUnavailable(b.PendingMoves[0].Move)) return true;
                _wildMenu = "learn-confirm"; _wildCursor = 1;
            }
            return true;
        }
        if (key.Keycode is Key.Up or Key.Down) _wildCursor ^= 1;
        if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _wildCursor == 1)
        { _wildMenu = "learn-moves"; _wildCursor = 0; return true; }
        if (key.Keycode == Key.Enter)
        {
            if (!MoveSelectionCurrent("wild:" + b.Id, WildRewardRecipient(b), b.PendingMoves[0].Move))
            { _wildCursor = 0; _wildMenu = "learn-moves"; _wildNotice = SummaryChangedNotice; return true; }
            if (_wildLearningSlot >= 0 && MoveLearningUnavailable(b.PendingMoves[0].Move))
            { _wildMenu = "learn-moves"; _wildCursor = WildRewardRecipient(b).Pokemon.Moves.Length; return true; }
            if (ApplyWildChoice(new("wild_learn", MoveSlot: _wildLearningSlot)))
            {
                if (PresentedWildBattle is { PendingMoves.IsEmpty: false } next &&
                    next.Pages.IsEmpty) BeginWildMoveLearning();
            }
            else if (_status == "hm_move_cannot_be_forgotten")
            { _wildMenu = "learn-moves"; _wildCursor = Math.Max(0, _wildLearningSlot); _wildNoticeReturn = null; }
        }
        return true;
    }
    private bool WildBattleInput(InputEventKey key)
    {
        if (key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape) return false;
        if (!key.Pressed || key.Echo) return true;
        if(SourceAnimationBlocking)return true;
        _held = null;
        var b = PresentedWildBattle!;
        if(PokemonTowerRevealInputLocked(b))return true;
        if (_wildNotice is not null)
        {
            if (key.Keycode is Key.Enter or Key.Backspace)
            {
                if (TryAdvanceWildNotice()) return true;
                _wildNotice = null;
                if (_wildNoticeReturn is { } target) { _wildMenu = target; if(target is not ("medicine-target" or "medicine-move"))_wildCursor = 0; _wildNoticeReturn = null; }
            }
            return true;
        }
        if (BattlePartyInspectionInput(key)) return true;
        if(b.Phase==CampaignBattlePhase.ChooseMove && b.Replacement is null && !ForestTrainerShiftPending && SourceBattleForcedMoveSlot(b) is {} forced)
        { SelectWildMoveMenu(b); if(key.Keycode==Key.Enter)ApplyWildChoice(new("battle_move",MoveSlot:forced));return true; }
        if (TryForestTrainerInput(key, b)) return true;
        if (TryCaptureWildInput(key, b)) return true;
        if (_wildMenu.StartsWith("learn-", StringComparison.Ordinal)) return WildLearningInput(key);
        if (b.Phase != CampaignBattlePhase.ChooseMove)
        {
            if (key.Keycode == Key.Enter && !WildCaptureBallBlocking(b) && _wildElapsed > (b.Phase == CampaignBattlePhase.Events &&
                WildCurrentEvent(b) is WildBattleWeatherDamageEvent or WildBattleStatusDamageEvent or WildBattleBindingDamageEvent or WildBattleConfusionSelfHitEvent or WildBattleTowerHpEvent ? .45 : .1))
            {
                if (WildStatsPage(b) && !_wildStatsTotals) { _wildStatsTotals = true; _wildElapsed = 0; Write("wildBattleStatsPage", new { page = 2 }); }
                else if (TryAdvanceWildVisualMessage()) return true;
                else if (_wildPage + 1 < _wildPages.Length) { _wildPage++; _wildElapsed = 0; Write("wildBattlePage", new { key = _wildPresentationKey, page = _wildPage }); }
                else if (b.Phase == CampaignBattlePhase.Results && b.PageIndex + 1 >= b.Pages.Length && !b.PendingMoves.IsEmpty) BeginWildMoveLearning();
                else if (!_wildPages.IsEmpty || b.Phase == CampaignBattlePhase.Results) AcknowledgeWildAttackText();
            }
            return true;
        }
        if (SingleMedicineInput(key,b)) return true;
        if (BattleMoveOrderInput(key, _wildMenu, ref _wildCursor, out var swapNotice))
        { _wildNotice = swapNotice; return true; }
        if (_wildMenu == "party")
        {
            int cancel = _session.State.Party.Length;
            NativePartyMove(key.Keycode,ref _wildCursor,cancel);
            if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _wildCursor == cancel)
            { _wildMenu = "actions"; _wildCursor = 2; }
            else if (key.Keycode == Key.Enter)
            {
                OpenBattlePartyActions(_wildCursor);
            }
            return true;
        }
        if (key.Keycode == Key.Backspace) { _wildMenu = "actions"; _wildCursor = 0; return true; }
        int count = _wildMenu == "actions" ? 4 : SourceBattleRequiresStruggle(b) ? 1 : b.State.Player.Pokemon.Moves.Length;
        if (_wildMenu == "actions" && key.Keycode is Key.Left or Key.Right) _wildCursor ^= 1;
        else if (_wildMenu == "actions" && key.Keycode is Key.Up or Key.Down) _wildCursor ^= 2;
        else if (_wildMenu == "moves" && key.Keycode is Key.Left or Key.Right) { int candidate = _wildCursor ^ 1; if (candidate < count) _wildCursor = candidate; }
        else if (_wildMenu == "moves" && key.Keycode is Key.Up or Key.Down) { int candidate = _wildCursor ^ 2; if (candidate < count) _wildCursor = candidate; }
        else if (key.Keycode == Key.Enter)
        {
            if (_wildMenu == "moves")
            {
                int slot=SourceBattleRequiresStruggle(b)?WildBattleRules.StruggleSlot:_wildCursor;
                if(!SourceBattleMoveSelectable(b,slot))
                {
                    if(SourceBattleSelectionNotice(b,slot) is {} sourceNotice) _wildNotice=string.Join('\n',WildSourcePages(sourceNotice.Symbol,sourceNotice.Values));
                    else if(Route11SealedMoveNotice(b,slot) is {} sealedNotice) _wildNotice=sealedNotice;
                    else if(b.State.Player.DisabledMove==b.State.Player.Pokemon.Moves[slot].Move)
                    {var message=DisabledMoveMessage(WildBattleSide.Player,b.State.Player.Pokemon.Moves[slot].Move);_wildNotice=string.Join('\n',WildSourcePages(message.Symbol,message.Values));}
                    else _wildNotice="This move has no PP left.";
                    return true;
                }
                _wildLastMoveCursor = _wildCursor; ApplyWildChoice(new("battle_move", MoveSlot:slot));
            }
            else switch (_wildCursor)
            {
                case 0: SelectWildMoveMenu(b); break;
                case 1: _wildMenu = "bag"; _wildCursor = 0; break;
                case 2: _wildMenu = "party"; _wildCursor = 0; break;
                case 3: ApplyWildChoice(new("battle_menu", Text: "run")); break;
            }
        }
        return true;
    }

    private void WildLines(string text)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length && i < 2; i++) SourceText(_battleWhite, LabBattleUiSkin.BattleMessageText + new Vector2(0, i * 16), lines[i]);
    }
    private void DrawWildBattle()
    {
        var b = PresentedWildBattle!;
        if (DrawBattlePartyInspection()) return;
        if (DrawSingleMedicineMove(b)) return;
        if (DrawForestTrainerChoice(b)) return;
        if (DrawCaptureWildOverlay(b)) return;
        if (b.Phase == CampaignBattlePhase.ChooseMove && _wildMenu is "bag" or "bag-actions" && _session.State.Inventory is { } inventory)
        {
            DrawItemList(inventory, "bag", _wildMenu == "bag-actions" ? 0 : _wildCursor, _wildNotice is null ? null : WildNoticeSample.Text);
            if (_wildMenu == "bag-actions") DrawItemActionPopup(false, [_itemSkin.Text("gOtherText_Use"), _itemSkin.Text("gFameCheckerText_Cancel")], _wildCursor);
            if (_wildNotice is not null) DrawMessageContinuation(WildNoticeSample,_sourceNormal,OpeningItemUiSkin.PcResultText,14,_wildElapsed);
            return;
        }
        DrawRect(new(0, 0, 240, 160), Colors.Black); _battleSkin.DrawBackground(this);DrawTexture(CurrentWildBattleTerrain,Vector2.Zero);
        if(SourceAnimationPresented)DrawTexture(_sourceMoveField!,Vector2.Zero);
        bool portrait = DrawForestTrainerPortrait(b);
        bool defeatedTrainer = DrawForestTrainerDefeatPortrait(b);
        var healthForeground=new List<Action>();
        foreach (var (side, mon) in new[] { (WildBattleSide.Opponent, b.DisplayOpponent), (WildBattleSide.Player, WildDisplayPlayer(b)) })
        {
            if (side == WildBattleSide.Opponent && TeachyTvHidesCaughtOpponent) continue;
            if (portrait || defeatedTrainer && side == WildBattleSide.Opponent || ForestTrainerHidePlayer(b, side) || PokemonTowerHidePlayer(b,side)) continue;
            if (side == WildBattleSide.Opponent && b.Phase == CampaignBattlePhase.Events &&
                WildCurrentEvent(b) is not WildBattleCaptureResultEvent &&
                b.Events.Take(b.EventIndex + 1).OfType<WildBattleCaptureResultEvent>().Any(c => c.Outcome == CaptureOutcome.Caught)) continue;
            bool faintShown = SingleBattlerFaintShown(b,mon,side);
            if (!faintShown)
            {
                var asset = SingleBattleArt(b,side,mon);
                if(!(side==WildBattleSide.Opponent && DrawCapturedTarget(b,asset.Texture,asset.Position)) && !SourceAnimationPresented && !DrawSourceSubstitute(side) && (side!=WildBattleSide.Opponent||!DrawPokemonTowerOpponent(b)))DrawTexture(asset.Texture, asset.Position);
            }
            int hp = WildDisplayedHp(b, mon, side);
            healthForeground.Add(()=>DrawWildHealth(mon,side,hp));
        }
        foreach(var drawHealth in healthForeground)drawHealth();
        DrawWildCaptureBall(b);
        bool selecting = b.Phase == CampaignBattlePhase.ChooseMove && _wildNotice is null && _wildMenu is "actions" or "moves";
        if (selecting) _uiCorrections.ClearBattleMenu(this); else _battleSkin.DrawBattleMessage(this);
        if (_wildMenu is "party" or "medicine-target")
        {
            DrawWildParty(b);
            if(_wildNotice is not null)
            {var notice=WildNoticeSample;DrawNativePartyMessage(notice.Text);DrawMessageContinuation(notice,_sourceNormal,new(8,120),16,_wildElapsed);}
            else if(!_wildPages.IsEmpty)DrawNativePartyMessage(WildMessageSample.Text);
            return;
        }
        if (_wildNotice is not null)
        {var notice=WildNoticeSample;WildLines(notice.Text);DrawMessageContinuation(notice,_battleWhite,LabBattleUiSkin.BattleMessageText,16,_wildElapsed,true);return;}
        if (_wildMenu.StartsWith("learn-", StringComparison.Ordinal)) { DrawWildLearning(b); return; }
        if (WildStatsPage(b)) { DrawWildStats(b, (WildRewardLevelEvent)b.Pages[b.PageIndex].Reward!); return; }
        if(_sourceResultContinuation is not null&&!_sourceResultCompleted)return;
        if (!_wildPages.IsEmpty)
        {
            if(WildCaptureBallBlocking(b))return;
            var message = WildMessageSample;
            bool textVisible = WildCurrentEvent(b) is not WildBattleTowerHpEvent {Effect:"curse-cost"} || _wildElapsed>=.4;
            if (NativeWildMessageEnabled)
            {
                DrawNativeBattleText(_nativeWildMessage, LabBattleUiSkin.BattleMessageText,
                    _battleWhite, _wildArrowPages.Contains(_wildPage)||!message.IsFinal, _wildElapsed, drawText: textVisible);
            }
            else
            {
                if (textVisible) WildLines(message.Text);
                if (_wildArrowPages.Contains(_wildPage)||!message.IsFinal) DrawTextCursor(_battleWhite,
                    LabBattleUiSkin.BattleMessageText, message.Text.Split('\n'), 16, _wildElapsed, true);
            }
            NotePokemonTowerFluteBattle(b);
            return;
        }
        if (b.Phase != CampaignBattlePhase.ChooseMove) return;
        if (_wildMenu == "medicine-target") WildLines(SourceMenuLabelPresentation.Plain(_battleText, "gText_UseOnWhichPokemon"));
        else if (_wildMenu == "party") WildLines(_summaryPartyText.ChoosePrompt);
        else if (_wildMenu == "actions")
        {
            _battleSkin.DrawActionMenu(this); DrawCompactBattlePrompt(_battleSelectionText.ActionPrompt(_catalog.PokemonDisplayName(b.OwnedBefore), _session.State.PlayerName));
            var labels = _battleSelectionText.ActionLabels;
            for (int i = 0; i < labels.Length; i++) SourceText(_battleMenuFont, LabBattleUiSkin.ActionText(i), labels[i]);
            _uiCorrections.DrawSelectionCursor(this, false, _wildCursor);
        }
        else
        {
            _battleSkin.DrawMoveMenu(this);
            if (SourceBattleRequiresStruggle(b)) { SourceText(_battleMoveFont, LabBattleUiSkin.MoveText(0), MoveName(new("pret:MOVE_STRUGGLE"))); _uiCorrections.DrawSelectionCursor(this, true, 0); return; }
            var moves = b.State.Player.Pokemon.Moves;
            for (int i = 0; i < 4; i++) SourceText(_battleMoveFont, LabBattleUiSkin.MoveText(i), i < moves.Length ? MoveName(moves[i].Move) : "-");
            _wildCursor = Math.Clamp(_wildCursor, 0, moves.Length - 1);
            _uiCorrections.DrawSelectionCursor(this, true, _wildCursor);
            if (DrawBattleMoveOrder(_wildCursor)) return;
            var move = moves[_wildCursor]; int ppState = SourceUiCorrections.PpPaletteState(move.CurrentPp, move.MaximumPp);
            SourceText(_battlePpSmallFont[ppState], new(168, 122), _battleSelectionText.PpLabel); SourceText(_battlePpFont[ppState], new(202, 122), $"{move.CurrentPp,2}/{move.MaximumPp,2}");
            SourceText(_battleMoveFont, new(168, 138), _battleSelectionText.TypeLabel);
            float width = _battleMoveFont.Font.GetStringSize(_battleMoveFont.PrepareText(_battleSelectionText.TypeLabel), fontSize: _battleMoveFont.Size).X;
            SourceText(_battleMenuFont, new(168 + width, 138), _catalog.DisplayNames.TypeName(_catalog.PokemonContent!.Catalog.Moves[move.Move].Type));
        }
    }
    private void DrawWildParty(CampaignWildBattle b) => DrawCaptureParty(b, true, true);
    private void DrawWildLearning(CampaignWildBattle b)
    {
        var moves = WildRewardRecipient(b).Pokemon.Moves;
        if (_wildMenu == "learn-intro")
        {
            var message = WildMessageSample;
            if (NativeWildMessageEnabled) DrawNativeBattleText(_nativeWildMessage, LabBattleUiSkin.BattleMessageText, _battleWhite, true, _wildElapsed);
            else { WildLines(message.Text); DrawTextCursor(_battleWhite, LabBattleUiSkin.BattleMessageText, message.Text.Split('\n'), 16, _wildElapsed, true); }
            return;
        }
        if (_wildMenu == "learn-moves")
        {
            DrawMoveSelectionSummary("wild:" + b.Id, WildRewardRecipient(b), b.PendingMoves[0].Move, _wildCursor);
            return;
        }
        WildLines(_wildLearningSlot < 0 ? string.Join('\n', WildSourcePages("sText_StopLearningMove", ("B_BUFF2", MoveName(b.PendingMoves[0].Move))))
            : _summaryText.Get("prompt.learning-replace", ("forgottenMove", MoveName(moves[_wildLearningSlot].Move)),
                ("newMove", MoveName(b.PendingMoves[0].Move))));
        _battleSkin.DrawStandardFrame(this, new(184, 64, 56, 48));
        SourceText(_sourceNormal, new(200, 72), InventorySourceText("gText_Yes")); SourceText(_sourceNormal, new(200, 88), InventorySourceText("gText_No"));
        SourceText(_sourceNormal, new(188, 72 + _wildCursor * 16), "▶");
    }
    private void DrawWildStats(CampaignWildBattle b, WildRewardLevelEvent level)
    {
        WildLines(string.Join('\n', WildSourcePages("sText_PkmnGrewToLv", ("B_BUFF1", _catalog.PokemonDisplayName(WildRewardRecipient(b))), ("B_BUFF2", level.After.ToString()))));
        _battleSkin.DrawStandardFrame(this, new(144, 56, 96, 104));
        var stats = new[] { ("gText_LevelUp_MaxHP", level.StatsBefore.MaxHp, level.StatsAfter.MaxHp), ("gText_LevelUp_Attack", level.StatsBefore.Attack, level.StatsAfter.Attack), ("gText_LevelUp_Defense", level.StatsBefore.Defense, level.StatsAfter.Defense), ("gText_LevelUp_SpAtk", level.StatsBefore.SpecialAttack, level.StatsAfter.SpecialAttack), ("gText_LevelUp_SpDef", level.StatsBefore.SpecialDefense, level.StatsAfter.SpecialDefense), ("gText_LevelUp_Speed", level.StatsBefore.Speed, level.StatsAfter.Speed) };
        for (int i = 0; i < stats.Length; i++)
        {
            var (symbol, before, after) = stats[i]; DrawStatLabel(new(152, 64 + i * 15), _battleText[symbol]);
            if (_wildStatsTotals) SourceText(_battleMenuFont, new(208 + 6 * (4 - after.ToString().Length), 64 + i * 15), after.ToString());
            else { int difference = after - before; DrawStatLabel(new(208, 64 + i * 15), _battleText[difference >= 0 ? "gText_LevelUp_Plus" : "gText_LevelUp_Minus"]); SourceText(_battleMenuFont, new(208 + (Math.Abs(difference) < 10 ? 12 : 6), 64 + i * 15), " " + Math.Abs(difference)); }
        }
    }
    private long WildDisplayedExperience(PokemonInstance mon, WildBattleSide side)
    {
        var b = PresentedWildBattle!;
        // Bench recipients have their own reward pages, but never drive the active HUD.
        return side == WildBattleSide.Player && b.RewardPartyIndex == b.ActivePartyIndex &&
            b.Phase == CampaignBattlePhase.Results && b.PageIndex < b.Pages.Length &&
            b.Pages[b.PageIndex].Reward is WildRewardExperienceEvent xp
                ? (long)Math.Round(Mathf.Lerp(xp.Before, xp.After, (float)Math.Clamp(_wildElapsed / .6, 0, 1)))
                : mon.Experience;
    }
    private void DrawWildHealth(PokemonInstance mon, WildBattleSide side, int hp)
    {
        bool player = side == WildBattleSide.Player;
        var growth = _catalog.PokemonContent!.Catalog.Species[mon.Species].GrowthRate;
        uint minimum = GrowthRules.ExperienceAtLevel(growth, mon.Level), maximum = GrowthRules.ExperienceAtLevel(growth, Math.Min(100, mon.Level + 1));
        long experience = WildDisplayedExperience(mon, side);
        int bar = maximum == minimum ? 0 : (int)Math.Clamp((experience - minimum) * 64 / (maximum - minimum), 0, 64);
        _battleSkin.DrawHealthbox(this, player, hp, mon.Stats.MaxHp, bar,
            showHpLabel: ForestHealthShowsHpLabel(PresentedWildBattle!, side));
        var name = player ? LabBattleUiSkin.PlayerName : LabBattleUiSkin.OpponentName;
        var levelOrigin = (player ? LabBattleUiSkin.PlayerLevelWindow : LabBattleUiSkin.OpponentLevelWindow) + new Vector2(5 * (3 - mon.Level.ToString().Length), 0);
        bool showGender = mon.Gender != PokemonGender.Genderless && (player || TowerOpponentAppearance()?.ShowGender != false);
        DrawHealthName(_healthFont, showGender ? mon.Gender == PokemonGender.Female ? _healthFemale : _healthMale : null,
            name, WildName(side), mon.Gender, levelOrigin.X);
        SourceText(_healthFont, levelOrigin, _healthFont.LevelPrefix + mon.Level);
        if (player) { SourceText(_healthFont, LabBattleUiSkin.PlayerCurrentHpText, hp.ToString().PadLeft(3) + "/"); SourceText(_healthFont, LabBattleUiSkin.PlayerMaximumHpText, mon.Stats.MaxHp.ToString().PadLeft(3)); }
        DrawForestHealthStatus(PresentedWildBattle!, side);
    }
}
