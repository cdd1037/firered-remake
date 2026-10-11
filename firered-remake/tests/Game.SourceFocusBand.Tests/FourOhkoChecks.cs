using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

internal static class FourOhkoChecks
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

  var attacks=new[]{("PINSIR","GUILLOTINE"),("NIDORAN_M","HORN_DRILL"),("DUGTRIO","FISSURE"),("LAPRAS","SHEER_COLD")};
  Test("four authentic OHKO moves at all positions retain source strict chance and no critical damage draws",()=>{
   foreach(var(species,move)in attacks)for(int actor=0;actor<4;actor++)for(uint seed=0;seed<12;seed++)
   {
    int target=actor^1;var mons=new[]{Mon(),Mon(),Mon(),Mon()};mons[actor]=Mon(species,move,80);mons[target]=Mon("SNORLAX","AMNESIA",80);
    var f=Frame(mons,seed);var r=Hit(f,actor,target);Check(r.Succeeded,r.Fault??"OHKO action");
    var roll=r.Trace.Single(t=>t.Purpose.EndsWith(".ohko"));bool hit=roll.Raw%100+1<30;
    Check((r.State.Get(new(target)).Body!.Pokemon.CurrentHp==0)==hit,"source strict29/100 mapping");
    Check(!r.Trace.Any(t=>t.Purpose.EndsWith(".accuracy")||t.Purpose.EndsWith(".critical")||t.Purpose.EndsWith(".damage")),"no ordinary accuracy/critical/damage draw");
    Check(r.State.Get(new(actor)).Body!.Pokemon.Moves[0].CurrentPp==4,"one source PP");
    Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid hit or miss state");
    if(seed==0)observations.Add(new{kind="positions",move,actor,target,trace=r.Trace,events=r.Events});
   }
  });
  Test("actual Focus precedes Sturdy or failed level; Endure and Substitute preserve tryKO semantics",()=>{
   for(int actor=0;actor<4;actor++)
   {
    int target=actor^1;var mons=new[]{Mon(),Mon(),Mon(),Mon()};mons[actor]=Mon("NIDORAN_M","HORN_DRILL",100);var sturdy=Mon("GEODUDE","DEFENSE_CURL",80,"ITEM_FOCUS_BAND");
    if(sturdy.Pokemon.AbilityId!="ABILITY_STURDY") {var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_GEODUDE"),80,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(1)),new(0)).Pokemon;sturdy=sturdy with{Pokemon=p with{Moves=sturdy.Pokemon.Moves}};}
    Check(sturdy.Pokemon.AbilityId=="ABILITY_STURDY","authentic Sturdy donor");mons[target]=sturdy;var blocked=Hit(Frame(mons,31),actor,target);Check(blocked.Succeeded&&blocked.Trace.Length==1&&blocked.Trace[0].Purpose.EndsWith(".focus-band"),"Band before Sturdy only draw");Check(blocked.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="ohko-sturdy","Sturdy result");
    mons[actor]=Mon("NIDORAN_M","HORN_DRILL",70);mons[target]=Mon("SNORLAX","AMNESIA",80,"ITEM_FOCUS_BAND");var low=Hit(Frame(mons,31),actor,target);Check(low.Succeeded&&low.Trace.Length==2&&low.Trace[0].Purpose.EndsWith(".focus-band")&&low.Trace[1].Purpose.EndsWith(".ohko"),"lower level still consumes both draws");Check(low.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="ohko-level","lower level fails");
    mons[actor]=Mon("NIDORAN_M","HORN_DRILL",100);mons[target]=Mon("SNORLAX","AMNESIA",40,"ITEM_FOCUS_BAND",20)with{Enduring=true};bool endured=false,sub=false;
    for(uint seed=0;seed<64&&!endured;seed++){var r=Hit(Frame(mons,seed),actor,target);Check(r.Succeeded,r.Fault??"Endure fixture");if(r.Events.OfType<SourceDamageAdjustedEvent>().FirstOrDefault()is{} a){Check(a.Endured&&!a.HungOn&&r.State.Get(new(target)).Body!.Pokemon.CurrentHp==1,"Endure precedence");endured=true;}}
    mons[target]=mons[target]with{Enduring=false,CalledVolatile=new(){SubstituteHp=10}};
    for(uint seed=0;seed<64&&!sub;seed++){var r=Hit(Frame(mons,seed),actor,target);Check(r.Succeeded,r.Fault??"Substitute fixture");if(r.Events.OfType<SourceDamageAdjustedEvent>().Any(a=>a.HungOn)){Check(r.Events.OfType<SourceDamageAdjustedEvent>().Single().DamageAfter==19,"OHKO protection ignores substitute clamp guard");Check(r.State.Get(new(target)).Body!.Pokemon.CurrentHp==20,"Substitute absorbs exact OHKO without body spill");sub=true;}}
    Check(endured&&sub,"bounded successful protection vectors");
   }
  });
  Test("type immunity and Protect are before Focus; matching Lock-On skips only KO roll",()=>{
   foreach(var(species,move)in attacks){var mons=new[]{Mon(species,move,100),Mon("SNORLAX","AMNESIA",40,"ITEM_FOCUS_BAND"),Mon(),Mon()};mons[1]=mons[1]with{Protected=true};var p=Hit(Frame(mons),0,1);Check(p.Succeeded&&p.Trace.IsEmpty&&p.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="ohko-unreachable","Protect before item roll");}
   foreach(var (species,move,foe,foeMove) in new[]{("PINSIR","GUILLOTINE","GASTLY","NIGHT_SHADE"),("DUGTRIO","FISSURE","PIDGEY","SAND_ATTACK"),("DUGTRIO","FISSURE","KOFFING","SMOKESCREEN")}){
    var mons=new[]{Mon(species,move,100),Mon(foe,foeMove,50,"ITEM_FOCUS_BAND"),Mon(),Mon()};var r=Hit(Frame(mons),0,1);Check(r.Succeeded&&r.Trace.Length==1&&r.Trace[0].Purpose.EndsWith(".secondary-effect"),"immune only consumes zero-chance command");Check(r.Events.OfType<SourceEffectivenessEvent>().Any(x=>x.Effectiveness==WildBattleEffectiveness.NoEffect),"immune reported");
   }
   var list=new[]{Mon("NIDORAN_M","HORN_DRILL",100),Mon("SNORLAX","AMNESIA",50,"ITEM_FOCUS_BAND"),Mon(),Mon()};var f=Frame(list,31);var cell=f.Slots[1];f=f with{Slots=f.Slots.SetItem(1,cell with{Registers=cell.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,null,2,new(0))}})};var sure=Hit(f,0,1);Check(sure.Succeeded&&sure.Trace.Any(t=>t.Purpose.EndsWith(".focus-band"))&&!sure.Trace.Any(t=>t.Purpose.EndsWith(".ohko")),"matching mark skips chance only");
   var bad=f with{EventSequence=long.MaxValue};var rejected=Hit(bad,0,1);Check(!rejected.Succeeded&&ReferenceEquals(bad,rejected.State)&&rejected.Events.IsEmpty&&rejected.Trace.IsEmpty,"publication failure atomic");
  });
  Test("real complete four turn settles OHKO EXP once and deferred replacement",()=>{
   var mons=new[]{Mon("PINSIR","GUILLOTINE",100),Mon("SNORLAX","AMNESIA",10),Mon(),Mon()};bool observed=false;
   for(uint seed=0;seed<16&&!observed;seed++){
    var f=Frame(mons,seed,true);var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Picks(f)));var events=Finish(ref r);if(!events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==1))continue;
    Check(r.CompletedCleanTurn&&r.State.Get(new(1)).Member!.Value.Index==2,"actual replacement returns Ready");Check(r.State.Get(new(0)).Body!.Pokemon.Moves[0].CurrentPp==4,"complete turn single PP");observations.Add(new{kind="complete-turn",seed,events,state=r.State});observed=true;
   }Check(observed,"actual OHKO turn vector");
  });
  File.WriteAllText(Path.Combine(output,"result.json"),Json(new{groups,checks,failures,runtime=Environment.Version.ToString(),recipeAdmission=catalog.Moves.Values.Count(SourceBattleKernel.SupportsRecipe),admittedMoves=catalog.Moves.Values.Where(SourceBattleKernel.SupportsRecipe).Select(m=>m.Id.Value).Order().ToArray(),observations}));
  Console.WriteLine($"RESULT OHKO {groups} groups {checks} checks {failures} failures");return failures==0?0:1;
 }
}
