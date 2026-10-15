#!/usr/bin/env python3
"""Generate bounded trainer identities; all creation uses the shared trainer factory."""
import argparse,  json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
data=(a.root/'content/generated/sevii-ruby-rules/trainers.json').read_bytes()
doc=json.loads(data);q=json.dumps
s='''using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Exact initial Ruby-region teams; shared source creation retains held items and double PID.</summary>
public static class SeviiRubyTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-sevii-ruby-trainers-v1";
    private static readonly ImmutableHashSet<TrainerId> TrainerIds = new HashSet<TrainerId>
    {
'''
for t in doc['trainers']:s+='        new('+q(t['id']['value'])+'),\n'
s+='    }.ToImmutableHashSet();\n    private static readonly ImmutableHashSet<string> BattleScripts = new HashSet<string>(StringComparer.Ordinal)\n    {\n'
for b in doc['battles']:s+='        '+q(b['scriptEntry'])+',\n'
s+='    }.ToImmutableHashSet(StringComparer.Ordinal);\n'
s+='    public static ImmutableArray<TrainerId> AllTrainers { get; } = ['+','.join('new('+q(t['id']['value'])+')' for t in doc['trainers'])+'];\n'
s+='    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves, string HeldItem);\n'
s+='    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party, int BasePrize);\n'
s+='    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>\n    {\n'
for t in doc['trainers']:
 resolved=next(r['party'] for r in doc['resolvedMoves'] if 'pret:'+r['trainer']==t['id']['value'])
 party=[]
 for m,r in zip(t['party'],resolved):
  party.append('new('+','.join([q(m['species']['value']),str(m['sourceSpeciesId']),q(m['speciesName']),str(m['level']),str(m['sourceIv']),q(m['encodedSpeciesName']),q(['pret:'+x for x in r['moves']]),q(m['heldItem'])])+')')
 s+='        [new('+q(t['id']['value'])+')] = new('+','.join([q(t['sourceTrainerName']),str(t['encounterMusicGender']),str(t['aiFlags'])+'u',str(t['doubleBattle']).lower(),q(t['encodedTrainerName']),q(t['items']),'['+','.join(party)+']',str((8 if t['doubleBattle'] else 4)*t['baseMoney']*t['party'][-1]['level'])])+'),\n'
s+='    }.ToImmutableDictionary();\n'
s+='    public static TrainerId DoubleTrainer { get; } = new("pret:TRAINER_CRUSH_KIN_MIK_KIA");\n'
s+='    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(id => id != DoubleTrainer).ToImmutableArray();\n'
s+='''
    public static int BasePrize(SeviiRubyTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("sevii_ruby_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }

    public static TrainerPartyCreationResult Create(SeviiRubyTrainerCatalog trainers,
        SeviiRubyOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(SeviiRubyOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("sevii_ruby_trainer_catalog");
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("sevii_ruby_trainer_species");
            if (member.CustomMoves.Any(m => !opponents.Mechanics.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("sevii_ruby_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, opponents.Mechanics.Catalog, rng, opponents.Mechanics);
        // Party flag bit0 controls custom moves independently of bit1 held items.
        // Logan has original custom moves; all six Black Belt teams use natural moves.
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.Select(m => new PokemonMoveSlot(m,
                opponents.Mechanics.Catalog.Moves[m].Pp, opponents.Mechanics.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }

    public static void ValidateCatalog(SeviiRubyTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 17 || catalog.BattlesByScript.Count != 18)
            throw new NotSupportedException("sevii_ruby_trainer_scope");
        foreach (var id in TrainerIds)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.Party.IsDefaultOrEmpty || trainer.Party.Any(m => m is null || m.CustomMoves.IsDefault ||
                    m.EncodedSpeciesName.IsDefaultOrEmpty) || trainer.Items.IsDefault ||
                trainer.EncodedTrainerName.IsDefaultOrEmpty || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("sevii_ruby_trainer_source");
        }
        foreach (var script in BattleScripts)
        {
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null || battle.ScriptEntry != script)
                throw new NotSupportedException("sevii_ruby_trainer_battle_source");
        }
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("sevii_ruby_trainer_battle_source");
    }
}
'''
path=a.root/'src/Game.Domain/Trainers/SeviiRubyTrainerPartyFactory.cs'
if a.check:
 if path.read_text()!=s:raise ValueError('Factory generation differs')
else:path.write_text(s)
print(json.dumps(dict(checked=a.check,trainers=len(doc['trainers']),battles=len(doc['battles']))))
