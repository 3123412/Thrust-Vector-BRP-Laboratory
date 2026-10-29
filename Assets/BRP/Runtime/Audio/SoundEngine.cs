using UnityEngine;

namespace BRP
{
    public enum SoundCue { Click, Snap, Error, Launch, Separate, Success, Abort }

    public sealed class SoundEngine : MonoBehaviour
    {
        AudioSource effects,engine,ambience;
        AudioClip[] cues;
        void Awake()
        {
            effects=gameObject.AddComponent<AudioSource>();
            engine=gameObject.AddComponent<AudioSource>();
            ambience=gameObject.AddComponent<AudioSource>();
            cues=new AudioClip[7];
            for(int i=0;i<cues.Length;i++) cues[i]=Resources.Load<AudioClip>("BRP/Audio/"+((SoundCue)i).ToString().ToLowerInvariant());
            engine.clip=Resources.Load<AudioClip>("BRP/Audio/engine");engine.loop=true;engine.volume=0;engine.Play();
            ambience.clip=Resources.Load<AudioClip>("BRP/Audio/ambience");ambience.loop=true;ambience.volume=.12f;ambience.Play();
        }
        public void Play(SoundCue cue)
        {
            var clip=cues[(int)cue];if(clip!=null) effects.PlayOneShot(clip,.65f);
        }
        public void SetEngine(float thrust)
        {
            engine.volume=Mathf.Lerp(engine.volume,Mathf.Clamp01(thrust)*.4f,Time.unscaledDeltaTime*3);
            engine.pitch=.65f+Mathf.Clamp01(thrust)*.65f;
        }
    }
}
