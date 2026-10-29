using TMPro;
using UnityEngine;

namespace BRP
{
    [CreateAssetMenu(menuName="BRP/Scene Assets")]
    public sealed class LabSceneAssets : ScriptableObject
    {
        public GameObject hangar,workbench,launchpad,teststand,planet,canopy,xrRig;
        public TMP_FontAsset font;
        public Material orbitMaterial,planetMaterial;
    }
}
