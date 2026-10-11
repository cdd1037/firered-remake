using System.Collections.Immutable;
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
string Hash<T>(T x)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(x)))).ToLowerInvariant();
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 foreach(string name in moves)if(!catalog.Species[p.Species].LevelUpMoves.Any(m=>m.Move==Id(name)&&m.Level<=level)&&!(name=="SECRET_POWER"&&species is "CLEFAIRY" or "TOGETIC" or "DUNSPARCE"))throw new Exception("unlearned fixture "+species+"/"+name);
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
var snapshots=new List<object>();var singlesRuns=new List<WildBattleCalledMoveResult>();
var original=PretRocketHideoutPokemon.Load(args[0]).Catalog;
var supplement=PretVermilionGymTrainers.ExtendPokemon(args[0],PretCeladonApproachPokemon.Load(args[0]).Catalog,celadonApproachRules:true);
var prior=original with{Moves=original.Moves.SetItems(supplement.Moves)};
var old=PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior,args[0],args[0]).Catalog,args[0]).Catalog,args[0]).Catalog;
var legacy=PretMetronomeMoveCatalog.Load(args[0]).Extend(PretFuchsiaRoutePokemon.Load(old,args[0]).Catalog);
foreach(bool canonical in new[]{false,true})foreach(string move in new[]{"PRESENT","SECRET_POWER","TEETER_DANCE"})foreach(uint seed in new uint[]{0,1,31,99,137})foreach(int scenario in Enumerable.Range(0,10))foreach(bool reverse in new[]{false,true}){
 var actor=Mon("CLEFAIRY",50,"METRONOME");var target=Mon(scenario==3?"GASTLY":scenario==8?"CATERPIE":"MAGIKARP",100,scenario==3?"NIGHT_SHADE":scenario==8?"STRING_SHOT":"SPLASH");
 if(scenario!=0)target=Hp(target,Math.Max(1,target.Pokemon.Stats.MaxHp/2));
 if(scenario==4)target=target with{Protected=true};
 if(scenario is 5 or 6)target=target with{CalledVolatile=new(){SubstituteHp=scenario==5?1:100}};
 if(scenario==7)target=target with{ConfusionTurnsRemaining=3};
 var state=new WildBattleState(reverse?target:actor,reverse?actor:target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=scenario==2?WildBattleTerrain.LongGrass:scenario==8?WildBattleTerrain.Sand:scenario==9?WildBattleTerrain.Mountain:WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null};
 if(scenario==6)state=state with{Terrain=WildBattleTerrain.LongGrass};
 var result=WildBattleRules.ResolveCalledMoveAction(canonical?catalog:legacy,state,new(reverse?WildBattleSide.Opponent:WildBattleSide.Player,0,Id(move)));
 if(!result.Succeeded)throw new Exception($"baseline {canonical}/{move}/{scenario}: "+result.Fault);
 singlesRuns.Add(result);snapshots.Add(new{canonical,move,seed,scenario,reverse,sha256=Hash(result),bytes=Encoding.UTF8.GetByteCount(Json(result)),draws=result.Battle.Trace.Length,rng=result.State!.Rng});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("Refuse to replace immutable baseline");
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasMiscTargetRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unchanged aaec150 source");
 File.WriteAllText(args[1],Json(new{baseCommit="aaec1508447dd1116b6107deaa3adee2edd454c0",snapshots}));Console.WriteLine($"Captured {snapshots.Count} immutable singles results");return 0;
}
int checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
var differences=new List<object>();
Test("immutable pre-edit canonical and historical singles results",()=>{
 Check(Environment.Version.Major==10,"actual .NET10 runtime");
 using var b=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline-aaec150.json")));
 var rows=b.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(rows.Length==600&&rows.Length==snapshots.Count,"complete frozen corpus");
 for(int i=0;i<rows.Length;i++){
  var now=JsonSerializer.SerializeToElement(snapshots[i]);bool same=rows[i].GetProperty("sha256").GetString()==now.GetProperty("sha256").GetString();
  if(!same)differences.Add(new{before=rows[i].Clone(),after=now});
  if(!same){
   Check(now.GetProperty("canonical").GetBoolean(),"historical singles changed "+now);
   if(now.GetProperty("move").GetString()=="TEETER_DANCE"){Check(singlesRuns[i].State!.Player.LastLandedMove==Id("TEETER_DANCE")||singlesRuns[i].State!.Opponent.LastLandedMove==Id("TEETER_DANCE"),"numeric user pass records source history "+now);Check(rows[i].GetProperty("draws").GetInt32()==singlesRuns[i].Battle.Trace.Length,"Teeter flag fix preserves RNG");continue;}
   Check(now.GetProperty("move").GetString()=="SECRET_POWER"&&now.GetProperty("scenario").GetInt32()==6,"unexpected singles change "+now);
   Check(rows[i].GetProperty("draws").GetInt32()==singlesRuns[i].Battle.Trace.Length+1,"only blocked sleep duration disappears");
   var target=now.GetProperty("reverse").GetBoolean()?singlesRuns[i].State!.Player:singlesRuns[i].State!.Opponent;
   Check(target.Status==0&&!singlesRuns[i].Battle.Trace.Any(t=>t.Purpose.EndsWith("sleep-duration")),"source Substitute gate before sleep RNG");
  }
 }
 Check(differences.Count==103,"only the 100 numeric Teeter history changes and three Substitute sleep changes");
});
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant actor,WildBattleCombatant target,uint seed=31,int actorSlot=2,int targetSlot=3){
 var mons=Enumerable.Range(0,4).Select(_=>Mon("MAGIKARP",100,"SPLASH")).ToArray();mons[actorSlot]=actor;mons[targetSlot]=target;
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Field=new(){Terrain=WildBattleTerrain.Plain},
  Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray(),
  Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var cell=f.Slots[slot];var key=cell.Member!.Value;var party=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(slot,cell with{Body=m}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(m))})};}
