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
    private sealed record TrainerScriptBinding(ImmutableArray<RegisteredTrainerSource> Sources);
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
        void Add(IEnumerable<TrainerDefinition> trainers,
            IEnumerable<RegisteredTrainerSource> scripts,
            Func<TrainerId, RngState, TrainerPartyCreationResult> factory, string tracePrefix)
        {
            var source = new TrainerScriptBinding(scripts.ToImmutableArray());
            // Some authenticated catalogs intentionally include an earlier roster
            // (Bill includes Cerulean). Later registrations own those trainers,
            // matching the current Domain entry catalog as well as its script.
            foreach (var trainer in trainers.Where(t => !t.DoubleBattle))
                bindings[trainer.Id] = new(trainer, source, factory, tracePrefix);
        }
        if (_content.ForestTrainers is { } forest)
        {
            Add(forest.Trainers.Values, forest.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => ForestTrainerPartyFactory.Create(forest, _binding.Ownership.Opponents.Catalog, trainer, rng), "forest-trainer");
        }
        if (_content.PewterTrainers is { } pewter)
        {
            Add(pewter.Trainers.Values, pewter.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => PewterTrainerPartyFactory.Create(pewter, _binding.Ownership.Opponents.Catalog, trainer, rng), "pewter-trainer");
        }
        if (_content.Route22Trainers is { } route22)
        {
            Add(route22.Trainers.Values, route22.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => Route22TrainerPartyFactory.Create(route22, _binding.Ownership.Opponents.Catalog, trainer, rng), "route22-trainer");
        }
        if (_content.Route3Trainers is { } route3)
        {
            Add(route3.Trainers.Values, route3.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => Route3TrainerPartyFactory.Create(route3, _binding.Ownership.Opponents.Catalog, trainer, rng), "route3-trainer");
        }
        if (_content.MtMoonTrainers is { } mtMoon)
        {
            Add(mtMoon.Trainers.Values, mtMoon.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => MtMoonTrainerPartyFactory.Create(mtMoon, _binding.Ownership.Opponents.Catalog, trainer, rng), "mtmoon-trainer");
        }
        if (_content.CeruleanTrainers is { } cerulean)
        {
            Add(cerulean.Trainers.Values, cerulean.Battles.Values.Select(ProjectTrainerScript)
                    .Concat((_content.BillTrainers?.AdditionalOccurrences ?? []).Select(ProjectTrainerScript)),
                (trainer, rng) => CeruleanTrainerPartyFactory.Create(cerulean, _binding.Ownership.Opponents.Catalog, trainer, rng), "cerulean-trainer");
        }
        if (_content.BillTrainers is { } bill)
        {
            Add(bill.Trainers.Values, bill.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => BillTrainerPartyFactory.Create(bill, _binding.Ownership.Opponents.Catalog, trainer, rng), "bill-trainer");
        }
        if (_content.VermilionTrainers is { } vermilion)
        {
            Add(vermilion.Trainers.Values, vermilion.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => VermilionTrainerPartyFactory.Create(vermilion, _binding.Ownership.Opponents.Catalog, trainer, rng), "vermilion-trainer");
        }
        if (_content.SsAnneTrainers is { } ssAnne)
        {
            Add(ssAnne.Trainers.Values, ssAnne.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SsAnneTrainerPartyFactory.Create(ssAnne, _binding.Ownership.Opponents.Catalog, trainer, rng), "ssanne-trainer");
        }
        if (_content.VermilionGymTrainers is { } vermilionGym)
        {
            Add(vermilionGym.Trainers.Values, vermilionGym.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => VermilionGymTrainerPartyFactory.Create(vermilionGym, _binding.Ownership.Opponents.Catalog, trainer, rng), "vermilion-gym-trainer");
        }
        if (_content.Route11Trainers is { } route11)
        {
            Add(route11.Trainers.Values, route11.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => Route11TrainerPartyFactory.Create(route11, _binding.Ownership.Opponents.Catalog, trainer, rng), "route11-trainer");
        }
        if (_content.RockTunnelTrainers is { } rockTunnel)
        {
            Add(rockTunnel.Trainers.Values, rockTunnel.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => RockTunnelTrainerPartyFactory.Create(rockTunnel, _binding.Ownership.Opponents.Catalog, trainer, rng), "rocktunnel-trainer");
        }
        if (_content.CeladonApproachTrainers is { } celadonApproach)
        {
            Add(celadonApproach.Trainers.Values, celadonApproach.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => CeladonApproachTrainerPartyFactory.Create(celadonApproach, _binding.Ownership.Opponents.Catalog, trainer, rng), "celadon-approach-trainer");
        }
        if (_content.CeladonGymTrainers is { } celadonGym)
        {
            Add(celadonGym.Trainers.Values, celadonGym.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => CeladonGymTrainerPartyFactory.Create(celadonGym, _binding.Ownership.Opponents.Catalog, trainer, rng), "celadon-gym-trainer");
        }
        if (_content.RocketHideoutTrainers is { } rocketHideout)
        {
            Add(rocketHideout.Trainers.Values, rocketHideout.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => RocketHideoutTrainerPartyFactory.Create(rocketHideout, _binding.Ownership.Opponents.Catalog, trainer, rng), "rocket-hideout-trainer");
        }
        if (_content.PokemonTowerTrainers is { } pokemonTower)
        {
            Add(pokemonTower.Trainers.Values, pokemonTower.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => PokemonTowerTrainerPartyFactory.Create(pokemonTower, _binding.Ownership.Opponents.Catalog, trainer, rng), "pokemon-tower-trainer");
        }
        if (_content.FuchsiaRouteTrainers is { } fuchsiaRoute)
        {
            Add(fuchsiaRoute.Trainers.Values, fuchsiaRoute.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FuchsiaRouteTrainerPartyFactory.Create(fuchsiaRoute, _binding.Ownership.Opponents.Catalog, trainer, rng), "fuchsia-route-trainer");
        }
        if (_content.FuchsiaGymTrainerParties is { } fuchsiaGym)
        {
            Add(fuchsiaGym.Trainers.Values, fuchsiaGym.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FuchsiaGymTrainerPartyFactory.Create(fuchsiaGym, _binding.Ownership.Opponents.Catalog, trainer, rng), "fuchsia-gym-trainer");
        }
        if (_content.SilphTrainerParties is { } silph)
        {
            Add(silph.Trainers.Values, silph.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SilphTrainerPartyFactory.Create(silph, _binding.Ownership.Opponents, trainer, rng), "silph-trainer");
        }
        if (_content.SaffronGymTrainerParties is { } saffronGym)
        {
            Add(saffronGym.Trainers.Values, saffronGym.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SaffronGymTrainerPartyFactory.Create(saffronGym, _binding.Ownership.Opponents, trainer, rng), "saffron-gym-trainer");
        }
        if (_content.CinnabarTrainerParties is { } cinnabar)
        {
            Add(cinnabar.Trainers.Values, cinnabar.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => CinnabarTrainerPartyFactory.Create(cinnabar, _content.OpponentPokemon!, trainer, rng), "cinnabar-trainer");
        }
        if (_content.FirstSeviiTrainerParties is { } firstSevii)
        {
            Add(firstSevii.Trainers.Values, firstSevii.BattlesBySource.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FirstSeviiTrainerPartyFactory.Create(firstSevii, _content.OpponentPokemon!, trainer, rng), "first-sevii-trainer");
        }
        if (_content.ViridianGymTrainerParties is { } viridianGym)
        {
            Add(viridianGym.Trainers.Values, viridianGym.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => ViridianGymTrainerPartyFactory.Create(viridianGym, _content.OpponentPokemon!, trainer, rng), "viridian-gym-trainer");
        }
        if (_content.LeagueRivalTrainerParties is { } leagueRival)
        {
            Add(leagueRival.Trainers.Values, leagueRival.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => LeagueRivalTrainerPartyFactory.Create(leagueRival, _content.OpponentPokemon!, trainer, rng), "league-rival-trainer");
        }
        if (_content.VictoryRoadTrainerParties is { } victoryRoad)
        {
            Add(victoryRoad.Trainers.Values, victoryRoad.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => VictoryRoadTrainerPartyFactory.Create(victoryRoad, _content.OpponentPokemon!, trainer, rng), "victory-road-trainer");
        }
        if (_content.PokemonLeagueTrainerParties is { } pokemonLeague)
        {
            Add(pokemonLeague.Trainers.Values, pokemonLeague.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => PokemonLeagueTrainerPartyFactory.Create(pokemonLeague, _content.OpponentPokemon!, trainer, rng), "pokemon-league-trainer");
        }
        if (_content.LeagueRematchTrainerParties is { } rematch)
        {
            Add(rematch.Trainers.Values, rematch.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => LeagueRematchTrainerPartyFactory.Create(rematch, _content.OpponentPokemon!, trainer, rng), "league-rematch-trainer");
        }
        if (_content.SeviiRubyTrainers is { } seviiRuby)
        {
            Add(seviiRuby.Trainers.Values, seviiRuby.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SeviiRubyTrainerPartyFactory.Create(seviiRuby, _content.OpponentPokemon!, trainer, rng), "sevii-ruby-trainer");
        }
        if (_content.FourIslandTrainers is { } fourIsland)
        {
            Add(fourIsland.Trainers.Values, fourIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FourIslandTrainerPartyFactory.Create(fourIsland, _content.OpponentPokemon!, trainer, rng), "four-island-trainer");
        }
        if (_content.FiveIslandTrainers is { } fiveIsland)
        {
            Add(fiveIsland.Trainers.Values, fiveIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FiveIslandTrainerPartyFactory.Create(fiveIsland, _content.OpponentPokemon!, trainer, rng), "five-island-trainer");
        }
        if (_content.SixIslandTrainers is { } sixIsland)
        {
            Add(sixIsland.Trainers.Values, sixIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SixIslandTrainerPartyFactory.Create(sixIsland, _content.OpponentPokemon!, trainer, rng), "six-island-trainer");
        }
        if (_content.SevenIslandTrainers is { } sevenIsland)
        {
            Add(sevenIsland.Trainers.Values, sevenIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SevenIslandTrainerPartyFactory.Create(sevenIsland, _content.OpponentPokemon!, trainer, rng), "seven-island-trainer");
        }
        if (_content.FightingDojoTrainers is { } fightingDojo)
        {
            Add(fightingDojo.Trainers.Values, fightingDojo.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FightingDojoTrainerPartyFactory.Create(fightingDojo, _content.OpponentPokemon!, trainer, rng), "fighting-dojo-trainer");
        }
        if (_content.SeafoamTrainers is { } seafoam)
        {
            Add(seafoam.Trainers.Values, seafoam.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SeafoamTrainerPartyFactory.Create(seafoam, _content.OpponentPokemon!, trainer, rng), "seafoam-trainer");
        }
        if (_content.MemorialPillarTrainers is { } memorial)
        {
            Add(memorial.Trainers.Values, memorial.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => MemorialPillarTrainerPartyFactory.Create(memorial, _content.OpponentPokemon!, trainer, rng),
                "memorial-pillar-trainer");
        }
        if (_content.LostCaveTrainers is { } lostCave)
        {
            Add(lostCave.Trainers.Values, lostCave.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => LostCaveTrainerPartyFactory.Create(lostCave, _binding.Ownership, trainer, rng),
                "lost-cave-trainer");
        }
        if (_content.CyclingRoadTrainers is { } cyclingRoad)
        {
            Add(cyclingRoad.Trainers.Values,cyclingRoad.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer,rng)=>CyclingRoadTrainerPartyFactory.Create(cyclingRoad,_binding.Ownership,trainer,rng),
                "cycling-road-trainer");
        }
        if (VsSeekerEnabled && _content.VsSeekerTrainers is { } vsSeeker)
        {
            // Existing first-battle bindings keep their declared ordinary occurrences.
            // Additional parties are reachable only through the typed rematch adapter.
            var source = new TrainerScriptBinding([]);
            foreach (var trainer in vsSeeker.Trainers.Values.Where(t => !t.DoubleBattle && !bindings.ContainsKey(t.Id)))
                bindings.Add(trainer.Id, new(trainer, source,
                    (id, rng) => VsSeekerTrainerPartyFactory.Create(vsSeeker, _binding.Ownership.Mechanics, id, rng), "vs-seeker-trainer"));
        }
        return bindings;
    }

    private static RegisteredTrainerSource ProjectTrainerScript(ForestTrainerBattleDefinition source) =>
        new(source, source.Mode == 3 ? "trainerbattle_no_intro" : "trainerbattle_single",
            source.Mode == 3 ? [source.Trainer.Value[5..], source.DefeatText]
                : [source.Trainer.Value[5..], source.IntroText, source.DefeatText]);

    private static RegisteredTrainerSource ProjectTrainerScript(CeruleanTrainerBattleDefinition source)
    {
        var definition = new ForestTrainerBattleDefinition(source.Trainer, source.Mode, source.BattleTypeFlags,
            source.ScriptEntry, source.IntroText, source.DefeatText, source.PostBattleText,
            source.EntryScript, source.Callback, source.Source);
        var arguments = source.Mode == 3
            ? ImmutableArray.Create(source.Trainer.Value[5..], source.DefeatText)
            : ImmutableArray.Create(source.Trainer.Value[5..], source.IntroText, source.DefeatText);
        if (source.Mode != 3 && source.PostBattleScript is { } callback) arguments = arguments.Add(callback);
        if (source.Mode != 3 && source.MusicOption is { } music) arguments = arguments.Add(music);
        return new(definition, source.Mode == 3 ? "trainerbattle_no_intro" : "trainerbattle_single",
            arguments, source.PostBattleScript, source.MusicOption);
    }

    private static RegisteredTrainerSource ProjectTrainerScript(PewterTrainerBattleDefinition source)
    {
        var definition = new ForestTrainerBattleDefinition(source.Trainer, source.Mode, source.BattleTypeFlags,
            source.ScriptEntry, source.IntroText, source.DefeatText, source.PostBattleText,
            source.EntryScript, source.Callback, source.Source);
        var arguments = ImmutableArray.Create(source.Trainer.Value[5..], source.IntroText, source.DefeatText);
        if (source.PostBattleScript is { } callback) arguments = arguments.Add(callback);
        if (source.MusicOption is { } music) arguments = arguments.Add(music);
        return new(definition, "trainerbattle_single", arguments, source.PostBattleScript, source.MusicOption);
    }

    private static RegisteredTrainerSource ProjectTrainerScript(Route22TrainerBattleDefinition source) =>
        new(new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, "Route22_Text_EarlyRivalIntro",
                source.DefeatText, "Route22_Text_EarlyRivalPostBattle", source.EntryScript, source.Callback, source.Source),
            "trainerbattle_earlyrival", [source.Trainer.Value[5..], source.RivalFlags.ToString(System.Globalization.CultureInfo.InvariantCulture),
                source.DefeatText, source.VictoryText]);

    private ForestTrainerBattleDefinition BoundTrainerScript(OrdinaryTrainerBinding binding, string? script, int? sourceLine) =>
        ResolveTrainerOccurrence(binding.Definition.Id, script, sourceLine).Source.Definition;

    private void RequireOrdinaryTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro, string fault)
    {
        if (noIntro || source.Mode != 0) throw new InvalidDataException(fault);
        RequireTrainerActorOccurrence(state, ResolveTrainerOccurrence(trainer, source.ScriptEntry));
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
