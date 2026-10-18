"""Shared source AnimCmd normalization; finite loops retain their native boundary."""
import re

def frame_program(body):
 frames=[];loop=-1;controls=[];previous_loop=0;raw_frames={};jump=None;terminated=False
 rows=[r.strip().rstrip(',') for r in body.splitlines() if r.strip()]
 if len(rows)>256:raise ValueError('Sprite command count exceeds source byte index')
 for index,row in enumerate(rows):
  if terminated:raise ValueError('Sprite frame command follows termination')
  frame=re.fullmatch(r'ANIMCMD_FRAME\((\d+),\s*(\d+)((?:, \.[hv]Flip = TRUE)*)\)',row)
  command=re.fullmatch(r'ANIMCMD_(LOOP|JUMP)\((\d+)\)',row)
  if frame:
   if not 0<=int(frame[2])<=63:raise ValueError('Sprite duration exceeds source bitfield')
   raw_frames[index]=len(frames);frames.append(dict(tile=int(frame[1]),duration=int(frame[2]),hFlip='.hFlip' in frame[3],vFlip='.vFlip' in frame[3]))
  elif command:
   value=int(command[2])
   if value>63:raise ValueError('Sprite control exceeds source bitfield')
   if command[1]=='LOOP':
    # Adjacent controls have source scan/counter interactions, not a frame loop.
    # Admit bounded frame regions only; zero-count markers bound the next region.
    if len(frames)<=previous_loop:raise ValueError('Sprite loops must enclose frame commands')
    if value:controls.append(dict(at=len(frames),start=previous_loop,count=value))
    previous_loop=len(frames)
   else:jump=value;terminated=True
  elif row=='ANIMCMD_END':terminated=True
  else:raise ValueError('Unimplemented frame operation '+row)
 if not frames or 0 not in raw_frames or not terminated:raise ValueError('Frame animation has no accepted termination/loop')
 if jump is not None:
  if jump not in raw_frames:raise ValueError('Sprite jump must target a frame command')
  loop=raw_frames[jump]
 return frames,loop,controls
