using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BRP.Editor
{
    public static class WorldUpgradeBuilder
    {
        const string Art="Assets/BRP/Art";
        const string World="Assets/BRP/Resources/BRP/World";
        static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();

        [MenuItem("BRP/Apply World Upgrade")]
        public static void ConfigureAssets()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before applying the world upgrade");
            Directory.CreateDirectory(World);AssetDatabase.Refresh();Materials.Clear();
            Material("BRP_Shell_V3",new Color(.58f,.62f,.63f),.08f,.35f,"Brushed_Detail_V3");
            Material("BRP_Paint_V3",new Color(.22f,.29f,.33f),.3f,.42f,"Brushed_Detail_V3");
            Material("BRP_Brushed_V3",new Color(.38f,.43f,.46f),.75f,.52f,"Brushed_Detail_V3");
            Material("BRP_Concrete_V3",new Color(.58f,.59f,.55f),0,.08f,"Concrete_Detail_V3");
            Material("BRP_Refractory_V3",new Color(.34f,.31f,.27f),0,.05f,"Concrete_Detail_V3");
            Material("BRP_Orange_V3",new Color(.88f,.34f,.07f),.1f,.32f);
            Material("BRP_Rubber_V3",new Color(.025f,.028f,.03f),0,.08f);
            Material("BRP_Window_V3",new Color(.08f,.19f,.25f),.25f,.7f);
            Material("BRP_White_V3",new Color(.77f,.76f,.68f),.1f,.35f);
            var moonMaterial=Material("BRP_Moon_V3",Color.white,0,.05f,"Moon_Albedo_V3");
            Material("BRP_LunarSoil_V3",new Color(.65f,.64f,.61f),0,0,"Lunar_Regolith_V3");
            Material("BRP_LunarRock_V3",new Color(.45f,.44f,.42f),0,0,"Lunar_Regolith_V3");
            foreach(string id in new[]{"studio_hangar_v3","spaceport_v3","moon_v3","lunar_surface_v3","planet_sphere_v3","ring_saturn_v3"}) ConfigureModel(id);
            var assets=Resources.Load<LabSceneAssets>("BRP/SceneAssets");
            if(assets==null) throw new FileNotFoundException("BRP/SceneAssets");
            assets.hangar=Wrap("Hangar",LoadModel("studio_hangar_v3"));
            assets.launchpad=Wrap("Spaceport",LoadModel("spaceport_v3"));
            assets.moon=Wrap("Moon",LoadModel("moon_v3"));assets.moonMaterial=moonMaterial;
            assets.lunarSurface=Wrap("LunarSurface",LoadModel("lunar_surface_v3"));
            assets.planetSphere=Wrap("PlanetSphere",LoadModel("planet_sphere_v3"));
            assets.saturnRings=Wrap("SaturnRings",LoadModel("ring_saturn_v3"));
            foreach(string name in new[]{"Mars","Venus","Jupiter","Saturn","Uranus","Neptune"})
            {
                var material=Material("BRP_"+name+"_V3",Color.white,0,.15f,name+"_Albedo_V3");
                var instance=UnityEngine.Object.Instantiate(assets.planetSphere);instance.name=name;
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=material;
                if(name=="Saturn")
                {
                    var rings=(GameObject)PrefabUtility.InstantiatePrefab(assets.saturnRings,instance.transform);
                    rings.name="Saturn rings";rings.transform.localRotation=Quaternion.Euler(0,0,27);
                    foreach(var renderer in rings.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=Material("BRP_SaturnRings_V3",new Color(.6f,.53f,.39f),0,.1f,"Concrete_Detail_V3");
                }
                PrefabUtility.SaveAsPrefabAsset(instance,World+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(instance);
            }
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            ApplyToOpenLaboratory(assets);
            SaveSpaceportScene(assets);
            Debug.Log("BRP world upgrade applied: closed hangar, flame trench, Moon and planetary prefabs");
        }

        static Material Material(string name,Color color,float metallic,float smoothness,string texture=null)
        {
            if(Materials.TryGetValue(name,out var cached)) return cached;
            string path=Art+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name=name;AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);
            material.SetFloat("_Surface",0);material.SetFloat("_SrcBlend",1);material.SetFloat("_DstBlend",0);material.SetFloat("_ZWrite",1);material.SetFloat("_Cull",2);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.renderQueue=(int)RenderQueue.Geometry;
            if(texture!=null)
            {
                string texturePath=Art+"/Textures/"+texture+".png";
                var map=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if(map==null) throw new FileNotFoundException(texturePath);
                var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Compressed;importer.maxTextureSize=2048;importer.anisoLevel=4;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            }
            EditorUtility.SetDirty(material);Materials.Add(name,material);return material;
        }

        static void ConfigureModel(string id)
        {
            string path=Art+"/Models/"+id+".fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer==null) throw new FileNotFoundException(path);
            importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(var material in Materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),material.Key),material.Value);
            foreach(string materialPath in AssetDatabase.FindAssets("t:Material",new[]{Art+"/Materials"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!Materials.ContainsKey(material.name)) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),material.name),material);
            }
            importer.SaveAndReimport();
        }

        static GameObject LoadModel(string id)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/"+id+".fbx");
            return model!=null?model:throw new FileNotFoundException(id);
        }

        static GameObject Wrap(string name,GameObject model)
        {
            var root=new GameObject(name);var child=(GameObject)PrefabUtility.InstantiatePrefab(model,root.transform);child.name="Geometry";
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,World+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }

        static void ApplyToOpenLaboratory(LabSceneAssets assets)
        {
            var game=UnityEngine.Object.FindAnyObjectByType<LaboratoryGame>();if(game==null||game.workshop==null) return;
            var old=game.workshop.Find("Hangar Architecture");if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var room=(GameObject)PrefabUtility.InstantiatePrefab(assets.hangar,game.workshop);room.name="Hangar Architecture";room.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(float x in new[]{-9.1f,9.1f})
            {
                var wall=room.AddComponent<BoxCollider>();wall.center=new Vector3(x,3.2f,0);wall.size=new Vector3(.22f,6.4f,18.6f);
            }
            foreach(float z in new[]{-9.2f,9.2f})
            {
                var wall=room.AddComponent<BoxCollider>();wall.center=new Vector3(0,3.2f,z);wall.size=new Vector3(18.6f,6.4f,.22f);
            }
            RenderSettings.ambientSkyColor=new Color(.52f,.57f,.62f);RenderSettings.ambientEquatorColor=new Color(.37f,.41f,.44f);RenderSettings.ambientGroundColor=new Color(.2f,.22f,.24f);
            var lighting=game.gameObject.scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Lighting");
            if(lighting!=null)
            {
                foreach(var light in lighting.GetComponentsInChildren<Light>())
                {
                    if(light.type!=LightType.Point) continue;
                    light.shadows=LightShadows.None;light.range=Mathf.Max(light.range,15);
                    if(light.name=="OverheadSoftbox") {light.intensity=28;light.color=new Color(.85f,.9f,1);}
                    if(light.name=="RoomBounce") {light.intensity=35;light.color=new Color(1,.93f,.8f);}
                }
            }
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            PrefabUtility.SaveAsPrefabAsset(game.workshop.gameObject,"Assets/BRP/Prefabs/WorkshopEnvironment.prefab");
        }

        static void SaveSpaceportScene(LabSceneAssets assets)
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var spaceport=(GameObject)PrefabUtility.InstantiatePrefab(assets.launchpad,scene);spaceport.name="BRP Spaceport / Service Infrastructure";
            PrefabUtility.SaveAsPrefabAsset(spaceport,"Assets/BRP/Prefabs/SpaceportEnvironment.prefab");
            if(assets.terrain!=null)
            {
                var terrain=(GameObject)PrefabUtility.InstantiatePrefab(assets.terrain,scene);terrain.transform.localScale*=4;
            }
            var camera=new GameObject("Spaceport Overview",typeof(Camera));camera.transform.position=new Vector3(58,32,-65);camera.transform.LookAt(new Vector3(-5,8,0));
            camera.GetComponent<Camera>().farClipPlane=5000;SceneManager.MoveGameObjectToScene(camera,scene);
            var sun=new GameObject("Sun",typeof(Light));sun.transform.rotation=Quaternion.Euler(38,-40,0);
            var light=sun.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.color=new Color(1,.94f,.83f);light.shadows=LightShadows.Soft;SceneManager.MoveGameObjectToScene(sun,scene);
            RenderSettings.skybox=assets.skyMaterial;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.52f,.57f,.62f);RenderSettings.ambientEquatorColor=new Color(.37f,.41f,.44f);RenderSettings.ambientGroundColor=new Color(.2f,.22f,.24f);
            EditorSceneManager.SaveScene(scene,"Assets/BRP/Scenes/Spaceport.unity");EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
        }
    }
}
