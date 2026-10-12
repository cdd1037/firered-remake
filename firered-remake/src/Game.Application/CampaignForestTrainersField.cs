using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.WildBattle;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Like the script-preflight guard, this only selects the atomic entry path. It is never
    // saved and never represents a partially committed approach.
    private bool _forestTrainerApproaching;
    private static string ForestTrainerFacingType(Direction direction) => "MOVEMENT_TYPE_FACE_" +
        (direction == Direction.North ? "UP" : direction == Direction.South ? "DOWN" : direction == Direction.East ? "RIGHT" : "LEFT");

    private CampaignState CheckForestTrainerSight(CampaignState state)
    {
        if (!_content.ForestTrainersEnabled || state.World.Player.MapId.Value != "map.viridian-forest" && !(_content.PewterEnabled && state.World.Player.MapId.Value == "map.pewter-gym") && !(_content.VermilionGymEnabled && state.World.Player.MapId.Value == "map.vermilion-city-gym") && !(_content.Route11Enabled && state.World.Player.MapId.Value == "map.route-11") && !(_content.PokemonTowerEnabled && TowerMap(state.World.Player.MapId.Value)) && !(_content.RocketHideoutEnabled && (state.World.Player.MapId.Value.StartsWith("map.rocket-hideout-",StringComparison.Ordinal)||state.World.Player.MapId.Value=="map.celadon-city-game-corner")) && !(_content.SilphEnabled && state.World.Player.MapId.Value.StartsWith("map.silph-co-",StringComparison.Ordinal)) && !(_content.VictoryRoadEnabled && Game.Domain.Encounters.VictoryRoadEncounterTables.IsCave(state.World.Player.MapId)) && !(_content.ViridianGymEnabled && state.World.Player.MapId.Value=="map.viridian-city-gym") && !(_content.FirstSeviiEnabled && state.World.Player.MapId.Value=="map.three-island-bond-bridge") && !(_content.CinnabarEnabled && (state.World.Player.MapId.Value is "map.route-21-north" or "map.route-21-south" or "map.cinnabar-island-gym" ||state.World.Player.MapId.Value.StartsWith("map.pokemon-mansion-",StringComparison.Ordinal))) && !(_content.SaffronGymEnabled && state.World.Player.MapId.Value=="map.saffron-city-gym") && !(_content.FuchsiaGymEnabled && state.World.Player.MapId.Value=="map.fuchsia-city-gym") && !(_content.CeladonGymEnabled && state.World.Player.MapId.Value=="map.celadon-city-gym") && !(_content.CeladonApproachEnabled && state.World.Player.MapId.Value=="map.route-8") && !(_content.FuchsiaRouteEnabled && state.World.Player.MapId.Value is "map.route-12" or "map.route-13" or "map.route-14" or "map.route-15") && !(_content.RockTunnelEnabled && state.World.Player.MapId.Value is "map.route-9" or "map.route-10" or "map.rock-tunnel-1f" or "map.rock-tunnel-b1f") && !(_content.Route3Enabled && state.World.Player.MapId.Value == "map.route-3") && !(_content.MtMoonEnabled && state.World.Player.MapId.Value.StartsWith("map.mt-moon-",StringComparison.Ordinal)) && !(_content.CeruleanEnabled&&state.World.Player.MapId.Value=="map.cerulean-city-gym") && !(_content.BillEnabled&&state.World.Player.MapId.Value is "map.route-24" or "map.route-25") && !(_content.VermilionEnabled&&state.World.Player.MapId.Value=="map.route-6") && !(_content.SsAnneEnabled&&state.World.Player.MapId.Value.StartsWith("map.ssanne-",StringComparison.Ordinal)) ||
            !state.Script.Completed || state.Script.Wait is not null || state.Dialogue is not null ||
            state.DoubleBattle is not null || state.ForestTrainerBattle is not null || state.WildBattle is not null || state.FieldPoison is not null) return state;
        var definitions = _content.MapDefinition(state.World.Player.MapId).Actors.ToDictionary(a => a.LocalId);
        // Original CheckForTrainersWantingBattle iterates the 16 physical object slots.
        var order = state.ObjectSlots is { } slots ? slots.Where(s => s.Key != 0).Select(s => s.Value)
            : state.Actors.Values.OrderBy(a => a.SourceLocalId).Select(a => a.LocalId);
        foreach (string id in order)
        {
            var actor = state.Actors[id]; var definition = definitions[id];
            if (!actor.Active || definition.TrainerType != "TRAINER_TYPE_NORMAL") continue;
            // The preset owner is separate from the single-trainer registry. Earlier
            // profiles leave these actors visible and report their original boundary;
            // this successor admits their shared roster only with two usable members.
            bool twins=IsTwinsScript(actor.ScriptId);
            if(twins&&(!_content.PresetDoublesEnabled||state.Party.Count(p=>!p.IsEgg&&p.Pokemon.CurrentHp>0)<2))continue;
            if(_content.FuchsiaRouteTrainers?.Battles.TryGetValue(actor.ScriptId,out var fuchsiaEntry)==true&&fuchsiaEntry.Mode==4)continue;
            var battle = OrdinaryTrainerScripts.SingleOrDefault(b => b.ScriptEntry == actor.ScriptId);
            if (battle is null&&!twins) throw new InvalidDataException("forest_trainer_actor_definition_missing");
            if (state.BattledTrainers.Contains(twins?DoubleTrainerId(actor.ScriptId).Value:battle!.Trainer.Value)) continue;
            int distance = ForestTrainerApproachDistance(state, actor, definition.TrainerSight);
            if (distance == 0) continue;
            if(twins)_=PrepareDoubleBattle(state,actor.ScriptId);else _ = PrepareForestTrainerBattle(state, battle!.Trainer);
            var path = ImmutableArray.CreateBuilder<TilePosition>();
            var actions = ImmutableArray.CreateBuilder<string>(); actions.Add("emote_exclamation_mark");
            var (dx, dy) = Delta(actor.Facing);
            var position = new TilePosition(state.World.Player.MapId, actor.X, actor.Y, actor.Elevation, actor.Facing);
            path.Add(position);
            for (int i = 1; i < distance; i++)
            {
                int x = position.X + dx, y = position.Y + dy;
                int elevation = _content.ConnectedFieldMaps![position.MapId].CellAt(x, y)!.Elevation;
                position = position with { X = x, Y = y, Elevation = elevation is 0 or 15 ? position.Elevation : elevation };
                path.Add(position); actions.Add("walk_normal_" + (actor.Facing == Direction.North ? "up" : actor.Facing == Direction.South ? "down" : actor.Facing == Direction.East ? "right" : "left"));
            }
            actions.Add("step_end");
            // PrepareToEngage overrides current template coordinates. FireRed's commented-out
            // player turn is deliberately not enabled: approach leaves player facing unchanged.
            var approached = actor with { X = position.X, Y = position.Y, Elevation = position.Elevation,
                TemplateX = position.X, TemplateY = position.Y, MovementType = ForestTrainerFacingType(actor.Facing) };
            var next = SelectInteractedObject(state, id) with { Actors = state.Actors.SetItem(id, approached),
                LastTalked = id, Motions = state.Motions.Add(new(id, path.ToImmutable(), actions.ToImmutable())),
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_LAST_TALKED", actor.SourceLocalId) } };
            _forestTrainerApproaching = true;
            try { return Pump(StartStory(next, actor.ScriptId)); }
            finally { _forestTrainerApproaching = false; }
        }
        return state;
    }

    private int ForestTrainerApproachDistance(CampaignState state, CampaignActor actor, int sight)
    {
        var player = state.World.Player; var (dx, dy) = Delta(actor.Facing);
        int distance = actor.Facing switch
        {
            Direction.North when actor.X == player.X && player.Y < actor.Y => actor.Y - player.Y,
            Direction.South when actor.X == player.X && player.Y > actor.Y => player.Y - actor.Y,
            Direction.West when actor.Y == player.Y && player.X < actor.X => actor.X - player.X,
            Direction.East when actor.Y == player.Y && player.X > actor.X => player.X - actor.X,
            _ => 0
        };
        if (distance == 0 || distance > sight || actor.Facing == Direction.South && sight > 3 &&
            (state.ObjectSlots?.Count ?? state.Actors.Values.Count(a => a.Active) + 1) >= 16) return 0;
        var map = (_content.RocketHideoutEnabled?EditedFieldMaps(state):_content.ConnectedFieldMaps!)[player.MapId]; var origin = map.CellAt(actor.X, actor.Y);
        if (origin is null || BlocksForestTrainerEdge(origin.SourceBehavior, actor.Facing)) return 0;
        for (int step = 0; step <= distance; step++)
        {
            int x = actor.X + dx * step, y = actor.Y + dy * step; var cell = map.CellAt(x, y);
            if (cell is null || cell.Collision != 0 || BlocksForestTrainerEdge(cell.SourceBehavior, Opposite(actor.Facing)) ||
                actor.Elevation != 0 && cell.Elevation is not (0 or 15) && cell.Elevation != actor.Elevation) return 0;
            if (state.Actors.Values.Any(other => other.Active && other.LocalId != actor.LocalId && other.X == x && other.Y == y &&
                (actor.Elevation == 0 || other.Elevation == 0 || actor.Elevation == other.Elevation))) return 0;
        }
        return actor.Elevation == 0 || player.Elevation == 0 || actor.Elevation == player.Elevation ? distance : 0;
    }

    private static bool BlocksForestTrainerEdge(int behavior, Direction edge) => edge switch
    {
        Direction.East => behavior is 0x30 or 0x34 or 0x36,
        Direction.West => behavior is 0x31 or 0x35 or 0x37,
        Direction.North => behavior is 0x32 or 0x34 or 0x35,
        _ => behavior is 0x33 or 0x36 or 0x37
    };

    private CampaignState ReleaseForestTrainerPostDialogue(CampaignState before, CampaignState next)
    {
        if (_content.ForestTrainersEnabled && before.ForestTrainerBattle is null &&
            before.Dialogue is { CloseOnCompletion: true } dialogue && dialogue.Page + 1 == dialogue.Pages.Length &&
            OrdinaryTrainerScripts.Any(b => b.PostBattleText == dialogue.TextSymbol && before.BattledTrainers.Contains(b.Trainer.Value)))
            return next with { Script = next.Script with { Locked = false } };
        return next;
    }

    private string? ValidateForestTrainerHistory(CampaignState state)
    {
        if (!_content.ForestTrainersEnabled)
            return state.ForestTrainerHistory is not null || state.ForestTrainerBattle is not null ? "save_forest_trainer_profile_missing" : null;
        var history = state.ForestTrainerHistory ?? [];
        if (history.IsDefault) return "save_forest_trainer_history_invalid";
        if(ValidateFirstSeviiTrainerHistory(state) is {} firstSeviiError)return firstSeviiError;
        var winners = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < history.Length; i++)
        {
            var h = history[i];
            if (h is null || h.Id != "forest-trainer-battle:" + (i + 1) || h.TrainerId is null ||
                !OrdinaryTrainerIds.Contains(new TrainerId(h.TrainerId)) ||
                h.Outcome is not (WildBattleOutcome.Won or WildBattleOutcome.Lost or WildBattleOutcome.Draw) ||
                h.ExperienceGained < 0 || h.MoneyGained < 0 || h.MoneyMultiplier is not (null or 2) || h.MoneyMultiplier is not null && !_content.PresetDoublesEnabled || h.OpponentsSent.IsDefaultOrEmpty ||
                h.OpponentsSent[0] != 0 || h.OpponentsSent.Length > OrdinaryTrainer(new TrainerId(h.TrainerId)).Party.Length ||
                h.OpponentsSent.Distinct().Count() != h.OpponentsSent.Length ||
                h.OpponentsSent.Any(n => n < 0 || n >= OrdinaryTrainer(new TrainerId(h.TrainerId)).Party.Length) || winners.Contains(h.TrainerId)&&!IsFirstSeviiBikerHistory(h))
                return "save_forest_trainer_history_invalid";
            var trainer=OrdinaryTrainer(new TrainerId(h.TrainerId));
            if (h.OpponentsSent.Any(n => !state.SeenSpecies.Contains(trainer.Party[n].SourceSpeciesId)))
                return "save_forest_trainer_seen_missing";
            int maximumExp = trainer.Party.Sum(p => Math.Max(1, (BattlePokemon.Species.GetValueOrDefault(p.Species)??(IsVictoryRoadTrainer(new TrainerId(h.TrainerId))?_content.VictoryRoadOpponents?.Catalog.Species.GetValueOrDefault(p.Species):null)??(IsLeagueRivalTrainer(new TrainerId(h.TrainerId))||IsViridianGymTrainer(new TrainerId(h.TrainerId))||IsFirstSeviiTrainer(new TrainerId(h.TrainerId))||IsCinnabarTrainer(new TrainerId(h.TrainerId))?_content.CinnabarOpponents?.Catalog.Species.GetValueOrDefault(p.Species):null)??(IsSaffronGymTrainer(new TrainerId(h.TrainerId))?_content.SaffronGymOpponents?.Catalog.Species.GetValueOrDefault(p.Species):null)??_content.SilphOpponents?.Catalog.Species.GetValueOrDefault(p.Species)??throw new InvalidDataException("trainer_reward_species")).ExperienceYield * p.Level / 7) * 150 / 100);
            if (h.ExperienceGained > maximumExp || h.Outcome == WildBattleOutcome.Won &&
                (h.OpponentsSent.Length != trainer.Party.Length || h.MoneyGained > 4 * trainer.Party[^1].Level * trainer.BaseMoney * (h.MoneyMultiplier ?? 1)) ||
                h.Outcome != WildBattleOutcome.Won && h.MoneyGained != 0)
                return "save_forest_trainer_reward_invalid";
            if (h.Outcome == WildBattleOutcome.Won) winners.Add(h.TrainerId);
        }
        if(_content.PewterEnabled&&winners.Contains(PewterTrainerPartyFactory.Brock.Value)&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"))
            winners.Add(PewterTrainerPartyFactory.Liam.Value);
        if(_content.CeruleanEnabled&&winners.Contains(CeruleanTrainerPartyFactory.Misty.Value)&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE02_GET"))
        {winners.Add(CeruleanTrainerPartyFactory.Diana.Value);winners.Add(CeruleanTrainerPartyFactory.Luis.Value);}
        if(_content.VermilionGymEnabled&&winners.Contains(VermilionGymTrainerPartyFactory.LtSurge.Value)&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"))
            foreach(var id in VermilionGymTrainerPartyFactory.SupportedTrainers)winners.Add(id.Value);
        if(_content.CeladonGymEnabled&&winners.Contains(CeladonGymTrainerPartyFactory.Erika.Value)&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE04_GET"))
            foreach(var id in CeladonGymTrainerPartyFactory.SupportedTrainers)winners.Add(id.Value);
        if(_content.FuchsiaGymEnabled&&winners.Contains(FuchsiaGymTrainerPartyFactory.Koga.Value)&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"))
            foreach(var id in FuchsiaGymTrainerPartyFactory.SupportedTrainers)winners.Add(id.Value);
        if(_content.SaffronGymEnabled&&winners.Contains(SaffronGymTrainerPartyFactory.Sabrina.Value)&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE06_GET"))
            foreach(var id in SaffronGymTrainerPartyFactory.SupportedTrainers)winners.Add(id.Value);
        if(_content.CinnabarEnabled&&winners.Contains(CinnabarTrainerPartyFactory.Blaine.Value)&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"))
            foreach(var symbol in CinnabarGymTrainerSymbols)winners.Add("pret:"+symbol);
        if(_content.ViridianGymEnabled&&winners.Contains(ViridianGymTrainerPartyFactory.Giovanni.Value)&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE08_GET"))
            foreach(var id in ViridianGymTrainerPartyFactory.GymTrainers)winners.Add(id.Value);
        var recorded = state.BattledTrainers.Where(id => OrdinaryTrainerIds.Contains(new TrainerId(id)));
        return winners.SetEquals(recorded) ? null : "save_forest_trainer_flag_mismatch";
    }
}
