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

if(args.Length==4 && args[3]=="--four") return FourFocusBandChecks.Run(args[0],args[1],args[2]);
if(args.Length==4 && args[3]=="--ohko-four") return FourOhkoChecks.Run(args[0],args[1],args[2]);
bool messagesOnly=args.Length==4 && args[3]=="--messages";
bool ohkoOnly=args.Length==4 && args[3]=="--ohko";
if (args.Length != 3 && !messagesOnly && !ohkoOnly) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
string source = args[1], evidence = args[2];
string tm = File.ReadAllText(Path.Combine(source,"src/data/pokemon/tmhm_learnsets.h"));
string tutor = File.ReadAllText(Path.Combine(source,"src/data/pokemon/tutor_learnsets.h"));
int groups=0, checks=0, failures=0, turns=0, nonMoveActions=0, vectors=0, sourceOracleVectors=0;
var receipts = new List<object>();
const string Band = "ITEM_FOCUS_BAND";
string Json<T>(T value) => JsonSerializer.Serialize(value);
void Check(bool ok,string label) { checks++; if(!ok) throw new Exception(label); }
void Test(string label,Action body) {
    if(ohkoOnly&&!label.StartsWith("OHKO"))return;
    if(messagesOnly&&!new[]{"ordinary lethal","ordinary accuracy","nonlethal and substitute","two-hit sticky","Explosion adjustment","real Future Sight","confusion normal2"}.Any(label.StartsWith))return;
    groups++; try { body(); Console.WriteLine("PASS "+label); } catch(Exception ex) { failures++; Console.WriteLine("FAIL "+label+" "+ex); } }
