using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
if(args.Length!=2)return 2;
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;var output=new List<object>();
MoveId Id(string x)=>new("pret:MOVE_"+x);
WildBattleCombatant Mon(string species,string moves="METRONOME",int hp=-1){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),70,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{CurrentHp=hp<0?p.CurrentHp:hp,Moves=moves.Split(',').Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});
}
WildBattleCalledPartyMember M(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
WildBattleState Pair(WildBattleCombatant reserve,int layers=0){var p=Mon("CLEFAIRY");var o=Mon("DUSCLOPS");return new(p,o,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){
 SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,Terrain=WildBattleTerrain.Plain,CalledParty=new([M(p),M(reserve),M(Mon("PIDGEY"))],[M(o),M(Mon("RATTATA"))],0,0,WildBattleCalledBattleKind.Trainer){PlayerSpikes=layers}};}
WildBattleState Save(string name,WildBattleResult r){if(!r.Succeeded)throw new Exception(name+" "+r.Fault);var body=JsonSerializer.Serialize(new{state=r.State,events=r.Events,trace=r.Trace});output.Add(new{name,sha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(body))).ToLowerInvariant()});return r.State!;}
foreach(var species in new[]{"RATTATA","PIDGEY","KOFFING","GROWLITHE","SLAKOTH"})foreach(var layers in new[]{1,2,3}){
 var f=Pair(Mon(species),layers);Save("single-entry-"+species+"-"+layers,WildBattleRules.ResolveCalledPartySwitch(catalog,f,new(WildBattleSide.Player,1)));
}
{
 var f=Pair(Mon("RATTATA"));for(int i=0;i<4;i++)f=Save("set-spikes-pressure-"+i,WildBattleRules.ResolveCalledMoveAction(catalog,f,new(WildBattleSide.Player,0,Id("SPIKES"))).Battle);
}
foreach(var move in new[]{"BATON_PASS","ROAR"}){
 var f=Pair(Mon("GROWLITHE"),3);var r=WildBattleRules.ResolveCalledMoveAction(catalog,f,new(WildBattleSide.Player,0,Id(move)));f=Save("called-"+move,r.Battle);
 if(f.CalledParty?.PendingSwitch is{} choice)Save("called-baton-choice",WildBattleRules.ResolveCalledPartySwitch(catalog,f,new(choice.Side,1)));
}
{
 var f=Pair(Mon("GROWLITHE",hp:1),3);f=Save("entry-KO",WildBattleRules.ResolveCalledPartySwitch(catalog,f,new(WildBattleSide.Player,1)));Save("entry-KO-next",WildBattleRules.ResolveCalledPartySwitch(catalog,f,new(WildBattleSide.Player,2)));
}
{
 var f=Save("canonical-start",WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("SKARMORY","SPIKES"),Mon("GIRAFARIG","BATON_PASS")],0,Mon("MAGIKARP","SPLASH"),new(71)));
 f=Save("canonical-spikes-turn",WildBattleRules.ResolveMove(catalog,f,0,true));Save("canonical-voluntary-turn",WildBattleRules.SwitchSourceWildPlayer(catalog,f,1));
}
foreach(var species in new[]{"RATTATA","PIDGEY","KOFFING"}){
 var f=Pair(Mon(species),3);f=f with{Player=f.Player with{Rooted=true,CalledVolatile=new(){SubstituteHp=20}}};
 f=Save("rooted-substitute-baton-"+species,WildBattleRules.ResolveCalledMoveAction(catalog,f,new(WildBattleSide.Player,0,Id("BATON_PASS"))).Battle);
 f=Save("rooted-substitute-entry-"+species,WildBattleRules.ResolveCalledPartySwitch(catalog,f,new(WildBattleSide.Player,1)));
 if(f.Player.CalledVolatile?.SubstituteHp!=20||!f.Player.Rooted)throw new Exception("Baton subset was not retained");
 int expected=Mon(species).Pokemon.CurrentHp-(species=="RATTATA"?Mon(species).Pokemon.Stats.MaxHp/4:0);if(f.Player.Pokemon.CurrentHp!=expected)throw new Exception("source Rooted/Flying/Levitate passive behavior");
}
File.WriteAllText(args[1],JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{hashes=output.Count,runtime=Environment.Version.ToString()}));return output.Count==33?0:1;
