using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
if(args.Length!=4)throw new ArgumentException("CONTENT_ROOT SOURCE_ROOT OUTPUT capture|verify");
string root=args[0],source=args[1],output=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(root);var catalog=proof.Catalog;
string J<T>(T x)=>JsonSerializer.Serialize(x);
string Hash<T>(T x)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(J(x))));
MoveId Id(string x)=>new("pret:MOVE_"+x);
string tm=File.ReadAllText(Path.Combine(source,"src/data/pokemon/tmhm_learnsets.h"));
int groups=0,checks=0,failures=0,completeTurns=0;var observations=new List<object>();
void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
void Test(string name,Action f){groups++;try{f();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);}}
WildBattleCombatant Mon(string species="SNORLAX",string move="AMNESIA",int level=100,uint pid=0){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 string block=Regex.Match(tm,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;
 Check(catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==Id(move)&&x.Level<=level)||Regex.IsMatch(block,@"[TH]M\d\d_"+move+@"\)"),"authentic natural/TM donor "+species+"/"+move);
 return new(p with{Moves=[new(Id(move),catalog.Moves[Id(move)].Pp,catalog.Moves[Id(move)].Pp)]});
}
var moves=new[]{("PIDGEOT","GUST"),("DRAGONAIR","TWISTER"),("PIKACHU","THUNDER"),("BLAZIKEN","SKY_UPPERCUT")};
var original=PretRocketHideoutPokemon.Load(root).Catalog;
var supplement=PretVermilionGymTrainers.ExtendPokemon(root,PretCeladonApproachPokemon.Load(root).Catalog,celadonApproachRules:true);
var prior=original with{Moves=original.Moves.SetItems(supplement.Moves)};
var old=PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior,root,root).Catalog,root).Catalog,root).Catalog;
var legacy=PretMetronomeMoveCatalog.Load(root).Extend(PretFuchsiaRoutePokemon.Load(old,root).Catalog);
WildBattleState Pair(WildBattleCombatant a,WildBattleCombatant b,uint seed,bool canonical=true)=>new(a,b,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null};
var snapshots=new List<object>();
foreach(bool canonical in new[]{false,true})foreach(var (_,move) in moves)foreach(string scenario in new[]{"plain","protected","air","substitute","sun","rain"})foreach(uint seed in new uint[]{0,31}){
 var d=Mon();if(scenario=="protected")d=d with{Protected=true};if(scenario=="substitute")d=d with{CalledVolatile=new(){SubstituteHp=25}};
 if(scenario=="air"){d=Mon("PIDGEOT","FLY");d=d with{CalledTurnState=new(){Lock=new(WildBattleCalledLockKind.Charge,Id("FLY"),0,Id("FLY"),WildBattleSide.Player,1)}};}
 var s=Pair(Mon("CLEFAIRY","METRONOME",80),d,seed,canonical);if(scenario=="rain")s=s with{RainTurnsRemaining=5};if(scenario=="sun")s=s with{CalledField=new(){SunTurnsRemaining=5}};
 var r=WildBattleRules.ResolveCalledMoveAction(canonical?catalog:legacy,s,new(WildBattleSide.Player,0,Id(move)));Check(r.Succeeded,"baseline accepted "+move+"/"+scenario+" "+r.Fault);snapshots.Add(new{canonical,move,scenario,seed,sha256=Hash(r),result=r});
}
string baseline=Path.Combine(AppContext.BaseDirectory,"singles-baseline-b01522c.json");
if(args[3]=="capture"){
 Check(typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasWeatherHitRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)==null,"capture requires pre-edit product");
 string path=Path.Combine(output,"singles-baseline-b01522c.json");Check(!File.Exists(path),"immutable baseline");File.WriteAllText(path,J(snapshots)+"\n");Console.WriteLine("Captured "+snapshots.Count+" complete results");return 0;
}
Test("96 exact complete-result baselines preserve historical and canonical profiles",()=>{using var b=JsonDocument.Parse(File.ReadAllText(baseline));Check(b.RootElement.GetArrayLength()==96,"nonempty baseline");var now=JsonSerializer.SerializeToElement(snapshots);for(int i=0;i<96;i++)Check(b.RootElement[i].GetProperty("sha256").GetString()==now[i].GetProperty("sha256").GetString(),"complete result "+now[i].GetProperty("move")+"/"+now[i].GetProperty("scenario")+"/"+now[i].GetProperty("canonical")+"/"+now[i].GetProperty("seed"));});
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(int actor,string species,string move,uint seed=31){var m=Enumerable.Range(0,4).Select(i=>Mon() with{SpeedStage=i}).ToArray();m[actor]=Mon(species,move,80) with{SpeedStage=12};return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var c=f.Slots[slot];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=m}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(m))})};}
SourceBattleFrame Semi(SourceBattleFrame f,int slot,string move,int actor){var d=move switch{"FLY"=>Mon("PIDGEOT","FLY"),"BOUNCE"=>Mon("SPOINK","BOUNCE"),"DIG"=>Mon("DIGLETT","DIG"),_=>Mon("WAILMER","DIVE")};f=Put(f,slot,d);var c=f.Slots[slot];return f with{Slots=f.Slots.SetItem(slot,c with{Registers=c.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Charge,Id(move),0,Id(move),new(actor),1,0,null,false),0,null)}})};}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,"four action "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)==null,"four identity");return r;}
SourceFourActionResult Start(SourceBattleFrame f,int actor,int? target=null){var choices=f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?c.Slot:new(c.Slot.Value==actor?target??(actor^1):c.Slot.Value^1))).ToImmutableArray();return Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices));}
SourceFourActionResult Next(SourceFourActionResult r){var copy=JsonSerializer.Deserialize<SourceBattleFrame>(J(r.State))!;var a=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var b=SourceFourActionTurns.Advance(catalog,copy,r.Continuation.Value);Check(J(a)==J(b),"exact saved continuation");return Good(a);}
SourceKernelResult Hit(SourceBattleFrame f,int actor,int? target=null){
 // Flinch requires the actual queued-turn owner, even in diagnostic single-action probes.
 if(catalog.Moves[f.Slots[actor].Body!.Pokemon.Moves[0].Move].Effect=="EFFECT_TWISTER"){
  var start=Start(f,actor,target);Check(start.State.Queue[0].Slot.Value==actor,"weather-hit actor first");
  Check(start.State.Rng==f.Rng,"distinct source speeds avoid order RNG");
  // Protected is a diagnostic already-executed target action; BeginActions clears the prior turn bit.
  for(int i=0;i<4;i++)if(f.Slots[i].Body!.Protected)start=start with{State=Put(start.State,i,start.State.Slots[i].Body! with{Protected=true})};var next=Next(start);return new(next.State,next.Events,next.Trace,null,next.Fault);
 }
 var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target??(actor^1))));Check(r.Succeeded,"kernel "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)==null,"identity");return r;
}

