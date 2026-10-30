import json
import mimetypes
import os
import subprocess
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SLUG='3123412/Thrust-Vector-BRP-Laboratory'
TAG='v1.0.0'
API='https://api.github.com/repos/'+SLUG
ASSETS=['BRP_Windows.zip','BRP_Source.zip','BRP_Gameplay.mp4','checksums.json']
for name in ASSETS:
    path=ROOT/'Delivery'/name
    if not path.is_file() or path.stat().st_size==0:
        raise SystemExit('Missing or empty release asset: '+name)
process=subprocess.run(['git','credential','fill'],input='protocol=https\nhost=github.com\n\n',text=True,capture_output=True,cwd=ROOT,env={**os.environ,'GIT_TERMINAL_PROMPT':'0','GCM_INTERACTIVE':'never'})
if process.returncode:
    raise SystemExit('Git credentials unavailable')
credential=dict(line.split('=',1) for line in process.stdout.splitlines() if '=' in line)
secret=credential.get('password')
if not secret: raise SystemExit('Git credential has no password')
HEADERS={'Authorization':'Bearer '+secret,'Accept':'application/vnd.github+json','X-GitHub-Api-Version':'2026-03-10','User-Agent':'BRP-release'}

def request(url,method='GET',data=None,headers=None):
    req=urllib.request.Request(url,data=data,method=method,headers={**HEADERS,**(headers or {})})
    with urllib.request.urlopen(req,timeout=300) as response:
        return json.load(response)

try:
    release=request(API+'/releases/tags/'+TAG)
except urllib.error.HTTPError as error:
    if error.code!=404: raise SystemExit('Release lookup failed: HTTP '+str(error.code))
    body={'tag_name':TAG,'target_commitish':'main','name':'Вектор Тяги: Лаборатория BRP / 1.0','body':'Windows-сборка, видео и исходники.\n\nРаспакуйте BRP_Windows.zip и запустите BRP.exe. Для исходников нужна Unity 6000.6.7f1. Управление находится в CONTROLS.md.','draft':False,'prerelease':False}
    release=request(API+'/releases','POST',json.dumps(body).encode(),{'Content-Type':'application/json'})
upload=release['upload_url'].split('{')[0]
if urllib.parse.urlsplit(upload).hostname!='uploads.github.com': raise SystemExit('Unexpected upload host')
existing={asset['name']:asset for asset in release['assets']}
for name in ASSETS:
    path=ROOT/'Delivery'/name
    if not path.is_file(): raise FileNotFoundError(path)
    if name in existing:
        if existing[name]['size']!=path.stat().st_size: raise SystemExit('Existing release asset differs: '+name)
        print('already uploaded',name)
        continue
    media=mimetypes.guess_type(name)[0] or 'application/octet-stream'
    with path.open('rb') as file:
        asset=request(upload+'?name='+urllib.parse.quote(name),'POST',file,{'Content-Type':media,'Content-Length':str(path.stat().st_size)})
    print('uploaded',asset['name'],asset['size'],flush=True)
print(release['html_url'])
