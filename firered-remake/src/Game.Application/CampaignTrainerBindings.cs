using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // This private composition table binds the same source catalog to definition,
    // script and Domain entry. It accepts no caller-provided catalog or executor.
    // The Domain overloads retain catalog identity, owned/opponent and party checks.
    private enum TrainerRegion
    {
        Forest, Pewter, Route22, Route3, MtMoon, Cerulean, Bill, Vermilion, SsAnne, VermilionGym, Route11, RockTunnel, CeladonApproach, CeladonGym, RocketHideout, PokemonTower, FuchsiaRoute, FuchsiaGym, Silph, SaffronGym, Cinnabar, FirstSevii, ViridianGym, LeagueRival, VictoryRoad, PokemonLeague, SeviiRuby, FourIsland, FiveIsland, SixIsland, SevenIsland, FightingDojo, Seafoam, MemorialPillar
    }

    private sealed record TrainerScriptBinding(TrainerRegion Region, ImmutableArray<ForestTrainerBattleDefinition> Sources,
        ImmutableDictionary<string, CeruleanTrainerBattleDefinition> AuthenticatedSources);
    private sealed record OrdinaryTrainerBinding(TrainerDefinition Definition, TrainerScriptBinding Scripts,
        Func<TrainerId, RngState, TrainerPartyCreationResult> Factory, string TracePrefix);
    private Dictionary<TrainerId, OrdinaryTrainerBinding>? _ordinaryTrainerBindings;
    private Dictionary<TrainerId, OrdinaryTrainerBinding> OrdinaryTrainerBindings =>
        _ordinaryTrainerBindings ??= BindOrdinaryTrainers();
    private OrdinaryTrainerBinding OrdinaryBinding(TrainerId trainer) => OrdinaryTrainerBindings[trainer];

    /// <summary>Read-only source definition from the same authenticated registry used for battle entry.</summary>
    public TrainerDefinition GetOrdinaryTrainerDefinition(TrainerId trainer) => OrdinaryBinding(trainer).Definition;

    private Dictionary<TrainerId, OrdinaryTrainerBinding> BindOrdinaryTrainers()
    {
        var bindings = new Dictionary<TrainerId, OrdinaryTrainerBinding>();
        void Add(TrainerRegion region, IEnumerable<TrainerDefinition> trainers,
            IEnumerable<ForestTrainerBattleDefinition> scripts,
            Func<TrainerId, RngState, TrainerPartyCreationResult> factory, string tracePrefix,
            IEnumerable<CeruleanTrainerBattleDefinition>? authenticated = null)
        {
            var source = new TrainerScriptBinding(region, scripts.ToImmutableArray(),
                (authenticated ?? []).ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
            // Some authenticated catalogs intentionally include an earlier roster
            // (Bill includes Cerulean). Later registrations own those trainers,
            // matching the current Domain entry catalog as well as its script.
            foreach (var trainer in trainers.Where(t => !t.DoubleBattle))
                bindings[trainer.Id] = new(trainer, source, factory, tracePrefix);
        }
        if (_content.ForestTrainers is { } forest)
        {
            Add(TrainerRegion.Forest, forest.Trainers.Values, forest.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => ForestTrainerPartyFactory.Create(forest, SafariOrdinaryOwnership!.Predecessor, trainer, rng), "forest-trainer");
        }
        if (_content.PewterTrainers is { } pewter)
        {
            Add(TrainerRegion.Pewter, pewter.Trainers.Values, pewter.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => PewterTrainerPartyFactory.Create(pewter, SafariOrdinaryOwnership!.Predecessor, trainer, rng), "pewter-trainer");
        }
        if (_content.Route22Trainers is { } route22)
        {
            Add(TrainerRegion.Route22, route22.Trainers.Values, route22.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => Route22TrainerPartyFactory.Create(route22, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "route22-trainer");
        }
        if (_content.Route3Trainers is { } route3)
        {
            Add(TrainerRegion.Route3, route3.Trainers.Values, route3.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => Route3TrainerPartyFactory.Create(route3, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "route3-trainer");
        }
        if (_content.MtMoonTrainers is { } mtMoon)
        {
            Add(TrainerRegion.MtMoon, mtMoon.Trainers.Values, mtMoon.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => MtMoonTrainerPartyFactory.Create(mtMoon, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "mtmoon-trainer");
        }
        if (_content.CeruleanTrainers is { } cerulean)
        {
            Add(TrainerRegion.Cerulean, cerulean.Trainers.Values, cerulean.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => CeruleanTrainerPartyFactory.Create(cerulean, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "cerulean-trainer");
        }
        if (_content.BillTrainers is { } bill)
        {
            Add(TrainerRegion.Bill, bill.Trainers.Values, bill.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => BillTrainerPartyFactory.Create(bill, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "bill-trainer");
        }
        if (_content.VermilionTrainers is { } vermilion)
        {
            Add(TrainerRegion.Vermilion, vermilion.Trainers.Values, vermilion.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => VermilionTrainerPartyFactory.Create(vermilion, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "vermilion-trainer");
        }
        if (_content.SsAnneTrainers is { } ssAnne)
        {
            Add(TrainerRegion.SsAnne, ssAnne.Trainers.Values, ssAnne.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SsAnneTrainerPartyFactory.Create(ssAnne, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "ssanne-trainer");
        }
        if (_content.VermilionGymTrainers is { } vermilionGym)
        {
            Add(TrainerRegion.VermilionGym, vermilionGym.Trainers.Values, vermilionGym.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => VermilionGymTrainerPartyFactory.Create(vermilionGym, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "vermilion-gym-trainer");
        }
        if (_content.Route11Trainers is { } route11)
        {
            Add(TrainerRegion.Route11, route11.Trainers.Values, route11.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => Route11TrainerPartyFactory.Create(route11, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "route11-trainer");
        }
        if (_content.RockTunnelTrainers is { } rockTunnel)
        {
            Add(TrainerRegion.RockTunnel, rockTunnel.Trainers.Values, rockTunnel.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => RockTunnelTrainerPartyFactory.Create(rockTunnel, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "rocktunnel-trainer");
        }
        if (_content.CeladonApproachTrainers is { } celadonApproach)
        {
            Add(TrainerRegion.CeladonApproach, celadonApproach.Trainers.Values, celadonApproach.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => CeladonApproachTrainerPartyFactory.Create(celadonApproach, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "celadon-approach-trainer");
        }
        if (_content.CeladonGymTrainers is { } celadonGym)
        {
            Add(TrainerRegion.CeladonGym, celadonGym.Trainers.Values, celadonGym.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => CeladonGymTrainerPartyFactory.Create(celadonGym, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "celadon-gym-trainer");
        }
        if (_content.RocketHideoutTrainers is { } rocketHideout)
        {
            Add(TrainerRegion.RocketHideout, rocketHideout.Trainers.Values, rocketHideout.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => RocketHideoutTrainerPartyFactory.Create(rocketHideout, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "rocket-hideout-trainer");
        }
        if (_content.PokemonTowerTrainers is { } pokemonTower)
        {
            Add(TrainerRegion.PokemonTower, pokemonTower.Trainers.Values, pokemonTower.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => PokemonTowerTrainerPartyFactory.Create(pokemonTower, SafariOrdinaryOwnership!.Predecessor, trainer, rng, fuchsiaRules: true), "pokemon-tower-trainer");
        }
        if (_content.FuchsiaRouteEnabled && _content.FuchsiaRouteTrainers is { } fuchsiaRoute)
        {
            Add(TrainerRegion.FuchsiaRoute, fuchsiaRoute.Trainers.Values, fuchsiaRoute.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FuchsiaRouteTrainerPartyFactory.Create(fuchsiaRoute, SafariOrdinaryOwnership!.Predecessor, trainer, rng), "fuchsia-route-trainer");
        }
        if (_content.FuchsiaGymTrainerParties is { } fuchsiaGym)
        {
            Add(TrainerRegion.FuchsiaGym, fuchsiaGym.Trainers.Values, fuchsiaGym.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FuchsiaGymTrainerPartyFactory.Create(fuchsiaGym, SafariOrdinaryOwnership!.Predecessor, trainer, rng), "fuchsia-gym-trainer");
        }
        if (_content.SilphTrainerParties is { } silph)
        {
            Add(TrainerRegion.Silph, silph.Trainers.Values, silph.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SilphTrainerPartyFactory.Create(silph, _content.CinnabarOpponents!.Predecessor.Predecessor, trainer, rng), "silph-trainer");
        }
        if (_content.SaffronGymTrainerParties is { } saffronGym)
        {
            Add(TrainerRegion.SaffronGym, saffronGym.Trainers.Values, saffronGym.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SaffronGymTrainerPartyFactory.Create(saffronGym, _content.CinnabarOpponents!.Predecessor, trainer, rng), "saffron-gym-trainer");
        }
        if (_content.CinnabarTrainerParties is { } cinnabar)
        {
            Add(TrainerRegion.Cinnabar, cinnabar.Trainers.Values, cinnabar.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => CinnabarTrainerPartyFactory.Create(cinnabar, _content.CinnabarOpponents!, trainer, rng), "cinnabar-trainer");
        }
        if (_content.FirstSeviiEnabled && _content.FirstSeviiTrainerParties is { } firstSevii)
        {
            Add(TrainerRegion.FirstSevii, firstSevii.Trainers.Values, firstSevii.BattlesBySource.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FirstSeviiTrainerPartyFactory.Create(firstSevii, _content.CinnabarOpponents!, trainer, rng), "first-sevii-trainer");
        }
        if (_content.ViridianGymEnabled && _content.ViridianGymTrainerParties is { } viridianGym)
        {
            Add(TrainerRegion.ViridianGym, viridianGym.Trainers.Values, viridianGym.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => ViridianGymTrainerPartyFactory.Create(viridianGym, _content.CinnabarOpponents!, trainer, rng), "viridian-gym-trainer");
        }
        if (_content.LeagueRivalEnabled && _content.LeagueRivalTrainerParties is { } leagueRival)
        {
            Add(TrainerRegion.LeagueRival, leagueRival.Trainers.Values, leagueRival.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => LeagueRivalTrainerPartyFactory.Create(leagueRival, _content.CinnabarOpponents!, trainer, rng), "league-rival-trainer");
        }
        if (_content.VictoryRoadEnabled && _content.VictoryRoadTrainerParties is { } victoryRoad)
        {
            Add(TrainerRegion.VictoryRoad, victoryRoad.Trainers.Values, victoryRoad.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => VictoryRoadTrainerPartyFactory.Create(victoryRoad, _content.VictoryRoadOpponents!, trainer, rng), "victory-road-trainer");
        }
        if (_content.PokemonLeagueEnabled && _content.PokemonLeagueTrainerParties is { } pokemonLeague)
        {
            Add(TrainerRegion.PokemonLeague, pokemonLeague.Trainers.Values, pokemonLeague.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => PokemonLeagueTrainerPartyFactory.Create(pokemonLeague, _content.PokemonLeagueOpponents!, trainer, rng), "pokemon-league-trainer");
        }
        if (_content.LeagueRematchEnabled && _content.LeagueRematchTrainerParties is { } rematch)
        {
            Add(TrainerRegion.PokemonLeague, rematch.Trainers.Values, rematch.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => LeagueRematchTrainerPartyFactory.Create(rematch, _content.LeagueRematchOpponents!, trainer, rng), "league-rematch-trainer");
        }
        if (_content.SeviiRubyEnabled && _content.SeviiRubyTrainers is { } seviiRuby)
        {
            Add(TrainerRegion.SeviiRuby, seviiRuby.Trainers.Values, seviiRuby.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SeviiRubyTrainerPartyFactory.Create(seviiRuby, _content.SeviiRubyOpponents!, trainer, rng), "sevii-ruby-trainer");
        }
        if (_content.FourIslandEnabled && _content.FourIslandTrainers is { } fourIsland)
        {
            Add(TrainerRegion.FourIsland, fourIsland.Trainers.Values, fourIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FourIslandTrainerPartyFactory.Create(fourIsland, _content.FourIslandOpponents!, trainer, rng), "four-island-trainer", fourIsland.BattlesByScript.Values);
        }
        if (_content.FiveIslandEnabled && _content.FiveIslandTrainers is { } fiveIsland)
        {
            Add(TrainerRegion.FiveIsland, fiveIsland.Trainers.Values, fiveIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FiveIslandTrainerPartyFactory.Create(fiveIsland, _content.FiveIslandOpponents!, trainer, rng), "five-island-trainer", fiveIsland.BattlesByScript.Values);
        }
        if (_content.SixIslandEnabled && _content.SixIslandTrainers is { } sixIsland)
        {
            Add(TrainerRegion.SixIsland, sixIsland.Trainers.Values, sixIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SixIslandTrainerPartyFactory.Create(sixIsland, _content.SixIslandOpponents!, trainer, rng), "six-island-trainer", sixIsland.BattlesByScript.Values);
        }
        if (_content.SevenIslandEnabled && _content.SevenIslandTrainers is { } sevenIsland)
        {
            Add(TrainerRegion.SevenIsland, sevenIsland.Trainers.Values, sevenIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SevenIslandTrainerPartyFactory.Create(sevenIsland, _content.SevenIslandOpponents!, trainer, rng), "seven-island-trainer", sevenIsland.BattlesByScript.Values);
        }
        if (_content.FightingDojoEnabled && _content.FightingDojoTrainers is { } fightingDojo)
        {
            Add(TrainerRegion.FightingDojo, fightingDojo.Trainers.Values, fightingDojo.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FightingDojoTrainerPartyFactory.Create(fightingDojo, _content.FightingDojoOpponents!, trainer, rng), "fighting-dojo-trainer", fightingDojo.BattlesByScript.Values);
        }
        if (_content.SeafoamEnabled && _content.SeafoamTrainers is { } seafoam)
        {
            Add(TrainerRegion.Seafoam, seafoam.Trainers.Values, seafoam.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SeafoamTrainerPartyFactory.Create(seafoam, _content.SeafoamOpponents!, trainer, rng), "seafoam-trainer", seafoam.BattlesByScript.Values);
        }
        if (_content.MemorialPillarTrainers is { } memorial)
        {
            Add(TrainerRegion.MemorialPillar, memorial.Trainers.Values, memorial.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => MemorialPillarTrainerPartyFactory.Create(memorial, _content.MemorialPillarOpponents!, trainer, rng),
                "memorial-pillar-trainer", memorial.BattlesByScript.Values);
        }
        return bindings;
    }

    private static ForestTrainerBattleDefinition ProjectTrainerScript(ForestTrainerBattleDefinition source) => source;
    private static ForestTrainerBattleDefinition ProjectTrainerScript(CeruleanTrainerBattleDefinition source) =>
        new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, source.IntroText,
            source.DefeatText, source.PostBattleText, source.EntryScript, source.Callback, source.Source);
    private static ForestTrainerBattleDefinition ProjectTrainerScript(PewterTrainerBattleDefinition source) =>
        new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, source.IntroText,
            source.DefeatText, source.PostBattleText, source.EntryScript, source.Callback, source.Source);
    private static ForestTrainerBattleDefinition ProjectTrainerScript(Route22TrainerBattleDefinition source) =>
        new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, "Route22_Text_EarlyRivalIntro",
            source.DefeatText, "Route22_Text_EarlyRivalPostBattle", source.EntryScript, source.Callback, source.Source);

    private ForestTrainerBattleDefinition BoundTrainerScript(OrdinaryTrainerBinding binding, string? script, int? sourceLine)
    {
        var trainer = binding.Definition.Id;
        var region = binding.Scripts.Region;
        var sources = binding.Scripts.Sources;
        if (region == TrainerRegion.FirstSevii)
        {
            var source = sourceLine is { } line && script is not null
                ? ProjectTrainerScript(_content.FirstSeviiTrainerParties!.Battle(script, line))
                : sources.Single(b => b.Trainer == trainer && (script is null || b.ScriptEntry == script));
            if (source.Trainer != trainer || source.Mode == 4)
                throw new InvalidDataException("first_sevii_single_script_mismatch");
            return source;
        }
        string? prefix = region switch
        {
            TrainerRegion.MemorialPillar => "memorial_pillar",
            TrainerRegion.Seafoam => "seafoam",
            TrainerRegion.FightingDojo => "fighting_dojo",
            TrainerRegion.SevenIsland => "seven_island", TrainerRegion.FiveIsland => "five_island", TrainerRegion.SixIsland => "six_island",
            TrainerRegion.FourIsland => "four_island", TrainerRegion.SeviiRuby => "sevii_ruby",
            TrainerRegion.VictoryRoad => "victory_road", TrainerRegion.ViridianGym => "viridian_gym",
            TrainerRegion.Cinnabar => "cinnabar", _ => null
        };
        if (prefix is null)
        {
            var source = sources.Single(b => b.Trainer == trainer);
            if (region == TrainerRegion.LeagueRival && (source.Mode != 3 || script is not null && script != source.ScriptEntry))
                throw new InvalidDataException("league_rival_script_mismatch");
            if (region == TrainerRegion.PokemonLeague && (source.Mode != 3 || source.BattleTypeFlags != 8 ||
                script is not null && script != source.ScriptEntry))
                throw new InvalidDataException("pokemon_league_trainer_script");
            return source;
        }
        if (region == TrainerRegion.FourIsland && script is not null && script != FourIslandTrainerPartyFactory.BattleScript)
            throw new InvalidDataException("four_island_trainer_script_missing");
        var selected = script is null
            ? sources.Single(b => b.Trainer == trainer && (region switch
            {
                TrainerRegion.Seafoam or TrainerRegion.SevenIsland or TrainerRegion.SixIsland or TrainerRegion.VictoryRoad => b.Mode == 0,
                TrainerRegion.Cinnabar => b.Mode is 0 or 1 or 2,
                TrainerRegion.SeviiRuby => b.Mode != 4,
                _ => true
            }))
            : sources.SingleOrDefault(b => b.ScriptEntry == script) ?? throw new InvalidDataException(prefix + "_trainer_script_missing");
        bool accepted = region switch
        {
            TrainerRegion.FightingDojo => selected.Mode is 0 or 2 && selected.BattleTypeFlags == 8 && selected.Source.Path == FightingDojoSource,
            TrainerRegion.MemorialPillar or TrainerRegion.Seafoam or TrainerRegion.SevenIsland or TrainerRegion.FiveIsland or TrainerRegion.SixIsland => selected.Mode == 0 && selected.BattleTypeFlags == 8,
            TrainerRegion.FourIsland => selected.Mode == 3 && selected.BattleTypeFlags == 8 && selected.Source.Path == FourIslandBackSource,
            TrainerRegion.SeviiRuby => selected.Mode is 0 or 3,
            TrainerRegion.VictoryRoad => selected.Mode == 0,
            TrainerRegion.ViridianGym => selected.Mode is 0 or 1,
            TrainerRegion.Cinnabar => selected.Mode != 4,
            _ => false
        };
        // Preserve each region's fingerprint validation on the original complete
        // battle record, including Five's callback and Six's double parameters.
        if (region == TrainerRegion.MemorialPillar) MemorialPillarTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (region == TrainerRegion.FiveIsland) FiveIslandTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (region == TrainerRegion.SixIsland) SixIslandTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (region == TrainerRegion.SevenIsland) SevenIslandTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (region == TrainerRegion.Seafoam) SeafoamTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (region == TrainerRegion.FourIsland) FourIslandTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (region == TrainerRegion.FightingDojo) FightingDojoTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (selected.Trainer != trainer || !accepted)
            throw new InvalidDataException(prefix + "_single_script_mismatch");
        return selected;
    }

    private void RequireOrdinaryTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro, TrainerRegion region, string fault)
    {
        if (!OrdinaryTrainerBindings.TryGetValue(trainer, out var binding) || binding.Scripts.Region != region ||
            noIntro || source.Mode != 0 || state.World.Player.MapId != OrdinaryTrainerMap(trainer) ||
            state.Script.Wait?.Kind != StoryWaitKind.Battle || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.ScriptId != source.ScriptEntry ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId)
            throw new InvalidDataException(fault);
    }

    private bool OrdinaryTrainerHistoryOriginMatches(CampaignForestTrainerHistory history, TrainerRegion region)
    {
        if (!OrdinaryTrainerBindings.TryGetValue(new(history.TrainerId), out var binding) || binding.Scripts.Region != region ||
            history.SourceScriptEntry is not { } script || !binding.Scripts.AuthenticatedSources.TryGetValue(script, out var source) ||
            source.Trainer.Value != history.TrainerId || source.Mode != 0) return false;
        int line = region switch
        {
            TrainerRegion.MemorialPillar => MemorialPillarTrainerSourceLine(script),
            TrainerRegion.FiveIsland => FiveIslandTrainerSourceLine(script),
            TrainerRegion.SixIsland => SixIslandTrainerSourceLine(script),
            TrainerRegion.SevenIsland => SevenIslandTrainerSourceLine(script),
            TrainerRegion.Seafoam => SeafoamTrainerSourceLine(script),
            _ => throw new InvalidDataException("ordinary_trainer_history_policy")
        };
        return history.SourceScriptLine == line;
    }

    private int RequireTrainerInstruction(CeruleanTrainerBattleDefinition source, string script,
        string opcode, string[] args, int line, string fault)
    {
        var instruction = _content.FindProgram(script)?.Instructions.SingleOrDefault(i => i.Op == StoryOp.TrainerBattle);
        if (instruction is null || instruction.Opcode != opcode || instruction.Source.Path != source.Source.Path ||
            instruction.Source.Symbol != script || instruction.Source.Line != line || !instruction.Args.SequenceEqual(args))
            throw new InvalidDataException(fault);
        return instruction.Source.Line;
    }
}
