using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Capture;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 2) { Console.Error.WriteLine("CONTENT_ROOT OUTPUT_PREFIX"); return 2; }
string testRoot=Path.GetDirectoryName(Path.GetFullPath("tests/Game.SourceCaptureBalls.Tests/Program.cs"))!;
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog=proof.Catalog;
string Json<T>(T value)=>JsonSerializer.Serialize(value);
int checks=0;var results=new List<(string Name,string? Error)>();var captures=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Equal<T>(T expected,T actual,string message)=>Check(EqualityComparer<T>.Default.Equals(expected,actual),$"{message}: expected {expected}; got {actual}");
void Same<T>(T expected,T actual,string message)=>Check(JsonNode.DeepEquals(JsonNode.Parse(Json(expected)),JsonNode.Parse(Json(actual))),message);
void Test(string name,Action run){try{run();results.Add((name,null));Console.WriteLine("PASS "+name);}catch(Exception e){results.Add((name,e.ToString()));Console.WriteLine("FAIL "+name+" "+e.Message);}}
JsonDocument Fixture(string file,string hash){var bytes=File.ReadAllBytes(Path.Combine(testRoot,file));Equal(hash,Convert.ToHexStringLower(SHA256.HashData(bytes)),"frozen "+file);return JsonDocument.Parse(bytes);}
MoveId Move(string name)=>new("pret:MOVE_"+name); SpeciesId Species(string name)=>new("pret:SPECIES_"+name);
WildBattleCombatant Mon(string species,string move="DEFENSE_CURL",int level=40){
 var p=SourcePokemonFactory.Create(proof,new(Species(species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 int pp=catalog.Moves[Move(move)].Pp;return new(p with{Moves=[new(Move(move),pp,pp)]});
}
WildBattleState Start(WildBattleCombatant? player=null,WildBattleCombatant? foe=null,uint seed=31,WildBattleTerrain terrain=WildBattleTerrain.Plain,WildBattleCombatant? reserve=null){
 var party=reserve is null?ImmutableArray.Create(player??Mon("SNORLAX")):ImmutableArray.Create(player??Mon("SNORLAX"),reserve);
 var r=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,party,0,foe??Mon("RATTATA"),new(seed),terrain);Check(r.Succeeded,"source entry "+r.Fault);return r.State!;
}
WildBattleCaptureContext Context(bool underwater=false,params string[] caught)=>new(underwater,caught.Select(Species).ToImmutableHashSet());
WildBattleCaptureActionResult Throw(WildBattleState state,CaptureBall ball,WildBattleCaptureContext? context=null){
 var r=WildBattleRules.ResolveBall(proof,state,ball,context??Context());Check(r.Succeeded,"capture accepted "+r.Battle.Fault);
 Equal<string?>(null,WildBattleRules.Validate(catalog,r.Battle.State),"capture postcondition");
 var replay=WildBattleRules.ResolveBall(proof,JsonSerializer.Deserialize<WildBattleState>(Json(state))!,ball,context??Context());Same(r,replay,"exact JSON resumed state, events and RNG");
 uint seed=state.Rng.Seed;foreach(var t in r.Battle.Trace){seed=unchecked(seed*1103515245+24691);Equal(seed,t.After,"LCG after");Equal((ushort)(seed>>16),t.Raw,"LCG output");}
 Equal(seed,r.Battle.State!.Rng.Seed,"committed seed");Equal(state.Rng.Cursor+(ulong)r.Battle.Trace.Length,r.Battle.State.Rng.Cursor,"committed cursor");
 return r;
}
WildBattleCaptureResultEvent Catch(WildBattleCaptureActionResult r)=>r.Battle.Events.OfType<WildBattleCaptureResultEvent>().Single();
WildBattleState Turn(WildBattleState state){var r=WildBattleRules.ResolveMove(catalog,state,0);Check(r.Succeeded,"real move turn "+r.Fault);Equal<string?>(null,WildBattleRules.Validate(catalog,r.State),"turn postcondition");return r.State!;}
void Atomic(WildBattleState state,WildBattleCaptureActionResult r,string message){Check(!r.Succeeded&&ReferenceEquals(state,r.Battle.State)&&r.Battle.Events.IsEmpty&&r.Battle.Trace.IsEmpty&&r.CapturedPokemon is null&&r.CapturedStatus is null&&r.CapturedHeldItem is null,message);}

Test("official hosted runtime",()=>Equal(10,Environment.Version.Major,"runtime major"));
Test("872 complete pre-edit core and historical action results",()=>{
 using var doc=Fixture("legacy-results.json","3a82e87489ee74ad35c6a06743e51d3cf7dfffb247b50992b7fa8d02962d5289");var rows=doc.RootElement.GetProperty("results");Equal(872,rows.GetArrayLength(),"nonempty frozen corpus");
 foreach(var row in rows.EnumerateArray()){
  object actual;
  if(row.GetProperty("kind").GetString()=="core"){
   var input=row.GetProperty("input").Deserialize<CaptureInput>();var expected=row.GetProperty("result").Deserialize<CaptureResult>()!;
   var first=expected.RandomTrace.FirstOrDefault();var initial=first is null?expected.Rng:new RngState(first.Before,7);actual=CaptureRules.Resolve(input,initial);
  }else actual=WildBattleRules.ResolveBall(catalog,row.GetProperty("state").Deserialize<WildBattleState>()!,row.GetProperty("ball").Deserialize<CaptureBall>());
  Check(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("result").GetRawText()),JsonNode.Parse(Json(actual))),"full result corpus mismatch");
 }
});
Test("3011 extracted original C boundary results",()=>{
 using var doc=Fixture("source-vectors.json","8c2b331c6aac2c8fde0d95e3ce6e49ff6d7db151dcd5b02b615d5d551a04ddf0");var rows=doc.RootElement.GetProperty("vectors");Equal(3011,rows.GetArrayLength(),"source matrix count");
 foreach(var row in rows.EnumerateArray()){
  var x=row.GetProperty("input");int I(string n)=>x.GetProperty(n).GetInt32();var o=row.GetProperty("output").EnumerateArray().Select(v=>v.GetInt64()).ToArray();
  var status=I("status") switch{1=>CaptureStatus.Sleep,32=>CaptureStatus.Freeze,8=>CaptureStatus.Poison,16=>CaptureStatus.Burn,64=>CaptureStatus.Paralysis,128=>CaptureStatus.BadPoison,_=>CaptureStatus.None};
  var ball=(CaptureBall)I("ball");var c=new CaptureBallContext(I("level"),I("type1") is 6 or 11||I("type2") is 6 or 11,I("mapType")==5,I("caught")==1,I("counter"));
  var r=CaptureRules.Resolve(new(I("maxHp"),I("hp"),I("catchRate"),ball,status),new(x.GetProperty("seed").GetUInt32(),7),c);Check(r.Calculation is not null,"source case supported");var calc=r.Calculation!;
  Equal(o[0],(long)calc.BallMultiplierTenths,"source byte multiplier");Equal(o[1],(long)calc.BallAdjustedCatchRate,"source first integer floor");Equal(o[2],(long)calc.HpAdjustedCatchValue,"source HP floor");Equal(o[3],(long)calc.CatchValue,"source status floor");Equal(o[4],calc.ShakeThreshold is null?-1:(long)calc.ShakeThreshold,"source threshold");
  Equal(o[5],(long)r.ChecksPassed,"source comparisons");Equal(o[6],(long)r.AnimationCode!,"source animation");Equal(o[7],(long)r.Rng.Seed,"source final seed");Equal(o[8],(long)r.RandomTrace.Length,"source RNG count");Equal((ulong)(7+o[8]),r.Rng.Cursor,"source RNG cursor");
  for(int i=0;i<r.RandomTrace.Length;i++)Equal(o[9+i],(long)r.RandomTrace[i].Raw,"source raw draw");Equal((long)Math.Min(255,I("counter")+1),o[13],"source saturating counter");
  Equal(o[6]==4?CaptureOutcome.Caught:CaptureOutcome.Escaped,r.Outcome,"source outcome");Equal(o[4]<0?CaptureDecisionBranch.GuaranteedCatchValue:ball==CaptureBall.Master?CaptureDecisionBranch.MasterBallOverride:CaptureDecisionBranch.ShakeChecks,r.Branch!.Value,"source branch");
 }
});
Test("all context and mode failures preserve exact RNG",()=>{
 var seed=new RngState(31,7);var input=new CaptureInput(101,50,45,CaptureBall.Timer);
 foreach(var mode in Enum.GetValues<CaptureEncounterKind>().Where(m=>m!=CaptureEncounterKind.OrdinaryWild)){
  var r=CaptureRules.Resolve(input with{Encounter=mode},seed,new(40,false,false,false,0));Check(r.Outcome==CaptureOutcome.Unsupported&&r.Rng==seed&&r.RandomTrace.IsEmpty,"independent mode rejects "+mode);
 }
 foreach(var ball in new[]{CaptureBall.Net,CaptureBall.Dive,CaptureBall.Nest,CaptureBall.Repeat,CaptureBall.Timer}){var r=CaptureRules.Resolve(input with{Ball=ball},seed,null);Equal("ball-context-required",r.Diagnostic,"context mandatory");Check(r.RandomTrace.IsEmpty&&r.Rng==seed,"missing context atomic");}
 foreach(var context in new[]{new CaptureBallContext(0,false,false,false,0),new(101,false,false,false,0),new(40,false,false,false,-1),new(40,false,false,false,256)}){var r=CaptureRules.Resolve(input,seed,context);Check(r.Outcome==CaptureOutcome.InvalidInput&&r.RandomTrace.IsEmpty&&r.Rng==seed,"invalid context atomic");}
 Equal(CaptureOutcome.Unsupported,CaptureRules.Resolve(input with{Ball=CaptureBall.Safari},seed,new(40,false,false,false,0)).Outcome,"Safari remains separate");
 foreach(var ball in new[]{CaptureBall.Luxury,CaptureBall.Premier})Equal(10,CaptureRules.Resolve(input with{Ball=ball},seed,null).Calculation!.BallMultiplierTenths,"unconditional 1x");
 var exhausted=new RngState(31,ulong.MaxValue);var failure=CaptureRules.Resolve(input,exhausted,new(40,false,false,false,246));Check(failure.Outcome==CaptureOutcome.InvalidInput&&failure.Rng==exhausted&&failure.RandomTrace.IsEmpty,"zero-value exhausted cursor atomic");
});
Test("all eleven ordinary balls execute actual selected enemy and delivery boundaries",()=>{
 foreach(var ball in Enum.GetValues<CaptureBall>().Where(b=>b!=CaptureBall.Safari))foreach(uint seed in new uint[]{0,31,42}){
  var state=Start(seed:seed);var result=Throw(state,ball);var e=Catch(result);var next=result.Battle.State!;
  captures.Add(new{ball,seed,calculation=e.Calculation,outcome=e.Outcome,turn=next.Turn,trace=result.Battle.Trace.Select(t=>t.Purpose).ToArray()});
  if(e.Outcome==CaptureOutcome.Caught){Same(WildBattleTransformationRules.PersistentPokemon(state.Opponent),result.CapturedPokemon,"original captured instance");Equal(state.Turn,next.Turn,"success skips boundary");Check(!result.Battle.Events.OfType<WildBattleTurnReadyEvent>().Any()&&!result.Battle.Events.OfType<WildBattlePpCommandEvent>().Any(),"success no opponent PP or end-turn");}
  else{Check(result.CapturedPokemon is null&&result.CapturedStatus is null&&result.CapturedHeldItem is null,"failure no delivery");Equal(state.Turn+1,next.Turn,"failed throw full boundary");Equal(state.Opponent.Pokemon.Moves[0].CurrentPp-1,next.Opponent.Pokemon.Moves[0].CurrentPp,"one actual foe action");Equal(1,result.Battle.Events.OfType<WildBattlePpCommandEvent>().Count(e=>e.Actor==WildBattleSide.Opponent),"one enemy PP command");}
  Check(!result.Battle.Trace.Any(t=>t.Purpose=="wild-battle.actions.speed-tie"),"item precedes move without action speed tie");
 }
});
Test("Net reads both live types and actual Color Change",()=>{
 foreach(string name in new[]{"RATTATA","MAGIKARP","CATERPIE","SURSKIT"}){
  var state=Start(foe:Mon(name));int expected=name=="RATTATA"?10:30;Equal(expected,Catch(Throw(state,CaptureBall.Net)).Calculation.BallMultiplierTenths,"natural type "+name);
 }
 var pair=Start(Mon("SQUIRTLE","WATER_GUN",20),Mon("KECLEON","DEFENSE_CURL",100));var changed=Turn(pair);Equal(("TYPE_WATER","TYPE_WATER"),WildBattleEffective.Types(catalog,changed.Opponent),"real water Color Change");
 Equal(30,Catch(Throw(changed,CaptureBall.Net)).Calculation.BallMultiplierTenths,"Net live Color Change");
 var later=changed with{Player=changed.Player with{Pokemon=changed.Player.Pokemon with{Moves=[new(Move("TACKLE"),35,35)]}}};
 later=later with{CalledParty=later.CalledParty! with{Player=later.CalledParty.Player.SetItem(0,later.CalledParty.Player[0] with{Pokemon=later.Player.Pokemon})}};
 var normal=Turn(later);Equal(("TYPE_NORMAL","TYPE_NORMAL"),WildBattleEffective.Types(catalog,normal.Opponent),"real normal Color Change");Equal(10,Catch(Throw(normal,CaptureBall.Net)).Calculation.BallMultiplierTenths,"Net no stale water bonus");
});
Test("Dive requires the actual underwater flag independently of terrain",()=>{
 foreach(var terrain in new[]{WildBattleTerrain.Cave,WildBattleTerrain.Water,WildBattleTerrain.Pond,WildBattleTerrain.Underwater,WildBattleTerrain.Plain}){
  var state=Start(terrain:terrain);Equal(10,Catch(Throw(state,CaptureBall.Dive,Context(false))).Calculation.BallMultiplierTenths,"no map flag "+terrain);Equal(35,Catch(Throw(state,CaptureBall.Dive,Context(true))).Calculation.BallMultiplierTenths,"explicit underwater map "+terrain);
 }
});
Test("Nest source level-40 formula and Repeat effective Transform species",()=>{
 foreach(int level in new[]{1,29,30,31,39,40,100})Equal(Math.Max(10,40-level),Catch(Throw(Start(foe:Mon("RATTATA",level:level)),CaptureBall.Nest)).Calculation.BallMultiplierTenths,"Nest level "+level);
 var pair=Start(Mon("MAGIKARP"),Mon("DITTO","TRANSFORM"));var transformed=Turn(pair);Check(transformed.Opponent.Transform is not null,"actual opponent Transform");Equal(Species("MAGIKARP"),transformed.Opponent.Pokemon.Species,"live copied species");
 Equal(30,Catch(Throw(transformed,CaptureBall.Repeat,Context(false,"MAGIKARP"))).Calculation.BallMultiplierTenths,"Repeat copied caught species");Equal(10,Catch(Throw(transformed,CaptureBall.Repeat,Context(false,"DITTO"))).Calculation.BallMultiplierTenths,"Repeat not permanent Ditto species");
 Equal(30,Catch(Throw(transformed,CaptureBall.Net)).Calculation.BallMultiplierTenths,"Net copied types");
 var capture=Throw(transformed,CaptureBall.Master);Equal(Species("DITTO"),capture.CapturedPokemon!.Species,"permanent Ditto delivery");Same(WildBattleTransformationRules.PersistentPokemon(transformed.Opponent),capture.CapturedPokemon,"persistent PID IV original moves and live HP exact");
 var repeat=Throw(transformed,CaptureBall.Repeat,Context(false,"MAGIKARP"));Equal((uint)(catalog.Species[Species("MAGIKARP")].CatchRate*3),Catch(repeat).Calculation.BallAdjustedCatchRate,"catch rate is transformed battle species");
});
Test("Timer reads actual completed turns then byte wraps before cap",()=>{
 var state=Start();for(int turn=0;turn<3;turn++){Equal(turn,state.Turn,"earned turn counter");Equal(10+turn,Catch(Throw(state,CaptureBall.Timer)).Calculation.BallMultiplierTenths,"earned Timer multiplier");state=Turn(state);}
 foreach(int count in new[]{29,30,31,245,246,247,254,255}){
  var boundary=Start() with{Turn=count};var r=Throw(boundary,CaptureBall.Timer);Equal(Math.Min(40,(int)unchecked((byte)(count+10))),Catch(r).Calculation.BallMultiplierTenths,"byte boundary "+count);
  if(Catch(r).Outcome==CaptureOutcome.Escaped)Equal(Math.Min(255,count+1),r.Battle.State!.Turn,"source saturating ready boundary");
 }
});
Test("failed capture faint replacement resumes the existing source action",()=>{
 var player=Mon("RATTATA",level:5);player=player with{Pokemon=player.Pokemon with{CurrentHp=1}};
 var state=Start(player,Mon("SNORLAX","SEISMIC_TOSS",100),reserve:Mon("SNORLAX")) with{Turn=246};var r=Throw(state,CaptureBall.Timer);
 Equal(CaptureOutcome.Escaped,Catch(r).Outcome,"wrapped-zero guaranteed failure");Check(r.Battle.State!.AwaitingReplacement,"actual enemy move fainted player");Check(r.Battle.State.SourceWild!.Turn is{NextAction:2},"existing continuation retained");
 var resumed=WildBattleRules.ReplaceSourceWildPlayer(catalog,r.Battle.State,1);Check(resumed.Succeeded,"real reserve selected "+resumed.Fault);Check(!resumed.Events.OfType<WildBattleBallThrownEvent>().Any()&&!resumed.Events.OfType<WildBattlePpCommandEvent>().Any(),"resume no repeat throw or enemy move");Equal(247,resumed.State!.Turn,"one completed turn after replacement");
});
Test("capture item cleanup and persistent status are source-owned",()=>{
 var after=Turn(Start(Mon("SNORLAX","GRUDGE"),Mon("RATTATA")));Check(after.Player.CalledVolatile?.Grudge==true,"real Grudge active");var caught=Throw(after,CaptureBall.Master);Check(caught.Battle.State!.Player.CalledVolatile?.Grudge!=true,"ball cancels Grudge");
 var target=Mon("RATTATA") with{Status=2,HeldItem="ITEM_ORAN_BERRY"};var state=Start(foe:target);var r=Throw(state,CaptureBall.Master);Equal((uint?)2,r.CapturedStatus,"success retains actual sleep countdown");Equal("ITEM_ORAN_BERRY",r.CapturedHeldItem,"actual persistent held item");Equal(1,r.Battle.Trace.Count(t=>t.Purpose.StartsWith("capture:")),"Master still uses one draw");
});
Test("canonical boundary rejects modes and forged context without changes",()=>{
 var state=Start();foreach(var kind in Enum.GetValues<CaptureEncounterKind>().Where(x=>x!=CaptureEncounterKind.OrdinaryWild))Atomic(state,WildBattleRules.ResolveBall(proof,state,CaptureBall.Master,Context() with{Encounter=kind}),"mode "+kind);
 foreach(var ball in new[]{CaptureBall.Safari,(CaptureBall)0,(CaptureBall)13})Atomic(state,WildBattleRules.ResolveBall(proof,state,ball,Context()),"unsupported ball");
 Atomic(state,WildBattleRules.ResolveBall(proof,state,CaptureBall.Repeat,new(false,[new("pret:SPECIES_FORGED")])),"caught set authenticated species");
 Atomic(state,WildBattleRules.ResolveBall(null!,state,CaptureBall.Master,Context()),"no proof");
 var legacy=state with{SourceMechanicsRulesVersion=null};Atomic(legacy,WildBattleRules.ResolveBall(proof,legacy,CaptureBall.Master,Context()),"legacy not silently promoted");
 var story=state with{ScriptedSnorlax=true};Atomic(story,WildBattleRules.ResolveBall(proof,story,CaptureBall.Master,Context()),"story separate");
 var ghost=state with{TowerBattleKind=PokemonTowerBattleKind.DisguisedMarowak};Atomic(ghost,WildBattleRules.ResolveBall(proof,ghost,CaptureBall.Master,Context()),"ghost separate");
 var trainer=state with{CalledParty=state.CalledParty! with{BattleKind=WildBattleCalledBattleKind.Trainer}};Atomic(trainer,WildBattleRules.ResolveBall(proof,trainer,CaptureBall.Master,Context()),"trainer separate");
 var rejected=WildBattleRules.ResolveBall(catalog,state,CaptureBall.Ultra);Check(!rejected.Succeeded&&ReferenceEquals(state,rejected.State),"old regional API still Poké/Great only");
});
int failed=results.Count(r=>r.Error is not null);Check(results.Count==12,"nonempty exact suite");
var report=new{passed=failed==0,suite="Game.SourceCaptureBalls.Tests",runtime=Environment.Version.ToString(),sourceCommit=CaptureRules.SourceCommit,groups=results.Count,checks,failed,legacyFullResults=872,sourceCResults=3011,results=results.Select(r=>new{name=r.Name,passed=r.Error is null,error=r.Error}),captures};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);File.WriteAllText(args[1]+".json",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+"\n");
new XDocument(new XElement("testsuite",new XAttribute("name","Game.SourceCaptureBalls.Tests"),new XAttribute("tests",results.Count),new XAttribute("failures",failed),results.Select(r=>new XElement("testcase",new XAttribute("name",r.Name),r.Error is null?null:new XElement("failure",r.Error))))).Save(args[1]+".xml");
Console.WriteLine($"SOURCE_CAPTURE_BALLS_{(failed==0?"PASS":"FAIL")} groups={results.Count} checks={checks} failures={failed}");return failed==0?0:1;
