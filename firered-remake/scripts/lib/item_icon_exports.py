"""Semantic shared item images, named by upstream icon and palette (no runtime lookup)."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from pathlib import PurePosixPath

PREFIX = "content/generated/sprites/item-icons/"

def item_icon_path(image, palette):
    icon = PurePosixPath(image).stem.replace("_", "-")
    colors = PurePosixPath(palette).stem.replace("_", "-")
    return PREFIX + icon + ("-" + colors if icon != colors else "") + ".png"

def output_materials(package_directory, outputs):
    """Publish package data and transient icons under one explicit generated tree.

    A single-file package is passed as its JSON destination. Directory packages
    keep their local filenames. Transient semantic icons are packed afterward.
    """
    from pathlib import Path
    package = Path(package_directory)
    generated = next((p for p in (package, *package.parents) if p.name == 'generated'), None)
    if generated is None:
        raise ValueError('Output must be inside a generated resource tree: ' + str(package))
    material = {}
    for name, data in outputs.items():
        if name.startswith(PREFIX):
            target = (generated, name.removeprefix('content/generated/'))
        elif package.suffix == '.json':
            if not name.endswith('.json') or any(key[0] == package.parent and key[1] == package.name for key in material):
                raise ValueError('Single-file package must contain exactly one JSON document')
            target = (package.parent, package.name)
        else:
            target = (package, name)
        material[target] = data
    return material

def decode_icon_page(png, icon):
    """Importer-only logical image read, using Pillow and explicit atlas metadata."""
    import io
    from PIL import Image
    with Image.open(io.BytesIO(png)) as source:
        image=source.convert('RGBA')
        if icon.get('region'):
            r=icon['region'];x,y,w,h=(r[k] for k in ('x','y','width','height'))
            if x<0 or y<0 or x+w>image.width or y+h>image.height:
                raise ValueError('Inherited icon region outside page')
            image=image.crop((x,y,x+w,y+h))
        if image.size != (icon['width'],icon['height']):raise ValueError('Inherited icon dimensions differ')
        return image

# These families use the three typed inventory-icon records. Other presentation
# atlases retain their own cohorts/lifetimes; they are not swept into this page.
ATLAS_PATH = 'content/generated/sprites/items.png'


def pack_project(project):
    """Finalize inventory imports into one bounded, exact-RGBA native PNG page.

    Run after individual source importers. Existing atlas regions are valid input,
    so one freshly reimported family can be combined with the retained cohort.
    Metadata and output lists stay in their existing documents; no lookup sidecar.
    """
    from pathlib import Path
    import hashlib
    import io
    import json
    from PIL import Image
    from presentation_atlas import pack
    project = Path(project)
    generated = project / 'content/generated'
    documents = {p: json.loads(p.read_text()) for p in generated.rglob('*.json')}
    def nodes(value):
        if isinstance(value, dict):
            yield value
            for child in value.values(): yield from nodes(child)
        elif isinstance(value, list):
            for child in value: yield from nodes(child)
    def absolute(document, value):
        return project / value if value.startswith('content/generated/') else document.parent / value
    rows = []
    images = {}
    originals = set()
    old_regions = {}
    inventory_roots = ('data/items/', 'data/machine-learning/', 'data/services/', 'data/regions/saffron-gym/',
        'maps/regions/city-services/', 'maps/regions/good-rod-service/', 'maps/regions/super-rod-service/',
        'maps/regions/npc-trade-elyssa/', 'ui/vermilion-gym-assets/', 'ui/npc-trade-mail/')
    for document, data in documents.items():
        inventory_document = document.relative_to(generated).as_posix().startswith(inventory_roots)
        for row in nodes(data):
            field = 'pngPath' if 'pngPath' in row else 'texturePath'
            if row.get('width') != 24 or row.get('height') != 24 or field not in row:continue
            if not (inventory_document and isinstance(row.get('item'), dict) or row[field] == ATLAS_PATH and row.get('region')):continue
            path = absolute(document, row[field])
            with Image.open(path) as source:
                image = source.convert('RGBA')
                if 'region' in row:
                    box = row['region']; x,y,w,h = (box[k] for k in ('x','y','width','height'))
                    if w != 24 or h != 24 or x < 0 or y < 0 or x+w > image.width or y+h > image.height:
                        raise ValueError('Item atlas region outside page: ' + str(document))
                    image = image.crop((x,y,x+w,y+h))
                elif image.size != (24,24): raise ValueError('Item icon size: ' + str(path))
            pixels = image.tobytes()
            key = hashlib.sha256(pixels).hexdigest()
            if key in images and images[key].tobytes() != pixels: raise ValueError('Item pixel identity collision')
            images[key] = image
            region = row.get('region')
            token = (str(path), tuple(region[k] for k in ('x','y','width','height')) if region else None)
            old_regions[token] = key
            rows.append((row,field,key))
            originals.add(path)
    if not rows: raise ValueError('No inventory icons found')
    page, regions = pack(sorted(images.items()), ATLAS_PATH, width=416)
    regions = {r['id']: r['region'] for r in regions}
    # Capture old standalone paths before editing any document. Atlas aliases have
    # separate rectangles, so their identity includes the old region, never path only.
    standalone = {p for p in originals if p != project / ATLAS_PATH}
    changed = []
    for document, data in documents.items():
        before = json.dumps(data, sort_keys=True)
        for row in nodes(data):
            for field,region_field in (('pngPath','region'),('texturePath','region'),('tmTexture','tmRegion')):
                if field not in row or not isinstance(row[field], str): continue
                path = absolute(document,row[field]); old = row.get(region_field)
                token = (str(path), tuple(old[k] for k in ('x','y','width','height')) if old else None)
                if token in old_regions:
                    if field!='tmTexture' and (row.get('width') != 24 or row.get('height') != 24):
                        raise ValueError('Inventory image reference lacks dimensions: ' + str(document))
                    row[field] = ATLAS_PATH; row[region_field] = regions[old_regions[token]]
            for field,value in list(row.items()):
                if isinstance(value,str) and absolute(document,value) in originals:
                    row[field]=ATLAS_PATH
            for collection in ('outputs','inheritedInputs','reusedInputs','inputs'):
                if not isinstance(row.get(collection),list):continue
                outputs=[];seen=set()
                for output in row[collection]:
                    if isinstance(output,dict) and isinstance(output.get('path'),str):
                        if absolute(document,output['path']) in originals: output['path']=ATLAS_PATH
                        identity=output['path']
                        if identity in seen and (collection=='outputs' or identity==ATLAS_PATH):continue
                        seen.add(identity)
                    outputs.append(output)
                row[collection]=outputs
        if json.dumps(data,sort_keys=True) != before:
            changed.append((document,(json.dumps(data,ensure_ascii=False,sort_keys=True,separators=(',',':'))+'\n').encode()))
    target=project/ATLAS_PATH;target.parent.mkdir(parents=True,exist_ok=True)
    if not target.exists() or target.read_bytes()!=page: target.write_bytes(page)
    for path,data in changed:path.write_bytes(data)
    # Only the known inventory inputs are retired, after every document and page
    # has been produced. Reimporting can safely recreate them before the next pass.
    for path in standalone:
        path.unlink()
        parent=path.parent
        while parent != generated and not any(parent.iterdir()):parent.rmdir();parent=parent.parent
    return dict(iconRecords=len(rows),uniqueImages=len(images),atlasPages=1,
        atlasSize=[416,Image.open(io.BytesIO(page)).height],pngBytes=len(page),
        retiredPngFiles=len(standalone),updatedDocuments=len(changed))


if __name__ == '__main__':
    import argparse
    import json
    from pathlib import Path
    parser=argparse.ArgumentParser(description='Pack generated inventory icons after any regional import.')
    parser.add_argument('--project-root',type=Path,default=_PROJECT_ROOT)
    print(json.dumps(pack_project(parser.parse_args().project_root),sort_keys=True))
