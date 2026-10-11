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

if(args.Length is not (3 or 4)) throw new ArgumentException("CONTENT_ROOT OUTPUT_DIRECTORY capture|verify [SOURCE_EVIDENCE]");
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
string Json<T>(T x)=>JsonSerializer.Serialize(x);
string Hash<T>(T x)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(x))));
MoveId Id(string name)=>new("pret:MOVE_"+name);
const string Rock="ITEM_KINGS_ROCK",Bell="ITEM_SHELL_BELL";
WildBattleCombatant Mon(string species,int level,string item,params string[] names){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{Moves=names.Select(n=>new PokemonMoveSlot(Id(n),catalog.Moves[Id(n)].Pp,catalog.Moves[Id(n)].Pp)).ToImmutableArray()},HeldItem:item);
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
var goldens=new List<object>();
foreach(var recipe in new[]{("PIDGEY","TACKLE"),("SPEAROW","FURY_ATTACK"),("LAPRAS","SURF"),("DEWGONG","ICE_BEAM"),("SNORLAX","BELLY_DRUM"),("SNORLAX","SUBSTITUTE"),("EEVEE","QUICK_ATTACK"),("ABRA","SEISMIC_TOSS")})
foreach(string item in new[]{Rock,Bell})foreach(bool opponent in new[]{false,true})foreach(uint seed in new uint[]{0,31,38}){
 var actor=Mon(recipe.Item1,50,item,recipe.Item2);actor=Hp(actor,actor.Pokemon.CurrentHp*3/4);
 var defender=Mon("SNORLAX",100,"ITEM_NONE","DEFENSE_CURL");
 var started=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[opponent?defender:actor],0,opponent?actor:defender,new(seed));
 if(!started.Succeeded)throw new Exception("canonical start "+started.Fault);
 var result=WildBattleRules.ResolveMove(catalog,started.State!,0);if(!result.Succeeded)throw new Exception("canonical turn "+result.Fault);
 goldens.Add(new{species=recipe.Item1,move=recipe.Item2,item,opponent,seed,hash=Hash(result)});
}
var twoGoldens=new List<object>();
foreach(var pair in new[]{("NIDORAN_M","DOUBLE_KICK"),("MAROWAK","BONEMERANG"),("BEEDRILL","TWINEEDLE")})
foreach(uint seed in new uint[]{0,31,38,127})foreach(bool opponent in new[]{false,true}){
 var attacker=Mon(pair.Item1,50,Bell,pair.Item2);attacker=Hp(attacker,attacker.Pokemon.CurrentHp/2);var defender=Mon("SNORLAX",100,"ITEM_NONE","DEFENSE_CURL");
 var started=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[opponent?defender:attacker],0,opponent?attacker:defender,new(seed));if(!started.Succeeded)throw new Exception("fixed-two canonical start "+started.Fault);
 var result=WildBattleRules.ResolveMove(catalog,started.State!,0);if(!result.Succeeded)throw new Exception("fixed-two canonical action "+result.Fault);
 twoGoldens.Add(new{move=pair.Item2,seed,opponent,hash=Hash(result)});
}
if(args[2]=="capture-two"){
 var path=Path.Combine(output,"fixed-two-singles-before.json");if(File.Exists(path))throw new Exception("Refuse golden overwrite");
 File.WriteAllText(path,Json(new{baseCommit="db8579a28734e83ead8c3fa77bd57825a84370d3",twoGoldens})+"\n");return 0;
}
bool fixedTwoOnly=args[2]=="fixed-two";
var goldenPath=Path.Combine(AppContext.BaseDirectory,"singles-before.json");
if(args[2]=="capture"){
 if(typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.FrameRecipeContext")!.GetMethod("ApplyFrameHitItems",BindingFlags.Instance|BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unmodified frame source");
 var path=Path.Combine(output,"singles-before.json");if(File.Exists(path))throw new Exception("Never overwrite frozen golden");
 File.WriteAllText(path,Json(new{baseline="242275ce63a8c1241e5a1ffd068b651141890fc1 + shared dynamic/freeze/recharge",goldens})+"\n");
 Console.WriteLine("CAPTURED "+goldens.Count+" complete canonical result hashes");return 0;
}
int groups=0,checks=0,failures=0,actualActions=0,completeTurns=0,sourceVectors=0;var report=new List<object>();var actionEvidence=new List<object>();
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
void Test(string label,Action body){if(fixedTwoOnly&&!label.StartsWith("fixed-two"))return;groups++;try{body();report.Add(new{label,passed=true});Console.WriteLine("PASS "+label);}catch(Exception ex){failures++;report.Add(new{label,passed=false,error=ex.ToString()});Console.WriteLine("FAIL "+label+" "+ex);}}
Test("unchanged complete canonical singles results",()=>{
 using var old=JsonDocument.Parse(File.ReadAllText(goldenPath));Check(goldens.Count==96,"nonempty frozen corpus");
 Check(Json(old.RootElement.GetProperty("goldens"))==Json(goldens),"all 96 serialized state/event/RNG result hashes unchanged");
});

WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(int actor,string species,string move,string item,uint seed=31) {
 var mons=Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,"ITEM_NONE","DEFENSE_CURL") with{SpeedStage=i}).ToArray();
 mons[actor]=Mon(species,50,item,move) with{SpeedStage=12};
 if(item==Bell)mons[actor]=Hp(mons[actor],mons[actor].Pokemon.CurrentHp/2);
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
  BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray()};
}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var c=f.Slots[slot];var k=c.Member!.Value;var party=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=m}),Parties=f.Parties.SetItem((int)k.Side,party with{Members=party.Members.SetItem(k.Index,Member(m))})};}
SourceBattleFrame Receipt(SourceBattleFrame f,int slot,SourceHitDamageReceipt r){var c=f.Slots[slot];return f with{Slots=f.Slots.SetItem(slot,c with{Position=c.Position with{HitItemReceipt=r}})};}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int actor,int? target=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{
 var m=catalog.Moves[c.Body!.Pokemon.Moves[0].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(m.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value==actor?target??(actor^1):c.Slot.Value^1));}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,"accepted: "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid result: "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult First(SourceBattleFrame f,int actor,int? target=null){var start=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,actor,target)));Check(start.State.Queue[0].Slot.Value==actor,"fixture actor really moves first");var result=Ok(SourceFourActionTurns.Advance(catalog,start.State,start.Continuation!.Value));actualActions++;actionEvidence.Add(new{actor,move=f.Slots[actor].Body!.Pokemon.Moves[0].Move,item=f.Slots[actor].Body!.HeldItem,inputHash=Hash(f),resultHash=Hash(result),result.Events,result.Trace});return result;}
