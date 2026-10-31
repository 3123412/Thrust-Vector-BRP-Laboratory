import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools/.packages'))
import numpy as np
import torch
import generate_tutorial_audio as common

MODEL_URL = 'https://models.silero.ai/models/tts/ru/v5_6_ru.pt'
LICENSE_URL = 'https://raw.githubusercontent.com/snakers4/silero-models/master/LICENSE'
RUNTIME = ROOT / 'Artifacts/SileroRuntime'
MODEL = RUNTIME / 'v5_6_ru.pt'
LICENSE = RUNTIME / 'LICENSE_CC_BY_NC_SA_4.txt'
STAGE = ROOT / 'Artifacts/Voice_Aidar_20261031'
PROFILE = {'voice': 'silero-v5_6_ru/aidar', 'provider': 'Silero', 'speaker': 'aidar', 'gender': 'Male', 'sample_rate': 24000, 'channels': 1, 'rate': 'native', 'pitch': 'native', 'loudness_lufs': -18, 'true_peak_db': -3, 'license': 'CC-BY-NC-SA-4.0'}

def files():
    RUNTIME.mkdir(parents=True, exist_ok=True)
    if not MODEL.exists():
        temporary = RUNTIME / 'model.pt.pending'
        urllib.request.urlretrieve(MODEL_URL, temporary)
        temporary.replace(MODEL)
    if not LICENSE.exists():
        urllib.request.urlretrieve(LICENSE_URL, LICENSE)
    return hashlib.sha256(MODEL.read_bytes()).hexdigest()

def verify(stage, lines, entries, model_hash):
    total = 0
    baseline = ROOT / 'Artifacts/voice-before-dmitry.json'
    previous = json.loads(baseline.read_text()) if baseline.exists() else {}
    reused = 0
    for entry in entries:
        path = stage / (entry['id'] + '.wav')
        pcm = common.read_pcm(path).astype(np.float64) / 32768
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if entry['voice'] != PROFILE['voice'] or entry['text'] != lines[entry['id']] or entry['sha256'] != digest:
            raise ValueError('Voice provenance mismatch: ' + entry['id'])
        if len(pcm) < 9600 or np.sqrt(np.mean(pcm * pcm)) < .005:
            raise ValueError('Invalid speech: ' + entry['id'])
        if previous.get(entry['id']) == digest:
            reused += 1
        total += len(pcm) / 24000
    if reused:
        raise ValueError('Old voice recording reused')
    return {'utc': datetime.now(timezone.utc).isoformat(), **PROFILE, 'model_sha256': model_hash, 'clips': len(entries), 'voice_seconds': round(total, 2), 'all_verified': True, 'female_source_reused': False, 'old_voice_hash_matches': reused}

