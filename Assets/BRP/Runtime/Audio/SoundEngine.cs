using UnityEngine;
using UnityEngine.Audio;

namespace BRP
{
    public enum SoundCue { Click, Snap, Error, Launch, Separate, Success, Abort, Explosion }

    public sealed class SoundEngine : MonoBehaviour
    {
        public AudioMixer mixer;
        public AudioMixerGroup interfaceGroup,engineGroup,environmentGroup;
        public AudioSource effects,engine,engineLow,ambience,mechanical;
        AudioClip[] cues;
        AudioClip hover;
        float hoverAfter,engineSetting;
        void Awake()
        {
            effects=Source(effects,"Interface",interfaceGroup,false,0);
            engine=Source(engine,"Main Engine",engineGroup,true,0);
            engineLow=Source(engineLow,"Engine Body",engineGroup,true,0);
            ambience=Source(ambience,"Control Room",environmentGroup,true,.055f);
            mechanical=Source(mechanical,"Mechanical",environmentGroup,false,0);
            effects.volume=.7f;mechanical.volume=.65f;
            cues=new AudioClip[8];
            for(int i=0;i<cues.Length;i++) cues[i]=Resources.Load<AudioClip>("BRP/Audio/"+((SoundCue)i).ToString().ToLowerInvariant());
            hover=Resources.Load<AudioClip>("BRP/Audio/hover");
            engine.clip=Resources.Load<AudioClip>("BRP/Audio/engine");
            engineLow.clip=Resources.Load<AudioClip>("BRP/Audio/engine_low");
            ambience.clip=Resources.Load<AudioClip>("BRP/Audio/ambience");
            engine.Play();engineLow.Play();ambience.Play();
        }
        AudioSource Source(AudioSource source,string label,AudioMixerGroup group,bool loop,float volume)
        {
            if(source==null)
            {
                var child=new GameObject(label);child.transform.SetParent(transform,false);source=child.AddComponent<AudioSource>();
            }
            source.playOnAwake=false;source.loop=loop;source.volume=volume;source.outputAudioMixerGroup=group;
            return source;
        }
        public void Hover()
        {
            if(Time.unscaledTime<hoverAfter||hover==null||effects==null) return;
            hoverAfter=Time.unscaledTime+.08f;effects.PlayOneShot(hover,.16f);
        }
        public void Play(SoundCue cue)
        {
            if(cues==null) return;
            var clip=cues[(int)cue];if(clip==null) return;
            bool physical=cue==SoundCue.Snap||cue==SoundCue.Separate||cue==SoundCue.Explosion;
            var source=physical?mechanical:effects;
            source.pitch=physical?Random.Range(.96f,1.04f):1;
            source.PlayOneShot(clip,cue==SoundCue.Launch?.65f:cue==SoundCue.Click?.35f:.7f);
        }
        public void SetEngine(float setting) => engineSetting=Mathf.Clamp01(setting);
        void Update()
        {
            if(engine==null) return;
            float ramp=Time.unscaledDeltaTime*(engineSetting>0?1.6f:.8f);
            engine.volume=Mathf.MoveTowards(engine.volume,Mathf.Sqrt(engineSetting)*.5f,ramp);
            engine.pitch=Mathf.Lerp(.85f,1.06f,engineSetting);
            engineLow.volume=Mathf.MoveTowards(engineLow.volume,engineSetting*.15f,ramp*.6f);
            engineLow.pitch=.8f+engineSetting*.15f;
            ambience.volume=Mathf.Lerp(ambience.volume,engineSetting>0?.008f:.055f,Time.unscaledDeltaTime*2);
        }
    }
}