SourceKernelResult Action(SourceBattleFrame f,int actor=2,int target=3,int slot=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,slot,new(target)));Check(r.Succeeded,r.Fault??"kernel");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid result identity");return r;}
uint PresentSeed(int roll){for(uint seed=0;seed<200000;seed++){uint first=unchecked(seed*1103515245u+24691u),second=unchecked(first*1103515245u+24691u);if((first>>16)%100<90&&((second>>16)&255)==roll)return seed;}throw new Exception("present seed unavailable");}
uint SeedFor(int draw,int modulus,int desired){for(uint seed=0;seed<200000;seed++){uint s=seed;for(int i=0;i<draw;i++)s=unchecked(s*1103515245u+24691u);if((s>>16)%modulus==desired)return seed;}throw new Exception("seed unavailable");}
WildBattleCombatant Serene(){var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_TOGEPI"),100,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(1)),new(0)).Pokemon;return new(p with{Moves=[new(Id("SECRET_POWER"),20,20)]});}
ImmutableArray<SourceActionSelection> Picks(SourceBattleFrame f)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?c.Slot:c.Slot.Opposite)).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"four-turn");return r;}
(SourceFourActionResult Result,List<SourceBattleEvent> Events,List<RandomTrace> Trace) Turn(SourceBattleFrame f){
 var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Picks(f)));var events=r.Events.ToList();var trace=r.Trace.ToList();int n=0;
 while(!r.CompletedCleanTurn){Check(++n<30,"finite full turn");var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var next=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var restored=SourceFourActionTurns.Advance(catalog,copy,r.Continuation.Value);Check(Json(next)==Json(restored),"exact persisted continuation");r=Ok(next);events.AddRange(r.Events);trace.AddRange(r.Trace);}
 Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid terminal state");return(r,events,trace);
}
using var oracle=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));
var vectors=oracle.RootElement.GetProperty("vectors").EnumerateArray().ToArray();
Test("bounded Present branches share original C thresholds and selected slot costs",()=>{
 var user=Mon("DELIBIRD",50,"PRESENT");var target=Hp(Mon("MAGIKARP",100,"SPLASH"),150);
 foreach(int roll in new[]{0,101,102,177,178,203,204,255})foreach(int actor in Enumerable.Range(0,4))foreach(int targetSlot in Enumerable.Range(0,4).Where(t=>t!=actor)){
  uint seed=PresentSeed(roll);var f=Frame(user,target,seed,actor,targetSlot);var r=Action(f,actor,targetSlot);
  var selection=r.Trace.SingleOrDefault(t=>t.Purpose.EndsWith(".present"));if(selection is null){Check(r.Events.OfType<SourceMoveMissedEvent>().Any(),"only original 90% miss bypasses Present command");continue;}
  var source=vectors.Single(v=>v.GetProperty("kind").GetString()=="present"&&v.GetProperty("max").GetInt32()==100&&v.GetProperty("full").GetInt32()==0&&v.GetProperty("inputFlags").GetInt32()==0&&v.GetProperty("roll").GetInt32()==roll);
  var pp=r.Events.OfType<SourcePpCommandEvent>().Single();Check(pp.Actor.Value==actor&&pp.Target.Value==targetSlot&&pp.Before==15&&pp.After==14&&pp.RngCursor==1,"actual position and one PP after accuracy");
  Check(r.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(roll<204?new[]{"accuracy","present","critical","damage","secondary-effect"}:new[]{"accuracy","present"}),"source RNG sequence");
  if(roll>=204){var heal=r.Events.OfType<SourceRecoveredEvent>().Single();Check(source.GetProperty("branch").GetInt32()==3&&heal.Side.Value==targetSlot&&heal.Recovered==Math.Min(target.Pokemon.Stats.MaxHp-target.Pokemon.CurrentHp,target.Pokemon.Stats.MaxHp/4),"source quarter HP capped heal");}
  else{
   var hp=r.Events.OfType<SourceHpChangedEvent>().Single();int power=source.GetProperty("power").GetInt32();int expected=user.Pokemon.Stats.Attack*power*(2*user.Pokemon.Level/5+2)/target.Pokemon.Stats.Defense/50+2;if(hp.Critical)expected*=2;expected=Math.Max(1,expected*hp.RandomPercent/100);Check(hp.Damage==expected,"independent source power and physical damage");
  }
 }
});
Test("Present healing ignores Substitute and Ghost, retains Wonder Guard and maxHP failure",()=>{
 uint seed=PresentSeed(255);
 foreach(string species in new[]{"MAGIKARP","GASTLY","SHEDINJA"})foreach(bool full in new[]{false,true}){
  var target=Mon(species,100,species=="MAGIKARP"?"SPLASH":species=="GASTLY"?"NIGHT_SHADE":"HARDEN");if(!full)target=Hp(target,species=="SHEDINJA"?1:5);target=target with{CalledVolatile=new(){SubstituteHp=1}};
  var f=Frame(Mon("DELIBIRD",50,"PRESENT"),target,seed);var r=Action(f);
  Check(r.State.Slots[3].Body!.CalledVolatile?.SubstituteHp==1,"heal bypasses substitute");Check(r.Trace.Length==2,"healing no critical damage secondary RNG");
  if(full||species=="SHEDINJA")Check(r.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="already-full-hp","full maxHP branch");
  else Check(r.State.Slots[3].Body!.Pokemon.CurrentHp>5&&!r.Events.OfType<SourceEffectivenessEvent>().Any(),"Ghost healing clears immune flag");
  if(full&&species=="GASTLY")Check(r.State.Slots[3].Body!.LastLandedMove?.Value=="pret:MOVE_UNAVAILABLE","full-HP branch preserves ordinary immunity result flag");
 }
 var p=Frame(Mon("DELIBIRD",50,"PRESENT"),Mon("DUSCLOPS",100,"NIGHT_SHADE") with{Protected=true},seed);var blocked=Action(p);Check(blocked.Trace.IsEmpty&&blocked.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==2,"Protect and Pressure before branch RNG");
});
Test("Secret Power terrain table and shared secondary RNG match the original commands",()=>{
 foreach(var terrain in Enum.GetValues<WildBattleTerrain>().Where(t=>t!=WildBattleTerrain.Cave)){
  var f=Frame(Mon("CLEFAIRY",50,"SECRET_POWER"),Mon("MAGIKARP",100,"SPLASH"),SeedFor(4,100,0));f=f with{Field=f.Field with{Terrain=terrain}};var r=Action(f);var b=r.State.Slots[3].Body!;
  int effect=vectors.Single(v=>v.GetProperty("kind").GetString()=="secret"&&v.GetProperty("terrain").GetInt32()==(int)terrain).GetProperty("effect").GetInt32();
  Check(effect switch{2=>b.Status==WildBattleStatus.Poison,1=>WildBattleStatus.IsSleeping(b.Status),27=>b.AccuracyStage==5,23=>b.DefenseStage==5,22=>b.AttackStage==5,24=>b.SpeedStage==5,7=>b.ConfusionTurnsRemaining>0,5=>b.Status==WildBattleStatus.Paralysis,_=>false},"exact terrain effect byte "+terrain+"/"+effect);
  Check(r.Trace.Count(t=>t.Purpose.EndsWith("secondary-effect"))==1,"one shared secondary roll");
 }
 foreach(bool serene in new[]{false,true})foreach(int roll in new[]{0,30,31,60,61,99}){
  var actor=serene?Serene():Mon("CLEFAIRY",50,"SECRET_POWER");var f=Frame(actor,Mon("MAGIKARP",100,"SPLASH"),SeedFor(4,100,roll));var r=Action(f);
  int expected=vectors.Single(v=>v.GetProperty("kind").GetString()=="chance"&&v.GetProperty("serene").GetInt32()==(serene?1:0)&&v.GetProperty("noEffect").GetInt32()==0&&v.GetProperty("roll").GetInt32()==roll).GetProperty("apply").GetInt32();Check((r.State.Slots[3].Body!.Status!=0)==(expected==1),"source inclusive chance + Serene Grace "+serene+"/"+roll);
 }
});
Test("Secret Power Substitute Shield Dust immunity safeguards and source sleep guards",()=>{
 foreach(var terrain in new[]{WildBattleTerrain.Grass,WildBattleTerrain.LongGrass,WildBattleTerrain.Sand,WildBattleTerrain.Mountain,WildBattleTerrain.Plain})foreach(int mode in Enumerable.Range(0,5)){
  var target=Mon(mode==2?"CATERPIE":mode==3?"GASTLY":"MAGIKARP",100,mode==2?"STRING_SHOT":mode==3?"NIGHT_SHADE":"SPLASH");if(mode<=1)target=target with{CalledVolatile=new(){SubstituteHp=mode==0?1:100}};
  var f=Frame(Mon("CLEFAIRY",50,"SECRET_POWER"),target,SeedFor(4,100,0));f=f with{Field=f.Field with{Terrain=terrain}};if(mode==4)f=f with{Sides=f.Sides.SetItem(1,f.Sides[1] with{SafeguardTurnsRemaining=4})};var r=Action(f);var body=r.State.Slots[3].Body!;
  Check(body.Status==0&&body.ConfusionTurnsRemaining==0,"source gate keeps status "+terrain+"/"+mode);if(mode!=4)Check(body.AccuracyStage==6,"source stat gate");
  Check(r.Trace.Count(t=>t.Purpose.EndsWith("secondary-effect"))==1&&!r.Trace.Any(t=>t.Purpose.EndsWith("sleep-duration")),"chance retained and blocked sleep consumes no duration");
 }
});
Test("Teeter Dance numeric loop uses original source checks including dead and source-absent bodies",()=>{
 foreach(int actor in Enumerable.Range(0,4))foreach(int mode in Enumerable.Range(0,32)){
  var f=Frame(Mon("SPINDA",100,"TEETER_DANCE"),Mon("MAGIKARP",100,"SPLASH"),31,actor,actor^1);
  for(int i=0;i<4;i++)if(i!=actor){var b=i==0&&(mode&1)!=0?Mon("SPINDA",100,"TEETER_DANCE"):Mon("MAGIKARP",100,"SPLASH");if((mode&16)!=0)b=Hp(b,0);else if(i==1&&(mode&2)!=0)b=b with{CalledVolatile=new(){SubstituteHp=1}};else if(i==2&&(mode&4)!=0)b=b with{ConfusionTurnsRemaining=2};if(i==3&&(mode&4)!=0&&b.Pokemon.CurrentHp>0)b=b with{Protected=true};f=Put(f,i,b);}
  if((mode&8)!=0)f=f with{Sides=f.Sides.Select(s=>s with{SafeguardTurnsRemaining=4}).ToImmutableArray()};
  // A real selected target must still be alive; a retained dead body in the other
  // positions is enough to test source numeric traversal without synthetic singles.
  if((mode&16)!=0){int live=actor^1;f=Put(f,live,Mon("MAGIKARP",100,"SPLASH"));}
  var r=Action(f,actor,actor^1);var source=vectors.Single(v=>v.GetProperty("kind").GetString()=="teeter"&&v.GetProperty("actor").GetInt32()==actor&&v.GetProperty("mode").GetInt32()==mode);
  Check(r.Events.OfType<SourcePpCommandEvent>().Count()==1,"one loop PP command");Check(r.State.Slots[actor].Body!.ConfusionTurnsRemaining==0,"user effect excluded");
  var confused=r.Events.OfType<SourceConfusionChangedEvent>().Select(e=>e.Side.Value).ToArray();Check(confused.SequenceEqual(confused.Order()),"numeric source sequence");
  if((mode&16)==0){int mask=confused.Aggregate(0,(v,i)=>v|(1<<i));Check(mask==source.GetProperty("confused").GetInt32(),"original command confusion mask "+actor+"/"+mode);Check(r.Trace.Count(t=>t.Purpose.EndsWith("accuracy"))==System.Numerics.BitOperations.PopCount((uint)source.GetProperty("accuracy").GetInt32()),"accuracy only after own-tempo/substitute/existing gates");}
 }
 var absent=Frame(Mon("SPINDA",100,"TEETER_DANCE"),Mon("MAGIKARP",100,"SPLASH"));absent=absent with{SourceAbsentMask=1,TurnStartAbsentMask=1};var a=Action(absent);Check(a.State.Slots[0].Body!.ConfusionTurnsRemaining>0,"Teeter does not use damaging spread absence filter");
});
Test("real four-action turns retain flinch order, move end, Pressure PP and continuation",()=>{
 foreach(string move in new[]{"PRESENT","SECRET_POWER","TEETER_DANCE"})foreach(int actor in Enumerable.Range(0,4)){
  var user=Mon(move=="PRESENT"?"DELIBIRD":move=="TEETER_DANCE"?"SPINDA":"CLEFAIRY",100,move);var f=Frame(user,Mon("DUSCLOPS",100,"LEER"),31,actor,actor^1);if(move=="SECRET_POWER")f=f with{Field=f.Field with{Terrain=WildBattleTerrain.Cave}};var done=Turn(f);
  Check(done.Events.OfType<SourcePpCommandEvent>().Any(e=>e.Actor.Value==actor&&e.CurrentMove==Id(move)),"real move reached");Check(done.Result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp<=user.Pokemon.Moves[0].CurrentPp-2,"Pressure charged actual opponent");
  Check(done.Result.State.Slots.All(c=>!c.Body!.Flinched),"turn cleanup clears flinch");
 }
});
Test("source Teeter direct-string labels preserve result flags and move-end history",()=>{
 foreach(int mode in Enumerable.Range(0,3)){
  var f=Frame(Mon("SPINDA",100,"TEETER_DANCE"),Mon(mode==2?"SPINDA":"MAGIKARP",100,mode==2?"TEETER_DANCE":"SPLASH"));
  if(mode==0)f=Put(f,3,f.Slots[3].Body! with{CalledVolatile=new(){SubstituteHp=1}});
  if(mode==1)f=f with{Sides=f.Sides.SetItem(1,f.Sides[1] with{SafeguardTurnsRemaining=4})};
  if(mode==2)f=Put(f,3,f.Slots[3].Body! with{Protected=true});
  var r=Action(f);Check(r.State.Slots[3].Body!.ConfusionTurnsRemaining==0,"prevented confusion");Check(r.State.Slots[3].Body!.LastLandedMove==Id("TEETER_DANCE"),"direct print does not set FAILED/MISSED");
  if(mode==2)Check(r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Target.Value==3)&&!r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Target.Value==3),"Own Tempo precedes Protect after movevaluescleanup");
 }
 var state=new WildBattleState(Mon("SPINDA",100,"TEETER_DANCE"),Mon("MAGIKARP",100,"SPLASH"),0,0,0,new(0),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,CalledField=new(){OpponentSafeguardTurnsRemaining=4}};
 var single=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));Check(single.Succeeded,single.Fault??"single safeguard");Check(single.State!.Player.LastUsedMove==Id("TEETER_DANCE")&&single.State.Opponent.LastLandedMove==Id("TEETER_DANCE"),"canonical safeguard rejoins move-end");
});
Test("Present heal preserves Wonder Guard MISSED after genuine four-slot Trace",()=>{
 var f=Frame(Mon("DELIBIRD",50,"PRESENT"),Mon("PORYGON",100,"TACKLE"),31,1,3);f=Put(Put(f,0,Mon("SHEDINJA",100,"HARDEN")),2,Mon("SHEDINJA",100,"HARDEN"));
 var entry=SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision);Check(entry.Succeeded,entry.Fault??"real Trace entry");f=entry.State;Check(WildBattleEffective.Ability(f.Slots[3].Body!)=="ABILITY_WONDER_GUARD","Trace supplies legitimate copied ability");f=Put(f,3,Hp(f.Slots[3].Body!,10));f=f with{Rng=new(PresentSeed(255))};var r=Action(f,1,3);
 Check(r.State.Slots[3].Body!.Pokemon.CurrentHp==10&&r.Events.OfType<SourceRecoveredEvent>().Single().Recovered==0,"healing message retains zero HP gain under MISSED");Check(r.State.Slots[3].Registers.History!.LastHitByType=="TYPE_NORMAL","original typecalc resets hit type");
});
Test("Secret Power actual Uproar guard and critical zero-loss command",()=>{
 var state=new WildBattleState(Mon("CLEFAIRY",50,"SECRET_POWER"),Mon("SPINDA",50,"UPROAR"),0,0,0,new(0),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.LongGrass,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
 var started=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Opponent,0));Check(started.Succeeded,started.Fault??"Uproar action");Check(started.State!.Opponent.CalledTurnState?.Lock?.Kind==WildBattleCalledLockKind.Uproar,"genuine Uproar retained");
 var r=WildBattleRules.ResolveSourceMoveAction(catalog,started.State! with{Rng=new(SeedFor(4,100,0))},new(WildBattleSide.Player,0));Check(r.Succeeded,r.Fault??"Secret during Uproar");Check(r.State!.Opponent.Status==0&&!r.Battle.Trace.Any(t=>t.Purpose.EndsWith("sleep-duration")),"Uproar prevents duration RNG");
 var f=Frame(Mon("CLEFAIRY",100,"SECRET_POWER"),Hp(Mon("MAGIKARP",100,"SPLASH"),1) with{Enduring=true},SeedFor(2,16,0));var hit=Action(f);Check(hit.State.Slots[3].Body!.Pokemon.CurrentHp==1&&hit.Events.OfType<SourceCriticalHitEvent>().Count()==1,"critical print survives effective zero loss");
});
Test("singles numeric self-pass shares the move-end owner once per source pass",()=>{
 foreach(bool reverse in new[]{false,true}){
  var actor=Mon("SPINDA",100,"TEETER_DANCE");var target=Mon("MAGIKARP",100,"SPLASH") with{HeldItem="ITEM_PERSIM_BERRY"};
  var state=new WildBattleState(reverse?target:actor,reverse?actor:target,0,0,0,new(0),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
  var r=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(reverse?WildBattleSide.Opponent:WildBattleSide.Player,0));Check(r.Succeeded,r.Fault??"numeric singles");
  Check(r.State!.Player.LastLandedMove==Id("TEETER_DANCE")&&r.State.Opponent.LastLandedMove==Id("TEETER_DANCE"),"both numeric targets, including skipped user, reach source history");
  var cured=reverse?r.State.Player:r.State.Opponent;Check(cured.ConfusionTurnsRemaining==0&&cured.HeldItem=="ITEM_NONE","per-target berry cures confusion");
  Check(r.Battle.Events.OfType<WildBattleConfusionChangedEvent>().Count()==2,"one confusion application and one berry cure");Check(r.Battle.Events.OfType<WildBattlePpCommandEvent>().Count()==1,"single action PP despite two move-end passes");
  var next=WildBattleRules.ResolveSourceMoveAction(catalog,r.State,new(reverse?WildBattleSide.Player:WildBattleSide.Opponent,0));Check(next.Succeeded,next.Fault??"next real invocation");Check((reverse?next.State!.Player:next.State!.Opponent).LastUsedMove==Id("SPLASH"),"per-target completion does not skip next action history");
 }
 var entry=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("SPINDA",100,"TEETER_DANCE")],0,Mon("SNORLAX",100,"AMNESIA"),new(31));Check(entry.Succeeded,entry.Fault??"source singles entry");var turn=WildBattleRules.ResolveMove(catalog,entry.State!,0);Check(turn.Succeeded&&turn.State!.Turn==1,"complete canonical singles turn");Check(turn.State!.Player.LastLandedMove==Id("TEETER_DANCE"),"turn owner retained source user pass");
});
Test("unsupported and stale input rejection is atomic",()=>{
 var f=Frame(Mon("CLEFAIRY",50,"SECRET_POWER"),Mon("MAGIKARP",100,"SPLASH"));
 foreach(var bad in new[]{f with{Field=new()},f with{Field=new(){Terrain=(WildBattleTerrain)99}},f with{Field=new(){Terrain=WildBattleTerrain.Cave}}}){var r=SourceBattleKernel.ResolveAction(catalog,bad,new(bad.Slots[2].Occupant!.Value,bad.Revision,0,new(3)));Check(!r.Succeeded&&ReferenceEquals(r.State,bad)&&r.Trace.IsEmpty&&r.Events.IsEmpty,"atomic terrain/order boundary");}
 var stale=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[2].Occupant!.Value,f.Revision+1,0,new(3)));Check(!stale.Succeeded&&ReferenceEquals(stale.State,f)&&stale.Trace.IsEmpty&&stale.Events.IsEmpty,"atomic revision");
 foreach(string move in new[]{"PRESENT","SECRET_POWER","TEETER_DANCE"}){Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(move)]),"admitted exact metadata");Check(!SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(move)] with{Accuracy=1}),"forged metadata rejected");}
});
var report=new{passed=failures==0,checks,results,differences,runtime=Environment.Version.ToString()};File.WriteAllText(args[1],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(Json(report));return failures==0?0:1;
