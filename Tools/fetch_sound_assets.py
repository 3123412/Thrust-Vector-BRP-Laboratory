import io
import json
import http.cookiejar
import shutil
import subprocess
import sys
import urllib.request
import zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools/.packages'))
import imageio_ffmpeg

SOURCE=ROOT/'SourceAudio'
OUT=ROOT/'Assets/BRP/Resources/BRP/Audio'
SOURCE.mkdir(exist_ok=True)
OUT.mkdir(parents=True,exist_ok=True)
opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

def pack(name):
    dest=SOURCE/name
    if not dest.exists():
        url='https://kenney.nl/assets/'+name
        opener.open(url,timeout=30).read()
        request=urllib.request.Request('https://kenney.nl/download-link/'+('assets/'+name).encode().hex(),headers={'Referer':url,'Accept':'application/json','Cache-Control':'no-cache'})
        link=json.loads(opener.open(request,timeout=30).read())['url']
        data=opener.open(link,timeout=60).read()
        with zipfile.ZipFile(io.BytesIO(data)) as z:
            for entry in z.infolist():
                path=(dest/entry.filename).resolve()
                if not path.is_relative_to(dest.resolve()):raise ValueError('Archive path')
            z.extractall(dest)
    print(name,len(list(dest.rglob('*.ogg'))))
    return dest

interface=pack('interface-sounds')
scifi=pack('sci-fi-sounds')
impact=pack('impact-sounds')
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()

def find(folder,name):
    items=list(folder.rglob(name+'.ogg'))
    if not items:raise FileNotFoundError(name)
    return items[0]

mapping={
    'click':(interface,'click_003'),
    'hover':(interface,'switch_001'),
    'snap':(impact,'impactMetal_light_002'),
    'error':(interface,'error_005'),
    'success':(interface,'confirmation_002'),
    'separate':(scifi,'doorOpen_001'),
    'abort':(interface,'error_008'),
    'engine_low':(scifi,'spaceEngineLow_000'),
    'ambience':(scifi,'computerNoise_001'),
    'explosion':(scifi,'explosionCrunch_003'),
    'mechanical':(impact,'impactMetal_heavy_001'),
    'ignition':(scifi,'thrusterFire_000')
}

for target,(folder,name) in mapping.items():
    try:
        source=find(folder,name)
    except FileNotFoundError:
        print('missing',name,'available',','.join(p.stem for p in folder.rglob('*.ogg')))
        continue
    level=-25 if target in ['click','hover','ambience'] else -18
    subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-i',str(source),'-ac','2','-ar','48000','-af',f'loudnorm=I={level}:TP=-3:LRA=6',str(OUT/(target+'.wav'))],check=True)

nasa=SOURCE/'nasa_sts131_launch.mp3'
if not nasa.exists():
    data=urllib.request.urlopen('https://www.nasa.gov/wp-content/uploads/2015/01/590189main_ringtone_131_launchNats.mp3',timeout=45).read()
    nasa.write_bytes(data)
subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-i',str(nasa),'-ss','4','-t','12','-ac','2','-ar','48000','-af','highpass=f=35,lowpass=f=6500,loudnorm=I=-16:TP=-2:LRA=7,afade=t=in:d=0.08,afade=t=out:st=11.8:d=0.2',str(OUT/'engine.wav')],check=True)
subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-i',str(nasa),'-t','6','-ac','2','-ar','48000','-af','highpass=f=35,loudnorm=I=-17:TP=-2:LRA=7,afade=t=in:d=0.2,afade=t=out:st=5.4:d=0.6',str(OUT/'launch.wav')],check=True)
print('Sound assets prepared')
