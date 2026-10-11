using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
if(args.Length<2)throw new ArgumentException("CONTENT_ROOT OUTPUT [--capture]");
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
string Json<T>(T x)=>JsonSerializer.Serialize(x);
string Hash<T>(T x)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(x))));
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(species=="GROWLITHE"?1u:0u)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
var original=PretRocketHideoutPokemon.Load(args[0]).Catalog;
var supplement=PretVermilionGymTrainers.ExtendPokemon(args[0],PretCeladonApproachPokemon.Load(args[0]).Catalog,celadonApproachRules:true);
var prior=original with{Moves=original.Moves.SetItems(supplement.Moves)};
var old=PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior,args[0],args[0]).Catalog,args[0]).Catalog,args[0]).Catalog;
var legacy=PretMetronomeMoveCatalog.Load(args[0]).Extend(PretFuchsiaRoutePokemon.Load(old,args[0]).Catalog);
var names=new[]{"RAGE","FURY_CUTTER","TRIPLE_KICK","FAKE_OUT"};
var snapshots=new List<object>();
foreach(bool canonical in new[]{false,true})foreach(string move in names)foreach(int scenario in Enumerable.Range(0,10)){
 bool reverse=scenario%2==1;uint seed=31;
 var actor=Mon("CLEFAIRY",60,"METRONOME") with{FirstTurnCounter=2};var target=Mon(scenario==3?"GASTLY":"SNORLAX",100,scenario==3?"NIGHT_SHADE":"DEFENSE_CURL");
 if(scenario==1)target=Hp(target,1);if(scenario==2)target=target with{CalledVolatile=new(){SubstituteHp=1}};
 if(scenario==4)target=target with{Protected=true};if(scenario==5)target=Hp(target with{Enduring=true},1);
 if(scenario==6){actor=actor with{AccuracyStage=0};target=target with{EvasionStage=12};}
 if(scenario==7)actor=actor with{FirstTurnCounter=0};
 if(scenario==8)actor=actor with{FuryCutterCounter=4};
 if(scenario==9)target=target with{Rage=true};
 var state=new WildBattleState(reverse?target:actor,reverse?actor:target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null};
 var result=WildBattleRules.ResolveCalledMoveAction(canonical?catalog:legacy,state,new(reverse?WildBattleSide.Opponent:WildBattleSide.Player,0,Id(move)));
 if(!result.Succeeded)throw new Exception($"baseline {canonical}/{move}/{scenario}: "+result.Fault);
 snapshots.Add(new{canonical,move,scenario,reverse,seed,sha256=Hash(result),result});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("Refuse to overwrite immutable baseline");
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasHitSequenceRecipe",BindingFlags.Static|BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unchanged a7d1936 product");
 File.WriteAllText(args[1],Json(new{baseCommit="a7d1936f9e5f446a36209599dc909749ec96eec0",snapshots}));Console.WriteLine($"Captured {snapshots.Count} complete pre-edit results");return 0;
}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
File.WriteAllText(args[1]+".singles-after.json",Json(snapshots));
Test("immutable canonical and historical complete results",()=>{
 using var b=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-before-a7d1936.json")));
 var rows=b.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(rows.Length==80&&rows.Length==snapshots.Count,"80 full results");int same=0,delta=0;
 for(int i=0;i<rows.Length;i++){
  var now=JsonSerializer.SerializeToElement(snapshots[i]);bool corrected=rows[i].GetProperty("canonical").GetBoolean()&&rows[i].GetProperty("move").GetString()=="RAGE"&&rows[i].GetProperty("scenario").GetInt32()==2;
  if(!corrected){Check(rows[i].GetProperty("sha256").GetString()==now.GetProperty("sha256").GetString(),"unchanged "+i);same++;continue;}
  var prior=System.Text.Json.Nodes.JsonNode.Parse(rows[i].GetProperty("result").GetRawText())!;
  foreach(var state in new[]{prior["State"]!,prior["Battle"]!["State"]!}){Check(state["Player"]!["Rage"]!.GetValue<bool>(),"old erroneous Rage set");state["Player"]!.AsObject().Remove("Rage");state["EventSequence"]=state["EventSequence"]!.GetValue<long>()-1;}
  var events=prior["Battle"]!["Events"]!.AsArray();int index=Enumerable.Range(0,events.Count).Single(i=>events[i]?["Volatile"]?.GetValue<string>()=="rage");events.RemoveAt(index);for(int n=index;n<events.Count;n++)events[n]!["Sequence"]=events[n]!["Sequence"]!.GetValue<long>()-1;
  Check(Hash(prior)==now.GetProperty("sha256").GetString(),"only source-blocked Rage flag/event and dependent sequences differ");delta++;
 }
 Check(same==79&&delta==1,"40 historical and 39 canonical unchanged; one exact canonical source correction");
});
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(int actor,string species,string move,uint seed=31,int? target=null){
 var mons=Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,"DEFENSE_CURL") with{SpeedStage=i}).ToArray();
 mons[actor]=Mon(species,60,move) with{SpeedStage=12,FirstTurnCounter=1};
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
  BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray(),
  Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var c=f.Slots[slot];var k=c.Member!.Value;var party=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=m}),Parties=f.Parties.SetItem((int)k.Side,party with{Members=party.Members.SetItem(k.Index,Member(m))})};}
