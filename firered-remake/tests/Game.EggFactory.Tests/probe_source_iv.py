#!/usr/bin/env python3
"""Bounded original C oracle; reads pinned Git text, never executes reference scripts."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
if len(sys.argv) != 3:
    raise SystemExit("PINNED_REFERENCE_GIT OUTPUT_JSON")
source = subprocess.check_output(["git", "-C", sys.argv[1], "show", COMMIT + ":src/daycare.c"], text=True)

def function(signature):
    start = source.index(signature)
    left = source.index("{", start)
    depth = 1
    end = left + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]

original = "\n\n".join(function(s) for s in [
    "static void RemoveIVIndexFromList(u8 *ivs, u8 selectedIv)",
    "static void InheritIVs(struct Pokemon *egg, struct DayCare *daycare)"])
support = r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
typedef uint8_t u8;
typedef int32_t s32;
#define NUM_STATS 6
#define INHERITED_IV_COUNT 3
#define DAYCARE_MON_COUNT 2
#define NELEMS(x) (sizeof(x)/sizeof((x)[0]))
enum { MON_DATA_HP_IV, MON_DATA_ATK_IV, MON_DATA_DEF_IV, MON_DATA_SPEED_IV, MON_DATA_SPATK_IV, MON_DATA_SPDEF_IV };
struct BoxPokemon { u8 iv[6]; };
struct Pokemon { u8 iv[6]; };
struct DaycareMon { struct BoxPokemon mon; };
struct DayCare { struct DaycareMon mons[2]; };
static uint16_t draws[6];
static int cursor, selected[3], written;
static uint16_t Random(void) { if(cursor>=6) abort(); return draws[cursor++]; }
static u8 GetBoxMonData(struct BoxPokemon *p,int stat) { return p->iv[stat]; }
static void SetMonData(struct Pokemon *p,int stat,u8 *iv) { if(written>=3) abort(); selected[written++]=stat; p->iv[stat]=*iv; }
'''
main = r'''
int main(void) {
  struct DayCare parents={0};
  for(int i=0;i<6;i++) { parents.mons[0].mon.iv[i]=3; parents.mons[1].mon.iv[i]=27; }
  for(int seed=0;seed<128;seed++) {
    uint32_t state=(uint32_t)seed; uint16_t raw[9];
    for(int i=0;i<9;i++) { state=state*0x41c64e6du+0x6073u; raw[i]=state>>16; }
    struct Pokemon egg={{raw[1]&31,(raw[1]>>5)&31,(raw[1]>>10)&31,raw[2]&31,(raw[2]>>5)&31,(raw[2]>>10)&31}};
    for(int i=0;i<6;i++) draws[i]=raw[i+3];
    cursor=written=0; InheritIVs(&egg,&parents);
    if(cursor!=6||written!=3) return 2;
    printf("%d %u %d %d %d",seed,state,selected[0],selected[1],selected[2]);
    for(int i=0;i<6;i++) printf(" %u",egg.iv[i]);
    puts("");
  }
  return 0;
}
'''
with tempfile.TemporaryDirectory(prefix="firered-iv-oracle-") as directory:
    path = Path(directory)
    (path / "probe.c").write_text(support + original + main)
    subprocess.run(["cc", "-std=c11", "-O0", "-Wall", "-Wextra", "-Werror", str(path / "probe.c"), "-o", str(path / "probe")], check=True)
    lines = subprocess.check_output([str(path / "probe")], text=True).splitlines()
rows = []
for line in lines:
    v = list(map(int, line.split()))
    rows.append({"seed": v[0], "finalRngSeed": v[1], "selectedStats": v[2:5], "finalIvs": v[5:]})
assert len(rows) == 128
result = {"passed": True, "sourceCommit": COMMIT, "sourcePath": "src/daycare.c",
          "sourceSha256": hashlib.sha256(source.encode()).hexdigest(),
          "extractedFunctionsSha256": hashlib.sha256(original.encode()).hexdigest(),
          "vectors": len(rows), "rows": rows}
output = Path(sys.argv[2]); output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(result, indent=2) + "\n")
print(f"PASS {len(rows)} original C receipt IV vectors")
