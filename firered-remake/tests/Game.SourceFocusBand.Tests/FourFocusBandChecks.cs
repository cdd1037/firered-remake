using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

internal static class FourFocusBandChecks
{
 public static int Run(string root,string reference,string output)
 {
  Directory.CreateDirectory(output);var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(root);var catalog=proof.Catalog;
  int groups=0,checks=0,failures=0;var observations=new List<object>();
  string Json<T>(T v)=>JsonSerializer.Serialize(v);
  void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
  void Test(string name,Action run){groups++;try{run();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);}}
  MoveId Id(string n)=>new("pret:MOVE_"+n);
  WildBattleCombatant Mon(string species="MR_MIME",string move="BARRIER",int level=40,string item="ITEM_NONE",int? hp=null,uint status=0)
  {
   var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
   Check(catalog.Species[p.Species].LevelUpMoves.Any(m=>m.Move==Id(move)&&m.Level<=level),"authentic donor move "+species+"/"+move);
   var d=catalog.Moves[Id(move)];return new(p with{CurrentHp=hp??p.CurrentHp,Moves=[new(d.Id,d.Pp,d.Pp)]},HeldItem:item,Status:status);
  }
  WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
  SourceBattleFrame Frame(WildBattleCombatant[] m,uint seed=0,bool reserves=false)=>new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),
   Parties=[new(BattleSide.Player,reserves?[Member(m[0]),Member(m[2]),Member(Mon())]:[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,reserves?[Member(m[1]),Member(m[3]),Member(Mon())]:[Member(m[1]),Member(m[3])])],
   Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),
   Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,reserves?3:2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
  SourceKernelResult Hit(SourceBattleFrame f,int actor,int target)=>SourceBattleKernel.ResolveAction(catalog,f,new(f.Get(new(actor)).Occupant!.Value,f.Revision,0,new(target)));
  SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State)is null,"identity valid");return r;}
  ImmutableArray<SourceActionSelection> Picks(SourceBattleFrame f)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?c.Slot:c.Slot.Opposite)).ToImmutableArray();
  SourceFourActionResult Advance(SourceFourActionResult r)
  {
   var f=r.State;var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(f))!;
   var a=SourceFourActionTurns.Advance(catalog,f,r.Continuation!.Value);var b=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);
   Check(Json(a)==Json(b),"exact serialized phase continuation");return Ok(a);
  }
  List<SourceBattleEvent> Finish(ref SourceFourActionResult r)
  {
   var all=r.Events.ToList();int bound=0;
   while(r.State.FourTurn is not null&&r.State.Outcome==WildBattleOutcome.Ongoing)
   {
    Check(++bound<36,"finite complete turn");var f=r.State;
    if(f.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){var p=f.PendingSwitches[0];r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,f,r.Continuation!.Value,p.Slot,new(p.Slot.Side,p.LegalPartyIndices[0])));}
    else if(f.Rewards?.Pending is {} pending){var m=pending.Moves[0];r=Ok(SourceFourRewards.Learn(catalog,f,r.Continuation!.Value,new(pending.Recipient,m.Level,m.Move,null)));}
    else r=Advance(r);
    all.AddRange(r.Events);
   }
   return all;
  }
  uint multihitSeed=0;
  Test("all positions use sticky Focus Band within a real multihit and clear it after the action",()=>{
   var actor=Mon("PERSIAN","FURY_SWIPES",70);var holder=Mon("PIDGEY","TACKLE",20,"ITEM_FOCUS_BAND",1);
   var baseMons=new[]{actor,holder,Mon(),Mon()};SourceKernelResult? found=null;
   for(uint seed=0;seed<128;seed++)
   {
    var result=Hit(Frame(baseMons,seed),0,1);Check(result.Succeeded,result.Fault??"probe accepted");var draws=result.Trace.Where(t=>t.Purpose.EndsWith(".focus-band")).ToArray();
    if(draws.Length>=2&&draws[0].Raw%100<10&&draws.Skip(1).Any(t=>t.Raw%100>=10)){multihitSeed=seed;found=result;break;}
   }
   Check(found is not null,"bounded seed with proc then non-proc repeated hits");
   for(int side=0;side<4;side++)
   {
    int target=side^1;var m=new[]{Mon(),Mon(),Mon(),Mon()};m[side]=actor;m[target]=holder;var f=Frame(m,multihitSeed);var r=Hit(f,side,target);Check(r.Succeeded,r.Fault??"real action");
    var draws=r.Trace.Where(t=>t.Purpose.EndsWith(".focus-band")).ToArray();Check(draws.Length>=2&&draws[0].Raw%100<10&&draws.Skip(1).Any(t=>t.Raw%100>=10),"each hit draws even when sticky");
    Check(r.State.Get(new(target)).Body!.Pokemon.CurrentHp==1&&r.State.Get(new(target)).Body!.HeldItem=="ITEM_FOCUS_BAND","HP1 survives multiple hits without consuming item");
    Check(r.Events.OfType<SourceDamageAdjustedEvent>().Count(e=>e.HungOn&&e.Target.Value==target)>=2,"sticky survival receipts belong to actual target");
    Check(r.State.Slots.All(c=>!c.Position.FocusBanded&&c.Position.HitItemReceipt is null),"ActionFinished clears all special-status fields");
    var message=r.Events.OfType<SourceHeldItemSurvivalEvent>().Single();Check(message.Target.Value==target&&message.Move==Id("FURY_SWIPES")&&message.Item=="ITEM_FOCUS_BAND","one successful-hit item result belongs to actual multihit target");
    Check(message.Sequence>r.Events.OfType<SourceHpChangedEvent>().Max(e=>e.Sequence),"multihit result is after actual HP commands");
    Check(Json<SourceBattleEvent>(message)==Json(JsonSerializer.Deserialize<SourceBattleEvent>(Json<SourceBattleEvent>(message))!),"typed survival result round trip");
    if(target<2)Check(Json<SourceBattleEvent>(message)==Json(SourceBattleEventAdapter.Import(SourceBattleEventAdapter.Export(message))),"singles event adapter retains item and target");
    Check(r.State.Get(new(side)).Body!.Pokemon.Moves[0].CurrentPp==actor.Pokemon.Moves[0].CurrentPp-1,"one PP per multihit");
    observations.Add(new{kind="multihit",actor=side,target,seed=multihitSeed,draws,events=r.Events});
    bool failedNext=false;
    for(uint seed=0;seed<32;seed++){var next=Hit(r.State with{Rng=new(seed)},side,target);Check(next.Succeeded,next.Fault??"second action");if(next.State.Get(new(target)).Body!.Pokemon.CurrentHp==0){Check(!next.Events.OfType<SourceDamageAdjustedEvent>().Any(e=>e.HungOn),"previous action success cannot protect next action");failedNext=true;break;}}
    Check(failedNext,"next action can fail its own new roll");
    var exhausted=f with{EventSequence=long.MaxValue};var no=Hit(exhausted,side,target);Check(!no.Succeeded&&ReferenceEquals(no.State,exhausted)&&no.Events.IsEmpty&&no.Trace.IsEmpty,"publication failure has no HP/item/PP/RNG mutation");
   }
  });
  Test("real four-action Focus survival still loses to poison and settles one reward then replacement",()=>{
   var f=Frame([Mon("PERSIAN","FURY_SWIPES",70),Mon("PIDGEY","TACKLE",20,"ITEM_FOCUS_BAND",1,WildBattleStatus.Poison),Mon(),Mon()],multihitSeed,true);
   // Turn setup consumes source order RNG; locate one bounded actual complete-turn survivor.
   bool seen=false;
   for(uint seed=0;seed<128&&!seen;seed++)
   {
    f=f with{Rng=new(seed)};var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Picks(f)));var all=Finish(ref r);
    if(!all.OfType<SourceDamageAdjustedEvent>().Any(e=>e.HungOn))continue;
    Check(all.OfType<SourceStatusDamageEvent>().Any(e=>e.Side.Value==1&&e.HpAfter==0),"residual poison bypasses Focus Band");
    Check(all.OfType<SourceOpponentRewardCompletedEvent>().Count(e=>e.Defeated.Slot.Value==1)==1,"poison faint rewards once");
    Check(r.CompletedCleanTurn&&r.State.Get(new(1)).EntrySerial==2,"replacement only after turn residuals");
    Check(r.State.Party(BattleSide.Opponent).Members[0].HeldItem=="ITEM_FOCUS_BAND","fainted holder retains item");seen=true;observations.Add(new{kind="residual",seed,events=all});
   }
   Check(seen,"actual turn with Focus survival and poison KO");
  });
  Test("delayed Focus state survives turn start but real voluntary switch clears it for every position",()=>{
   bool seen=false;
   for(uint seed=0;seed<128&&!seen;seed++)
   {
    var f=Frame([Mon(),Mon("PIDGEY","TACKLE",20,"ITEM_FOCUS_BAND",1),Mon(),Mon()],seed,true);
    // Controlled legal queued delayed attack. Future Sight casting remains outside this slice.
    f=f with{Slots=f.Slots.SetItem(1,f.Slots[1] with{Position=new(){FutureAttack=new(1,Id("FUTURE_SIGHT"),new(0),999)}})};
    var picks=Picks(f).SetItem(1,new(f.Slots[1].Occupant!.Value,SourceActionKind.Move,0,new(0)));
    var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,picks));var all=Finish(ref r);
    if(r.State.Get(new(1)).Body!.Pokemon.CurrentHp!=1||!r.State.Get(new(1)).Position.FocusBanded)continue;
    var delayedMessage=all.OfType<SourceHeldItemSurvivalEvent>().Single(e=>e.Move==Id("FUTURE_SIGHT"));Check(delayedMessage.Target.Value==1&&delayedMessage.Sequence>all.OfType<SourceHpChangedEvent>().Last(e=>e.Move==Id("FUTURE_SIGHT")).Sequence,"delayed item result follows committed HP update");
    Check(r.CompletedCleanTurn,"delayed success retained through Ready");var next=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;
    var choices=Picks(next).SetItem(0,new(next.Slots[0].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2)));
    r=Ok(SourceFourActionTurns.BeginActions(catalog,next,next.Revision,choices));Check(r.State.Get(new(1)).Position.FocusBanded,"TurnValuesCleanUp does not erase SpecialStatus");
    r=Advance(r);Check(r.State.Get(new(0)).EntrySerial==2&&r.State.Slots.All(c=>!c.Position.FocusBanded&&c.Position.HitItemReceipt is null),"completed switch owns source SpecialStatusesClear");
    observations.Add(new{kind="delayed-switch",seed,delayedEvents=all,switchEvents=r.Events});seen=true;
   }
   Check(seen,"bounded delayed Focus trigger");
  });
  Test("adjustment before Explosion miss is silent; exact source item text resolves",()=>{
   bool seen=false;
   for(uint seed=0;seed<256&&!seen;seed++)
   {
    var attacker=Mon("GOLEM","EXPLOSION",70) with{AccuracyStage=0};
    var f=Frame([attacker,Mon("PIDGEY","TACKLE",20,"ITEM_FOCUS_BAND",1),Mon(),Mon()],seed);
    var r=Hit(f,0,1);Check(r.Succeeded,r.Fault??"Explosion accepted");
    if(!r.Events.OfType<SourceDamageAdjustedEvent>().Any(e=>e.Target.Value==1&&e.HungOn)||!r.Events.OfType<SourceMoveMissedEvent>().Any(e=>e.Target.Value==1))continue;
    Check(!r.Events.OfType<SourceHeldItemSurvivalEvent>().Any(e=>e.Target.Value==1),"missed target cannot print mechanical pre-hit survival");
    seen=true;observations.Add(new{kind="explosion-adjusted-miss",seed,events=r.Events,trace=r.Trace});
   }
   Check(seen,"bounded actual Explosion adjust-then-miss");
   var templates=PretBattleText.Load(root);var pages=PretBattleText.Pages(templates,"sText_PkmnHungOnWithX",new Dictionary<string,string>{{"B_DEF_NAME_WITH_PREFIX","Foe PIDGEY"},{"B_LAST_ITEM","FOCUS BAND"}});
   Check(string.Join(" ",pages).Replace('\n',' ').Contains("Foe PIDGEY hung on using its FOCUS BAND!"),"pinned source result text and exact substitutions");
  });
  Test("confusion resultmessage follows self damage in each real position",()=>{
   for(int slot=0;slot<4;slot++) {
    bool found=false;
    for(uint seed=0;seed<128&&!found;seed++) {
     var mons=Enumerable.Range(0,4).Select(_=>Mon()).ToArray();mons[slot]=Mon(item:"ITEM_FOCUS_BAND",hp:1)with{ConfusionTurnsRemaining=3};
     var r=Hit(Frame(mons,seed),slot,slot);Check(r.Succeeded,r.Fault??"confusion action");
     var hit=r.Events.OfType<SourceConfusionSelfHitEvent>().SingleOrDefault();if(hit is null||hit.HpAfter!=1)continue;
     var message=r.Events.OfType<SourceHeldItemSurvivalEvent>().Single();Check(message.Target.Value==slot&&message.Sequence>hit.Sequence,"own slot survival text after confusion HP");Check(!r.Events.OfType<SourcePpCommandEvent>().Any(),"confusion does not spend selected move PP");found=true;
    }
    Check(found,"real successful confusion survival vector");
   }
  });
  string source=Path.Combine(reference,"src/battle_main.c");Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant()=="c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928","fixed original battle_main SHA256");var text=File.ReadAllText(source);Check(text.Contains("static void SpecialStatusesClear(void)")&&text.Contains("[B_ACTION_SWITCH]                 = HandleAction_Switch"),"pinned action owner source available");
  File.WriteAllText(Path.Combine(output,"result.json"),Json(new{groups,checks,failures,runtime=Environment.Version.ToString(),recipeAdmission=catalog.Moves.Values.Count(SourceBattleKernel.SupportsRecipe),admittedMoves=catalog.Moves.Values.Where(SourceBattleKernel.SupportsRecipe).Select(m=>m.Id.Value).Order().ToArray(),multihitSeed,observations,sourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant(),sharedPrimitiveChanged=false}));
  Console.WriteLine($"RESULT four Focus Band {groups} groups {checks} checks {failures} failures");return failures==0?0:1;
 }
}
