using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;

namespace BRP.Editor
{
    public static class ContentBuilder
    {
        const string ResourcesPath="Assets/BRP/Resources/BRP";
        [Serializable] sealed class SeedFile { public SeedPart[] parts; }
        [Serializable] sealed class SeedPart
        {
            public string id,title,description,kind,category,mount;
            public float dryMass,height,radius,fuelCapacity,oxidizerCapacity,thrust,specificImpulse,dragCoefficient,liftArea;
            public float electricCapacity,electricGeneration,electricConsumption,controlTorque,temperatureLimit,strength,heatCapacity,coolingArea,parachuteArea,price;
            public bool surface;
        }

        [MenuItem("BRP/Build Content")]
        public static void BuildContent()
        {
            Directory.CreateDirectory(ResourcesPath+"/Definitions");Directory.CreateDirectory("Assets/BRP/Art/Materials");Directory.CreateDirectory("Assets/BRP/Scenes");
            AssetDatabase.Refresh();
            var seed=JsonUtility.FromJson<SeedFile>(File.ReadAllText(ResourcesPath+"/parts.json"));
            var catalog=LoadOrCreate<PartCatalog>(ResourcesPath+"/Catalog.asset");
            catalog.parts=new PartDefinition[seed.parts.Length];
            for(int i=0;i<seed.parts.Length;i++)
            {
                var row=seed.parts[i];var d=LoadOrCreate<PartDefinition>(ResourcesPath+"/Definitions/"+row.id+".asset");
                foreach(var field in typeof(SeedPart).GetFields(BindingFlags.Public|BindingFlags.Instance))
                {
                    if(field.Name=="kind"||field.Name=="category"||field.Name=="mount") continue;
                    typeof(PartDefinition).GetField(field.Name)?.SetValue(d,field.GetValue(row));
                }
                d.kind=Enum.Parse<PartKind>(row.kind);d.category=Enum.Parse<PartCategory>(row.category);d.mount=Enum.Parse<MountType>(row.mount);
                d.model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BRP/Art/Models/"+row.id+".fbx");
                if(d.model==null) throw new InvalidDataException("Missing model: "+row.id);
                catalog.parts[i]=d;EditorUtility.SetDirty(d);
            }
            EditorUtility.SetDirty(catalog);
            var assets=LoadOrCreate<LabSceneAssets>(ResourcesPath+"/SceneAssets.asset");
            assets.hangar=Model("hangar");assets.workbench=Model("workbench");assets.launchpad=Model("launchpad");assets.teststand=Model("teststand");
            assets.planet=Model("planet");assets.canopy=Model("canopy");
            assets.font=BuildFont();
            assets.orbitMaterial=MakeMaterial("Signal",new Color(1,1,1),false,true);
            var earth=AssetDatabase.LoadAssetAtPath<Material>("Assets/BRP/Art/Materials/Earth.mat");
            if(earth==null) {earth=new Material(Shader.Find("BRP/Earth"));AssetDatabase.CreateAsset(earth,"Assets/BRP/Art/Materials/Earth.mat");}
            assets.planetMaterial=earth;
            string rig=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Samples/XR Interaction Toolkit"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains("Starter Assets")&&p.EndsWith("XR Origin (XR Rig).prefab")).OrderByDescending(p=>p).FirstOrDefault();
            assets.xrRig=rig==null?null:AssetDatabase.LoadAssetAtPath<GameObject>(rig);
            EditorUtility.SetDirty(assets);
            ConfigureModels();
            PlayerSettings.companyName="BRP";PlayerSettings.productName="Вектор Тяги: Лаборатория BRP";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            if(XRGeneralSettings.Instance!=null) {XRGeneralSettings.Instance.InitManagerOnStart=false;EditorUtility.SetDirty(XRGeneralSettings.Instance);}
            AssetDatabase.SaveAssets();
            BuildScene();
            Debug.Log("BRP content ready: "+catalog.parts.Length+" parts, "+MissionDefinition.All.Length+" missions");
        }

