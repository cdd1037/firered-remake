#!/usr/bin/env python3
"""Bounded oracle: pinned C functions versus the new Domain file, without a product build.

Usage: python3 scripts/verify-magikarp-size-source.py REFERENCE MAIN_PROJECT [--output RESULT_JSON]
The original data/functions are compiled in a temporary harness, never an upstream script.
The C# file is compiled alone against the existing main Game.Domain.dll and net10 references.
"""
import argparse
import json
import pathlib
import re
import subprocess
import tempfile

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
ROOT = pathlib.Path(__file__).resolve().parents[1]


def run(args, **kwargs):
    return subprocess.check_output([str(a) for a in args], **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=pathlib.Path)
    parser.add_argument('main_project', type=pathlib.Path)
    parser.add_argument('--output', type=pathlib.Path)
    parser.add_argument('--integrated-domain', type=pathlib.Path, help='Use the exact already-built Domain assembly; do not compile the new type again')
    args = parser.parse_args()
    source = run(['git', 'show', COMMIT + ':src/pokemon_size_record.c'], cwd=args.reference)
    source = source.decode()
    table = re.search(r'struct UnknownStruct\n.*?\n\};\n\nstatic const struct UnknownStruct sBigMonSizeTable\[\] =\n\{.*?\n\};', source, re.S)[0]
    functions = source[source.index('static u32 GetMonSizeHash'):source.index('static void FormatMonSizeRecord')]
    cases = [(0, [0] * 6), (0xffffffff, [31] * 6), (0x12345678, [1, 2, 3, 4, 5, 6]),
             (0x89abcdef, [31, 0, 15, 16, 30, 17]), (0x00001234, [0, 15, 31, 16, 1, 30]),
             (0xabcd1234, [16, 31, 15, 0, 17, 14]), (0xff00, [0] * 6), (0x00ff, [0] * 6)]
    c_rows = ',\n'.join('{' + str(pid) + 'u, {' + ','.join(map(str, ivs)) + '}}' for pid, ivs in cases)
    c = '''#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef uint64_t u64;
enum { MON_DATA_PERSONALITY, MON_DATA_HP_IV, MON_DATA_ATK_IV, MON_DATA_DEF_IV,
       MON_DATA_SPEED_IV, MON_DATA_SPATK_IV, MON_DATA_SPDEF_IV };
struct Pokemon { u32 pid; u32 iv[6]; };
static u32 GetMonData(struct Pokemon *p, int key) { return key ? p->iv[key-1] : p->pid; }
static u16 SpeciesToNationalPokedexNum(u16 species) { return species; }
static u32 GetPokedexHeightWeight(u16 species, int weight) { (void)species; (void)weight; return 9; }
''' + table + '\n' + functions + '''
static void emit(u32 value) { for (int i=0; i<4; i++) putchar((value >> (8*i)) & 255); }
int main(void) {
  for (u32 i=0; i<65536; i++) emit(GetMonSize(129, (u16)i));
  struct Pokemon cases[] = {''' + c_rows + '''};
  for (unsigned i=0; i<sizeof(cases)/sizeof(cases[0]); i++) emit(GetMonSizeHash(&cases[i]));
  return 0;
}
'''
    cs_rows = ',\n'.join('(' + str(pid) + 'u, new SixStats(' + ','.join(map(str, ivs)) + '))' for pid, ivs in cases)
    cs = '''global using System;
global using System.Linq;
using System.IO;
using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.Ids;
static class Program {
 static void Need(bool value, string name) { if (!value) throw new Exception(name); }
 static PartyPokemon Mon(uint pid, bool egg=false, bool wrong=false) {
  var p = new PokemonInstance(new SpeciesId(wrong ? "pret:SPECIES_PIDGEY" : "pret:SPECIES_MAGIKARP"),
   wrong ? 16 : 129, 20, 8000, 1, pid, Nature.Hardy, SixStats.Zero, SixStats.Zero,
   new BattleStats(20,10,10,10,10,10), 0, PokemonGender.Male, 0, "ABILITY_SWIFT_SWIM", 70, []);
  return new PartyPokemon(1,p,"","",0,2,"",20,4,"ITEM_POKE_BALL","ITEM_NONE") { IsEgg=egg };
 }
 static void Main() {
  Need(Environment.Version.Major==10,"runtime10");
  var m=Mon(0); var high=Mon(65535);
  Need(MagikarpSizeRecordRules.DefaultRecord==0,"default0");
  Need(MagikarpSizeRecordRules.SizeTenthsCentimeters(0)==261,"default261");
  Need(MagikarpSizeRecordRules.FormatInches(261)=="10.2","imperial truncation");
  Need(MagikarpSizeRecordRules.FormatCentimeters(261)=="26.1","metric display");
  Need((int)MagikarpSizeRecordRules.Compare(6,m,0).Outcome==0,"cancel");
  Need((int)MagikarpSizeRecordRules.Compare(-1,m,0).Outcome==0,"cancel sentinel");
  Need((int)MagikarpSizeRecordRules.Compare(0,Mon(0,true),0).Outcome==1,"egg");
  Need((int)MagikarpSizeRecordRules.Compare(0,Mon(0,false,true),0).Outcome==1,"wrong species");
  Need((int)MagikarpSizeRecordRules.Compare(0,null,0).Outcome==1,"empty slot");
  Need((int)MagikarpSizeRecordRules.Compare(0,m,65535).Outcome==2,"smaller");
  Need((int)MagikarpSizeRecordRules.Compare(0,high,0).Outcome==3,"new record");
  Need((int)MagikarpSizeRecordRules.Compare(0,m,0).Outcome==4,"equal");
  Need(!MagikarpSizeRecordRules.Compare(0,Mon(256),0).RecordChanged,"equal length distinct hashes");
  var firstTie=MagikarpSizeRecordRules.Evaluate(0,m,0,false);
  Need(firstTie.AttemptNetBallGift && firstTie.RecordFlagAfterComparison && !firstTie.Comparison.RecordChanged,"first tie reward");
  var firstSmaller=MagikarpSizeRecordRules.Evaluate(0,m,65535,false);
  Need(firstSmaller.AttemptNetBallGift && firstSmaller.Comparison.UpdatedRecord==65535,"first smaller keeps record");
  Need(!MagikarpSizeRecordRules.Evaluate(0,m,0,true).AttemptNetBallGift,"repeat tie no gift");
  var record=MagikarpSizeRecordRules.Evaluate(0,high,0,true);
  Need(record.AttemptNetBallGift && record.Comparison.UpdatedRecord==65535 && record.RecordFlagAfterComparison,"record state before gift");
  Need(!MagikarpSizeRecordRules.Evaluate(6,m,0,false).RecordFlagAfterComparison,"cancel no flag");
  Need(MagikarpSizeRecordRules.FormatInches(262)==MagikarpSizeRecordRules.FormatInches(263),"display collision");
  Need((int)MagikarpSizeRecordRules.Compare(0,Mon(768),2).Outcome==3,"compare before display");
  bool rejected=false; try { MagikarpSizeRecordRules.Hash(0,new SixStats(32,0,0,0,0,0)); } catch(ArgumentOutOfRangeException) { rejected=true; }
  Need(rejected,"malformed IV rejected");
  using var w=new BinaryWriter(Console.OpenStandardOutput());
  for(int i=0;i<65536;i++) w.Write(MagikarpSizeRecordRules.SizeTenthsCentimeters((ushort)i));
  var cases=new (uint Pid,SixStats Ivs)[] {''' + cs_rows + '''};
  foreach(var row in cases) w.Write((uint)MagikarpSizeRecordRules.Hash(row.Pid,row.Ivs));
 }
}
'''
    toolchain = pathlib.Path((args.main_project / '.tools/toolchain-root').read_text().strip())
    dotnet = toolchain / 'dotnet/dotnet'
    sdk = toolchain / 'dotnet/sdk/10.0.401/Roslyn/bincore/csc.dll'
    refs = toolchain / 'dotnet/packs/Microsoft.NETCore.App.Ref/10.0.12/ref/net10.0'
    domain = args.integrated_domain or args.main_project / 'src/Game.Domain/bin/Debug/net10.0/Game.Domain.dll'
    assert sdk.is_file() and refs.is_dir() and domain.is_file()
    with tempfile.TemporaryDirectory(prefix='magikarp-size-oracle-') as temp:
        folder = pathlib.Path(temp)
        (folder/'oracle.c').write_text(c)
        run(['cc','-std=c11','-O0','-o',folder/'oracle',folder/'oracle.c'])
        expected = run([folder/'oracle'])
        (folder/'Program.cs').write_text(cs)
        (folder/'Game.Domain.dll').write_bytes(domain.read_bytes())
        (folder/'check.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm':'net10.0','framework':{'name':'Microsoft.NETCore.App','version':'10.0.12'}}}))
        compiler = [dotnet,sdk,'-nologo','-target:exe','-langversion:14','-nullable:enable','-warnaserror+',
                    '-out:'+str(folder/'check.dll'),'-r:'+str(domain),*[ '-r:'+str(p) for p in sorted(refs.glob('*.dll')) ],
                    *([] if args.integrated_domain else [ROOT/'src/Game.Domain/Pokemon/MagikarpSizeRecordRules.cs']),folder/'Program.cs']
        run(compiler)
        actual = run([dotnet,folder/'check.dll'])
        assert expected == actual, 'C# output differs from extracted pinned C source'
    values = [int.from_bytes(expected[i:i+4], 'little') for i in range(0,len(expected),4)]
    boundaries = [0,1,9,10,109,110,309,310,709,710,2709,2710,7709,7710,17709,17710,
                  32709,32710,47709,47710,57709,57710,62709,62710,64709,64710,65209,65210,
                  65409,65410,65509,65510,65535]
    result = dict(sourceCommit=COMMIT, sourcePath='src/pokemon_size_record.c',
                  sdk='10.0.401', runtime='10.0.12', fullProductBuild=False, integratedDomain=bool(args.integrated_domain), comparedSizeHashes=65536,
                  comparedPersonalityIvVectors=len(cases),
                  boundaryVectors=[dict(record=i,sizeTenthsCentimeters=values[i]) for i in boundaries],
                  hashVectors=[dict(personality=pid,ivs=ivs,expectedHash=values[65536+i]) for i,(pid,ivs) in enumerate(cases)],
                  comparisonAndScriptChecksPassed=True, allPassed=True)
    if args.output:
        args.output.parent.mkdir(parents=True,exist_ok=True)
        args.output.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))


if __name__ == '__main__':
    main()
