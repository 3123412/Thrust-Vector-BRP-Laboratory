using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace BRP.Editor
{
    public static class StudioSceneBuilder
    {
        const string Art="Assets/BRP/Art";
        const string Prefabs="Assets/BRP/Prefabs";

        [MenuItem("BRP/Author Laboratory Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Prefabs);Directory.CreateDirectory(Art+"/Thumbnails");
            AssetDatabase.Refresh();
            RenderingSetup.Configure();
            var catalog=Resources.Load<PartCatalog>("BRP/Catalog");
            var assets=Resources.Load<LabSceneAssets>("BRP/SceneAssets");
            ConfigureMaterials();
            ModelOptimization.Build(catalog);
            assets.hangar=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/studio_hangar.fbx");
            assets.workbench=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/studio_bench.fbx");
            assets.terrain=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/launch_terrain.fbx");
            var spaceport=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/spaceport.fbx");
            if(spaceport!=null) assets.launchpad=spaceport;
            assets.flameMaterial=ParticleMaterial("ExhaustFlame",new Color(2.6f,1.5f,.55f),true,1.2f,.2f);
            assets.smokeMaterial=ParticleMaterial("ExhaustSmoke",new Color(.32f,.37f,.4f,.5f),false,.9f,.5f);
            assets.sparkMaterial=ParticleMaterial("HotSparks",new Color(3,1.4f,.4f),true,2,0);
            assets.skyMaterial=Material("NightSky","BRP/Starfield",Color.white);
            var earthTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/NASA_BlueMarble.png");
            if(earthTexture==null) earthTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/Earth_Day.jpg");
            if(earthTexture!=null) assets.planetMaterial.SetTexture("_MainTex",earthTexture);
            EditorUtility.SetDirty(assets.planetMaterial);
            assets.engineEffect=BuildEngineEffect(assets);
            MakeThumbnails(catalog);
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            catalog=AssetDatabase.LoadAssetAtPath<PartCatalog>("Assets/BRP/Resources/BRP/Catalog.asset");
            assets=AssetDatabase.LoadAssetAtPath<LabSceneAssets>("Assets/BRP/Resources/BRP/SceneAssets.asset");
            var lighting=new GameObject("Lighting").transform;
            var sun=Light("Daylight",lighting,LightType.Directional,new Color(1,.94f,.83f),1.8f,0);
            sun.transform.rotation=Quaternion.Euler(30,-35,0);sun.shadows=LightShadows.Soft;
            Point("OverheadSoftbox",lighting,new Vector3(-1,5,-1),new Color(.8f,.88f,1),18,14);
            Point("AmberRim",lighting,new Vector3(3,3,2),new Color(1,.75f,.46f),12,10);
            Point("BenchFill",lighting,new Vector3(-3,2,-3),new Color(.7f,.85f,.9f),10,10);
            Point("RoomBounce",lighting,new Vector3(0,5,5),new Color(.85f,.87f,.82f),30,20);
            RenderSettings.skybox=assets.skyMaterial;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.48f,.54f,.6f);RenderSettings.ambientEquatorColor=new Color(.36f,.4f,.43f);RenderSettings.ambientGroundColor=new Color(.18f,.2f,.22f);
            assets.skyMaterial.SetFloat("_Atmosphere",1);EditorUtility.SetDirty(assets.skyMaterial);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.025f,.04f,.055f);RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.012f;
            var cameraObject=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));cameraObject.tag="MainCamera";
            var camera=cameraObject.GetComponent<Camera>();camera.fieldOfView=42;camera.nearClipPlane=.035f;camera.farClipPlane=4000;camera.allowHDR=true;
            camera.transform.position=new Vector3(2.15f,3.18f,-6.3f);camera.transform.LookAt(new Vector3(.1f,2.25f,0));
            var cameraData=cameraObject.AddComponent<UniversalAdditionalCameraData>();cameraData.renderPostProcessing=true;cameraData.volumeLayerMask=1;
            var urp=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            urp.supportsHDR=true;urp.msaaSampleCount=4;urp.renderScale=1;EditorUtility.SetDirty(urp);
            var volume=new GameObject("Laboratory Color Grade",typeof(Volume)).GetComponent<Volume>();volume.transform.SetParent(lighting);volume.isGlobal=true;
            string profilePath=Art+"/Materials/StudioVolume.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(profile==null) {profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
            profile.components.RemoveAll(component=>component==null);
            if(!profile.TryGet<Tonemapping>(out var tone)) tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
            if(!profile.TryGet<Bloom>(out var bloom)) bloom=profile.Add<Bloom>();bloom.intensity.Override(.35f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.6f);
            if(!profile.TryGet<ColorAdjustments>(out var grade)) grade=profile.Add<ColorAdjustments>();grade.postExposure.Override(.2f);grade.contrast.Override(8);grade.saturation.Override(-7);
            foreach(var component in profile.components) if(!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component,profile);
            volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
            var environment=new GameObject("Workshop").transform;
            var room=(GameObject)PrefabUtility.InstantiatePrefab(assets.hangar,environment);room.name="Hangar Architecture";room.transform.localRotation=Quaternion.Euler(0,180,0);
            var bench=(GameObject)PrefabUtility.InstantiatePrefab(assets.workbench,environment);bench.name="Assembly Pedestal";
            var floor=environment.gameObject.AddComponent<BoxCollider>();floor.center=new Vector3(0,-.12f,0);floor.size=new Vector3(20,.25f,20);
            var teleport=environment.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();teleport.interactionLayers=1<<31;
            teleport.colliders.Clear();teleport.colliders.Add(floor);
            var desk=bench.AddComponent<BoxCollider>();desk.center=new Vector3(0,.96f,0);desk.size=new Vector3(2.9f,.14f,1.8f);
            Sign("WallBrand",environment,assets.font,"БРП / ОПЫТНОЕ КОНСТРУКТОРСКОЕ БЮРО",new Vector3(0,5.7f,8.2f),.15f);
            Sign("BayNumber",environment,assets.font,"01    /    СБОРКА",new Vector3(-4,4.55f,8.2f),.12f);
            Sign("SafetyLabel",environment,assets.font,"ВНИМАНИЕ\nИСПЫТАТЕЛЬНАЯ ЗОНА",new Vector3(4,1.6f,8.15f),.09f);
            for(int i=0;i<6;i++)
            {
                var definition=catalog.parts[i+10];var display=(GameObject)PrefabUtility.InstantiatePrefab(definition.model,environment);
                display.name="Display / "+definition.title;display.transform.position=new Vector3(-4.5f+i*.55f,1.08f,2.3f);display.transform.localScale=Vector3.one*.4f;
            }
            foreach(var renderer in environment.GetComponentsInChildren<MeshRenderer>()) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic);
            PrefabUtility.SaveAsPrefabAsset(environment.gameObject,Prefabs+"/WorkshopEnvironment.prefab");
            var reflection=new GameObject("Workshop Reflection",typeof(ReflectionProbe)).GetComponent<ReflectionProbe>();
            reflection.transform.SetParent(lighting);reflection.transform.position=new Vector3(0,2,0);reflection.size=new Vector3(18,8,18);
            reflection.mode=ReflectionProbeMode.Realtime;reflection.refreshMode=ReflectionProbeRefreshMode.OnAwake;reflection.resolution=128;reflection.intensity=.7f;
            var interaction=new GameObject("Interaction");interaction.AddComponent<XRInteractionManager>();
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(interaction.transform);events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var systems=new GameObject("Game Systems");
            var game=systems.AddComponent<LaboratoryGame>();game.mainCamera=camera;
            systems.AddComponent<SelectionFrame>();
            systems.AddComponent<VROperatorControls>().game=game;
            systems.AddComponent<TutorialDirector>();
            var assembly=systems.AddComponent<AssemblyController>();
            var audio=new GameObject("Audio").AddComponent<SoundEngine>();audio.transform.SetParent(systems.transform);
            audio.effects=AudioSource(audio.transform,"Interface");audio.engine=AudioSource(audio.transform,"Main Engine");audio.engineLow=AudioSource(audio.transform,"Engine Low");
            audio.ambience=AudioSource(audio.transform,"Room Ambience");audio.mechanical=AudioSource(audio.transform,"Mechanical Foley");
            AudioSetup.Configure(audio);
            var reverb=audio.mechanical.gameObject.AddComponent<AudioReverbFilter>();reverb.reverbPreset=AudioReverbPreset.Hangar;
            var root=new GameObject("Assembly / Editable Rocket").transform;root.SetParent(environment,false);root.position=new Vector3(0,1.42f,0);root.localScale=Vector3.one*AssemblyController.Scale;
            assembly.Initialize(catalog,root);assembly.Load(CraftPresets.Create(catalog,1),false);
            var ui=new GameObject("Interface",typeof(RectTransform)).AddComponent<LaboratoryUI>();
            ui.gameObject.layer=5;
            game.ConfigureScene(catalog,assets,environment,root,assembly,ui,audio);
            ui.BuildLayout(game,assets.font);ui.PopulateCatalog(PartCategory.Structure);ui.UpdateStats();ui.ShowHome();
            game.FitAssembly();
            if(assets.xrRig!=null)
            {
                var rig=(GameObject)PrefabUtility.InstantiatePrefab(assets.xrRig);rig.name="XR Origin / OpenXR";rig.SetActive(false);game.ConfigureRig(rig);
                teleport.teleportationProvider=rig.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>(true);
            }
            var monitorTarget=AssetDatabase.LoadAssetAtPath<RenderTexture>(Art+"/Materials/FlightMonitor.renderTexture");
            if(monitorTarget==null) {monitorTarget=new RenderTexture(1600,900,24);AssetDatabase.CreateAsset(monitorTarget,Art+"/Materials/FlightMonitor.renderTexture");}
            var monitorCamera=new GameObject("Flight Monitor Camera").AddComponent<Camera>();monitorCamera.CopyFrom(camera);monitorCamera.cullingMask=1<<29;monitorCamera.targetTexture=monitorTarget;monitorCamera.gameObject.SetActive(false);monitorCamera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
            monitorCamera.GetComponent<UniversalAdditionalCameraData>().allowXRRendering=false;
            game.flightCamera=monitorCamera;
            var monitor=new GameObject("Flight Monitor / VR",typeof(RectTransform),typeof(Canvas));
            monitor.transform.SetParent(environment);var monitorCanvas=monitor.GetComponent<Canvas>();monitorCanvas.renderMode=RenderMode.WorldSpace;
            var monitorRect=(RectTransform)monitor.transform;monitorRect.sizeDelta=new Vector2(1600,900);monitorRect.position=new Vector3(0,2.6f,2);monitorRect.localScale=Vector3.one*.0025f;
            var image=monitor.AddComponent<UnityEngine.UI.RawImage>();image.texture=monitorTarget;monitor.SetActive(false);game.flightMonitor=monitor;
            var effectPreview=(GameObject)PrefabUtility.InstantiatePrefab(assets.engineEffect);effectPreview.name="Engine Effect / Preview";effectPreview.SetActive(false);
            EditorSceneManager.SaveScene(scene,"Assets/BRP/Scenes/Laboratory.unity");
            PrefabUtility.SaveAsPrefabAsset(ui.gameObject,Prefabs+"/LaboratoryInterface.prefab");
            SaveSpaceportScene(assets);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/BRP/Scenes/Laboratory.unity",true)};
            SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0,2,0),new Vector3(10,6,10)),false);
            Debug.Log("BRP authored scene saved: environment, editable rocket, UI and effects");
            WorldUpgradeBuilder.ConfigureAssets();
        }

        static void SaveSpaceportScene(LabSceneAssets assets)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(assets.launchpad,scene);
            root.name="BRP Spaceport / Service Infrastructure";
            PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/SpaceportEnvironment.prefab");
            var terrain=(GameObject)PrefabUtility.InstantiatePrefab(assets.terrain,scene);terrain.transform.localScale*=4;
            var camera=new GameObject("Spaceport Overview",typeof(Camera));SceneManager.MoveGameObjectToScene(camera,scene);
            camera.transform.position=new Vector3(58,32,-65);camera.transform.LookAt(new Vector3(-5,8,0));camera.GetComponent<Camera>().farClipPlane=5000;
            var sun=new GameObject("Sun",typeof(Light));SceneManager.MoveGameObjectToScene(sun,scene);
            sun.transform.rotation=Quaternion.Euler(38,-40,0);var light=sun.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.color=new Color(1,.94f,.83f);light.shadows=LightShadows.Soft;
            EditorSceneManager.SaveScene(scene,"Assets/BRP/Scenes/Spaceport.unity");EditorSceneManager.CloseScene(scene,true);
        }

        static AudioSource AudioSource(Transform parent,string name)
        {
            var source=new GameObject(name).AddComponent<AudioSource>();source.transform.SetParent(parent,false);source.playOnAwake=false;return source;
        }
        static Light Light(string name,Transform parent,LightType type,Color color,float intensity,float range)
        {
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(parent);light.type=type;light.color=color;light.intensity=intensity;light.range=range;
            if(type==LightType.Point) light.cullingMask=~(1<<29);return light;
        }
        static void Point(string name,Transform parent,Vector3 position,Color color,float intensity,float range)
        {
            var light=Light(name,parent,LightType.Point,color,intensity,range);light.transform.position=position;
        }
        static void Sign(string name,Transform parent,TMP_FontAsset font,string text,Vector3 position,float size)
        {
            var label=new GameObject(name).AddComponent<TextMeshPro>();label.transform.SetParent(parent);label.transform.position=position;
            label.font=font;label.text=text;label.fontSize=size*10;label.color=new Color(.7f,.77f,.74f);label.alignment=TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta=new Vector2(100,12);label.fontSize=10;label.ForceMeshUpdate();
            float width=name=="WallBrand"?7:name=="BayNumber"?2.5f:1.5f;
            label.transform.localScale=Vector3.one*width/Mathf.Max(.01f,label.textBounds.size.x);
        }
        static Material Material(string name,string shader,Color color)
        {
            string path=Art+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) {material=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);EditorUtility.SetDirty(material);return material;
        }
        static Material ParticleMaterial(string name,Color color,bool additive,float softness,float turbulence)
        {
            var material=Material(name,"BRP/Exhaust",color);
            material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",additive?1:10);material.SetFloat("_Softness",softness);material.SetFloat("_Turbulence",turbulence);
            return material;
        }
        static void ConfigureMaterials()
        {
            var ground=Material("BRP_Grass","Universal Render Pipeline/Lit",new Color(.19f,.24f,.15f));
            ground.SetFloat("_Metallic",0);ground.SetFloat("_Smoothness",.08f);
            var terrainImporter=AssetImporter.GetAtPath(Art+"/Models/launch_terrain.fbx") as ModelImporter;
            if(terrainImporter!=null)
            {
                terrainImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"BRP_Concrete"),ground);
                terrainImporter.SaveAndReimport();
            }
            string[] names={"BRP_Steel","BRP_Ivory","BRP_Navy","BRP_Rubber","BRP_Floor","BRP_Orange","BRP_CyanLight","BRP_WarmLight","BRP_Screen"};
            Color[] colors={new Color(.24f,.29f,.31f),new Color(.64f,.65f,.6f),new Color(.23f,.29f,.32f),new Color(.016f,.022f,.025f),new Color(.16f,.18f,.19f),new Color(.85f,.25f,.055f),new Color(.08f,.65f,.8f),new Color(1,.63f,.27f),new Color(.035f,.14f,.16f)};
            var materials=names.Select((name,i)=>Material(name,"Universal Render Pipeline/Lit",colors[i])).ToArray();
            var silver=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/BRP_Silver.mat");
            if(silver!=null) {silver.SetFloat("_Metallic",.38f);silver.SetFloat("_Smoothness",.38f);EditorUtility.SetDirty(silver);}
            for(int i=0;i<materials.Length;i++)
            {
                materials[i].SetFloat("_Metallic",i==0?.65f:i==1||i==2?.15f:0);materials[i].SetFloat("_Smoothness",i==4?.18f:i==0?.42f:.28f);
                if(i>=6) {materials[i].EnableKeyword("_EMISSION");materials[i].SetColor("_EmissionColor",colors[i]*(i==8?1.2f:3.5f));}
            }
            foreach(string file in new[]{"studio_hangar","studio_bench","spaceport"})
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(Art+"/Models/"+file+".fbx");
                if(importer==null) continue;
                for(int i=0;i<names.Length;i++) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),names[i]),materials[i]);
                if(file=="spaceport")
                {
                    string[] extraNames={"BRP_Concrete","BRP_Asphalt","BRP_Grass"};
                    Color[] extraColors={new Color(.32f,.34f,.33f),new Color(.065f,.075f,.08f),new Color(.18f,.23f,.14f)};
                    for(int i=0;i<extraNames.Length;i++)
                    {
                        var material=Material(extraNames[i],"Universal Render Pipeline/Lit",extraColors[i]);
                        material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.15f);
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),extraNames[i]),material);
                    }
                }
                importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.SaveAndReimport();
            }
        }
        static GameObject BuildEngineEffect(LabSceneAssets assets)
        {
            var go=new GameObject("EngineExhaust");var effect=go.AddComponent<EngineVfx>();
            var mesh=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/exhaust_core.fbx");
            if(mesh!=null)
            {
                var core=(GameObject)PrefabUtility.InstantiatePrefab(mesh,go.transform);core.name="Continuous Hot Core";
                effect.continuousCore=core.GetComponentInChildren<Renderer>();
                effect.continuousCore.sharedMaterial=Material("ContinuousPlume","BRP/Plume",new Color(3,1.4f,.35f));
            }
            effect.core=Particles(go.transform,"WhiteHotCore",assets.flameMaterial,new Color(1,.84f,.52f,.9f),.2f,.24f,14,300);
            effect.plume=Particles(go.transform,"TurbulentPlume",assets.flameMaterial,new Color(.75f,.34f,.09f,.55f),.45f,.65f,8,200);
            effect.smoke=Particles(go.transform,"ExhaustSmoke",assets.smokeMaterial,new Color(.7f,.75f,.79f,.35f),3.5f,1.1f,2.5f,100);
            effect.embers=Particles(go.transform,"HotFragments",assets.sparkMaterial,new Color(1,.7f,.3f,.9f),.7f,.04f,7,15);
            var smokeSize=effect.smoke.sizeOverLifetime;smokeSize.enabled=true;smokeSize.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.25f),new Keyframe(1,3)));
            var light=new GameObject("ExhaustLight").AddComponent<Light>();light.transform.SetParent(go.transform,false);light.type=LightType.Point;light.color=new Color(1,.45f,.12f);light.range=5;light.intensity=0;light.cullingMask=1<<29;effect.glow=light;
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"/EngineExhaust.prefab");UnityEngine.Object.DestroyImmediate(go);return prefab;
        }
        static ParticleSystem Particles(Transform parent,string name,Material material,Color color,float lifetime,float size,float speed,float rate)
        {
            var system=new GameObject(name).AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            system.transform.SetParent(parent,false);system.transform.localRotation=Quaternion.Euler(90,0,0);
            var main=system.main;main.duration=5;main.loop=true;main.startLifetime=new ParticleSystem.MinMaxCurve(lifetime*.7f,lifetime);main.startSize=new ParticleSystem.MinMaxCurve(size*.7f,size*1.2f);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.8f,speed*1.2f);
            main.startColor=color;main.maxParticles=1500;main.simulationSpace=ParticleSystemSimulationSpace.World;main.playOnAwake=true;
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var emission=system.emission;emission.rateOverTime=rate;
            var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=name=="ExhaustSmoke"?22:6;shape.radius=name=="HotFragments"?.16f:.12f;
            var colorLife=system.colorOverLifetime;colorLife.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(.55f,.6f),new GradientAlphaKey(0,1)});colorLife.color=gradient;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.sortingFudge=name=="WhiteHotCore"?2:0;
            system.Play();return system;
        }
        public static void MakeThumbnails(PartCatalog catalog,bool overwrite=false)
        {
            var originalScene=SceneManager.GetActiveScene();
            var previewScene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var camera=new GameObject("ThumbnailCamera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,previewScene);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.11f,0);camera.fieldOfView=28;camera.nearClipPlane=.01f;camera.cullingMask=1<<30;
            var light=new GameObject("ThumbnailLight").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,previewScene);light.type=LightType.Directional;light.intensity=3;light.transform.rotation=Quaternion.Euler(35,-35,0);
            var fill=new GameObject("ThumbnailFill").AddComponent<Light>();SceneManager.MoveGameObjectToScene(fill.gameObject,previewScene);fill.type=LightType.Directional;fill.intensity=1.8f;fill.transform.rotation=Quaternion.Euler(-25,135,0);
            var target=new RenderTexture(256,256,24);camera.targetTexture=target;
            foreach(var d in catalog.parts)
            {
                string path=Art+"/Thumbnails/"+d.id+".png";
                if(!File.Exists(path)||overwrite)
                {
                    var model=UnityEngine.Object.Instantiate(d.model);SceneManager.MoveGameObjectToScene(model,previewScene);
                    foreach(var child in model.GetComponentsInChildren<Transform>()) child.gameObject.layer=30;
                    var bounds=new Bounds();bool first=true;
                    foreach(var renderer in model.GetComponentsInChildren<Renderer>()) {if(first) {bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
                    float size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                    camera.transform.position=bounds.center+new Vector3(1,.55f,-1.6f).normalized*size*2.8f;camera.transform.LookAt(bounds.center);
                    camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                    var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();
                    File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(model);
                    AssetDatabase.ImportAsset(path);
                }
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
                d.thumbnail=AssetDatabase.LoadAssetAtPath<Sprite>(path);EditorUtility.SetDirty(d);
            }
            camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.CloseScene(previewScene,true);SceneManager.SetActiveScene(originalScene);
        }
    }
}
