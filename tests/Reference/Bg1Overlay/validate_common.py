"""Source pins and optimizer parity for authored oracle generators."""
import hashlib,json,subprocess
from pathlib import Path
HERE=Path(__file__).resolve().parent

def verify(root, paths):
    pins=json.loads((HERE/'pinned-inputs.json').read_text())
    for p in paths:
        actual=hashlib.sha256((root/p).read_bytes()).hexdigest()
        assert pins[p]==actual, 'Pinned source mismatch: '+p

def compile_pair(source, output):
    for suffix,opt in [('', '-O0'),('.o2','-O2')]:
        subprocess.run(['cc','-std=c11',opt,'-fwrapv',str(source),'-o',str(output)+suffix],check=True)

def observe(command):
    actual=subprocess.check_output(command,text=True)
    optimized=subprocess.check_output([command[0]+'.o2',*command[1:]],text=True)
    assert actual==optimized, 'O0/O2 observations differ: '+str(command)
    return [json.loads(s) for s in actual.splitlines()]
