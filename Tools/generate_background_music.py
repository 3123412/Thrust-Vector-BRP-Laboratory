import math
import wave
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
MUSIC=ROOT/'Assets/BRP/Resources/BRP/Music'

def music():
    sample_rate = 44100
    duration = 120
    t = np.arange(sample_rate * duration, dtype=np.float64) / sample_rate
    stereo = np.zeros((len(t), 2), dtype=np.float64)
    progression = [(146.8324, 174.6141, 220), (130.8128, 164.8138, 195.9977), (116.5409, 146.8324, 174.6141), (130.8128, 164.8138, 220)]
    def add_note(start, length, frequency, level, pan, attack=1.5, release=2.5, bell=False):
        first = int(start * sample_rate)
        count = min(int(length * sample_rate), len(t) - first)
        local = np.arange(count) / sample_rate
        envelope = np.minimum(1, local / attack) * np.minimum(1, (length - local) / release)
        if bell:
            envelope *= np.exp(-local * .5)
            signal = np.sin(2 * np.pi * frequency * local) + .22 * np.sin(2 * np.pi * frequency * 2.003 * local)
        else:
            signal = np.sin(2 * np.pi * frequency * local + .012 * np.sin(2 * np.pi * .11 * local))
            signal += .3 * np.sin(2 * np.pi * frequency * 2.0006 * local) + .1 * np.sin(2 * np.pi * frequency * 3 * local)
        signal *= envelope * level
        stereo[first:first + count, 0] += signal * math.sqrt((1 - pan) / 2)
        stereo[first:first + count, 1] += signal * math.sqrt((1 + pan) / 2)
    for block in range(8):
        chord = progression[block % 4]
        start = block * 15
        for voice, frequency in enumerate(chord):
            add_note(start, 18 if block < 7 else 15, frequency, .06, -.5 + voice * .5)
            add_note(start, 18 if block < 7 else 15, frequency / 2, .025, .35 - voice * .3)
        for offset, note in [(1.9, chord[0] * 4), (7.5, chord[2] * 4), (11.25, chord[1] * 4)]:
            add_note(start + offset, min(6, duration - start - offset), note, .035, math.sin(block + offset) * .6, .04, 3, True)
    delay = int(sample_rate * .375)
    stereo[delay:, 0] += stereo[:-delay, 1] * .16
    stereo[delay:, 1] += stereo[:-delay, 0] * .13
    fade = np.minimum(1, t / 4) * np.minimum(1, (duration - t) / 5)
    stereo *= fade[:, None]
    stereo = np.tanh(stereo * 1.1)
    destination = MUSIC / 'orbital_night.wav'
    temporary = ROOT / 'Artifacts/orbital_night-pcm.wav'
    with wave.open(str(temporary), 'wb') as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(sample_rate)
        output.writeframes((stereo * 32767).astype('<i2').tobytes())
    temporary.replace(destination)
    print('original music', destination.stat().st_size, flush=True)


if __name__ == '__main__':
    music()
