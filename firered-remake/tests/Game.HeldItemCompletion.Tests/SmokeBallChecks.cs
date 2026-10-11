using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

internal static class SmokeBallChecks
{
    internal static void Run(SourceMechanicsCatalog proof, string root, Action<bool, string> check, Action<string, Action> test)
    {
        var catalog = proof.Catalog;
        WildBattleCombatant Mon(string item = "ITEM_NONE", string species = "MAGIKARP", string move = "SPLASH", int level = 30, uint pid = 0)
        {
            var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42,
                IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
            var id = new MoveId("pret:MOVE_" + move);
            return new(p with { Moves = [new(id, catalog.Moves[id].Pp, catalog.Moves[id].Pp)] }, HeldItem: item);
        }
        WildBattleState Good(WildBattleResult r)
        {
            check(r.Succeeded, r.Fault ?? "run accepted");
            check(WildBattleRules.Validate(catalog, r.State) is null, "run result validates");
            return r.State!;
        }
        WildBattleState Start(WildBattleCombatant p, WildBattleCombatant? o = null, params WildBattleCombatant[] reserves) =>
            Good(WildBattleRules.StartSourceWild(proof, BattleRulesProfile.CanonicalSourceSingles, [p, ..reserves], 0, o ?? Mon(level: 80), new(31)));
        WildBattleRunAttemptEvent Escape(WildBattleState before, bool afterFaint = false)
        {
            string frozen = JsonSerializer.Serialize(before);
            var r = afterFaint ? WildBattleRules.TryRunAfterFaint(catalog, before) : WildBattleRules.Run(catalog, before);
            var state = Good(r);
            var attempt = r.Events.OfType<WildBattleRunAttemptEvent>().Single();
            check(state.Outcome == WildBattleOutcome.Escaped && !state.AwaitingReplacement, "escaped and replacement cleared");
            check(attempt.Escaped && attempt.Threshold is null && attempt.Roll is null, "guaranteed item escape without roll");
            check(attempt.AttemptsAfter == before.RunAttempts && state.RunAttempts == before.RunAttempts, "attempt counter preserved");
            check(!r.Trace.Any(t => t.Purpose == "wild-battle.run"), "no escape RNG draw");
            check(JsonSerializer.Serialize(attempt).Contains("\"EscapingItem\":\"ITEM_SMOKE_BALL\""), "source item flee metadata");
            check(state.Player.HeldItem == "ITEM_SMOKE_BALL" && state.CalledParty!.Player[0].HeldItem == "ITEM_SMOKE_BALL" &&
                state.CalledParty.PlayerUsedHeldItem is null, "item retained and not recyclable");
            check(JsonSerializer.Serialize(WildBattleTransformationRules.PersistentPokemon(state.Player)) ==
                JsonSerializer.Serialize(WildBattleTransformationRules.PersistentPokemon(before.Player)), "permanent Pokemon unchanged");
            check(JsonSerializer.Serialize(before) == frozen, "input immutable");
            check(!r.Events.OfType<WildBattleHeldItemConsumedEvent>().Any(), "no consumption event");
            check(JsonSerializer.Serialize<WildBattleEvent>(SourceBattleEventAdapter.Export(SourceBattleEventAdapter.Import(attempt))) ==
                JsonSerializer.Serialize<WildBattleEvent>(attempt), "item metadata survives both adapters");
            if (afterFaint) check(r.Trace.IsEmpty && state.Rng == before.Rng, "after-faint escape has no RNG");
            return attempt;
        }
        test("Smoke Ball bypasses all six ordinary run traps without consumption", () =>
        {
            foreach (string trap in new[] { "shadow_tag", "arena_trap", "magnet_pull", "mean_look", "rooted", "binding" })
            {
                var foe = trap switch {
                    "shadow_tag" => Mon(species: "WOBBUFFET", move: "SAFEGUARD"),
                    "arena_trap" => Mon(species: "DIGLETT", move: "GROWL", pid: 1),
                    "magnet_pull" => Mon(species: "MAGNEMITE", move: "SUPERSONIC"), _ => Mon(level: 80) };
                WildBattleState State(string item)
                {
                    var s = Start(trap == "magnet_pull" ? Mon(item, "MAGNEMITE", "TACKLE") : Mon(item), foe);
                    var player = trap switch {
                        "mean_look" => s.Player with { MeanLookSource = WildBattleSide.Opponent },
                        "rooted" => s.Player with { Rooted = true },
                        "binding" => s.Player with { BindTurnsRemaining = 3, BindingMove = new("pret:MOVE_WRAP") }, _ => s.Player };
                    return s with { Player = player, RunAttempts = 7 };
                }
                var blocked = State("ITEM_NONE");
                string frozen = JsonSerializer.Serialize(blocked);
                var rejected = WildBattleRules.Run(catalog, blocked);
                check(rejected.Fault == "wild_battle_run_" + trap && ReferenceEquals(rejected.State, blocked) && rejected.Trace.IsEmpty && rejected.Events.IsEmpty,
                    "same trap rejects ordinary runner: " + trap + " / " + rejected.Fault);
                check(JsonSerializer.Serialize(blocked) == frozen, "rejected run unchanged");
                Escape(State("ITEM_SMOKE_BALL"));
            }
        });
        test("Smoke Ball precedes Run Away and remains an authenticated canonical-only item", () =>
        {
            var runaway = Mon("ITEM_SMOKE_BALL", "RATTATA", "TAIL_WHIP");
            check(runaway.Pokemon.AbilityId == "ABILITY_RUN_AWAY", "source Run Away fixture");
            Escape(Start(runaway));
            var normal = Start(Mon());
            var r = WildBattleRules.Run(catalog, normal);
            var e = r.Events.OfType<WildBattleRunAttemptEvent>().Single();
            check(!JsonSerializer.Serialize(e).Contains("EscapingItem"), "old null event bytes remain absent");
            check(e.AttemptsAfter == 1 && e.Roll is not null && r.Trace.Count(t => t.Purpose == "wild-battle.run") == 1, "normal slower runner retains attempt and one draw");
            var enigma = WildBattleRules.StartSourceWild(proof, BattleRulesProfile.CanonicalSourceSingles, [Mon("ITEM_ENIGMA_BERRY")], 0, Mon(), new(31));
            check(!enigma.Succeeded, "dynamic Enigma stays unsupported");
            var historical = WildBattleRules.StartSourceWild(catalog, [Mon("ITEM_SMOKE_BALL")], 0, Mon(), new(31));
            check(!historical.Succeeded, "noncanonical profile remains rejected");
        });
        test("fainted Smoke Ball holder exits through real public faint and No branch", () =>
        {
            var holder = Mon("ITEM_SMOKE_BALL"); holder = holder with { Pokemon = holder.Pokemon with { CurrentHp = 1 } };
            var state = Start(holder, Mon(species: "MAGIKARP", move: "TACKLE", level: 80), Mon());
            var fainted = Good(WildBattleRules.ResolveMove(catalog, state, 0));
            check(fainted.Player.Pokemon.CurrentHp == 0 && fainted.AwaitingReplacement && fainted.CalledParty!.Player.Length == 2, "real faint awaits living reserve");
            Escape(fainted, afterFaint: true);
        });
        test("Smoke Ball flee page uses exact authenticated source message", () =>
        {
            var text = PretBattleText.Load(root);
            var pages = PretBattleText.Pages(text, "sText_PkmnFledUsingIts", new Dictionary<string, string> {
                ["PLAY_SE SE_FLEE"] = "", ["B_ATK_NAME_WITH_PREFIX"] = "MAGIKARP", ["B_LAST_ITEM"] = "SMOKE BALL" });
            check(pages.SequenceEqual(new[] { "MAGIKARP fled\nusing its SMOKE BALL!" }) && text["sText_PkmnFledUsingIts"].EndsWith("\\p", StringComparison.Ordinal),
                "source flee text and final input wait");
        });
    }
}