SourceKernelResult Action(SourceBattleFrame f,int actor,int? target=null){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target??(actor^1))));Check(r.Succeeded,"real slot action "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid action state");return r;}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int actor,int? target=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{
 var m=catalog.Moves[c.Body!.Pokemon.Moves[0].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(m.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value==actor?target??(actor^1):c.Slot.Value^1));}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,"four-turn accepted: "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid four-turn state");return r;}
int actualTurns=0;var turnEvidence=new List<object>();
SourceFourActionResult First(SourceBattleFrame f,int actor,int? target=null){var begun=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,actor,target)));Check(begun.State.Queue[0].Slot.Value==actor,"actor really first");var r=Ok(SourceFourActionTurns.Advance(catalog,begun.State,begun.Continuation!.Value));turnEvidence.Add(new{input=f,first=r});return r;}
SourceFourActionResult Finish(SourceFourActionResult r){int steps=0;while(r.State.FourTurn is not null){Check(++steps<40,"bounded actual turn");var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;
 SourceFourActionResult expected,replayed;
 if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){var pending=r.State.PendingSwitches[0];var party=r.State.Party(pending.Slot.Side);int reserve=Enumerable.Range(0,party.Members.Length).First(i=>party.Members[i].Pokemon.CurrentHp>0&&!r.State.Slots.Any(c=>c.Member==new PartyMemberKey(pending.Slot.Side,i)));expected=SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,pending.Slot,new(pending.Slot.Side,reserve));replayed=SourceFourActionTurns.ChooseReplacement(catalog,copy,r.Continuation.Value,pending.Slot,new(pending.Slot.Side,reserve));}
 else{expected=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);replayed=SourceFourActionTurns.Advance(catalog,copy,r.Continuation.Value);}
 Check(Json(expected)==Json(replayed),"every continuation and replacement exact through JSON");r=Ok(expected);
 }Check(r.CompletedCleanTurn,"completed real four-slot turn");actualTurns++;return r;}
