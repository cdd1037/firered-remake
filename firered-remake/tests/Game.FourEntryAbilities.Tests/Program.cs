using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length is not (3 or 4) || args.Length == 4 && args[3] != "--type-changes") return 2;
bool typeChangesOnly = args.Length == 4;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
string reference = args[1], output = args[2];
string tm = File.ReadAllText(Path.Combine(reference, "src/data/pokemon/tmhm_learnsets.h"));
string tutor = File.ReadAllText(Path.Combine(reference, "src/data/pokemon/tutor_learnsets.h"));
int checks = 0, groups = 0, failures = 0, turns = 0, entries = 0, replacements = 0;
var results = new List<object>();
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
void Test(string label, Action body) { groups++; try { body(); results.Add(new {label, passed = true}); Console.WriteLine("PASS " + label); } catch(Exception e) { failures++; results.Add(new {label, passed = false, error = e.ToString()}); Console.WriteLine("FAIL " + label + " " + e); } }
string Json<T>(T value) => JsonSerializer.Serialize(value);
MoveId Move(string name) => new("pret:MOVE_" + name);
WildBattleCombatant Mon(string species, string move, int level = 50, uint pid = 0)
{
    var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42,
        IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
    string tmBlock = Regex.Match(tm, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
    string tutorBlock = Regex.Match(tutor, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
    Check(catalog.Species[p.Species].LevelUpMoves.Any(m => m.Move == Move(move) && m.Level <= level) ||
        Regex.IsMatch(tmBlock, @"(?:TM|HM)\d\d_" + move + @"\)") || tutorBlock.Contains("TUTOR(MOVE_" + move + ")"), "authentic move " + species + "/" + move);
    Check(SourcePokemonValidator.ValidatePermanent(proof, p) is null, "authentic identity " + species);
    return new(p with {Moves = [new(Move(move), catalog.Moves[Move(move)].Pp, catalog.Moves[Move(move)].Pp)]});
}
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, m.Status);
SourceBattleFrame Frame(WildBattleCombatant[] mons, uint seed = 31, WildBattleCombatant[]? playerReserves = null)
{
    return new() {Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
        SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(seed),
        Parties = [new(BattleSide.Player, new[]{Member(mons[0]),Member(mons[2])}.Concat((playerReserves ?? []).Select(Member)).ToImmutableArray()),
                   new(BattleSide.Opponent, [Member(mons[1]), Member(mons[3])])],
        Slots = Enumerable.Range(0,4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, mons[i])).ToImmutableArray()};
}
SourceBattleFrame Plain() => Frame([Mon("PIDGEY", "TACKLE"), Mon("RATTATA", "TACKLE"), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")]);
SourceFourActionResult Success(SourceFourActionResult r)
{
    Check(r.Succeeded, r.Fault ?? "success"); Check(SourceBattleIdentityRules.Validate(r.State) is null, "valid result " + SourceBattleIdentityRules.Validate(r.State));
    return r;
}
SourceFourActionResult Entry(SourceBattleFrame f)
{
    var r = Success(SourceFourEntryAbilities.InitializeEntry(catalog, f, f.Revision)); entries++;
    uint seed = f.Rng.Seed; foreach (var draw in r.Trace) { seed = unchecked(seed * 1103515245 + 24691); Check(draw.Raw == (ushort)(seed >> 16), "entry exact LCG"); }
    Check(r.State.Rng.Seed == seed && r.State.Rng.Cursor == f.Rng.Cursor + (ulong)r.Trace.Length, "entry RNG cursor");
    Check(Json(f.Parties) == Json(r.State.Parties), "entry never changes permanent identity"); return r;
}
void Atomic(SourceBattleFrame f, SourceFourActionResult r, string label) =>
    Check(!r.Succeeded && ReferenceEquals(f,r.State) && r.Events.IsEmpty && r.Trace.IsEmpty, "atomic " + label + ": " + r.Fault);
SourceFourActionResult Begin(SourceBattleFrame f, int[]? targets = null)
{
    var choices = f.Slots.Where(c => f.CanAct(c.Slot)).Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, new(catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : targets?[c.Slot.Value] ?? (c.Slot.Value ^ 1)))).ToImmutableArray();
    return Success(SourceFourActionTurns.BeginActions(catalog, f, f.Revision, choices));
}
SourceFourActionResult Next(SourceFourActionResult r) => Success(SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value));
(SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events) Turn(SourceBattleFrame f, int[]? targets = null)
{
    var r = Begin(f, targets); var events = r.Events.ToBuilder();
    for(int i=0; i<20 && r.State.FourTurn is not null; i++) { r = Next(r); events.AddRange(r.Events); }
    Check(r.CompletedCleanTurn, "complete actual four-slot turn"); turns++; return (r.State, events.ToImmutable());
}
SourceFourActionResult Replace(SourceFourActionResult r, int slot, int index)
{
    var result = Success(SourceFourActionTurns.ChooseReplacement(catalog, r.State, r.Continuation!.Value, new(slot), new(BattleSide.Player,index)));
    replacements++; return result;
}
SourceFourActionResult KnockOutPlayers(WildBattleCombatant[] reserves)
{
    var f = Entry(Frame([Mon("PIDGEY","TACKLE",1), Mon("RATTATA","QUICK_ATTACK",100), Mon("PIDGEY","TACKLE",1), Mon("FEAROW","PECK",100)], playerReserves:reserves)).State;
    var r = Begin(f); while(r.State.FourTurn?.Phase == SourceFourActionPhase.Actions) r = Next(r);
    r = Next(r); Check(r.State.FourTurn?.Phase == SourceFourActionPhase.ReplacementsPending && r.State.PendingSwitches.Length == 2, "two genuine deferred KO entries"); return r;
}
if (!typeChangesOnly)
{
Test("official runtime and four authentic source positions", () => { Check(Environment.Version.Major == 10, "runtime10"); Check(catalog.Species.Count == 386 && catalog.Moves.Count == 354, "authenticated catalog"); Entry(Plain()); });
Test("competing entry weather uses one fastest-first sort then slower final setter", () => {
    var f = Frame([Mon("KYOGRE","CALM_MIND"),Mon("GROUDON","BULK_UP"),Mon("TYRANITAR","CRUNCH"),Mon("CASTFORM","TACKLE")]); var r = Entry(f);
    var order = r.Events.OfType<SourceOrderEvent>().Single().Order;
    var weather = r.Events.OfType<SourcePermanentWeatherChangedEvent>().ToArray();
    Check(weather.Select(e=>e.Side).SequenceEqual(order.Where(s=>s.Value!=3)), "actual setter event order");
    Check(r.State.Field.PermanentWeather?.Kind == SourcePermanentWeatherKind.Sandstorm, "slower Tyranitar wins");
    Check(r.State.Slots[3].Body!.CalledOverlay?.CastformForm == SourceCastformForm.Normal, "Forecast follows every weather script");
    var (after, _) = Turn(r.State); Check(after.Field.PermanentWeather?.Kind == SourcePermanentWeatherKind.Sandstorm, "real endturn retains ability weather");
});
Test("256 exact original-C initial tie-order and RNG vectors", () => {
    var same = Mon("CASTFORM","TACKLE");
    foreach(var row in File.ReadLines(Path.Combine(output,"source-order-vectors.csv")).Select(l=>l.Split(',').Select(uint.Parse).ToArray()).Where(r=>r[0]==0)) {
        var r = Entry(Frame([same,same,same,same],row[1]));
        Check(r.Events.OfType<SourceOrderEvent>().Single().Order.Select(s=>(uint)s.Value).SequenceEqual(row.Skip(2).Take(4)), "source initial permutation");
        Check(r.State.Rng.Seed==row[6] && (uint)r.Trace.Length==row[7], "source initial tie draws");
    }
});
Test("Intimidate settles numeric holders and both opposing positions with source stat immunities", () => {
    var r = Entry(Frame([Mon("GYARADOS","DRAGON_DANCE"),Mon("PIDGEY","TACKLE"),Mon("GYARADOS","DRAGON_DANCE"),Mon("RATTATA","TACKLE")]));
    Check(r.Events.OfType<SourceAbilityActivatedEvent>().Where(e=>e.Ability=="ABILITY_INTIMIDATE").Select(e=>e.Side.Value).SequenceEqual(new[]{0,2}), "numeric holder order");
    Check(r.State.Slots[1].Body!.AttackStage==4 && r.State.Slots[3].Body!.AttackStage==4, "both opposing positions twice");
    foreach(var (species,move,ability) in new[]{("TENTACRUEL","BARRIER","ABILITY_CLEAR_BODY"),("KINGLER","VICE_GRIP","ABILITY_HYPER_CUTTER"),("TORKOAL","EMBER","ABILITY_WHITE_SMOKE")}) {
        var immune=Mon(species,move); Check(immune.Pokemon.AbilityId==ability,"authentic immune identity");
        var a=Entry(Frame([Mon("GYARADOS","DRAGON_DANCE"),immune,Mon("PIDGEY","TACKLE"),Mon("RATTATA","TACKLE")]));
        Check(a.State.Slots[1].Body!.AttackStage==6 && a.State.Slots[3].Body!.AttackStage==5,"immune and vulnerable target");
        Check(a.Events.OfType<SourceAbilityPreventedEvent>().Single().Ability==ability,"specific stat immunity event");
        Turn(a.State);
    }
});
Test("Intimidate targets absence rather than HP while Trace donor query ignores absence", () => {
    var dead=Mon("PIDGEY","TACKLE"); dead=dead with{Pokemon=dead.Pokemon with{CurrentHp=0}};
    var f=Frame([Mon("GYARADOS","DRAGON_DANCE"),dead,Mon("SPEAROW","PECK"),Mon("RATTATA","TACKLE")]) with {SourceAbsentMask=8,TurnStartAbsentMask=8};
    var r=Entry(f);Check(r.State.Slots[1].Body!.AttackStage==5 && r.State.Slots[3].Body!.AttackStage==6,"source Intimidate predicates");
    var trace=Frame([Mon("PORYGON","TACKLE"),dead,Mon("SPEAROW","PECK"),Mon("GROUDON","BULK_UP")]) with {SourceAbsentMask=8,TurnStartAbsentMask=8};
    var t=Entry(trace);Check(WildBattleEffective.Ability(t.State.Slots[0].Body!)=="ABILITY_DROUGHT" && !t.Trace.Any(d=>d.Purpose=="wild-battle.trace.target"),"single living retained absent donor no draw");
});
Test("256 original-C Intimidate absence and HP predicate vectors", () => {
    var f=Frame([Mon("GYARADOS","DRAGON_DANCE"),Mon("PIDGEY","TACKLE"),Mon("SPEAROW","PECK"),Mon("RATTATA","TACKLE")]);
    foreach(var row in File.ReadLines(Path.Combine(output,"source-intimidate-vectors.csv")).Select(l=>l.Split(',').Select(int.Parse).ToArray())) {
        var current=f with{SourceAbsentMask=(byte)row[0],TurnStartAbsentMask=(byte)row[0]};
        // The entering actor must live; retained opposite bodies can have zero HP.
        foreach(int i in new[]{1,3}) if((row[1] & (1<<i))==0) {
            var cell=current.Slots[i];var dead=cell.Body! with{Pokemon=cell.Body.Pokemon with{CurrentHp=0}};var party=current.Party(cell.Slot.Side);
            current=current with{Slots=current.Slots.SetItem(i,cell with{Body=dead}),Parties=current.Parties.SetItem((int)cell.Slot.Side,party with{Members=party.Members.SetItem(cell.Member!.Value.Index,Member(dead))})};
        }
        var r=Entry(current);int mask=r.Events.OfType<SourceStatChangedEvent>().Aggregate(0,(m,e)=>m|(1<<e.Target.Value));
        Check(mask==row[2],"original Intimidate eligible target mask");
    }
});
Test("failed Trace selection remains pending without a target draw", () => {
    var dead=Mon("PIDGEY","TACKLE");dead=dead with{Pokemon=dead.Pokemon with{CurrentHp=0}};
    var r=Entry(Frame([Mon("PORYGON","TACKLE"),dead,Mon("CASTFORM","TACKLE"),dead]));
    Check(r.State.Slots[0].Body!.SourceTracePending && !r.Trace.Any(d=>d.Purpose=="wild-battle.trace.target"),"pending bit retained with no eligible donor");
    Check(!r.Events.OfType<SourceAbilityActivatedEvent>().Any(e=>e.Ability=="ABILITY_TRACE"),"no invented Trace activation");
});
Test("two Trace donors consume one draw and copied Drizzle never replays weather entry", () => {
    bool copied=false;
    for(uint seed=0;seed<16;seed++) {
        var f=Frame([Mon("PORYGON","TACKLE"),Mon("KYOGRE","CALM_MIND",70),Mon("CASTFORM","TACKLE"),Mon("GROUDON","BULK_UP",40)],seed);
        var r=Entry(f);Check(r.Trace.Count(d=>d.Purpose=="wild-battle.trace.target")==1,"two eligible donors exactly one draw");
        Check(r.State.Field.PermanentWeather?.Kind==SourcePermanentWeatherKind.Sun,"slower Groudon remains last setter");
        Check(r.Events.OfType<SourcePermanentWeatherChangedEvent>().Count()==2,"Trace copies without entry replay");
        Check(r.State.Party(BattleSide.Player).Members[0].Pokemon.AbilityId=="ABILITY_TRACE","permanent native Trace retained");
        if(WildBattleEffective.Ability(r.State.Slots[0].Body!)=="ABILITY_DRIZZLE") { copied=true; Turn(r.State); break; }
    }
    Check(copied,"authentic Drizzle donor selected");
    // Combined 7d1983e admission: copied abilities use the same newly shared cancellers.
    foreach(var (species,move,ability) in new[]{("SLAKOTH","SCRATCH","ABILITY_TRUANT"),("PIKACHU","THUNDER_SHOCK","ABILITY_STATIC")}) {
        var donor=Mon(species,move);
        var initial=Entry(Frame([Mon("PORYGON","TACKLE"),donor,Mon("CASTFORM","TACKLE"),donor])).State;
        Check(WildBattleEffective.Ability(initial.Slots[0].Body!)==ability,"actual copied admitted ability "+ability);
        var (after,_) = Turn(initial);
        if(ability=="ABILITY_TRUANT") {
            Check(after.Slots[0].Body!.TruantLoafing,"copied Truant reaches shared endturn state");
            var (_,events) = Turn(after);
            Check(events.OfType<SourceMoveFailedEvent>().Any(e=>e.Actor.Value==0 && e.Reason=="truant"),"copied Truant uses shared next-action canceller");
        }
    }
    var asleep=Mon("PORYGON","TACKLE") with{Status=3};var truant=Mon("SLAKOTH","SCRATCH");
    var sleepingEntry=Entry(Frame([asleep,truant,Mon("CASTFORM","TACKLE"),truant])).State;
    var (sleepAfter,sleepEvents)=Turn(sleepingEntry);
    Check(sleepAfter.Slots[0].Body!.Status==2 && sleepAfter.Slots[0].Body!.TruantLoafing,"sleep countdown and copied Truant endturn both retained");
    Check(sleepEvents.OfType<SourceActionBlockedEvent>().Any(e=>e.Side.Value==0 && WildBattleStatus.IsSleeping(e.Status)),"entry overlay does not bypass sleep canceller");
});
Test("real four-slot weather move updates Forecast immediately and expiry settles after residuals", () => {
    var f=Entry(Frame([Mon("CASTFORM","RAIN_DANCE"),Mon("RATTATA","TACKLE"),Mon("SPEAROW","PECK"),Mon("DODUO","PECK")])).State;
    var (a,events)=Turn(f);Check(a.Slots[0].Body!.CalledOverlay?.CastformForm==SourceCastformForm.Rainy,"rain script Forecast");
    var set=events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="rain" && e.Reason=="move");
    Check(events.OfType<SourceCalledOverlayChangedEvent>().Single().Sequence>set.Sequence,"form callback follows weather change");
    a=a with{Field=a.Field with{RainTurnsRemaining=1}};
    var r=Begin(a);while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions)r=Next(r);
    r=Next(r);Check(r.CompletedCleanTurn && r.State.Slots[0].Body!.CalledOverlay?.CastformForm==SourceCastformForm.Normal,"expiry Forecast at settled boundary"); turns++;
});
Test("retained Forecast resumes after a living weather suppressor faints and leaves its body", () => {
    var cloud=Mon("GOLDUCK","SCRATCH",50,1);Check(cloud.Pokemon.AbilityId=="ABILITY_CLOUD_NINE","actual suppressor");cloud=cloud with{Pokemon=cloud.Pokemon with{CurrentHp=1}};
    var f=Frame([cloud,Mon("RATTATA","QUICK_ATTACK",100),Mon("CASTFORM","TACKLE"),Mon("MAGIKARP","TACKLE")]) with{Field=new(){PermanentWeather=new(SourcePermanentWeatherKind.Sun)}};
    f=Entry(f).State;Check(f.Slots[2].Body!.CalledOverlay is null,"suppressed initial Castform stays Normal");
    var r=Next(Begin(f));
    Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==0 && r.State.Slots[2].Body!.CalledOverlay?.CastformForm==SourceCastformForm.Sunny,
        "no required entry: Forecast settles immediately after suppressor faints");
    Check(r.Events.OfType<SourceCalledOverlayChangedEvent>().Any(e=>e.Side.Value==2),"real settled Forecast event");
    while(r.State.FourTurn is not null) r=Next(r);
    Check(r.CompletedCleanTurn && r.State.IsSourceAbsent(new(0)),"retained fainted suppressor becomes absent at sole endturn boundary"); turns++;
});
Test("two real KO replacements defer Intimidate then Trace and preserve occupant identity", () => {
    var r=KnockOutPlayers([Mon("GYARADOS","DRAGON_DANCE"),Mon("PORYGON","TACKLE")]);var first=r.State;
    r=Replace(r,0,2);Check(!r.Events.OfType<SourceAbilityActivatedEvent>().Any(),"first entry arms but does not settle Intimidate");
    Check(r.State.EntryAbilities!.PendingIntimidateMask==1 && r.State.Slots[0].EntrySerial==2,"pending and new occupant token");
    Check(r.State.Queue.Single(q=>q.Slot.Value==0).Kind==SourceActionKind.Cancelled,"incoming never inherits queued action");
    var saved=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;
    var direct=SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,new(2),new(BattleSide.Player,3));
    var resumed=SourceFourActionTurns.ChooseReplacement(catalog,saved,r.Continuation!.Value,new(2),new(BattleSide.Player,3));
    Check(Json(direct)==Json(resumed),"exact pending-entry JSON resume");r=Success(direct);replacements++;
    Check(r.Events.OfType<SourceAbilityActivatedEvent>().Select(e=>e.Ability).SequenceEqual(new[]{"ABILITY_INTIMIDATE","ABILITY_TRACE"}),"all entries then Intimidate before Trace");
    Check(r.Trace.Count(d=>d.Purpose=="wild-battle.trace.target")==1,"replacement Trace donor draw");
    Check(r.State.Slots[1].Body!.AttackStage==5 && r.State.Slots[3].Body!.AttackStage==5,"both enemies affected after replacement");
    Check(r.State.Party(BattleSide.Player).Members[3].Pokemon.AbilityId=="ABILITY_TRACE","reserve permanent identity retained");
    Atomic(r.State,SourceFourActionTurns.ChooseReplacement(catalog,r.State,new(first.Revision,first.EventSequence,first.NextAction,SourceFourActionPhase.ReplacementsPending),new(2),new(BattleSide.Player,3)),"stale replacement");
    r=Next(r);Check(r.CompletedCleanTurn,"replacement continues sole endturn cursor");turns++;
});
Test("clean KO weather entry updates retained Forecast before readiness", () => {
    var f=Entry(Frame([Mon("PIDGEY","TACKLE",1),Mon("RATTATA","QUICK_ATTACK",100),Mon("CASTFORM","TACKLE"),Mon("MAGIKARP","TACKLE")],playerReserves:[Mon("KYOGRE","CALM_MIND")])).State;
    var r=Begin(f);while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions)r=Next(r);r=Next(r);r=Replace(r,0,2);
    Check(r.State.Field.PermanentWeather?.Kind==SourcePermanentWeatherKind.Rain && r.State.Slots[2].Body!.CalledOverlay?.CastformForm==SourceCastformForm.Rainy,"shared actual replacement callback");
    Check(r.Events[0] is SourceCalledPartySwitchedEvent,"switch event precedes entry callback");r=Next(r);Check(r.CompletedCleanTurn,"sole resume reaches ready");turns++;
});
Test("initial and replacement rejection and overflow leave state events and RNG untouched", () => {
    var f=Frame([Mon("KYOGRE","CALM_MIND"),Mon("GROUDON","BULK_UP"),Mon("PORYGON","TACKLE"),Mon("CASTFORM","TACKLE")]);
    foreach(var bad in new[]{f with{EventSequence=long.MaxValue-1},f with{Revision=long.MaxValue},f with{Rng=f.Rng with{Cursor=ulong.MaxValue}},f with{Sides=f.Sides.SetItem(0,new(BattleSide.Player){Spikes=1})},f with{Slots=f.Slots.SetItem(0,f.Slots[0] with{Position=f.Slots[0].Position with{FocusBanded=true}})}})
        Atomic(bad,SourceFourEntryAbilities.InitializeEntry(catalog,bad,bad.Revision),"initial boundary");
    var once=Entry(f).State;Atomic(once,SourceFourEntryAbilities.InitializeEntry(catalog,once,once.Revision),"initial replay");
    Atomic(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,f.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(c.Slot.Value^1))).ToImmutableArray()),"uninitialized initial holders");
    var pending=KnockOutPlayers([Mon("KYOGRE","CALM_MIND"),Mon("PORYGON","TACKLE")]);
    var badEntry=pending.State with{EventSequence=long.MaxValue-1};
    Atomic(badEntry,SourceFourActionTurns.ChooseReplacement(catalog,badEntry,new(badEntry.Revision,badEntry.EventSequence,badEntry.NextAction,badEntry.FourTurn!.Phase),new(0),new(BattleSide.Player,2)),"replacement event overflow");
});
}

