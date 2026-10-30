import hashlib
import json
import subprocess
import zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Delivery'
OUT.mkdir(exist_ok=True)

def archive(path,items,prefix):
    total=0
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as target:
        for source,relative in items:
            if not source.is_file():
                raise FileNotFoundError(source)
            target.write(source,prefix+'/'+relative.replace('\\','/'))
            total+=source.stat().st_size
    print(path.name,path.stat().st_size,'bytes;',total,'uncompressed')

tracked=subprocess.run(['git','ls-files','-z'],cwd=ROOT,capture_output=True,check=True).stdout.decode().split('\0')
archive(OUT/'BRP_Source.zip',[(ROOT/name,name) for name in tracked if name], 'BRP_Source')
player=OUT/'Windows'
files=[]
for path in sorted(player.rglob('*')):
    relative=path.relative_to(player)
    if path.is_file() and not any('BackUpThisFolder' in part for part in relative.parts) and path.suffix!='.pdb':
        files.append((path,str(relative)))
for name in ['CONTROLS.md','VERIFICATION.md']:
    path=ROOT/'Docs'/name
    if path.exists(): files.append((path,name))
archive(OUT/'BRP_Windows.zip',files,'BRP_Windows')
checks={}
for name in ['BRP_Source.zip','BRP_Windows.zip','BRP_Gameplay.mp4']:
    path=OUT/name
    if path.exists():
        digest=hashlib.sha256()
        with path.open('rb') as file:
            for data in iter(lambda:file.read(1024*1024),b''): digest.update(data)
        checks[name]={'bytes':path.stat().st_size,'sha256':digest.hexdigest()}
(OUT/'checksums.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print(json.dumps(checks,indent=2))
