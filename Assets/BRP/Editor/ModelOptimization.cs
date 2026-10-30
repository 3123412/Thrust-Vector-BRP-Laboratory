using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BRP.Editor
{
    public static class ModelOptimization
    {
        public static void Build(PartCatalog catalog)
        {
            const string folder="Assets/BRP/Art/Optimized";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            int before=0,after=0;
            foreach(var definition in catalog.parts)
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BRP/Art/Models/"+definition.id+".fbx");
                if(source==null) throw new FileNotFoundException(definition.id);
                var groups=new Dictionary<Material,List<CombineInstance>>();
                foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null) continue;before++;
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<filter.sharedMesh.subMeshCount;i++)
                    {
                        var material=materials[Mathf.Min(i,materials.Length-1)];
                        if(!groups.TryGetValue(material,out var list)) {list=new List<CombineInstance>();groups.Add(material,list);}
                        list.Add(new CombineInstance {mesh=filter.sharedMesh,subMeshIndex=i,transform=source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                    }
                }
                var root=new GameObject(definition.id);int index=0;
                root.transform.localScale=source.transform.localScale;root.transform.localRotation=source.transform.localRotation;
                foreach(var group in groups)
                {
                    var combined=new Mesh {name=definition.id+"_"+index,indexFormat=IndexFormat.UInt32};
                    combined.CombineMeshes(group.Value.ToArray(),true,true);combined.RecalculateBounds();
                    string path=folder+"/"+definition.id+"_"+index+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing==null) AssetDatabase.CreateAsset(combined,path);
                    else {EditorUtility.CopySerialized(combined,existing);Object.DestroyImmediate(combined);combined=existing;}
                    var child=new GameObject(group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(root.transform,false);
                    child.GetComponent<MeshFilter>().sharedMesh=combined;child.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
                    index++;after++;
                }
                definition.model=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+definition.id+".prefab");
                EditorUtility.SetDirty(definition);Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();Debug.Log($"BRP module renderers: {before} -> {after}");
        }
    }
}
