"""Project import-only cue recipes onto the runtime synthesis contract."""
from runtime_json import runtime_json_bytes


def runtime_cue(cue):
    result = dict(id=cue['id'], sourceSymbol=cue['sourceSymbol'],
                  sourcePlayer=cue['sourcePlayer'], priority=cue['priority'],
                  sourceCounter=cue['sourceCounter'], songAddress=cue['songAddress'],
                  species=cue['species'], looping=cue['looping'])
    if 'cryMode' in cue:
        result['cryMode'] = cue['cryMode']
    return result


def runtime_manifest(recipe):
    result = dict(schemaVersion=recipe['schemaVersion'],
                  cues=[runtime_cue(cue) for cue in recipe['cues']])
    if 'inheritedCues' in recipe:
        result['inheritedCues'] = [runtime_cue(cue) for cue in recipe['inheritedCues']]
    return result


def runtime_manifest_bytes(recipe):
    return runtime_json_bytes(runtime_manifest(recipe))
