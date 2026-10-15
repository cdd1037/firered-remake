using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using Game.Domain.Rules;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Battle.FieldEffects;
[TestClass]
public abstract class FieldEffectFixture
{
    private protected global::Game.Domain.Pokemon.SourceMechanicsCatalog proof = default !;
    private protected global::Game.Domain.Pokemon.PokemonCatalog catalog = default !;
    private protected string tm = default !;
    private protected int turns = default !;
    private protected int vectors = default !;
    private protected (string, string, string)[] setters = default !;
    [TestInitialize]
    public void SetUp()
    {
        proof = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        catalog = proof.Catalog;
        tm = File.ReadAllText(Path.Combine(TestAssets.Source, "src/data/pokemon/tmhm_learnsets.h"));
        turns = 0;
        vectors = 0;
        setters = new[]
        {
            ("ABRA", "REFLECT", "reflect"),
            ("ABRA", "LIGHT_SCREEN", "light-screen"),
            ("LAPRAS", "MIST", "mist"),
            ("LAPRAS", "SAFEGUARD", "safeguard"),
            ("SQUIRTLE", "RAIN_DANCE", "rain"),
            ("SANDSHREW", "SANDSTORM", "sandstorm"),
            ("ODDISH", "SUNNY_DAY", "sun"),
            ("SEEL", "HAIL", "hail")
        };
    }

    private protected MoveId Id(string move) => new("pret:MOVE_" + move);
    private protected WildBattleCombatant Mon(string species, string move, uint personality = 0, int level = 50)
    {
        var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
        string block = Regex.Match(tm, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
        Assert.IsTrue(catalog.Species[p.Species].LevelUpMoves.Any(m => m.Move == Id(move) && m.Level <= level) || Regex.IsMatch(block, @"TM\d\d_" + move + @"\)"), "authentic retained move " + species + "/" + move);
        return new(p with { Moves = [new(Id(move), catalog.Moves[Id(move)].Pp, catalog.Moves[Id(move)].Pp)] });
    }

    private protected WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, WildBattleTransformationRules.PersistentStatus(m));
    private protected SourceBattleFrame Frame()
    {
        WildBattleCombatant[] m = [Mon("DROWZEE", "MEDITATE"), Mon("SLOWPOKE", "AMNESIA"), Mon("POOCHYENA", "HOWL"), Mon("RATICATE", "SCARY_FACE")];
        return new()
        {
            Layout = SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(31),
            Parties = [new(BattleSide.Player, [Member(m[0]), Member(m[2])]), new(BattleSide.Opponent, [Member(m[1]), Member(m[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, m[i])).ToImmutableArray()
        };
    }

    private protected SourceBattleFrame Rebind(SourceBattleFrame f, int slot, WildBattleCombatant body)
    {
        var c = f.Slots[slot];
        var key = c.Member!.Value;
        var party = f.Parties[(int)key.Side];
        return f with
        {
            Slots = f.Slots.SetItem(slot, c with { Body = body }),
            Parties = f.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, Member(body)) })
        };
    }

    private protected ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f) => f.Slots.Where(c => f.CanAct(c.Slot)).Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, new(c.Slot.Value == 1 ? 2 : c.Slot.Value == 3 ? 0 : c.Slot.Value ^ 1))).ToImmutableArray();
    private protected SourceFourActionResult Good(SourceFourActionResult r)
    {
        Assert.IsTrue(r.Succeeded, r.Fault ?? "accepted");
        Assert.IsTrue(SourceBattleIdentityRules.Validate(r.State)is null, "valid state " + SourceBattleIdentityRules.Validate(r.State));
        return r;
    }

    private protected SourceFourActionResult Next(SourceFourActionResult r) => Good(SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value));
    private protected (SourceFourActionResult Result, ImmutableArray<SourceBattleEvent> Events) Actions(SourceBattleFrame f)
    {
        var r = Good(SourceFourActionTurns.BeginActions(catalog, f, f.Revision, Choices(f)));
        var events = r.Events;
        while (r.State.FourTurn!.Phase == SourceFourActionPhase.Actions)
        {
            r = Next(r);
            events = events.AddRange(r.Events);
        }

        Assert.IsTrue(r.State.FourTurn.Phase == SourceFourActionPhase.EndTurn, "four-action end boundary");
        return (r, events);
    }

    private protected (SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events) Turn(SourceBattleFrame f)
    {
        var(r, e) = Actions(f);
        r = Next(r);
        Assert.IsTrue(r.CompletedCleanTurn, "complete real four-slot turn");
        turns++;
        return (r.State, e.AddRange(r.Events));
    }

    private protected int Timer(SourceBattleFrame f, int side, string effect) => effect switch
    {
        "reflect" => f.Sides[side].ReflectTurnsRemaining,
        "light-screen" => f.Sides[side].LightScreenTurnsRemaining,
        "mist" => f.Sides[side].MistTurnsRemaining,
        "safeguard" => f.Sides[side].SafeguardTurnsRemaining,
        "rain" => f.Field.RainTurnsRemaining,
        "sandstorm" => f.Field.SandstormTurnsRemaining,
        "sun" => f.Field.SunTurnsRemaining,
        _ => f.Field.HailTurnsRemaining
    };
    private protected SourceFieldRegisters Weather(string weather, int count) => weather switch
    {
        "rain" => new()
        {
            RainTurnsRemaining = count
        },
        "sandstorm" => new()
        {
            SandstormTurnsRemaining = count
        },
        "sun" => new()
        {
            SunTurnsRemaining = count
        },
        _ => new()
        {
            HailTurnsRemaining = count
        }
    };
    private protected void Atomic(SourceBattleFrame f)
    {
        var r = SourceFourActionTurns.BeginActions(catalog, f, f.Revision, Choices(f));
        Assert.IsTrue(!r.Succeeded && ReferenceEquals(f, r.State) && r.Events.IsEmpty && r.Trace.IsEmpty, "atomic boundary " + r.Fault);
    }

    private protected DamageResult Damage(SourceBattleFrame frame, BattlerSlot actor, BattlerSlot target, bool critical, int percent)
    {
        var move = catalog.Moves[frame.Get(actor).Body!.Pokemon.Moves[0].Move];
        var method = typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.WildBattle.ForestBattleDamage")!.GetMethod("Calculate", BindingFlags.Static | BindingFlags.NonPublic)!;
        var values = method.GetParameters().Select(p => p.DefaultValue).ToArray();
        var named = new Dictionary<string, object?>
        {
            ["catalog"] = catalog,
            ["user"] = frame.Get(actor).Body,
            ["target"] = frame.Get(target).Body,
            ["moveId"] = move.Id,
            ["critical"] = critical,
            ["randomPercent"] = percent,
            ["sourceDefinition"] = move,
            ["rockTunnelRules"] = true,
            ["mtMoonRules"] = true,
            ["reflect"] = frame.Sides[(int)target.Side].ReflectTurnsRemaining > 0,
            ["lightScreen"] = frame.Sides[(int)target.Side].LightScreenTurnsRemaining > 0,
            ["sourceDoubleBattle"] = frame.Layout == SourceBattleLayout.PresetDoubles,
            ["sourceDefenderCount"] = SourceMoveTargeting.CountSourcePresentDefenders(frame, target)
        };
        foreach (var parameter in method.GetParameters())
            if (named.TryGetValue(parameter.Name!, out var value))
                values[parameter.Position] = value;
        return (DamageResult)method.Invoke(null, values)!;
    }
}
