import argparse
import asyncio
import hashlib
import json
import re
import shutil
import subprocess
import sys
import wave
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools/.packages'))
import edge_tts
import imageio_ffmpeg
import numpy as np

FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
PROFILE = {'voice': 'ru-RU-DmitryNeural', 'gender': 'Male', 'rate': '+0%', 'pitch': '+0Hz', 'sample_rate': 24000, 'channels': 1, 'loudness_lufs': -18, 'true_peak_db': -3}
RATES = ['+0%', '-2%', '+1%', '-1%', '+2%', '-3%']
CACHE = ROOT / 'Artifacts/TutorialVoiceCache'

class VoiceRequestGate:
    def __init__(self, concurrency):
        self.semaphore = asyncio.Semaphore(concurrency)
        self.lock = asyncio.Lock()
        self.next_start = 0

    async def __aenter__(self):
        await self.semaphore.acquire()
        async with self.lock:
            loop = asyncio.get_running_loop()
            while self.next_start > loop.time():
                await asyncio.sleep(self.next_start - loop.time())
            self.next_start = loop.time() + 1.4
        return self

    async def __aexit__(self, kind, value, traceback):
        self.semaphore.release()

    def cooldown(self):
        self.next_start = max(self.next_start, asyncio.get_running_loop().time() + 18)

def script():
    lines = json.loads((ROOT / 'Tools/tutorial_voice_ru.json').read_text(encoding='utf-8'))
    parts = json.loads((ROOT / 'Assets/BRP/Resources/BRP/parts.json').read_text(encoding='utf-8'))['parts']
    for part in parts:
        mount = 'Устанавливается на боковой узел корпуса.' if part.get('surface') else 'Соединяется по продольной оси ракеты.'
        if part['kind'] == 'BoosterEngine':
            mount = 'Установи двигатель снизу, соплом от корпуса.'
        description = part['description'].replace('5 км', 'пяти километров').replace('180 м/с', 'ста восьмидесяти метров в секунду')
        lines['part_' + part['id']] = part['title'] + '. ' + description + ' ' + mount
    return lines

def chunks(text, maximum=115):
    sentences = re.split(r'(?<=[.!?])\s+', text.strip())
    result = []
    for sentence in sentences:
        current = ''
        for word in sentence.split():
            if current and len(current) + len(word) + 1 > maximum:
                result.append(current)
                current = word
            else:
                current = (current + ' ' + word).strip()
        if current:
            result.append(current)
    return result

def read_pcm(path):
    with wave.open(str(path)) as source:
        if source.getnchannels() != 1 or source.getframerate() != PROFILE['sample_rate'] or source.getsampwidth() != 2:
            raise ValueError('Unexpected PCM format: ' + path.name)
        return np.frombuffer(source.readframes(source.getnframes()), dtype='<i2').copy()

def write_pcm(path, samples):
    with wave.open(str(path), 'wb') as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(PROFILE['sample_rate'])
        target.writeframes(samples.astype('<i2').tobytes())

