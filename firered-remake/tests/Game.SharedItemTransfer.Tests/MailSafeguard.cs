using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

// Bounded current-catalog check, separate from the historical whole-result goldens.
internal static class MailSafeguard
{
    internal static int Run(SourceMechanicsCatalog proof, string output)
    {
        var catalog = proof.Catalog;
        int checks = 0;
        var cases = new List<object>();
        void Check(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException(message); }
        WildBattleCombatant Mon(string move, string item = "ITEM_NONE")
        {
            var id = new MoveId("pret:MOVE_" + move);
            var mon = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_SNORLAX"), 100, 42,
                IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
            return new(mon with { Moves = [new(id, catalog.Moves[id].Pp, catalog.Moves[id].Pp)] }, HeldItem: item);
        }
        WildBattleCalledPartyMember Member(WildBattleCombatant mon) => new(mon.Pokemon, mon.HeldItem);
        SourceBattleFrame Frame(SourceBattleLayout layout, int actor, string item, bool receipt)
        {
            int count = layout == SourceBattleLayout.Singles ? 2 : 4;
            var bodies = Enumerable.Range(0, count).Select(i => i == actor ? Mon("RECYCLE", item) : Mon("KNOCK_OFF")).ToArray();
            return new()
            {
                Layout = layout, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
                SourceMechanicsRulesVersion = SourceBattleKernel.Version,
                BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(31),
                SourceAbsentMask = (byte)(count == 2 ? 12 : 0), TurnStartAbsentMask = (byte)(count == 2 ? 12 : 0),
                Parties = [new(BattleSide.Player, bodies.Where((_, i) => i % 2 == 0).Select(Member).ToImmutableArray()),
                    new(BattleSide.Opponent, bodies.Where((_, i) => i % 2 == 1).Select(Member).ToImmutableArray())],
                Slots = Enumerable.Range(0, 4).Select(i => i < count
                    ? new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])
                    { Position = new() { UsedHeldItem = receipt && i == actor ? "ITEM_PERSIM_BERRY" : null } }
                    : new SourceBattleSlot(new(i), null, 0, null)).ToImmutableArray()
            };
        }
        string Permanent(SourceBattleFrame frame, int actor)
        {
            var member = frame.Slots[actor].Member!.Value;
            return WildBattleSourcePartyEffects.PersistentHeldItem(frame.Party(member.Side).Members[member.Index]);
        }
        SourceKernelResult Act(SourceBattleFrame frame, int actor, int target)
        {
            var result = SourceBattleKernel.ResolveAction(catalog, frame,
                new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
            Check(result.Succeeded, $"action {actor}->{target}: {result.Fault}");
            Check(SourceBattleIdentityRules.Validate(result.State) is null, "valid result identity");
            return result;
        }
        foreach (var layout in new[] { SourceBattleLayout.Singles, SourceBattleLayout.PresetDoubles })
        foreach (int actor in Enumerable.Range(0, layout == SourceBattleLayout.Singles ? 2 : 4))
        foreach (string item in new[] { "ITEM_RETRO_MAIL", "ITEM_CHOICE_BAND" })
        {
            bool mail = item == "ITEM_RETRO_MAIL";
            var initial = Frame(layout, actor, item, true);
            var knocked = Act(initial, actor ^ 1, actor);
            Check(knocked.Events.OfType<SourceHeldItemKnockedOffEvent>().Single().Item == item,
                "Knock Off still reports the actual Mail or ordinary item");
            Check(knocked.State.Slots[actor].Body is { HeldItem: "ITEM_NONE" } body && body.KnockedOffItem == item &&
                Permanent(knocked.State, actor) == item, "Knock Off remains temporary for Mail and ordinary items");
            Check(knocked.Trace.Length == 4, "Knock Off keeps its four source RNG draws");
            int pp = knocked.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp;
            var recycled = Act(knocked.State, actor, actor);
            var after = recycled.State.Slots[actor];
            Check(after.Body!.Pokemon.Moves[0].CurrentPp == pp - 1 &&
                recycled.Events.OfType<SourceMoveUsedEvent>().Single().Actor.Value == actor, "Recycle spends ordinary actor PP");
            Check(recycled.Trace.IsEmpty && recycled.State.Rng == knocked.State.Rng, "Recycle adds no RNG");
            Check(after.Body.KnockedOffItem == item, "original suppression marker remains");
            Check(after.Body.HeldItem == (mail ? "ITEM_NONE" : "ITEM_PERSIM_BERRY") &&
                Permanent(recycled.State, actor) == (mail ? item : "ITEM_PERSIM_BERRY"), "Mail stays attached; non-Mail Recycle still writes the item");
            Check(after.Position.UsedHeldItem == (mail ? "ITEM_PERSIM_BERRY" : null), "only successful Recycle consumes its position receipt");
            Check(mail ? recycled.Events.OfType<SourceMoveFailedEvent>().Single().Reason == "no-recyclable-item"
                : !recycled.Events.OfType<SourceMoveFailedEvent>().Any(), "Mail uses ordinary move failure");
            Check(!mail || !recycled.Events.OfType<SourceCalledPartyChangedEvent>().Any(e => e.Reason == "recycle-party-write"),
                "blocked Mail produces no permanent item write");
            var copy = JsonSerializer.Deserialize<SourceBattleFrame>(JsonSerializer.Serialize(knocked.State))!;
            Check(JsonSerializer.Serialize(Act(copy, actor, actor)) == JsonSerializer.Serialize(recycled),
                "diagnostic restoration preserves Mail guard and ordinary behavior");
            cases.Add(new { layout = layout.ToString(), actor, item, ppBefore = pp,
                ppAfter = after.Body.Pokemon.Moves[0].CurrentPp, permanentItem = Permanent(recycled.State, actor),
                receipt = after.Position.UsedHeldItem, failed = mail, rngDraws = recycled.Trace.Length });
        }
        Check(cases.Count == 12, "nonempty exact bounded cohort");
        File.WriteAllText(output, JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), checks, cases,
            scope = "BUG-08 remake safeguard: controlled actual Knock Off/Recycle actions on both singles and all doubles slots; no campaign, Mail UI or memory exploit claim." }));
        Console.WriteLine($"PASS Mail safeguard: {cases.Count} cases, {checks} assertions; runtime {Environment.Version}");
        return 0;
    }
}
