import math
import random
import struct
import wave
from pathlib import Path

OUT=Path(__file__).resolve().parents[1]/'Assets/BRP/Resources/BRP/Audio'
OUT.mkdir(parents=True,exist_ok=True)
RATE=44100
rng=random.Random(61)

def write(name,duration,sample):
    previous=0
    data=[]
    for i in range(int(duration*RATE)):
        t=i/RATE
        noise=rng.uniform(-1,1)
        previous=previous*.94+noise*.06
        v=sample(t,duration,noise,previous)
        data.append(struct.pack('<h',int(max(-1,min(1,v)) * 26000)))
    with wave.open(str(OUT/(name+'.wav')),'wb') as f:
        f.setparams((1,2,RATE,0,'NONE','not compressed'));f.writeframes(b''.join(data))

write('click',.09,lambda t,d,n,p:math.sin(t*math.tau*(900-500*t/d))*math.exp(-t*65)*.3)
write('snap',.22,lambda t,d,n,p:(math.sin(t*math.tau*1300)+.5*math.sin(t*math.tau*1950))*math.exp(-t*27)*.4+n*math.exp(-t*120)*.2)
write('error',.4,lambda t,d,n,p:math.sin(t*math.tau*(190 if t<.18 else 140))*math.sin(math.pi*t/d)*.25)
write('launch',1.8,lambda t,d,n,p:(p*2+math.sin(t*math.tau*55)*.25)*math.sin(math.pi*t/d))
write('separate',.65,lambda t,d,n,p:(n*.35+math.sin(t*math.tau*95)*.25)*math.exp(-t*10))
write('success',1.1,lambda t,d,n,p:math.sin(t*math.tau*[523.25,659.25,783.99,1046.5][min(3,int(t/.24))])*.23*min(1,t*20)*min(1,(d-t)*5))
write('abort',1.1,lambda t,d,n,p:math.sin(t*math.tau*(400+120*math.sin(t*14)))*.2*min(1,(d-t)*5))
write('engine',4,lambda t,d,n,p:p*2.5+math.sin(t*math.tau*45)*.14+math.sin(t*math.tau*91)*.05)
write('ambience',8,lambda t,d,n,p:p*.24+math.sin(t*math.tau*50)*.025+math.sin(t*math.tau*100)*.015)
print('9 audio clips written')
