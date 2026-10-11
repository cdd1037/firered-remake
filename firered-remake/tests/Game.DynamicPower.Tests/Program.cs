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
if(args.Contains("--capture-accuracy")||args.Contains("--capture-hustle")){
 if(File.Exists(args[1]))throw new Exception("Refuse to replace immutable baseline");
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasDynamicPowerRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unchanged 88543cb");
 File.WriteAllText(args[1],JsonSerializer.Serialize(new{baseCommit="88543cb9ee09fd1a56a5567bc1aaa7bc0aeea900",snapshots=AccuracyParity.Capture(proof,args.Contains("--capture-hustle"))}));return 0;
}
string Json<T>(T x)=>JsonSerializer.Serialize(x);
string Hash<T>(T x)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(x)))).ToLowerInvariant();
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 foreach(string move in moves)if(!(species=="CLEFAIRY"&&move is "RETURN" or "FRUSTRATION")&&!catalog.Species[p.Species].LevelUpMoves.Any(m=>m.Move==Id(move)&&m.Level<=level))throw new Exception("unlearned fixture "+species+"/"+move);
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
var moves=new[]{"RETURN","FRUSTRATION","FLAIL","REVERSAL","ERUPTION","WATER_SPOUT","LOW_KICK"};
var snapshots=new List<object>();var singlesRuns=new List<WildBattleCalledMoveResult>();
var original= PretRocketHideoutPokemon.Load(args[0]).Catalog;
var supplement= PretVermilionGymTrainers.ExtendPokemon(args[0],PretCeladonApproachPokemon.Load(args[0]).Catalog,celadonApproachRules:true);
var prior=original with{Moves=original.Moves.SetItems(supplement.Moves)};
var old= PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior,args[0],args[0]).Catalog,args[0]).Catalog,args[0]).Catalog;
var legacy=PretMetronomeMoveCatalog.Load(args[0]).Extend(PretFuchsiaRoutePokemon.Load(old,args[0]).Catalog);
foreach(bool canonical in args.Contains("--accuracy-only")?Array.Empty<bool>():new[]{false,true})foreach(string move in moves)foreach(uint seed in new uint[]{0,31,69})foreach(int scenario in Enumerable.Range(0,6))foreach(bool reverse in new[]{false,true}){
 var actor=Mon("CLEFAIRY",80,"METRONOME");var target=Mon(scenario==3?"GASTLY":"MAGIKARP",100,scenario==3?"NIGHT_SHADE":"SPLASH");
 actor=actor with{Pokemon=actor.Pokemon with{Friendship=scenario==0?0:scenario==1?255:70}};
 if(scenario==2)actor=Hp(actor,1);if(scenario==4)target=target with{Protected=true};if(scenario==5)target=target with{HeldItem="ITEM_FOCUS_BAND"};
 if(!canonical&&scenario==5)target=target with{HeldItem="ITEM_NONE",Enduring=true};
 var state=new WildBattleState(reverse?target:actor,reverse?actor:target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null};
 var result=WildBattleRules.ResolveCalledMoveAction(canonical?catalog:legacy,state,new(reverse?WildBattleSide.Opponent:WildBattleSide.Player,0,Id(move)));
 if(!result.Succeeded)throw new Exception($"baseline {canonical}/{move}/{scenario}: "+result.Fault);
 singlesRuns.Add(result);snapshots.Add(new{canonical,move,seed,scenario,reverse,sha256=Hash(result),bytes=Encoding.UTF8.GetByteCount(Json(result)),draws=result.Battle.Trace.Length,rng=result.State!.Rng});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("Refuse to replace immutable baseline");
 var family=typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasDynamicPowerRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
 if(family is not null)throw new Exception("Capture requires unchanged 88543cb source.");
 File.WriteAllText(args[1],Json(new{baseCommit="88543cb9ee09fd1a56a5567bc1aaa7bc0aeea900",snapshots}));Console.WriteLine($"Captured {snapshots.Count} immutable singles results");return 0;
}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
var differences=new List<object>();
Test("frozen 88543cb singles preserve historical profiles and identify source changes",()=>{
 if(args.Contains("--accuracy-only"))return;
 Check(Environment.Version.Major==10,"actual .NET10 runtime");
 using var b=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline-88543cb.json")));
 var rows=b.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(rows.Length==504&&rows.Length==snapshots.Count,"complete frozen corpus");
 for(int i=0;i<rows.Length;i++){
  var now=JsonSerializer.SerializeToElement(snapshots[i]);bool same=rows[i].GetProperty("sha256").GetString()==now.GetProperty("sha256").GetString();
  if(!same)differences.Add(new{before=rows[i].Clone(),after=now});
  bool expected=now.GetProperty("canonical").GetBoolean()&&now.GetProperty("move").GetString()=="LOW_KICK"&&now.GetProperty("scenario").GetInt32()!=4;
  Check(same!=expected,"only canonical Low Kick PP timing differs: "+now);
  if(expected){var trace=singlesRuns[i].Battle.Trace;var pp=singlesRuns[i].Battle.Events.OfType<WildBattlePpCommandEvent>().Single();Check(pp.RngCursor==(ulong)trace.TakeWhile(t=>!t.Purpose.EndsWith(".accuracy")).Count(),"Low Kick PP precedes accuracy "+Json(new{pp,trace}));Check(rows[i].GetProperty("draws").GetInt32()==trace.Length,"PP timing does not add RNG");}
 }
});
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant actor,WildBattleCombatant target,uint seed=31,int actorSlot=2,int targetSlot=3){
 var mons=Enumerable.Range(0,4).Select(_=>Mon("JIGGLYPUFF",80,"DEFENSE_CURL")).ToArray();mons[actorSlot]=actor;mons[targetSlot]=target;
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),
  Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray(),
  Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var cell=f.Slots[slot];var key=cell.Member!.Value;var party=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(slot,cell with{Body=m}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(m))})};}