async def synthesize_chunk(text, semaphore):
    for rate in RATES:
        identity = {**PROFILE, 'rate': rate, 'text': text}
        key = hashlib.sha256(json.dumps(identity, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
        folder = CACHE / key
        output = folder / 'speech.wav'
        metadata = folder / 'source.json'
        if output.exists() and metadata.exists():
            read_pcm(output)
            return output, key
    for attempt in range(6):
        rate = RATES[attempt]
        identity = {**PROFILE, 'rate': rate, 'text': text}
        key = hashlib.sha256(json.dumps(identity, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
        folder = CACHE / key
        folder.mkdir(parents=True, exist_ok=True)
        output = folder / 'speech.wav'
        metadata = folder / 'source.json'
        temporary = folder / 'download.mp3'
        try:
            async with semaphore:
                await edge_tts.Communicate(text, PROFILE['voice'], rate=rate, pitch=PROFILE['pitch']).save(str(temporary))
            raw = folder / 'raw.wav'
            subprocess.run([FFMPEG, '-v', 'error', '-y', '-i', str(temporary), '-ar', '24000', '-ac', '1', str(raw)], check=True)
            pcm = read_pcm(raw)
            active = np.flatnonzero(np.abs(pcm.astype(np.int32)) > 85)
            if len(active) < 200:
                raise ValueError('Silent speech')
            start = max(0, int(active[0]) - 1440)
            end = min(len(pcm), int(active[-1]) + 1920)
            write_pcm(output, pcm[start:end])
            metadata.write_text(json.dumps(identity, ensure_ascii=False, indent=2), encoding='utf-8')
            return output, key
        except Exception as error:
            if attempt == 5:
                raise RuntimeError('Speech failed: ' + text) from error
            print('retry chunk', key[:10], attempt + 1, type(error).__name__, flush=True)
            semaphore.cooldown()
            await asyncio.sleep(2 + attempt * 3)

async def synthesize_line(key, text, stage, semaphore, clip_semaphore):
    destination = stage / (key + '.wav')
    metadata = stage / (key + '.json')
    identity = hashlib.sha256(json.dumps({**PROFILE, 'text': text}, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
    if destination.exists() and metadata.exists():
        previous = json.loads(metadata.read_text(encoding='utf-8'))
        if previous.get('identity') == identity:
            read_pcm(destination)
            return previous
    async with clip_semaphore:
        segments = []
        samples = []
        frame = 0
        for phrase in chunks(text):
            source, cache_key = await synthesize_chunk(phrase, semaphore)
            pcm = read_pcm(source)
            duration = len(pcm) / PROFILE['sample_rate']
            source_profile = json.loads((source.parent / 'source.json').read_text(encoding='utf-8'))
            segments.append({'text': phrase, 'start': round(frame / PROFILE['sample_rate'], 5), 'end': round(frame / PROFILE['sample_rate'] + duration, 5), 'cache_key': cache_key, 'rate': source_profile['rate'], 'pitch': source_profile['pitch']})
            samples.append(pcm)
            pause = np.zeros(int(PROFILE['sample_rate'] * .18), dtype='<i2')
            samples.append(pause)
            frame += len(pcm) + len(pause)
        combined = stage / (key + '-joined.wav')
        write_pcm(combined, np.concatenate(samples))
        normalized = stage / (key + '-normalized.wav')
        subprocess.run([FFMPEG, '-v', 'error', '-y', '-i', str(combined), '-af', 'highpass=f=65,loudnorm=I=-18:TP=-3:LRA=7', '-ar', '24000', '-ac', '1', str(normalized)], check=True)
        normalized.replace(destination)
        result = {'id': key, 'text': text, 'identity': identity, 'voice': PROFILE['voice'], 'rate': PROFILE['rate'], 'pitch': PROFILE['pitch'], 'segments': segments, 'duration': round(len(read_pcm(destination)) / PROFILE['sample_rate'], 5), 'sha256': hashlib.sha256(destination.read_bytes()).hexdigest()}
        metadata.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
        print('male clip', key, result['duration'], flush=True)
        return result

async def generate(lines, stage, concurrency):
    semaphore = VoiceRequestGate(concurrency)
    clip_semaphore = asyncio.Semaphore(4)
    return await asyncio.gather(*(synthesize_line(key, text, stage, semaphore, clip_semaphore) for key, text in lines.items()))

def verify(stage, lines, entries):
    total = 0
    used_rates = set()
    for entry in entries:
        key = entry['id']
        if entry['voice'] != PROFILE['voice'] or entry['text'] != lines[key]:
            raise ValueError('Voice identity mismatch: ' + key)
        pcm = read_pcm(stage / (key + '.wav')).astype(np.float64) / 32768
        if len(pcm) < 9600 or np.sqrt(np.mean(pcm * pcm)) < .005:
            raise ValueError('Missing or silent clip: ' + key)
        if entry['sha256'] != hashlib.sha256((stage / (key + '.wav')).read_bytes()).hexdigest():
            raise ValueError('Clip checksum mismatch: ' + key)
        total += len(pcm) / PROFILE['sample_rate']
        for segment in entry['segments']:
            if 'rate' in segment:
                used_rates.add(segment['rate'])
            else:
                source = CACHE / segment['cache_key'] / 'source.json'
                used_rates.add(json.loads(source.read_text(encoding='utf-8'))['rate'])
    return {'utc': datetime.now(timezone.utc).isoformat(), **PROFILE, 'rate': '/'.join(sorted(used_rates)), 'used_rates': sorted(used_rates), 'clips': len(entries), 'voice_seconds': round(total, 2), 'all_verified': True, 'female_source_reused': False}

def install(stage):
    lines = script()
    report = json.loads((stage / 'profile.json').read_text(encoding='utf-8'))
    entries = json.loads((stage / 'captions.json').read_text(encoding='utf-8'))['entries']
    if set(lines) != {entry['id'] for entry in entries}:
        raise ValueError('Incomplete voice pack')
    verify(stage, lines, entries)
    if report['voice'] != PROFILE['voice']:
        raise ValueError('Not the requested male voice')
    destination = ROOT / 'Assets/BRP/Resources/BRP/Voice'
    destination.mkdir(parents=True, exist_ok=True)
    for key in lines:
        temporary = destination / (key + '.wav.pending')
        shutil.copyfile(stage / (key + '.wav'), temporary)
        temporary.replace(destination / (key + '.wav'))
    for name in ['script.json', 'captions.json', 'profile.json', 'PROVENANCE.txt']:
        temporary = destination / (name + '.pending')
        shutil.copyfile(stage / name, temporary)
        temporary.replace(destination / name)
    print('installed male voice pack', len(lines), flush=True)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', type=Path, default=ROOT / 'Artifacts/Voice_Dmitry_20261031')
    parser.add_argument('--only', default='')
    parser.add_argument('--concurrency', type=int, default=2)
    parser.add_argument('--initial-delay', type=int, default=0)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    stage = args.stage.resolve()
    stage.mkdir(parents=True, exist_ok=True)
    if args.install:
        install(stage)
        return
    lines = script()
    if args.only:
        wanted = set(args.only.split(','))
        lines = {key: text for key, text in lines.items() if key in wanted}
    async def run():
        if args.initial_delay:
            print('speech service cooldown', args.initial_delay, flush=True)
            await asyncio.sleep(args.initial_delay)
        return await generate(lines, stage, args.concurrency)
    entries = asyncio.run(run())
    profile = verify(stage, lines, entries)
    (stage / 'script.json').write_text(json.dumps(lines, ensure_ascii=False, indent=2), encoding='utf-8')
    (stage / 'captions.json').write_text(json.dumps({'entries': entries}, ensure_ascii=False, indent=2), encoding='utf-8')
    (stage / 'profile.json').write_text(json.dumps(profile, ensure_ascii=False, indent=2), encoding='utf-8')
    provenance = 'Русский инструктор BRP\n\nГолос: ru-RU-DmitryNeural (Male), Microsoft Edge Read Aloud, edge-tts.\nТекст: оригинальные реплики проекта, Tools/tutorial_voice_ru.json и parts.json.\nГенерация: 31 октября 2026 года, Tools/generate_tutorial_audio.py.\nДлинные реплики синтезированы короткими фразами с паузами.\nКеш включает голос, текст, скорость и высоту нейросинтеза.\nОбработка: mono PCM24kHz, -18LUFS, true peak -3dB; женские записи и изменение высоты женского голоса не использованы.\n\n«Поехали!» произносит голос инструктора; архивная запись и имитация Юрия Гагарина не использованы.\nБиблиотека: https://github.com/rany2/edge-tts\nУсловия онлайн-службы Microsoft применяются к использованию службы синтеза.\n'
    (stage / 'PROVENANCE.txt').write_text(provenance, encoding='utf-8')
    print(json.dumps(profile, ensure_ascii=False), flush=True)

if __name__ == '__main__':
    main()
