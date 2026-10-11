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
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(species is "GROWLITHE" or "GRIMER"?1u:0u)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
var original=PretRocketHideoutPokemon.Load(args[0]).Catalog;
var supplement=PretVermilionGymTrainers.ExtendPokemon(args[0],PretCeladonApproachPokemon.Load(args[0]).Catalog,celadonApproachRules:true);
var prior=original with{Moves=original.Moves.SetItems(supplement.Moves)};
var old=PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior,args[0],args[0]).Catalog,args[0]).Catalog,args[0]).Catalog;
var legacy=PretMetronomeMoveCatalog.Load(args[0]).Extend(PretFuchsiaRoutePokemon.Load(old,args[0]).Catalog);
var snapshots=new List<object>();
foreach(bool canonical in new[]{false,true})foreach(string move in new[]{"THIEF","COVET","TRICK","KNOCK_OFF","RECYCLE"})foreach(bool reverse in new[]{false,true}){
 var actor=Mon("CLEFAIRY",40,move);var target=Mon("SNORLAX",80,"DEFENSE_CURL") with{HeldItem="ITEM_ORAN_BERRY"};
 var player=reverse?target:actor;var opponent=reverse?actor:target;
 var state=new WildBattleState(player,opponent,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null,
 CalledParty=new([new(player.Pokemon,player.HeldItem)],[new(opponent.Pokemon,opponent.HeldItem)],0,0,WildBattleCalledBattleKind.Trainer){PlayerUsedHeldItem="ITEM_PERSIM_BERRY",OpponentUsedHeldItem="ITEM_PERSIM_BERRY"}};
 var r=WildBattleRules.ResolveSourceMoveAction(canonical?catalog:legacy,state,new(reverse?WildBattleSide.Opponent:WildBattleSide.Player,0));
 if(!r.Succeeded)throw new Exception($"baseline {canonical}/{move}/{reverse}: "+r.Fault);
 snapshots.Add(new{canonical,move,reverse,sha256=Hash(r)});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("Refuse baseline overwrite");
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasItemTransferRecipe",BindingFlags.Static|BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unchanged a51e8a6 product");
 File.WriteAllText(args[1],Json(new{baseCommit="a51e8a633b36739e398e2e3ef95bab32f15d6a72",snapshots}));Console.WriteLine("Captured "+snapshots.Count+" immutable whole-result hashes");return 0;
}
using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-before-a51e8a6.json")));
if(Json(snapshots)!=baseline.RootElement.GetProperty("snapshots").GetRawText()) {
 File.WriteAllText(args[1]+".singles-after.json",Json(snapshots));
 foreach(var pair in baseline.RootElement.GetProperty("snapshots").EnumerateArray().Zip(snapshots))if(pair.First.GetRawText()!=Json(pair.Second))Console.WriteLine("CHANGED "+pair.First.GetRawText()+" NOW "+Json(pair.Second));
 throw new Exception("Immutable singles whole-result hashes differ");
}
Console.WriteLine("PASS 20 immutable singles results");
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status,KnockedOffItem:m.KnockedOffItem);
SourceBattleFrame Frame(int actor,string species,string move,uint seed=31,int? target=null){
 var mons=Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,"DEFENSE_CURL") with{SpeedStage=i}).ToArray();
 mons[actor]=Mon(species,80,move) with{SpeedStage=12};
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
  BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray(),
  Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var c=f.Slots[slot];var k=c.Member!.Value;var party=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=m}),Parties=f.Parties.SetItem((int)k.Side,party with{Members=party.Members.SetItem(k.Index,Member(m))})};}
