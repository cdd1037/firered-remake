using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using FireRed.Tests;

namespace FireRed.Tests.Battle.Turns;
[TestClass, TestCategory("Game")]
public sealed class AllMoveRecipeTests
{
    public static IEnumerable<object[]> Recipes
    {
        get
        {
            var catalog = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root)).Catalog;
            if (catalog.Moves.Count != 354)
                throw new InvalidDataException("Complete 354-move inventory required");
            foreach (var move in catalog.Moves.Values.OrderBy(m => m.SourceMoveId))
                foreach (int actor in new[]
                {
                    0,
                    1,
                    2,
                    3
                }

                )
                    yield return[move.Id.Value, actor];
        }
    }

    [TestMethod]
    [DynamicData(nameof(Recipes))]
    public void SelectedMoveCompletesPublicTurn(string moveId, int actor)
    {
        var proof = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        var catalog = proof.Catalog;
        var move = catalog.Moves[new MoveId(moveId)];
        SourceBattleFrame? last = null;
        WildBattleCombatant Mon(string species, int level, MoveId move, uint pid)
        {
            var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
            return new(p with { Moves = [new(move, catalog.Moves[move].Pp, catalog.Moves[move].Pp)] });
        }

        var splash = new MoveId("pret:MOVE_SPLASH");
        var bodies = Enumerable.Range(0, 4).Select(i => Mon("BLISSEY", 100, splash, (uint)i)).ToArray();
        bodies[actor] = Mon("SMEARGLE", 50, move.Id, 42)with
        {
            SpeedStage = 12
        };
        WildBattleCalledPartyMember Member(WildBattleCombatant body) => new(body.Pokemon);
        var parties = Enumerable.Range(0, 2).Select(side => new SourceBattleParty((BattleSide)side, new[] { Member(bodies[side]), Member(bodies[side + 2]) }.Concat(Enumerable.Range(0, 2).Select(i => Member(Mon("BLISSEY", 100, splash, (uint)(10 + side + i))))).ToImmutableArray())).ToImmutableArray();
        last = new SourceBattleFrame
        {
            Layout = SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(31),
            Parties = parties,
            Field = new()
            {
                Terrain = WildBattleTerrain.Plain
            },
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray(),
            Rewards = new()
            {
                Context = new(SourceRewardBattleKind.OrdinaryTrainer, 42, "RED", "MAPSEC_ROUTE_1"),
                Metadata = Enumerable.Range(0, parties[0].Members.Length).Select(i => new SourceRewardMetadata(new(BattleSide.Player, i), "RED", "MAPSEC_PALLET_TOWN", "ITEM_POKE_BALL", SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()
            }
        };
        Assert.IsTrue(SourceBattleKernel.SupportsRecipe(move), "recipe admission");
        Assert.IsTrue(SourceBattleIdentityRules.Validate(last)is null, "prepared identity");
        var entry = SourceFourEntryAbilities.InitializeEntry(catalog, last, last.Revision);
        Assert.IsTrue(entry.Succeeded, "actual initial entry: " + entry.Fault);
        last = entry.State;
        var choices = last.Slots.Select(cell =>
        {
            var selected = catalog.Moves[cell.Body!.Pokemon.Moves[0].Move];
            int target = selected.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? cell.Slot.Value : cell.Slot.Value ^ 1;
            return new SourceActionSelection(cell.Occupant!.Value, SourceActionKind.Move, 0, new(target));
        }).ToImmutableArray();
        var r = SourceFourActionTurns.BeginActions(catalog, last, last.Revision, choices);
        var events = new List<SourceBattleEvent>();
        for (int step = 0;; step++)
        {
            Assert.IsTrue(r.Succeeded, "public continuation: " + r.Fault);
            last = r.State;
            events.AddRange(r.Events);
            Assert.IsTrue(SourceBattleIdentityRules.Validate(last)is null, "accepted identity: " + SourceBattleIdentityRules.Validate(last));
            if (last.FourTurn is null || last.FourTurn.Phase == SourceFourActionPhase.BattleEnded)
                break;
            Assert.IsTrue(step < 64, "bounded turn continuation");
            var token = new SourceTurnContinuation(last.Revision, last.EventSequence, last.NextAction, last.FourTurn.Phase);
            if (last.FourTurn.Phase is SourceFourActionPhase.ActionSwitchPending or SourceFourActionPhase.ReplacementsPending)
            {
                var pending = last.PendingSwitches[0];
                var incoming = new PartyMemberKey(pending.Slot.Side, pending.LegalPartyIndices[0]);
                r = last.FourTurn.Phase == SourceFourActionPhase.ActionSwitchPending ? SourceFourActionTurns.ChooseActionSwitch(catalog, last, token, pending.Slot, incoming) : SourceFourActionTurns.ChooseReplacement(catalog, last, token, pending.Slot, incoming);
            }
            else if (last.Rewards?.Pending is { } learning)
                r = SourceFourRewards.Learn(catalog, last, token, new(learning.Recipient, learning.Moves[0].Level, learning.Moves[0].Move, null));
            else
                r = SourceFourActionTurns.Advance(catalog, last, token);
        }

        Assert.IsTrue(events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == move.Id) || events.OfType<SourcePpCommandEvent>().Any(e => e.PpOwner.Value == actor && e.ChosenMove == move.Id), "selected move actually invoked or charged/called with its own PP");
        Assert.IsTrue(last.Outcome != WildBattleOutcome.Ongoing || r.CompletedCleanTurn, "completed original turn");
    }
}