        static GameObject Model(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BRP/Art/Models/"+name+".fbx");
        static T LoadOrCreate<T>(string path) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset==null) {asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}
            return asset;
        }
        static Material MakeMaterial(string name,Color color,bool metal=false,bool unlit=false)
        {
            string path="Assets/BRP/Art/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) {material=new Material(Shader.Find(unlit?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);
            if(!unlit) {material.SetFloat("_Metallic",metal?.7f:.1f);material.SetFloat("_Smoothness",metal?.6f:.3f);}
            EditorUtility.SetDirty(material);return material;
        }
        static void ConfigureModels()
        {
            string[] names={"BRP_Silver","BRP_Blue","BRP_Graphite","BRP_Orange","BRP_Copper","BRP_Ceramic","BRP_Glass","BRP_Solar","BRP_Concrete"};
            Color[] colors={new Color(.64f,.71f,.76f),new Color(.025f,.10f,.17f),new Color(.025f,.037f,.046f),new Color(.85f,.25f,.055f),new Color(.48f,.22f,.09f),new Color(.79f,.81f,.77f),new Color(.025f,.25f,.36f),new Color(.025f,.065f,.22f),new Color(.11f,.14f,.16f)};
            var materials=names.Select((name,i)=>MakeMaterial(name,colors[i],i!=5&&i!=8)).ToArray();
            foreach(string path in Directory.GetFiles("Assets/BRP/Art/Models","*.fbx"))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
                importer.globalScale=1;importer.useFileScale=true;
                for(int i=0;i<names.Length;i++) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),names[i]),materials[i]);
                importer.SaveAndReimport();
            }
        }
        static TMP_FontAsset BuildFont()
        {
            string path=ResourcesPath+"/InterfaceFont.asset";
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(font!=null) return font;
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf");
            font=TMP_FontAsset.CreateFontAsset(source,48,6,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic);
            font.name="BRP Interface";
            font.TryAddCharacters(string.Concat(Enumerable.Range(32,95).Concat(Enumerable.Range(0x400,0x130)).Select(c=>(char)c))+"×Δ∞°−…");
            AssetDatabase.CreateAsset(font,path);
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var texture in font.atlasTextures) if(texture!=null) AssetDatabase.AddObjectToAsset(texture,font);
            EditorUtility.SetDirty(font);return font;
        }
        public static void BuildScene()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";
            var cam=camera.GetComponent<Camera>();cam.fieldOfView=42;cam.nearClipPlane=.04f;cam.farClipPlane=2000;
            cam.backgroundColor=new Color(.015f,.027f,.045f);cam.clearFlags=CameraClearFlags.SolidColor;
            var data=camera.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;
            var sun=new GameObject("Key Light",typeof(Light));var light=sun.GetComponent<Light>();light.type=LightType.Directional;
            light.intensity=2.2f;light.color=new Color(.86f,.92f,1);light.shadows=LightShadows.Soft;
            sun.transform.rotation=Quaternion.Euler(42,-35,0);
            Point("BenchFill",new Vector3(-3,4,-3),new Color(.4f,.7f,1),18,10);
            Point("WarmRim",new Vector3(3,4,2),new Color(1,.5f,.25f),25,12);
            Point("HangarFill",new Vector3(0,5,4),new Color(.45f,.65f,1),30,18);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.19f,.25f,.31f);
            RenderSettings.fog=true;RenderSettings.fogColor=cam.backgroundColor;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.009f;
            var volume=new GameObject("Global Volume",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bloom=profile.Add<Bloom>();bloom.intensity.Override(.2f);bloom.threshold.Override(1.2f);
            AssetDatabase.CreateAsset(profile,"Assets/BRP/Art/Materials/LabVolume.asset");volume.sharedProfile=profile;
            var game=new GameObject("Laboratory",typeof(LaboratoryGame)).GetComponent<LaboratoryGame>();game.mainCamera=cam;
            EditorSceneManager.SaveScene(scene,"Assets/BRP/Scenes/Laboratory.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/BRP/Scenes/Laboratory.unity",true)};
            AssetDatabase.SaveAssets();
        }
        static void Point(string name,Vector3 position,Color color,float intensity,float range)
        {
            var light=new GameObject(name,typeof(Light)).GetComponent<Light>();light.type=LightType.Point;light.transform.position=position;light.color=color;light.intensity=intensity;light.range=range;
        }
        [MenuItem("BRP/Build Windows")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Delivery/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/BRP/Scenes/Laboratory.unity"},locationPathName="Delivery/Windows/BRP.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Build failed: "+report.summary.result);
            Debug.Log("BRP build ready: "+report.summary.totalSize);
        }
    }
}
