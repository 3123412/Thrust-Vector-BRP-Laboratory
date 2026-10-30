using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace BRP.Editor
{
    public static class AudioSetup
    {
        public static void Configure(SoundEngine sound)
        {
            Directory.CreateDirectory("Assets/BRP/Audio");
            const string path="Assets/BRP/Audio/Laboratory.mixer";
            var mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if(mixer==null)
            {
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.Audio.AudioMixerController")).First(t=>t!=null);
                mixer=(AudioMixer)type.GetMethod("CreateMixerControllerAtPath",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{path});
                var master=type.GetProperty("masterGroup").GetValue(mixer);
                var groupType=master.GetType();var children=Array.CreateInstance(groupType,3);
                string[] names={"Interface","Propulsion","Environment"};
                for(int i=0;i<names.Length;i++)
                {
                    var group=(UnityEngine.Object)type.GetMethod("CreateNewGroup").Invoke(mixer,new object[]{names[i],true});
                    if(!EditorUtility.IsPersistent(group)) AssetDatabase.AddObjectToAsset(group,mixer);
                    children.SetValue(group,i);
                }
                groupType.GetProperty("children").SetValue(master,children);
                EditorUtility.SetDirty(mixer);AssetDatabase.SaveAssets();
            }
            sound.mixer=mixer;
            sound.interfaceGroup=mixer.FindMatchingGroups("Interface").FirstOrDefault();
            sound.engineGroup=mixer.FindMatchingGroups("Propulsion").FirstOrDefault();
            sound.environmentGroup=mixer.FindMatchingGroups("Environment").FirstOrDefault();
            sound.effects.outputAudioMixerGroup=sound.interfaceGroup;
            sound.engine.outputAudioMixerGroup=sound.engineGroup;sound.engineLow.outputAudioMixerGroup=sound.engineGroup;
            sound.ambience.outputAudioMixerGroup=sound.environmentGroup;sound.mechanical.outputAudioMixerGroup=sound.environmentGroup;
            foreach(string file in Directory.GetFiles("Assets/BRP/Resources/BRP/Audio","*.wav"))
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(file);if(importer==null) continue;
                var settings=importer.defaultSampleSettings;settings.quality=.85f;
                settings.loadType=Path.GetFileName(file).Contains("engine")||file.EndsWith("ambience.wav")?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
        }
    }
}