def generate_line(key, text, stage, model, model_hash):
    identity = hashlib.sha256(json.dumps({**PROFILE, 'model_sha256': model_hash, 'text': text}, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
    output = stage / (key + '.wav')
    metadata = stage / (key + '.json')
    if output.exists() and metadata.exists():
        previous = json.loads(metadata.read_text(encoding='utf-8'))
        if previous['identity'] == identity:
            common.read_pcm(output)
            return previous
    samples = []
    segments = []
    frame = 0
    cache = ROOT / 'Artifacts/SileroVoiceCache'
    cache.mkdir(parents=True, exist_ok=True)
    for phrase in common.chunks(text):
        source = {**PROFILE, 'model_sha256': model_hash, 'text': phrase}
        cache_key = hashlib.sha256(json.dumps(source, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
        cached = cache / (cache_key + '.wav')
        if cached.exists():
            pcm = common.read_pcm(cached)
        else:
            with torch.inference_mode():
                tensor = model.apply_tts(text=phrase, speaker=PROFILE['speaker'], sample_rate=24000, put_accent=True, put_yo=True)
            pcm = (tensor.detach().cpu().numpy().clip(-1, 1) * 32767).astype('<i2')
            active = np.flatnonzero(np.abs(pcm.astype(np.int32)) > 85)
            if len(active) < 200:
                raise ValueError('Silent model output: ' + phrase)
            pcm = pcm[max(0, int(active[0]) - 1440):min(len(pcm), int(active[-1]) + 1920)]
            common.write_pcm(cached, pcm)
            (cache / (cache_key + '.json')).write_text(json.dumps(source, ensure_ascii=False, indent=2), encoding='utf-8')
        segments.append({'text': phrase, 'start': round(frame / 24000, 5), 'end': round((frame + len(pcm)) / 24000, 5), 'cache_key': cache_key})
        samples.append(pcm)
        pause = np.zeros(4320, dtype='<i2')
        samples.append(pause)
        frame += len(pcm) + len(pause)
    joined = stage / (key + '-joined.wav')
    common.write_pcm(joined, np.concatenate(samples))
    temporary = stage / (key + '-normalized.wav')
    subprocess.run([common.FFMPEG, '-v', 'error', '-y', '-i', str(joined), '-af', 'highpass=f=65,loudnorm=I=-18:TP=-3:LRA=7', '-ar', '24000', '-ac', '1', str(temporary)], check=True)
    temporary.replace(output)
    entry = {'id': key, 'voice': PROFILE['voice'], 'text': text, 'identity': identity, 'model_sha256': model_hash, 'segments': segments, 'duration': round(len(common.read_pcm(output)) / 24000, 5), 'sha256': hashlib.sha256(output.read_bytes()).hexdigest()}
    metadata.write_text(json.dumps(entry, ensure_ascii=False, indent=2), encoding='utf-8')
    print('offline male', key, entry['duration'], flush=True)
    return entry

def install(stage):
    lines = common.script()
    entries = json.loads((stage / 'captions.json').read_text(encoding='utf-8'))['entries']
    profile = json.loads((stage / 'profile.json').read_text(encoding='utf-8'))
    if set(lines) != {entry['id'] for entry in entries} or len(lines) != 98:
        raise ValueError('Incomplete offline voice pack')
    verify(stage, lines, entries, profile['model_sha256'])
    destination = ROOT / 'Assets/BRP/Resources/BRP/Voice'
    for key in lines:
        temporary = destination / (key + '.wav.pending')
        shutil.copyfile(stage / (key + '.wav'), temporary)
        temporary.replace(destination / (key + '.wav'))
    for name in ['script.json', 'captions.json', 'profile.json', 'PROVENANCE.txt', 'LICENSE_CC_BY_NC_SA_4.txt']:
        temporary = destination / (name + '.pending')
        shutil.copyfile(stage / name, temporary)
        temporary.replace(destination / name)
    print('installed uniform offline male voice pack', len(lines), flush=True)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', type=Path, default=STAGE)
    parser.add_argument('--only', default='')
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    stage = args.stage.resolve()
    stage.mkdir(parents=True, exist_ok=True)
    if args.install:
        install(stage)
        return
    model_hash = files()
    torch.set_num_threads(4)
    model = torch.package.PackageImporter(str(MODEL)).load_pickle('tts_models', 'model')
    model.to(torch.device('cpu'))
    lines = common.script()
    if args.only:
        wanted = set(args.only.split(','))
        lines = {key: text for key, text in lines.items() if key in wanted}
    entries = [generate_line(key, text, stage, model, model_hash) for key, text in lines.items()]
    profile = verify(stage, lines, entries, model_hash)
    (stage / 'script.json').write_text(json.dumps(lines, ensure_ascii=False, indent=2), encoding='utf-8')
    (stage / 'captions.json').write_text(json.dumps({'entries': entries}, ensure_ascii=False, indent=2), encoding='utf-8')
    (stage / 'profile.json').write_text(json.dumps(profile, ensure_ascii=False, indent=2), encoding='utf-8')
    shutil.copyfile(LICENSE, stage / LICENSE.name)
    provenance = 'Инструктор BRP — мужской нейроголос Aidar\n\nИсточник модели: Silero Models, snakers4/Silero Team.\nМодель: v5_6_ru, голос aidar.\nРепозиторий: https://github.com/snakers4/silero-models\nМодель: ' + MODEL_URL + '\nЛицензия: Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International. Полный текст находится рядом в LICENSE_CC_BY_NC_SA_4.txt.\n\nОригинальные реплики проекта: Tools/tutorial_voice_ru.json и описания модулей parts.json.\nСинтезировано офлайн 31 октября 2026 года, Tools/generate_offline_tutorial_audio.py.\nОбработка: разделение на смысловые фразы, паузы, обрезка краевой тишины, highpass65Hz, -18LUFS/truepeak-3dB, mono PCM24kHz.\nВесь набор озвучен одним мужским голосом; женские фонограммы и изменение их высоты не использованы.\n\nГотовые записи и внесённая обработка распространяются с атрибуцией Silero Team под CC BY-NC-SA4.0 для некоммерческого учебного проекта.\n«Поехали!» произносит инструктор; это не архивная запись и не имитация Юрия Гагарина.\nМодель и PyTorch в игру и публичный архив исходников не включаются.\n'
    (stage / 'PROVENANCE.txt').write_text(provenance, encoding='utf-8')
    print(json.dumps(profile, ensure_ascii=False), flush=True)

if __name__ == '__main__':
    main()
