using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

// One compact integration pass over real public turns. Controlled learned moves are deliberate:
// this tests recipe/owner composition, not natural encounter or learnset availability.
internal static class SharedRecipeSmoke
{
    public static int Run(string contentRoot, string output)
    {
        Directory.CreateDirectory(output);
        var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(contentRoot);
        var catalog = proof.Catalog;
        var rows = new List<object>();
        int failures = 0, cases = 0, checks = 0, advances = 0;
        void Check(bool ok, string message) { checks++; if (!ok) throw new InvalidDataException(message); }
        WildBattleCombatant Mon(string species, int level, MoveId move, uint pid)
        {
            var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42,
                IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
            return new(p with { Moves = [new(move, catalog.Moves[move].Pp, catalog.Moves[move].Pp)] });
        }
        var splash = new MoveId("pret:MOVE_SPLASH");
        foreach (var move in catalog.Moves.Values.OrderBy(m => m.SourceMoveId))
        foreach (int actor in new[] { 0, 1, 2, 3 })
        {
            cases++;
            SourceBattleFrame? last = null;
            try
            {
                var bodies = Enumerable.Range(0, 4).Select(i => Mon("BLISSEY", 100, splash, (uint)i)).ToArray();
                bodies[actor] = Mon("SMEARGLE", 50, move.Id, 42) with { SpeedStage = 12 };
                WildBattleCalledPartyMember Member(WildBattleCombatant body) => new(body.Pokemon);
                var parties = Enumerable.Range(0, 2).Select(side => new SourceBattleParty((BattleSide)side,
                    new[] { Member(bodies[side]), Member(bodies[side + 2]) }
                    .Concat(Enumerable.Range(0, 2).Select(i => Member(Mon("BLISSEY", 100, splash, (uint)(10 + side + i)))))
                    .ToImmutableArray())).ToImmutableArray();
                last = new SourceBattleFrame
                {
                    Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
                    SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer,
                    Rng = new(31), Parties = parties, Field = new() { Terrain = WildBattleTerrain.Plain },
                    Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray(),
                    Rewards = new() { Context = new(SourceRewardBattleKind.OrdinaryTrainer, 42, "RED", "MAPSEC_ROUTE_1"),
                        Metadata = Enumerable.Range(0, parties[0].Members.Length).Select(i => new SourceRewardMetadata(new(BattleSide.Player, i), "RED", "MAPSEC_PALLET_TOWN", "ITEM_POKE_BALL", SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray() }
                };
                Check(SourceBattleKernel.SupportsRecipe(move), "recipe admission");
                Check(SourceBattleIdentityRules.Validate(last) is null, "prepared identity");
                var entry = SourceFourEntryAbilities.InitializeEntry(catalog, last, last.Revision);
                Check(entry.Succeeded, "actual initial entry: " + entry.Fault);
                last = entry.State;
                var choices = last.Slots.Select(cell =>
                {
                    var selected = catalog.Moves[cell.Body!.Pokemon.Moves[0].Move];
                    int target = selected.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? cell.Slot.Value : cell.Slot.Value ^ 1;
                    return new SourceActionSelection(cell.Occupant!.Value, SourceActionKind.Move, 0, new(target));
                }).ToImmutableArray();
                var r = SourceFourActionTurns.BeginActions(catalog, last, last.Revision, choices);
                var events = new List<SourceBattleEvent>();
                for (int step = 0; ; step++)
                {
                    Check(r.Succeeded, "public continuation: " + r.Fault);
                    last = r.State; events.AddRange(r.Events);
                    Check(SourceBattleIdentityRules.Validate(last) is null, "accepted identity: " + SourceBattleIdentityRules.Validate(last));
                    if (last.FourTurn is null || last.FourTurn.Phase == SourceFourActionPhase.BattleEnded) break;
                    Check(step < 64, "bounded turn continuation");
                    var token = new SourceTurnContinuation(last.Revision, last.EventSequence, last.NextAction, last.FourTurn.Phase);
                    if (last.FourTurn.Phase is SourceFourActionPhase.ActionSwitchPending or SourceFourActionPhase.ReplacementsPending)
                    {
                        var pending = last.PendingSwitches[0]; var incoming = new PartyMemberKey(pending.Slot.Side, pending.LegalPartyIndices[0]);
                        r = last.FourTurn.Phase == SourceFourActionPhase.ActionSwitchPending
                            ? SourceFourActionTurns.ChooseActionSwitch(catalog, last, token, pending.Slot, incoming)
                            : SourceFourActionTurns.ChooseReplacement(catalog, last, token, pending.Slot, incoming);
                    }
                    else if (last.Rewards?.Pending is { } learning)
                        r = SourceFourRewards.Learn(catalog, last, token, new(learning.Recipient, learning.Moves[0].Level, learning.Moves[0].Move, null));
                    else r = SourceFourActionTurns.Advance(catalog, last, token);
                    advances++;
                }
                Check(events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == move.Id)
                    || events.OfType<SourcePpCommandEvent>().Any(e => e.PpOwner.Value == actor && e.ChosenMove == move.Id), "selected move actually invoked or charged/called with its own PP");
                Check(last.Outcome != WildBattleOutcome.Ongoing || r.CompletedCleanTurn, "completed original turn");
                rows.Add(new { move = move.Id.Value, actor, passed = true, events = events.Count, rngCursor = last.Rng.Cursor, outcome = last.Outcome.ToString() });
            }
            catch (Exception e)
            {
                failures++;
                rows.Add(new { move = move.Id.Value, actor, passed = false, error = e.Message });
                File.WriteAllText(Path.Combine(output, $"failure-{move.SourceMoveId}-{actor}.json"), JsonSerializer.Serialize(new { error = e.ToString(), frame = last }));
                Console.WriteLine($"FAIL {move.Id.Value}/{actor}: {e.Message}");
            }
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { cases, checks, failures, advances, results = rows,
            scope = "All354 definitions in each actual actor slot, one real public trainer turn including required rewards/replacement continuations. Controlled movesets, one seed, no complete cross-state/AI/item/campaign/visual claim." }));
        Console.WriteLine($"{cases} actual public recipe turns; {checks} checks; {failures} failures");
        return failures == 0 ? 0 : 1;
    }
}
