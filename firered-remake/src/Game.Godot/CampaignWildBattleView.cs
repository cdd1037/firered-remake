using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Capture;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
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
        if(_catalog.PokemonTowerEnabled&&PokemonTowerPresentationModel.UsesTowerTerrain(mapId.Value))return _towerTerrain??throw new InvalidDataException("Tower terrain missing.");
        if (_catalog.RocketHideoutRegion?.NewMaps.ContainsKey(mapId)==true&&_catalog.SourceMaps[mapId].Events.GetProperty("battle_scene").GetString()=="MAP_BATTLE_SCENE_INDOOR_2")
            return _rocketIndoorTerrain??throw new InvalidDataException("Rocket Indoor2 terrain was not loaded.");
        if (_catalog.FuchsiaRouteRegion?.NewMaps.ContainsKey(mapId) == true || _catalog.RocketHideoutRegion?.NewMaps.ContainsKey(mapId) == true || _catalog.CeladonApproachRegion?.NewMaps.ContainsKey(mapId) == true || _catalog.RockTunnelRegion?.NewMaps.ContainsKey(mapId) == true || _catalog.Route11Region?.NewMaps.ContainsKey(mapId) == true)
        {
            if (mapId.Value is "map.rock-tunnel-1f" or "map.rock-tunnel-b1f" or "map.digletts-cave-south-entrance" or "map.digletts-cave-b1f" or "map.digletts-cave-north-entrance")
                return _mtMoonTerrain ?? throw new InvalidDataException("Diglett cave terrain was not loaded.");
            // The transaction commits terrain on entry. Rendering does not reclassify it from the player's later tile.
            if (PresentedWildBattle is {} battle)
                return battle.State.Terrain switch
                {
                    WildBattleTerrain.Grass => _wildTerrain,
                    WildBattleTerrain.Plain => _cityBattleScenes["plain"],
                    WildBattleTerrain.Sand => _cityBattleScenes["sand"],
                    WildBattleTerrain.Building => _cityBattleScenes["building"],
                    _ => throw new InvalidDataException("Unsupported committed Route11 terrain.")
                };
        }
        if (_catalog.FuchsiaGymEnabled && mapId.Value == "map.fuchsia-city-gym" ||
            _catalog.CeladonGymEnabled && mapId.Value == "map.celadon-city-gym" ||
            _catalog.CeruleanEnabled && PretGymBattleTerrain.AppliesToMap(mapId))
        {
            bool leader = _session.State.ForestTrainerBattle?.Trainer.Value is
                "pret:TRAINER_LEADER_BROCK" or "pret:TRAINER_LEADER_MISTY" or "pret:TRAINER_LEADER_ERIKA" or "pret:TRAINER_LEADER_KOGA";
            return (leader ? _leaderTerrain : _gymTerrain) ?? throw new InvalidDataException("Gym terrain was not loaded.");
        }
        if (_catalog.CityServicesEnabled && _cityBattleTerrain is not null && _session.State.World.Player is var player && player.MapId == mapId)
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
    private Texture2D CurrentWildBattleTerrain => WildBattleTerrainForMap(_session.State.World.Player.MapId);
    private string? _wildPresentationKey, _wildNotice, _wildNoticeReturn;
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
        var terrain=System.IO.Path.Combine(_root,"content/generated/route1-battle-terrain/background-scene.png");
        if(Convert.ToHexStringLower(SHA256.HashData(System.IO.File.ReadAllBytes(terrain)))!="1517c01c0754425bf4f69b09ce8a1998a585e3d3ace47b2fc4e1c58e6eb77c9a")throw new InvalidDataException("Wild terrain checksum mismatch.");
        _wildTerrain=Texture(terrain);
        if(_wildTerrain.GetWidth()!=240||_wildTerrain.GetHeight()!=112)throw new InvalidDataException("Wild terrain size invalid.");
        if (_catalog.MtMoonRegion is not null)
        {
            var cave = PretMtMoonBattleTerrain.Load(_root);
            _mtMoonTerrain = SceneTextures.Texture(cave.SceneTexturePath, expectedSha256: PretMtMoonBattleTerrain.SceneSha256);
            if (_mtMoonTerrain.GetWidth() != 240 || _mtMoonTerrain.GetHeight() != 112)
                throw new InvalidDataException("MtMoon cave terrain size invalid.");
        }
        if (_catalog.CeruleanEnabled)
        {
            var gym = PretGymBattleTerrain.Load(_root);
            _gymTerrain = SceneTextures.Texture(gym.GymSceneTexturePath, expectedSha256: PretGymBattleTerrain.GymSceneSha256);
            // The source palettes differ in an unused bank. Share the authenticated
            // settled texture while preserving the distinct source selection above.
            _leaderTerrain = gym.GymSceneRgbaSha256 == gym.LeaderSceneRgbaSha256 ? _gymTerrain
                : SceneTextures.Texture(gym.LeaderSceneTexturePath, expectedSha256: PretGymBattleTerrain.LeaderSceneSha256);
            if (_gymTerrain.GetWidth() != 240 || _gymTerrain.GetHeight() != 112 || _leaderTerrain.GetWidth() != 240 || _leaderTerrain.GetHeight() != 112)
                throw new InvalidDataException("Gym terrain size invalid.");
        }
        if (_catalog.CityServicesEnabled)
        {
            _cityBattleTerrain = PretCityBattleTerrain.Load(_root);
            foreach (var scene in new[]{_cityBattleTerrain.PlainScene,_cityBattleTerrain.SandScene,_cityBattleTerrain.BuildingScene})
            {
                var texture = scene.Sha256 == PretGymBattleTerrain.GymSceneSha256 && _gymTerrain is not null
                    ? _gymTerrain : SceneTextures.Texture(scene.TexturePath,expectedSha256:scene.Sha256);
                if (texture.GetWidth()!=240 || texture.GetHeight()!=112)throw new InvalidDataException("City battle terrain size invalid.");
                _cityBattleScenes.Add(scene.Terrain,texture);
            }
        }
        if (_battleArt.Count != 12) throw new InvalidDataException("Load the lab battle presentation before wild battle art.");
        var folder = System.IO.Path.Combine(_root, "content/generated/route1-battle-art");
        var bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder, "route1-battle-art.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != "5fedaf6c584e4e76e1e8b248fba83a30c5d9ff5aa661947a38e0c640ee63691d")
            throw new InvalidDataException("Wild battle art catalog checksum mismatch.");
        using var json = JsonDocument.Parse(bytes);
        var sprites = json.RootElement.GetProperty("sprites");
        if (sprites.GetArrayLength() != 4) throw new InvalidDataException("Wild battle sprite set incomplete.");
        foreach (var sprite in sprites.EnumerateArray())
        {
            var file = sprite.GetProperty("texturePath").GetString()!;
            var species = sprite.GetProperty("sourceSpeciesId").GetInt32();
            var side = sprite.GetProperty("side").GetString()!;
            var variant = sprite.GetProperty("paletteVariant").GetString()!;
            if (System.IO.Path.GetFileName(file) != file || species is not (16 or 19) || side != "front" || variant is not ("normal" or "shiny"))
                throw new InvalidDataException("Wild battle art identity invalid.");
            var path = System.IO.Path.Combine(folder, file);
            if (Convert.ToHexStringLower(SHA256.HashData(System.IO.File.ReadAllBytes(path))) != sprite.GetProperty("sha256").GetString())
                throw new InvalidDataException("Wild battle art bytes differ.");
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

    private bool WildWithdrawing(CampaignWildBattle b) => b.Phase == CampaignBattlePhase.Events &&
        WildCurrentEvent(b) is WildBattlePlayerSwitchedEvent && !SourcePlayerBatonSwitch(b) && _wildPage == 0;
    private PokemonInstance WildDisplayPlayer(CampaignWildBattle b) => WildWithdrawing(b)
        ? ((WildBattlePlayerSwitchedEvent)WildCurrentEvent(b)!).Previous.Pokemon : b.DisplayPlayer;
    private PartyPokemon WildRewardRecipient(CampaignWildBattle b) => _session.State.Party[b.RewardPartyIndex];
    private string WildName(WildBattleSide side) => side == WildBattleSide.Player
        ? WildWithdrawing(PresentedWildBattle!) ? PresentedWildBattle!.LastSwitch!.Outgoing.Nickname : SsAnnePendingPlayerForceOut(PresentedWildBattle!) is {} forced ? _session.State.Party[forced.PreviousIndex].Nickname : PresentedWildBattle!.OwnedBefore.Nickname
        : TowerOpponentAppearance()?.Name ?? _catalog.PokemonContent!.SpeciesNames[PresentedWildBattle!.DisplayOpponent.SourceSpeciesId];
    private string WildPrefixedName(WildBattleSide side) => (side == WildBattleSide.Opponent ? _session.State.ForestTrainerBattle is not null ? "Foe " : "Wild " : "") + WildName(side);
    private static string WildStatName(WildBattleStat stat) => stat switch
    {
        WildBattleStat.Attack => "ATTACK", WildBattleStat.Defense => "DEFENSE", WildBattleStat.Accuracy => "accuracy",
        WildBattleStat.Speed => "SPEED",
        WildBattleStat.SpecialAttack => "SP. ATK", WildBattleStat.SpecialDefense => "SP. DEF",
        WildBattleStat.Evasion => "evasiveness",
        _ => throw new InvalidDataException("Unsupported wild battle stat presentation.")
    };
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
        return PretBattleText.Pages(_battleText, symbol, values);
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
        var key = $"{b.Id}:{b.Phase}:{b.State.EventSequence}:{b.EventIndex}:{b.PageIndex}:{b.PendingMoves.Length}:{b.Capture?.Stage}:{b.Replacement}:{b.ActivePartyIndex}:{b.RewardReceiverIndex}:{b.RewardPartyIndex}:{_session.State.ForestTrainerBattle?.Stage}:{_session.State.ForestTrainerBattle?.TrainerEventIndex}";
        if (_wildPresentationKey == key) return;
        _wildPresentationKey = key; _wildElapsed = 0; _wildPage = 0; _wildPages = []; _wildArrowPages.Clear();
        _wildNotice = null; _wildNoticeReturn = null; _wildStatsTotals = false; _wildMenu = "actions"; _wildCursor = 0;_wildMedicineItem=null;
        if (b.Capture is not null) return;
        if (b.Phase == CampaignBattlePhase.ChooseMove) { if(b.Replacement is null && !ForestTrainerShiftPending && SourceBattleForcedMoveSlot(b) is not null)SelectWildMoveMenu(b); SynchronizeForestTrainerChoice(); return; }
        var pages = ImmutableArray.CreateBuilder<string>();
        void Source(string symbol, params (string Key, string Value)[] values)
        {
            var added = WildSourcePages(symbol, values);
            if(_catalog.PokemonTowerEnabled&&symbol=="sText_TheGhostWas")added=added.Where(p=>!string.IsNullOrWhiteSpace(p)).ToImmutableArray();
            bool finalWait = _battleText[symbol].EndsWith("\\p", StringComparison.Ordinal) ||
                _battleText[symbol].EndsWith("\\l", StringComparison.Ordinal) || _battleText[symbol].Contains("{PAUSE_UNTIL_PRESS}", StringComparison.Ordinal);
            for (int i = 0; i < added.Length; i++) if (i + 1 < added.Length || finalWait) _wildArrowPages.Add(pages.Count + i);
            pages.AddRange(added);
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
                    string targetName=_session.State.Party.Single(p=>p.InstanceId==medicine.TargetInstanceId).Nickname;
                    string medicineText=medicine.HpAfter>medicine.HpBefore?"gText_PkmnHPRestoredByVar2":
                        WildBattleStatus.IsSleeping(medicine.StatusBefore)?"gText_PkmnWokeUp2":ForestPresentationStatus(medicine.StatusBefore) switch
                        {WildBattleStatus.Poison=>"gText_PkmnCuredOfPoison",WildBattleStatus.Burn=>"gText_PkmnBurnHealed",WildBattleStatus.Freeze=>"gText_PkmnThawedOut",WildBattleStatus.Paralysis=>"gText_PkmnCuredOfParalysis",_=>throw new InvalidDataException("Unsupported medicine result message.")};
                    Source(medicineText,("STR_VAR_1",targetName),("STR_VAR_2",(medicine.HpAfter-medicine.HpBefore).ToString()));break;
                case WildBattleRecoilEvent recoil:
                    Source("sText_PkmnHitWithRecoil", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(recoil.Actor))); break;
                case WildBattleCriticalHitEvent: Source("sText_CriticalHit"); break;
                case WildBattleStatChangedEvent stat:
                    Source("sText_DefendersStatFell", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat)), ("B_BUFF2", "fell!")); break;
                case WildBattleStatAtMinimumEvent stat:
                    Source("sText_StatsWontDecrease", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat))); break;
                case WildBattleAbilityPreventedEvent ability when ability.Ability == "ABILITY_KEEN_EYE":
                    Source("sText_PkmnsXPreventsYLoss", ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_SCR_ACTIVE_ABILITY", "KEEN EYE"), ("B_BUFF1", "accuracy")); break;
                case WildBattleFaintedEvent fainted:
                    Source("sText_TargetFainted", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(fainted.Side))); break;
                case WildBattleRunAttemptEvent run:
                    if (run.Escaped && run.EscapingItem is { } escapingItem)
                        Source("sText_PkmnFledUsingIts", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(WildBattleSide.Player)),
                            ("B_LAST_ITEM", escapingItem == "ITEM_SMOKE_BALL" ? "SMOKE BALL" : throw new InvalidDataException("Unadmitted escape item: " + escapingItem)));
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
                    { Source(SourceBatonSendOutText(b), ("B_BUFF1", b.OwnedBefore.Nickname)); break; }
                    var change = b.LastSwitch ?? throw new InvalidDataException("Voluntary switch presentation is missing.");
                    Source(change.WithdrawalTextSymbol, ("B_BUFF1", change.Outgoing.Nickname));
                    Source(change.SendOutTextSymbol, ("B_BUFF1", b.OwnedBefore.Nickname));
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
                    Source("sText_PkmnGainedEXP", ("B_BUFF1", WildRewardRecipient(b).Nickname), ("B_BUFF2", ""), ("B_BUFF3", xp.Gained.ToString())); break;
                case WildRewardLevelEvent level when page.Text.Length > 0:
                    Source("sText_PkmnGrewToLv", ("B_BUFF1", WildRewardRecipient(b).Nickname), ("B_BUFF2", level.After.ToString())); break;
                case WildRewardLevelEvent: break;
                case WildRewardMoveLearnedEvent learned:
                    Source("sText_PkmnLearnedMove", ("B_BUFF1", WildRewardRecipient(b).Nickname), ("B_BUFF2", MoveName(learned.Move))); break;
                default:
                    if (VermilionPayDayResultPage(b, Source) || ForestTrainerResultPage(b, Source)) break;
                    if (_session.State.ForestTrainerBattle is null && b.State.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw) Source("sText_PlayerWhiteout");
                    else if (_session.State.ForestTrainerBattle is not null)
                    {
                        var added = ForestTrainerPlainPages(page.Text).ToArray();
                        for (int i = 0; i + 1 < added.Length; i++) _wildArrowPages.Add(pages.Count + i);
                        pages.AddRange(added);
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
        if(PresentedWildBattle is {Phase:CampaignBattlePhase.Events} fluteBattle && ProcessPokemonTowerFluteBattle(fluteBattle))return;
        if (_wildNotice is null && PresentedWildBattle is { Phase: CampaignBattlePhase.Events } b &&
            _wildPages.IsEmpty && _wildElapsed >= .45 && (WildCurrentEvent(b) is
                WildBattleHpChangedEvent or WildBattleStatusChangedEvent { Reason: "faint" } or WildBattleAbilityActivatedEvent or WildBattleDisableChangedEvent or WildBattleRolloutChangedEvent or WildBattleInfatuationChangedEvent || SourceTrainerSilentEvent(b) || SnorlaxSilentEvent(b) || PokemonTowerSilentEvent(b) || CeladonGymSilentEvent(b) || RockTunnelSilentEvent(b) || Route11SilentEvent(b) || MtMoonSilentEvent(b) || VermilionSilentEvent(b))) Apply(new("ack"));
    }
    private string WildCommandError() => _lastCommandFault ? "The battle step could not\nbe completed." : _status switch
    {
        "hm_move_cannot_be_forgotten" => HmCannotForgetText,
        "pokemon_learning_effect_unsupported" => UnsupportedLearningText,
        "wild_battle_switch_rooted" or "wild_battle_switch_mean_look" or "celadon_gym_trainer_switch_rooted" or "wild_battle_switch_binding" or "pewter_trainer_switch_binding" => WildName(WildBattleSide.Player)+" can't be switched\nout!",
        "wild_battle_switch_arena_trap" or "route11_trainer_switch_arena_trap" => string.Join('\n',WildSourcePages("gText_PkmnsXPreventsSwitching",("B_BUFF1",WildName(WildBattleSide.Opponent)),("B_LAST_ABILITY","ARENA TRAP"))),
        "wild_battle_run_arena_trap" => string.Join('\n',WildSourcePages("sText_PreventsEscape",("B_SCR_ACTIVE_NAME_WITH_PREFIX",WildPrefixedName(WildBattleSide.Opponent)),("B_SCR_ACTIVE_ABILITY","ARENA TRAP"))),
        "wild_battle_run_rooted" or "wild_battle_run_binding" or "wild_battle_run_mean_look" => string.Join('\n',WildSourcePages("sText_CantEscape2")),
        "trainer_battle_cannot_run" => string.Join('\n', WildSourcePages("sText_NoRunningFromTrainers")),
        "trainer_battle_cannot_capture" => string.Join('\n', WildSourcePages("sText_DontBeAThief")),
        "wild_battle_player_move_pp_empty" => "This move has no PP left.",
        "wild_battle_player_move_disabled" => "That move is disabled.",
        "wild_battle_player_rollout_locked" => "The current move must\nfinish first.",
        "wild_battle_player_move_effect_unsupported" => "This move's effect is not\nimplemented yet.",
        "wild_battle_opponent_move_effect_unsupported" => "The wild move's effect is\nnot implemented yet.",
        "potion_no_effect" or "medicine_no_effect" => ItemText("gText_WontHaveEffect"),
        "medicine_not_in_bag" => "There are none left\nin the BAG.",
        "battle_medicine_target_identity" => "Choose the target again.",
        "potion_not_in_bag" => "There are no POTIONS\nin the BAG.",
        "capture_no_poke_ball" or "capture_ball_not_in_bag" or "ball_not_in_bag" or "poke_ball_not_in_bag" => "There are no POKé BALLS\nin the BAG.",
        "storage_full" => PcText("gText_BoxFull"),
        "storage_delivery_unsupported" => "Your party has six POKéMON.\nPC delivery is not available yet.",
        "capture_profile_unsupported" or "capture_not_enabled" or "capture_ball_unsupported" or "wild_capture_unsupported" => "This capture is not supported.\nNo BALL was used.",
        "replacement_member_invalid" or "wild_replacement_fainted" or "wild_replacement_invalid" or "wild_replacement_target_invalid" => "Choose a living POKéMON\nthat is not already in battle.",
        "nickname_length" => "Use a name of up to\n10 characters.",
        "nickname_character_unsupported" => "That character is not\navailable in this game.",
        _ => "That action is not available\nright now."
    };
    private bool ApplyWildChoice(CampaignCommand command)
    {
        var revision = _session.Revision; Apply(command);
        if (_session.Revision > revision) { _wildMenu = "actions"; _wildCursor = 0; return true; }
        _wildNotice = WildCommandError(); return false;
    }
    private void BeginWildMoveLearning()
    {
        var b = PresentedWildBattle!;
        if (b.PendingMoves.IsEmpty) return;
        if (MoveLearningUnavailable(b.PendingMoves[0].Move))
        {
            _wildPages = []; _wildPage = 0; _wildElapsed = 0; _wildMenu = "learn-moves";
            _wildCursor = WildRewardRecipient(b).Pokemon.Moves.Length; return;
        }
        var values = new[] { ("B_BUFF1", WildRewardRecipient(b).Nickname), ("B_BUFF2", MoveName(b.PendingMoves[0].Move)) };
        _wildPages = [.. WildSourcePages("sText_TryToLearnMove1", values), .. WildSourcePages("sText_TryToLearnMove2", values), .. WildSourcePages("sText_TryToLearnMove3", values)];
        _wildPage = 0; _wildElapsed = 0; _wildMenu = "learn-intro"; _wildCursor = 0;
    }
    private bool WildLearningInput(InputEventKey key)
    {
        var b = PresentedWildBattle!;
        if (_wildMenu == "learn-intro")
        {
            if (key.Keycode == Key.Enter && _wildElapsed > .1)
            {
                if (_wildPage + 1 < _wildPages.Length) { _wildPage++; _wildElapsed = 0; }
                else { _wildMenu = "learn-moves"; _wildCursor = 0; _wildPages = []; }
            }
            else if (key.Keycode == Key.Backspace) { _wildMenu = "learn-confirm"; _wildLearningSlot = -1; _wildCursor = 1; }
            return true;
        }
        if (_wildMenu == "learn-moves")
        {
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
            if (_wildLearningSlot >= 0 && MoveLearningUnavailable(b.PendingMoves[0].Move))
            { _wildMenu = "learn-moves"; _wildCursor = WildRewardRecipient(b).Pokemon.Moves.Length; return true; }
            if (ApplyWildChoice(new("wild_learn", MoveSlot: _wildLearningSlot)))
            {
                if (PresentedWildBattle is { PendingMoves.IsEmpty: false } next &&
                    (next.RewardReceiverIndex == b.RewardReceiverIndex || next.Pages.IsEmpty)) BeginWildMoveLearning();
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
                _wildNotice = null;
                if (_wildNoticeReturn is { } target) { _wildMenu = target; if(target is not ("medicine-target" or "medicine-move"))_wildCursor = 0; _wildNoticeReturn = null; }
            }
            return true;
        }
        if(b.Phase==CampaignBattlePhase.ChooseMove && b.Replacement is null && !ForestTrainerShiftPending && SourceBattleForcedMoveSlot(b) is {} forced)
        { SelectWildMoveMenu(b); if(key.Keycode==Key.Enter)ApplyWildChoice(new("battle_move",MoveSlot:forced));return true; }
        if (TryForestTrainerInput(key, b)) return true;
        if (TryCaptureWildInput(key, b)) return true;
        if (_wildMenu.StartsWith("learn-", StringComparison.Ordinal)) return WildLearningInput(key);
        if (b.Phase != CampaignBattlePhase.ChooseMove)
        {
            if (key.Keycode == Key.Enter && !FuchsiaTransformCuePending(b) && _wildElapsed > (b.Phase == CampaignBattlePhase.Events &&
                WildCurrentEvent(b) is WildBattleWeatherDamageEvent or WildBattleStatusDamageEvent or WildBattleBindingDamageEvent or WildBattleConfusionSelfHitEvent or WildBattleTowerHpEvent ? .45 : .1))
            {
                if (WildStatsPage(b) && !_wildStatsTotals) { _wildStatsTotals = true; _wildElapsed = 0; Write("wildBattleStatsPage", new { page = 2 }); }
                else if (_wildPage + 1 < _wildPages.Length) { _wildPage++; _wildElapsed = 0; Write("wildBattlePage", new { key = _wildPresentationKey, page = _wildPage }); }
                else if (b.Phase == CampaignBattlePhase.Results && b.PageIndex + 1 >= b.Pages.Length && !b.PendingMoves.IsEmpty) BeginWildMoveLearning();
                else if (!_wildPages.IsEmpty || b.Phase == CampaignBattlePhase.Results) AcknowledgeWildAttackText();
            }
            return true;
        }
        if (_wildMenu == "bag")
        {
            int entries = (_session.State.Inventory?.BagItems.Length ?? 0) + 1;
            if (key.Keycode == Key.Up) _wildCursor = Math.Max(0, _wildCursor - 1);
            if (key.Keycode == Key.Down) _wildCursor = Math.Min(entries - 1, _wildCursor + 1);
            if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _wildCursor == entries - 1) { _wildMenu = "actions"; _wildCursor = 0; }
            else if (key.Keycode == Key.Enter) { _wildMenu = "bag-actions"; _wildCursor = 0; }
            return true;
        }
        if (_wildMenu == "bag-actions")
        {
            if (key.Keycode is Key.Up or Key.Down) _wildCursor ^= 1;
            if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _wildCursor == 1) { _wildMenu = "bag"; _wildCursor = 0; }
            else if (key.Keycode == Key.Enter) { _wildMenu = "medicine-target"; _wildCursor = 0; }
            return true;
        }
        if (SingleMedicineInput(key,b)) return true;
        if (_wildMenu == "medicine-target")
        {
            if(_session.BattleMedicineEnabled&&_wildMedicineItem is {} medicine)
            {
                int cancel=_session.State.Party.Length;
                if(key.Keycode==Key.Up)_wildCursor=Math.Max(0,_wildCursor-1);
                if(key.Keycode==Key.Down)_wildCursor=Math.Min(cancel,_wildCursor+1);
                if(key.Keycode==Key.Backspace||key.Keycode==Key.Enter&&_wildCursor==cancel)
                {_wildMenu="bag";_wildCursor=_captureBagSelected;}
                else if(key.Keycode==Key.Enter)
                {
                    var selected=_session.State.Party[_wildCursor];
                    if(!ApplyWildChoice(new("battle_medicine",Text:medicine.Value,PartyIndex:_wildCursor){TargetInstanceId=selected.InstanceId}))
                        _wildNoticeReturn="medicine-target";
                }
                return true;
            }
            if (key.Keycode == Key.Backspace) { _wildMenu = "bag"; _wildCursor = 0; }
            else if (key.Keycode == Key.Enter && !ApplyWildChoice(new("battle_potion", PartyIndex: b.ActivePartyIndex))) _wildNoticeReturn = "bag";
            return true;
        }
        if (_wildMenu == "party")
        {
            if (!_catalog.PartyTrainingEnabled)
            { if (key.Keycode is Key.Backspace or Key.Enter) { _wildMenu = "actions"; _wildCursor = 0; } return true; }
            int cancel = _session.State.Party.Length;
            if (key.Keycode == Key.Up) _wildCursor = Math.Max(0, _wildCursor - 1);
            if (key.Keycode == Key.Down) _wildCursor = Math.Min(cancel, _wildCursor + 1);
            if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _wildCursor == cancel)
            { _wildMenu = "actions"; _wildCursor = 2; }
            else if (key.Keycode == Key.Enter)
            {
                var member = _session.State.Party[_wildCursor];
                string? error = _wildCursor == b.ActivePartyIndex ? "gText_PkmnAlreadyInBattle" :
                    member.IsEgg ? "gText_EggCantBattle" : member.Pokemon.CurrentHp == 0 ? "gText_PkmnHasNoEnergy" : null;
                if (error is not null) _wildNotice = string.Join('\n', WildSourcePages(error, ("STR_VAR_1", member.Nickname)));
                else ApplyWildChoice(new("battle_switch", PartyIndex: _wildCursor));
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
        if (DrawSingleMedicineMove(b)) return;
        if (DrawForestTrainerChoice(b)) return;
        if (DrawCaptureWildOverlay(b)) return;
        if (b.Phase == CampaignBattlePhase.ChooseMove && _wildMenu is "bag" or "bag-actions" && _session.State.Inventory is { } inventory)
        {
            DrawItemList(inventory, "bag", _wildMenu == "bag-actions" ? 0 : _wildCursor, _wildNotice);
            if (_wildMenu == "bag-actions") DrawItemActionPopup(false, [_itemSkin.Text("gOtherText_Use"), _itemSkin.Text("gFameCheckerText_Cancel")], _wildCursor);
            return;
        }
        DrawRect(new(0, 0, 240, 160), Colors.Black); _battleSkin.DrawBackground(this);DrawTexture(CurrentWildBattleTerrain,Vector2.Zero);
        if(SourceAnimationPresented)DrawTexture(_sourceMoveField!,Vector2.Zero);
        bool portrait = DrawForestTrainerPortrait(b);
        bool defeatedTrainer = DrawForestTrainerDefeatPortrait(b);
        foreach (var (side, mon) in new[] { (WildBattleSide.Opponent, b.DisplayOpponent), (WildBattleSide.Player, WildDisplayPlayer(b)) })
        {
            if (portrait || defeatedTrainer && side == WildBattleSide.Opponent || ForestTrainerHidePlayer(b, side) || PokemonTowerHidePlayer(b,side)) continue;
            if (side == WildBattleSide.Opponent && b.Phase == CampaignBattlePhase.Events &&
                b.Events.Take(b.EventIndex + 1).OfType<WildBattleCaptureResultEvent>().Any(c => c.Outcome == CaptureOutcome.Caught)) continue;
            bool faintShown = mon.CurrentHp == 0 && (b.Phase == CampaignBattlePhase.Results || b.Phase == CampaignBattlePhase.Events && b.Events.Take(b.EventIndex + 1).OfType<WildBattleFaintedEvent>().Any(f => f.Side == side));
            if (!faintShown)
            {
                var appearance=FuchsiaBattleAppearance(b,side,mon);
                var asset = _battleArt[(appearance.Species, side == WildBattleSide.Player ? "back" : "front", appearance.Shiny)];
                if(!SourceAnimationPresented && !DrawSourceSubstitute(side) && (side!=WildBattleSide.Opponent||!DrawPokemonTowerOpponent(b)))DrawTexture(asset.Texture, asset.Position);
            }
            int hp = WildDisplayedHp(b, mon, side);
            DrawWildHealth(mon, side, hp);
        }
        bool selecting = b.Phase == CampaignBattlePhase.ChooseMove && _wildNotice is null && _wildMenu is "actions" or "moves";
        if (selecting) _uiCorrections.ClearBattleMenu(this); else _battleSkin.DrawBattleMessage(this);
        if (_wildMenu is "party" or "medicine-target") DrawWildParty(b);
        if (_wildNotice is not null) { WildLines(_wildNotice); return; }
        if (_wildMenu.StartsWith("learn-", StringComparison.Ordinal)) { DrawWildLearning(b); return; }
        if (WildStatsPage(b)) { DrawWildStats(b, (WildRewardLevelEvent)b.Pages[b.PageIndex].Reward!); return; }
        if(_sourceStatContinuation is not null)return;
        if (!_wildPages.IsEmpty)
        {
            if(WildCurrentEvent(b) is not WildBattleTowerHpEvent {Effect:"curse-cost"} || _wildElapsed>=.4)WildLines(_wildPages[_wildPage]);
            NotePokemonTowerFluteBattle(b);
            if (_wildArrowPages.Contains(_wildPage)) DrawTextCursor(_battleWhite, LabBattleUiSkin.BattleMessageText, _wildPages[_wildPage].Split('\n'), 16, _wildElapsed, true);
            return;
        }
        if (b.Phase != CampaignBattlePhase.ChooseMove) return;
        if (_wildMenu == "medicine-target") WildLines("Use on which POKéMON?");
        else if (_wildMenu == "party") WildLines(_catalog.CaptureEnabled && !_catalog.PartyTrainingEnabled ? "Party view only. Switching during\na turn is not available yet." : "Choose a POKéMON.");
        else if (_wildMenu == "actions")
        {
            _battleSkin.DrawActionMenu(this); WildLines("What will\n" + b.OwnedBefore.Nickname + " do?");
            var labels = new[] { "FIGHT", "BAG", "POKéMON", "RUN" };
            for (int i = 0; i < labels.Length; i++) SourceText(_battleMenuFont, LabBattleUiSkin.ActionText(i), labels[i]);
            _uiCorrections.DrawSelectionCursor(this, false, _wildCursor);
        }
        else
        {
            _battleSkin.DrawMoveMenu(this);
            if (SourceBattleRequiresStruggle(b)) { SourceText(_battleMoveFont, LabBattleUiSkin.MoveText(0), "STRUGGLE"); _uiCorrections.DrawSelectionCursor(this, true, 0); return; }
            var moves = b.State.Player.Pokemon.Moves;
            for (int i = 0; i < 4; i++) SourceText(_battleMoveFont, LabBattleUiSkin.MoveText(i), i < moves.Length ? MoveName(moves[i].Move) : "-");
            _wildCursor = Math.Clamp(_wildCursor, 0, moves.Length - 1);
            _uiCorrections.DrawSelectionCursor(this, true, _wildCursor);
            var move = moves[_wildCursor]; int ppState = SourceUiCorrections.PpPaletteState(move.CurrentPp, move.MaximumPp);
            SourceText(_battlePpSmallFont[ppState], new(168, 122), "PP"); SourceText(_battlePpFont[ppState], new(202, 122), $"{move.CurrentPp,2}/{move.MaximumPp,2}");
            SourceText(_battleMoveFont, new(168, 138), "TYPE/");
            float width = _battleMoveFont.Font.GetStringSize(_battleMoveFont.PrepareText("TYPE/"), fontSize: _battleMoveFont.Size).X;
            SourceText(_battleMenuFont, new(168 + width, 138), _catalog.PokemonContent!.Catalog.Moves[move.Move].Type.Replace("TYPE_", "", StringComparison.Ordinal));
        }
    }
    private void DrawWildParty(CampaignWildBattle b)
    {
        if(_wildMenu=="medicine-target"&&_session.BattleMedicineEnabled){DrawCaptureParty(b,true,true);return;}
        if (_catalog.CaptureEnabled && _wildMenu == "party") { DrawCaptureParty(b, _catalog.PartyTrainingEnabled, _catalog.PartyTrainingEnabled); return; }
        // Functional one-member summary; the original full-screen party presentation remains a separate gate.
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 112)); var p = b.DisplayPlayer;
        SourceText(_sourceNormal, new(12, 8), $"{b.OwnedBefore.Nickname} {p.CurrentHp}/{p.Stats.MaxHp} HP");
        for (int i = 0; i < p.Moves.Length; i++) SourceText(_sourceNormal, new(12, 29 + i * 16), $"{MoveName(p.Moves[i].Move)} {p.Moves[i].CurrentPp}/{p.Moves[i].MaximumPp}");
    }
    private void DrawWildLearning(CampaignWildBattle b)
    {
        var moves = WildRewardRecipient(b).Pokemon.Moves;
        if (_wildMenu == "learn-intro") { WildLines(_wildPages[_wildPage]); DrawTextCursor(_battleWhite, LabBattleUiSkin.BattleMessageText, _wildPages[_wildPage].Split('\n'), 16, _wildElapsed, true); return; }
        if (_wildMenu == "learn-moves")
        {
            _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 112));
            bool unavailable = MoveLearningUnavailable(b.PendingMoves[0].Move);
            SourceText(_sourceNormal, new(16, 8), unavailable ? MoveName(b.PendingMoves[0].Move) : "Forget which move?");
            for (int i = 0; i <= moves.Length; i++) SourceText(unavailable && i < moves.Length ? _sourceInactiveCursor : _sourceNormal,
                new(24, 28 + i * 16), i == moves.Length ? unavailable ? "DECLINE" : "CANCEL" : MoveName(moves[i].Move));
            SourceText(_sourceNormal, new(12, 28 + _wildCursor * 16), "▶");
            WildLines(unavailable ? UnsupportedLearningText : "Learn " + MoveName(b.PendingMoves[0].Move) + "?"); return;
        }
        WildLines(_wildLearningSlot < 0 ? string.Join('\n', WildSourcePages("sText_StopLearningMove", ("B_BUFF2", MoveName(b.PendingMoves[0].Move))))
            : "Forget " + MoveName(moves[_wildLearningSlot].Move) + "\nand learn " + MoveName(b.PendingMoves[0].Move) + "?");
        _battleSkin.DrawStandardFrame(this, new(184, 64, 56, 48));
        SourceText(_sourceNormal, new(200, 72), "YES"); SourceText(_sourceNormal, new(200, 88), "NO");
        SourceText(_sourceNormal, new(188, 72 + _wildCursor * 16), "▶");
    }
    private void DrawWildStats(CampaignWildBattle b, WildRewardLevelEvent level)
    {
        WildLines(string.Join('\n', WildSourcePages("sText_PkmnGrewToLv", ("B_BUFF1", WildRewardRecipient(b).Nickname), ("B_BUFF2", level.After.ToString()))));
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
        SourceText(_healthFont, name, WildName(side));
        if (mon.Gender != PokemonGender.Genderless && (player || TowerOpponentAppearance()?.ShowGender != false))
        {
            float width = _healthFont.Font.GetStringSize(_healthFont.PrepareText(WildName(side)), fontSize: _healthFont.Size).X;
            SourceText(mon.Gender == PokemonGender.Female ? _healthFemale : _healthMale, name + new Vector2(width, 0), mon.Gender == PokemonGender.Female ? "♀" : "♂");
        }
        var levelOrigin = (player ? LabBattleUiSkin.PlayerLevelWindow : LabBattleUiSkin.OpponentLevelWindow) + new Vector2(5 * (3 - mon.Level.ToString().Length), 0);
        SourceText(_healthFont, levelOrigin, _healthFont.LevelPrefix + mon.Level);
        if (player) { SourceText(_healthFont, LabBattleUiSkin.PlayerCurrentHpText, hp.ToString().PadLeft(3) + "/"); SourceText(_healthFont, LabBattleUiSkin.PlayerMaximumHpText, mon.Stats.MaxHp.ToString().PadLeft(3)); }
        DrawForestHealthStatus(PresentedWildBattle!, side);
    }
}
