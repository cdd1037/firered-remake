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
var names=new[]{"TAKE_DOWN","SUBMISSION","DOUBLE_EDGE","VOLT_TACKLE","JUMP_KICK","HI_JUMP_KICK"};
var snapshots=new List<object>();
foreach(bool canonical in new[]{false,true})foreach(string move in names)foreach(int scenario in Enumerable.Range(0,8)){
 bool reverse=scenario%2==1;uint seed=scenario<4?31u:0u;
 var actor=Mon("CLEFAIRY",80,"METRONOME");var target=Mon(scenario is 3 or 4?"GASTLY":"MAGIKARP",100,scenario is 3 or 4?"NIGHT_SHADE":"SPLASH");
 if(scenario==1)target=Hp(target,1);if(scenario==2)target=target with{CalledVolatile=new(){SubstituteHp=1}};
 if(scenario==4)target=target with{Protected=true};if(scenario==5)target=Hp(target with{Enduring=true},1);
 if(scenario==6){actor=actor with{AccuracyStage=0};target=target with{EvasionStage=12,HeldItem=canonical?"ITEM_FOCUS_BAND":"ITEM_NONE"};}
 if(scenario==7){actor=Hp(actor,1);target=target with{Protected=true};}
 var state=new WildBattleState(reverse?target:actor,reverse?actor:target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null};
 var result=WildBattleRules.ResolveCalledMoveAction(canonical?catalog:legacy,state,new(reverse?WildBattleSide.Opponent:WildBattleSide.Player,0,Id(move)));
 if(!result.Succeeded)throw new Exception($"baseline {canonical}/{move}/{scenario}: "+result.Fault);
 snapshots.Add(new{canonical,move,scenario,reverse,seed,sha256=Hash(result),result});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("Refuse to overwrite immutable baseline");
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasRecoilRecipe",BindingFlags.Static|BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unchanged db8579a product");
 File.WriteAllText(args[1],Json(new{baseCommit="db8579a28734e83ead8c3fa77bd57825a84370d3",snapshots}));Console.WriteLine($"Captured {snapshots.Count} complete pre-edit results");return 0;
}
var utilitySnapshots=new List<object>();
foreach(var pair in new[]{("MISDREAVUS","PAIN_SPLIT"),("GRIMER","MEMENTO"),("MAGIKARP","SPLASH")})
foreach(int variant in Enumerable.Range(0,8))foreach(bool reverse in new[]{false,true}){
 var a=Mon(pair.Item1,80,pair.Item2);var b=Mon("SNORLAX",100,"DEFENSE_CURL");
 a=Hp(a,variant==0?1:a.Pokemon.CurrentHp);if(variant==1)b=Hp(b,1);
 if(variant==2)b=b with{Protected=true};if(variant==3)b=b with{CalledVolatile=new(){SubstituteHp=7}};
 if(variant==4)b=b with{AttackStage=0,SpecialAttackStageDelta=-6};if(variant==5)b=Mon("REGICE",80,"AMNESIA");
 if(variant==6)b=b with{HeldItem="ITEM_SHELL_BELL"};if(variant==7)a=a with{HeldItem="ITEM_SHELL_BELL"};
 var st=new WildBattleState(reverse?b:a,reverse?a:b,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
 var r=WildBattleRules.ResolveSourceMoveAction(catalog,st,new(reverse?WildBattleSide.Opponent:WildBattleSide.Player,0));if(!r.Succeeded)throw new Exception("utility baseline "+pair.Item2+" "+r.Fault);
 utilitySnapshots.Add(new{move=pair.Item2,variant,reverse,hash=Hash(r)});
}
if(args.Contains("--utility-capture")){if(File.Exists(args[1]))throw new Exception("Never overwrite frozen utility baseline");File.WriteAllText(args[1],Json(new{baseCommit="a51e8a633b36739e398e2e3ef95bab32f15d6a72",snapshots=utilitySnapshots})+"\n");return 0;}
bool utilityOnly=args.Contains("--utility");
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){if(utilityOnly&&!label.StartsWith("utility"))return;groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
File.WriteAllText(args[1]+".singles-after.json",Json(snapshots));
Test("complete frozen canonical and historical singles corpus",()=>{
 using var b=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-before-db8579a.json")));
 var before=b.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(before.Length==96&&before.Length==snapshots.Count,"96 complete results");
 int same=0,corrected=0;
 for(int i=0;i<before.Length;i++){
  var now=JsonSerializer.SerializeToElement(snapshots[i]);bool changed=before[i].GetProperty("sha256").GetString()!=now.GetProperty("sha256").GetString();
  bool expected=before[i].GetProperty("canonical").GetBoolean()&&before[i].GetProperty("scenario").GetInt32()==7&&before[i].GetProperty("move").GetString() is "JUMP_KICK" or "HI_JUMP_KICK";
  Check(changed==expected,"only source crash receipt changes: "+now.GetProperty("move")+"/"+now.GetProperty("scenario"));
  if(!changed){same++;continue;}
  var node=System.Text.Json.Nodes.JsonNode.Parse(now.GetProperty("result").GetRawText())!;
  foreach(var state in new[]{node["State"]!,node["Battle"]!["State"]!}){
   var actor=state["Opponent"]!.AsObject();Check(actor["CalledDamage"]!["PhysicalDamage"]!.GetValue<int>()==1&&actor["CalledDamage"]!["PhysicalSource"]!.GetValue<int>()==0,"crash source target and actual HP receipt");actor.Remove("CalledDamage");
  }
  Check(Hash(node)==before[i].GetProperty("sha256").GetString(),"every other state field, event and RNG draw remains byte-identical");corrected++;
 }
 Check(same==94&&corrected==2,"all 48 historical unchanged; 46 canonical unchanged, two exact source receipts corrected");
});

WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(int actor,string species,string move,uint seed=31,int? target=null){
 var mons=Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,"DEFENSE_CURL") with{SpeedStage=i}).ToArray();
 mons[actor]=Mon(species,80,move) with{SpeedStage=12};
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
Test("utility immutable canonical results and authentic donors",()=>{
 using var b=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"utility-hp-before-a51e8a6.json")));
 Check(Json(b.RootElement.GetProperty("snapshots"))==Json(utilitySnapshots),"48 complete state/event/RNG hashes unchanged");
 foreach(var pair in new[]{("MISDREAVUS","PAIN_SPLIT"),("GRIMER","MEMENTO"),("MAGIKARP","SPLASH")}){
  var m=Mon(pair.Item1,80,pair.Item2);Check(catalog.Species[m.Pokemon.Species].LevelUpMoves.Any(x=>x.Move==Id(pair.Item2)&&x.Level<=80),"authentic source level-up "+pair.Item1);
 }
});
Test("utility all actual actor positions and complete turns",()=>{
 foreach(var pair in new[]{("MISDREAVUS","PAIN_SPLIT"),("GRIMER","MEMENTO"),("MAGIKARP","SPLASH")})foreach(int actor in Enumerable.Range(0,4)){
  var f=Frame(actor,pair.Item1,pair.Item2);f=Put(f,actor,Hp(f.Slots[actor].Body!,Math.Max(1,f.Slots[actor].Body!.Pokemon.CurrentHp/3)));
  int target=pair.Item2=="SPLASH"?actor:actor^1;var action=Action(f,actor,target);
  Check(action.Trace.IsEmpty,"no utility RNG "+pair.Item2);Check(action.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(pair.Item2)].Pp-1,"one PP "+pair.Item2);
  if(pair.Item2=="PAIN_SPLIT"){
   int avg=(f.Slots[actor].Body!.Pokemon.CurrentHp+f.Slots[target].Body!.Pokemon.CurrentHp)/2;
   foreach(int i in new[]{actor,target})Check(action.State.Slots[i].Body!.Pokemon.CurrentHp==Math.Min(avg,f.Slots[i].Body!.Pokemon.Stats.MaxHp),"source average capped per body");
   Check(action.State.Slots[target].Registers.Damage is null,"Pain Split never becomes Counter receipt");
   Check(!action.Events.OfType<SourceCriticalHitEvent>().Any(),"no critical command");
  }
  if(pair.Item2=="MEMENTO")Check(action.State.Slots[actor].Body!.Pokemon.CurrentHp==0&&action.State.Slots[target].Body!.AttackStage==4&&action.State.Slots[target].Body!.SpecialAttackStageDelta==-2,"actual sacrifice and two stages");
  var done=Finish(First(f,actor,target));Check(done.CompletedCleanTurn,"whole utility turn "+pair.Item2);
 }
});
Test("utility guards, receipts, items and deferred reserve",()=>{
 var f=Frame(2,"MISDREAVUS","PAIN_SPLIT");f=Put(f,2,Hp(f.Slots[2].Body! with{HeldItem="ITEM_SHELL_BELL"},1));
 var doll=Put(f,3,f.Slots[3].Body! with{CalledVolatile=new(){SubstituteHp=7}});var rejected=Action(doll,2,3);
 Check(rejected.State.Slots[2].Body!.Pokemon.CurrentHp==1&&rejected.State.Slots[3].Body!.Pokemon.CurrentHp==f.Slots[3].Body!.Pokemon.CurrentHp&&rejected.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="substitute","Substitute blocks after PP without HP");
 var protectedTarget=Put(f,3,f.Slots[3].Body! with{Protected=true});var protectedResult=Action(protectedTarget,2,3);Check(protectedResult.State.Slots[2].Body!.Pokemon.CurrentHp==1&&protectedResult.Trace.IsEmpty,"Protect stops HP sharing without random draw");
 var pain=Action(f,2,3);Check(pain.State.Slots[2].Body!.HeldItem=="ITEM_SHELL_BELL"&&!pain.Events.OfType<SourceHeldItemHealEvent>().Any(),"target sentinel prevents false Shell Bell heal");
 foreach(int variant in Enumerable.Range(0,4)){
  var m=Frame(2,"GRIMER","MEMENTO");var foe=m.Slots[3].Body!;
  foe=variant switch{0=>foe with{AttackStage=0,SpecialAttackStageDelta=-6},1=>foe with{Protected=true,AttackStage=0,SpecialAttackStageDelta=-6},2=>foe with{CalledVolatile=new(){SubstituteHp=9}},_=>Mon("REGICE",100,"AMNESIA")};m=Put(m,3,foe);var r=Action(m,2,3);
  Check((r.State.Slots[2].Body!.Pokemon.CurrentHp==0)==(variant!=0),"minimum fails but Protect/Substitute/Clear Body sacrifice");Check(r.State.Slots[3].Body!.AttackStage==foe.AttackStage&&r.State.Slots[3].Body!.SpecialAttackStageDelta==foe.SpecialAttackStageDelta,"blocked stats remain");
 }
 var reserve=Frame(2,"GRIMER","MEMENTO");var party=reserve.Party(BattleSide.Player);reserve=reserve with{Parties=reserve.Parties.SetItem(0,party with{Members=party.Members.Add(Member(Mon("SNORLAX",100,"DEFENSE_CURL")))}),Rewards=reserve.Rewards! with{Metadata=reserve.Rewards!.Metadata.Add(new(new(BattleSide.Player,2),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected))}};
 var first=First(reserve,2,3);Check(first.State.Slots[2].Member==new PartyMemberKey(BattleSide.Player,1),"self KO retains original member during remaining actions");var final=Finish(first);Check(final.State.Slots[2].Member==new PartyMemberKey(BattleSide.Player,2)&&final.State.Slots[2].EntrySerial==2,"reserve enters only at deferred boundary");
 var stale=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[2].Occupant!.Value,f.Revision+1,0,new(3)));Check(!stale.Succeeded&&ReferenceEquals(stale.State,f)&&stale.Events.IsEmpty&&stale.Trace.IsEmpty,"stale command atomic");
});
var donors=new[]{("GROWLITHE","TAKE_DOWN"),("MACHAMP","SUBMISSION"),("SNORLAX","DOUBLE_EDGE"),("PIKACHU","VOLT_TACKLE"),("HITMONLEE","JUMP_KICK"),("HITMONLEE","HI_JUMP_KICK")};
Test("180 exact original C arithmetic vectors",()=>{
 using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));
 var type=typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecoilRules")!;int count=0;
 foreach(var row in doc.RootElement.EnumerateArray()){int kind=row.GetProperty("kind").GetInt32(),damage=row.GetProperty("damage").GetInt32(),hp=row.GetProperty("targetMaxHp").GetInt32();var method=type.GetMethod(kind==2?"Crash":"Recoil",BindingFlags.NonPublic|BindingFlags.Static)!;int result=(int)method.Invoke(null,[damage,kind==2?hp:kind==0?4:3])!;Check(result==row.GetProperty("expected").GetInt32(),"source scalar "+row);count++;}Check(count==180,"all C vectors consumed");
});
Test("all six exact moves and all twelve actor-target pairs include allies",()=>{
 foreach(var (species,move) in donors)foreach(int actor in Enumerable.Range(0,4))foreach(int target in Enumerable.Range(0,4).Where(t=>t!=actor)){
  var f=Frame(actor,species,move);var r=Action(f,actor,target);var hp=r.Events.OfType<SourceHpChangedEvent>().Single();Check(hp.Actor.Value==actor&&hp.Target.Value==target,"real selected slots");
  bool crash=catalog.Moves[Id(move)].Effect=="EFFECT_RECOIL_IF_MISS";var recoil=r.Events.OfType<SourceRecoilEvent>().ToArray();Check(recoil.Length==(crash?0:1),"successful hit recoil family");
  if(!crash)Check(recoil[0].Damage==Math.Max(1,(hp.HpBefore-hp.HpAfter)/(move is "TAKE_DOWN" or "SUBMISSION"?4:3)),"actual HP divisor");
  Check(r.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(crash?new[]{"accuracy","critical","damage","secondary-effect"}:new[]{"accuracy","critical","damage"}),"exact hit RNG order "+move);
  Check(r.Events.OfType<SourcePpCommandEvent>().Single().RngCursor==1&&r.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(move)].Pp-1,"accuracy then actual selected PP owner");
  Check(r.State.Slots.Where((_,i)=>i!=actor&&i!=target).Select(c=>Json(c.Body)).SequenceEqual(f.Slots.Where((_,i)=>i!=actor&&i!=target).Select(c=>Json(c.Body))),"unrelated bodies untouched");
 }
 foreach(var (species,move) in donors)foreach(int actor in Enumerable.Range(0,4))Finish(First(Frame(actor,species,move),actor,actor^2));
});
Test("capped body and doll loss, Endure zero, Focus Band and Rock Head",()=>{
 foreach(var (species,move) in donors.Take(4))foreach(bool doll in new[]{false,true}){
  var f=Frame(2,species,move);f=Put(f,3,doll?f.Slots[3].Body! with{CalledVolatile=new(){SubstituteHp=1}}:Hp(f.Slots[3].Body!,1));var r=Action(f,2);Check(r.Events.OfType<SourceRecoilEvent>().Single().Damage==1,"recoil uses actual one HP body/doll loss");
  if(doll)Check(r.State.Slots[3].Body!.Pokemon.CurrentHp==f.Slots[3].Body!.Pokemon.CurrentHp,"doll protects body");
 }
 foreach(var (species,move) in donors.Take(4)){
  var f=Frame(2,species,move);f=Put(f,3,Hp(f.Slots[3].Body! with{Enduring=true},1));var r=Action(f,2);Check(r.Events.OfType<SourceHpChangedEvent>().Single().Damage==0&&r.Events.OfType<SourceRecoilEvent>().Single().Damage==1,"effective zero still minimum-one recoil");
  var band=Frame(2,species,move);band=Put(band,3,Hp(band.Slots[3].Body! with{HeldItem="ITEM_FOCUS_BAND"},1));var cell=band.Slots[3];band=band with{Slots=band.Slots.SetItem(3,cell with{Position=cell.Position with{FocusBanded=true}})};var br=Action(band,2);Check(br.State.Slots[3].Body!.Pokemon.CurrentHp==1&&br.Events.OfType<SourceRecoilEvent>().Single().Damage==1,"sticky Focus Band leaves one HP");Check(br.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(new[]{"accuracy","critical","damage","focus-band"}),"Focus draw, no secondary");
 }
 foreach(string move in new[]{"TAKE_DOWN","DOUBLE_EDGE","SUBMISSION","VOLT_TACKLE"}){
  var f=Frame(2,"ONIX",move);Check(f.Slots[2].Body!.Pokemon.AbilityId=="ABILITY_ROCK_HEAD","natural Rock Head");var r=Action(f,2);Check(!r.Events.OfType<SourceRecoilEvent>().Any()&&r.Trace.Length==3,"Rock Head prevents recoil without secondary RNG");
 }
});
Test("ordinary misses, Protect, immunity and absorb retain source RNG branches",()=>{
 foreach(string move in names){
  var f=Frame(2,move.Contains("KICK")?"HITMONLEE":"SNORLAX",move);f=Put(f,2,f.Slots[2].Body! with{AccuracyStage=0});f=Put(f,3,f.Slots[3].Body! with{EvasionStage=12});var r=Action(f,2);bool crash=move.Contains("KICK");
  Check(r.Events.OfType<SourceMoveMissedEvent>().Any(),"fixed ordinary miss");Check(r.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(crash?new[]{"accuracy","damage"}:new[]{"accuracy"}),"ordinary miss RNG");Check(r.Events.OfType<SourceRecoilEvent>().Any()==crash,"only kicks crash");
  f=Put(f,3,f.Slots[3].Body! with{Protected=true,HeldItem="ITEM_FOCUS_BAND"});r=Action(f,2);Check(r.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(crash?new[]{"damage","focus-band"}:Array.Empty<string>()),"Protect miss RNG");
 }
 foreach(string move in new[]{"JUMP_KICK","HI_JUMP_KICK"})foreach(bool protect in new[]{false,true})foreach(bool inaccurate in new[]{false,true}){
  var f=Frame(2,"HITMONLEE",move);f=Put(f,3,Mon("GASTLY",100,"NIGHT_SHADE") with{Protected=protect,HeldItem="ITEM_FOCUS_BAND",EvasionStage=inaccurate?12:6});if(inaccurate)f=Put(f,2,f.Slots[2].Body! with{AccuracyStage=0});var r=Action(f,2);
  Check(!r.Events.OfType<SourceRecoilEvent>().Any()&&r.State.Slots[2].Body!.Pokemon.CurrentHp==f.Slots[2].Body!.Pokemon.CurrentHp,"Ghost cannot cause crash HP loss");
  string[] expected=protect?["damage","focus-band"]:inaccurate?["accuracy"]:["accuracy","critical","damage","focus-band","secondary-effect"];
  Check(r.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(expected),"Ghost miss/hit/protect exact RNG "+Json(r.Trace));
 }
 var electric=Frame(0,"PIKACHU","VOLT_TACKLE");electric=Put(electric,1,Hp(Mon("JOLTEON",100,"GROWL"),1));var absorbed=Action(electric,0);Check(absorbed.Trace.Length==1&&!absorbed.Events.OfType<SourceRecoilEvent>().Any()&&absorbed.Events.OfType<SourceRecoveredEvent>().Any(),"Volt Absorb precedes crit/damage/recoil");
});
Test("all three semi-invulnerable flags reach crash calculation even with Ghost immunity",()=>{
 foreach(string charge in new[]{"FLY","DIG","DIVE"})foreach(bool ghost in new[]{false,true})foreach(string kick in new[]{"JUMP_KICK","HI_JUMP_KICK"}){
  var f=Frame(2,"HITMONLEE",kick);f=Put(f,3,Mon(ghost?"GASTLY":"CHARIZARD",100,charge) with{HeldItem="ITEM_FOCUS_BAND"});var charging=Action(f,3,2).State;var r=Action(charging,2);
  Check(r.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(new[]{"damage","focus-band"}),"semi miss skips accuracy but computes crash "+charge);Check(r.Events.OfType<SourceRecoilEvent>().Any()==!ghost,"late immunity suppresses self HP");
 }
});
Test("crash uses target-adjusted damage and target max HP; ignores own doll, Endure and Rock Head",()=>{
 foreach(string kick in new[]{"JUMP_KICK","HI_JUMP_KICK"}){
  var f=Frame(2,"HITMONLEE",kick);f=Put(f,2,f.Slots[2].Body! with{AccuracyStage=0});f=Put(f,3,Hp(f.Slots[3].Body! with{EvasionStage=12,Enduring=true},1));var r=Action(f,2);Check(r.Events.OfType<SourceDamageAdjustedEvent>().Single().DamageAfter==0&&r.Events.OfType<SourceRecoilEvent>().Single().Damage==1,"target Endure adjustment before crash minimum");
  var rock=Frame(2,"ONIX",kick);rock=Put(rock,2,Hp(rock.Slots[2].Body! with{Enduring=true,CalledVolatile=new(){SubstituteHp=1}},1));rock=Put(rock,3,rock.Slots[3].Body! with{Protected=true});var ko=Action(rock,2);Check(ko.Events.OfType<SourceRecoilEvent>().Single().HpAfter==0&&ko.Events.OfType<SourceFaintedEvent>().Single().Side.Value==2,"Rock Head/own doll/own Endure cannot prevent crash KO");
 }
});
Test("recoil precedes contact and held items; crash is recorded without move-end healing",()=>{
 foreach(bool lethal in new[]{false,true}){
  var f=Frame(2,"SNORLAX","DOUBLE_EDGE");f=Put(f,2,Hp(f.Slots[2].Body! with{HeldItem="ITEM_SHELL_BELL"},lethal?1:200));f=Put(f,3,Mon("CARVANHA",100,"LEER") with{SpeedStage=0});var r=First(f,2);var recoil=r.Events.OfType<SourceRecoilEvent>().Single();
  Check(r.Events.OfType<SourceAbilityDamageEvent>().Any()==!lethal,"recoil KO suppresses Rough Skin");Check(r.Events.OfType<SourceHeldItemHealEvent>().Any()==!lethal,"recoil KO suppresses Shell Bell");if(!lethal){var contact=r.Events.OfType<SourceAbilityDamageEvent>().Single();var heal=r.Events.OfType<SourceHeldItemHealEvent>().Single();Check(recoil.Sequence<contact.Sequence&&contact.Sequence<heal.Sequence,"recoil then contact then holder heal");}Finish(r);
 }
 var crash=Frame(2,"HITMONLEE","JUMP_KICK");crash=Put(crash,2,Hp(crash.Slots[2].Body! with{HeldItem="ITEM_SHELL_BELL"},100));crash=Put(crash,3,crash.Slots[3].Body! with{Protected=true});var cr=Action(crash,2);var loss=cr.Events.OfType<SourceRecoilEvent>().Single();var receipt=cr.State.Slots[2].Registers.Damage;Check(receipt?.PhysicalSource?.Value==3&&receipt.PhysicalDamage==loss.HpBefore-loss.HpAfter,"nonpassive crash records actual target source for Counter");Check(!cr.Events.OfType<SourceHeldItemHealEvent>().Any(),"restored MISSED gates Shell Bell");
 var passive=Action(Frame(2,"SNORLAX","DOUBLE_EDGE"),2);Check(passive.State.Slots[2].Registers.Damage is null,"ordinary recoil never creates Counter receipt");
});
Test("King's Rock reads original target receipts after recoil for every actor slot",()=>{
 foreach(int actor in Enumerable.Range(0,4))foreach(bool rockHead in new[]{false,true}){
  var f=Frame(actor,rockHead?"ONIX":"SNORLAX","DOUBLE_EDGE",38);f=Put(f,actor,f.Slots[actor].Body! with{HeldItem="ITEM_KINGS_ROCK"});var r=First(f,actor);
  Check(r.Trace.Count(t=>t.Purpose.Contains("kings-rock"))==1,"one actual attacker item draw after certain recoil");Check(r.Events.OfType<SourceRecoilEvent>().Any()==!rockHead,"Rock Head retains item processing");Finish(r);
 }
 foreach(int actor in Enumerable.Range(0,4)){
  var f=Frame(actor,"HITMONLEE","JUMP_KICK");f=Put(f,actor,f.Slots[actor].Body! with{HeldItem="ITEM_KINGS_ROCK"});f=Put(f,actor^1,f.Slots[actor^1].Body! with{Protected=true});var r=First(f,actor);
  Check(r.Events.OfType<SourceRecoilEvent>().Any()&&!r.Trace.Any(t=>t.Purpose.Contains("kings-rock")),"restored MISSED blocks crash King's Rock");Finish(r);
 }
});
Test("canonical singles uses shared crash state and semi-invulnerability commands",()=>{
 foreach(string charge in new[]{"FLY","DIG","DIVE"})foreach(string kick in new[]{"JUMP_KICK","HI_JUMP_KICK"}){
  var actor=Mon("CLEFAIRY",80,"METRONOME");var target=Mon("GASTLY",100,"NIGHT_SHADE") with{HeldItem="ITEM_FOCUS_BAND",CalledTurnState=new(){Lock=new(WildBattleCalledLockKind.Charge,Id(charge),0,Id("NIGHT_SHADE"),WildBattleSide.Player,1)}};
  var state=new WildBattleState(actor,target,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
  var r=WildBattleRules.ResolveCalledMoveAction(catalog,state,new(WildBattleSide.Player,0,Id(kick)));Check(r.Succeeded,"canonical semi crash "+r.Fault);Check(r.Battle.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(new[]{"called-target","damage","focus-band"}),"canonical all-semi immunity retains crash RNG "+charge+" "+Json(r.Battle.Trace));Check(!r.Battle.Events.OfType<WildBattleRecoilEvent>().Any(),"late Ghost immunity prevents crash HP mutation");
 }
 foreach(var (species,move) in donors){var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon(species,80,move)],0,Mon("SNORLAX",100,"DEFENSE_CURL"),new(31));Check(start.Succeeded,"authentic complete singles start "+start.Fault);var r=WildBattleRules.ResolveMove(catalog,start.State!,0);Check(r.Succeeded&&r.State!.Turn>start.State!.Turn,"complete canonical singles turn "+r.Fault);}
});
Test("real simultaneous actor/target KO settles rewards then deferred replacement with JSON replay",()=>{
 var f=Frame(2,"SNORLAX","DOUBLE_EDGE");f=Put(f,2,Hp(f.Slots[2].Body!,1));f=Put(f,3,Hp(f.Slots[3].Body!,1));
 f=f with{Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,3).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()},Parties=f.Parties.Select(p=>p with{Members=p.Members.Add(Member(Mon("SNORLAX",100,"DEFENSE_CURL")))}).ToImmutableArray()};
 var first=First(f,2);Check(first.Events.OfType<SourceFaintedEvent>().Select(e=>e.Side.Value).SequenceEqual(new[]{2,3}),"attacker recoil faint precedes target faint");Check(first.State.FourTurn?.Phase==SourceFourActionPhase.RewardsPending,"source rewards pause after real KO");var completed=Finish(first);Check(completed.State.Slots[2].Member?.Index==2&&completed.State.Slots[3].Member?.Index==2,"both real reserves enter only at end of turn");Check(completed.State.Rewards!.CompletedOpponentMask==2,"one opponent pays once");
});
Test("unsupported neighbors and stale requests stay atomically gated",()=>{
 foreach(string name in new[]{"PRESENT","TRIPLE_KICK","METRONOME","UPROAR"})Check(!SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(name)]),"neighbor stays gated "+name);
 var f=Frame(2,"SNORLAX","DOUBLE_EDGE");var bad=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[2].Occupant!.Value,f.Revision+1,0,new(3)));Check(!bad.Succeeded&&ReferenceEquals(f,bad.State)&&bad.Events.IsEmpty&&bad.Trace.IsEmpty,"stale request atomic");
});
File.WriteAllText(args[1]+".turns.json",Json(turnEvidence));
File.WriteAllText(args[1],Json(new{runtime=Environment.Version.ToString(),groups,checks,failures,actualTurns,utilityOnly,utilityBaseline=utilitySnapshots.Count,recipeAdmission=catalog.Moves.Values.Count(SourceBattleKernel.SupportsRecipe),admittedMoves=catalog.Moves.Values.Where(SourceBattleKernel.SupportsRecipe).Select(m=>m.Id.Value).Order().ToArray(),results}));return failures==0?0:1;