SourceKernelResult Action(SourceBattleFrame f,int actor,int? target=null){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target??(catalog.Moves[f.Slots[actor].Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?actor:actor^1))));Check(r.Succeeded,"real slot action "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid action state");return r;}
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

string Item(SourceBattleFrame f,int slot)=>f.Get(new(slot)).Body!.HeldItem;
string Permanent(SourceBattleFrame f,int slot){var k=f.Get(new(slot)).Member!.Value;return WildBattleSourcePartyEffects.PersistentHeldItem(f.Party(k.Side).Members[k.Index]);}
SourceBattleFrame Used(SourceBattleFrame f,int slot,string? item){var c=f.Slots[slot];return f with{Slots=f.Slots.SetItem(slot,c with{Position=c.Position with{UsedHeldItem=item}})};}
SourceBattleFrame Reserve(SourceBattleFrame f,int side,params WildBattleCombatant[] mons){var p=f.Parties[side];var next=f with{Parties=f.Parties.SetItem(side,p with{Members=p.Members.AddRange(mons.Select(Member))})};return side!=0?next:next with{Rewards=next.Rewards! with{Metadata=Enumerable.Range(0,next.Parties[0].Members.Length).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};}
Test("all five source moves, all four owners and allies",()=>{
 foreach(string name in new[]{"THIEF","COVET","TRICK","KNOCK_OFF","RECYCLE"})foreach(int actor in Enumerable.Range(0,4)){
  foreach(int target in name=="RECYCLE"?new[]{actor}:Enumerable.Range(0,4).Where(i=>i!=actor)){
   var f=Frame(actor,"SNORLAX",name);f=Used(f,actor,"ITEM_PERSIM_BERRY");
   if(name!="RECYCLE")f=Put(f,target,f.Slots[target].Body! with{HeldItem="ITEM_CHOICE_BAND"});
   var r=Action(f,actor,target);
   if(name=="RECYCLE")Check(Item(r.State,actor)=="ITEM_PERSIM_BERRY"&&Permanent(r.State,actor)=="ITEM_PERSIM_BERRY"&&r.State.Slots[actor].Position.UsedHeldItem is null,"slot receipt consumed and party written");
   else if(name=="KNOCK_OFF")Check(Item(r.State,target)=="ITEM_NONE"&&Permanent(r.State,target)=="ITEM_CHOICE_BAND"&&r.State.Slots[target].Body!.KnockedOffItem=="ITEM_CHOICE_BAND","knock removes only battle item");
   else if(actor%2==0)Check(Item(r.State,actor)=="ITEM_CHOICE_BAND"&&Permanent(r.State,actor)=="ITEM_CHOICE_BAND"&&Item(r.State,target)=="ITEM_NONE"&&Permanent(r.State,target)=="ITEM_NONE"&&r.State.Slots[actor].Body!.SourceChoiceMove is null,"ordinary player transfer writes both real party identities, delayed Choice");
   else Check(Item(r.State,actor)=="ITEM_NONE"&&Item(r.State,target)=="ITEM_CHOICE_BAND","ordinary opponent transfer blocked");
   Check(r.State.Slots.Where((_,i)=>i!=actor&&i!=target).Select(c=>Json(c.Body)).SequenceEqual(f.Slots.Where((_,i)=>i!=actor&&i!=target).Select(c=>Json(c.Body))),"independent bodies unchanged");
   Check(r.Trace.Length==(name=="RECYCLE"?0:name=="TRICK"?1:4),"exact script RNG count");
   Check(r.Events.OfType<SourceMoveUsedEvent>().Single().Actor.Value==actor,"PP owner is actual slot");
   Check(r.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(name)].Pp-1,"one PP spent");
  }
 }
});
Test("Sticky Hold, Substitute, Protect and KO source distinctions",()=>{
 foreach(string name in new[]{"THIEF","COVET","TRICK","KNOCK_OFF"}){
  var f=Frame(2,"SNORLAX",name);f=Put(f,3,Mon("GRIMER",100,"HARDEN") with{HeldItem="ITEM_CHOICE_BAND"});
  var r=Action(f,2);Check(r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Target.Value==3&&e.Ability=="ABILITY_STICKY_HOLD"),"Sticky Hold event");Check(Item(r.State,3)=="ITEM_CHOICE_BAND","Sticky Hold item retained");
  f=Put(f,3,f.Slots[3].Body! with{CalledOverlay=null,CalledVolatile=new(){SubstituteHp=1}});r=Action(f,2);
  Check(Item(r.State,3)=="ITEM_CHOICE_BAND"&&Item(r.State,2)=="ITEM_NONE","broken and unbroken doll block transfer");Check(r.Trace.Length==(name=="TRICK"?0:4),"Trick tests doll before accuracy");
  f=Put(f,3,f.Slots[3].Body! with{CalledVolatile=null,Protected=true});r=Action(f,2);Check(r.Trace.IsEmpty&&Item(r.State,3)=="ITEM_CHOICE_BAND","Protect skips accuracy RNG and all mutation");
  if(name=="TRICK")continue;
  f=Put(f,3,Hp(Mon("MAGIKARP",5,"SPLASH") with{HeldItem="ITEM_CHOICE_BAND"},1));r=Action(f,2);
  Check(r.State.Slots[3].Body!.Pokemon.CurrentHp==0,"actual target KO");
  Check(r.Events.OfType<SourceHeldItemStolenEvent>().Any()==(name!="KNOCK_OFF"),"theft works on KO, Knock Off does not");
 }
});
Test("occupied thief cannot steal twice; choice clears and selection stays bounded",()=>{
 var f=Frame(2,"SNORLAX","THIEF");f=Put(f,3,f.Slots[3].Body! with{HeldItem="ITEM_CHOICE_BAND",SourceChoiceMove=Id("DEFENSE_CURL")});
 var r=Action(f,2);Check(r.State.Slots[3].Body!.SourceChoiceMove is null,"stolen target choice cleared");
 f=Put(r.State,1,r.State.Slots[1].Body! with{HeldItem="ITEM_SHELL_BELL"});r=Action(f,2,1);
 Check(!r.Events.OfType<SourceHeldItemStolenEvent>().Any()&&Item(r.State,1)=="ITEM_SHELL_BELL","second item cannot be stolen while holding first");
 Check(r.State.Slots[2].Body!.SourceChoiceMove==Id("THIEF"),"next action is Choice locked");
 var user=r.State.Slots[2].Body!;user=user with{Pokemon=user.Pokemon with{Moves=user.Pokemon.Moves.Add(new(Id("DEFENSE_CURL"),40,40))}};f=Put(r.State,2,user);
 var bad=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[2].Occupant!.Value,f.Revision,1,new(2)));
 Check(!bad.Succeeded&&bad.Fault=="source_kernel_choice_locked"&&bad.State==f&&bad.Events.IsEmpty&&bad.Trace.IsEmpty,"unselectable Choice move rejected atomically");
});
Test("source item guard order, failed Recycle receipts and Choice damage",()=>{
 var f=Frame(2,"SNORLAX","THIEF");f=Put(f,2,f.Slots[2].Body! with{HeldItem="ITEM_CHOICE_BAND"});f=Put(f,3,Mon("GRIMER",100,"HARDEN") with{HeldItem="ITEM_SHELL_BELL"});var r=Action(f,2);
 Check(r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_STICKY_HOLD"),"Sticky Hold precedes occupied thief check");
 f=Put(f,2,f.Slots[2].Body! with{HeldItem="ITEM_NONE",KnockedOffItem="ITEM_CHOICE_BAND"});r=Action(f,2);Check(!r.Events.OfType<SourceAbilityPreventedEvent>().Any()&&Item(r.State,3)=="ITEM_SHELL_BELL","knocked thief exits before Sticky Hold");
 f=Frame(0,"SNORLAX","TRICK");f=Put(f,0,f.Slots[0].Body! with{HeldItem="ITEM_CHOICE_BAND"});f=Put(f,1,Mon("GRIMER",100,"HARDEN"));r=Action(f,0);Check(r.Events.OfType<SourceAbilityPreventedEvent>().Any(),"Trick blocked by itemless Sticky Hold target");
 f=Put(f,1,f.Slots[1].Body! with{KnockedOffItem="ITEM_SHELL_BELL"});r=Action(f,0);Check(r.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="item-swap-not-allowed"&&!r.Events.OfType<SourceAbilityPreventedEvent>().Any(),"either knocked party index blocks Trick before Sticky Hold");
 f=Used(Frame(0,"SNORLAX","RECYCLE"),0,"ITEM_PERSIM_BERRY");f=Put(f,0,f.Slots[0].Body! with{HeldItem="ITEM_CHOICE_BAND"});r=Action(f,0,0);Check(r.State.Slots[0].Position.UsedHeldItem=="ITEM_PERSIM_BERRY"&&r.Events.OfType<SourceMoveFailedEvent>().Any(),"occupied Recycle retains unconsumed receipt");
 f=Frame(2,"SNORLAX","COVET");int ordinary=Action(f,2).Events.OfType<SourceHpChangedEvent>().Single().Damage;f=Put(f,2,f.Slots[2].Body! with{HeldItem="ITEM_CHOICE_BAND"});r=Action(f,2);Check(r.Events.OfType<SourceHpChangedEvent>().Single().Damage>ordinary&&r.State.Slots[2].Body!.SourceChoiceMove==Id("COVET"),"real four-slot physical damage uses existing Choice Band multiplier and lock");
 var family=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasItemTransferRecipe",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
 foreach(string name in new[]{"PRESENT","BEAT_UP","METRONOME","PAIN_SPLIT"})Check(!(bool)family.Invoke(null,[catalog.Moves[Id(name)].Effect])!,"unrelated item-transfer family "+name);
});
Test("Trick transfers both ways and clears both choice memories",()=>{
 var f=Frame(0,"SNORLAX","TRICK");f=Put(f,0,f.Slots[0].Body! with{HeldItem="ITEM_CHOICE_BAND",SourceChoiceMove=Id("TRICK")});f=Put(f,2,f.Slots[2].Body! with{HeldItem="ITEM_SHELL_BELL",SourceChoiceMove=Id("DEFENSE_CURL")});var r=Action(f,0,2);
 Check(Item(r.State,0)=="ITEM_SHELL_BELL"&&Item(r.State,2)=="ITEM_CHOICE_BAND"&&Permanent(r.State,0)=="ITEM_SHELL_BELL"&&Permanent(r.State,2)=="ITEM_CHOICE_BAND","ally swap persists by member key");
 Check(r.State.Slots[0].Body!.SourceChoiceMove is null&&r.State.Slots[2].Body!.SourceChoiceMove is null,"both source choice memories clear");
 f=Frame(0,"SNORLAX","TRICK");f=Put(f,0,f.Slots[0].Body! with{HeldItem="ITEM_CHOICE_BAND"});r=Action(f,0,1);Check(Item(r.State,0)=="ITEM_NONE"&&Item(r.State,1)=="ITEM_CHOICE_BAND"&&Permanent(r.State,0)=="ITEM_NONE","give-only swap does not install pending none");
});
Test("real four-action berry and held-item phases",()=>{
 var f=Frame(2,"SNORLAX","THIEF");f=Put(f,2,Hp(f.Slots[2].Body! with{Status=WildBattleStatus.Burn},20));f=Put(f,3,f.Slots[3].Body! with{HeldItem="ITEM_RAWST_BERRY"});var r=First(f,2);
 var stolen=r.Events.OfType<SourceHeldItemStolenEvent>().Single();var consumed=r.Events.OfType<SourceHeldItemConsumedEvent>().Single(e=>e.Side.Value==2);
 Check(stolen.Sequence<consumed.Sequence&&Item(r.State,2)=="ITEM_NONE"&&Permanent(r.State,2)=="ITEM_NONE"&&r.State.Slots[2].Position.UsedHeldItem=="ITEM_RAWST_BERRY","received berry consumed after change and remembers receiver slot");Finish(r);
 f=Frame(2,"SNORLAX","THIEF");f=Put(f,2,Hp(f.Slots[2].Body!,20));f=Put(f,3,f.Slots[3].Body! with{HeldItem="ITEM_SHELL_BELL"});r=First(f,2);Check(r.Events.OfType<SourceHeldItemHealEvent>().Single().Sequence>r.Events.OfType<SourceHeldItemStolenEvent>().Single().Sequence,"new Bell heals same move after changed items");Finish(r);
 f=Frame(2,"SNORLAX","COVET");f=Put(f,3,Mon("CARVANHA",100,"LEER") with{HeldItem="ITEM_CHOICE_BAND"});r=First(f,2);Check(!r.Events.OfType<SourceAbilityDamageEvent>().Any(),"Covet has no contact flag in FireRed");Finish(r);
});
Test("actual consumption, simultaneous switches and slot-local Recycle",()=>{
 var f=Frame(0,"SNORLAX","DEFENSE_CURL");f=Put(f,0,Hp(f.Slots[0].Body! with{HeldItem="ITEM_PERSIM_BERRY",ConfusionTurnsRemaining=1},200));
 // A different battler's ordinary move ends first and consumes player-left's berry.
 f=Put(f,1,f.Slots[1].Body! with{SpeedStage=12});f=Put(f,0,f.Slots[0].Body! with{SpeedStage=0});
 var eaten=Finish(First(f,1)).State;Check(eaten.Slots[0].Position.UsedHeldItem=="ITEM_PERSIM_BERRY"&&eaten.Slots[2].Position.UsedHeldItem is null,"real berry receipt belongs only to slot zero");
 eaten=Reserve(eaten,0,Mon("SNORLAX",100,"RECYCLE"),Mon("SNORLAX",100,"RECYCLE"));
 var selections=Choices(eaten,0).Select(c=>c.Actor.Slot.Value is 0 or 2?new SourceActionSelection(c.Actor,SourceActionKind.Switch,Incoming:new(BattleSide.Player,c.Actor.Slot.Value==0?2:3)):c).ToImmutableArray();
 var switched=Finish(Ok(SourceFourActionTurns.BeginActions(catalog,eaten,eaten.Revision,selections))).State;
 Check(switched.Slots[0].Member!.Value.Index==2&&switched.Slots[2].Member!.Value.Index==3&&switched.Slots[0].Position.UsedHeldItem=="ITEM_PERSIM_BERRY","both real switch identities and slot receipt survive");
 switched=Put(switched,0,switched.Slots[0].Body! with{SpeedStage=12});var recycled=First(switched,0,0);Check(Item(recycled.State,0)=="ITEM_PERSIM_BERRY"&&Permanent(recycled.State,0)=="ITEM_PERSIM_BERRY"&&recycled.State.Slots[0].Position.UsedHeldItem is null,"new occupant recycles predecessor consumed item");Finish(recycled);
});
Test("Knock Off then Recycle keeps permanent item and suppression on return",()=>{
 var f=Frame(1,"SNORLAX","KNOCK_OFF");f=Put(f,0,f.Slots[0].Body! with{HeldItem="ITEM_CHOICE_BAND"});f=Used(f,0,"ITEM_PERSIM_BERRY");
 var r=First(f,1,0);Check(Item(r.State,0)=="ITEM_NONE"&&Permanent(r.State,0)=="ITEM_CHOICE_BAND","Knock Off permanent item retained");f=Finish(r).State;
 f=Put(f,0,f.Slots[0].Body! with{Pokemon=f.Slots[0].Body!.Pokemon with{Moves=[new(Id("RECYCLE"),10,10)]}});f=Put(f,0,f.Slots[0].Body! with{SpeedStage=12});
 var restored=First(f,0,0);Check(Item(restored.State,0)=="ITEM_PERSIM_BERRY"&&Permanent(restored.State,0)=="ITEM_PERSIM_BERRY"&&restored.State.Slots[0].Body!.KnockedOffItem=="ITEM_CHOICE_BAND","Recycle overwrites party held item while original knocked bit remains");f=Finish(restored).State;
 f=Reserve(f,0,Mon("SNORLAX",100,"DEFENSE_CURL"));
 SourceBattleFrame SwitchTurn(SourceBattleFrame input,int index){var selections=Choices(input,0).Select(c=>c.Actor.Slot.Value==0?new SourceActionSelection(c.Actor,SourceActionKind.Switch,Incoming:new(BattleSide.Player,index)):c).ToImmutableArray();return Finish(Ok(SourceFourActionTurns.BeginActions(catalog,input,input.Revision,selections))).State;}
 f=SwitchTurn(f,2);f=SwitchTurn(f,0);Check(Item(f,0)=="ITEM_NONE"&&Permanent(f,0)=="ITEM_PERSIM_BERRY","switch loads party item then suppresses it using same party-index marker");
});
Test("simultaneous theft-target and Rough Skin attacker KO, rewards and replacements",()=>{
 var f=Frame(2,"SNORLAX","THIEF");f=Put(f,2,Hp(f.Slots[2].Body!,1));f=Put(f,3,Hp(Mon("CARVANHA",100,"LEER") with{HeldItem="ITEM_SHELL_BELL"},1));
 f=Reserve(f,0,Mon("SNORLAX",100,"DEFENSE_CURL"));f=Reserve(f,1,Mon("SNORLAX",100,"DEFENSE_CURL"));
 var r=First(f,2);Check(r.Events.OfType<SourceFaintedEvent>().Select(e=>e.Side.Value).SequenceEqual(new[]{3,2}),"target KO then Rough Skin attacker KO");
 Check(Item(r.State,2)=="ITEM_SHELL_BELL"&&Permanent(r.State,2)=="ITEM_SHELL_BELL"&&Permanent(r.State,3)=="ITEM_NONE","source controller item writes survive simultaneous KOs");
 Check(!r.Events.OfType<SourceHeldItemHealEvent>().Any(),"dead thief cannot use newly acquired Shell Bell");
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending,"real reward continuation after both KOs");
 var done=Finish(r).State;Check(done.Slots[2].Member!.Value.Index==2&&done.Slots[3].Member!.Value.Index==2&&done.Rewards!.CompletedOpponentMask==2,"each reserve enters at end, defeated owner pays once");
 Check(WildBattleSourcePartyEffects.PersistentHeldItem(done.Parties[0].Members[1])=="ITEM_SHELL_BELL"&&WildBattleSourcePartyEffects.PersistentHeldItem(done.Parties[1].Members[1])=="ITEM_NONE","departed original owners keep actual permanent settlement");
});
Test("canonical trainer and wild singles retain source permanent settlement",()=>{
 foreach(var kind in new[]{WildBattleCalledBattleKind.Trainer,WildBattleCalledBattleKind.Wild})foreach(string name in new[]{"THIEF","COVET","TRICK","KNOCK_OFF","RECYCLE"}){
  var a=Mon("SNORLAX",80,name);var b=Mon("SNORLAX",100,"DEFENSE_CURL") with{HeldItem="ITEM_CHOICE_BAND"};
  var state=new WildBattleState(a,b,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,CalledParty=new([Member(a)],[Member(b)],0,0,kind){PlayerUsedHeldItem="ITEM_PERSIM_BERRY"}};
  var r=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));Check(r.Succeeded,"canonical "+kind+"/"+name+" "+r.Fault);var party=r.State!.CalledParty!;
  Check(WildBattleRules.ValidateCalledMoveActionState(catalog,r.State!) is null,"singles output party valid");
  if(name=="KNOCK_OFF")Check(WildBattleSourcePartyEffects.PersistentHeldItem(party.Opponent[0])=="ITEM_CHOICE_BAND"&&r.State.Opponent.HeldItem=="ITEM_NONE","wild/trainer Knock Off temporary");
  else if(name=="RECYCLE")Check(WildBattleSourcePartyEffects.PersistentHeldItem(party.Player[0])=="ITEM_PERSIM_BERRY","wild/trainer Recycle permanent");
  else Check(WildBattleSourcePartyEffects.PersistentHeldItem(party.Player[0])=="ITEM_CHOICE_BAND"&&WildBattleSourcePartyEffects.PersistentHeldItem(party.Opponent[0])=="ITEM_NONE","Gen III trainer as well as wild theft/swap is permanent");
 }
});
Test("malformed party, used-item and choice receipts reject before spending",()=>{
 var f=Frame(0,"SNORLAX","RECYCLE");
 foreach(string invalid in new[]{"ITEM_NONE","ITEM_CHOICE_BAND","ITEM_PAMTRE_BERRY","ITEM_NOT_REAL"}){var bad=Used(f,0,invalid);var r=SourceBattleKernel.ResolveAction(catalog,bad,new(bad.Slots[0].Occupant!.Value,bad.Revision,0,new(0)));Check(!r.Succeeded&&r.State==bad&&r.Events.IsEmpty&&r.Trace.IsEmpty,"invalid consumed-item receipt atomic");}
 var p=f.Parties[0];var wrong=f with{Parties=f.Parties.SetItem(0,p with{Members=p.Members.SetItem(1,p.Members[1] with{PersistentHeldItemOverride="ITEM_NOT_REAL"})})};
 Check(SourceFourActionTurns.ValidateCapability(catalog,wrong)=="source_item_party_receipt","reserve/permanent item receipts validated");
 wrong=Put(f,2,f.Slots[2].Body! with{SourceChoiceMove=new("pret:MOVE_NOT_REAL")});Check(SourceFourActionTurns.ValidateCapability(catalog,wrong)=="source_item_choice_receipt","nonacting choice receipt validated");
 Check(SourceFourActionTurns.ValidateCapability(catalog,f with{BattleKind=WildBattleCalledBattleKind.Wild})=="source_four_action_battle_kind","four owner remains trainer-only");
});
File.WriteAllText(args[1],Json(new{baseCommit="a51e8a633b36739e398e2e3ef95bab32f15d6a72",runtime=Environment.Version.ToString(),groups,checks,failures,actualTurns,singlesHashes=snapshots,results}));
File.WriteAllText(args[1]+".four-actions.json",Json(turnEvidence));
Console.WriteLine($"{groups} groups, {checks} assertions, {actualTurns} actual turns, {failures} failures");return failures==0?0:1;
