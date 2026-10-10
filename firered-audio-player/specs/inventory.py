#!/usr/bin/env python3
"""Read-only original-ROM inventory for the isolated FireRed audio MVP.
No playback code or third-party synthesizer implementation is used.
"""
import argparse, collections, hashlib, json, pathlib, re, struct
P=argparse.ArgumentParser(); P.add_argument('--reference',type=pathlib.Path,default=pathlib.Path(__file__).resolve().parents[2]/'audio-reference-build/pokefirered');P.add_argument('--out',type=pathlib.Path)
a=P.parse_args();rom=(a.reference/'pokefirered.gba').read_bytes(); mp=(a.reference/'pokefirered.map').read_text()
def u8(p):return rom[p-0x08000000]
def u32(p):return struct.unpack_from('<I',rom,p-0x08000000)[0]
def sym(name):return int(re.search(r'0x([0-9a-f]{8})\s+'+re.escape(name)+r'\s*$',mp,re.M)[1],16)
clock=list(range(25))+[28,30,32,36,40,42,44,48,52,54,56,60,64,66,68,72,76,78,80,84,88,90,92,96]
commands={0xb1:'FINE',0xb2:'GOTO',0xb3:'PATT',0xb4:'PEND',0xbb:'TEMPO',0xbc:'KEYSH',0xbd:'VOICE',0xbe:'VOL',0xbf:'PAN',0xc0:'BEND',0xc1:'BENDR',0xc2:'LFOS',0xc3:'LFODL',0xc4:'MOD',0xc5:'MODT',0xc8:'TUNE',0xce:'EOT'}
def tone(p):
 t,k,l,pan,w,A,D,S,R=struct.unpack_from('<4BI4B',rom,p-0x8000000)
 out={'address':hex(p),'type':t,'key':k,'length':l,'pan_sweep':pan,'pointer_or_duty':hex(w),'adsr':[A,D,S,R]}
 if t&0xc0:out['key_split_map']=hex(u32(p+8));return out
 if t&7==0:
  wt,ws,f,ls,n=struct.unpack_from('<HHIII',rom,w-0x8000000)
  out['pcm']={'address':hex(w),'type':wt,'status':ws,'frequency_word':f,'c60_hz':f/1024,'loop_start':ls,'sample_count':n,'header_and_data_bytes':16+n,'data_sha256':hashlib.sha256(rom[w-0x8000000+16:w-0x8000000+16+n]).hexdigest()}
 return out
songs=[]
for name,sid in [('mus_pallet',300),('se_select',5)]:
 h=sym(name);n=u8(h);vg=u32(h+4);song={'name':name,'id':sid,'header':hex(h),'header_bytes':rom[h-0x8000000:h-0x8000000+8+4*n].hex(' '),'priority':u8(h+2),'reverb':u8(h+3),'voicegroup':hex(vg),'tracks':[]};used={};subtones={}
 for ti in range(n):
  p=u32(h+8+ti*4);start=p;t=0;key=vel=voice=running=0;notes=[];controls=[];ops=collections.Counter();seen=set();loop=None
  for step in range(10000):
   if p in seen:loop=hex(p);break
   seen.add(p);cmd=u8(p)
   if cmd>=128:
    p+=1
    if cmd>=0xbd:running=cmd
   else:cmd=running
   if cmd>=0xcf:
    dur=clock[cmd-0xcf]
    if u8(p)<128:
     key=u8(p);p+=1
     if u8(p)<128:
      vel=u8(p);p+=1
      if u8(p)<128:dur+=u8(p);p+=1
    ops['N'+str(clock[cmd-0xcf])]+=1;tp=vg+voice*12;vt=tone(tp);used[str(voice)]=vt
    resolved=tp
    if vt['type']&0x40:resolved=u32(tp+4)+12*u8(u32(tp+8)+key);subtones[hex(resolved)]=tone(resolved)
    notes.append({'tick':t,'duration':dur,'key':key,'velocity':vel,'voice':voice,'resolved_tone':hex(resolved)})
   elif 0x80<=cmd<=0xb0:ops['W'+str(clock[cmd-0x80])]+=1;t+=clock[cmd-0x80]
   elif cmd==0xb1:ops['FINE']+=1;break
   elif cmd==0xb2:ops['GOTO']+=1;p=u32(p)
   elif cmd in commands:
    ops[commands[cmd]]+=1;x=u8(p);p+=1;controls.append({'tick':t,'command':commands[cmd],'value':x,'address':hex(p-2)})
    if cmd==0xbd:voice=x
   else:raise ValueError(f'Unsupported command {cmd:#x} at {p:#x}')
  song['tracks'].append({'index':ti+1,'address':hex(start),'ticks':t,'loop_target':loop,'note_count':len(notes),'opcodes':dict(sorted(ops.items())),'notes':notes,'controls':controls})
 song['voices']=used;song['resolved_key_split_tones']=subtones;songs.append(song)
output={'source_sha1':hashlib.sha1(rom).hexdigest(),'source_sha256':hashlib.sha256(rom).hexdigest(),'clock_table':clock,'songs':songs};text=json.dumps(output,indent=2)
if a.out:a.out.write_text(text+'\n')
else:print(text)
