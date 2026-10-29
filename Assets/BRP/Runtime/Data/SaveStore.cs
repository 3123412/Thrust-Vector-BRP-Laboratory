using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BRP
{
    [Serializable]
    public sealed class PlayerProgress
    {
        public int version = 1;
        public List<int> completed = new List<int>();
        public List<float> bestTimes = new List<float>();
        public float volume = 0.65f;
        public int quality = 1;
        public bool tutorialSeen;
        public List<CraftBlueprint> blueprints = new List<CraftBlueprint>();
    }

    public static class SaveStore
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Saves");
        static string ProgressPath => Path.Combine(Folder, "progress.json");

        public static PlayerProgress Load()
        {
            foreach (string path in new[] { ProgressPath, ProgressPath + ".bak" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var progress = JsonUtility.FromJson<PlayerProgress>(File.ReadAllText(path));
                    if (progress != null && progress.version == 1 && progress.completed != null && progress.blueprints != null)
                        return progress;
                }
                catch (Exception e) when (e is IOException || e is ArgumentException)
                {
                    Debug.LogWarning("Не удалось прочитать сохранение: " + e.Message);
                }
            }
            return new PlayerProgress();
        }

        public static void Save(PlayerProgress progress)
        {
            Directory.CreateDirectory(Folder);
            string temp = ProgressPath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(progress, true));
            if (File.Exists(ProgressPath)) File.Replace(temp, ProgressPath, ProgressPath + ".bak");
            else File.Move(temp, ProgressPath);
        }
    }
}