SourceKernelResult Action(SourceBattleFrame f,int actor=2,int target=3,int slot=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,slot,new(target)));Check(r.Succeeded,r.Fault??"kernel");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid result identity");return r;}
var donors=new[]{("CLEFAIRY","RETURN"),("CLEFAIRY","FRUSTRATION"),("MAGIKARP","FLAIL"),("HERACROSS","REVERSAL"),("CAMERUPT","ERUPTION"),("WAILMER","WATER_SPOUT"),("MANKEY","LOW_KICK")};
Test("all seven moves use exact definitions, actual four slots, selected PP and source command RNG",()=>{
 foreach(var (species,move) in donors)foreach(int actor in Enumerable.Range(0,4))foreach(int target in Enumerable.Range(0,4).Where(t=>t!=actor&&(move is not("ERUPTION" or "WATER_SPOUT")||(t&1)!=(actor&1)))){
  var user=Mon(species,80,species=="MAGIKARP"?"TACKLE":species is "MANKEY" or "HERACROSS"?"LEER":"GROWL",move);var f=Frame(user,Mon("JIGGLYPUFF",100,"DEFENSE_CURL"),31,actor,target);var r=Action(f,actor,target,1);
  Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(move)]),"exact metadata supported");
  var hits=r.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Move==Id(move)).ToArray();bool spread=move is "ERUPTION" or "WATER_SPOUT";
  Check(hits.Length==(spread?2:1)&&hits[0].Actor.Value==actor&&hits[0].Target.Value==target,"actual chosen positions");
  Check(r.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==f.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp,"unchosen PP unchanged");
  var pp=r.Events.OfType<SourcePpCommandEvent>().ToArray();Check(pp.Length==(spread?2:1)&&pp[0].After==catalog.Moves[Id(move)].Pp-1,"one real PP cost across passes");
  Check(pp[0].RngCursor==(move=="LOW_KICK"?0UL:1UL),"PP command position");
  Check(r.Trace.Where(t=>!t.Purpose.EndsWith(".cute-charm")).Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(Enumerable.Range(0,spread?2:1).SelectMany(_=>new[]{"accuracy","critical","damage","secondary-effect"})),"exact standard command order "+move+" "+Json(r.Trace));
  uint seed=f.Rng.Seed;foreach(var draw in r.Trace){seed=unchecked(seed*1103515245u+24691u);Check(draw.Raw==(ushort)(seed>>16),"source LCG raw");}Check(r.State.Rng.Seed==seed&&r.State.Rng.Cursor==(ulong)r.Trace.Length,"terminal RNG");
 }
});
Test("protection, immunity, absorption, Pressure and Endure keep distinct script paths",()=>{
 foreach(var (species,move) in donors){
  var f=Frame(Mon(species,80,move),Mon("DUSCLOPS",100,"NIGHT_SHADE") with{Protected=true});
  var r=Action(f);var pp=r.Events.OfType<SourcePpCommandEvent>().First();Check(pp.CommandCost==2,"Pressure charges actual target");
  Check(!r.Trace.Any(t=>t.Purpose.EndsWith(".accuracy"))== (move is not("ERUPTION" or "WATER_SPOUT")),"protected first pass skips its accuracy only");
  Check(r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Target.Value==3&&e.Reason=="protected"),"protect event selected flank");
  if(move is "RETURN" or "FRUSTRATION" or "FLAIL" or "REVERSAL" or "LOW_KICK"){
   var immune=Action(Frame(Mon(species,80,move),Mon("DUSCLOPS",100,"NIGHT_SHADE")));
   Check(immune.Events.OfType<SourceHpChangedEvent>().Single().Damage==0&&immune.Trace.Length==4,"Ghost immunity retains crit variance secondary draws");
  }
  var endure=Action(Frame(Mon(species,80,move),Hp(Mon("JIGGLYPUFF",100,"DEFENSE_CURL"),1) with{Enduring=true}));
  Check(endure.State.Slots[3].Body!.Pokemon.CurrentHp==1,"Endure effective zero leaves one HP");
 }
 foreach(var (move,target) in new[]{("WATER_SPOUT","POLIWAG"),("ERUPTION","VULPIX")}){
  var r=Action(Frame(Mon(move=="ERUPTION"?"CAMERUPT":"WAILMER",80,move),Hp(Mon(target,100,target=="VULPIX"?"EMBER":"BUBBLE"),30)));
  Check(r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Target.Value==3),"source absorption");Check(r.Trace.Length==5,"one accuracy-only absorbed pass plus partner full pass");
 }
});
using var oracle=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));
var vectors=oracle.RootElement.GetProperty("vectors").EnumerateArray().ToArray();
int OriginalPower(string move,WildBattleCombatant actor,WildBattleCombatant target){
 if(move is "RETURN" or "FRUSTRATION")return vectors.Single(v=>v.GetProperty("kind").GetString()=="friendship"&&v.GetProperty("move").GetInt32()==catalog.Moves[Id(move)].SourceMoveId).GetProperty("effectivePowers")[actor.Pokemon.Friendship].GetInt32();
 if(move=="LOW_KICK"){
  int weight=proof.SpeciesMechanics[target.Pokemon.Species].WeightHectograms;
  return vectors.Single(v=>v.GetProperty("kind").GetString()=="weight").GetProperty("ranges").EnumerateArray().Single(v=>weight>=v.GetProperty("from").GetInt32()&&weight<=v.GetProperty("through").GetInt32()).GetProperty("power").GetInt32();
 }
 return vectors.Single(v=>v.GetProperty("kind").GetString()=="hp"&&v.GetProperty("maxHp").GetInt32()==actor.Pokemon.Stats.MaxHp&&v.GetProperty("moves").EnumerateArray().Any(x=>x.GetInt32()==catalog.Moves[Id(move)].SourceMoveId)).GetProperty("powers")[actor.Pokemon.CurrentHp].GetInt32();
}
// Independent integer damage transcription for these neutral Jigglypuff fixtures.
// C commands provide power; this helper never calls production damage or type helpers.
int Expected(WildBattleCombatant actor,WildBattleCombatant target,string move,int power,bool critical,int percent,bool spread=false,int fightingMultiplier=2){
 bool physical=move is not("ERUPTION" or "WATER_SPOUT");
 int attack=physical?actor.Pokemon.Stats.Attack:actor.Pokemon.Stats.SpecialAttack;
 int defense=physical?target.Pokemon.Stats.Defense:target.Pokemon.Stats.SpecialDefense;
 int value=attack*power*(2*actor.Pokemon.Level/5+2)/defense/50;
 if(physical)value=Math.Max(1,value);if(spread)value/=2;value+=2;if(critical)value*=2;
 string type=catalog.Moves[Id(move)].Type;var a=catalog.Species[actor.Pokemon.Species];
 if(a.Type1==type||a.Type2==type)value=value*15/10;
 if(type=="TYPE_FIGHTING")value*=fightingMultiplier;
 return Math.Max(1,value*percent/100);
}
Test("grouped unchanged source commands cover every friendship, HP and weight integer",()=>{
 Check(oracle.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"pinned source");
 var type=typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceDynamicPowerRules")!;
 int Rule(string name,params object[] values)=>(int)type.GetMethod(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,values)!;
 int groupsSeen=0,points=0;
 foreach(var v in vectors){string kind=v.GetProperty("kind").GetString()!;
  if(kind=="friendship"){
   bool frustration=v.GetProperty("move").GetInt32()==218;var powers=v.GetProperty("effectivePowers");Check(powers.GetArrayLength()==256,"all friendship");
   for(int i=0;i<256;i++){Check(Rule("Friendship",i,frustration)==powers[i].GetInt32(),"source friendship "+i);points++;}groupsSeen++;
  }else if(kind=="hp"){
   int max=v.GetProperty("maxHp").GetInt32();bool flail=v.GetProperty("moves")[0].GetInt32()==175;var powers=v.GetProperty("powers");Check(powers.GetArrayLength()==max+1,"complete HP denominator");
   for(int hp=0;hp<=max;hp++){Check((flail?Rule("Flail",hp,max):Rule("Eruption",hp,max,150))==powers[hp].GetInt32(),"source HP boundary");points++;}groupsSeen++;
  }else if(kind=="weight"){
   foreach(var range in v.GetProperty("ranges").EnumerateArray())for(int i=range.GetProperty("from").GetInt32();i<=range.GetProperty("through").GetInt32();i++){Check(Rule("Weight",i)==range.GetProperty("power").GetInt32(),"source weight boundary");points++;}groupsSeen++;
  }
 }
 Check(groupsSeen>=37&&points>130000,"nonempty exhaustive integer vector groups");
});
Test("actual actions consume source power at every Flail threshold and friendship endpoint",()=>{
 foreach(var (species,move) in donors){
  var user=Mon(species,80,move);var target=Mon("JIGGLYPUFF",100,"DEFENSE_CURL");var settings=new List<WildBattleCombatant>();
  if(move is "RETURN" or "FRUSTRATION")foreach(int friend in new[]{0,1,2,3,4,5,69,70,127,128,252,253,254,255})settings.Add(user with{Pokemon=user.Pokemon with{Friendship=friend}});
  else if(move is "FLAIL" or "REVERSAL"){
   int last=-1;for(int hp=1;hp<=user.Pokemon.Stats.MaxHp;hp++){int power=OriginalPower(move,Hp(user,hp),target);if(power!=last){settings.Add(Hp(user,hp));if(hp>1)settings.Add(Hp(user,hp-1));last=power;}}
  }else if(move is "ERUPTION" or "WATER_SPOUT")foreach(int hp in new[]{1,2,3,user.Pokemon.Stats.MaxHp/2,user.Pokemon.Stats.MaxHp-1,user.Pokemon.Stats.MaxHp})settings.Add(Hp(user,hp));
  else settings.Add(user);
  foreach(var a in settings)foreach(int side in new[]{2,3}){
   int t=side==2?3:2;var f=Frame(a,target,31,side,t);var r=Action(f,side,t);int power=OriginalPower(move,a,target);
   foreach(var e in r.Events.OfType<SourceHpChangedEvent>())Check(e.Damage==Expected(a,f.Get(e.Target).Body!,move,power,e.Critical,e.RandomPercent,move is "ERUPTION" or "WATER_SPOUT"),"source-derived exact damage "+move+"/"+a.Pokemon.CurrentHp+"/"+a.Pokemon.Friendship);
  }
 }
});
Test("spread actions reuse their source HP power, while the next action observes changed HP",()=>{
 foreach(string move in new[]{"ERUPTION","WATER_SPOUT"}){
  var actor=Hp(Mon(move=="ERUPTION"?"CAMERUPT":"WAILMER",80,move),60);
  var f=Frame(actor,Mon("JIGGLYPUFF",100,"DEFENSE_CURL"));var r=Action(f);int power=OriginalPower(move,actor,f.Slots[3].Body!);
  var hits=r.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Move==Id(move)).ToArray();Check(hits.Length==2,"both actual targets");
  foreach(var e in hits)Check(e.Damage==Expected(actor,f.Get(e.Target).Body!,move,power,e.Critical,e.RandomPercent,true),"same cached C power both targets");
  var changedState=Put(r.State,2,Hp(r.State.Slots[2].Body!,80));var next=Action(changedState);int changed=OriginalPower(move,changedState.Slots[2].Body!,changedState.Slots[3].Body!);Check(changed>power,"next action uses changed HP");
  foreach(var e in next.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Move==Id(move)))Check(e.Damage==Expected(changedState.Slots[2].Body!,changedState.Get(e.Target).Body!,move,changed,e.Critical,e.RandomPercent,true),"new action register resets");
 }
});
ImmutableArray<SourceActionSelection> Picks(SourceBattleFrame f)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?c.Slot:c.Slot.Opposite)).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"four-turn");return r;}
SourceFourActionResult Next(SourceFourActionResult r){var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var next=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var copy=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);Check(Json(next)==Json(copy),"exact JSON continuation");return Ok(next);}
Test("live HP after a faster hit determines Flail and Eruption in real four-action turns",()=>{
 foreach(string move in new[]{"FLAIL","ERUPTION","WATER_SPOUT"}){
  var actor=Mon(move=="FLAIL"?"MAGIKARP":move=="ERUPTION"?"CAMERUPT":"WAILMER",80,move);
  var f=Frame(actor,Mon("MANKEY",100,"SEISMIC_TOSS"));var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Picks(f)));var ev=r.Events.ToList();int limit=0;
  while(!r.CompletedCleanTurn){Check(++limit<25,"finite full turn");r=Next(r);ev.AddRange(r.Events);}
  var priorHit=ev.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Id("SEISMIC_TOSS")&&e.Target.Value==2);var hits=ev.OfType<SourceHpChangedEvent>().Where(e=>e.Move==Id(move)).ToArray();Check(hits.Length>=1&&priorHit.Sequence<hits[0].Sequence,"faster attack precedes power command "+move+" "+Json(ev.OfType<SourceHpChangedEvent>()));
  var live=Hp(actor,priorHit.HpAfter);int power=OriginalPower(move,live,f.Slots[3].Body!);Check(power!=OriginalPower(move,actor,f.Slots[3].Body!),"power changes with live HP");
  // Slot3 Mankey is neutral to Normal/Fire/Water and has no preceding stage changes.
  var e=hits.Single(x=>x.Target.Value==3);Check(e.Damage==Expected(live,f.Slots[3].Body!,move,power,e.Critical,e.RandomPercent,move!="FLAIL"),"actual live execution power");
 }
});
Test("real spread KO pauses for rewards and resumes without replaying PP or targets",()=>{
 var f=Frame(Mon("WAILMER",80,"WATER_SPOUT"),Hp(Mon("JIGGLYPUFF",5,"DEFENSE_CURL"),1));f=Put(f,1,Mon("JIGGLYPUFF",100,"DEFENSE_CURL"));
 var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Picks(f)));var ev=r.Events.ToList();bool paused=false;int limit=0;
 while(r.State.FourTurn is not null&&r.State.FourTurn.Phase is not(SourceFourActionPhase.BattleEnded or SourceFourActionPhase.ReplacementsPending)){
  Check(++limit<30,"finite KO continuation");paused|=r.State.FourTurn.Phase==SourceFourActionPhase.RewardsPending;
  if(r.State.Rewards?.Pending is{} pending){var m=pending.Moves[0];r=Ok(SourceFourRewards.Learn(catalog,r.State,r.Continuation!.Value,new(pending.Recipient,m.Level,m.Move,null)));}
  else r=Next(r);ev.AddRange(r.Events);
 }
 Check(paused&&ev.OfType<SourceRewardAppliedEvent>().Any(),"actual KO reward pause");Check(ev.OfType<SourceHpChangedEvent>().Count(e=>e.Move==Id("WATER_SPOUT"))==2,"both targets execute exactly once");
 Check(ev.OfType<SourcePpCommandEvent>().Where(e=>e.CurrentMove==Id("WATER_SPOUT")).Sum(e=>e.Before-e.After)==1,"one PP across pauses");Check(!ev.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==3),"KO cancels victim action");
});
Test("Low Kick reads all six real species weight tiers and a newly switched target",()=>{
 var user=Mon("MANKEY",80,"LOW_KICK");var weights=new List<int>();
 foreach(string species in new[]{"HORSEA","GOLDEEN","SEAKING","CLAMPERL","WAILMER","WAILORD"}){
  var target=Mon(species,100,species is "WAILMER" or "WAILORD"?"GROWL":species is "GOLDEEN" or "SEAKING"?"PECK":"WATER_GUN");int power=OriginalPower("LOW_KICK",user,target);weights.Add(power);
  foreach(int actor in new[]{2,3}){var r=Action(Frame(user,target,31,actor,actor^1),actor,actor^1);var e=r.Events.OfType<SourceHpChangedEvent>().Single();Check(e.Damage==Expected(user,target,"LOW_KICK",power,e.Critical,e.RandomPercent,fightingMultiplier:1),"authentic target weight "+species);}
 }
 Check(weights.SequenceEqual(new[]{20,40,60,80,100,120}),"six authenticated species tiers");
 var outgoing=Mon("HORSEA",100,"WATER_GUN");var incoming=Mon("WAILORD",100,"GROWL");var f=Frame(user,outgoing);var party=f.Party(BattleSide.Opponent);f=f with{Parties=f.Parties.SetItem(1,party with{Members=party.Members.Add(Member(incoming))})};
 var choices=Picks(f).SetItem(3,new(f.Slots[3].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Opponent,2)));
 var rturn=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices));var ev=rturn.Events.ToList();int limit=0;
 while(!rturn.CompletedCleanTurn){Check(++limit<25,"finite switch turn");rturn=Next(rturn);ev.AddRange(rturn.Events);}
 var entry=ev.OfType<SourceCalledPartySwitchedEvent>().Single();var hit=ev.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Id("LOW_KICK"));
 Check(entry.Sequence<hit.Sequence&&hit.Target.Value==3,"actual target replaced before weight command");
 Check(hit.Damage==Expected(user,incoming,"LOW_KICK",120,hit.Critical,hit.RandomPercent,fightingMultiplier:1),"uses incoming Wailord weight");
});
Test("accuracy misses and sleep cancellation preserve exact RNG and PP positions",()=>{
 foreach(var (species,move) in donors.Where(x=>x.Item2 is not("ERUPTION" or "WATER_SPOUT")))foreach(int side in new[]{2,3}){
  var actor=Mon(species,80,move) with{AccuracyStage=0};var r=Action(Frame(actor,Mon("MAGIKARP",100,"TACKLE"),31,side,side^1),side,side^1);
  Check(r.Events.OfType<SourceMoveMissedEvent>().Any()&&r.Trace.Length==1,"accuracy-only miss");Check(r.Events.OfType<SourcePpCommandEvent>().Single().RngCursor==(move=="LOW_KICK"?0UL:1UL),"miss keeps original PP order");
  var asleep=Action(Frame(Mon(species,80,move) with{Status=2},Mon("MAGIKARP",100,"TACKLE"),31,side,side^1),side,side^1);
  Check(asleep.Trace.IsEmpty&&!asleep.Events.OfType<SourcePpCommandEvent>().Any(),"sleep does not charge PP or hit RNG");
 }
});
Test("shared Eruption register survives an isolated target-pass HP change",()=>{
 // The admitted four-slot held-item cohort cannot heal HP between spread passes.
 // Exercise that future-facing command boundary directly against the same concrete frame
 // context, alongside the actual public-action and unchanged-C cache checks above.
 var assembly=typeof(WildBattleRules).Assembly;var ownerType=assembly.GetType("Game.Domain.Battle.FrameRecipeContext")!;var recipe=ownerType.BaseType!;
 var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
 foreach(string move in new[]{"ERUPTION","WATER_SPOUT"}){
  var actor=Hp(Mon(move=="ERUPTION"?"CAMERUPT":"WAILMER",80,move),60);var f=Frame(actor,Mon("JIGGLYPUFF",100,"DEFENSE_CURL"));var def=catalog.Moves[Id(move)];
  var ctx=ownerType.GetConstructors(flags).Single().Invoke([catalog,f,new SourceResolvedMoveAction(f.Slots[2].Occupant!.Value,f.Revision,0,new(3)),null]);
  recipe.GetMethod("PrepareDynamicPower",flags)!.Invoke(ctx,[new BattlerSlot(2),def]);
  var changed=Put(f,2,Hp(actor,1));ownerType.GetProperty("Frame",flags)!.SetValue(ctx,changed);
  recipe.GetMethod("ExecuteDynamicPower",flags)!.Invoke(ctx,[catalog,new BattlerSlot(2),new BattlerSlot(3),0,def,"source-battle.slot-2"]);
  var events=(ImmutableArray<SourceBattleEvent>.Builder)ownerType.GetField("Events",flags)!.GetValue(ctx)!;var e=events.OfType<SourceHpChangedEvent>().Single();
  Check(e.Damage==Expected(actor,f.Slots[3].Body!,move,OriginalPower(move,actor,f.Slots[3].Body!),e.Critical,e.RandomPercent,true),"cached action-start power after live HP changed");
  Check((int)recipe.GetField("preparedDynamicPower",flags)!.GetValue(ctx)! ==OriginalPower(move,actor,f.Slots[3].Body!),"damage command does not reset register");
 }
});
Test("frozen canonical accuracy cases document inherited shared-command corrections",()=>{
 var current=AccuracyParity.Capture(proof);current.AddRange(AccuracyParity.Capture(proof,true));var before=JsonSerializer.Deserialize<AccuracyBaseline>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"accuracy-baseline-88543cb.json")))!.snapshots;
 before.AddRange(JsonSerializer.Deserialize<AccuracyBaseline>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"accuracy-hustle-baseline-88543cb.json")))!.snapshots);
 Check(current.Count==150&&before.Count==current.Count,"complete accuracy corpus");int changed=0,hustleMisses=0;
 for(int i=0;i<current.Count;i++){
  var a=current[i];var b=before[i];Check((a.Move,a.Context,a.Seed,a.Reverse)==(b.Move,b.Context,b.Seed,b.Reverse),"immutable case identity");
  bool expectedHit=a.Context is "fly" or "dive" or "dig"?false:a.Context=="lock-on"?true:a.AccuracyRoll<=(a.Context=="hustle"?80:a.Context=="bright-powder"?90:a.Context=="lax-incense"?95:100);
  Check(a.Hit==expectedHit,"source accuracy branch "+Json(a));
  if(a.Context=="hustle"&&a.Seed==58){Check(!a.Hit&&b.Hit,"frozen legacy Hustle miss correction");hustleMisses++;}
  if(a.Context is "fly" or "dive" or "dig" or "lock-on")Check(a.AccuracyRoll is null,"source early gate omits ordinary accuracy");
  if(a.Sha256!=b.Sha256){changed++;differences.Add(new{accuracyBefore=b,accuracyAfter=a});}
  if(a.Context=="plain")Check(a.WithoutPpCursorSha256==b.WithoutPpCursorSha256,"only PP cursor changes in plain accuracy case");
 }
 Check(changed>50&&hustleMisses==6,"explicitly frozen source changes include both Hustle sides");
});
Test("invalid action inputs fail atomically while authenticated recipes are admitted",()=>{
 var f=Frame(Mon("MANKEY",80,"LOW_KICK"),Mon("JIGGLYPUFF",100,"DEFENSE_CURL"));var valid=new SourceResolvedMoveAction(f.Slots[2].Occupant!.Value,f.Revision,0,new(3));
 void Atomic(SourceBattleFrame state,SourceResolvedMoveAction a){var r=SourceBattleKernel.ResolveAction(catalog,state,a);Check(!r.Succeeded&&ReferenceEquals(r.State,state)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic "+r.Fault);}
 Atomic(f,valid with{Revision=-1});Atomic(f,valid with{ChosenSlot=4});Atomic(f,valid with{Actor=valid.Actor with{EntrySerial=9}});Atomic(f with{Rng=new(31,ulong.MaxValue)},valid);
 Check(!SourceBattleKernel.SupportsRecipe(catalog.Moves[Id("LOW_KICK")] with{Power=20}),"forged metadata rejected");Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id("PRESENT")]),"authenticated Present recipe admitted");
});
File.WriteAllText(args[1],Json(new{baseCommit="88543cb9ee09fd1a56a5567bc1aaa7bc0aeea900",groups,checks,failures,differences,results}));
Console.WriteLine($"{groups} groups, {checks} checks, {failures} failures; {differences.Count} explicit singles changes");return failures==0?0:1;

internal sealed record AccuracyBaseline(string baseCommit,List<AccuracyCase> snapshots);
