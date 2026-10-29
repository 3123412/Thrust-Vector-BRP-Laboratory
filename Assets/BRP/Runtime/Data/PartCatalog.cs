using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BRP
{
    [CreateAssetMenu(menuName = "BRP/Catalog")]
    public sealed class PartCatalog : ScriptableObject
    {
        public PartDefinition[] parts;
        Dictionary<string, PartDefinition> byId;

        public PartDefinition Find(string id)
        {
            byId ??= parts.ToDictionary(p => p.id);
            return byId.TryGetValue(id, out var part) ? part : null;
        }

        public PartDefinition Find(PartKind kind) => parts.First(p => p.kind == kind);
    }
}
