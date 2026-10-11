#!/usr/bin/env python3
"""Reject missing, empty, partial, failed, or mismatched focused capture evidence."""
import argparse,hashlib,json,pathlib,xml.etree.ElementTree as ET
p=argparse.ArgumentParser();p.add_argument('prefix',type=pathlib.Path);p.add_argument('oracle',type=pathlib.Path);a=p.parse_args()
report=json.loads(a.prefix.with_suffix('.json').read_text());xml=ET.parse(a.prefix.with_suffix('.xml')).getroot();log=a.prefix.with_suffix('.log').read_text()
assert report['passed'] is True and report['groups']==12 and report['failed']==0 and report['checks']>40000
assert report['runtime']=='10.0.12' and report['legacyFullResults']==872 and report['sourceCResults']==3011
assert len(report['results'])==12 and all(x['passed'] is True and x['error'] is None for x in report['results'])
assert xml.tag=='testsuite' and xml.attrib['tests']=='12' and xml.attrib['failures']=='0'
assert len(xml.findall('testcase'))==12 and not xml.findall('.//failure') and not xml.findall('.//skipped')
assert log.rstrip().endswith(f"SOURCE_CAPTURE_BALLS_PASS groups=12 checks={report['checks']} failures=0")
manifest=json.loads((a.oracle/'manifest.json').read_text());assert manifest['passed'] is True and manifest['count']==3011 and manifest['debugOptimizedSanitizedAgree'] is True
vectors=json.loads((a.oracle/'source-vectors.json').read_text());assert len(vectors['vectors'])==3011
assert hashlib.sha256((a.oracle/'oracle.c').read_bytes()).hexdigest()==manifest['oracleSha256']
print(json.dumps(dict(passed=True,groups=12,checks=report['checks'],xmlCases=12,tailMarker=True,sourceCResults=3011,legacyFullResults=872,hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for f in [a.prefix.with_suffix('.json'),a.prefix.with_suffix('.xml'),a.prefix.with_suffix('.log'),a.oracle/'manifest.json',a.oracle/'oracle.c']}),indent=2))
