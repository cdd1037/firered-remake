#!/usr/bin/env python3
"""Compile exactly the pinned original control/volume C functions, never upstream scripts."""
import hashlib,json,pathlib,subprocess,sys
source=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
expected="037335f4c725d7c9aecdac87066f2002b4bd7e14"
assert subprocess.check_output(["git","rev-parse","HEAD"],cwd=source,text=True).strip()==expected
sourceHashes={"src/m4a.c":"db6bea72be02870e19a9c95f5895c6f34b75af36e6bf7f3b75b10f5b4877fe92","src/m4a_1.s":"348ed832bb30b24fa3ad9cf90242cc708ad972c19dff43b4dd550de4e5716dbf","include/gba/m4a_internal.h":"9e841d8fe72d6af9f2d5a8135f686618e20975132a6d2fc95e6c7604213505e6"}
for path,sha in sourceHashes.items():assert hashlib.sha256((source/path).read_bytes()).hexdigest()==sha,path+" source identity differs"
code=(source/"src/m4a.c").read_text()
def extract(name):
 start=code.index("void "+name+"(");body=code.index("{",start);depth=1;end=body+1
 while depth:
  depth+=(code[end]=="{")-(code[end]=="}");end+=1
 return code[start:end]
functions="\n\n".join(extract(n) for n in ["TrkVolPitSet","m4aMPlayPanpotControl"])
header=r'''#include <stdint.h>
#include <stdio.h>
typedef int32_t s32; typedef uint32_t u32; typedef uint16_t u16; typedef int8_t s8;
#define ID_NUMBER 0x68736d53
#define MPT_FLG_VOLSET 1
#define MPT_FLG_VOLCHG 3
#define MPT_FLG_PITSET 4
#define MPT_FLG_EXIST 0x80
struct MusicPlayerTrack { uint8_t flags,vol,volX,volML,volMR,modT; int8_t pan,panX,modM,bend,bendRange,tune,keyShift,keyShiftX,pitX,keyM; uint16_t pitM; };
struct MusicPlayerInfo { u32 ident; int trackCount; struct MusicPlayerTrack *tracks; };
'''
driver=r'''
int main(void) {
 int ps[]={-64,-32,-1,0,1,32,63}, xs[]={-128,-64,-1,0,1,63,127}, vs[]={1,64,127}, es[]={0,21,64}, vel[]={64,127}, rp[]={-64,0,63};
 for(int p=0;p<7;p++)for(int x=0;x<7;x++)for(int v=0;v<3;v++)for(int e=0;e<3;e++)for(int k=0;k<2;k++)for(int r=0;r<3;r++){
  struct MusicPlayerTrack t={.flags=MPT_FLG_VOLSET,.vol=vs[v],.volX=es[e],.pan=ps[p],.panX=xs[x]};
  struct MusicPlayerInfo m={.ident=ID_NUMBER,.trackCount=1,.tracks=&t};TrkVolPitSet(&m,&t);
  /* ChnVolSetAsm (src/m4a_1.s:1509-1536), gain-byte multiply/shift/saturate. */
  int l=(127-rp[r])*vel[k]*t.volML>>14, rr=(128+rp[r])*vel[k]*t.volMR>>14;
  printf("V %d %d %d %d %d %d %d %d\n",ps[p],xs[x],vs[v],es[e],vel[k],rp[r],l>255?255:l,rr>255?255:rr);
 }
 int masks[]={0,1,2,4,8,5,65535,32768};
 for(int valid=0;valid<2;valid++)for(int mask=0;mask<8;mask++)for(int pan=0;pan<7;pan++){
  struct MusicPlayerTrack t[4]={{.flags=0x80,.panX=7},{.flags=0,.panX=7},{.flags=0xc0,.panX=7},{.flags=0x80,.panX=7}};
  struct MusicPlayerInfo m={.ident=valid?ID_NUMBER:0,.trackCount=4,.tracks=t};
  m4aMPlayPanpotControl(&m,masks[mask],xs[pan]);
  printf("C %d %d %d %d %d %d %d\n",valid,masks[mask],xs[pan],t[0].panX,t[1].panX,t[2].panX,t[3].panX);
 }
 return 0;
}
'''
(out/"oracle.c").write_text(header+functions+driver)
subprocess.run(["cc","-std=c99","-Wall","-Wextra","-Werror","-Wno-unused-parameter",str(out/"oracle.c"),"-o",str(out/"oracle")],check=True)
rows=subprocess.check_output([str(out/"oracle")]);(out/"oracle.tsv").write_bytes(rows)
info=dict(sourceCommit=expected,sourceFile="src/m4a.c",sourceSha256=hashlib.sha256((source/"src/m4a.c").read_bytes()).hexdigest(),functionsSha256=hashlib.sha256(functions.encode()).hexdigest(),tableSha256=hashlib.sha256(rows).hexdigest(),volumeRows=sum(x.startswith(b"V ") for x in rows.splitlines()),controlRows=sum(x.startswith(b"C ") for x in rows.splitlines()),upstreamScriptsExecuted=False,sourceFiles=sourceHashes,externalVolumeVectors=[0,21,64],excluded="Other external volumes and unrelated modulation branches are not covered")
(out/"oracle.json").write_text(json.dumps(info,indent=2)+"\n");print(json.dumps(info))
