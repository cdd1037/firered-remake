using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.FirstBattle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public enum CampaignForestTrainerStage { Intro, Combat, Rewards, Finished }
public sealed record CampaignForestTrainerHistory(string Id, string TrainerId, WildBattleOutcome Outcome,
    int ExperienceGained, int MoneyGained, ImmutableArray<int> OpponentsSent)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? MoneyMultiplier { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? SourceScriptEntry { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? SourceScriptLine { get; init; }
}

/// <summary>Owned party, EXP and script continuation stay here; the deterministic multi-opponent
/// controller owns turn execution. Presentation deliberately shares the existing wild event types.</summary>
public sealed record CampaignForestTrainerBattle(string Id, TrainerId Trainer, string ActorId,
    ForestTrainerCombatState Combat, CampaignWildBattle Presentation, CampaignForestTrainerStage Stage,
    ImmutableArray<int> LevelsBeforeBattle, ImmutableArray<ForestTrainerCombatEvent> TrainerEvents,
    int TrainerEventIndex, int ExperienceGained, ImmutableArray<int> OpponentsSent)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? SourceScriptEntry { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? SourceScriptLine { get; init; }
    [JsonIgnore] public ForestTrainerCombatEvent? CurrentTrainerEvent => TrainerEventIndex < TrainerEvents.Length &&
        (Presentation.EventIndex >= Presentation.Events.Length ||
         TrainerEvents[TrainerEventIndex].Sequence < Presentation.Events[Presentation.EventIndex].Sequence)
        ? TrainerEvents[TrainerEventIndex] : null;
}

public sealed partial class CampaignSession
{
    private ForestTrainerCatalog ForestTrainers => _content.ForestTrainers ??
        throw new InvalidDataException("forest_trainer_content_missing");

    private ForestTrainerCombatResult PrepareForestTrainerBattle(CampaignState state, TrainerId trainer)
    {
        if (!_content.ForestTrainersEnabled || state.Party.Length is < 1 or > 6 || state.Battle is not null ||
            state.WildBattle is not null || state.ForestTrainerBattle is not null ||
            state.Script.QuestLogState != 0 || state.Party.All(p => p.IsEgg || p.Pokemon.CurrentHp == 0))
            throw new InvalidDataException("forest_trainer_entry_context");
        if (long.MaxValue - Revision < 4096 || long.MaxValue - state.Script.Revision < 4096 ||
            long.MaxValue - state.Script.EffectSequence < 4096 || ulong.MaxValue - state.World.Rng.Cursor < (1UL << 32))
            throw new InvalidDataException("forest_trainer_counter_headroom_required");
        if (!_content.FuchsiaGymEnabled && !state.Party.Any(p => !p.IsEgg && p.Pokemon.CurrentHp > 0 &&
            (p.Pokemon.Moves.All(m => m.CurrentPp == 0) || p.Pokemon.Moves.Any(m => m.CurrentPp > 0 && WildBattleRules.IsMoveSupported(m.Move, CampaignWildRulesVersion)))))
            throw new InvalidDataException("forest_trainer_no_supported_party_action");
        var definition = OrdinaryTrainerScript(trainer);
        // The next city chapter owns the rival's Fame Checker and Rocket TM28 continuations.
        // Reject before committing their first lock/battle until those complete reward paths exist.
        if(!_content.BillEnabled&&IsCeruleanTrainer(trainer)&&definition.Mode==3)throw new InvalidDataException("cerulean_north_events_not_implemented");
        if (definition.Mode != 0 && !(IsLeagueRivalTrainer(trainer) && definition.Mode==3) && !(IsViridianGymTrainer(trainer) && definition.Mode==1) && !(IsFirstSeviiTrainer(trainer) && definition.Mode==3) && !(IsCinnabarTrainer(trainer) && definition.Mode is 1 or 2 or 3) && !(IsSilphTrainer(trainer) && definition.Mode is 2 or 3) && !(IsPokemonTowerTrainer(trainer) && definition.Mode is 2 or 3) && !(IsRocketHideoutTrainer(trainer) && definition.Mode is 2 or 3) && !(IsSaffronGymTrainer(trainer) && definition.Mode == 1) && !(IsFuchsiaGymTrainer(trainer) && definition.Mode == 1) && !(IsCeladonGymTrainer(trainer) && definition.Mode == 1) && !(IsPewterTrainer(trainer) && definition.Mode == 1) && !(IsRoute22Trainer(trainer) && definition.Mode == 9) && !(IsMtMoonTrainer(trainer) && definition.Mode == 3) && !(IsCeruleanTrainer(trainer)&&definition.Mode is 1 or 3) && !(IsBillTrainer(trainer)&&definition.Mode==3) && !(IsSsAnneTrainer(trainer)&&definition.Mode==3)) throw new InvalidDataException("forest_trainer_mode_unsupported");
        if(!string.IsNullOrEmpty(definition.IntroText))_ = _content.TextPages(definition.IntroText);
        _ = _content.TextPages(definition.DefeatText);
        if(!string.IsNullOrEmpty(definition.PostBattleText))_ = _content.TextPages(definition.PostBattleText);
        // The source successor receives every genuine party member before entry.
        // Historical profiles keep their previous bounded admission checks.
        if(_content.FuchsiaGymEnabled)return PrepareFuchsiaSourceTrainerBattle(state,trainer);
        ForestTrainerCombatResult? first = null;
        IEnumerable<PartyPokemon> entryMembers=state.Party;
        foreach (var owned in entryMembers)
        {
            if (owned.IsEgg) throw new InvalidDataException("forest_trainer_eggs_unsupported");
            if (owned.Pokemon.OriginalTrainerId != state.TrainerId || owned.OriginalTrainerName != state.PlayerName ||
                owned.OriginalTrainerGender != state.PlayerGender)
                throw new InvalidDataException("forest_trainer_local_owner_required");
            var mon = owned.Pokemon.CurrentHp == 0 ? owned.Pokemon with { CurrentHp = 1 } : owned.Pokemon;
            var combatant=new WildBattleCombatant(mon,HeldItem:owned.HeldItem,Status:owned.Status){AttackBadgeBoost=_content.PewterEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),SpeedBadgeBoost=_content.VermilionGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),DefenseBadgeBoost=_content.FuchsiaGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET")};
            var probe = IsFuchsiaRouteTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.FuchsiaRouteTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state))
                : IsPokemonTowerTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.PokemonTowerTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsRocketHideoutTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.RocketHideoutTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsCeladonGymTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.CeladonGymTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsCeladonApproachTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.CeladonApproachTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsRockTunnelTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.RockTunnelTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsRoute11Trainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.Route11Trainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsVermilionGymTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.VermilionGymTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsSsAnneTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.SsAnneTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsVermilionTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.VermilionTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),ssAnneRules:_content.SsAnneEnabled,vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsBillTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.BillTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),terrain:CampaignBattleTerrain(state),vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsCeruleanTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.CeruleanTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),billRules:_content.BillEnabled,terrain:CampaignBattleTerrain(state),vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsMtMoonTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.MtMoonTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),ceruleanRules:_content.CeruleanEnabled,billRules:_content.BillEnabled,terrain:CampaignBattleTerrain(state),vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsRoute3Trainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.Route3Trainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),mtMoonRules:_content.MtMoonEnabled,ceruleanRules:_content.CeruleanEnabled,billRules:_content.BillEnabled,terrain:CampaignBattleTerrain(state),vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsRoute22Trainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.Route22Trainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),route3Rules:_content.Route3Enabled,mtMoonRules:_content.MtMoonEnabled,ceruleanRules:_content.CeruleanEnabled,billRules:_content.BillEnabled,terrain:CampaignBattleTerrain(state),vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : IsPewterTrainer(trainer)
                ? ForestTrainerCombatRules.Start(_content.PewterTrainers!,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),earlyStarterRules:_content.EarlyStarterEnabled,earlyPartyRules:_content.EarlyPartyEnabled,route22Rules:_content.Route22Enabled,route3Rules:_content.Route3Enabled,mtMoonRules:_content.MtMoonEnabled,ceruleanRules:_content.CeruleanEnabled,billRules:_content.BillEnabled,terrain:CampaignBattleTerrain(state),vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled)
                : ForestTrainerCombatRules.Start(ForestTrainers,BattlePokemon,trainer,combatant,state.World.Rng,battleStyle:EffectiveBattleStyle(state),pewterRules:_content.PewterEnabled,earlyStarterRules:_content.EarlyStarterEnabled,earlyPartyRules:_content.EarlyPartyEnabled,route22Rules:_content.Route22Enabled,route3Rules:_content.Route3Enabled,mtMoonRules:_content.MtMoonEnabled,ceruleanRules:_content.CeruleanEnabled,billRules:_content.BillEnabled,terrain:CampaignBattleTerrain(state),vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,towerRules:_content.PokemonTowerEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled);
            if (!probe.Succeeded) throw new InvalidDataException(probe.Fault);
            if (owned.Pokemon.CurrentHp > 0) first ??= probe;
        }
        return first??throw new InvalidDataException("forest_trainer_live_party_required");
    }

    private CampaignState BeginForestTrainerBattle(CampaignState state, StoryEffect effect)
    {
        bool noIntro=effect.Opcode=="trainerbattle_no_intro";
        if ((noIntro?effect.Args.Length!=2:effect.Args.Length is <3 or >5) || state.Script.Wait?.Kind != StoryWaitKind.Battle)
            throw new InvalidDataException("forest_trainer_source_wait_invalid");
        var trainer = new TrainerId("pret:" + effect.Args[0]);
        var definition = IsFirstSeviiTrainer(trainer)?OrdinaryTrainerScript(trainer,effect.Source.Symbol,effect.Source.Line):IsViridianGymTrainer(trainer)||IsCinnabarTrainer(trainer)?OrdinaryTrainerScript(trainer,effect.Source.Symbol):OrdinaryTrainerScript(trainer);
        string? battleActorId = state.LastTalked;
        if(noIntro)
        {
            if(!(IsLeagueRivalTrainer(trainer)||IsFirstSeviiTrainer(trainer)||IsCinnabarTrainer(trainer)||IsSilphTrainer(trainer)||IsPokemonTowerTrainer(trainer)||IsRocketHideoutTrainer(trainer)||IsMtMoonTrainer(trainer)||IsCeruleanTrainer(trainer)||IsBillTrainer(trainer)||IsSsAnneTrainer(trainer))||definition.Mode!=3||effect.Args[1]!=definition.DefeatText)throw new InvalidDataException("mtmoon_no_intro_context_invalid");
            // Quiz consoles have no talking actor. Source no-intro battle setup does not
            // call SetMapVarsToTrainer, so its stale VAR_LAST_TALKED is not the opponent.
            // Resolve only the authenticated Cinnabar trainer actor for presentation;
            // preserve the source object-selection globals through the quiz branch.
            var selected=IsCinnabarTrainer(trainer)
                ? state.Actors.Values.SingleOrDefault(a=>a.Active&&a.ScriptId==OrdinaryNoIntroActorScript(trainer))
                : state.Actors.Values.SingleOrDefault(a=>a.Active&&a.SourceLocalId==state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED"));
            if(selected is null)throw new InvalidDataException("mtmoon_no_intro_actor_missing");
            battleActorId=selected.LocalId;
            if(!IsCinnabarTrainer(trainer))state=SelectInteractedObject(state,selected.LocalId) with{LastTalked=selected.LocalId};
        }
        var expectedLine=IsLeagueRivalTrainer(trainer)?definition.Source.StartLine+1:IsFirstSeviiTrainer(trainer)?definition.Source.StartLine:IsViridianGymTrainer(trainer)||IsCinnabarTrainer(trainer)||IsSaffronGymTrainer(trainer)||IsSilphTrainer(trainer)||IsFuchsiaGymTrainer(trainer)||IsFuchsiaRouteTrainer(trainer)||IsPokemonTowerTrainer(trainer)||IsRocketHideoutTrainer(trainer)||IsCeladonGymTrainer(trainer)||IsCeladonApproachTrainer(trainer)||IsRockTunnelTrainer(trainer)||IsRoute11Trainer(trainer)||IsVermilionGymTrainer(trainer)||IsMtMoonTrainer(trainer)||IsCeruleanTrainer(trainer)||IsBillTrainer(trainer)||IsSsAnneTrainer(trainer)?_content.FindProgram(definition.ScriptEntry)!.Instructions.Single(i=>i.Op==StoryOp.TrainerBattle).Source.Line:definition.Source.StartLine+(IsPewterTrainer(trainer)&&definition.Mode==1?2:1);
        if (effect.Source.Path != definition.Source.Path || ((effect.Source.Symbol != definition.ScriptEntry || effect.Source.Line != expectedLine) && !IsBillRocketTriggeredBattle(effect,trainer)) ||
            (!noIntro&&(effect.Args[1] != definition.IntroText || effect.Args[2] != definition.DefeatText)) ||
            battleActorId is not { } actorId || !state.Actors.TryGetValue(actorId, out var actor) ||
            actor.ScriptId != (noIntro ? OrdinaryNoIntroActorScript(trainer) : definition.ScriptEntry) ||
            (noIntro&&IsCeruleanRival(trainer)&&actor.LocalId!="LOCALID_CERULEAN_RIVAL") ||
            state.World.Player.MapId != OrdinaryTrainerMap(trainer))
            throw new InvalidDataException("forest_trainer_source_definition_mismatch");
        if(IsPewterTrainer(trainer))
        {
            var source=_content.PewterTrainers!.Battles[trainer];
            if(source.Mode==1 ? effect.Args.Length!=5||effect.Args[3]!=source.PostBattleScript||effect.Args[4]!=source.MusicOption : effect.Args.Length!=3)
                throw new InvalidDataException("pewter_trainer_optional_arguments_mismatch");
        }
        else if(IsPokemonTowerTrainer(trainer)&&!noIntro)
        {
            var source=_content.PokemonTowerTrainers!.Battles[trainer];int expected=source.PostBattleScript is null?3:4;
            if(effect.Args.Length!=expected||expected==4&&effect.Args[3]!=source.PostBattleScript)throw new InvalidDataException("tower_trainer_optional_arguments");
        }
        else if(IsRocketHideoutTrainer(trainer)&&!noIntro)
        {
            var source=_content.RocketHideoutTrainers!.Battles[trainer];
            int expected=source.PostBattleScript is not null?4:3;
            if(effect.Args.Length!=expected||expected==4&&effect.Args[3]!=source.PostBattleScript)
                throw new InvalidDataException("rocket_trainer_optional_arguments_mismatch");
        }
        else if(IsViridianGymTrainer(trainer))
        {
            var source=_content.ViridianGymTrainerParties!.BattlesByScript[definition.ScriptEntry];
            int expected=source.MusicOption is not null?5:source.PostBattleScript is not null?4:3;
            if(noIntro||effect.Args.Length!=expected||expected>=4&&effect.Args[3]!=source.PostBattleScript||expected==5&&effect.Args[4]!=source.MusicOption)
                throw new InvalidDataException("viridian_gym_trainer_optional_arguments_mismatch");
        }
        else if(IsCinnabarTrainer(trainer))
        {
            var source=_content.CinnabarTrainerParties!.BattlesByScript[definition.ScriptEntry];
            int expected=noIntro?2:source.MusicOption is not null?5:source.PostBattleScript is not null?4:3;
            if(effect.Args.Length!=expected||!noIntro&&(expected>=4&&effect.Args[3]!=source.PostBattleScript||expected==5&&effect.Args[4]!=source.MusicOption))
                throw new InvalidDataException("cinnabar_trainer_optional_arguments_mismatch");
        }
        else if(IsSaffronGymTrainer(trainer))
        {
            var source=_content.SaffronGymTrainerParties!.Battles[trainer];
            int expected=source.MusicOption is not null?5:source.PostBattleScript is not null?4:3;
            if(effect.Args.Length!=expected||expected>=4&&effect.Args[3]!=source.PostBattleScript||expected==5&&effect.Args[4]!=source.MusicOption)
                throw new InvalidDataException("saffron_gym_trainer_optional_arguments_mismatch");
        }
        else if(IsFuchsiaGymTrainer(trainer))
        {
            var source=_content.FuchsiaGymTrainerParties!.Battles[trainer];
            int expected=source.MusicOption is not null?5:source.PostBattleScript is not null?4:3;
            if(effect.Args.Length!=expected||expected>=4&&effect.Args[3]!=source.PostBattleScript||expected==5&&effect.Args[4]!=source.MusicOption)
                throw new InvalidDataException("fuchsia_gym_trainer_optional_arguments_mismatch");
        }
        else if(IsCeladonGymTrainer(trainer))
        {
            var source=_content.CeladonGymTrainers!.Battles[trainer];
            int expected=source.MusicOption is not null?5:source.PostBattleScript is not null?4:3;
            if(effect.Args.Length!=expected||expected>=4&&effect.Args[3]!=source.PostBattleScript||expected==5&&effect.Args[4]!=source.MusicOption)
                throw new InvalidDataException("celadon_gym_trainer_optional_arguments_mismatch");
        }
        else if(IsVermilionGymTrainer(trainer))
        {
            var source=_content.VermilionGymTrainers!.Battles[trainer];
            int expected=source.MusicOption is not null?5:source.PostBattleScript is not null?4:3;
            if(effect.Args.Length!=expected||expected>=4&&effect.Args[3]!=source.PostBattleScript||expected==5&&effect.Args[4]!=source.MusicOption)
                throw new InvalidDataException("vermilion_gym_trainer_optional_arguments_mismatch");
        }
        else if(IsCeruleanTrainer(trainer)&&definition.Mode==1)
        {
            var source=_content.CeruleanTrainers!.Battles[trainer];
            if(effect.Args.Length!=5||effect.Args[3]!=source.PostBattleScript||effect.Args[4]!=source.MusicOption)
                throw new InvalidDataException("cerulean_trainer_optional_arguments_mismatch");
        }
        else if(!noIntro&&effect.Args.Length!=3)throw new InvalidDataException("forest_trainer_source_wait_invalid");
        if (!_forestTrainerApproaching&&!noIntro) state = FaceSelectedObject(state);
        if(IsFirstSeviiTrainer(trainer)&&noIntro)RequireFirstSeviiBikerOccurrence(state,trainer,definition);
        if(IsLeagueRivalTrainer(trainer))RequireLeagueRivalOccurrence(state,trainer,definition,noIntro);
        if (state.BattledTrainers.Contains(trainer.Value)&&!(IsFirstSeviiTrainer(trainer)&&_content.FirstSeviiTrainerParties!.ReplaysDefeatedTrainer(definition.ScriptEntry,definition.Source.StartLine)))
        {
            // Script_HasTrainerBeenFought -> gotopostbattlescript: resume at the instruction
            // immediately after trainerbattle_single, preserving source MSGBOX_AUTOCLOSE.
            state = state with { Script = state.Script with { Locked = true,
                Variables = state.Script.Variables.SetItem("VAR_RESULT", 1) } };
            return Advance(state, StoryRules.Continue(state.Script, state.Script.Wait!.CorrelationId, _content));
        }
        var prepared = PrepareForestTrainerBattle(state, trainer);
        int active = Enumerable.Range(0, state.Party.Length).First(i => !state.Party[i].IsEgg && state.Party[i].Pokemon.CurrentHp > 0);
        string id = "forest-trainer-battle:" + ((state.ForestTrainerHistory?.Length ?? 0) + 1);
        var combat = prepared.State!;
        if(_content.CeruleanEnabled)combat=combat with{CeruleanPlayerPartyIndex=active};
        var owned = state.Party[active];
        var presentation = new CampaignWildBattle(id, owned, combat.Battle, prepared.Events, 0,
            combat.Battle.Player.Pokemon, combat.Battle.Opponent.Pokemon, CampaignBattlePhase.Events,
            null, [], 0, prepared.Trace, [])
        { ActivePartyIndex = active, ParticipatingMembers = ImmutableSortedSet<long>.Empty.Add(owned.InstanceId),
            FaintedMembers = ImmutableSortedSet<long>.Empty, OpponentHpAtLastSwitch = combat.Battle.Opponent.Pokemon.CurrentHp };
        var battle = new CampaignForestTrainerBattle(id, trainer, actorId, combat, presentation,
            CampaignForestTrainerStage.Intro, state.Party.Select(p => p.Pokemon.Level).ToImmutableArray(),
            prepared.TrainerEvents, 0, 0, [0]){SourceScriptEntry=IsLeagueRivalTrainer(trainer)||IsViridianGymTrainer(trainer)||IsFirstSeviiTrainer(trainer)||IsCinnabarTrainer(trainer)?definition.ScriptEntry:null,SourceScriptLine=IsLeagueRivalTrainer(trainer)?definition.Source.StartLine+1:IsFirstSeviiTrainer(trainer)?definition.Source.StartLine:null};
        var fixedActor = state.Actors[actorId];
        state = state with { Actors = state.Actors.SetItem(actorId, fixedActor with { MovementType = ForestTrainerFacingType(fixedActor.Facing) }) };
        var next=state with { ForestTrainerBattle = battle,
            Dialogue = noIntro?null:new(definition.IntroText, _content.TextPages(definition.IntroText), 0, false, false),
            Script = state.Script with { Locked = true, Variables = _forestTrainerApproaching ? state.Script.Variables : state.Script.Variables.SetItem("VAR_RESULT", 0) },
            Music = noIntro||IsViridianGymTrainer(trainer)&&_content.ViridianGymTrainerParties!.BattlesByScript[definition.ScriptEntry].MusicOption=="NO_MUSIC"||IsCinnabarTrainer(trainer)&&_content.CinnabarTrainerParties!.BattlesByScript[definition.ScriptEntry].MusicOption=="NO_MUSIC"||IsSaffronGymTrainer(trainer)&&_content.SaffronGymTrainerParties!.Battles[trainer].MusicOption=="NO_MUSIC"||IsFuchsiaGymTrainer(trainer)&&_content.FuchsiaGymTrainerParties!.Battles[trainer].MusicOption=="NO_MUSIC"||IsCeladonGymTrainer(trainer)&&_content.CeladonGymTrainers!.Battles[trainer].MusicOption=="NO_MUSIC"||IsVermilionGymTrainer(trainer)&&_content.VermilionGymTrainers!.Battles[trainer].MusicOption=="NO_MUSIC"||(IsPewterTrainer(trainer)||IsCeruleanTrainer(trainer)) && definition.Mode == 1 ? state.Music : OrdinaryEncounterMusic(trainer) };
        return noIntro?PrepareForestTrainerPresentation(ResetFieldPoisonCounterForBattle(next) with{ForestTrainerBattle=battle with{Stage=CampaignForestTrainerStage.Combat},Dialogue=null,Motions=[],PresentationTicks=0,World=next.World with{Rng=combat.Battle.Rng},SeenSpecies=next.SeenSpecies.Add(combat.Battle.Opponent.Pokemon.SourceSpeciesId)}):next;
    }

    private bool HasForestTrainerReserves(CampaignState state, int active) =>
        state.Party.Where((p, i) => i != active).Any(p => !p.IsEgg && p.Pokemon.CurrentHp > 0);

    private Decision<CampaignState> DecideForestTrainerBattle(CampaignState state, CampaignCommand command)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        trainer=WithSsAnnePartyContext(state,trainer);
        if (state.Script.Wait?.Kind != StoryWaitKind.Battle)
            return new(CommandStatus.Fault, state, "forest_trainer_script_context");
        if (trainer.Stage == CampaignForestTrainerStage.Intro)
        {
            if (command.Kind != "ack") return new(CommandStatus.Rejected, state, "forest_trainer_intro_pending");
            if (state.Dialogue is { } dialogue && dialogue.Page + 1 < dialogue.Pages.Length)
                return new(CommandStatus.Accepted, state with { Dialogue = dialogue with { Page = dialogue.Page + 1 } });
            return new(CommandStatus.Accepted, PrepareForestTrainerPresentation(ResetFieldPoisonCounterForBattle(state) with
            { ForestTrainerBattle = trainer with { Stage = CampaignForestTrainerStage.Combat }, Dialogue = null,
                Motions = [], PresentationTicks = 0, World = state.World with { Rng = trainer.Combat.Battle.Rng },
                SeenSpecies = state.SeenSpecies.Add(trainer.Combat.Battle.Opponent.Pokemon.SourceSpeciesId) }));
        }
        if (command.Kind == "ack")
        {
            if (b.Phase == CampaignBattlePhase.Events)
            {
                trainer = trainer.CurrentTrainerEvent is null
                    ? trainer with { Presentation = b with { EventIndex = b.EventIndex + 1 } }
                    : trainer with { TrainerEventIndex = trainer.TrainerEventIndex + 1 };
                return new(CommandStatus.Accepted, PrepareForestTrainerPresentation(state with { ForestTrainerBattle = trainer }));
            }
            if (b.Phase == CampaignBattlePhase.Results)
            {
                if (b.PageIndex + 1 < b.Pages.Length)
                {
                    int index = b.PageIndex + 1;
                    return new(CommandStatus.Accepted, state with { ForestTrainerBattle = trainer with { Presentation = b with
                    { PageIndex = index, DisplayPlayer = b.Pages[index].Reward is WildRewardLevelEvent && b.RewardPartyIndex == b.ActivePartyIndex
                        ? state.Party[b.ActivePartyIndex].Pokemon : b.DisplayPlayer } } });
                }
                if (!b.PendingMoves.IsEmpty) return new(CommandStatus.Rejected, state, "trainer_move_learning_required");
                return new(CommandStatus.Accepted, trainer.Stage == CampaignForestTrainerStage.Finished
                    ? BeginForestTrainerEvolution(state) : CompleteForestTrainerRewardReceiver(state));
            }
            return new(CommandStatus.Rejected, state, "battle_move_required");
        }
        if (command.Kind == "wild_learn") return DecideForestTrainerLearning(state, command);
        if (b.Phase != CampaignBattlePhase.ChooseMove) return new(CommandStatus.Rejected, state, "battle_presentation_pending");
        if(SourceTrainerBatonPassPending(trainer.Combat))
            return DecideSourceTrainerPartyChoice(state with{ForestTrainerBattle=trainer},command);
        if (command.Kind == "battle_ball") return new(CommandStatus.Rejected, state, "trainer_battle_cannot_capture");
        if (command.Kind == "battle_menu" && command.Text == "run") return new(CommandStatus.Rejected, state, "trainer_battle_cannot_run");
        if (trainer.Combat.Phase is ForestTrainerCombatPhase.AwaitingPlayerReplacement or ForestTrainerCombatPhase.AwaitingShiftChoice)
            return DecideForestTrainerReplacement(state, command);
        if (command.Kind == "battle_menu" && command.Text is "bag" or "party") return new(CommandStatus.Accepted, state);
        if (command.Kind == "battle_switch") return DecideForestTrainerReplacement(state, command);
        if (command.Kind == "battle_poke_flute") return DecideTrainerPokeFlute(state, command);
        if (command.Kind == "battle_medicine") return DecideTrainerMedicine(state, command);
        bool reserves = HasForestTrainerReserves(state, b.ActivePartyIndex);
        ForestTrainerCombatResult turn; PotionUseResult? potion = null;
        if (command.Kind == "battle_move") turn = ForestTrainerCombatRules.ResolveMove(OrdinaryExecutionPokemon(trainer.Combat.Battle), trainer.Combat, command.MoveSlot, reserves);
        else if (command.Kind == "battle_potion")
        {
            if (_content.OpeningItems is null || state.Inventory is null || command.PartyIndex != b.ActivePartyIndex)
                return new(CommandStatus.Rejected, state, "trainer_item_context_invalid");
            potion = UseCampaignPotion(state.Inventory, WildBattleTransformationRules.PersistentPokemon(trainer.Combat.Battle.Player), PotionUseContext.Battle);
            if (!potion.Succeeded) return new(CommandStatus.Rejected, state, potion.Error);
            turn = ForestTrainerCombatRules.ResolvePotion(OrdinaryExecutionPokemon(trainer.Combat.Battle), trainer.Combat, potion.Pokemon, reserves);
        }
        else return new(CommandStatus.Rejected, state, "forest_trainer_battle_active");
        if (!turn.Succeeded) return new(CommandStatus.Rejected, state, turn.Fault);
        return new(CommandStatus.Accepted, CommitForestTrainerTurn(state with { Inventory = potion?.Inventory ?? state.Inventory }, turn));
    }

    private Decision<CampaignState> DecideForestTrainerReplacement(CampaignState state, CampaignCommand command)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        trainer=WithSsAnnePartyContext(state,trainer);
        bool shift = trainer.Combat.Phase == ForestTrainerCombatPhase.AwaitingShiftChoice;
        if (shift && command.Kind == "trainer_shift" && command.Choice == 0)
        {
            var declined = ForestTrainerCombatRules.ChooseShift(OrdinaryExecutionPokemon(trainer.Combat.Battle), trainer.Combat, null, HasForestTrainerReserves(state, b.ActivePartyIndex));
            return declined.Succeeded ? new(CommandStatus.Accepted, CommitForestTrainerTurn(state, declined))
                : new(CommandStatus.Rejected, state, declined.Fault);
        }
        if (shift && command.Kind == "trainer_shift" && command.Choice == 1)
            return new(CommandStatus.Accepted, state with { ForestTrainerBattle = trainer with
            { Presentation = b with { Replacement = CampaignReplacementStage.Select } } });
        if (command.Kind != "battle_switch") return new(CommandStatus.Rejected, state, shift ? "trainer_shift_choice_required" : "trainer_replacement_required");
        int index = command.PartyIndex;
        if (index < 0 || index >= state.Party.Length || index == b.ActivePartyIndex || state.Party[index].IsEgg || state.Party[index].Pokemon.CurrentHp == 0)
            return new(CommandStatus.Rejected, state, "replacement_member_invalid");
        var incoming = state.Party[index];
        var replacement=trainer.Combat.SourceMoveRulesVersion is not null
            ? trainer.Combat.SsAnnePlayerParty!.Value[index]??throw new InvalidDataException("source_trainer_reserve_missing")
            : OwnedBattleMember(state,b,incoming);
        bool reserves = HasForestTrainerReserves(state, index);
        var turn = shift ? ForestTrainerCombatRules.ChooseShift(OrdinaryExecutionPokemon(trainer.Combat.Battle), trainer.Combat, replacement, reserves, replacementPartyIndex:index)
            : trainer.Combat.Phase == ForestTrainerCombatPhase.AwaitingPlayerReplacement
                ? ForestTrainerCombatRules.ReplaceFaintedPlayer(OrdinaryExecutionPokemon(trainer.Combat.Battle), trainer.Combat, replacement, reserves, replacementPartyIndex:index)
                : ForestTrainerCombatRules.SwitchPlayer(OrdinaryExecutionPokemon(trainer.Combat.Battle), trainer.Combat, replacement, reserves, replacementPartyIndex:index);
        if (!turn.Succeeded) return new(CommandStatus.Rejected, state, turn.Fault);
        if(_content.CeruleanEnabled&&!_content.SsAnneEnabled&&turn.Events.OfType<WildBattlePlayerSwitchedEvent>().Any())
            turn=turn with{State=turn.State! with{CeruleanPlayerPartyIndex=index}};
        if(trainer.Combat.SourceMoveRulesVersion is null&&_content.Route22Enabled&&turn.Events.OfType<WildBattlePlayerSwitchedEvent>().SingleOrDefault() is {} switched)
        {
            b=RememberKnockedOff(b,state.Party[b.ActivePartyIndex].InstanceId,switched.Previous);
            var preserved=state.Party[b.ActivePartyIndex] with{Pokemon=WildBattleTransformationRules.PersistentPokemon(switched.Previous),Status=WildBattleTransformationRules.PersistentStatus(switched.Previous),HeldItem=OwnedBattleHeldItem(switched.Previous)};
            var fainted=b.FaintedMembers??ImmutableSortedSet<long>.Empty;
            if(preserved.Pokemon.CurrentHp==0&&!fainted.Contains(preserved.InstanceId))
            {preserved=preserved with{Pokemon=preserved.Pokemon with{Friendship=Math.Max(0,preserved.Pokemon.Friendship-1)}};fainted=fainted.Add(preserved.InstanceId);}
            state=state with{Party=state.Party.SetItem(b.ActivePartyIndex,preserved)};
            b=b with{FaintedMembers=fainted};trainer=trainer with{Presentation=b};
        }
        var outgoing = state.Party[b.ActivePartyIndex]; int hp = b.State.Opponent.Pokemon.CurrentHp;
        int snapshot = b.OpponentHpAtLastSwitch ?? hp; int loss = hp >= snapshot || snapshot == 0 ? 0 : (snapshot - hp) * 100 / snapshot;
        string withdrawal = hp >= snapshot ? "sText_PkmnThatsEnough" : loss < 30 ? "sText_PkmnComeBack" : loss < 70 ? "sText_PkmnOkComeBack" : "sText_PkmnGoodComeBack";
        int percent = Math.Max(1, hp * 100 / b.State.Opponent.Pokemon.Stats.MaxHp);
        string send = hp == 0 || percent >= 70 ? "sText_GoPkmn2" : percent >= 40 ? "sText_DoItPkmn" : percent >= 10 ? "sText_GoForItPkmn" : "sText_YourFoesWeakGetEmPkmn";
        return new(CommandStatus.Accepted, CommitForestTrainerTurn(state with { ForestTrainerBattle = trainer with { Presentation = b with
        { ActivePartyIndex = index, OwnedBefore = incoming, Replacement = null,
            ParticipatingMembers = b.ParticipatingMembers!.Add(incoming.InstanceId), OpponentHpAtLastSwitch = hp,
            LastSwitch = new(outgoing, withdrawal, send) } } }, turn));
    }

    private CampaignState CommitForestTrainerTurn(CampaignState state, ForestTrainerCombatResult result)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        // Medicine/flute results may already have changed the owned party; validate the
        // returned complete source party below, rather than rebuilding the input context.
        if(trainer.Combat.SourceMoveRulesVersion is null)trainer=WithSsAnnePartyContext(state,trainer);
        var combat = result.State!;
        if(combat.SourceMoveRulesVersion is not null)
            state=ProjectSourceTrainerOwnedParty(state,combat);
        foreach (var forced in result.TrainerEvents.OfType<SsAnneTrainerPlayerForcedOutEvent>())
        {
            if (!_content.SsAnneEnabled || forced.PreviousIndex != b.ActivePartyIndex || forced.PartyIndex < 0 || forced.PartyIndex >= state.Party.Length)
                throw new InvalidDataException("ssanne_forced_party_context");
            if(combat.SourceMoveRulesVersion is null)
            {
                b=RememberKnockedOff(b,state.Party[forced.PreviousIndex].InstanceId,forced.Previous);
                var previous = state.Party[forced.PreviousIndex] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(forced.Previous), Status = WildBattleTransformationRules.PersistentStatus(forced.Previous), HeldItem = OwnedBattleHeldItem(forced.Previous) };
                state = state with { Party = state.Party.SetItem(forced.PreviousIndex, previous) };
            }
            b = b with { ActivePartyIndex = forced.PartyIndex, OwnedBefore = state.Party[forced.PartyIndex], LastSwitch = null,
                ParticipatingMembers = b.ParticipatingMembers!.Add(state.Party[forced.PartyIndex].InstanceId), OpponentHpAtLastSwitch = combat.Battle.Opponent.Pokemon.CurrentHp };
        }
        if(combat.SourceMoveRulesVersion is not null&&combat.Battle.CalledParty!.PlayerActiveIndex!=b.ActivePartyIndex)
            throw new InvalidDataException("source_trainer_replacement_event_context");
        if(combat.SourceMoveRulesVersion is null)b=RememberKnockedOff(b,state.Party[b.ActivePartyIndex].InstanceId,combat.Battle.Player);
        var owned = state.Party[b.ActivePartyIndex] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(combat.Battle.Player), Status = WildBattleTransformationRules.PersistentStatus(combat.Battle.Player), HeldItem = combat.SourceMoveRulesVersion is not null ? WildBattleSourcePartyEffects.PersistentHeldItem(combat.Battle.CalledParty!.Player[b.ActivePartyIndex]) : OwnedBattleHeldItem(combat.Battle.Player) };
        var fainted = b.FaintedMembers ?? ImmutableSortedSet<long>.Empty;
        if (owned.Pokemon.CurrentHp == 0 && !fainted.Contains(owned.InstanceId))
        {
            // All admitted opponents are level 6-9: FAINT_LARGE's >=30-level gap cannot apply.
            owned = owned with { Pokemon = owned.Pokemon with { Friendship = Math.Max(0, owned.Pokemon.Friendship - 1) } };
            combat = combat with { Battle = combat.Battle with { Player = WildBattleTransformationRules.ApplyPersistentPokemon(combat.Battle.Player,owned.Pokemon) } };
            fainted = fainted.Add(owned.InstanceId);
        }
        if(combat.SourceMoveRulesVersion is not null)
            combat=SyncSourceTrainerOwnedPokemon(combat,b.ActivePartyIndex,owned);
        var participants = b.ParticipatingMembers!; var sent = trainer.OpponentsSent; var seen = state.SeenSpecies;
        foreach (var replacement in result.TrainerEvents.OfType<ForestTrainerOpponentReplacedEvent>())
        {
            participants = ImmutableSortedSet<long>.Empty.Add(owned.InstanceId);
            // The history records distinct opponents seen, not every entrance.
            // Whirlwind may bring the same living opponent back before its KO.
            if(!_content.EarlyPartyEnabled||!sent.Contains(replacement.OpponentIndex))sent = sent.Add(replacement.OpponentIndex);
            seen = seen.Add(replacement.Replacement.Pokemon.SourceSpeciesId);
        }
        b = b with { State = combat.Battle, Events = result.Events, EventIndex = 0, Phase = CampaignBattlePhase.Events,
            RandomTrace = result.Trace, FaintedMembers = fainted, ParticipatingMembers = participants,
            Reward = null, RewardReceivers = null, RewardReceiverIndex = 0, Pages = [], PageIndex = 0, PendingMoves = [], Replacement = null };
        trainer = trainer with { Combat = combat, Presentation = b, TrainerEvents = result.TrainerEvents,
            TrainerEventIndex = 0, Stage = CampaignForestTrainerStage.Combat, OpponentsSent = sent };
        return PrepareForestTrainerPresentation(state with { ForestTrainerBattle = trainer, Party = state.Party.SetItem(b.ActivePartyIndex, owned),
            SeenSpecies = seen, World = state.World with { Rng = combat.Battle.Rng } });
    }

    private CampaignState PrepareForestTrainerPresentation(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        trainer=WithSsAnnePartyContext(state,trainer);
        while (b.EventIndex < b.Events.Length || trainer.TrainerEventIndex < trainer.TrainerEvents.Length)
        {
            trainer = trainer with { Presentation = b };
            if (trainer.CurrentTrainerEvent is { } te)
            {
                if(te is SsAnneTrainerPlayerForcedOutEvent forced)return state with{ForestTrainerBattle=trainer with{Presentation=b with{DisplayPlayer=BattleDisplayPokemon(forced.Replacement),DisplayPlayerAppearance=null}}};
                if(te is CeruleanTrainerItemUsedEvent)return state with{ForestTrainerBattle=trainer};
                if (te is ForestTrainerOpponentReplacedEvent replacement)
                    return state with { ForestTrainerBattle = trainer with { Presentation = b with { DisplayOpponent = BattleDisplayPokemon(replacement.Replacement),DisplayOpponentAppearance=null } } };
                // Started is paired with the ordinary started event. AI/EXP/selection/SHIFT are
                // typed diagnostics; SHIFT receives its own decision screen after event draining.
                trainer = trainer with { TrainerEventIndex = trainer.TrainerEventIndex + 1 }; continue;
            }
            var e = b.Events[b.EventIndex];
            if(HiddenEarlyPartyEvent(e)){b=b with{EventIndex=b.EventIndex+1};continue;}
            if(e is WildBattleStatusChangedEvent {Reason:"sleep-countdown"} or WildBattleLeechSeedChangedEvent {Reason:not "move"}){b=b with{EventIndex=b.EventIndex+1};continue;}
            if (e is WildBattleOrderEvent or WildBattleTurnReadyEvent or WildBattleEndedEvent or WildBattleReplacementRequiredEvent or WildBattleDefenseCurledEvent || e is WildBattleBindingChangedEvent binding && binding.Reason is not ("bind" or "wrap" or "expired"))
            { b = b with { EventIndex = b.EventIndex + 1 }; continue; }
            void Update(WildBattleSide side, Func<PokemonInstance, PokemonInstance> update) =>
                b = side == WildBattleSide.Player ? b with { DisplayPlayer = update(b.DisplayPlayer) } : b with { DisplayOpponent = update(b.DisplayOpponent) };
            switch (e)
            {
                case WildBattlePpCommandEvent pp when pp.PpSlot>=0:Update(pp.PpOwner,p=>p with{Moves=p.Moves.SetItem(pp.PpSlot,p.Moves[pp.PpSlot] with{CurrentPp=pp.After})});break;
                case WildBattleSnatchPressurePpEvent pp:Update(pp.Snatcher,p=>p with{Moves=p.Moves.SetItem(pp.Slot,p.Moves[pp.Slot] with{CurrentPp=pp.After})});break;
                case WildBattleGrudgePpEvent grudge:Update(grudge.Attacker,p=>p with{Moves=p.Moves.SetItem(grudge.ChosenSlot,p.Moves[grudge.ChosenSlot] with{CurrentPp=0})});break;
                case WildBattleWeatherDamageEvent weather:Update(weather.Side,p=>p with{CurrentHp=weather.HpAfter});break;
                case WildBattleDrainEvent drain:Update(drain.Actor,p=>p with{CurrentHp=drain.HpAfter});break;
                case WildBattleMoveCopiedEvent copied:Update(copied.Actor,p=>p with{Moves=p.Moves.SetItem(copied.MoveSlot,copied.Replacement)});break;
                case WildBattleTransformedEvent transformed:b=ApplyTransformDisplay(b,transformed);break;
                case WildBattlePlayerSwitchedEvent change: b = b with { DisplayPlayer = change.Replacement.Pokemon }; break;
                case WildBattlePlayerReplacedEvent change: b = b with { DisplayPlayer = change.Replacement.Pokemon }; break;
                case WildBattleMoveUsedEvent used when used.Slot >= 0: Update(used.Actor, p => p with { Moves = p.Moves.SetItem(used.Slot, p.Moves[used.Slot] with { CurrentPp = used.PpAfter }) }); break;
                case WildBattleHpChangedEvent hp: Update(hp.Target, p => p with { CurrentHp = hp.HpAfter }); break;
                case WildBattleHeldItemConsumedEvent item:Update(item.Side,p=>p with{CurrentHp=item.HpAfter});break;
                case WildBattleIngrainHealEvent roots: Update(roots.Side, p => p with { CurrentHp = roots.HpAfter }); break;
                case WildBattleRecoveredEvent recovered: Update(recovered.Side, p => p with { CurrentHp = recovered.HpAfter }); break;
                case WildBattlePotionRecoveredEvent potion: Update(potion.Side, p => p with { CurrentHp = potion.HpAfter }); break;
                case WildBattleMedicineAppliedEvent {ActiveTarget:true} medicine:Update(WildBattleSide.Player,p=>p with{CurrentHp=medicine.HpAfter});break;
                case WildBattleRecoilEvent recoil: Update(recoil.Actor, p => p with { CurrentHp = recoil.HpAfter }); break;
                case WildBattleAbilityDamageEvent ability: Update(ability.Target, p => p with { CurrentHp = ability.HpAfter }); break;
                case WildBattleConfusionSelfHitEvent self: Update(self.Side, p => p with { CurrentHp = self.HpAfter }); break;
                case WildBattleLeechSeedDrainEvent seed:Update(seed.DrainedSide,p=>p with{CurrentHp=seed.HpAfter});Update(seed.ReceivingSide,p=>p with{CurrentHp=seed.ReceiverHpAfter});break;
                case WildBattleBindingDamageEvent bind:Update(bind.Side,p=>p with{CurrentHp=bind.HpAfter});break;
                case WildBattleStatusDamageEvent status: Update(status.Side, p => p with { CurrentHp = status.HpAfter }); break;
            }
            return state with { ForestTrainerBattle = trainer with { Presentation = b } };
        }
        state = state with { ForestTrainerBattle = trainer with { Presentation = b with
        { DisplayPlayer = BattleDisplayPokemon(trainer.Combat.Battle.Player), DisplayOpponent = BattleDisplayPokemon(trainer.Combat.Battle.Opponent),DisplayPlayerAppearance=BattleAppearance(trainer.Combat.Battle.Player),DisplayOpponentAppearance=BattleAppearance(trainer.Combat.Battle.Opponent) } } };
        if (trainer.Combat.Phase == ForestTrainerCombatPhase.AwaitingExperience) return BeginForestTrainerRewards(state);
        if (trainer.Combat.Phase == ForestTrainerCombatPhase.Ended) return SettleForestTrainerBattle(state);
        return state with { ForestTrainerBattle = state.ForestTrainerBattle! with { Presentation = state.ForestTrainerBattle.Presentation with
        { Phase = CampaignBattlePhase.ChooseMove, Replacement = trainer.Combat.Phase == ForestTrainerCombatPhase.AwaitingPlayerReplacement || SourceTrainerBatonPassPending(trainer.Combat) ? CampaignReplacementStage.Select : null } } };
    }

    private CampaignState BeginForestTrainerRewards(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        trainer=WithSsAnnePartyContext(state,trainer);
        var indices = Enumerable.Range(0, state.Party.Length).Where(i => b.ParticipatingMembers!.Contains(state.Party[i].InstanceId) &&
            !state.Party[i].IsEgg && state.Party[i].Pokemon.CurrentHp > 0).ToArray();
        var receivers = indices.Select(i => new CampaignWildRewardReceiver(i, IsLeagueRivalTrainer(trainer.Trainer)?WildVictoryRewards.ApplyLeagueRivalTrainer(_content.CinnabarOpponents!,state.Party[i],trainer.Combat,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length):IsViridianGymTrainer(trainer.Trainer)?WildVictoryRewards.ApplyViridianGymTrainer(_content.CinnabarOpponents!,state.Party[i],trainer.Combat,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length):IsFirstSeviiTrainer(trainer.Trainer)?WildVictoryRewards.ApplyFirstSeviiTrainer(_content.CinnabarOpponents!,state.Party[i],trainer.Combat,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length):IsCinnabarTrainer(trainer.Trainer)?WildVictoryRewards.ApplyCinnabarTrainer(_content.CinnabarOpponents!,state.Party[i],trainer.Combat,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length):IsSaffronGymTrainer(trainer.Trainer)?(_content.CinnabarEnabled?WildVictoryRewards.ApplySaffronGymTrainer(_content.CinnabarOpponents!,state.Party[i],trainer.Combat,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length):WildVictoryRewards.ApplySaffronGymTrainer(_content.SaffronGymOpponents!,state.Party[i],trainer.Combat,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length)):IsSilphTrainer(trainer.Trainer)?(_content.CinnabarEnabled?WildVictoryRewards.ApplySilphTrainer(_content.CinnabarOpponents!,state.Party[i],trainer.Combat,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length):WildVictoryRewards.ApplySilphTrainer(_content.SilphOwnership!,state.Party[i],trainer.Combat,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length)):SafariOrdinaryOwnership is {} safari?WildVictoryRewards.ApplySafariOwned(safari,state.Party[i],trainer.Combat.Battle.Opponent.Pokemon,_content.MapDefinition(state.World.Player.MapId).RegionMapSection,indices.Length,trainerBattle:true,defeatedBattleView:trainer.Combat.Battle.Opponent):WildVictoryRewards.Apply(BattlePokemon, state.Party[i],
            trainer.Combat.Battle.Opponent.Pokemon, _content.MapDefinition(state.World.Player.MapId).RegionMapSection,
            indices.Length, forestRules: true, trainerBattle: true,pewterRules:_content.PewterEnabled,route22Rules:_content.Route22Enabled,route3Rules:_content.Route3Enabled,mtMoonRules:_content.MtMoonEnabled,ceruleanRules:_content.CeruleanEnabled,billRules:_content.BillEnabled,vermilionRules:_content.VermilionEnabled,ssAnneRules:_content.SsAnneEnabled,vermilionGymRules:_content.VermilionGymEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,pokemonTowerRules:_content.PokemonTowerEnabled,snorlaxRules:_content.SnorlaxEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled,defeatedBattleView:trainer.Combat.Battle.Opponent))).ToImmutableArray();
        return StartForestTrainerRewardReceiver(state with { ForestTrainerBattle = trainer with
        { Stage = CampaignForestTrainerStage.Rewards, ExperienceGained = checked(trainer.ExperienceGained + receivers.Sum(r => r.Result.ExperienceGained)),
            Presentation = b with { RewardReceivers = receivers, RewardReceiverIndex = 0, Phase = CampaignBattlePhase.Results } } });
    }

    private CampaignState StartForestTrainerRewardReceiver(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        trainer=WithSsAnnePartyContext(state,trainer); var receivers = b.RewardReceivers!.Value;
        while (b.RewardReceiverIndex < receivers.Length)
        {
            var receiver = receivers[b.RewardReceiverIndex]; var reward = receiver.Result;
            var pages = ImmutableArray.CreateBuilder<CampaignWildPage>();
            foreach (var e in reward.Events)
            {
                if (e is WildRewardExperienceEvent xp) pages.Add(new($"{reward.Pokemon.Nickname} gained\n{xp.Gained} EXP. Points!", e));
                if (e is WildRewardLevelEvent level) { pages.Add(new($"{reward.Pokemon.Nickname} grew to\nlevel {level.After}!", e)); pages.Add(new("", e)); }
                if (e is WildRewardMoveLearnedEvent learned) pages.Add(new($"{reward.Pokemon.Nickname} learned\n{BattleContent.MoveNames[learned.Move.Value]}!", e));
            }
            state = state with { Party = state.Party.SetItem(receiver.PartyIndex, reward.Pokemon) };
            if(trainer.Combat.SourceMoveRulesVersion is not null)
                trainer=trainer with{Combat=SyncSourceTrainerOwnedPokemon(trainer.Combat,receiver.PartyIndex,reward.Pokemon)};
            b = b with { Reward = reward, Pages = pages.ToImmutable(), PageIndex = 0, PendingMoves = reward.PendingMoves };
            if (!b.Pages.IsEmpty || !b.PendingMoves.IsEmpty) return state with { ForestTrainerBattle = trainer with { Presentation = b } };
            b = b with { RewardReceiverIndex = b.RewardReceiverIndex + 1 };
        }
        state = state with { ForestTrainerBattle = trainer with { Presentation = b } };
        var complete = ForestTrainerCombatRules.CompleteOpponentExperience(OrdinaryExecutionPokemon(trainer.Combat.Battle), trainer.Combat,
            state.Party[b.ActivePartyIndex].Pokemon, HasForestTrainerReserves(state, b.ActivePartyIndex));
        if (!complete.Succeeded) throw new InvalidDataException(complete.Fault);
        return CommitForestTrainerTurn(state, complete);
    }

    private CampaignState CompleteForestTrainerRewardReceiver(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        trainer=WithSsAnnePartyContext(state,trainer);
        return StartForestTrainerRewardReceiver(state with { ForestTrainerBattle = trainer with { Presentation = b with
        { RewardReceiverIndex = b.RewardReceiverIndex + 1, DisplayPlayer = state.Party[b.ActivePartyIndex].Pokemon } } });
    }

    private Decision<CampaignState> DecideForestTrainerLearning(CampaignState state, CampaignCommand command)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        trainer=WithSsAnnePartyContext(state,trainer);
        if (trainer.Stage != CampaignForestTrainerStage.Rewards || b.Phase != CampaignBattlePhase.Results ||
            b.PageIndex + 1 < b.Pages.Length || b.PendingMoves.IsEmpty)
            return new(CommandStatus.Rejected, state, "trainer_learning_not_pending");
        var owned = state.Party[b.RewardPartyIndex];
        if (command.MoveSlot < -1 || command.MoveSlot >= owned.Pokemon.Moves.Length)
            return new(CommandStatus.Rejected, state, "trainer_learning_slot_invalid");
            if (_content.SsAnneEnabled && command.MoveSlot >= 0 && command.MoveSlot < owned.Pokemon.Moves.Length && PokemonMoveRules.IsHiddenMachineMove(owned.Pokemon.Moves[command.MoveSlot].Move))
                return new(CommandStatus.Rejected, state, "hm_move_cannot_be_forgotten");
        if (command.MoveSlot >= 0 && !PokemonMoveRules.IsLearningSupported(BattlePokemon, b.PendingMoves[0].Move))
            return new(CommandStatus.Rejected, state, "pokemon_learning_effect_unsupported");
        if (command.MoveSlot >= 0) owned = owned with { Pokemon = owned.Pokemon with
        { Moves = PokemonMoveRules.Replace(BattlePokemon, owned.Pokemon.Moves, command.MoveSlot, b.PendingMoves[0].Move) } };
        var pending = b.PendingMoves.RemoveAt(0);
        // The reward preflight cannot predict earlier replacement choices. Source learnsets
        // can repeat a candidate; recheck current knowledge before offering the next choice.
        while (!pending.IsEmpty && owned.Pokemon.Moves.Any(m => m.Move == pending[0].Move)) pending = pending.RemoveAt(0);
        b = b with { PendingMoves = pending, DisplayPlayer = b.RewardPartyIndex == b.ActivePartyIndex ? owned.Pokemon : b.DisplayPlayer };
        if(trainer.Combat.SourceMoveRulesVersion is not null)
            trainer=trainer with{Combat=SyncSourceTrainerOwnedPokemon(trainer.Combat,b.RewardPartyIndex,owned)};
        var next = state with { Party = state.Party.SetItem(b.RewardPartyIndex, owned), ForestTrainerBattle = trainer with { Presentation = b } };
        return new(CommandStatus.Accepted, b.PendingMoves.IsEmpty ? CompleteForestTrainerRewardReceiver(next) : next);
    }

    private CampaignState SettleForestTrainerBattle(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        trainer=WithSsAnnePartyContext(state,trainer);
        if ((state.ForestTrainerHistory ?? []).Any(h => h.Id == trainer.Id)) throw new InvalidDataException("forest_trainer_settlement_duplicate");
        bool won = trainer.Combat.Battle.Outcome == WildBattleOutcome.Won;
        var prize = SourceBattleMoneyRules.SettleTrainerPrize(state.Money,
            4 * OrdinaryTrainer(trainer.Trainer).Party[^1].Level * OrdinaryTrainer(trainer.Trainer).BaseMoney,
            trainer.Combat.Battle.Outcome,trainer.Combat.Battle.SourceMoneyMultiplier);
        int award=prize.DisplayAmount,money=prize.Money;
        var pages = ImmutableArray.CreateBuilder<CampaignWildPage>();
        if (won)
        {
            pages.Add(new($"{state.PlayerName} defeated\n{OrdinaryTrainerClass(trainer.Trainer)} {(IsLeagueRivalTrainer(trainer.Trainer)||IsSilphRival(trainer.Trainer)||IsPokemonTowerRival(trainer.Trainer)||IsRoute22Trainer(trainer.Trainer)||IsCeruleanRival(trainer.Trainer)||IsSsAnneRival(trainer.Trainer)?state.RivalName:OrdinaryTrainer(trainer.Trainer).SourceTrainerName)}!"));
            foreach (var text in _content.TextPages(OrdinaryTrainerScript(trainer.Trainer,trainer.SourceScriptEntry,trainer.SourceScriptLine).DefeatText)) pages.Add(new(text.Replace("{PLAYER}",state.PlayerName,StringComparison.Ordinal).Replace("{RIVAL}",state.RivalName,StringComparison.Ordinal)));
            pages.Add(new($"{state.PlayerName} got {award}\nfor winning!"));
        }
        else
        {
            if(IsRoute22Trainer(trainer.Trainer))foreach(var text in _content.TextPages(_content.Route22Trainers!.Battles[trainer.Trainer].VictoryText))pages.Add(new(text.Replace("{PLAYER}",state.PlayerName,StringComparison.Ordinal).Replace("{RIVAL}",state.RivalName,StringComparison.Ordinal)));
            pages.Add(new($"{state.PlayerName} is out of usable\nPOKéMON!"));
            int lost = WhiteOutLoss(state);
            pages.Add(new(state.Money == 0 ? $"{state.PlayerName} whited out!" : $"{state.PlayerName} panicked and lost\n{lost}...\n{state.PlayerName} whited out!"));
            foreach (var page in _content.TextPages(HasCenterRespawn(state) ? "gText_PlayerScurriedToCenter" : "gText_PlayerScurriedBackHome"))
                pages.Add(new(page.Replace("{PLAYER}", state.PlayerName, StringComparison.Ordinal)));
        }
        var history = new CampaignForestTrainerHistory(trainer.Id, trainer.Trainer.Value, trainer.Combat.Battle.Outcome,
            trainer.ExperienceGained, prize.Awarded, trainer.OpponentsSent){MoneyMultiplier=trainer.Combat.Battle.SourceMoneyMultiplier,SourceScriptEntry=IsLeagueRivalTrainer(trainer.Trainer)||IsFirstSeviiTrainer(trainer.Trainer)?trainer.SourceScriptEntry:null,SourceScriptLine=trainer.SourceScriptLine};
        if (_content.VermilionEnabled && won)
        {
            var payday = SourceBattleMoneyRules.SettlePayDay(money, trainer.Combat.Battle.PayDayMoney, WildBattleOutcome.Won,trainer.Combat.Battle.SourceMoneyMultiplier);
            var pickup = SafariOrdinaryOwnership is {} pickupOwned?VermilionPickupRules.ApplySafariOwned(pickupOwned,state.Party,state.World.Rng,WildBattleOutcome.Won):VermilionPickupRules.Apply(BattlePokemon, state.Party, state.World.Rng, WildBattleOutcome.Won,ssAnneRules:_content.SsAnneEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,pokemonTowerRules:_content.PokemonTowerEnabled,snorlaxRules:_content.SnorlaxEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled);
            money = payday.Money;
            if (payday.Amount != 0) pages.Add(new($"{state.PlayerName} picked up\n¥{payday.DisplayAmount}!"));
            state = state with { Party = pickup.Party, World = state.World with { Rng = pickup.Rng } };
            trainer = trainer with { Combat = trainer.Combat with { Battle = trainer.Combat.Battle with { Rng = pickup.Rng } } };
            if(trainer.Combat.SourceMoveRulesVersion is not null)
                trainer=trainer with{Combat=SyncSourceTrainerPickup(trainer.Combat,state.Party)};
            b = b with { RandomTrace = pickup.Trace };
        }
        return state with { Money = money, ForestTrainerHistory = (state.ForestTrainerHistory ?? []).Add(history),
            ForestTrainerBattle = trainer with { Stage = CampaignForestTrainerStage.Finished,
                Presentation = b with { Phase = CampaignBattlePhase.Results, Pages = pages.ToImmutable(), PageIndex = 0, PendingMoves = [] } } };
    }

    private CampaignState BeginForestTrainerEvolution(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!;
        if (trainer.Stage != CampaignForestTrainerStage.Finished || !trainer.Presentation.PendingMoves.IsEmpty || state.Evolution is not null)
            throw new InvalidDataException("forest_trainer_evolution_context");
        if (trainer.Combat.Battle.Outcome != WildBattleOutcome.Won) return ReturnFromForestTrainerBattle(state);
        var proposals = LevelEvolutionRules.ForParty(_content.PokemonContent!, state.Party, trainer.LevelsBeforeBattle);
        return proposals.IsEmpty ? ReturnFromForestTrainerBattle(state) : state with
        { Evolution = new(trainer.Id, proposals, 0, CampaignEvolutionStage.Proposal, [], 0, []) };
    }

    private CampaignState ReturnFromForestTrainerBattle(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!;
        if (!(state.ForestTrainerHistory ?? []).Any(h => h.Id == trainer.Id) || !trainer.Presentation.PendingMoves.IsEmpty)
            throw new InvalidDataException("forest_trainer_not_settled");
        if(IsRoute22Trainer(trainer.Trainer))return ReturnFromRoute22Rival(state);
        var next = state with { ForestTrainerBattle = null, Dialogue = null, Motions = [], PresentationTicks = 0,
            Script = state.Script with { Stack = [], Wait = null, Completed = true, Locked = false } };
        if (trainer.Combat.Battle.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw) return WhiteOutFromRoute1(next);
        // Normal callback sets the fought flag, then common single-battle script sets mode 0,
        // releases and ends. The map post-battle speech is reached on a later interaction.
        next=next with { BattledTrainers = next.BattledTrainers.Add(trainer.Trainer.Value),
            Script = next.Script with { Variables = next.Script.Variables.SetItem("VAR_RESULT", 0) },
            Music = FieldTravelMusic(next), Exploration = ResetEncounterModifiers(next.Exploration!) };
        if((IsLeagueRivalTrainer(trainer.Trainer)||IsFirstSeviiTrainer(trainer.Trainer)||IsCinnabarTrainer(trainer.Trainer)||IsSilphTrainer(trainer.Trainer)||IsPokemonTowerTrainer(trainer.Trainer)||IsRocketHideoutTrainer(trainer.Trainer)||IsMtMoonTrainer(trainer.Trainer)||IsCeruleanTrainer(trainer.Trainer)||IsBillTrainer(trainer.Trainer)||IsSsAnneTrainer(trainer.Trainer))&&OrdinaryTrainerScript(trainer.Trainer,trainer.SourceScriptEntry,trainer.SourceScriptLine).Mode==3)
        {
            var continuation=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",0)};
            return Pump(Advance(next with{Script=continuation},StoryRules.Continue(continuation,continuation.Wait!.CorrelationId,_content)));
        }
        if(IsPokemonTowerTrainer(trainer.Trainer)&&_content.PokemonTowerTrainers!.Battles[trainer.Trainer].PostBattleScript is {} towerCallback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},towerCallback));
        if(IsRocketHideoutTrainer(trainer.Trainer)&&_content.RocketHideoutTrainers!.Battles[trainer.Trainer].PostBattleScript is {} rocketCallback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},rocketCallback));
        if(IsViridianGymTrainer(trainer.Trainer)&&_content.ViridianGymTrainerParties!.BattlesByScript[trainer.SourceScriptEntry!].PostBattleScript is {} viridianGymCallback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},viridianGymCallback));
        if(IsCinnabarTrainer(trainer.Trainer)&&_content.CinnabarTrainerParties!.BattlesByScript[trainer.SourceScriptEntry!].PostBattleScript is {} cinnabarCallback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},cinnabarCallback));
        if(IsSaffronGymTrainer(trainer.Trainer)&&_content.SaffronGymTrainerParties!.Battles[trainer.Trainer].PostBattleScript is {} saffronGymCallback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},saffronGymCallback));
        if(IsFuchsiaGymTrainer(trainer.Trainer)&&_content.FuchsiaGymTrainerParties!.Battles[trainer.Trainer].PostBattleScript is {} fuchsiaGymCallback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},fuchsiaGymCallback));
        if(IsCeladonGymTrainer(trainer.Trainer)&&_content.CeladonGymTrainers!.Battles[trainer.Trainer].PostBattleScript is {} celadonGymCallback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},celadonGymCallback));
        if(IsVermilionGymTrainer(trainer.Trainer)&&_content.VermilionGymTrainers!.Battles[trainer.Trainer].PostBattleScript is {} gymCallback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},gymCallback));
        if(IsCeruleanTrainer(trainer.Trainer)&&_content.CeruleanTrainers!.Battles[trainer.Trainer].PostBattleScript is {} ceruleanCallback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},ceruleanCallback));
        if(IsPewterTrainer(trainer.Trainer)&&_content.PewterTrainers!.Battles[trainer.Trainer].PostBattleScript is {} callback)
            return Pump(StartStory(next with{Script=next.Script with{Locked=true}},callback));
        return next;
    }
}