Test("all four moves use all four actual actor positions",()=>{
 foreach(var move in names)foreach(int actor in Enumerable.Range(0,4)){
  var f=Frame(actor,"CLEFAIRY",move);Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(move)]),"admitted "+move);
  var r=First(f,actor);Check(r.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(move)].Pp-1,"one owned PP");
  Check(r.Events.OfType<SourceHpChangedEvent>().Any(),"actual damage "+move);Finish(r);
 }
});
Test("Fury Cutter powers, cap, failure and interruption owners",()=>{
 var f=Frame(2,"CLEFAIRY","FURY_CUTTER");var damages=new List<int>();
 for(int i=0;i<6;i++){var r=Action(f,2);Check(r.State.Slots[2].Body!.FuryCutterCounter==Math.Min(5,i+1),"source counter capped");damages.Add(r.Events.OfType<SourceHpChangedEvent>().Single().Damage);f=r.State with{Rng=new(31)};f=Put(f,3,Mon("SNORLAX",100,"DEFENSE_CURL"));}
 Check(damages[0]<damages[1]&&damages[1]<damages[2]&&damages[2]<damages[3]&&damages[3]<damages[4]&&damages[4]==damages[5],"10/20/40/80/160/160 powers");
 foreach(string mode in new[]{"protected","miss","immune","flinch","paralysis","other-move"}){
  var state=Frame(2,"CLEFAIRY","FURY_CUTTER");state=Put(state,2,state.Slots[2].Body! with{FuryCutterCounter=4});
  if(mode=="protected")state=Put(state,3,state.Slots[3].Body! with{Protected=true});
  if(mode=="miss"){state=Put(state,2,state.Slots[2].Body! with{AccuracyStage=0});state=Put(state,3,state.Slots[3].Body! with{EvasionStage=12});}
  if(mode=="immune")state=Put(state,3,Mon("SHEDINJA",100,"HARDEN"));
  if(mode=="flinch")state=Put(state,2,state.Slots[2].Body! with{Flinched=true});
  if(mode=="paralysis"){state=Put(state,2,state.Slots[2].Body! with{Status=WildBattleStatus.Paralysis});state=state with{Rng=new(0)};}
  if(mode=="other-move")state=Put(state,2,Mon("CLEFAIRY",60,"DEFENSE_CURL") with{FuryCutterCounter=4});
  var r=Action(state,2,mode=="other-move"?2:3);Check(r.State.Slots[2].Body!.FuryCutterCounter==(mode=="other-move"?4:0),"counter "+mode);
  if(mode is "protected" or "miss" or "immune")Check(!r.Trace.Any(t=>t.Purpose.EndsWith(".damage")),"failed Fury Cutter skips variance "+mode);
 }
});
Test("Triple Kick independent accuracy, power, Substitute and KO",()=>{
 var f=Frame(2,"CLEFAIRY","TRIPLE_KICK");var r=Action(f,2);var damages=r.Events.OfType<SourceHpChangedEvent>().ToArray();
 Check(damages.Length==3&&r.Trace.Count(t=>t.Purpose.EndsWith(".accuracy"))==3,"three separately checked hits");Check(damages[0].Damage<damages[1].Damage&&damages[1].Damage<damages[2].Damage,"10/20/30 increasing power");
 f=Put(f,3,f.Slots[3].Body! with{CalledVolatile=new(){SubstituteHp=1}});var doll=Action(f,2);Check(doll.Events.OfType<SourceSubstituteDamagedEvent>().Count()==1&&doll.Events.OfType<SourceHpChangedEvent>().Count()==2,"break doll then hit actual body");
 var ko=Put(Frame(2,"CLEFAIRY","TRIPLE_KICK"),3,Hp(Mon("SNORLAX",100,"DEFENSE_CURL"),1));var stopped=Action(ko,2);Check(stopped.Events.OfType<SourceHpChangedEvent>().Count()==1&&stopped.Trace.Count(t=>t.Purpose.EndsWith(".accuracy"))==1,"target KO stops later accuracy and hits");
 var endure=Put(Frame(2,"CLEFAIRY","TRIPLE_KICK"),3,Hp(Mon("SNORLAX",100,"DEFENSE_CURL") with{Enduring=true},1));var end=Action(endure,2);Check(end.State.Slots[3].Body!.Pokemon.CurrentHp==1&&end.Trace.Count(t=>t.Purpose.EndsWith(".accuracy"))==1,"Endure terminates loop at one HP");
 bool partial=false;for(uint seed=0;seed<100&&!partial;seed++){var x=Action(Frame(2,"CLEFAIRY","TRIPLE_KICK",seed),2);var count=x.Events.OfType<SourceMultiHitEvent>().Single().ActualHits;if(count is 1 or 2){Check(!x.Events.OfType<SourceMoveMissedEvent>().Any(),"later accuracy miss suppresses MISSED text");partial=true;}}Check(partial,"observed independent later miss");
});
Test("Rage only builds on enemy actual body damage",()=>{
 foreach(string mode in new[]{"enemy","ally","substitute","zero","fainted"}){
  var f=Frame(2,"CLEFAIRY","POUND");int target=mode=="ally"?0:3;var mon=f.Slots[target].Body! with{Rage=true};
  if(mode=="substitute")mon=mon with{CalledVolatile=new(){SubstituteHp=100}};if(mode=="zero")mon=Hp(mon with{Enduring=true},1);if(mode=="fainted")mon=Hp(mon,1);f=Put(f,target,mon);
  var r=Action(f,2,target);Check(r.State.Slots[target].Body!.AttackStage==(mode=="enemy"?7:6),"Rage building "+mode);if(mode=="fainted")Check(!r.State.Slots[target].Body!.Rage,"faint clears Rage");
 }
 var chosen=Frame(2,"CLEFAIRY","RAGE");chosen=Put(chosen,3,chosen.Slots[3].Body! with{Rage=true});var first=First(chosen,2);Check(!first.State.Slots[3].Body!.Rage&&first.State.Slots[3].Body!.AttackStage==6,"other chosen move clears Rage before incoming hit");Finish(first);
});
Test("Fake Out first-turn counter, certain flinch, immunity and entry",()=>{
 var f=Frame(2,"CLEFAIRY","FAKE_OUT");f=Put(f,2,f.Slots[2].Body! with{FirstTurnCounter=2});var entry=Ok(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision));Check(entry.State.Slots[2].Body!.FirstTurnCounter==1,"entry Ready decrements original counter");
 var first=First(entry.State,2);Check(first.State.Slots[3].Body!.Flinched&&!first.Trace.Any(t=>t.Purpose.EndsWith(".secondary-effect")),"certain flinch skips secondary RNG");var end=Finish(first);Check(end.State.Slots[2].Body!.FirstTurnCounter==0,"end-turn expires first-turn flag");
 var again=First(end.State,2);Check(again.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="not-first-turn")&&!again.Events.OfType<SourceHpChangedEvent>().Any(),"second actual turn fails");Finish(again);
 foreach(var species in new[]{"CATERPIE","ZUBAT","GASTLY"}){var g=Put(Frame(2,"CLEFAIRY","FAKE_OUT"),3,Mon(species,100,"TACKLE"));var hit=First(g,2);Check(!hit.State.Slots[3].Body!.Flinched,"flinch guard "+species);Finish(hit);}
});
Test("canonical protection and paralysis use existing cancellation owners",()=>{
 foreach(string move in new[]{"RAGE","FURY_CUTTER"}){
  var a=Mon("CLEFAIRY",60,move) with{Rage=true,FuryCutterCounter=4};var b=Mon("SNORLAX",100,"DEFENSE_CURL") with{Protected=true};
  var state=new WildBattleState(a,b,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
  var r=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));Check(r.Succeeded&&r.State!.Player.FuryCutterCounter==0,"Protect cancels canonical Fury chain "+move);if(move=="RAGE")Check(!r.State!.Player.Rage,"Protect reaches Rage miss clear");
 }
 var p=Mon("CLEFAIRY",60,"FURY_CUTTER") with{FuryCutterCounter=4,Status=WildBattleStatus.Paralysis};var foe=Mon("SNORLAX",100,"DEFENSE_CURL");
 var paused=new WildBattleState(p,foe,0,0,0,new(0),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
 var blocked=WildBattleRules.ResolveSourceMoveAction(catalog,paused,new(WildBattleSide.Player,0));Check(blocked.Succeeded&&blocked.State!.Player.FuryCutterCounter==0&&blocked.State.Player.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("FURY_CUTTER")].Pp,"full paralysis clears canonical chain without PP");
});
Test("per-hit contact and held items stay inside Triple Kick loop",()=>{
 var shell=Frame(2,"CLEFAIRY","TRIPLE_KICK");shell=Put(shell,2,Hp(shell.Slots[2].Body! with{HeldItem="ITEM_SHELL_BELL"},20));var healed=Action(shell,2);
 Check(healed.Events.OfType<SourceHeldItemHealEvent>().Count()==3,"Shell Bell runs once per completed hit");
 var rock=Frame(2,"CLEFAIRY","TRIPLE_KICK");rock=Put(rock,2,rock.Slots[2].Body! with{HeldItem="ITEM_KINGS_ROCK"});var first=First(rock,2);
 Check(first.Trace.Count(t=>t.Purpose.EndsWith("kings-rock"))==first.Events.OfType<SourceMultiHitEvent>().Single().ActualHits,"no extra final King's Rock draw");Finish(first);
 var contact=Frame(2,"CLEFAIRY","TRIPLE_KICK");contact=Put(contact,2,Hp(contact.Slots[2].Body!,1));contact=Put(contact,3,Mon("CARVANHA",100,"HARDEN"));var stopped=Action(contact,2);
 Check(stopped.State.Slots[2].Body!.Pokemon.CurrentHp==0&&stopped.Events.OfType<SourceHpChangedEvent>().Count()==1&&stopped.Trace.Count(t=>t.Purpose.EndsWith(".accuracy"))==1,"Rough Skin KO prevents the second kick");
});
Test("real voluntary switch clears chain and grants one Fake Out turn",()=>{
 var f=Frame(2,"CLEFAIRY","FURY_CUTTER");f=Put(f,2,f.Slots[2].Body! with{FuryCutterCounter=4,Rage=true,FirstTurnCounter=0});var reserve=Mon("CLEFAIRY",60,"FAKE_OUT");
 f=f with{Parties=f.Parties.SetItem(0,f.Parties[0] with{Members=f.Parties[0].Members.Add(Member(reserve))}),Rewards=f.Rewards! with{Metadata=f.Rewards!.Metadata.Add(new(new(BattleSide.Player,2),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected))}};
 var initial=Ok(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision));var options=Choices(initial.State,2).Select(x=>x.Actor.Slot.Value==2?new SourceActionSelection(x.Actor,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2)):x).ToImmutableArray();
 var begin=Ok(SourceFourActionTurns.BeginActions(catalog,initial.State,initial.State.Revision,options));var switched=Ok(SourceFourActionTurns.Advance(catalog,begin.State,begin.Continuation!.Value));
 Check(switched.State.Slots[2].Member!.Value.Index==2&&switched.State.Slots[2].Body!.FirstTurnCounter==2&&!switched.State.Slots[2].Body!.Rage&&switched.State.Slots[2].Body!.FuryCutterCounter==0,"actual new occupant owns fresh counters");var done=Finish(switched);
 Check(done.State.Slots[2].Body!.FirstTurnCounter==1,"existing turn boundary decrements switched entrant once");var fake=First(done.State,2);Check(fake.Events.OfType<SourceHpChangedEvent>().Any()&&fake.State.Slots[3].Body!.Flinched,"reserve Fake Out succeeds next turn");var ended=Finish(fake);var fails=First(ended.State,2);Check(fails.Events.OfType<SourceMoveFailedEvent>().Any(x=>x.Reason=="not-first-turn"),"reserve Fake Out then expires");Finish(fails);
 var again=SourceFourEntryAbilities.InitializeEntry(catalog,initial.State,initial.State.Revision);Check(!again.Succeeded&&ReferenceEquals(again.State,initial.State)&&again.Events.IsEmpty&&again.Trace.IsEmpty,"cannot initialize entry twice");
});
Test("canonical full singles turns use shared recipes",()=>{
 foreach(string move in names){var a=Mon("CLEFAIRY",60,move);var b=Mon("MAGIKARP",100,"SPLASH");var started=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[a],0,b,new(31),WildBattleTerrain.Plain);Check(started.Succeeded,"source entry "+started.Fault);var turn=WildBattleRules.ResolveMove(catalog,started.State!,0);Check(turn.Succeeded,"full singles turn "+move+" "+turn.Fault);Check(turn.State!.Turn==1,"singles Ready reached");}
});
Test("invalid and stale actions remain atomic with complete recipe admission",()=>{
 var f=Frame(2,"CLEFAIRY","FURY_CUTTER");foreach(var bad in new[]{f with{Revision=1},Put(f,2,f.Slots[2].Body! with{FuryCutterCounter=6})}){var r=SourceBattleKernel.ResolveAction(catalog,bad,new(f.Slots[2].Occupant!.Value,0,0,new(3)));Check(!r.Succeeded&&ReferenceEquals(r.State,bad)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"invalid source action atomic");}
 Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id("PRESENT")]),"authenticated Present recipe admitted");
});
File.WriteAllText(args[1]+".turns.json",Json(turnEvidence));File.WriteAllText(args[1],Json(new{runtime=Environment.Version.ToString(),groups,checks,failures,actualTurns,baselineResults=snapshots.Count,recipeAdmission=catalog.Moves.Values.Count(SourceBattleKernel.SupportsRecipe),results}));Console.WriteLine($"RESULT groups={groups} checks={checks} failures={failures} turns={actualTurns}");return failures==0?0:1;
