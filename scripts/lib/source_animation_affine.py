"""Shared source task-affine command extraction for battle animation producers."""
import re

def task_affine_program(source,name):
 match=re.search(r'const union AffineAnimCmd '+re.escape(name)+r'\[\] =\s*\{(.*?)\};',source,re.S)
 if match is None:raise ValueError('Missing task affine program '+name)
 commands=[]
 for row in match[1].splitlines():
  row=row.split('//',1)[0].strip().rstrip(',')
  if not row:continue
  command=dict(op='',x=0,y=0,rotation=0,duration=0,target=0,count=0)
  frame=re.fullmatch(r'AFFINEANIMCMD_FRAME\(([^)]+)\)',row)
  control=re.fullmatch(r'AFFINEANIMCMD_(JUMP|LOOP)\(([^)]+)\)',row)
  if frame:
   x,y,rotation,duration=[int(v.strip(),0) for v in frame[1].split(',')]
   if not -32768<=x<=32764 or not -32768<=y<=32767 or not -128<=rotation<=255 or not -128<=duration<=255:raise ValueError('Task affine frame outside source representation')
   # Both fields are u8 in the source union; Teleport spells duration 200 as -56.
   command.update(op='frame',x=x,y=y,rotation=rotation&255,duration=duration&255)
  elif control:
   value=int(control[2],0)
   if not 0<=value<=32767:raise ValueError('Task affine control outside source representation')
   command.update(op=control[1].lower());command['target' if control[1]=='JUMP' else 'count']=value
  elif row=='AFFINEANIMCMD_END':command['op']='end'
  else:raise ValueError('Unsupported task affine command '+row)
  commands.append(command)
 if not commands or len(commands)>256 or commands[-1]['op'] not in {'end','jump'}:raise ValueError('Unterminated task affine program')
 if any(c['op']=='jump' and c['target']>=len(commands) for c in commands):raise ValueError('Invalid task affine jump')
 return commands
