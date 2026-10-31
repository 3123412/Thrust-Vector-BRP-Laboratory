import hashlib
import json
import re
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools/.packages'))
import imageio_ffmpeg

out = ROOT / 'Delivery'
report = {'archives': {}, 'video': {}}
for name in ('BRP_Source.zip', 'BRP_Windows.zip'):
    with zipfile.ZipFile(out / name) as archive:
        assert archive.testzip() is None, name
        entries = archive.namelist()
        pointers = []
        for entry in archive.infolist():
            if 0 < entry.file_size < 1024:
                with archive.open(entry) as source:
                    if source.read(80).startswith(b'version https://git-lfs.github.com/spec/v1'):
                        pointers.append(entry.filename)
        assert not pointers, pointers
        required = ('BRP.exe', 'UnityPlayer.dll', 'Assembly-CSharp.dll') if 'Windows' in name else ('ProjectVersion.txt', 'Laboratory.unity', 'intro.wav', 'BRP_World_V3.blend')
        for suffix in required:
            assert any(entry.endswith('/' + suffix) for entry in entries), (name, suffix)
        assert not any('/.packages/' in entry or '/Artifacts/' in entry or '/Library/' in entry for entry in entries)
        report['archives'][name] = {'entries': len(entries), 'crc_valid': True, 'lfs_pointers': 0}

video = out / 'BRP_Gameplay.mp4'
ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
metadata = subprocess.run([ffmpeg, '-i', str(video)], capture_output=True, text=True).stderr
duration = re.search(r'Duration: (\d+):(\d+):(\d+\.\d+)', metadata)
assert duration and '1920x1080' in metadata and 'Audio: aac' in metadata, metadata[-2000:]
seconds = int(duration[1]) * 3600 + int(duration[2]) * 60 + float(duration[3])
assert seconds > 120, seconds
decode = subprocess.run([ffmpeg, '-v', 'error', '-i', str(video), '-f', 'null', '-'], capture_output=True, text=True)
assert decode.returncode == 0 and not decode.stderr.strip(), decode.stderr
report['video'] = {'seconds': seconds, 'width': 1920, 'height': 1080, 'audio': 'AAC', 'full_decode_passed': True}
checks = json.loads((out / 'checksums.json').read_text())
for name, expected in checks.items():
    path = out / name
    assert path.stat().st_size == expected['bytes']
    with path.open('rb') as source:
        assert hashlib.file_digest(source, 'sha256').hexdigest() == expected['sha256'], name
report['checksums_valid'] = True
(ROOT / 'Artifacts/delivery-verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
