import argparse
import hashlib
import json
import sys
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools/.packages'))
import numpy as np

parser = argparse.ArgumentParser()
parser.add_argument('--voice-dir', type=Path, default=ROOT / 'Assets/BRP/Resources/BRP/Voice')
args = parser.parse_args()
voice = args.voice_dir.resolve()
script = json.loads((voice / 'script.json').read_text(encoding='utf-8'))
profile = json.loads((voice / 'profile.json').read_text(encoding='utf-8'))
captions = json.loads((voice / 'captions.json').read_text(encoding='utf-8'))['entries']
assert profile['voice'] in ['ru-RU-DmitryNeural', 'silero-v5_6_ru/aidar'] and profile['gender'] == 'Male'
assert len(script) == 98 and set(script) == {entry['id'] for entry in captions}
entries = {entry['id']: entry for entry in captions}
baseline_path = ROOT / 'Artifacts/voice-before-dmitry.json'
baseline = json.loads(baseline_path.read_text()) if baseline_path.exists() else {}
report = {'voice': profile['voice'], 'gender': profile['gender'], 'passed': 0, 'failed': 0, 'old_voice_hash_matches': 0, 'clips': []}
for key, text in script.items():
    path = voice / (key + '.wav')
    with wave.open(str(path)) as source:
        assert source.getnchannels() == 1
        assert source.getframerate() == 24000
        frames = source.readframes(source.getnframes())
        duration = source.getnframes() / source.getframerate()
    signal = np.frombuffer(frames, dtype='<i2').astype(np.float64) / 32768
    rms = float(np.sqrt(np.mean(signal * signal)))
    assert duration > .4 and rms > .005, key
    entry = entries[key]
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert entry['voice'] == profile['voice'] and entry['text'] == text
    assert digest == entry['sha256']
    assert ' '.join(segment['text'] for segment in entry['segments']) == ' '.join(text.split())
    previous = 0
    for segment in entry['segments']:
        assert previous <= segment['start'] < segment['end'] <= duration + .05, key
        previous = segment['end']
    if baseline.get(key) == digest:
        report['old_voice_hash_matches'] += 1
    report['clips'].append({'id': key, 'duration': round(duration, 3), 'rms': round(rms, 4), 'sha256': digest, 'caption_segments': len(entry['segments'])})
    report['passed'] += 1
music = ROOT / 'Assets/BRP/Resources/BRP/Music/orbital_night.wav'
with wave.open(str(music)) as source:
    assert source.getnchannels() == 2 and source.getframerate() == 44100
    assert source.getnframes() / source.getframerate() == 120
report['passed'] += 1
report['music'] = {'name': 'Orbital Night', 'duration': 120, 'channels': 2, 'original': True}
assert report['old_voice_hash_matches'] == 0
(ROOT / 'Artifacts/tutorial-audio-verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'voice': report['voice'], 'gender': report['gender'], 'passed': report['passed'], 'failed': report['failed'], 'old_voice_hash_matches': report['old_voice_hash_matches'], 'voice_seconds': round(sum(c['duration'] for c in report['clips']), 1), 'music_seconds': 120}))
