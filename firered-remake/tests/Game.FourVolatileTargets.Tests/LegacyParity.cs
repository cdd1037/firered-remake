using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

static class LegacyParity
{
    public static string Run(string root, bool verifyCanonical = false)
    {
        var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(root);var catalog=proof.Catalog;var records=new List<object>();
        MoveId Id(string s)=>new("pret:MOVE_"+s);
        WildBattleCombatant Mon(string species,int level,string name) {
            var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
            return new(p with {Moves=[new(Id(name),catalog.Moves[Id(name)].Pp,catalog.Moves[Id(name)].Pp)]});
        }
        foreach(var name in new[]{"CONFUSE_RAY","SUPERSONIC","SWEET_KISS","MEAN_LOOK","SPIDER_WEB","BLOCK","WRAP","BIND","FIRE_SPIN","WHIRLPOOL","CLAMP","SAND_TOMB"})
        foreach(uint seed in new uint[]{0,7,31}) {
            var actor=Mon("PIDGEY",20,name);var foe=Mon("RATTATA",100,"DEFENSE_CURL");
            var state=new WildBattleState(actor,foe,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
            var r=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));
            if(!r.Succeeded)throw new Exception(name+": "+r.Fault);
            records.Add(new{name,seed,state=r.State,events=r.Battle.Events,trace=r.Battle.Trace});
            if (verifyCanonical)
            {
                WildBattleCalledPartyMember M(WildBattleCombatant m) => new(m.Pokemon,m.HeldItem,m.Status);
                var adapterState=state with { CalledField = new() };
                var expected=WildBattleRules.ResolveSourceMoveAction(catalog,adapterState,new(WildBattleSide.Player,0));
                if(!expected.Succeeded)throw new Exception(name+" adapter expected: "+expected.Fault);
                var imported=SinglesBattleAdapter.Import(catalog,adapterState,[new(BattleSide.Player,[M(actor)]),new(BattleSide.Opponent,[M(foe)])]);
                if(!imported.Succeeded)throw new Exception(name+" singles import: "+imported.Fault);
                var f=imported.Frame!;var actual=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(1)));
                if(!actual.Succeeded)throw new Exception(name+" singles kernel: "+actual.Fault);
                var exported=SinglesBattleAdapter.Export(actual.State);
                if(!exported.Succeeded||JsonSerializer.Serialize(exported.State)!=JsonSerializer.Serialize(expected.State)||
                    JsonSerializer.Serialize(actual.Events.Select(SourceBattleEventAdapter.Export).ToImmutableArray())!=JsonSerializer.Serialize(expected.Battle.Events)||
                    JsonSerializer.Serialize(actual.Trace)!=JsonSerializer.Serialize(expected.Battle.Trace))
                    throw new Exception(name+" seed "+seed+" singles adapter state/events/RNG parity");
            }
        }
        var json=JsonSerializer.Serialize(records);return JsonSerializer.Serialize(new{cases=records.Count,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant()});
    }
}
