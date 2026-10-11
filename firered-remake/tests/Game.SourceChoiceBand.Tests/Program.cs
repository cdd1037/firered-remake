using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

if (args.Length != 3) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
string source = args[1], evidence = args[2];
string tm = File.ReadAllText(Path.Combine(source, "src/data/pokemon/tmhm_learnsets.h"));
string tutor = File.ReadAllText(Path.Combine(source, "src/data/pokemon/tutor_learnsets.h"));
int groups = 0, checks = 0, failures = 0, turns = 0, vectors = 0;
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
void Test(string label, Action action) { groups++; try { action(); Console.WriteLine("PASS " + label); } catch (Exception ex) { failures++; Console.WriteLine("FAIL " + label + " " + ex); } }
string Json<T>(T value) => JsonSerializer.Serialize(value);
MoveId Move(string name) => new("pret:MOVE_" + name);
WildBattleCombatant Mon(string species, string[] moves, string item = "ITEM_NONE", int level = 50, uint pid = 0)
{
    var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
    foreach (var name in moves)
    {
        bool learned = catalog.Species[p.Species].LevelUpMoves.Any(m => m.Move == Move(name) && m.Level <= level);
        string tmBlock = Regex.Match(tm, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
        string tutorBlock = Regex.Match(tutor, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
        Check(learned || Regex.IsMatch(tmBlock, @"TM\d\d_" + name + @"\)") || tutorBlock.Contains("TUTOR(MOVE_" + name + ")"), "authentic move " + species + "/" + name);
    }
    return new(p with { Moves = moves.Select(name => new PokemonMoveSlot(Move(name), catalog.Moves[Move(name)].Pp, catalog.Moves[Move(name)].Pp)).ToImmutableArray() }, HeldItem: item);
}
WildBattleState Require(WildBattleResult r, string label)
{
    Check(r.Succeeded, label + ": " + r.Fault);
    Check(WildBattleRules.Validate(catalog, r.State) is null, label + " output validates: " + WildBattleRules.Validate(catalog, r.State));
    return r.State!;
}
WildBattleResult StartResult(WildBattleCombatant p, WildBattleCombatant o, uint seed = 31, params WildBattleCombatant[] reserves) =>
    WildBattleRules.StartSourceWild(proof, BattleRulesProfile.CanonicalSourceSingles, [p, .. reserves], 0, o, new(seed));
WildBattleState Start(WildBattleCombatant p, WildBattleCombatant o, uint seed = 31, params WildBattleCombatant[] reserves) => Require(StartResult(p,o,seed,reserves), "source entry");
WildBattleResult Turn(WildBattleState s, int slot = 0)
{
    var result = WildBattleRules.ResolveMove(catalog, s, slot); var next = Require(result, "whole turn"); turns++;
    Check(next.Outcome != WildBattleOutcome.Ongoing || next.CalledParty?.PendingSwitch is not null || next.Turn == s.Turn + 1, "actual Ready completed");
    return result;
}
WildBattleState Restore(WildBattleState s)
{
    var clone = JsonSerializer.Deserialize<WildBattleState>(Json(s))!;
    Check(Json(clone) == Json(s) && WildBattleRules.Validate(catalog, clone) is null, "canonical save round trip"); return clone;
}
const string Band = "ITEM_CHOICE_BAND";
var items = typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceBattleHeldItems")!;
var finish = items.GetMethod("FinishChoice", BindingFlags.Static | BindingFlags.NonPublic)!;
var physical = items.GetMethod("PhysicalAttack", BindingFlags.Static | BindingFlags.NonPublic)!;
MoveId? FromSource(int id) => id == 0 ? null : id == 65535 ? new("pret:MOVE_UNAVAILABLE") : catalog.Moves.Values.Single(m => m.SourceMoveId == id).Id;
int SourceId(MoveId? id) => id is null ? 0 : id.Value.Value == "pret:MOVE_UNAVAILABLE" ? 65535 : catalog.Moves[id.Value].SourceMoveId;
WildBattleResult Action(WildBattleState s, WildBattleSide side, int slot = 0)
{
    var r = WildBattleRules.ResolveSourceMoveAction(catalog, s, new(side,slot)).Battle;
    Require(r,"source action"); return r;
}
var calculate = typeof(WildBattleRules).Assembly.GetType("Game.Domain.WildBattle.ForestBattleDamage")!.GetMethod("Calculate", BindingFlags.Static | BindingFlags.NonPublic)!;
DamageResult Damage(WildBattleCombatant actor, WildBattleCombatant target, string name, bool critical = false, int percent = 100)
{
    var move = catalog.Moves[Move(name)];
    var inputs = calculate.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
    var values = new Dictionary<string, object?> { ["catalog"] = catalog, ["user"] = actor, ["target"] = target, ["moveId"] = move.Id,
        ["critical"] = critical, ["randomPercent"] = percent, ["sourceDefinition"] = move };
    foreach (string flag in new[] {"pewterRules","earlyStarterRules","earlyPartyRules","route22Rules","route3Rules","mtMoonRules","ceruleanRules","billRules","vermilionRules","ssAnneRules","vermilionGymRules","route11Rules","rockTunnelRules"}) values[flag] = true;
    foreach(var p in calculate.GetParameters()) if(values.TryGetValue(p.Name!,out var value)) inputs[p.Position] = value;
    return (DamageResult)calculate.Invoke(null,inputs)!;
}
Test("exact original Choice item, move-end and u16 Attack: 1540 vectors",()=>{
    Check(Environment.Version.Major==10,"official runtime10");
    using var proofJson=JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence,"choice-source-proof.json")));
    Check(proofJson.RootElement.GetProperty("sourceCommit").GetString()==catalog.SourceCommit,"pinned original source");
    var definition=items.GetField("ChoiceBand",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
    Check((int)definition.GetType().GetProperty("SourceItemId")!.GetValue(definition)! ==186 &&
        (string)definition.GetType().GetProperty("HoldEffect")!.GetValue(definition)! =="HOLD_EFFECT_CHOICE_BAND" &&
        (int)definition.GetType().GetProperty("Parameter")!.GetValue(definition)! ==0,"exact source item identity");
    var mon=Mon("PIDGEY",["TACKLE"]);int rows=0;
    foreach(string line in File.ReadLines(Path.Combine(evidence,"choice-vectors.csv"))) {
        var n=line.Split(',').Skip(1).Select(int.Parse).ToArray();
        if(line.StartsWith("A,")) {
            Check((int)physical.Invoke(null,[n[0],"ITEM_NONE"])! == n[1],"no modifier");
            Check((int)physical.Invoke(null,[n[0],Band])! == n[2],"original u16 attack "+line);
        } else {
            int present=n[5] switch {0=>45,1=>33,2=>226,_=>0};
            var moves=present==0?ImmutableArray<PokemonMoveSlot>.Empty:[new(FromSource(present)!.Value,1,1)];
            var before=mon with {HeldItem=n[1]==1?Band:"ITEM_NONE",SourceChoiceMove=FromSource(n[2]),Pokemon=mon.Pokemon with{Moves=moves}};
            var result=(WildBattleCombatant)finish.Invoke(null,[before,FromSource(n[3])??new MoveId("pret:MOVE_NONE"),n[0]==1,n[4]==1])!;
            Check(SourceId(result.SourceChoiceMove)==n[6] && n[7]==7,"original move-end "+line);
        }
        rows++;vectors++;
    }
    Check(rows==1540,"complete exact source grid");
});
Test("512 complete original CalculateBaseDamage vectors",()=>{
    using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence,"choice-damage-vectors.json")));
    Check(data.RootElement.GetProperty("sourceCommit").GetString()==catalog.SourceCommit,"damage pinned");int rows=0;
    var a=Mon("PIDGEY",["TACKLE"],level:37);var t=Mon("PSYDUCK",["SCRATCH"],level:37);
    foreach(var row in data.RootElement.GetProperty("vectors").EnumerateArray()) {
        int I(string k)=>row.GetProperty(k).GetInt32();bool B(string k)=>row.GetProperty(k).GetBoolean();
        var actor=a with {HeldItem=B("band")?Band:"ITEM_NONE",AttackBadgeBoost=B("badge"),AttackStage=I("stageA"),SpecialAttackStageDelta=I("stageA")-6,
            Status=B("burn")?WildBattleStatus.Burn:0,Pokemon=a.Pokemon with {AbilityId=row.GetProperty("ability").GetString()!,Stats=a.Pokemon.Stats with {Attack=I("attack"),SpecialAttack=I("attack")}}};
        var target=t with {DefenseStage=I("stageD"),SpecialDefenseStageDelta=I("stageD")-6,Pokemon=t.Pokemon with {Stats=t.Pokemon.Stats with {Defense=I("defense"),SpecialDefense=I("defense")}},CalledOverlay=new(){Type1="TYPE_MYSTERY",Type2="TYPE_MYSTERY"}};
        // Controlled registers deliberately remove STAB and type effects, as does the source C base-damage function.
        actor=actor with {CalledOverlay=new(){Type1="TYPE_MYSTERY",Type2="TYPE_MYSTERY"}};
        var result=Damage(actor,target,B("special")?"EMBER":"TACKLE",B("critical"),I("percent"));
        Check(result.Damage==I("damage"),"full damage vector "+row);rows++;vectors++;
    }
    Check(rows==512,"full damage grid");
});
Test("real turn locks chosen status move and rejects another atomically",()=>{
    var start=Start(Mon("PIDGEY",["SAND_ATTACK","TACKLE"],Band),Mon("MAGIKARP",["SPLASH"],level:100));
    var next=Turn(start).State!;Check(next.Player.SourceChoiceMove==Move("SAND_ATTACK"),"status move locks");
    var rejected=WildBattleRules.ResolveMove(catalog,Restore(next),1);
    Check(!rejected.Succeeded && rejected.Trace.IsEmpty && rejected.Events.IsEmpty && Json(rejected.State)==Json(next),"rejected choice is atomic "+rejected.Fault);
    Check(!WildBattleRules.IsMoveSelectable(next,WildBattleSide.Player,1),"UI selection agrees");
    Check(Turn(next).State!.Player.SourceChoiceMove==Move("SAND_ATTACK"),"existing lock survives");
});
Test("last PP forces real Struggle without replacing lock",()=>{
    var p=Mon("PIDGEY",["SAND_ATTACK","TACKLE"],Band);p=p with {Pokemon=p.Pokemon with {Moves=p.Pokemon.Moves.SetItem(0,p.Pokemon.Moves[0] with{CurrentPp=1})}};
    var next=Turn(Start(p,Mon("MAGIKARP",["SPLASH"],level:100))).State!;
    Check(WildBattleRules.ValidateSourceMoveSelection(catalog,next,WildBattleSide.Player,-1) is null,"choice exhausted");
    next=Turn(next,-1).State!;Check(next.Player.SourceChoiceMove==Move("SAND_ATTACK"),"forced struggle preserves original choice");
});
Test("real physical boost, special and fixed exclusion with same RNG",()=>{
    foreach(var fixture in new[]{("PIDGEY","TACKLE",true),("PIKACHU","THUNDER_SHOCK",false),("MACHOP","SEISMIC_TOSS",false)}) {
        var p=Mon(fixture.Item1,[fixture.Item2],level:20);var o=Mon("MAGIKARP",["SPLASH"],level:100);
        var plain=Turn(Start(p,o,73));var band=Turn(Start(p with{HeldItem=Band},o,73));
        int plainLoss=o.Pokemon.CurrentHp-plain.State!.Opponent.Pokemon.CurrentHp,bandLoss=o.Pokemon.CurrentHp-band.State!.Opponent.Pokemon.CurrentHp;
        Check(fixture.Item3?bandLoss>plainLoss:bandLoss==plainLoss,"source damage category "+fixture.Item2);
        Check(Json(plain.Trace)==Json(band.Trace),"no extra Choice RNG");
    }
});
Test("pre-obedience sleep stays unlocked; obeyed failure locks",()=>{
    var p=Mon("PIDGEY",["SAND_ATTACK"],Band) with{Status=3};var s=Turn(Start(p,Mon("MAGIKARP",["SPLASH"]))).State!;
    Check(s.Player.SourceChoiceMove is null,"asleep unchosen");
    s=Turn(Start(Mon("MAGIKARP",["SPLASH"],Band),Mon("MAGIKARP",["SPLASH"]))).State!;
    Check(s.Player.SourceChoiceMove==Move("SPLASH"),"failed Splash still obeys");
    var hit=Start(Mon("PIDGEY",["TACKLE"],Band),Mon("GASTLY",["HYPNOSIS"],level:100));
    var r=Action(hit,WildBattleSide.Player);Check(r.State!.Player.SourceChoiceMove==Move("TACKLE"),"immune hit locks");
});
Test("Transform command sentinel on success and failure",()=>{
    var s=Start(Mon("DITTO",["TRANSFORM"],Band),Mon("DITTO",["TRANSFORM"]));
    s=Action(s,WildBattleSide.Opponent).State!;
    var failed=Action(s,WildBattleSide.Player);
    Check(failed.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason=="target-transformed") && failed.State!.Player.SourceChoiceMove is null,"failed Transform does not lock");
    var success=Turn(Start(Mon("DITTO",["TRANSFORM"],Band),Mon("MAGIKARP",["SPLASH"])));
    Check(success.State!.Player.Transform is not null && success.State.Player.SourceChoiceMove is null,"successful Transform sentinel");
    var copied=Turn(success.State);Check(copied.State!.Player.SourceChoiceMove==Move("SPLASH"),"copied current move then locks");
});
Test("Mimic and Sketch command clears unavailable chosen",()=>{
    foreach(var fixture in new[]{("MR_MIME","MIMIC"),("SMEARGLE","SKETCH")}) {
        var s=Start(Mon(fixture.Item1,[fixture.Item2],Band),Mon("MAGIKARP",["SPLASH"]));
        s=Action(s,WildBattleSide.Opponent).State!;s=Action(s,WildBattleSide.Player).State!;
        Check(s.Player.SourceChoiceMove is null && s.Player.Pokemon.Moves[0].Move==Move("SPLASH"),"copy chosen sentinel "+fixture.Item2);
        s=Turn(s).State!;Check(s.Player.SourceChoiceMove==Move("SPLASH"),"copied move locks next turn");
    }
});
Test("Trick delayed acquiring actor stays unlocked; losing actor clears",()=>{
    var s=Start(Mon("ALAKAZAM",["TRICK","KINESIS"]),Mon("MAGIKARP",["SPLASH"],Band));
    s=Turn(s).State!;Check(s.Player.HeldItem==Band && s.Player.SourceChoiceMove is null && s.Opponent.HeldItem=="ITEM_NONE","Trick acquiring current turn unlocked");
    s=Turn(s,1).State!;Check(s.Player.SourceChoiceMove==Move("KINESIS"),"acquirer next turn locks");
    var reverse=Turn(Start(Mon("ALAKAZAM",["TRICK"],Band),Mon("MAGIKARP",["SPLASH"])));
    Check(reverse.State!.Player.SourceChoiceMove is null && reverse.State.Opponent.SourceChoiceMove==Move("SPLASH"),"queued target received Band before its move");
});
Test("successful Knock Off and Thief clear victim lock",()=>{
    foreach(var fixture in new[]{("FARFETCHD","KNOCK_OFF"),("MEOWTH","THIEF")}) {
        var s=Start(Mon(fixture.Item1,[fixture.Item2],level:65),Mon("MAGIKARP",["SPLASH"],Band,100));
        s=Action(s,WildBattleSide.Opponent).State!;Check(s.Opponent.SourceChoiceMove is not null,"victim first locked");
        s=Action(s,WildBattleSide.Player).State!;Check(s.Opponent.HeldItem=="ITEM_NONE" && s.Opponent.SourceChoiceMove is null,"removed victim choice "+fixture.Item2);
        Check(s.Player.SourceChoiceMove is null,"acquiring thief not locked to current move");
    }
});
Test("switch and live forgotten-slot update clear choice",()=>{
    var reserve=Mon("RATTATA",["TACKLE"],pid:1);var s=Turn(Start(Mon("PIDGEY",["SAND_ATTACK","TACKLE"],Band),Mon("MAGIKARP",["SPLASH"]),31,reserve)).State!;
    var replaced=WildBattleRules.SwitchSourceWildPlayer(catalog,s,1);
    s=Require(replaced,"voluntary source switch");Check(s.Player.SourceChoiceMove is null,"incoming clean choice");
    var mon=Mon("PIDGEY",["SAND_ATTACK","TACKLE"],Band) with{SourceChoiceMove=Move("SAND_ATTACK")};
    var learned=mon.Pokemon with {Moves=mon.Pokemon.Moves.SetItem(0,new(Move("GUST"),35,35))};
    Check(WildBattleTransformationRules.ApplyPersistentPokemon(mon,learned).SourceChoiceMove is null,"forgotten move clears");
});
Test("caller, locked continuation and Beat Up share choice boundary",()=>{
    var s=Start(Mon("CLEFAIRY",["METRONOME"],Band),Mon("MAGIKARP",["SPLASH"],level:100));
    var called=WildBattleRules.ResolveCalledMoveAction(catalog,s,new(WildBattleSide.Player,0,Move("TACKLE")));
    Require(called.Battle,"supplied authentic caller result");Check(called.State!.Player.SourceChoiceMove==Move("METRONOME"),"original caller chosen lock");
    s=Start(Mon("JIGGLYPUFF",["ROLLOUT"],Band,25),Mon("MAGIKARP",["SPLASH"],level:100));
    s=Turn(s).State!;Check(s.Player.SourceChoiceMove==Move("ROLLOUT") && (s.Player.RolloutMoveSlot is not null || s.Player.CalledTurnState?.Lock is not null),"real locked move starts");
    s=Turn(s).State!;Check(s.Player.SourceChoiceMove==Move("ROLLOUT"),"continuation retains choice");
    var a=Mon("SNEASEL",["BEAT_UP"],level:57);var o=Mon("MAGIKARP",["SPLASH"],level:100);var reserve=Mon("PIDGEY",["TACKLE"],pid:2);
    var plain=Turn(Start(a,o,91,reserve));var band=Turn(Start(a with{HeldItem=Band},o,91,reserve));
    Check(plain.State!.Opponent.Pokemon.CurrentHp==band.State!.Opponent.Pokemon.CurrentHp && Json(plain.Trace)==Json(band.Trace),"Beat Up species formula unboosted");
    Check(band.State.Player.SourceChoiceMove==Move("BEAT_UP"),"Beat Up locks at shared end");
});
Test("Baton success resumes new battler without lock; failure locks",()=>{
    var a=Mon("EEVEE",["BATON_PASS"],Band);var o=Mon("MAGIKARP",["SPLASH"]);
    var failed=Turn(Start(a,o));Check(failed.State!.Player.SourceChoiceMove==Move("BATON_PASS"),"failed Baton locks");
    var pending=Turn(Start(a,o,31,Mon("PIDGEY",["TACKLE"],Band,pid:1))).State!;
    Check(pending.CalledParty?.PendingSwitch?.Kind==WildBattleCalledSwitchKind.BatonPass,"real Baton suspended");
    var r=WildBattleRules.ChooseSourceWildParty(catalog,pending,1);
    var next=Require(r,"Baton continuation");Check(next.Player.SourceChoiceMove is null && next.Player.HeldItem==Band,"incoming Band does not acquire Baton lock");
});
Test("Pursuit interception has no new OBEYS choice",()=>{
    var s=Start(Mon("PIDGEY",["SAND_ATTACK"],level:70),Mon("RATTATA",["PURSUIT"],Band,30),31,Mon("PIDGEY",["SAND_ATTACK"],level:70,pid:1));
    var r=WildBattleRules.SwitchSourceWildPlayer(catalog,s,1);Require(r,"Pursuit switch");
    Check(r.Events.OfType<WildBattlePursuitInterceptedEvent>().Any() && r.State!.Opponent.SourceChoiceMove is null,"interception does not establish lock");
    var next=Turn(r.State!).State!;Check(next.Opponent.SourceChoiceMove==Move("PURSUIT"),"next ordinary action locks");
});
Test("confusion uses common Band Attack and remains pre-obedience",()=>{
    var p=Mon("PIDGEY",["TACKLE"],level:50);var o=Mon("MAGIKARP",["SPLASH"],level:100);bool observed=false;
    for(uint seed=0;seed<16 && !observed;seed++) {
        var s=Start(p,o,seed);s=s with {Player=s.Player with {ConfusionTurnsRemaining=3}};
        var b=Start(p with{HeldItem=Band},o,seed);b=b with {Player=b.Player with {ConfusionTurnsRemaining=3}};
        var ordinary=Turn(s);var boosted=Turn(b);var hit=ordinary.Events.OfType<WildBattleConfusionSelfHitEvent>().SingleOrDefault();
        if(hit is null) continue;
        var bandHit=boosted.Events.OfType<WildBattleConfusionSelfHitEvent>().Single();
        Check(bandHit.Damage>hit.Damage && boosted.State!.Player.SourceChoiceMove is null,"Band confusion boost without choice");
        Check(Json(ordinary.Trace)==Json(boosted.Trace),"confusion no extra RNG");observed=true;
    }
    Check(observed,"actual confusion self-hit observed");
});
Test("canonical-only state and item admission remain atomic",()=>{
    var p=Mon("PIDGEY",["TACKLE"],Band);var o=Mon("MAGIKARP",["SPLASH"]);
    var forged=StartResult(p with{SourceChoiceMove=Move("TACKLE")},o);Check(!forged.Succeeded && forged.Trace.IsEmpty,"no preloaded entry lock");
    var s=Start(p,o);var withoutProfile=s with {SourceMechanicsRulesVersion=null};var denied=WildBattleRules.ResolveMove(catalog,withoutProfile,0);
    Check(!denied.Succeeded && denied.Trace.IsEmpty && ReferenceEquals(denied.State,withoutProfile),"canonical marker required");
    var raw=WildBattleRules.Start(catalog,p,o,new(0),rulesVersion:WildBattleRules.FuchsiaRulesVersion);Check(!raw.Succeeded && raw.Trace.IsEmpty,"legacy API does not admit canonical Band");
});
Test("Protect, miss and lost Focus Punch obey and lock",()=>{
    var s=Start(Mon("PIDGEY",["TACKLE"],Band),Mon("SQUIRTLE",["PROTECT"],level:100));
    s=Action(s,WildBattleSide.Opponent).State!;s=Action(s,WildBattleSide.Player).State!;Check(s.Player.SourceChoiceMove==Move("TACKLE"),"Protect failure locks");
    bool missed=false;
    for(uint seed=0;seed<16 && !missed;seed++) {
        var r=Turn(Start(Mon("ALAKAZAM",["KINESIS"],Band),Mon("MAGIKARP",["SPLASH"]),seed));
        if(!r.Events.OfType<WildBattleMoveMissedEvent>().Any()) continue;
        Check(r.State!.Player.SourceChoiceMove==Move("KINESIS"),"accuracy miss locks");missed=true;
    }
    Check(missed,"actual accuracy miss observed");
    var focus=Turn(Start(Mon("MACHOP",["FOCUS_PUNCH"],Band),Mon("PIDGEY",["TACKLE"],level:10)));
    Check(focus.Events.OfType<WildBattleFocusPunchLostEvent>().Any() && focus.State!.Player.SourceChoiceMove==Move("FOCUS_PUNCH"),"Focus Punch loss after obeys locks");
});
Test("Disable, Torment and Imprison combine with Choice to exhaust selection",()=>{
    var s=Turn(Start(Mon("PIDGEY",["SAND_ATTACK","TACKLE"],Band),Mon("MAGIKARP",["SPLASH"]))).State!;
    var disabled=s with {Player=s.Player with {DisabledMove=Move("SAND_ATTACK"),DisableTurnsRemaining=2}};
    Check(WildBattleRules.ValidateSourceMoveSelection(catalog,disabled,WildBattleSide.Player,-1) is null,"disabled chosen slot forces Struggle");
    var tormented=s with {Player=s.Player with {CalledVolatile=new(){Tormented=true}}};
    Check(WildBattleRules.ValidateSourceMoveSelection(catalog,tormented,WildBattleSide.Player,-1) is null,"tormented chosen slot forces Struggle");
    var same=Start(Mon("ABRA",["TELEPORT","CALM_MIND"],Band),Mon("RALTS",["IMPRISON","CALM_MIND"]));
    same=Action(same,WildBattleSide.Player,1).State!;same=Action(same,WildBattleSide.Opponent).State!;
    Check(WildBattleRules.ValidateSourceMoveSelection(catalog,same,WildBattleSide.Player,-1) is null,"real opposing Imprison exhausts Choice");
});
Test("real KO clears victim choice",()=>{
    var s=Start(Mon("PIDGEY",["TACKLE"],level:100),Mon("MAGIKARP",["SPLASH"],Band,5));
    s=Action(s,WildBattleSide.Opponent).State!;Check(s.Opponent.SourceChoiceMove is not null,"victim locked");
    var r=Turn(s);Check(r.State!.Opponent.Pokemon.CurrentHp==0 && r.State.Opponent.SourceChoiceMove is null,"faint clears choice");
});
var report=new {groups,checks,failures,fullTurns=turns,sourceVectors=vectors,runtime=Environment.Version.ToString(),baseline="eae4df332469f1df0cec8d5406f72e298b733a10",scope="Canonical source singles Choice Band; independent arithmetic/state fixtures and real turns; Focus Band remains separate."};
File.WriteAllText(Path.Combine(evidence,"result.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(Json(report));
return failures==0 && groups>=12 && turns>=15 && vectors==2052 ? 0:1;
