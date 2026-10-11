using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

internal static class FourHustleChecks
{
    internal static void Run(string root, string output)
    {
        if (File.Exists(output)) throw new ArgumentException("Keep prior results; output must be new.");
        var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(root); var catalog = proof.Catalog;
        int checks = 0, cases = 0; var observations = new List<object>();
        void Check(bool good, string why) { checks++; if (!good) throw new Exception(why); }
        MoveId Id(string s) => new("pret:MOVE_" + s);
        WildBattleCombatant Mon(string name, string move, int level = 70, uint pid = 0)
        {
            var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + name), level, 42,
                IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
            Check(catalog.Species[p.Species].LevelUpMoves.Any(m => m.Move == Id(move) && m.Level <= level), "natural learned move " + name + "/" + move);
            return new(p with { Moves = [new(Id(move), catalog.Moves[Id(move)].Pp, catalog.Moves[Id(move)].Pp)] });
        }
        WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, m.Status);
        SourceBattleFrame Frame(WildBattleCombatant actor, uint seed, int slot = 2)
        {
            var mons = new[] { Mon("DROWZEE", "MEDITATE", 45), Mon("DROWZEE", "MEDITATE", 100),
                Mon("DROWZEE", "MEDITATE", 50), Mon("DROWZEE", "MEDITATE", 90) }; mons[slot] = actor;
            return new() { Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
                SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(seed),
                Parties = [new(BattleSide.Player, [Member(mons[0]), Member(mons[2])]), new(BattleSide.Opponent, [Member(mons[1]), Member(mons[3])])],
                Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, mons[i])).ToImmutableArray() };
        }
        uint SeedFor(int roll)
        {
            for (uint i = 0; i < 10000; i++) if (((unchecked(i * 1103515245u + 24691u) >> 16) % 100) + 1 == roll) return i;
            throw new Exception("seed not found");
        }
        SourceKernelResult Act(WildBattleCombatant actor, uint seed, int slot = 2)
        {
            var f = Frame(actor, seed, slot);
            var r = SourceBattleKernel.ResolveAction(catalog, f, new(f.Slots[slot].Occupant!.Value, f.Revision, 0, new(slot ^ 1)));
            Check(r.Succeeded, "actual Hustle action " + r.Fault);
            Check(SourceBattleIdentityRules.Validate(r.State) is null, "valid permanent and live result");
            Check(r.State.Slots[slot].Body!.Pokemon.AbilityId == actor.Pokemon.AbilityId, "ability identity remains canonical");
            Check(r.State.Slots[slot].Body!.Pokemon.Moves[0].CurrentPp == actor.Pokemon.Moves[0].CurrentPp - 1, "one actual PP debit");
            observations.Add(new { slot, move = actor.Pokemon.Moves[0].Move.Value, seed, r.Trace, r.Events });
            return r;
        }
        foreach (int slot in new[] { 0, 1, 2, 3 })
        {
            var actor = Mon("CORSOLA", "TACKLE", pid: 0);
            Check(actor.Pokemon.AbilityId == "ABILITY_HUSTLE", "natural species ability");
            foreach (int roll in new[] { 76, 77 })
            {
                var r = Act(actor, SeedFor(roll), slot);
                Check(r.Events.OfType<SourceMoveMissedEvent>().Any() == (roll == 77), "95*80/100 physical cutoff in actual slot");
                Check(r.Trace[0].Raw % 100 + 1 == roll, "independent raw seed cutoff");
            }
            var special = Act(Mon("CORSOLA", "BUBBLE", pid: 0), SeedFor(99), slot);
            Check(!special.Events.OfType<SourceMoveMissedEvent>().Any(), "special type unaffected by Hustle accuracy");
        }
        cases++;
        // Gen III's source test is type-based, even when power is zero.
        var charm = Mon("TOGETIC", "CHARM", pid: 0);
        Check(charm.Pokemon.AbilityId == "ABILITY_HUSTLE", "natural Togetic Hustle");
        foreach (int roll in new[] { 80, 81 })
        {
            var r = Act(charm, SeedFor(roll));
            Check(r.Events.OfType<SourceMoveMissedEvent>().Any() == (roll == 81), "physical-type status cutoff preserved");
            Check(r.Events.OfType<SourceStatChangedEvent>().Any() == (roll == 80), "only successful Charm lowers actual target");
        }
        // Controlled legal move registers exercise an otherwise not naturally paired
        // ability/status move. This is not a campaign learnset or Metronome claim.
        var powder = charm with { Pokemon = charm.Pokemon with { Moves = [new(Id("POISON_POWDER"), 35, 35)] } };
        foreach (int roll in new[] { 60, 61 })
        {
            var r = Act(powder, SeedFor(roll));
            Check(r.Events.OfType<SourceMoveMissedEvent>().Any() == (roll == 61), "primary poison shares 75*80/100 command");
            Check(r.State.Slots[3].Body!.Status == (roll == 60 ? WildBattleStatus.Poison : 0u), "controlled primary status result");
        }
        cases++;
        // A real complete turn with the same natural donor at both player positions.
        var frame = Frame(Mon("CORSOLA", "TACKLE", pid: 0), 31);
        var left = Mon("CORSOLA", "BUBBLE", 65, 0); var cell = frame.Slots[0];
        frame = frame with { Slots = frame.Slots.SetItem(0, cell with { Body = left }),
            Parties = frame.Parties.SetItem(0, frame.Parties[0] with { Members = frame.Parties[0].Members.SetItem(0, Member(left)) }) };
        var choices = frame.Slots.Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0,
            new(catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target == "MOVE_TARGET_USER" ? c.Slot.Value : c.Slot.Value ^ 1))).ToImmutableArray();
        var step = SourceFourActionTurns.BeginActions(catalog, frame, frame.Revision, choices);
        var events = new List<SourceBattleEvent>(); int advances = 0;
        while (true)
        {
            Check(step.Succeeded, "full turn " + step.Fault); Check(SourceBattleIdentityRules.Validate(step.State) is null, "valid turn phase");
            events.AddRange(step.Events); if (step.CompletedCleanTurn) break;
            Check(++advances < 16, "bounded complete turn"); step = SourceFourActionTurns.Advance(catalog, step.State, step.Continuation!.Value);
        }
        Check(events.OfType<SourceMoveUsedEvent>().Select(e => e.Actor.Value).Order().SequenceEqual(new[] { 0, 1, 2, 3 }), "four real actors each act once");
        Check(step.State.Parties[0].Members.All(m => m.Pokemon.AbilityId == "ABILITY_HUSTLE"), "party projection retains both natural abilities");
        var stale = SourceFourActionTurns.BeginActions(catalog, step.State, frame.Revision, choices);
        Check(!stale.Succeeded && ReferenceEquals(stale.State, step.State) && stale.Events.IsEmpty && stale.Trace.IsEmpty, "stale turn is atomic");
        observations.Add(new { completeTurn = step.State, events }); cases++;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { passed = true, cases, checks, runtime = Environment.Version.ToString(), observations }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"PASS {cases} Hustle cases / {checks} checks; all four positions and one actual full turn");
    }
}
