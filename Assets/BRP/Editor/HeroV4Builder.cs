using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BRP.Editor
{
    public static class HeroV4Builder
    {
        const string Art="Assets/BRP/Art";
        public static readonly string[] PartIds={"booster_engine","vacuum_engine","first_stage","second_stage","fuel_tank","oxidizer_tank","capsule","heat_shield","landing_leg","adapter","fairing","docking_port","stack_decoupler","instrument_bay","gimbal","rcs"};
        static readonly HashSet<string> MaterialNames=new HashSet<string>{"BRP_Silver","BRP_Blue","BRP_Graphite","BRP_Copper","BRP_Ceramic","BRP_Glass"};

        [Serializable] public sealed class Check
        {
            public string id,message;
            public bool passed;
            public float boundsError;
            public int materialGroups,triangles;
            public string[] materialPaths;
        }

        [Serializable] public sealed class Report
        {
            public string utc;
            public int passed,failed;
            public List<Check> checks=new List<Check>();
        }

        [MenuItem("BRP/Apply Hero V4")]
        public static Report Apply()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before applying hero models");
            AssetDatabase.Refresh();
            var catalog=Resources.Load<PartCatalog>("BRP/Catalog");
            if(catalog==null) throw new FileNotFoundException("BRP/Catalog");
            var contracts=catalog.parts.ToDictionary(p=>p.id,Contract);
            var prefabGuids=PartIds.ToDictionary(id=>id,id=>AssetDatabase.AssetPathToGUID(Art+"/Optimized/"+id+".prefab"));
            foreach(string id in PartIds)
            {
                var source=Load(Art+"/HeroV4/"+id+".fbx");var original=Load(Art+"/Models/"+id+".fbx");
                var materials=source.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                if(materials.Length==0||materials.Length>4||materials.Any(m=>m==null||!MaterialNames.Contains(m.name))) throw new InvalidDataException("Unexpected hero material layout: "+id);
                RequireTransforms(source,original,id);
                if(Error(GeometryBounds(source),GeometryBounds(original))>.0001f) throw new InvalidDataException("Hero bounds differ from original: "+id);
            }
            foreach(string id in PartIds)
            {
                string path=Art+"/HeroV4/"+id+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale=1;importer.useFileScale=true;
                importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
                importer.importNormals=ModelImporterNormals.Import;importer.meshCompression=ModelImporterMeshCompression.Off;
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                foreach(string name in MaterialNames)
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/"+name+".mat");
                    if(material==null) throw new FileNotFoundException("Missing canonical material: "+name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),material);
                }
                importer.SaveAndReimport();
            }
            ModelOptimization.Build(catalog);
            foreach(var definition in catalog.parts)
                if(Contract(definition)!=contracts[definition.id]) throw new InvalidOperationException("Definition physics or mount data changed: "+definition.id);
            foreach(string id in PartIds)
                if(AssetDatabase.AssetPathToGUID(Art+"/Optimized/"+id+".prefab")!=prefabGuids[id]) throw new InvalidOperationException("Optimized prefab GUID changed: "+id);
            AssetDatabase.SaveAssets();
            var report=Verify();if(report.failed>0) throw new InvalidDataException("Hero verification failed: "+JsonUtility.ToJson(report));
            return report;
        }

        [MenuItem("BRP/Verify Hero V4")]
        public static Report Verify()
        {
            var report=new Report {utc=DateTime.UtcNow.ToString("O")};
            foreach(string id in PartIds)
            {
                var row=new Check {id=id};
                try
                {
                    var original=Load(Art+"/Models/"+id+".fbx");var source=Load(Art+"/HeroV4/"+id+".fbx");var optimized=Load(Art+"/Optimized/"+id+".prefab");
                    RequireTransforms(source,original,id);RequireTransforms(optimized,original,id);
                    row.boundsError=Error(GeometryBounds(optimized),GeometryBounds(original));
                    if(row.boundsError>.0001f) throw new InvalidDataException("Vertex envelope changed");
                    var renderers=optimized.GetComponentsInChildren<MeshRenderer>();
                    row.materialGroups=renderers.Length;
                    if(row.materialGroups<1||row.materialGroups>4) throw new InvalidDataException("Material group budget exceeded");
                    row.materialPaths=renderers.Select(r=>AssetDatabase.GetAssetPath(r.sharedMaterial)).ToArray();
                    if(renderers.Any(r=>r.sharedMaterial==null||!MaterialNames.Contains(r.sharedMaterial.name)||AssetDatabase.GetAssetPath(r.sharedMaterial)!=Art+"/Materials/"+r.sharedMaterial.name+".mat")) throw new InvalidDataException("Embedded or unexpected material reference");
                    row.triangles=optimized.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
                    var importer=(ModelImporter)AssetImporter.GetAtPath(Art+"/HeroV4/"+id+".fbx");
                    var remaps=importer.GetExternalObjectMap();
                    foreach(string name in renderers.Select(r=>r.sharedMaterial.name).Distinct())
                    {
                        var key=new AssetImporter.SourceAssetIdentifier(typeof(Material),name);
                        if(!remaps.TryGetValue(key,out var mapped)||mapped==null||AssetDatabase.GetAssetPath(mapped)!=Art+"/Materials/"+name+".mat") throw new InvalidDataException("FBX material remap missing: "+name);
                    }
                    if(ModelOptimization.SourcePath(id)!=Art+"/HeroV4/"+id+".fbx") throw new InvalidDataException("Builder resolver does not select hero source");
                    row.passed=true;row.message="Envelope, source transforms and canonical material references preserved";report.passed++;
                }
                catch(Exception error) {row.message=error.Message;report.failed++;}
                report.checks.Add(row);
            }
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/hero-v4-verification.json",JsonUtility.ToJson(report,true));
            Debug.Log($"BRP hero models: {report.passed} passed, {report.failed} failed");return report;
        }

        static GameObject Load(string path)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);return model!=null?model:throw new FileNotFoundException(path);
        }

        static string Contract(PartDefinition definition)
        {
            return string.Join("|",typeof(PartDefinition).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>f.Name!="model"&&f.Name!="thumbnail").OrderBy(f=>f.Name).Select(f=>f.Name+"="+Convert.ToString(f.GetValue(definition),CultureInfo.InvariantCulture)));
        }

        static Bounds GeometryBounds(GameObject model)
        {
            var bounds=new Bounds();bool first=true;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var matrix=model.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var point=matrix.MultiplyPoint3x4(vertex);
                    if(first) {bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            if(first) throw new InvalidDataException("Model contains no mesh vertices: "+model.name);
            return bounds;
        }

        static float Error(Bounds a,Bounds b) => Mathf.Max((a.min-b.min).magnitude,(a.max-b.max).magnitude);

        static void RequireTransforms(GameObject model,GameObject original,string id)
        {
            if((model.transform.localScale-original.transform.localScale).magnitude>.0001f||Quaternion.Angle(model.transform.localRotation,original.transform.localRotation)>.001f) throw new InvalidDataException("Source root transform changed: "+id);
        }
    }
}