if (typeChangesOnly)
{
    WildBattleCombatant Moves(string species, int level, params string[] names)
    {
        var body = Mon(species, names[0], level);
        foreach (var name in names.Skip(1)) Mon(species, name, level);
        return body with { Pokemon = body.Pokemon with { Moves = names.Select(n => new PokemonMoveSlot(Move(n), catalog.Moves[Move(n)].Pp, catalog.Moves[Move(n)].Pp)).ToImmutableArray() } };
    }
    void VerifyTrace(SourceBattleFrame before, SourceFourActionResult r)
    {
        uint seed = before.Rng.Seed;
        foreach (var draw in r.Trace) { seed = unchecked(seed * 1103515245 + 24691); Check(draw.Raw == (ushort)(seed >> 16), "actual LCG"); }
        Check(r.State.Rng.Seed == seed && r.State.Rng.Cursor == before.Rng.Cursor + (ulong)r.Trace.Length, "actual cursor");
    }
    SourceFourActionResult Finish(SourceFourActionResult r)
    {
        for (int i = 0; i < 30 && r.State.FourTurn is not null; i++) r = Next(r);
        Check(r.CompletedCleanTurn, "type-change complete turn"); turns++; return r;
    }
    SourceFourActionResult Switch(SourceBattleFrame f, int slot, int reserve)
    {
        var choices = f.Slots.Select(c => c.Slot.Value == slot
            ? new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Switch, null, null, new(c.Slot.Side, reserve))
            : new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, new(catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1))).ToImmutableArray();
        return Finish(Success(SourceFourActionTurns.BeginActions(catalog, f, f.Revision, choices)));
    }
    Test("authentic spread Color Change updates both opposing positions and retains source identity", () => {
        var f = Entry(Frame([Mon("BLASTOISE", "SURF", 40), Mon("KECLEON", "TAIL_WHIP", 100), Mon("METAPOD", "HARDEN", 20), Mon("KECLEON", "TAIL_WHIP", 100)])).State;
        var result = Turn(f);
        var colors = result.Events.OfType<SourceAbilityActivatedEvent>().Where(e => e.Ability == "ABILITY_COLOR_CHANGE").ToArray();
        Check(colors.Select(e => e.Side.Value).Order().SequenceEqual(new[]{1,3}), "both exact recipients");
        foreach (var color in colors) {
            var change = result.Events.OfType<SourceCalledOverlayChangedEvent>().Single(e => e.Sequence == color.Sequence + 1);
            Check(change.Side == color.Side && change.After is {Type1:"TYPE_WATER", Type2:"TYPE_WATER"}, "adjacent activation overlay");
            Check(WildBattleEffective.Types(catalog,result.State.Get(color.Side).Body!) == ("TYPE_WATER", "TYPE_WATER"), "retained live type");
            Check(result.State.Party(BattleSide.Opponent).Members[color.Side.Value / 2].Pokemon.AbilityId == "ABILITY_COLOR_CHANGE", "permanent ability");
        }
        Check(result.State.Slots.All(c => c.Position.HitItemReceipt is null), "ActionFinished clears receipts");
        var restored = JsonSerializer.Deserialize<SourceBattleFrame>(Json(result.State))!;
        Check(Json(restored) == Json(result.State), "type overlay JSON round trip");
        Turn(restored);
    });
    Test("Conversion retains slot rejection RNG, Trace overlay, and real switch lifetime", () => {
        var f = Entry(Frame([Moves("PORYGON", 50, "CONVERSION", "PSYBEAM"), Mon("METAPOD", "HARDEN", 20), Mon("METAPOD", "HARDEN", 20), Mon("METAPOD", "HARDEN", 20)], playerReserves:[Mon("METAPOD", "HARDEN", 20)])).State;
        var begin = Begin(f); var r = Next(begin); VerifyTrace(begin.State, r);
        Check(r.Events.OfType<SourceCalledOverlayChangedEvent>().Single().Side == new BattlerSlot(0), "actor overlay");
        Check(r.State.Slots[0].Body!.CalledOverlay is { Type1:"TYPE_PSYCHIC", Type2:"TYPE_PSYCHIC", Ability:"ABILITY_SHED_SKIN" }, "Conversion preserves copied Trace ability");
        Check(r.Trace.Any(d => d.Purpose.EndsWith(".conversion-slot")) && r.Trace.Where(d => d.Purpose.EndsWith(".conversion-slot")).Last().Raw % 4 == 1, "original move-position sampling");
        r = Finish(r); var away = Switch(r.State, 0, 2); var back = Switch(away.State, 0, 0);
        Check(back.State.Slots[0].Body!.CalledOverlay is {Type1:null,Type2:null,Ability:"ABILITY_SHED_SKIN"}, "switch clears types and reruns Trace");
        Check(back.State.Party(BattleSide.Player).Members[0].Pokemon.AbilityId == "ABILITY_TRACE", "Trace permanent identity");
    });
    Test("Conversion2 consumes actual slot-three hit history and one approved candidate draw", () => {
        var f = Entry(Frame([Mon("METAPOD", "HARDEN", 20), Mon("METAPOD", "HARDEN", 20), Mon("PORYGON", "CONVERSION_2", 20), Mon("PIKACHU", "THUNDER_SHOCK", 25)], seed:19)).State;
        var r = Begin(f, [0,1,2,2]); bool saw = false;
        while(r.State.FourTurn?.Phase == SourceFourActionPhase.Actions) {
            var before = r.State; r = Next(r); VerifyTrace(before,r);
            if (r.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == Move("CONVERSION_2"))) {
                Check(before.Slots[2].Registers.LastHitBy == new BattlerSlot(3) && before.Slots[2].Registers.History?.LastHitByType == "TYPE_ELECTRIC", "actual last attacker position and type");
                var draw = r.Trace.Single(d => d.Purpose.EndsWith(".conversion2-candidate"));
                var chosen = WildBattleSourcePartyEffects.Conversion2Candidates("TYPE_ELECTRIC", "TYPE_NORMAL", "TYPE_NORMAL")[draw.Raw % WildBattleSourcePartyEffects.Conversion2Candidates("TYPE_ELECTRIC", "TYPE_NORMAL", "TYPE_NORMAL").Length];
                Check(r.State.Slots[2].Body!.CalledOverlay is {} overlay && overlay.Type1 == chosen && overlay.Type2 == chosen, "one approved integer candidate selection"); saw = true;
            }
        }
        Check(saw, "Conversion2 executed"); Finish(r);
    });
    Test("Conversion and Conversion2 failures consume PP without candidate RNG", () => {
        foreach (var move in new[]{"CONVERSION","CONVERSION_2"}) {
            var f = Entry(Frame([Mon("PORYGON",move,50), Mon("METAPOD","HARDEN",20),Mon("METAPOD","HARDEN",20),Mon("METAPOD","HARDEN",20)])).State;
            var r = Next(Begin(f));
            Check(r.Events.OfType<SourceMoveFailedEvent>().Single().Reason == (move == "CONVERSION" ? "no-different-move-type" : "no-landed-move"), "normal failure");
            Check(!r.Trace.Any(d => d.Purpose.Contains("conversion")), "no candidate draw");
            Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp == catalog.Moves[Move(move)].Pp - 1, "PP retained on failure"); Finish(r);
        }
    });
    Test("Trace copies Color Change then activates without rewriting permanent ability", () => {
        var f = Entry(Frame([Mon("PORYGON","RECOVER",100),Mon("KECLEON","PSYBEAM",30),Mon("METAPOD","HARDEN",20),Mon("KECLEON","TAIL_WHIP",100)])).State;
        Check(f.Slots[0].Body!.CalledOverlay?.Ability == "ABILITY_COLOR_CHANGE", "source donor copied");
        var r = Turn(f, [1,0,2,2]);
        Check(r.Events.OfType<SourceAbilityActivatedEvent>().Any(e => e.Side == new BattlerSlot(0) && e.Ability == "ABILITY_COLOR_CHANGE"), "copied ability triggered");
        Check(r.State.Slots[0].Body!.CalledOverlay is {Type1:"TYPE_PSYCHIC",Type2:"TYPE_PSYCHIC",Ability:"ABILITY_COLOR_CHANGE"}, "overlay fields coexist");
        Check(r.State.Slots[0].Body!.Pokemon.AbilityId == "ABILITY_TRACE", "permanent Trace unchanged");
    });
    Test("canonical singles adapters match shared Conversion and Conversion2 state events and RNG", () => {
        foreach (string name in new[]{"CONVERSION","CONVERSION_2"}) foreach (uint seed in Enumerable.Range(0,32).Select(i=>(uint)i)) {
            var actor = Moves("PORYGON",50,name,"PSYBEAM"); var target = Mon("METAPOD","HARDEN",30);
            if(name=="CONVERSION_2") actor=actor with {LastLandedMove=Move("EMBER"),LastHitBy=WildBattleSide.Opponent,SourceMoveHistory=new(){LastHitByType="TYPE_FIRE"}};
            var single = new WildBattleState(actor,target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion) {
                Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,
                CalledParty=new([Member(actor)],[Member(target)],0,0)};
            var imported = SinglesBattleAdapter.Import(catalog,single); Check(imported.Succeeded,"parity import "+imported.Fault); var frame=imported.Frame!;
            var legacy = WildBattleRules.ResolveSourceMoveAction(catalog,single,new(WildBattleSide.Player,0)).Battle;
            var shared = SourceBattleKernel.ResolveAction(catalog,frame,new(frame.Slots[0].Occupant!.Value,frame.Revision,0,new(0)));
            Check(legacy.Succeeded && shared.Succeeded,"parity success "+legacy.Fault+"/"+shared.Fault);
            var exported = SinglesBattleAdapter.Export(shared.State); Check(exported.Succeeded,"parity export "+exported.Fault);
            Check(Json(exported.State)==Json(legacy.State),"whole singles state parity "+name+" seed="+seed);
            Check(Json(shared.Events.Select(SourceBattleEventAdapter.Export).ToArray())==Json(legacy.Events),"whole singles events parity "+name);
            Check(Json(shared.Trace)==Json(legacy.Trace),"whole singles RNG parity "+name);
        }
    });
    Test("malformed type overlays and unmigrated callers remain atomically closed", () => {
        var f = Entry(Plain()).State;
        foreach (var overlay in new WildBattleCalledOverlay[]{new(){Type1="TYPE_FIRE"},new(){Type1="TYPE_MYSTERY",Type2="TYPE_MYSTERY"},new(){Type1="TYPE_FIRE",Type2="TYPE_WATER"},new(){Type1="TYPE_FIRE",Type2="TYPE_FIRE",Ability="ABILITY_COLOR_CHANGE"},new(){Type1="TYPE_FIRE",Type2="TYPE_FIRE",CastformForm=SourceCastformForm.Sunny}}) {
            var bad = f with {Slots=f.Slots.SetItem(0,f.Slots[0] with {Body=f.Slots[0].Body! with {CalledOverlay=overlay}})};
            Atomic(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,bad.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(c.Slot.Value^1))).ToImmutableArray()),"malformed overlay");
        }
        foreach (var bad in new[]{
            f with {Slots=f.Slots.SetItem(0,f.Slots[0] with {Registers=f.Slots[0].Registers with {History=new(){LastHitByType="TYPE_INVALID"}}})},
            f with {Slots=f.Slots.SetItem(0,f.Slots[0] with {Body=f.Slots[0].Body! with {LastLandedMove=Move("UNKNOWN")}})}}) {
            var rejected=SourceBattleKernel.ResolveAction(catalog,bad,new(bad.Slots[0].Occupant!.Value,bad.Revision,0,new(1)));
            Check(rejected.Fault=="source_kernel_type_change_history" && ReferenceEquals(rejected.State,bad) && rejected.Events.IsEmpty && rejected.Trace.IsEmpty,"invalid type history atomic");
        }
        Check(!SourceBattleKernel.SupportsRecipe(catalog.Moves[Move("TRANSFORM")]) && !SourceBattleKernel.SupportsRecipe(catalog.Moves[Move("METRONOME")]) && !SourceBattleKernel.SupportsRecipe(catalog.Moves[Move("ROLE_PLAY")]), "caller and transform admission unchanged");
    });
}

var summary=new {passed=failures==0,groups,checks,failures,entries,turns,replacements,runtime=Environment.Version.ToString(),results};
Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"RESULT groups={groups} checks={checks} entries={entries} turns={turns} replacements={replacements} failures={failures} runtime={Environment.Version}");
return failures==0?0:1;
