using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BRP
{
    [Serializable]
    public sealed class PlacedPart
    {
        public string instanceId = Guid.NewGuid().ToString("N");
        public string partId;
        public string parentId;
        public int port;
        public int stage;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Color color = new Color(0.82f, 0.86f, 0.88f);
    }

    [Serializable]
    public sealed class CraftBlueprint
    {
        public int version = 1;
        public string name = "Новая ракета";
        public List<PlacedPart> parts = new List<PlacedPart>();

        public CraftBlueprint Clone() => JsonUtility.FromJson<CraftBlueprint>(JsonUtility.ToJson(this));

        public List<PlacedPart> Subtree(string root)
        {
            var found = new HashSet<string> { root };
            bool changed;
            do
            {
                changed = false;
                foreach (var part in parts)
                    if (!string.IsNullOrEmpty(part.parentId) && found.Contains(part.parentId))
                        changed |= found.Add(part.instanceId);
            } while (changed);
            return parts.Where(p => found.Contains(p.instanceId)).ToList();
        }

        public List<string> Validate(PartCatalog catalog)
        {
            var errors = new List<string>();
            if (parts.Count == 0) errors.Add("Установите корпус ракеты");
            if (parts.Count > 128) errors.Add("Предел конструкции: 128 деталей");
            var ids = new HashSet<string>();
            foreach (var part in parts)
            {
                if (!ids.Add(part.instanceId)) errors.Add("Повтор идентификатора детали");
                if (catalog.Find(part.partId) == null) errors.Add("Неизвестная деталь: " + part.partId);
                if (part.stage < 0 || part.stage > 5) errors.Add("Номер ступени должен быть от 1 до 6");
            }
            foreach (var part in parts)
            {
                var visited = new HashSet<string> { part.instanceId };
                var current = part;
                while (!string.IsNullOrEmpty(current.parentId))
                {
                    if (!visited.Add(current.parentId)) { errors.Add("Цикл креплений"); break; }
                    current = parts.Find(p => p.instanceId == current.parentId);
                    if (current == null) { errors.Add("Отсутствует несущая деталь"); break; }
                }
            }
            if (parts.Count(p => string.IsNullOrEmpty(p.parentId)) > 1) errors.Add("Есть отсоединённые узлы");
            return errors.Distinct().ToList();
        }
    }
}
