using System.IO;
using UnityEditor;
using UnityEngine;

namespace BRP.Editor
{
    public sealed class TutorialAudioImport : AssetPostprocessor
    {
        [MenuItem("BRP/Refresh Tutorial Audio")]
        public static void Configure()
        {
            foreach(string folder in new[]{"Assets/BRP/Resources/BRP/Voice","Assets/BRP/Resources/BRP/Music"})
            {
                if(!Directory.Exists(folder)) continue;
                foreach(string path in Directory.GetFiles(folder,"*.wav")) AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            }
        }

        void OnPreprocessAudio()
        {
            bool voice=assetPath.StartsWith("Assets/BRP/Resources/BRP/Voice/");
            bool music=assetPath.StartsWith("Assets/BRP/Resources/BRP/Music/");
            if(!voice&&!music) return;
            var importer=(AudioImporter)assetImporter;
            var settings=importer.defaultSampleSettings;
            settings.loadType=music?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=music?.75f:.9f;
            settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=!music;
            importer.forceToMono=voice;importer.loadInBackground=true;importer.defaultSampleSettings=settings;
        }
    }
}
