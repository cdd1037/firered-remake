using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

if(args.Length!=2)return 2;
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;var results=new List<string>();int transitions=0,turns=0;
MoveId Id(string move)=>new("pret:MOVE_"+move);
WildBattleCombatant Mon(string species,string moves,int level=70){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 foreach(var m in moves.Split(','))if(!catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==Id(m)&&x.Level<=level))throw new Exception("unauthentic natural move "+species+" "+m);
 return new(p with{Moves=moves.Split(',').Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});
}
WildBattleState Result(string name,WildBattleResult result){if(!result.Succeeded)throw new Exception(name+" "+result.Fault);results.Add(JsonSerializer.Serialize(new{name,state=result.State,events=result.Events,trace=result.Trace}));transitions++;return result.State!;}
WildBattleState Start(WildBattleCombatant p,WildBattleCombatant reserve,uint seed=7)=>Result("entry",WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[p,reserve],0,Mon("MAGIKARP","SPLASH",5),new(seed)));
WildBattleState Turn(WildBattleState state,int slot=0){var r=WildBattleRules.ResolveMove(catalog,state,slot,true);var next=Result("turn",r);if(next.Turn>state.Turn)turns++;return next;}
foreach(uint seed in new uint[]{7,31}){
 var state=Start(Mon("GIRAFARIG","AGILITY,BATON_PASS"),Mon("GYARADOS","DRAGON_DANCE"),seed);
 state=Turn(state);state=Turn(state,1);if(state.CalledParty?.PendingSwitch?.Kind!=WildBattleCalledSwitchKind.BatonPass)throw new Exception("no real Baton pause");
 state=Result("Baton choice",WildBattleRules.ChooseSourceWildParty(catalog,state,1));turns++;state=Turn(state);
 state=Result("voluntary reentry",WildBattleRules.SwitchSourceWildPlayer(catalog,state,0));turns++;state=Turn(state);
}
{
 var state=Start(Mon("CHANSEY","SOFT_BOILED")with{Status=WildBattleStatus.Poison},Mon("SLAKOTH","SCRATCH"));
 state=Result("Natural Cure switch",WildBattleRules.SwitchSourceWildPlayer(catalog,state,1));turns++;state=Turn(state);
}
foreach(var move in new[]{"ROAR","WHIRLWIND"})foreach(uint seed in new uint[]{0,31}){
 var p=Mon("CLEFAIRY","METRONOME");var foe=Mon("RATTATA","TACKLE",80);var incoming=Mon("GYARADOS","DRAGON_DANCE");
 WildBattleCalledPartyMember M(WildBattleCombatant x)=>new(x.Pokemon,x.HeldItem,x.Status);
 var state=new WildBattleState(p,foe,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){
  Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,CalledParty=new([M(p)],[M(foe),M(incoming)],0,0,WildBattleCalledBattleKind.Trainer)};
 var result=WildBattleRules.ResolveCalledMoveAction(catalog,state,new(WildBattleSide.Player,0,Id(move)));
 Result("Metronome "+move,result.Battle);
}
File.WriteAllLines(args[1],results);Console.WriteLine(JsonSerializer.Serialize(new{transitions,turns,bytes=new FileInfo(args[1]).Length,runtime=Environment.Version.ToString()}));return 0;
