import sys
import subprocess
import wave
from pathlib import Path
import numpy as np
root=Path.cwd()
sys.path.insert(0,str(root/'Tools/.packages'))
import imageio_ffmpeg
out=root/'Assets/BRP/Resources/BRP/Audio/engine.wav'
raw=root/'Artifacts/engine-linear.wav'
subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-hide_banner','-loglevel','error','-y','-i',str(root/'SourceAudio/nasa_sts131_launch.mp3'),'-ss','4','-t','12','-ac','2','-ar','48000','-af','highpass=f=35,lowpass=f=6500,loudnorm=I=-16:TP=-2:LRA=7',str(raw)],check=True)
with wave.open(str(raw),'rb') as file:
    samples=np.frombuffer(file.readframes(file.getnframes()),dtype=np.int16).reshape(-1,2).astype(np.float64)
    rate=file.getframerate()
length=int(rate*.8)
fade=np.linspace(0,1,length)[:,None]
result=samples[:-length].copy()
result[:length]=samples[-length:]*(1-fade)+samples[:length]*fade
with wave.open(str(out),'wb') as file:
    file.setnchannels(2)
    file.setsampwidth(2)
    file.setframerate(rate)
    file.writeframes(np.clip(result,-32768,32767).astype(np.int16).tobytes())
print('engine loop crossfaded',len(result)/rate)