SourceFourActionResult Finish(SourceFourActionResult r){int steps=0;while(r.State.FourTurn is not null){Check(++steps<30,"bounded turn completion");Check(r.State.FourTurn.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume,"no unhandled external wait");var json=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var expected=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var replay=SourceFourActionTurns.Advance(catalog,json,r.Continuation.Value);Check(Json(expected)==Json(replay),"continuation survives JSON exactly");r=Ok(expected);}Check(r.CompletedCleanTurn,"finished real four-action turn");completeTurns++;return r;}
RandomTrace[] Rocks(SourceFourActionResult r)=>r.Trace.Where(t=>t.Purpose.Contains("kings-rock")).ToArray();
SourceHeldItemHealEvent[] Heals(SourceFourActionResult r)=>r.Events.OfType<SourceHeldItemHealEvent>().ToArray();
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string label)=>Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,label+" atomic: "+r.Fault);
Test("positive battle fixtures use natural learned moves or authenticated TM/tutor entitlements",()=>{
 foreach(var pair in new[]{("PIDGEY","TACKLE"),("SPEAROW","FURY_ATTACK"),("SNORLAX","BELLY_DRUM"),("SNORLAX","DEFENSE_CURL"),("EEVEE","QUICK_ATTACK"),("CATERPIE","STRING_SHOT"),("ZUBAT","CONFUSE_RAY"),("WEEDLE","POISON_STING"),("SCYTHER","FALSE_SWIPE"),("GENGAR","CONFUSE_RAY"),("CARVANHA","LEER"),("LAPRAS","GROWL")})
  Check(catalog.Species[new("pret:SPECIES_"+pair.Item1)].LevelUpMoves.Any(m=>m.Move==Id(pair.Item2)&&m.Level<=50),"authentic level-up fixture "+pair);
 using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(args[3],"source-report.json")));Check(source.RootElement.GetProperty("fixtureEntitlementSources").EnumerateObject().Count()==2,"source-authenticated Surf/Substitute/Seismic Toss/Ice Beam/Explosion TM/tutor tables");
});
Test("four-position corrected C agrees with shared item formulas",()=>{
 Check(args.Length==4,"authenticated source evidence required");using var vectors=JsonDocument.Parse(File.ReadAllText(Path.Combine(args[3],"four-slot-vectors.json")));
 var type=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.SourceHitItemRules")!;
 var draw=type.GetMethod("KingsRockDraw",BindingFlags.NonPublic|BindingFlags.Static)!;var proc=type.GetMethod("KingsRockProc",BindingFlags.NonPublic|BindingFlags.Static)!;var heal=type.GetMethod("ShellBellHeal",BindingFlags.NonPublic|BindingFlags.Static)!;
 foreach(var row in vectors.RootElement.EnumerateArray()){
  var i=row.GetProperty("input");var e=row.GetProperty("expected");int V(string k)=>i.GetProperty(k).GetInt32();int E(string k)=>e.GetProperty(k).GetInt32();
  var receipt=new SourceHitDamageReceipt(V("receipt"),V("physicalDmg"),V("specialDmg"));
  if(V("item")==187){bool drawn=(bool)draw.Invoke(null,[V("damage"),false,receipt])!;bool flinch=drawn&&(bool)proc.Invoke(null,[(ushort)V("draw"),10,true,V("targetHp")])!;Check(drawn==(E("drawCount")==1)&&flinch==(E("flinch")==1),"corrected C King's Rock "+row.GetProperty("name"));}
  else{int amount=(int)heal.Invoke(null,[V("damage"),false,receipt,false,V("attackerHp"),V("maxHp")])!;Check(Math.Min(V("maxHp"),V("attackerHp")+amount)==E("attackerHp"),"original Gen III Shell Bell "+row.GetProperty("name"));}
  sourceVectors++;
 }Check(sourceVectors==72,"all 72 original/corrected command vectors consumed");
});
Test("ineligible flag and ordinary secondary flinch guards follow the item draw",()=>{
 foreach(string species in new[]{"CATERPIE","ZUBAT"})foreach(uint seed in new uint[]{0,31,38,127}){
  var f=Frame(2,"PIDGEY","TACKLE",Rock,seed);f=Put(f,3,Mon(species,100,"ITEM_NONE",species=="CATERPIE"?"STRING_SHOT":"CONFUSE_RAY") with{SpeedStage=0});var r=First(f,2);
  Check(Rocks(r).Length==1&&!r.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied),"Shield Dust/Inner Focus guard occurs after draw");
 }
 var ineligible=Frame(2,"WEEDLE","POISON_STING",Rock);Check(!catalog.Moves[Id("POISON_STING")].Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"),"source ineligible move flag");var result=First(ineligible,2);Check(Rocks(result).Length==1&&!result.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied),"ineligible flag follows draw");
});
Test("actual holder in all four slots uses corrected strict ten-percent roll",()=>{
 int successes=0;
 foreach(int actor in Enumerable.Range(0,4))foreach(uint seed in new uint[]{0,31,38,127}){
  var f=Frame(actor,"PIDGEY","TACKLE",Rock,seed);var r=First(f,actor);var draw=Rocks(r).Single();
  bool expected=draw.Raw%100<10;Check(r.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied)==expected,"corrected attacker parameter at slot "+actor);if(expected)successes++;
  Check(draw.Purpose=="wild-battle.slot"+actor+".kings-rock","actual slot RNG owner");
  Check(r.Trace.Last()==draw,"item chance follows normal hit secondary RNG");
  Check(r.State.Slots[actor].Body!.HeldItem==Rock&&r.State.Party((BattleSide)(actor&1)).Members[actor/2].HeldItem==Rock,"nonconsumption and real party member");
  Check(r.State.Slots.All(c=>c.Position.HitItemReceipt is null),"ActionFinished clears all receipts");Finish(r);
 }Check(successes>0,"positive flinch witnesses exist");
});
Test("Shell Bell actual capped HP loss, all holders, allies and full HP",()=>{
 foreach(int actor in Enumerable.Range(0,4))foreach(bool ally in new[]{false,true}){
  var f=Frame(actor,"PIDGEY","TACKLE",Bell);int target=ally?actor^2:actor^1;var r=First(f,actor,target);
  var hit=r.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Actor.Value==actor&&e.Target.Value==target);var heal=Heals(r).Single();
  Check(heal.Actor.Value==actor&&heal.Target.Value==target&&heal.DamageReceipt==hit.HpBefore-hit.HpAfter,"actual position and capped receipt");
  Check(heal.HpRestored==Math.Max(1,(hit.HpBefore-hit.HpAfter)/8),"Gen III divisor and minimum");
  Check(r.State.Party((BattleSide)(actor&1)).Members[actor/2].Pokemon.CurrentHp==heal.HpAfter,"exact holder party HP synchronized");Finish(r);
 }
 var full=Frame(2,"PIDGEY","TACKLE",Bell);full=Put(full,2,Hp(full.Slots[2].Body!,full.Slots[2].Body!.Pokemon.Stats.MaxHp));Check(Heals(First(full,2)).Length==0,"full holder does not heal");
 var capped=Frame(3,"PIDGEY","TACKLE",Bell);capped=Put(capped,3,Hp(capped.Slots[3].Body!,capped.Slots[3].Body!.Pokemon.Stats.MaxHp-1));Check(Heals(First(capped,3)).Single().HpRestored==1,"healing capped at max HP");
});
Test("spread targets keep independent source passes and one PP charge",()=>{
 foreach(string item in new[]{Rock,Bell})foreach(int actor in Enumerable.Range(0,4)){
  var f=Frame(actor,"LAPRAS","SURF",item);var r=First(f,actor);var targets=new[]{actor^1,actor^3};
  var hits=r.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Actor.Value==actor&&e.Actor!=e.Target).ToArray();Check(hits.Select(e=>e.Target.Value).SequenceEqual(targets),"real opposing target pass order");
  Check(r.Events.OfType<SourcePpCommandEvent>().Select(e=>e.Before-e.After).SequenceEqual(new[]{1,0}),"spread once-only PP");
  if(item==Rock)Check(Rocks(r).Length==2,"one eligible body receipt chance per spread pass");
  else Check(Heals(r).Select(e=>e.DamageReceipt).SequenceEqual(hits.Select(e=>e.HpBefore-e.HpAfter)),"each spread recipient provides its own receipt");Finish(r);
 }
 var protect=Frame(2,"LAPRAS","SURF",Rock);protect=Put(protect,3,protect.Slots[3].Body! with{Protected=true});var blocked=First(protect,2);
 Check(Rocks(blocked).Length==1&&blocked.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="protected"&&e.Target.Value==3),"first protected pass does not contaminate successful second pass");
 var immune=Frame(2,"LAPRAS","SURF",Bell);immune=Put(immune,1,Mon("LAPRAS",100,"ITEM_NONE","GROWL") with{SpeedStage=0});var absorbed=First(immune,2);
 Check(Heals(absorbed).Length==1&&Heals(absorbed)[0].Target.Value==3,"second absorbed pass cannot reuse first target command or receipt");
});
Test("ordinary multihit per-hit and final visits; substitute break lifetime",()=>{
 foreach(string item in new[]{Rock,Bell})foreach(bool substitute in new[]{false,true}){
  var f=Frame(2,"SPEAROW","FURY_ATTACK",item,38);if(substitute)f=Put(f,3,f.Slots[3].Body! with{CalledVolatile=new(){SubstituteHp=1}});
  var r=First(f,2);var summary=r.Events.OfType<SourceMultiHitEvent>().Single();Check(summary.ActualHits>=2,"nonempty multihit");
  var body=r.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Target.Value==3).ToArray();
  if(item==Rock)Check(Rocks(r).Length==body.Length+1,"body hits plus final item chance; first substitute no body chance");
  else{var heals=Heals(r);Check(heals.Length==summary.ActualHits,"one heal per actual hit, no duplicate final heal");if(substitute)Check(heals[0].DamageReceipt==1&&heals[0].HpRestored==1,"breaking substitute records actual loss and minimum heal");}
  Check(r.State.Slots.All(c=>c.Position.HitItemReceipt is null),"only ActionFinished clears receipts");Finish(r);
 }
 var sub=Frame(0,"PIDGEY","TACKLE",Rock);sub=Put(sub,1,sub.Slots[1].Body! with{CalledVolatile=new(){SubstituteHp=1}});sub=Receipt(sub,1,new(0xFFFF,0,8));var retained=First(sub,0);
 Check(Rocks(retained).Length==1&&!retained.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied),"retained special receipt draws but broken substitute still blocks before cleanup");
});
Test("zero-loss, immunity, stale receipt, self cost and self damage guards",()=>{
 foreach(string item in new[]{Rock,Bell}){
  var zero=Frame(0,"SCYTHER","FALSE_SWIPE",item);zero=Put(zero,1,Hp(zero.Slots[1].Body!,1));zero=Receipt(zero,1,new(16,16));var z=First(zero,0);
  Check(z.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Target.Value==1&&e.Damage==0)&&Rocks(z).Length==0&&Heals(z).Length==0,"zero command damage suppresses retained receipt");
  var immune=Frame(0,"PIDGEY","TACKLE",item);immune=Put(immune,1,Mon("GENGAR",100,"ITEM_NONE","CONFUSE_RAY") with{SpeedStage=0});immune=Receipt(immune,1,new(16,16));var i=First(immune,0);Check(Rocks(i).Length==0&&Heals(i).Length==0,"immunity gates stale damage");
  var self=Frame(2,"SNORLAX","SUBSTITUTE",item);var s=First(self,2);Check(Rocks(s).Length==(item==Rock?1:0)&&Heals(s).Length==0,"self cost draws before ineligible flag and never heals");
  var confusion=Frame(2,"PIDGEY","TACKLE",item,0);confusion=Put(confusion,2,confusion.Slots[2].Body! with{ConfusionTurnsRemaining=3});confusion=Receipt(confusion,2,new(8,8));var c=First(confusion,2);
  Check(c.Events.OfType<SourceConfusionSelfHitEvent>().Any(),"fixed confusion self-hit witness");Check(Heals(c).Length==0,"confusion redirects item target to holder");
 }
 var sentinel=Frame(2,"PIDGEY","TACKLE",Bell);sentinel=Receipt(sentinel,3,new(0xFFFF,5));Check(Heals(First(sentinel,2)).Length==0,"FFFF sentinel survives fresh dataHP and blocks bell");
 var first=Frame(2,"PIDGEY","TACKLE",Bell);first=Receipt(first,3,new(8,8));Check(Heals(First(first,2)).Single().DamageReceipt==8,"first nonzero receipt retained, never summed or replaced");
});
Test("KO and contact command order preserve real item actor",()=>{
 foreach(string item in new[]{Rock,Bell}){
  var f=Frame(2,"PIDGEY","TACKLE",item);f=Put(f,3,Hp(f.Slots[3].Body!,1));var r=First(f,2);
  Check(r.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==3),"target actual KO");
  if(item==Rock)Check(Rocks(r).Length==1&&!r.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied),"KO draws then HP rejects");
  else Check(Heals(r).Single().DamageReceipt==1&&Heals(r).Single().HpRestored==1,"KO heals only actual one HP lost");
 }
 foreach(bool lethal in new[]{false,true}){
  var f=Frame(2,"PIDGEY","TACKLE",Bell);f=Put(f,3,Mon("CARVANHA",100,"ITEM_NONE","LEER") with{SpeedStage=0});if(lethal)f=Put(f,2,Hp(f.Slots[2].Body!,1));var r=First(f,2);
  var contact=r.Events.OfType<SourceAbilityDamageEvent>().Single();Check(contact.Holder.Value==3&&contact.Target.Value==2,"Rough Skin actual holder and attacker");
  Check(Heals(r).Length==(lethal?0:1),"contact KO blocks healing");if(!lethal)Check(contact.Sequence<Heals(r)[0].Sequence,"contact precedes Shell Bell");
 }
});
Test("Explosion retains dead attacker ownership across all three target passes",()=>{
 foreach(string item in new[]{Rock,Bell}){
  var f=Frame(2,"GOLEM","EXPLOSION",item);var r=First(f,2);
  Check(r.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Actor.Value==2).Select(e=>e.Target.Value).SequenceEqual(new[]{0,1,3}),"numeric source target passes include ally");
  Check(r.State.Slots[2].Body!.Pokemon.CurrentHp==0&&r.Events.OfType<SourceFaintedEvent>().Last().Side.Value==2,"source actor faint after passes");
  Check(Rocks(r).Length==(item==Rock?3:0)&&Heals(r).Length==0,"King retains source draws for dead holder; dead holder Shell Bell cannot heal");
 }
});
Test("KO receipt settles once before rewards and replacement without leaking to entrant",()=>{
 var f=Frame(2,"PIDGEY","TACKLE",Bell);f=f with{Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};f=Put(f,3,Hp(f.Slots[3].Body!,1));var party=f.Party(BattleSide.Opponent);
 f=f with{Parties=f.Parties.SetItem(1,party with{Members=party.Members.Add(Member(Mon("SNORLAX",100,Rock,"DEFENSE_CURL")))})};
 var r=First(f,2);var initialHeal=Heals(r).Single();Check(initialHeal.DamageReceipt==1,"last one HP is recorded before faint");
 Check(r.State.FourTurn?.Phase==SourceFourActionPhase.RewardsPending,"receipt and healing commit before real reward wait");
 bool replaced=false;int steps=0;var all=r.Events.ToList();
 while(r.State.FourTurn is not null){Check(++steps<40,"bounded reward/replacement flow");
  if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){var pending=r.State.PendingSwitches.Single();Check(pending.Slot.Value==3,"actual target replacement slot");r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,new(3),new(BattleSide.Opponent,2)));replaced=true;}
  else r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));all.AddRange(r.Events);
 }
 Check(replaced&&r.CompletedCleanTurn&&r.State.Slots[3].Member?.Index==2&&r.State.Slots[3].EntrySerial==2,"real reserve owner enters at end of turn");
 Check(r.State.Slots[3].Position.HitItemReceipt is null&&all.OfType<SourceHeldItemHealEvent>().Count()==1,"no duplicate healing or receipt inherited by replacement");
 Check(all.Zip(all.Skip(1)).All(p=>p.First.Sequence<p.Second.Sequence),"strict source event sequence across wait and entry");completeTurns++;
});
Test("berry move end precedes items and actor-flinch obeys true action order",()=>{
 var f=Frame(2,"PIDGEY","TACKLE",Bell);f=Put(f,3,Hp(f.Slots[3].Body! with{HeldItem="ITEM_CHERI_BERRY",Status=WildBattleStatus.Paralysis},80));var r=First(f,2);
 Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Sequence<Heals(r).Single().Sequence,"all-battler berry before attacker hit item");
 var slow=Frame(2,"PIDGEY","TACKLE",Rock,38);slow=Put(slow,2,slow.Slots[2].Body! with{SpeedStage=0});slow=Put(slow,3,slow.Slots[3].Body! with{SpeedStage=12});
 var begun=Ok(SourceFourActionTurns.BeginActions(catalog,slow,slow.Revision,Choices(slow,2)));SourceFourActionResult? attack=null;
 while(begun.State.FourTurn?.Phase==SourceFourActionPhase.Actions&&begun.State.NextAction<4){begun=Ok(SourceFourActionTurns.Advance(catalog,begun.State,begun.Continuation!.Value));if(Rocks(begun).Length>0)attack=begun;}
 Check(attack is not null&&Rocks(attack).Length==1&&!attack.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied),"already-acted target still draws but never flinches");
});
Test("delayed MOVE_NONE draws retain FFFF body receipt through Ready",()=>{
 foreach(string item in new[]{Rock,Bell}){
  var f=Frame(2,"SNORLAX","DEFENSE_CURL",item);var c=f.Slots[3];f=f with{Slots=f.Slots.SetItem(3,c with{Position=c.Position with{FutureAttack=new(1,Id("FUTURE_SIGHT"),new(2),16)}})};
  var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,2)));var events=new List<SourceBattleEvent>();var trace=new List<RandomTrace>();
  while(r.State.FourTurn is not null){r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));events.AddRange(r.Events);trace.AddRange(r.Trace);}
  Check(r.State.Slots[3].Position.HitItemReceipt is{Damage:65535,Physical:>0},"delayed physical MOVE_NONE receipt retained through Ready");
  Check(!events.OfType<SourceHeldItemHealEvent>().Any()&&trace.Count(t=>t.Purpose.Contains("kings-rock"))==(item==Rock?1:0),"MOVE_NONE no-flinch flag after draw, sentinel no heal");
  var next=First(r.State,2);Check(next.State.Slots.All(c=>c.Position.HitItemReceipt is null),"next action clears previous delayed receipts");
 }
});
Test("dependent recipes and Serene Grace are admitted while invalid order stays atomic",()=>{
 foreach(string move in new[]{"TRIPLE_KICK","BEAT_UP","THIEF","METRONOME","SLEEP_TALK","MIRROR_MOVE","PAIN_SPLIT"}){
  var f=Frame(2,"SMEARGLE",move,Rock);var admitted=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,2)));Check(admitted.State.Queue.Single(a=>a.Slot.Value==2).OriginalChosenMove==Id(move)&&admitted.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==f.Slots[2].Body!.Pokemon.Moves[0].CurrentPp,"recipe admission retains chosen move and unspent PP "+move);
 }
 foreach(string species in new[]{"TOGEPI","DUNSPARCE"}){var f=Frame(2,species,"TACKLE",Rock); var m=f.Slots[2].Body!;var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),50,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(species=="TOGEPI"?1u:0u)),new(0)).Pokemon;f=Put(f,2,m with{Pokemon=p with{Moves=m.Pokemon.Moves}});Check(WildBattleEffective.Ability(f.Slots[2].Body!)=="ABILITY_SERENE_GRACE","actual Serene Grace fixture");var admitted=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,2)));Check(WildBattleEffective.Ability(admitted.State.Slots[2].Body!)=="ABILITY_SERENE_GRACE","admission retains authentic Serene Grace");}
 var alone=Frame(2,"PIDGEY","TACKLE",Rock);var single=SourceBattleKernel.ResolveAction(catalog,alone,new(alone.Slots[2].Occupant!.Value,alone.Revision,0,new(3)));
 Check(!single.Succeeded&&ReferenceEquals(alone,single.State)&&single.Trace.IsEmpty&&single.Events.IsEmpty,"standalone King requires actual action owner before any RNG");
 var malformed=Receipt(alone,3,new(-1,2));Atomic(malformed,SourceFourActionTurns.BeginActions(catalog,malformed,malformed.Revision,Choices(malformed,2)),"negative receipt");
 var begun=Ok(SourceFourActionTurns.BeginActions(catalog,alone,alone.Revision,Choices(alone,2)));var done=Ok(SourceFourActionTurns.Advance(catalog,begun.State,begun.Continuation!.Value));Atomic(done.State,SourceFourActionTurns.Advance(catalog,done.State,begun.Continuation.Value),"stale continuation");
});
Test("fixed-two unchanged canonical complete results and authentic source donors",()=>{
 using var b=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixed-two-singles-before.json")));Check(Json(b.RootElement.GetProperty("twoGoldens"))==Json(twoGoldens),"24 frozen canonical full-result hashes unchanged");
 foreach(var pair in new[]{("NIDORAN_M","DOUBLE_KICK"),("MAROWAK","BONEMERANG"),("BEEDRILL","TWINEEDLE")})Check(catalog.Species[new("pret:SPECIES_"+pair.Item1)].LevelUpMoves.Any(m=>m.Move==Id(pair.Item2)&&m.Level<=50),"natural donor "+pair.Item1+"/"+pair.Item2);
});
Test("fixed-two real holder positions finish two hits with one PP and no count draw",()=>{
 foreach(var pair in new[]{("NIDORAN_M","DOUBLE_KICK"),("MAROWAK","BONEMERANG"),("BEEDRILL","TWINEEDLE")})foreach(int actor in Enumerable.Range(0,4)){
  var f=Frame(actor,pair.Item1,pair.Item2,Bell,31);var r=First(f,actor);var count=r.Events.OfType<SourceMultiHitEvent>().Single();
  Check(count.Actor.Value==actor&&count.Target.Value==(actor^1)&&count.PlannedHits==2&&count.ActualHits==2,"two distinct actual hit commands");Check(r.Events.OfType<SourceHpChangedEvent>().Count(e=>e.Actor.Value==actor&&e.Target.Value==(actor^1))==2,"two actual HP applications");
  Check(!r.Trace.Any(t=>t.Purpose.Contains("multi-hit-count")),"fixed count consumes no count RNG");Check(r.Trace.Count(t=>t.Purpose.Contains("secondary-effect"))==1,"one final secondary command");
  Check(r.State.Get(new(actor)).Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(pair.Item2)].Pp-1,"one PP expenditure");Check(Heals(r).Length==2,"per-hit Shell Bell move end");Finish(r);
 }
});
Test("fixed-two substitute break contact KO Endure immunity and item ownership",()=>{
 var f=Frame(2,"NIDORAN_M","DOUBLE_KICK",Rock,31);f=Put(f,3,f.Slots[3].Body! with{CalledVolatile=new(){SubstituteHp=1}});int hp=f.Slots[3].Body!.Pokemon.CurrentHp;var r=First(f,2);Check(r.Events.OfType<SourceMultiHitEvent>().Single().ActualHits==2&&r.State.Slots[3].Body!.Pokemon.CurrentHp<hp,"second hit reaches body after doll breaks");Check(Rocks(r).Length==2,"doll-only first hit has no physical receipt; body second hit and final visit draw");
 var endure=Frame(2,"NIDORAN_M","DOUBLE_KICK","ITEM_NONE",31);endure=Put(endure,3,Hp(endure.Slots[3].Body!,1) with{Enduring=true});var end=First(endure,2);Check(end.Events.OfType<SourceMultiHitEvent>().Single().ActualHits==1&&end.State.Slots[3].Body!.Pokemon.CurrentHp==1,"Endure ends loop after one command");
 var rough=Frame(2,"NIDORAN_M","DOUBLE_KICK","ITEM_NONE",31);rough=Put(rough,2,Hp(rough.Slots[2].Body!,1));rough=Put(rough,3,Mon("CARVANHA",100,"ITEM_NONE","LEER"));var recoil=First(rough,2);Check(recoil.Events.OfType<SourceMultiHitEvent>().Single().ActualHits==1&&recoil.State.Slots[2].Body!.Pokemon.CurrentHp==0,"contact holder KO stops second strike");
 var immune=Frame(2,"MAROWAK","BONEMERANG","ITEM_NONE",31);immune=Put(immune,3,Mon("PIDGEY",100,"ITEM_NONE","GROWL"));var no=First(immune,2);Check(no.Events.OfType<SourceMultiHitEvent>().Single().ActualHits==0&&!no.Trace.Any(t=>t.Purpose.EndsWith(".damage")),"immune fixed-two stops before damage multiplier draw");
 var bad=f with{EventSequence=long.MaxValue};Atomic(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad,2)),"fixed-two publication overflow");
});
File.WriteAllText(Path.Combine(output,"results.json"),Json(new{groups,checks,failures,runtime=Environment.Version.ToString(),canonicalHashes=goldens.Count,fixedTwoHashes=twoGoldens.Count,fixedTwoOnly,recipeAdmission=catalog.Moves.Values.Count(SourceBattleKernel.SupportsRecipe),admittedMoves=catalog.Moves.Values.Where(SourceBattleKernel.SupportsRecipe).Select(m=>m.Id.Value).Order().ToArray(),actualActions,completeTurns,sourceVectors,results=report})+"\n");
File.WriteAllText(Path.Combine(output,"actions.json"),Json(actionEvidence)+"\n");
Console.WriteLine(Json(new{groups,checks,failures,actualActions,completeTurns,sourceVectors,runtime=Environment.Version.ToString()}));
return failures==0&&(fixedTwoOnly?groups==3:groups>=10)?0:1;
