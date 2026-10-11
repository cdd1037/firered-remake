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

if(args.Length is not (4 or 5))throw new ArgumentException("CONTENT SOURCE VECTOR_JSON OUTPUT");
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0],args[0]);var catalog=proof.Catalog;
string tm=File.ReadAllText(Path.Combine(args[1],"src/data/pokemon/tmhm_learnsets.h"));
string tutor=File.ReadAllText(Path.Combine(args[1],"src/data/pokemon/tutor_learnsets.h"));
int groups=0,checks=0,failures=0;var results=new List<object>();
const WildBattleSide P=WildBattleSide.Player,O=WildBattleSide.Opponent;
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
void Test(string label,Action action){groups++;try{action();results.Add(new{label,passed=true});Console.WriteLine("PASS "+label);}catch(Exception e){failures++;results.Add(new{label,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+label+" "+e);}}
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,string[] moves,int level=50,uint pid=0,IvPolicy? iv=null){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,iv??IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 foreach(string m in moves){string t=Regex.Match(tm,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;string u=Regex.Match(tutor,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;
 Check(catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==Id(m)&&x.Level<=level)||Regex.IsMatch(t,@"(?:TM|HM)\d\d_"+m+@"\)")||u.Contains("TUTOR(MOVE_"+m+")"),"real learned/TM/tutor "+species+"/"+m);}
 p=p with{Moves=moves.Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()};Check(SourcePokemonValidator.ValidatePermanent(proof,p)is null,"authentic permanent identity");return new(p){FirstTurnCounter=1};}
WildBattleState Pair(WildBattleCombatant a,WildBattleCombatant b,uint seed=0)=>new(a,b,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
WildBattleResult Act(WildBattleState s,int slot=0,WildBattleSide side=P,string? called=null){var r=called is null?WildBattleRules.ResolveSourceMoveAction(catalog,s,new(side,slot)).Battle:WildBattleRules.ResolveCalledMoveAction(catalog,s,new(side,slot,Id(called))).Battle;Check(r.Succeeded,"source action "+called+" "+r.Fault);uint seed=s.Rng.Seed;foreach(var d in r.Trace){seed=unchecked(seed*1103515245+24691);Check(d.Raw==(ushort)(seed>>16),"exact source LCG "+d.Purpose);}Check(r.State!.Rng.Seed==seed&&r.State.Rng.Cursor==s.Rng.Cursor+(ulong)r.Trace.Length,"terminal LCG");return r;}
bool Guard(WildBattleResult r)=>r.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_WONDER_GUARD");
SourceBattleFrame Frame(params WildBattleCombatant[] m)=>new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),Parties=[new(BattleSide.Player,[new(m[0].Pokemon,m[0].HeldItem,m[0].Status),new(m[2].Pokemon,m[2].HeldItem,m[2].Status)]),new(BattleSide.Opponent,[new(m[1].Pokemon,m[1].HeldItem,m[1].Status),new(m[3].Pokemon,m[3].HeldItem,m[3].Status)])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};
SourceKernelResult Run(SourceBattleFrame f,int a=0,int t=1,int slot=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[a].Occupant!.Value,f.Revision,slot,new(t)));Check(r.Succeeded,"four action "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid four result");return r;}
var baseline = new List<object>();
foreach(var row in new[]{("SQUIRTLE","WATER_GUN","BULBASAUR"),("GROWLITHE","EMBER","RATTATA"),("RATTATA","TACKLE","GASTLY"),("CLEFAIRY","METRONOME","SNORLAX")})
{
 var state=Pair(Mon(row.Item1,[row.Item2]),Mon(row.Item3,[row.Item3=="GASTLY"?"LICK":"TACKLE"]));
 var result=Act(state,called:row.Item1=="CLEFAIRY"?"DRAGON_RAGE":null);
 baseline.Add(new{actor=row.Item1,move=row.Item2,target=row.Item3,hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result))))});
}
if(args.Length==5&&args[4]=="--capture"){File.WriteAllText(args[3],JsonSerializer.Serialize(baseline)+"\n");Console.WriteLine("Captured four immutable non-WG results");return 0;}
Test("four immutable pre-edit non-WG state/event/RNG comparisons",()=>{
 string file=Path.Combine(Path.GetDirectoryName(args[2])!,"non-wg-before.json");
 Check(File.Exists(file),"required immutable pre-edit artifact");
 Check(JsonSerializer.Serialize(baseline)==JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file))),"exact immutable non-WG result hashes");
});
Test("source fragment result flags, mixed rows, dynamic types, powers and charging",()=>{
 var type=typeof(SourceBattleFrame).Assembly.GetType("Game.Domain.Battle.SourceTypeResultRules")!;var command=typeof(SourceBattleFrame).Assembly.GetType("Game.Domain.Battle.SourceTypeCommand")!;
 var calculate=type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="Calculate"&&m.GetParameters()[0].ParameterType==typeof(ElementType));
 using var doc=JsonDocument.Parse(File.ReadAllText(args[2]));var rows=doc.RootElement.GetProperty("vectors").EnumerateArray().ToArray();Check(rows.Length>=1000,"nonempty source vectors");
 foreach(var row in rows){var i=row.GetProperty("input").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var o=row.GetProperty("output").EnumerateArray().Select(x=>x.GetInt32()).ToArray();int at=i[0]==1||i[10]<0?i[1]:i[10];
 var x=calculate.Invoke(null,[(ElementType)at,(ElementType)i[2],(ElementType)i[3],i[4]==25?"ABILITY_WONDER_GUARD":"ABILITY_LEVITATE",i[6]!=0,i[5],i[9]!=0,o[6]==2,Enum.ToObject(command,i[0])])!;
 bool Flag(string n)=>(bool)x.GetType().GetProperty(n,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance)!.GetValue(x)!;
 Check(Flag("WonderGuard")== (o[2]==25),"source WG "+string.Join(',',i));
 if(i[0]!=2)Check((Flag("TypeImmune")||Flag("Levitate"))==((o[0]&8)!=0),"source DOESNT_AFFECT "+string.Join(',',i));
 }
 Check(Environment.Version.Major==10,"actual .NET 10");
});
Test("real Shedinja normal hit blocks with avoided-damage and cleared history on both sides",()=>{
 foreach(bool reverse in new[]{false,true})foreach(var pair in new[]{("SQUIRTLE","WATER_GUN"),("PIKACHU","THUNDER_SHOCK"),("RATTATA","TACKLE"),("MEOWTH","FURY_SWIPES")}){
 var a=Mon(pair.Item1,[pair.Item2]);var sh=Mon("SHEDINJA",["HARDEN"]) with{LastLandedMove=Id("EMBER"),SourceMoveHistory=new(){LastHitByType="TYPE_FIRE"}};
 var s=reverse?Pair(sh,a):Pair(a,sh);var r=Act(s,side:reverse?O:P);var target=reverse?r.State!.Player:r.State!.Opponent;
 Check(Guard(r)&&target.Pokemon.CurrentHp==1,"WG blocks "+pair);Check(target.LastLandedMove?.Value=="pret:MOVE_UNAVAILABLE"&&target.SourceMoveHistory?.LastHitByType=="TYPE_NORMAL","last hit cleared");
 Check(!r.Events.OfType<WildBattleEffectivenessEvent>().Any()&&!r.Events.OfType<WildBattleCriticalHitEvent>().Any(),"avoided damage text dominates");
 Check(!r.Trace.Any(x=>x.Purpose.Contains("static")||x.Purpose.Contains("king")),"no false contact/item draw");
 }
});
Test("super-effective attacks and Struggle bypass; status moves bypass",()=>{
 foreach(var pair in new[]{("GROWLITHE","EMBER"),("PIDGEY","GUST")}){var r=Act(Pair(Mon(pair.Item1,[pair.Item2]),Mon("SHEDINJA",["HARDEN"])));Check(!Guard(r)&&r.State!.Opponent.Pokemon.CurrentHp==0,"super hit "+pair);}
 var tox=Act(Pair(Mon("SQUIRTLE",["TOXIC"]),Mon("SHEDINJA",["HARDEN"])));Check(!Guard(tox)&&tox.State!.Opponent.Status!=0,"status bypass");
 var exhausted=Mon("RATTATA",["TACKLE"]);exhausted=exhausted with{Pokemon=exhausted.Pokemon with{Moves=exhausted.Pokemon.Moves.Select(x=>x with{CurrentPp=0}).ToImmutableArray()}};
 var ss=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[exhausted],0,Mon("SHEDINJA",["HARDEN"]),new(0));Check(ss.Succeeded,"Struggle entry");
 var st=WildBattleRules.ResolveMove(catalog,ss.State!,WildBattleRules.StruggleSlot);Check(st.Succeeded,"Struggle full turn "+st.Fault);Check(!Guard(st)&&st.State!.Opponent.Pokemon.CurrentHp==0&&st.Events.OfType<WildBattleRecoilEvent>().Any(),"Struggle typeless with recoil");
});
Test("fixed damage and OHKO use source type command; protect/miss and charge order",()=>{
 foreach(string move in new[]{"DRAGON_RAGE","SONIC_BOOM","SEISMIC_TOSS","NIGHT_SHADE","PSYWAVE","SUPER_FANG","FISSURE","SHEER_COLD"}){
 var r=Act(Pair(Mon("CLEFAIRY",["METRONOME"]),Mon("SHEDINJA",["HARDEN"])),called:move);bool se=move=="NIGHT_SHADE";Check(Guard(r)!=se,"fixed WG branch "+move);Check(r.State!.Opponent.Pokemon.CurrentHp==(se?0:1),"fixed HP "+move);}
 var protectedState=Pair(Mon("SQUIRTLE",["WATER_GUN"]),Mon("SHEDINJA",["PROTECT"]) with{Protected=true});var p=Act(protectedState);Check(!Guard(p)&&!p.Trace.Any(x=>x.Purpose.EndsWith("accuracy")),"Protect before ability probe");
 bool found=false;for(uint seed=0;seed<100;seed++){var s=Pair(Mon("SQUIRTLE",["WATER_GUN"]) with{AccuracyStage=0},Mon("SHEDINJA",["HARDEN"]),seed);var r=Act(s);if(!r.Trace.Any(x=>x.Purpose.EndsWith("critical"))){Check(Guard(r)&&!r.Events.OfType<WildBattleMoveMissedEvent>().Any(),"ordinary miss WG text");found=true;break;}}Check(found,"miss branch reached");
 var c=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("BULBASAUR",["SOLAR_BEAM"])],0,Mon("SHEDINJA",["HARDEN"]),new(0));Check(c.Succeeded,"charge entry");var first=WildBattleRules.ResolveMove(catalog,c.State!,0);Check(first.Succeeded,"charge preparation");Check(!Guard(first)&&first.State!.Player.CalledTurnState?.Lock is not null,"charge first turn no WG");var second=WildBattleRules.ResolveMove(catalog,first.State!,0);Check(second.Succeeded,"charge release");Check(Guard(second)&&second.State!.Opponent.Pokemon.CurrentHp==1,"charge release WG");
});
Test("actual Trace and Transform acquire WG without changing permanent identity",()=>{
 var entry=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("PORYGON",["TACKLE"])],0,Mon("SHEDINJA",["HARDEN"]),new(0));Check(entry.Succeeded,"Trace entry "+entry.Fault);Check(WildBattleEffective.Ability(entry.State!.Player)=="ABILITY_WONDER_GUARD"&&entry.State.Player.Pokemon.AbilityId=="ABILITY_TRACE","actual Trace");
 var transformed=Act(Pair(Mon("DITTO",["TRANSFORM"]),Mon("SHEDINJA",["SHADOW_BALL"])));Check(WildBattleEffective.Ability(transformed.State!.Player)=="ABILITY_WONDER_GUARD"&&transformed.State.Player.Transform is not null,"actual Transform");
 var hit=Act(transformed.State!,side:O);Check(!Guard(hit)&&hit.State!.Player.Pokemon.CurrentHp<transformed.State.Player.Pokemon.CurrentHp,"copied Ghost weakness receives hit");
});
Test("actual full singles turn and four-slot target pass keep PP and items separate",()=>{
 var entry=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("SQUIRTLE",["WATER_GUN"])],0,Mon("SHEDINJA",["HARDEN"]),new(0));Check(entry.Succeeded,"single full entry");var turn=WildBattleRules.ResolveMove(catalog,entry.State!,0);Check(turn.Succeeded&&turn.State!.Turn==1&&Guard(turn)&&turn.State.Opponent.Pokemon.CurrentHp==1,"whole turn WG");
 var f=Frame(Mon("BLASTOISE",["SURF"]) with{HeldItem="ITEM_SHELL_BELL"},Mon("SHEDINJA",["HARDEN"]),Mon("MAGIKARP",["SPLASH"]),Mon("LAPRAS",["GROWL"],pid:1));var r=Run(f);
 Check(r.Events.OfType<SourceAbilityPreventedEvent>().Count(e=>e.Ability=="ABILITY_WONDER_GUARD")==1,"one WG target message");Check(r.State.Slots[1].Body!.Pokemon.CurrentHp==1&&r.State.Slots[3].Body!.Pokemon.CurrentHp<f.Slots[3].Body!.Pokemon.CurrentHp,"spread independent targets");
 Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==f.Slots[0].Body!.Pokemon.Moves[0].CurrentPp-1,"one source PP");
 Check(r.State.Slots[1].Registers.History?.LastHitByType=="TYPE_NORMAL"&&r.State.Slots[1].Body!.LastLandedMove?.Value=="pret:MOVE_UNAVAILABLE","four history WG");
});
Test("dynamic Hidden Power and Weather Ball retain genuine type, power and history",()=>{
 foreach(int iv in new[]{0,31}){var r=Act(Pair(Mon("BULBASAUR",["HIDDEN_POWER"],iv:IvPolicy.Fixed(iv)),Mon("SHEDINJA",["HARDEN"])));Check(Guard(r)==(iv==0),"Hidden Power derived type "+iv);}
 var noWeather=Act(Pair(Mon("CASTFORM",["WEATHER_BALL"]),Mon("SHEDINJA",["HARDEN"])));Check(Guard(noWeather),"normal Weather Ball WG");
 var sunny=Pair(Mon("CASTFORM",["SUNNY_DAY","WEATHER_BALL"]),Mon("SHEDINJA",["HARDEN"]));var set=Act(sunny);var fire=Act(set.State!,1);Check(!Guard(fire)&&fire.State!.Opponent.Pokemon.CurrentHp==0,"sun Fire Weather Ball passes");
});
Test("actual Counter records no damage receipt and retains typecalc2 last-hit type",()=>{
 var s=Pair(Mon("WOBBUFFET",["COUNTER"],100),Mon("SHEDINJA",["SHADOW_BALL"]));var hit=Act(s,side:O);Check(hit.State!.Player.Pokemon.CurrentHp<s.Player.Pokemon.CurrentHp,"actual physical receipt");
 var marked=hit.State! with{Opponent=hit.State.Opponent with{SourceMoveHistory=(hit.State.Opponent.SourceMoveHistory??new()) with{LastHitByType="TYPE_FIRE"}}};
 var counter=Act(marked);Check(Guard(counter)&&counter.State!.Opponent.Pokemon.CurrentHp==1,"Counter blocked");Check(counter.State!.Opponent.SourceMoveHistory!.LastHitByType=="TYPE_FIRE","typecalc2 does not clear type");
});
Test("real Trace, party switches and Conversion expose WG crash and Present",()=>{
 var entry=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("SHEDINJA",["HARDEN"]),Mon("HITMONLEE",["HI_JUMP_KICK"]),Mon("CLEFAIRY",["METRONOME"])],0,Mon("PORYGON",["PSYBEAM","CONVERSION"],100),new(0));Check(entry.Succeeded,"Trace entry");
 var switched=WildBattleRules.ResolveCalledPartySwitch(catalog,entry.State!,new(P,1));Check(switched.Succeeded,"real donor switches out "+switched.Fault);
 var hurt=Act(switched.State!);Check(hurt.State!.Opponent.Pokemon.CurrentHp>0&&hurt.State.Opponent.Pokemon.CurrentHp<switched.State!.Opponent.Pokemon.CurrentHp,"super-effective hit wounds traced Normal target");
 var converted=Act(hurt.State!,1,O);var traced=converted.State!.Opponent;Check(WildBattleEffective.Types(catalog,traced)==("TYPE_PSYCHIC","TYPE_PSYCHIC"),"actual Conversion after Trace");
 bool found=false;for(uint seed=0;seed<100;seed++){var s=converted.State with{Player=converted.State.Player with{AccuracyStage=0},Rng=new(seed)};var r=Act(s);if(!r.Trace.Any(t=>t.Purpose.EndsWith("critical"))){Check(r.Events.OfType<WildBattleAbilityPreventedEvent>().Count(e=>e.Ability=="ABILITY_WONDER_GUARD")==1,"one miss result message");Check(r.State!.Player.Pokemon.CurrentHp<s.Player.Pokemon.CurrentHp,"cleared MISSED permits crash");found=true;break;}}Check(found,"crash branch");
 var switchAgain=WildBattleRules.ResolveCalledPartySwitch(catalog,converted.State,new(P,2));Check(switchAgain.Succeeded,"real Present user switch "+switchAgain.Fault);
 found=false;for(uint seed=0;seed<100;seed++){var r=Act(switchAgain.State! with{Rng=new(seed)},called:"PRESENT");if(r.Events.OfType<WildBattleRecoveredEvent>().Any()){Check(r.State!.Opponent.Pokemon.CurrentHp==traced.Pokemon.CurrentHp,"Present healing does not clear WG MISSED");Check(r.State.Opponent.SourceMoveHistory?.LastHitByType=="TYPE_NORMAL","Present typecalc history");found=true;break;}}Check(found,"Present heal branch");
});
Test("WG preserves Focus Band command damage and suppresses false survival or critical text",()=>{
 bool found=false;for(uint seed=0;seed<200;seed++){
 var s=Pair(Mon("SQUIRTLE",["WATER_GUN"]),Mon("SHEDINJA",["HARDEN"]) with{HeldItem="ITEM_FOCUS_BAND"},seed);var r=Act(s);
 if(r.Events.OfType<WildBattleDamageAdjustedEvent>().Any(e=>e.HungOn)){Check(Guard(r)&&r.State!.Opponent.Pokemon.CurrentHp==1,"Focus Band numeric path retains WG");Check(r.Events.OfType<WildBattleDamageAdjustedEvent>().Any(e=>e.DamageBefore>0&&e.DamageAfter==0),"raw register retained for adjustnormaldamage");Check(!r.Events.OfType<WildBattleHeldItemSurvivalEvent>().Any()&&!r.Events.OfType<WildBattleCriticalHitEvent>().Any(),"WG has result text priority");found=true;break;}}
 Check(found,"positive source Focus Band branch");
});
Test("real four-position Trace entry and complete four-action turn",()=>{
 var f=Frame(Mon("PORYGON",["TACKLE"]),Mon("SHEDINJA",["HARDEN"]),Mon("SQUIRTLE",["WATER_GUN"]),Mon("SHEDINJA",["HARDEN"]));
 var entry=SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision);Check(entry.Succeeded,"four Trace entry "+entry.Fault);f=entry.State;Check(WildBattleEffective.Ability(f.Slots[0].Body!)=="ABILITY_WONDER_GUARD","actual four Trace donor");
 var ally=Run(f,2,0);Check(ally.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Target.Value==0&&e.Ability=="ABILITY_WONDER_GUARD"),"copied WG on ally-targeted attack");
 var choices=f.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot.Value%2==1?c.Slot:c.Slot.Opposite)).ToImmutableArray();
 var r=SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices);Check(r.Succeeded,"four turn begin "+r.Fault);var events=r.Events.ToList();
 while(r.State.FourTurn is not null){Check(r.State.FourTurn.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume,"bounded turn phase");r=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);Check(r.Succeeded,"four advance "+r.Fault);events.AddRange(r.Events);}
 Check(r.CompletedCleanTurn&&events.OfType<SourceAbilityPreventedEvent>().Count(e=>e.Ability=="ABILITY_WONDER_GUARD")==2,"four turn completes two blocked hits");
});
Test("integrated weather hit and Serene Grace no-effect preserve utility bypass",()=>{
 foreach(bool reverse in new[]{false,true}) {
  var donor=Mon("DUNSPARCE",["THUNDER"]);Check(donor.Pokemon.AbilityId=="ABILITY_SERENE_GRACE","authentic Serene Grace donor");
  var target=Mon("SHEDINJA",["HARDEN"]);var state=reverse?Pair(target,donor):Pair(donor,target);
  var r=Act(state,side:reverse?O:P);var body=reverse?r.State!.Player:r.State!.Opponent;
  Check(Guard(r)&&body.Pokemon.CurrentHp==1&&body.Status==0,"Thunder WG blocks damage and doubled secondary");
  Check(r.Trace.Count(x=>x.Purpose.Contains("secondary"))==1,"no-effect secondary still consumes source command draw");
 }
 var f=Frame(Mon("DUNSPARCE",["THUNDER"]),Mon("SHEDINJA",["HARDEN"]),Mon("MAGIKARP",["SPLASH"]),Mon("SHEDINJA",["HARDEN"]));
 var four=Run(f);Check(four.State.Slots[1].Body!.Pokemon.CurrentHp==1&&four.State.Slots[1].Body!.Status==0,"four Thunder secondary is not applied through WG");
 Check(four.Events.OfType<SourceAbilityPreventedEvent>().Any(x=>x.Ability=="ABILITY_WONDER_GUARD"),"four Thunder WG message");
 var pain=Act(Pair(Mon("MISDREAVUS",["PAIN_SPLIT"]),Mon("SHEDINJA",["HARDEN"])));Check(!Guard(pain)&&pain.State!.Player.Pokemon.CurrentHp<pain.State.Player.Pokemon.Stats.MaxHp,"power-zero Pain Split bypass");
 var sacrifice=Act(Pair(Mon("GRIMER",["MEMENTO"],80),Mon("SHEDINJA",["HARDEN"])));Check(!Guard(sacrifice)&&sacrifice.State!.Player.Pokemon.CurrentHp==0&&sacrifice.State.Opponent.AttackStage==4,"power-zero Memento bypass");
});
var abilityPredicate=typeof(SourceBattleAdmission).GetMethod("SupportsAbility",BindingFlags.NonPublic|BindingFlags.Static)!;
var officialAbilities=proof.Abilities.Keys.Where(x=>x is not ("ABILITY_NONE" or "ABILITY_CACOPHONY")).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
var admittedAbilities=officialAbilities.Where(x=>(bool)abilityPredicate.Invoke(null,[catalog,x])!).ToArray();
var admittedMoves=catalog.Moves.Values.Where(SourceBattleKernel.SupportsRecipe).Select(x=>x.Id.Value).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
File.WriteAllText(args[3],JsonSerializer.Serialize(new{groups,checks,failures,runtime=Environment.Version.ToString(),results,admittedAbilities,abilityDenominator=officialAbilities.Length,recipeAdmission=admittedMoves.Length,admittedMoves},new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine($"{groups} groups; {checks} checks; {failures} failures");return failures==0?0:1;