MoveId Move(string name) => new("pret:MOVE_"+name);
WildBattleCombatant Mon(string species,string[] moves,string item="ITEM_NONE",int level=50,uint pid=0)
{
    var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
    foreach(var name in moves) {
        bool learned=catalog.Species[p.Species].LevelUpMoves.Any(m=>m.Move==Move(name)&&m.Level<=level);
        string tmBlock=Regex.Match(tm,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;
        string tutorBlock=Regex.Match(tutor,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;
        Check(learned||Regex.IsMatch(tmBlock,@"TM\d\d_"+name+@"\)")||tutorBlock.Contains("TUTOR(MOVE_"+name+")"),"authentic move "+species+"/"+name);
    }
    return new(p with { Moves=moves.Select(name=>new PokemonMoveSlot(Move(name),catalog.Moves[Move(name)].Pp,catalog.Moves[Move(name)].Pp)).ToImmutableArray() },HeldItem:item);
}
WildBattleCombatant Hp(WildBattleCombatant mon,int hp) => mon with {Pokemon=mon.Pokemon with {CurrentHp=hp}};
WildBattleState Require(WildBattleResult r,string label)
{
    Check(r.Succeeded,label+": "+r.Fault);
    Check(WildBattleRules.Validate(catalog,r.State) is null,label+" output validates: "+WildBattleRules.Validate(catalog,r.State));
    return r.State!;
}
WildBattleState Start(WildBattleCombatant p,WildBattleCombatant o,uint seed=31,params WildBattleCombatant[] reserves) =>
    Require(WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[p,..reserves],0,o,new(seed)),"source entry");
WildBattleResult Turn(WildBattleState s,int slot=0)
{
    var r=WildBattleRules.ResolveMove(catalog,s,slot); var next=Require(r,"whole turn"); turns++;
    Check(next.Outcome!=WildBattleOutcome.Ongoing||next.CalledParty?.PendingSwitch is not null||next.Turn==s.Turn+1,"actual Ready completion");
    foreach(var draw in r.Trace.Where(t=>t.Purpose.Contains("focus-band")))
        Check(draw.Bound==100&&draw.Result==draw.Raw%100,"Focus uses exact u16 draw modulo100");
    return r;
}
RandomTrace[] Focus(WildBattleResult r) => r.Trace.Where(t=>t.Purpose.Contains("focus-band")).ToArray();
WildBattleDamageAdjustedEvent[] Adjusted(WildBattleResult r,string? move=null) => r.Events.OfType<WildBattleDamageAdjustedEvent>().Where(e=>move is null||e.Move==Move(move)).ToArray();
void Save(string label,uint seed,WildBattleResult r)
{
    foreach(var e in Adjusted(r)) {
        var canonical=SourceBattleEventAdapter.Import(e);
        var restored=JsonSerializer.Deserialize<SourceBattleEvent>(Json<SourceBattleEvent>(canonical))!;
        Check(Json<WildBattleEvent>(SourceBattleEventAdapter.Export(restored))==Json<WildBattleEvent>(e),"mechanical receipt event adapter and serializer round trip");
    }
    if(messagesOnly) {
        var messages=r.Events.OfType<WildBattleHeldItemSurvivalEvent>().ToArray();
        bool expected=label is "ordinary proc" or "substitute primes next hit" or "sticky Double Kick" or "delayed Future Sight" or "Future Sight next-action sticky" or "confusion proc";
        Check(messages.Length==(expected?1:0),label+" actual result count");
        foreach(var message in messages) {
            var lastHp=r.Events.Where(e=>e is WildBattleHpChangedEvent hp&&hp.Target==message.Target&&hp.Move==message.Move || e is WildBattleConfusionSelfHitEvent self&&self.Side==message.Target&&self.SelectedMove==message.Move).Last();
            Check(message.Sequence>lastHp.Sequence&&message.Item==Band,"survival result follows last actual HP update");
            var canonical=SourceBattleEventAdapter.Import(message);
            Check(Json<WildBattleEvent>(SourceBattleEventAdapter.Export(JsonSerializer.Deserialize<SourceBattleEvent>(Json<SourceBattleEvent>(canonical))!))==Json<WildBattleEvent>(message),"survival event adapters and JSON round trip");
        }
    }
    receipts.Add(new {label,seed,trace=r.Trace,events=r.Events,mask=r.State!.SourceFocusBandedMask,playerHp=r.State.Player.Pokemon.CurrentHp,opponentHp=r.State.Opponent.Pokemon.CurrentHp,turn=r.State.Turn});
}
bool Find(string label,Func<uint,WildBattleResult?> attempt)
{
    for(uint seed=0;seed<=128;seed++) {
        try {var r=attempt(seed);if(r is null)continue;Save(label,seed,r);return true;}
        catch(Exception ex) {throw new Exception(label+" at bounded seed "+seed,ex);}
    }
    return false;
}
WildBattleState Restore(WildBattleState s)
{
    var copy=JsonSerializer.Deserialize<WildBattleState>(Json(s))!;
    Check(Json(copy)==Json(s)&&WildBattleRules.Validate(catalog,copy) is null,"state serializer round trip");return copy;
}
Test("exact source item and bounded post-random helper differential",()=>{
    Check(Environment.Version.Major==10,"official .NET10 runtime");
    using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence,"oracle/source-probe.json")));
    Check(manifest.RootElement.GetProperty("sourceCommit").GetString()==catalog.SourceCommit,"authenticated source commit");
    sourceOracleVectors=manifest.RootElement.GetProperty("vectors").GetInt32();Check(sourceOracleVectors==3770,"complete exact C oracle count");
    var assembly=typeof(WildBattleRules).Assembly;
    var item=assembly.GetType("Game.Domain.Battle.SourceBattleHeldItems")!.GetField("FocusBand",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
    Check((int)item.GetType().GetProperty("SourceItemId")!.GetValue(item)! ==196 &&
        (string)item.GetType().GetProperty("HoldEffect")!.GetValue(item)! =="HOLD_EFFECT_FOCUS_BAND" &&
        (int)item.GetType().GetProperty("Parameter")!.GetValue(item)! ==10,"exact source item196/effect39/parameter10");
    var helper=assembly.GetType("Game.Domain.Battle.SourceHeldDamageRules")!;
    var roll=helper.GetMethod("FocusRoll",BindingFlags.Static|BindingFlags.NonPublic)!;
    var adjust=helper.GetMethod("Adjust",BindingFlags.Static|BindingFlags.NonPublic)!;
    foreach(string line in File.ReadLines(Path.Combine(evidence,"oracle/source-vectors.jsonl"))) {
        using var doc=JsonDocument.Parse(line);var input=doc.RootElement.GetProperty("input");var output=doc.RootElement.GetProperty("output");
        int I(string k)=>input.GetProperty(k).GetInt32();int O(string k)=>output.GetProperty(k).GetInt32();
        int op=I("op");if(op>2||I("preserve")!=0||I("item")==175)continue; // Enigma dynamic effect is outside product admission.
        Check(I("item") is 0 or 196 or 42,"ordinary item fixture boundary");
        int damage=I("damage");if(op!=2&&damage!=0) {damage=unchecked(damage*(100-I("draw0")%16))/100;if(damage==0)damage=1;}
        bool focus=I("sticky")!=0;
        if(I("item")==196)focus=(bool)roll.Invoke(null,[focus,(ushort)I(op==2?"draw0":"draw1")])!;
        var result=adjust.Invoke(null,[I("hp"),damage,I("substitute")!=0,I("endure")!=0,op!=1&&I("falseSwipe")!=0,focus])!;
        int actualDamage=(int)result.GetType().GetProperty("Damage")!.GetValue(result)!;
        bool endured=(bool)result.GetType().GetProperty("Endured")!.GetValue(result)!;
        bool hungOn=(bool)result.GetType().GetProperty("HungOn")!.GetValue(result)!;
        int flags=I("flags")|(endured?64:0)|(hungOn?128:0);
        Check(actualDamage==O("damage")&&focus==(O("focus"+I("target"))!=0)&&flags==O("flags"),"scalar source row "+I("id"));vectors++;
    }
    Check(vectors==2123,"nonempty bounded scalar comparison");
    for(int value=0;value<=ushort.MaxValue;value++) {
        bool fresh=(bool)roll.Invoke(null,[false,(ushort)value])!;
        bool sticky=(bool)roll.Invoke(null,[true,(ushort)value])!;
        Check(fresh==(value%100<10)&&sticky,"Focus u16 modulo and sticky input "+value);
    }
});
Test("ordinary lethal proc and fail, retained item and actual ActionFinished clear",()=>{
    bool proc=false,fail=false;
    for(uint seed=0;seed<=128&&!(proc&&fail);seed++) {
        var r=Turn(Start(Mon("PIDGEY",["TACKLE"],level:100),Hp(Mon("MAGIKARP",["SPLASH"],Band,5),1),seed));var draws=Focus(r);
        if(draws.Length==0)continue;Check(draws.Length==1,"ordinary one command roll");
        int rollIndex=Array.FindIndex(r.Trace.ToArray(),t=>t.Purpose.Contains("focus-band"));
        Check(rollIndex>0&&r.Trace[rollIndex-1].Purpose.EndsWith(".damage"),"normal damage variance immediately precedes Focus");
        bool success=draws[0].Raw%100<10;var a=Adjusted(r);
        Check(r.State!.Opponent.HeldItem==Band,"Band is retained");Check(r.State.SourceFocusBandedMask==0,"whole action cleared all Focus flags");
        if(success) {Check(r.State.Opponent.Pokemon.CurrentHp==1&&a.Any(e=>e.HungOn&&!e.Endured&&e.DamageAfter==0),"proc lethal clamp");if(!proc)Save("ordinary proc",seed,r);proc=true;}
        else {Check(r.State.Opponent.Pokemon.CurrentHp==0&&!a.Any(e=>e.HungOn),"failed proc faints");if(!fail)Save("ordinary fail",seed,r);fail=true;}
    }
    Check(proc&&fail,"both actual outcomes observed");
});
Test("ordinary accuracy miss skips Focus; ordinary immunity still rolls",()=>{
    Check(Find("ordinary miss",seed=>{
        var p=Mon("PIDGEY",["TACKLE"],level:100);var o=Mon("MAGIKARP",["SPLASH"],Band,100);
        var s=Start(p,o,seed);s=s with{Player=s.Player with{AccuracyStage=0},Opponent=s.Opponent with{EvasionStage=12}};
        var r=Turn(s);if(!r.Events.OfType<WildBattleMoveMissedEvent>().Any(e=>e.Actor==WildBattleSide.Player))return null;
        var ps=Start(p,o with{HeldItem="ITEM_NONE"},seed);ps=ps with{Player=ps.Player with{AccuracyStage=0},Opponent=ps.Opponent with{EvasionStage=12}};var plain=Turn(ps);Check(Focus(r).Length==0&&Json(r.Trace)==Json(plain.Trace),"miss consumes no Band draw");return r;
    }),"miss observed");
    var immune=Turn(Start(Mon("PIDGEY",["TACKLE"],level:100),Mon("GASTLY",["NIGHT_SHADE"],Band,20),31));
    Check(Focus(immune).Length==1&&!immune.Events.OfType<WildBattleHpChangedEvent>().Any(e=>e.Target==WildBattleSide.Opponent&&e.HpBefore!=e.HpAfter),"ordinary type immunity still reaches adjustment");Save("ordinary immunity",31,immune);
});
Test("nonlethal and substitute proc draw occurs before damage application",()=>{
    Check(Find("nonlethal Focus proc",seed=>{
        var s=Start(Mon("PIDGEY",["TACKLE"],level:5),Mon("MAGIKARP",["SPLASH"],Band,100),seed);var r=Turn(s);var f=Focus(r);
        if(f.Length==0||f[0].Raw%100>=10)return null;
        Check(r.State!.Opponent.Pokemon.CurrentHp>1&&r.State.Opponent.Pokemon.CurrentHp<s.Opponent.Pokemon.CurrentHp&&!Adjusted(r).Any(e=>e.HungOn),"successful nonlethal roll is not a displayed survival receipt");return r;
    }),"nonlethal proc observed");
    Check(Find("substitute primes next hit",seed=>{
        var s=Start(Mon("NIDORAN_M",["DOUBLE_KICK"],level:100),Hp(Mon("MAGIKARP",["SPLASH"],Band,5),1),seed);
        s=s with{Opponent=s.Opponent with{CalledVolatile=new(){SubstituteHp=1}}};var r=Turn(s);var f=Focus(r);
        if(f.Length!=2||f[0].Raw%100>=10||f[1].Raw%100<10)return null;
        Check(r.Events.OfType<WildBattleSubstituteDamagedEvent>().Single().HpAfter==0&&Adjusted(r).Count(e=>e.HungOn)==1&&r.State!.Opponent.Pokemon.CurrentHp==1,"substitute success retained after substitute fades and later roll fails");return r;
    }),"substitute sticky sequence observed");
});
Test("two-hit sticky protection continues after failed second roll; Endure breaks",()=>{
    Check(Find("sticky Double Kick",seed=>{
        var r=Turn(Start(Mon("NIDORAN_M",["DOUBLE_KICK"],level:100),Hp(Mon("MAGIKARP",["SPLASH"],Band,5),1),seed));var f=Focus(r);
        if(f.Length!=2||f[0].Raw%100>=10||f[1].Raw%100<10)return null;
        Check(r.Events.OfType<WildBattleMultiHitEvent>().Single().ActualHits==2&&Adjusted(r).Count(e=>e.HungOn)==2&&r.State!.Opponent.Pokemon.CurrentHp==1,"sticky survives failed roll and zero damage");return r;
    }),"sticky two-hit observed");
    Check(Find("Endure Double Kick Focus proc",seed=>{
        var endure=Turn(Start(Mon("NIDORAN_M",["DOUBLE_KICK"],level:100),Hp(Mon("KANGASKHAN",["ENDURE"],Band,50),1),seed));
        if(Focus(endure).Length==0||Focus(endure)[0].Raw%100>=10)return null;
        Check(endure.Events.OfType<WildBattleMultiHitEvent>().Single().ActualHits==1&&Focus(endure).Length==1,"Endure stops ordinary multihit even after Focus success");
        Check(Adjusted(endure).Single().Endured&&!Adjusted(endure).Single().HungOn,"Endure result precedence");return endure;
    }),"actual Endure plus Focus proc observed");
});
Test("Double Kick immunity skips adjustment; Triple Kick reaches it; Twineedle is bounded",()=>{
    foreach(var fixture in new[]{("NIDORAN_M","DOUBLE_KICK","GASTLY","NIGHT_SHADE")}) {
        var r=Turn(Start(Mon(fixture.Item1,[fixture.Item2],level:100),Mon(fixture.Item3,[fixture.Item4],Band,50),31));
        Check(Focus(r).Length==0&&!r.Trace.Any(t=>t.Purpose.Contains("player")&&t.Purpose.EndsWith(".damage")),"multihit immune skips both adjustment draws "+fixture.Item2);Save(fixture.Item2+" immunity",31,r);
    }
    var denied=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("BEEDRILL",["TWINEEDLE"],level:100)],0,Mon("SHEDINJA",["HARDEN"],Band,50),new(31));
    Check(!denied.Succeeded&&denied.Fault=="source_mechanics_ability_not_implemented"&&denied.Trace.IsEmpty,"Wonder Guard immunity fixture is explicitly outside admission");
    var twineedle=Turn(Start(Mon("BEEDRILL",["TWINEEDLE"],level:20),Mon("MAGIKARP",["SPLASH"],Band,100),31));
    Check(Focus(twineedle).Length==2&&twineedle.Events.OfType<WildBattleMultiHitEvent>().Single().ActualHits==2,"admitted Twineedle two-hit command coverage");Save("Twineedle real two hits",31,twineedle);
    Check(Find("Triple Kick immunity",seed=>{
        var r=Turn(Start(Mon("HITMONTOP",["TRIPLE_KICK"],level:100),Mon("GASTLY",["NIGHT_SHADE"],Band,20),seed));
        if(r.Events.OfType<WildBattleMoveMissedEvent>().Any(e=>e.Actor==WildBattleSide.Player))return null;
        Check(Focus(r).Length>=1&&!r.Events.OfType<WildBattleHpChangedEvent>().Any(e=>e.Target==WildBattleSide.Opponent&&e.HpBefore!=e.HpAfter),"Triple Kick immune command still rolls");return r;
    }),"Triple Kick immunity reached");
});
Test("six-member Beat Up has no Endure loop break",()=>{
    var a=Mon("SNEASEL",["BEAT_UP"],level:100);var reserves=Enumerable.Range(1,5).Select(i=>Mon("PIDGEY",["TACKLE"],level:50,pid:(uint)i)).ToArray();
    var r=Turn(Start(a,Hp(Mon("KANGASKHAN",["ENDURE"],Band,50),1),31,reserves));
    Check(r.Events.OfType<WildBattleCalledBeatUpMemberEvent>().Count()==6&&Focus(r).Length==6,"all six source party contributors ran");
    Check(Focus(r).Any(t=>t.Raw%100<10)&&Adjusted(r).Count(e=>e.Endured)==6&&!Adjusted(r).Any(e=>e.HungOn)&&r.State!.Opponent.Pokemon.CurrentHp==1,"Endure takes precedence without Beat Up break");Save("six member Beat Up Endure",31,r);
});
Test("False Swipe clamps alone and still reports successful Focus",()=>{
    bool proc=false,fail=false;
    for(uint seed=0;seed<=128&&!(proc&&fail);seed++) {
        var r=Turn(Start(Mon("SCYTHER",["FALSE_SWIPE"],level:100),Hp(Mon("MAGIKARP",["SPLASH"],Band,5),1),seed));var f=Focus(r);if(f.Length==0)continue;
        Check(r.State!.Opponent.Pokemon.CurrentHp==1,"False Swipe survives with either Band roll");bool success=f[0].Raw%100<10;
        Check(Adjusted(r).Single().HungOn==success&&!Adjusted(r).Single().Endured,"False Swipe alone no item result");
        if(success&&!proc){Save("False Swipe Focus proc",seed,r);proc=true;}if(!success&&!fail){Save("False Swipe Focus fail",seed,r);fail=true;}
    }Check(proc&&fail,"both False Swipe cases");
});
Test("critical message survives an effective damaging hit clamped to zero",()=>{
    Check(Find("critical zero with Focus",seed=>{
        var s=Start(Mon("SCYTHER",["FOCUS_ENERGY","FALSE_SWIPE"],level:100),Hp(Mon("MAGIKARP",["SPLASH"],Band,5),1),seed);
        s=Turn(s).State!;var r=Turn(s,1);var hp=r.Events.OfType<WildBattleHpChangedEvent>().SingleOrDefault(e=>e.Target==WildBattleSide.Opponent&&e.Critical);
        if(hp is null||!Adjusted(r).Any(e=>e.HungOn))return null;
        Check(hp.Damage==0&&r.Events.OfType<WildBattleCriticalHitEvent>().Any(e=>e.Target==WildBattleSide.Opponent),"source critical-result receipt emitted after zero clamp");return r;
    }),"actual critical zero clamp observed");
});
Test("OHKO Focus before Sturdy and miss, and OHKO ignores substitute clamp guard",()=>{
    var sturdy=Mon("GEODUDE",["DEFENSE_CURL"],Band,50);uint pid=0;
    while(sturdy.Pokemon.AbilityId!="ABILITY_STURDY"&&pid<10)sturdy=Mon("GEODUDE",["DEFENSE_CURL"],Band,50,++pid);
    Check(sturdy.Pokemon.AbilityId=="ABILITY_STURDY","authentic Sturdy personality");
    var blocked=Turn(Start(Mon("NIDORAN_M",["HORN_DRILL"],level:100),sturdy,31));
    Check(Focus(blocked).Length==1&&!blocked.Trace.Any(t=>t.Purpose.EndsWith(".ohko"))&&blocked.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason=="ohko-sturdy"),"Focus precedes Sturdy and no OHKO chance draw");Save("Sturdy OHKO",31,blocked);
    Check(Find("missed OHKO",seed=>{
        var r=Turn(Start(Mon("NIDORAN_M",["HORN_DRILL"],level:50),Mon("MAGIKARP",["SPLASH"],Band,50),seed));
        if(!r.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason=="ohko-miss"))return null;
        int fi=Array.FindIndex(r.Trace.ToArray(),t=>t.Purpose.Contains("focus-band")),ki=Array.FindIndex(r.Trace.ToArray(),t=>t.Purpose.EndsWith(".ohko"));
        Check(fi>=0&&ki>fi,"Focus before OHKO chance on miss");return r;
    }),"OHKO miss observed");
    Check(Find("OHKO substitute Focus",seed=>{
        var s=Start(Mon("NIDORAN_M",["HORN_DRILL"],level:100),Mon("MAGIKARP",["SPLASH"],Band,5),seed);
        s=s with {Opponent=s.Opponent with{CalledVolatile=new(){SubstituteHp=10}}};var r=Turn(s);
        if(!Adjusted(r).Any(e=>e.HungOn))return null;
        Check(Adjusted(r).Single().DamageAfter==s.Opponent.Pokemon.CurrentHp-1&&r.Events.OfType<WildBattleSubstituteDamagedEvent>().Any(),"OHKO adjusts even behind substitute");return r;
    }),"OHKO substitute proc observed");
});
Test("OHKO real Endure takes precedence; valid sure-hit skips only OHKO accuracy draw",()=>{
    Check(Find("OHKO Endure plus Focus",seed=>{
        var r=Turn(Start(Mon("NIDORAN_M",["HORN_DRILL"],level:100),Hp(Mon("KANGASKHAN",["ENDURE"],Band,50),20),seed));
        if(Focus(r).Length==0||Focus(r)[0].Raw%100>=10||Adjusted(r).Length==0)return null;
        var adjustment=Adjusted(r).Single();
        Check(adjustment.Endured&&!adjustment.HungOn&&adjustment.DamageBefore==20&&adjustment.DamageAfter==19&&r.State!.Opponent.Pokemon.CurrentHp==1,"OHKO actual Endure precedes successful Band");return r;
    }),"OHKO Endure and successful Focus observed");
    var s=Start(Mon("NIDORAN_M",["HORN_DRILL"],level:100),Mon("MAGIKARP",["SPLASH"],Band,50),31);
    // Controlled live source Lock-On registers on the target, with sufficient attacker level.
    s=s with{Opponent=s.Opponent with{CalledTurnState=new(){SureHitTurnsRemaining=2,SureHitSource=WildBattleSide.Player}}};
    var sure=Turn(s);
    Check(Focus(sure).Length==1&&!sure.Trace.Any(t=>t.Purpose.EndsWith(".ohko"))&&!sure.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason.StartsWith("ohko-")),"matching sure-hit retains Focus draw and omits OHKO chance draw");Save("OHKO controlled sure-hit",31,sure);
});
Test("Explosion adjustment precedes its accuracy miss",()=>{
    Check(Find("Explosion proc before miss",seed=>{
        var a=Mon("GEODUDE",["EXPLOSION"],level:100);var o=Hp(Mon("MAGIKARP",["SPLASH"],Band,5),1);
        var s=Start(a,o,seed);s=s with{Player=s.Player with{AccuracyStage=0},Opponent=s.Opponent with{EvasionStage=12}};var r=Turn(s);if(!r.Events.OfType<WildBattleMoveMissedEvent>().Any(e=>e.Actor==WildBattleSide.Player)||!Adjusted(r).Any(e=>e.HungOn))return null;
        int receipt=Array.FindIndex(r.Events.ToArray(),e=>e is WildBattleDamageAdjustedEvent),miss=Array.FindIndex(r.Events.ToArray(),e=>e is WildBattleMoveMissedEvent);
        Check(Focus(r).Length==1&&receipt>=0&&receipt<miss&&r.State!.Opponent.Pokemon.CurrentHp==1,"mechanical receipt before miss, no target damage");return r;
    }),"Explosion misses after Focus proc");
});
Test("confusion normal2 ignores False Swipe, applies own Focus before substitute bypass",()=>{
    bool saved=false,failed=false,substitute=false;
    for(uint seed=0;seed<=128&&!(saved&&failed&&substitute);seed++) {
        var s=Start(Hp(Mon("SCYTHER",["FALSE_SWIPE"],Band,100),1),Mon("MAGIKARP",["SPLASH"],level:100),seed);
        s=s with {Player=s.Player with{ConfusionTurnsRemaining=3}};var r=Turn(s);
        var hit=r.Events.OfType<WildBattleConfusionSelfHitEvent>().SingleOrDefault();if(hit is null)continue;
        var f=Focus(r);Check(f.Length==1,"self-hit only own Band roll");bool proc=f[0].Raw%100<10;
        if(proc) {
            Check(hit.HpAfter==1&&Adjusted(r).Any(e=>e.HungOn),"own Band saves normal2");if(!saved){Save("confusion proc",seed,r);saved=true;}
            var sub=Turn(s with{Player=s.Player with{CalledVolatile=new(){SubstituteHp=10}}});
            Check(Focus(sub).Single().Raw==f[0].Raw&&sub.Events.OfType<WildBattleConfusionSelfHitEvent>().Single().HpAfter==0&&!Adjusted(sub).Any(e=>e.HungOn),"own substitute blocks protection before ignore-substitute damage");
            if(!substitute){Save("confusion own substitute",seed,sub);substitute=true;}
        } else {Check(hit.HpAfter==0,"selected False Swipe cannot save confusion");if(!failed){Save("confusion failed Focus",seed,r);failed=true;}}
    }Check(saved&&failed&&substitute,"all confusion boundaries observed");
});
Test("fixed immunity rolls Focus, Endeavor immunity does not; Super Fang and poison excluded",()=>{
    var fixedHit=Turn(Start(Mon("MACHOP",["SEISMIC_TOSS"],level:100),Mon("GASTLY",["NIGHT_SHADE"],Band,20),31));
    Check(Focus(fixedHit).Length==1&&!fixedHit.Events.OfType<WildBattleHpChangedEvent>().Any(e=>e.Target==WildBattleSide.Opponent&&e.HpBefore!=e.HpAfter),"fixed immunity reaches adjustsetdamage");Save("fixed immune",31,fixedHit);
    var endeavor=Turn(Start(Hp(Mon("RATTATA",["ENDEAVOR"],level:100),1),Mon("GASTLY",["NIGHT_SHADE"],Band,20),31));
    Check(Focus(endeavor).Length==0,"Endeavor immune skips adjustment");Save("Endeavor immune",31,endeavor);
    var fang=Turn(Start(Mon("RATTATA",["SUPER_FANG"],level:100),Hp(Mon("MAGIKARP",["SPLASH"],Band,5),1),31));
    Check(Focus(fang).Length==0&&fang.State!.Opponent.Pokemon.CurrentHp==0,"Super Fang at one HP bypasses Focus");Save("Super Fang at one HP",31,fang);
    var poison=Turn(Start(Hp(Mon("MAGIKARP",["SPLASH"],Band,50),1) with{Status=WildBattleStatus.Poison},Mon("MAGIKARP",["SPLASH"],level:100),31));
    Check(Focus(poison).Length==0&&poison.State!.Player.Pokemon.CurrentHp==0,"passive poison bypasses Focus");Save("passive poison",31,poison);
});
Test("real Future Sight retains serialized slot flag until next actual ActionFinished",()=>{
    Check(Find("Future Sight next-action sticky",seed=>{
        var s=Start(Mon("ALAKAZAM",["FUTURE_SIGHT","CALM_MIND","PSYCHIC"],level:100),Hp(Mon("MAGIKARP",["SPLASH"],Band,100),1),seed);
        s=Turn(s).State!;s=Turn(s,1).State!;var delayed=Turn(s,1);var f=Focus(delayed);
        if(f.Length==0||f[^1].Raw%100>=10)return null;
        Check(delayed.State!.SourceFocusBandedMask==2&&delayed.State.Opponent.Pokemon.CurrentHp==1,"delayed normal2 keeps target bit past Ready");
        var next=Turn(Restore(delayed.State),2);var n=Focus(next);
        if(n.Length==0||n[0].Raw%100<10)return null;
        Check(Adjusted(next,"PSYCHIC").Any(e=>e.HungOn)&&next.State!.Opponent.Pokemon.CurrentHp==1,"previous delayed success protects first next action despite failed roll");
        Check(next.State!.SourceFocusBandedMask==0,"actual first action clears sticky bit");Save("delayed Future Sight",seed,delayed);return next;
    }),"delayed save then failed next roll observed");
});
Test("completed switch clears incoming sticky flag before opponent action",()=>{
    Check(Find("switch clears before opponent OHKO",seed=>{
        var reserve=Hp(Mon("MAGIKARP",["SPLASH"],Band,5,pid:1),1);
        var s=Start(Hp(Mon("MAGIKARP",["SPLASH"],Band,50),1),Mon("NIDORAN_M",["HORN_DRILL"],level:100),seed,reserve);
        // A controlled incoming slot register; the preceding test proves genuine delayed creation.
        s=s with{SourceFocusBandedMask=1};s=Restore(s);
        var healed=s.Player.Pokemon with{CurrentHp=Math.Min(s.Player.Pokemon.Stats.MaxHp,21)};
        var oldPotion=WildBattleRules.ResolvePotion(catalog,s,healed);
        Check(!oldPotion.Succeeded&&oldPotion.Fault=="wild_battle_potion_pokemon_catalog_invalid"&&ReferenceEquals(oldPotion.State,s)&&oldPotion.Events.IsEmpty&&oldPotion.Trace.IsEmpty,"historical Potion API remains outside canonical admission and fails atomically");
        var r=WildBattleRules.SwitchSourceWildPlayer(catalog,s,1);Require(r,"completed source switch and foe response");nonMoveActions++;
        if(Focus(r).Length==0||Focus(r)[0].Raw%100<10)return null;
        Check(r.State!.Player.Pokemon.Personality==1&&r.Events.OfType<WildBattleHpChangedEvent>().Any(e=>e.Target==WildBattleSide.Player&&e.HpAfter==0),"completed switch before failed Band roll on foe OHKO");
        Check(!Adjusted(r).Any(e=>e.HungOn)&&r.State.Player.Pokemon.CurrentHp==0&&r.State.SourceFocusBandedMask==0,"switch does not leak incoming slot protection into foe action");return r;
    }),"switch cleanup distinguished from stale protection");
});
Test("zero flag omits save field; unsupported legacy profile rejects Focus atomically",()=>{
    var p=Mon("PIDGEY",["TACKLE"],Band);var o=Mon("MAGIKARP",["SPLASH"]);var s=Start(p,o);
    Check(!Json(s).Contains("SourceFocusBandedMask"),"default flag does not change historical JSON");
    var active=s with{SourceFocusBandedMask=3};Check(Restore(active).SourceFocusBandedMask==3,"both slot bits serialize");
    var bad=active with{SourceFocusBandedMask=4};Check(WildBattleRules.Validate(catalog,bad) is not null,"reject inactive singles slot");
    var imported=SinglesBattleAdapter.Import(catalog,active);Check(imported.Succeeded,"canonical position import: "+imported.Fault);
    Check(imported.Frame!.Slots[0].Position.FocusBanded&&imported.Frame.Slots[1].Position.FocusBanded,"slot bits in canonical positions");
    var exported=SinglesBattleAdapter.Export(imported.Frame);Check(exported.Succeeded&&exported.State!.SourceFocusBandedMask==3,"canonical position export preserves both bits");
    var legacy=WildBattleRules.Start(catalog,p,o,new(0),rulesVersion:WildBattleRules.FuchsiaRulesVersion);
    Check(!legacy.Succeeded&&legacy.Trace.IsEmpty&&legacy.Events.IsEmpty,"historical entry fails closed");
});
Test("four-slot action owner rejects Focus item and forged sticky register",()=>{
    foreach(bool item in new[]{true,false}) {
        WildBattleCombatant[] bodies=[Mon("PIDGEY",["TACKLE"],item?Band:"ITEM_NONE"),Mon("RATTATA",["TACKLE"]),Mon("SPEAROW",["PECK"]),Mon("DODUO",["PECK"])];
        WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
        var frame=new SourceBattleFrame {Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),
            Parties=[new(BattleSide.Player,[Member(bodies[0]),Member(bodies[2])]),new(BattleSide.Opponent,[Member(bodies[1]),Member(bodies[3])])],
            Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,bodies[i]) {Position=new(){FocusBanded=!item&&i==0}}).ToImmutableArray()};
        var choices=frame.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(c.Slot.Value^1))).ToImmutableArray();
        var r=SourceFourActionTurns.BeginActions(catalog,frame,frame.Revision,choices);
        Check(!r.Succeeded&&ReferenceEquals(r.State,frame)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"unsupported four-slot Focus boundary is atomic");
    }
});
var report=new {groups,checks,failures,fullTurns=turns,nonMoveActions,sourceOracleVectors,comparedScalarVectors=vectors,scenarioReceipts=receipts.Count,runtime=Environment.Version.ToString(),baseline="4c9fd2dab41635eb98661d0f2896b9e9a9a22380",messagesOnly,ohkoOnly,scope=ohkoOnly?"Two actual canonical OHKO/Focus/Endure/sure-hit groups; no source regeneration.":messagesOnly?"Seven affected result-message cases; reuses existing authentic singles fixtures, no new C oracle or whole historical suite.":"Bounded post-random scalar differential plus authentic canonical source singles actual-turn integration. Exact C oracle separately covers OHKO and command state; no ROM claim."};
File.WriteAllText(Path.Combine(evidence,"result.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(evidence,"scenario-receipts.json"),JsonSerializer.Serialize(receipts,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(Json(report));
return failures==0&&(ohkoOnly?groups==2&&turns==68&&receipts.Count==5:messagesOnly?groups==7&&receipts.Count>=11:groups>=18&&turns>=20&&nonMoveActions>0&&sourceOracleVectors==3770&&vectors==2123)?0:1;
