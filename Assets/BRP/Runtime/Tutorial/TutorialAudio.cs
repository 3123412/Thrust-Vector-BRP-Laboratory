using System;
using System.Collections.Generic;
using UnityEngine;

namespace BRP
{
    public sealed class TutorialAudio : MonoBehaviour
    {
        [Serializable] public sealed class CaptionSegment {public string text;public float start,end;}
        [Serializable] public sealed class CaptionEntry {public string id,text;public CaptionSegment[] segments;}
        [Serializable] public sealed class CaptionFile {public CaptionEntry[] entries;}
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,CaptionEntry> captions=new Dictionary<string,CaptionEntry>();
        CaptionEntry currentCaption;
        AudioSource narration,music;
        LaboratoryGame game;
        string queued;
        float launchUntil;
        bool suspended;
        float voiceVolume=.9f,musicVolume=.18f;
        public bool IsNarrating=>narration!=null&&narration.isPlaying;
        public string CurrentVoice { get; private set; }
        public string CurrentSubtitle
        {
            get
            {
                if(!IsNarrating||currentCaption?.segments==null) return null;
                float time=narration.time;
                foreach(var segment in currentCaption.segments) if(time<segment.end+.18f) return segment.text;
                return currentCaption.segments.Length>0?currentCaption.segments[currentCaption.segments.Length-1].text:null;
            }
        }
        public bool MusicEnabled { get; set; }=true;
        public float VoiceVolume
        {
            get=>voiceVolume;
            set {voiceVolume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("BRP.VoiceVolume",voiceVolume);}
        }
        public float MusicVolume
        {
            get=>musicVolume;
            set {musicVolume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("BRP.MusicVolume",musicVolume);}
        }

        public void Initialize(LaboratoryGame owner)
        {
            game=owner;
            var transcript=Resources.Load<TextAsset>("BRP/Voice/captions");
            if(transcript!=null)
            {
                var file=JsonUtility.FromJson<CaptionFile>(transcript.text);
                if(file?.entries!=null) foreach(var entry in file.entries) captions[entry.id]=entry;
            }
            voiceVolume=PlayerPrefs.GetFloat("BRP.VoiceVolume",.9f);musicVolume=PlayerPrefs.GetFloat("BRP.MusicVolume",.18f);
            narration=Source("Flight instructor",0);narration.priority=0;
            narration.outputAudioMixerGroup=game.Sound.interfaceGroup;
            music=Source("Orbital Night",0);music.priority=160;music.loop=true;
            music.outputAudioMixerGroup=game.Sound.environmentGroup;
            music.clip=Resources.Load<AudioClip>("BRP/Music/orbital_night");
            if(music.clip!=null) music.Play();
        }

        AudioSource Source(string label,float volume)
        {
            var child=new GameObject(label);child.transform.SetParent(transform,false);
            var source=child.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=volume;return source;
        }

        public AudioClip Clip(string key)
        {
            if(string.IsNullOrEmpty(key)) return null;
            if(!clips.TryGetValue(key,out var clip)) {clip=Resources.Load<AudioClip>("BRP/Voice/"+key);clips[key]=clip;}
            return clip;
        }

        public bool Speak(string key)
        {
            queued=key;
            if(Time.unscaledTime<launchUntil||suspended) return Clip(key)!=null;
            queued=null;var clip=Clip(key);if(clip==null) return false;
            CurrentVoice=key;captions.TryGetValue(key,out currentCaption);narration.clip=clip;narration.Play();return true;
        }

        public void Launch()
        {
            var clip=Clip("launch_call");if(clip==null) return;
            narration.Stop();narration.clip=clip;CurrentVoice="launch_call";captions.TryGetValue(CurrentVoice,out currentCaption);narration.Play();
            launchUntil=Time.unscaledTime+clip.length+.3f;
        }

        public void StopSpeech() {queued=null;if(narration!=null) narration.Stop();}
        public void Suspend(bool value)
        {
            if(value==suspended||narration==null) return;
            suspended=value;if(value) narration.Pause();else narration.UnPause();
        }

        void Update()
        {
            if(game==null||narration==null) return;
            narration.volume=voiceVolume;
            float target=MusicEnabled?musicVolume:0;
            if(IsNarrating) target*=.2f;
            music.volume=Mathf.MoveTowards(music.volume,target,Time.unscaledDeltaTime*.18f);
            if(!suspended&&queued!=null&&Time.unscaledTime>=launchUntil) Speak(queued);
        }
    }
}