Test("all four authentic moves execute at every actor position with source PP order",()=>{foreach(var(species,move)in moves)for(int actor=0;actor<4;actor++){var f=Frame(actor,species,move);var r=Hit(f,actor);var pp=r.Events.OfType<SourcePpCommandEvent>().ToArray();Check(pp.Length==(move=="TWISTER"?2:1),"per-pass PP command");Check(r.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(move)].Pp-1,"one PP debit");Check(pp[0].RngCursor==f.Rng.Cursor+1,"accuracy precedes PP");Check(r.Trace.Count(x=>x.Purpose.EndsWith("secondary-effect"))==r.Events.OfType<SourceHpChangedEvent>().Count(),"secondary follows every reached hit");observations.Add(new{move,actor,r.Events,r.Trace});}});
Test("Fly and Bounce take shared airborne damage; Dig and Dive miss before RNG",()=>{foreach(var(species,move)in moves)foreach(string state in new[]{"FLY","BOUNCE","DIG","DIVE"}){var f=Semi(Frame(2,species,move),3,state,2);var r=Hit(f,2,3);bool hit=state is "FLY" or "BOUNCE";Check(r.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Target.Value==3)==hit,"semi-state "+move+"/"+state);if(!hit)Check(r.Trace.FirstOrDefault()?.Purpose.EndsWith("accuracy")!=true||move=="TWISTER","no RNG for unavailable first target");if(hit){var clean=f with{Slots=f.Slots.SetItem(3,f.Slots[3] with{Registers=new()})};var plain=Hit(clean,2,3);var a=r.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Target.Value==3);var b=plain.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Target.Value==3);Check(move is "GUST" or "TWISTER"?a.Damage>=b.Damage*2-2&&a.Damage<=b.Damage*2+2:a.Damage==b.Damage,"air multiplier "+move);}}});
Test("Protect precedes Lock-On; Lock-On bypasses Dig and Dive; weather suppression reaches accuracy",()=>{foreach(string state in new[]{"DIG","DIVE"}){var f=Semi(Frame(0,"PIKACHU","THUNDER"),1,state,0);var c=f.Slots[1];f=f with{Slots=f.Slots.SetItem(1,c with{Registers=c.Registers with{Turn=c.Registers.Turn! with{SureHitTurnsRemaining=2,SureHitSource=new(0)}}}),Field=new(){RainTurnsRemaining=5}};var r=Hit(f,0);Check(!r.Trace.Any(x=>x.Purpose.EndsWith("accuracy"))&&r.Events.OfType<SourceHpChangedEvent>().Any(),"Lock-On no accuracy draw");f=Put(f,1,f.Slots[1].Body! with{Protected=true});r=Hit(f,0);Check(r.Trace.IsEmpty&&!r.Events.OfType<SourceHpChangedEvent>().Any(),"Protect beats Lock-On and rain");}foreach(string suppressor in new[]{"none","PSYDUCK","RAYQUAZA"}){var f=Frame(0,"PIKACHU","THUNDER");f=f with{Field=new(){RainTurnsRemaining=5}};if(suppressor!="none")f=Put(f,2,Mon(suppressor,suppressor=="PSYDUCK"?"TAIL_WHIP":"REST",100,suppressor=="PSYDUCK"?1u:0u));var r=Hit(f,0);Check(r.Trace.Any(x=>x.Purpose.EndsWith("accuracy"))==(suppressor!="none"),"weather suppression "+suppressor);}});
Test("176 original accuracy execution vectors and 16 explicit item-admission boundaries",()=>{
 using var v=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));Check(v.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"pinned source");int count=0;
 foreach(var row in v.RootElement.GetProperty("vectors").EnumerateArray()){
  int family=row.GetProperty("family").GetInt32(),mode=row.GetProperty("mode").GetInt32();var(species,move)=moves[family];var f=Frame(2,species,move,row.GetProperty("seed").GetUInt32());
  if(mode==1)f=Put(f,3,f.Slots[3].Body! with{Protected=true});if(mode==2)f=Semi(f,3,"FLY",2);if(mode is 3 or 9)f=Semi(f,3,"DIG",2);if(mode is 4 or 11)f=Semi(f,3,"DIVE",2);
  if(mode is 5 or 6 or 11)f=f with{Field=new(){RainTurnsRemaining=5}};if(mode is 7 or 8)f=f with{Field=new(){SunTurnsRemaining=5}};
  if(mode is 6 or 8)f=Put(f,0,Mon("PSYDUCK","TAIL_WHIP",100,1) with{SpeedStage=0});
  if(mode==9){var c=f.Slots[3];f=f with{Slots=f.Slots.SetItem(3,c with{Registers=c.Registers with{Turn=c.Registers.Turn! with{SureHitTurnsRemaining=2,SureHitSource=new(2)}}})};}
  if(mode==10){f=Put(f,2,f.Slots[2].Body! with{AccuracyStage=0});f=Put(f,3,f.Slots[3].Body! with{HeldItem="ITEM_BRIGHT_POWDER"});}
  if(mode==10){var blocked=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[2].Occupant!.Value,f.Revision,0,new(3)));Check(!blocked.Succeeded&&ReferenceEquals(blocked.State,f)&&blocked.Events.IsEmpty&&blocked.Trace.IsEmpty,"existing Bright Powder admission remains atomic");count++;continue;}
  var r=Hit(f,2,3);var targetPp=r.Events.OfType<SourcePpCommandEvent>().First();int draws=(int)(targetPp.RngCursor-f.Rng.Cursor);
  bool missed=r.Events.OfType<SourceMoveMissedEvent>().Any(e=>e.Target.Value==3)||r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Target.Value==3&&e.Reason=="protected");
  Check(!missed==row.GetProperty("hit").GetBoolean(),"source hit "+family+"/"+mode+"/"+row.GetProperty("seed"));Check(draws==row.GetProperty("draws").GetInt32(),"source accuracy cursor");
  if(draws==1)Check(r.Trace[0].After==row.GetProperty("finalSeed").GetUInt32(),"exact source RNG state");count++;
 }
 Check(count==192,"176 original C execution vectors plus 16 explicit existing item-admission rejections");
});
Test("Twister resets airborne multiplier and spends both Pressure holders once",()=>{
 var f=Frame(2,"DRAGONAIR","TWISTER");f=Put(f,1,Mon("AERODACTYL","AGILITY",100,1) with{SpeedStage=1});f=Put(f,3,Mon("AERODACTYL","FLY",100,1) with{SpeedStage=3});var c=f.Slots[3];f=f with{Slots=f.Slots.SetItem(3,c with{Registers=c.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Charge,Id("FLY"),0,Id("FLY"),new(2),1,0,null,false),0,null)}})};
 var r=Hit(f,2,3);var pp=r.Events.OfType<SourcePpCommandEvent>().ToArray();Check(pp.Length==2&&pp[0].CommandCost==3&&pp[0].Before-pp[0].After==3&&pp[1].Before==pp[1].After,"two target Pressure one debit");
 Check(r.Events.OfType<SourceHpChangedEvent>().Select(e=>e.Target.Value).SequenceEqual(new[]{3,1}),"source initial then partner target");
 var clean=f with{Slots=f.Slots.SetItem(3,f.Slots[3] with{Registers=new()})};var plain=Hit(clean,2,3);Check(r.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Target.Value==1).Damage==plain.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Target.Value==1).Damage,"second target multiplier reset");
});
Test("secondary follows target damage and respects substitute and immunity",()=>{
 int flinches=0,paralyses=0;foreach(var(species,move)in moves.Where(x=>x.Item2 is "TWISTER" or "THUNDER"))foreach(uint seed in new uint[]{0,2,6,31,60,61,62,63}){
  var f=Frame(2,species,move,seed);if(move=="THUNDER")f=f with{Field=new(){RainTurnsRemaining=5}};var r=Hit(f,2,3);
  var hits=r.Events.OfType<SourceHpChangedEvent>().ToArray();var rolls=r.Trace.Where(t=>t.Purpose.EndsWith("secondary-effect")).ToArray();Check(hits.Length==rolls.Length,"one reached-target secondary draw");
  for(int i=0;i<hits.Length;i++){int target=hits[i].Target.Value;bool expected=rolls[i].Raw%100<=catalog.Moves[Id(move)].SecondaryEffectChance;bool actual=move=="TWISTER"?r.State.Slots[target].Body!.Flinched:r.State.Slots[target].Body!.Status==WildBattleStatus.Paralysis;Check(expected==actual,"source inclusive secondary threshold");if(actual){if(move=="TWISTER")flinches++;else paralyses++;var e=r.Events.Single(e=>move=="TWISTER"?e is SourceFlinchEvent sf&&sf.Side.Value==target:e is SourceStatusChangedEvent st&&st.Side.Value==target);Check(e.Sequence>hits[i].Sequence,"secondary after HP command");}}
  f=Put(f,3,f.Slots[3].Body! with{CalledVolatile=new(){SubstituteHp=25}});r=Hit(f,2,3);Check(!r.State.Slots[3].Body!.Flinched&&r.State.Slots[3].Body!.Status==0,"substitute blocks both secondaries while reached draw remains");
 }
 Check(flinches>0&&paralyses>0,"nonempty positive secondary branches");
 foreach(var(species,move)in moves.Where(x=>x.Item2 is "TWISTER" or "THUNDER")){var f=Frame(2,species,move,60);f=Put(f,3,Mon("CATERPIE","STRING_SHOT") with{SpeedStage=3});if(move=="THUNDER")f=f with{Field=new(){RainTurnsRemaining=5}};var r=Hit(f,2,3);Check(!r.State.Slots[3].Body!.Flinched&&r.State.Slots[3].Body!.Status==0,"real Shield Dust donor");}
});
Test("effective zero-loss Endure still prints critical and allows Thunder paralysis",()=>{
 int critical=0,paralysis=0;
 foreach(uint seed in Enumerable.Range(0,24).Select(x=>(uint)x)){
  var f=Frame(0,"PIKACHU","THUNDER",seed);f=f with{Field=new(){RainTurnsRemaining=5}};f=Put(f,1,f.Slots[1].Body! with{Enduring=true,Pokemon=f.Slots[1].Body!.Pokemon with{CurrentHp=1}});var r=Hit(f,0);
  var hp=r.Events.OfType<SourceHpChangedEvent>().Single();Check(hp.Damage==0&&hp.HpAfter==1,"Endure preserves effective zero loss");var roll=r.Trace.Single(x=>x.Purpose.EndsWith("secondary-effect"));bool expected=roll.Raw%100<=30;Check((r.State.Slots[1].Body!.Status==WildBattleStatus.Paralysis)==expected,"effect follows original effectiveness rather than HP lost");if(expected)paralysis++;
  if(hp.Critical){critical++;var message=r.Events.OfType<SourceCriticalHitEvent>().Single();Check(message.Sequence>hp.Sequence,"critical command");}
 }
 Check(critical>0&&paralysis>0,"nonempty critical and zero-loss effect branches");
});
Test("complete natural singles and real spread turns preserve reward and atomic ownership",()=>{
 foreach(var(species,move)in moves){var entry=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon(species,move,80)],0,Mon(),new(31));Check(entry.Succeeded,"source singles entry "+entry.Fault);var r=WildBattleRules.ResolveMove(catalog,entry.State!,0);Check(r.Succeeded&&r.State!.Turn==entry.State!.Turn+1,"complete source singles "+r.Fault);Check(r.State!.SourceWild!.Turn==null,"clean singles boundary");completeTurns++;}
 foreach(bool lethal in new[]{false,true}){
  var f=Frame(2,"DRAGONAIR","TWISTER");if(lethal)foreach(int target in new[]{1,3})f=Put(f,target,f.Slots[target].Body! with{Pokemon=f.Slots[target].Body!.Pokemon with{CurrentHp=1}});
  var r=Start(f,2,3);var all=r.Events.ToList();int steps=0;while(r.State.FourTurn is{Phase:not SourceFourActionPhase.BattleEnded}){Check(++steps<30,"finite real spread turn");if(r.State.Rewards?.Pending is{} pending){var m=pending.Moves[0];r=Good(SourceFourRewards.Learn(catalog,r.State,r.Continuation!.Value,new(pending.Recipient,m.Level,m.Move,null)));}else r=Next(r);all.AddRange(r.Events);}
  Check(r.CompletedCleanTurn||lethal&&r.State.FourTurn?.Phase==SourceFourActionPhase.BattleEnded,"complete spread or terminal reward boundary");completeTurns++;
  if(lethal)Check(all.OfType<SourceOpponentRewardCompletedEvent>().Count()==2&&r.State.Outcome==WildBattleOutcome.Won,"both KOs rewarded exactly once before final outcome");
  else Check(all.OfType<SourceMoveUsedEvent>().Select(e=>e.Actor.Value).Concat(all.OfType<SourceActionBlockedEvent>().Select(e=>e.Side.Value)).Concat(all.OfType<SourceFlinchEvent>().Where(e=>!e.Applied).Select(e=>e.Side.Value)).Distinct().Order().SequenceEqual(new[]{0,1,2,3}),"all four actual actors accounted for");
 }
 var start=Frame(2,"DRAGONAIR","TWISTER");var withoutQueue=SourceBattleKernel.ResolveAction(catalog,start,new(start.Slots[2].Occupant!.Value,start.Revision,0,new(3)));Check(!withoutQueue.Succeeded&&withoutQueue.Fault=="source_kernel_flinch_order_not_migrated"&&ReferenceEquals(withoutQueue.State,start)&&withoutQueue.Events.IsEmpty&&withoutQueue.Trace.IsEmpty,"Twister requires actual flinch queue atomically before any RNG");var token=start.Slots[2].Occupant!.Value;var rejected=SourceBattleKernel.ResolveAction(catalog,start,new(token,start.Revision+1,0,new(3)));Check(!rejected.Succeeded&&ReferenceEquals(rejected.State,start)&&rejected.Events.IsEmpty&&rejected.Trace.IsEmpty,"stale action atomic");
 var good=Start(start,2,3);var stale=good.Continuation!.Value with{Revision=good.State.Revision+1};var bad=SourceFourActionTurns.Advance(catalog,good.State,stale);Check(!bad.Succeeded&&ReferenceEquals(bad.State,good.State)&&bad.Events.IsEmpty&&bad.Trace.IsEmpty,"stale continuation atomic");
});
File.WriteAllText(Path.Combine(output,"singles-after.json"),J(snapshots)+"\n");
File.WriteAllText(Path.Combine(output,"result.json"),J(new{groups,checks,failures,completeTurns,runtime=Environment.Version.ToString(),observations})+"\n");Console.WriteLine($"{groups} groups, {checks} checks, {failures} failures, {completeTurns} complete turns");return failures==0?0:1;
