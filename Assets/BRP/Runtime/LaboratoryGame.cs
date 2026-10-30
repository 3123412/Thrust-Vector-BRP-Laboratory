using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR.Management;

namespace BRP
{
    public sealed class LaboratoryGame : MonoBehaviour
    {
        public static LaboratoryGame Instance { get; private set; }
        [field: SerializeField] public PartCatalog Catalog { get; private set; }
        [field: SerializeField] public LabSceneAssets Assets { get; private set; }
        [field: SerializeField] public AssemblyController Assembly { get; private set; }
        [field: SerializeField] public LaboratoryUI UI { get; private set; }
        [field: SerializeField] public SoundEngine Sound { get; private set; }
        public PlayerProgress Progress { get; private set; }
        public MissionDefinition[] Missions { get; private set; }
        public PartView Selected { get; private set; }
        public FlightBody Flight { get; private set; }
        public FlightBody Target { get; private set; }
        public FlightBody Payload { get; private set; }
        public int TimeWarp { get; private set; }=1;
        public MissionDefinition CurrentMission { get; private set; }
        public bool IsFlying=>Flight!=null;
        public bool OnStand { get; private set; }
        public bool TimeTrial { get; private set; }
        public bool VREnabled=>vrEnabled;
        public Camera mainCamera;
        public Camera flightCamera;
        public GameObject flightMonitor;
        public Transform workshop;
        [SerializeField] Transform benchRoot;
        FlightPresentation presentation;
        GameObject flightScene;
        [SerializeField] GameObject xrRig;
        int workshopMask=-1;
        LineRenderer snapMarker,comLine;
        PartView dragging;
        List<PartView> dragGroup;
        Vector3 dragStart;
        Dictionary<PartView,Vector3> dragOffsets=new Dictionary<PartView,Vector3>();
        Plane dragPlane;
        Vector3 cameraTarget=new Vector3(.1f,2.25f,0);
        float yaw=-18,pitch=8,distance=6.7f,toastUntil,timer,uiTimer,flightAccumulator;
        double orbitPowerTime;
        bool missionDone,resultShown,orbitReached,vrEnabled;
        readonly List<FlightBody> debris=new List<FlightBody>();
        readonly List<FlightSample> samples=new List<FlightSample>();
        public AscentProgram Ascent { get; }=new AscentProgram();
        readonly RecoveryProgram recovery=new RecoveryProgram();
        readonly RendezvousProgram rendezvous=new RendezvousProgram();

        void Awake()
        {
            Instance=this;Application.runInBackground=true;Application.targetFrameRate=90;
            if(Catalog==null||Assets==null||workshop==null||Assembly==null||UI==null) throw new InvalidOperationException("Open the authored Laboratory scene");
            Progress=SaveStore.Load();Missions=MissionDefinition.All;AudioListener.volume=Progress.volume;
            Sound.ApplyVolumes(Progress);
            QualitySettings.SetQualityLevel(Mathf.Min(QualitySettings.names.Length-1,Mathf.Clamp(Progress.quality,0,2)*2+1),true);
            Assembly.Initialize(Catalog,benchRoot);
            Assembly.Changed+=OnAssemblyChanged;
            UI.Initialize(this,Assets.font);
            snapMarker=CreateLine("SnapMarker",UiTheme.Cyan,.008f);
            comLine=CreateLine("CenterOfMass",UiTheme.Orange,.008f);
            SetCamera();UI.UpdateStats();
        }

        public void ConfigureScene(PartCatalog catalog,LabSceneAssets assets,Transform environment,Transform assemblyTransform,AssemblyController assembly,LaboratoryUI ui,SoundEngine sound)
        {
            Catalog=catalog;Assets=assets;workshop=environment;benchRoot=assemblyTransform;Assembly=assembly;UI=ui;Sound=sound;
            Progress=new PlayerProgress();Missions=MissionDefinition.All;
        }
        public void ConfigureRig(GameObject rig) => xrRig=rig;
        public void ScaleAssembly(float factor)
        {
            if(IsFlying) return;
            float scale=Mathf.Clamp(benchRoot.localScale.x*factor,.13f,.5f);
            benchRoot.localScale=Vector3.one*scale;
            float bottom=Assembly.Blueprint.parts.Select(p=>p.position.y).DefaultIfEmpty(0).Min();
            benchRoot.position=new Vector3(0,1.06f-bottom*scale,0);
            OnAssemblyChanged();
        }
        public void FitAssembly()
        {
            if(Assembly.Blueprint.parts.Count==0) return;
            float bottom=Assembly.Blueprint.parts.Min(p=>p.position.y);
            float top=Assembly.Blueprint.parts.Max(p=>p.position.y+Catalog.Find(p.partId).height);
            float scale=Mathf.Min(AssemblyController.Scale,2.5f/Mathf.Max(1,top-bottom));
            benchRoot.localScale=Vector3.one*scale;benchRoot.position=new Vector3(0,1.06f-bottom*scale,0);
            if(Application.isPlaying) OnAssemblyChanged();
        }

        LineRenderer CreateLine(string name,Color color,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.material=Assets.orbitMaterial;
            line.startColor=line.endColor=color;line.startWidth=line.endWidth=width;return line;
        }
        void SetCamera()
        {
            if(mainCamera==null) mainCamera=Camera.main;
            if(vrEnabled||IsFlying) return;
            mainCamera.transform.position=cameraTarget+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);
            mainCamera.transform.LookAt(cameraTarget);
        }
        void OnAssemblyChanged()
        {
            UI?.UpdateStats();
            if(Assembly.Blueprint.parts.Count>0)
            {
                var body=new FlightBody(Assembly.Blueprint,Catalog);
                var center=benchRoot.TransformPoint(body.centerOfMass.Float);
                comLine.positionCount=2;comLine.SetPosition(0,center+Vector3.left*.5f);comLine.SetPosition(1,center+Vector3.right*.5f);
            }
            else comLine.positionCount=0;
        }
        public void Toast(string message) {UI.toast.text=message;toastUntil=Time.unscaledTime+4;}
        public void Select(PartView view) {Selected=view;if(view!=null) UI.ShowDefinition(view.Definition);}
        public void Spawn(PartDefinition definition)
        {
            if(IsFlying) return;
            var view=Assembly.Spawn(definition);if(view==null) {Toast("Предел: 128 деталей");return;}
            Select(view);Sound.Play(SoundCue.Click);Toast("Перенесите деталь к сборочному узлу");
        }
        public void BeginCatalogDrag(PartDefinition definition)
        {
            if(IsFlying) return;
            var view=Assembly.Spawn(definition);if(view==null) return;
            Select(view);BeginDrag(view,false);
        }
        void BeginDrag(PartView view,bool detach)
        {
            if(view.Attached&&!detach) {Select(view);Toast("Выберите «Снять», чтобы перенести установленную деталь");return;}
            dragging=view;dragGroup=detach?Assembly.Detach(view):new List<PartView>{view};
            dragOffsets.Clear();dragStart=view.transform.position;
            foreach(var item in dragGroup) {dragOffsets[item]=item.transform.position-dragStart;item.Body.isKinematic=true;item.Body.useGravity=false;}
            dragPlane=new Plane(mainCamera.transform.forward,benchRoot.position+Vector3.up*.4f);
        }
        public void EndDrag()
        {
            if(dragging==null) return;
            if(Assembly.TryTarget(dragging,out var target)) {Assembly.Attach(dragging,target,dragGroup);Sound.Play(SoundCue.Snap);Toast("Узел зафиксирован");}
            else
            {
                foreach(var item in dragGroup) {item.Body.isKinematic=false;item.Body.useGravity=true;item.Placement.position=benchRoot.InverseTransformPoint(item.transform.position);}
                Sound.Play(SoundCue.Click);
            }
            dragging=null;dragGroup=null;snapMarker.positionCount=0;
        }
        public void DetachSelected()
        {
            if(Selected==null||IsFlying) return;
            if(vrEnabled) {Assembly.Detach(Selected);Toast("Подсборка разблокирована. Захватите её контроллером.");}
            else {BeginDrag(Selected,true);Toast("Перенесите подсборку и нажмите ЛКМ для установки");}
        }
        public void DeleteSelected() {if(Selected!=null&&!IsFlying) {Assembly.Delete(Selected);Selected=null;Sound.Play(SoundCue.Click);}}
        public void SymmetrySelected() {if(Selected!=null&&!IsFlying) Assembly.Symmetry(Selected);}
        public void SetSelectedStage(int stage) {if(Selected!=null&&!IsFlying) {Assembly.SetStage(Selected,stage);Toast("Ступень "+(stage+1));}}
        public void ClearCraft() {if(!IsFlying) {Assembly.Load(new CraftBlueprint());Selected=null;}}
        public void LoadPreset(int level)
        {
            if(TimeTrial) {Toast("В сборке на время готовые чертежи отключены");return;}
            if(IsFlying) ReturnToWorkshop();
            Assembly.Load(CraftPresets.Create(Catalog,level));Selected=null;Sound.Play(SoundCue.Click);
            FitAssembly();
        }
        public void SaveBlueprint()
        {
            if(Assembly.Blueprint.parts.Count==0) {Toast("Сначала соберите конструкцию");return;}
            var craft=Assembly.Blueprint.Clone();craft.name="БРП / "+DateTime.Now.ToString("dd.MM HH:mm:ss");
            Progress.blueprints.Add(craft);SaveStore.Save(Progress);Toast("Чертёж сохранён");Sound.Play(SoundCue.Snap);
        }
        public void StartSandbox()
        {
            ReturnToWorkshop();CurrentMission=null;TimeTrial=false;timer=0;missionDone=false;
            UI.missionTitle.text="СВОБОДНЫЙ КОНСТРУКТОР";UI.missionGoal.text="Все 50 модулей доступны. Создавайте, испытывайте и сохраняйте свои конструкции.";
        }
        public void StartTimeTrial()
        {
            StartSandbox();TimeTrial=true;Assembly.Load(new CraftBlueprint());
            Assembly.snapCount=Assembly.detachCount=0;
            UI.missionTitle.text="СБОРКА НА ВРЕМЯ";UI.missionGoal.text="Корпус, двигатель, приборный отсек, капсула, обтекатель и четыре стабилизатора. Затем проверка на стенде.";
        }
        public void StartMission(int id)
        {
            ReturnToWorkshop();CurrentMission=Missions.First(m=>m.id==id);TimeTrial=false;timer=0;missionDone=false;
            Assembly.snapCount=Assembly.detachCount=0;
            UI.missionTitle.text=$"{id:00} / {CurrentMission.title.ToUpperInvariant()}";
            UI.missionGoal.text=CurrentMission.goal;
            Toast(CurrentMission.brief);
        }

        public void StartFlight(bool stand,bool orbital=false)
        {
            if(IsFlying) return;
            EndDrag();
            var errors=Assembly.Blueprint.Validate(Catalog);
            if(!orbital&&!Assembly.Blueprint.parts.Any(p=>Catalog.Find(p.partId).IsEngine)) errors.Add("Установите двигатель");
            if(errors.Count>0) {Toast(string.Join(" · ",errors.Take(2)));Sound.Play(SoundCue.Error);return;}
            EndDrag();Selected=null;OnStand=stand;Flight=new FlightBody(Assembly.Blueprint.Clone(),Catalog);
            Flight.windSpeed=CurrentMission?.wind??0;Flight.launched=true;
            Flight.lastEvent=stand?"Огневые испытания":"Двигатели работают";
            recovery.active=false;rendezvous.active=false;Ascent.active=false;
            debris.Clear();Target=null;Payload=null;TimeWarp=1;orbitPowerTime=0;orbitReached=false;resultShown=false;flightAccumulator=0;samples.Clear();
            Flight.PartFailed+=p=> {Sound.Play(SoundCue.Explosion);if(presentation!=null) presentation.Burst(p.placement.position);};
            workshop.gameObject.SetActive(vrEnabled);benchRoot.gameObject.SetActive(false);comLine.gameObject.SetActive(false);snapMarker.positionCount=0;
            flightScene=new GameObject("FlightScene");presentation=flightScene.AddComponent<FlightPresentation>();
            workshopMask=mainCamera.cullingMask;
            if(vrEnabled)
            {
                flightMonitor.SetActive(true);flightCamera.gameObject.SetActive(true);mainCamera.cullingMask&=~(1<<29);
                presentation.Initialize(Flight,Assets,flightCamera);
            }
            else {mainCamera.cullingMask=(1<<29)|(1<<5);presentation.Initialize(Flight,Assets,mainCamera);}
            UI.SetFlight(true);UI.CloseDialog();Sound.Play(SoundCue.Launch);
            SetLocomotion(false);
            Toast(stand?"Стенд: X отключает тягу. F открывает команды.":"Запуск. F: команды, Space: ступень, M: орбитальная карта.");
        }
        public void ReturnToWorkshop()
        {
            if(Flight!=null) ExportTelemetry();
            Flight=null;Target=null;Payload=null;debris.Clear();OnStand=false;TimeWarp=1;
            if(flightScene!=null) Destroy(flightScene);
            if(workshop!=null) workshop.gameObject.SetActive(true);
            if(benchRoot!=null) benchRoot.gameObject.SetActive(true);
            if(flightMonitor!=null) flightMonitor.SetActive(false);
            if(flightCamera!=null) flightCamera.gameObject.SetActive(false);
            if(mainCamera!=null) mainCamera.cullingMask=workshopMask;
            if(mainCamera!=null) mainCamera.farClipPlane=4000;
            if(Assets!=null) {RenderSettings.skybox=Assets.skyMaterial;Assets.skyMaterial.SetFloat("_Atmosphere",1);RenderSettings.fog=true;}
            if(comLine!=null) comLine.gameObject.SetActive(true);
            if(UI!=null) UI.SetFlight(false);
            if(Sound!=null) Sound.SetEngine(0);SetCamera();
            SetLocomotion(true);
        }
        void SetLocomotion(bool enabled)
        {
            if(!vrEnabled||xrRig==null) return;
            foreach(var provider in xrRig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true)) provider.enabled=enabled;
        }
        public void NextStage()
        {
            if(Flight==null) return;
            var detached=Flight.Separate();if(detached!=null) debris.Add(detached);
            Sound.Play(SoundCue.Separate);Toast(Flight.lastEvent);
        }
        public void ReleasePayload()
        {
            if(Flight==null) return;
            var detached=Flight.Separate(true);if(detached!=null) {debris.Add(detached);Payload=detached;Flight.payloadReleased=true;}
            Sound.Play(SoundCue.Separate);Toast(Flight.lastEvent);
        }
        public void DeployParachute() {if(Flight!=null) {Flight.DeployParachutes();Toast(Flight.lastEvent);}}
        public void SetGuidance(GuidanceMode mode) {if(Flight!=null) {Flight.guidance=mode;Toast("Режим ориентации: "+mode);}}
        public void ToggleMap() {if(presentation!=null) presentation.map=!presentation.map;}
        public void StartOrbitalPractice()
        {
            if(CurrentMission!=null&&CurrentMission.id!=18&&CurrentMission.id!=19) {Toast("Орбитальный стенд доступен в свободном режиме и миссиях сближения");return;}
            if(Flight!=null) ReturnToWorkshop();
            Assembly.Load(CraftPresets.OrbitalVehicle(Catalog));StartFlight(false,true);
            if(Flight==null) return;
            double radius=OrbitalMechanics.Radius+180000;
            Flight.position=new DVector3(0,radius,0);Flight.velocity=new DVector3(Math.Sqrt(OrbitalMechanics.Mu/radius),0,0);
            Flight.throttle=0;Flight.guidance=GuidanceMode.Manual;
            Target=new FlightBody(CraftPresets.OrbitalVehicle(Catalog),Catalog);
            Target.position=Flight.position+new DVector3(0,80,0);Target.velocity=Flight.velocity;Target.throttle=0;Target.launched=true;
            Target.orientation=new DQuaternion(Quaternion.Euler(0,0,180));Flight.lastEvent="Орбитальный стенд / высота 180 км";presentation.map=false;
            Toast("Орбитальный стенд: I/K, J/L, U/O для сближения");
        }
        public void TryDock()
        {
            if(Flight==null||Target==null) {Toast("Сначала выберите цель на орбитальном стенде");return;}
            if(!Flight.Has(PartKind.DockingPort)||!Target.Has(PartKind.DockingPort)) {Toast("Нужны совместимые стыковочные узлы");return;}
            if(!Flight.CanDock(Target,out double separation,out double speed,out double angle)) {Toast($"Узлы: {separation:F1} м / {speed:F2} м/с / {angle:F1}°. Нужно ≤0,6 м / 0,2 м/с / 5°");return;}
            Flight.Dock(Target);Target=null;rendezvous.active=false;Flight.translationInput=new DVector3();Sound.Play(SoundCue.Snap);Toast("Стыковка выполнена");
        }
        public void PlanDeorbit()
        {
            if(Flight==null) return;
            Ascent.active=false;recovery.active=true;Flight.guidance=GuidanceMode.Retrograde;
            Toast("Программа возвращения: торможение, отделение капсулы и раскрытие парашютов");
        }
        public void ToggleAscent()
        {
            if(Flight==null||OnStand) return;
            Ascent.active=!Ascent.active;recovery.active=false;
            Toast(Ascent.active?"Программа выведения / целевая орбита 180 км":"Программа выведения отключена");
        }
        public void ToggleRendezvous()
        {
            if(Target==null) {Toast("Сначала откройте орбитальный стенд");return;}
            rendezvous.active=!rendezvous.active;Toast(rendezvous.active?"Помощник сближения включён":"Ручное сближение");
        }
        public void ToggleValve() {if(Flight==null) return;Flight.valveOpen=!Flight.valveOpen;Toast(Flight.valveOpen?"Топливный клапан открыт":"Топливный клапан закрыт");}
        public void SeparateCapsule()
        {
            if(Flight==null) return;
            var detached=Flight.SeparateCapsule();if(detached!=null) {debris.Add(detached);Sound.Play(SoundCue.Separate);}
            Toast(Flight.lastEvent);
        }
        void UpdatePrograms(double dt)
        {
            if(Ascent.active) {var detached=Ascent.Update(Flight,dt);if(detached!=null) debris.Add(detached);}
            if(Target!=null&&rendezvous.Update(Flight,Target)) TryDock();
            if(!recovery.active) return;
            Ascent.active=false;
            var carrier=recovery.Update(Flight);if(carrier!=null) {debris.Add(carrier);Sound.Play(SoundCue.Separate);}
        }
        public void SetVolume(float delta) {Progress.volume=Mathf.Clamp01(Progress.volume+delta);AudioListener.volume=Progress.volume;SaveStore.Save(Progress);Toast("Громкость: "+Mathf.RoundToInt(Progress.volume*100)+"%");}
        public void SetAudioChannel(int channel,float value)
        {
            if(channel==0) Progress.interfaceVolume=value;
            if(channel==1) Progress.engineVolume=value;
            if(channel==2) Progress.environmentVolume=value;
            Sound.ApplyVolumes(Progress);SaveStore.Save(Progress);
        }
        public void SetQuality(int index) {Progress.quality=Mathf.Clamp(index,0,2);QualitySettings.SetQualityLevel(Mathf.Min(QualitySettings.names.Length-1,Progress.quality*2+1),true);SaveStore.Save(Progress);Toast("Настройки графики применены");}

        public void EnableVR()
        {
            if(IsFlying) ReturnToWorkshop();
            StartCoroutine(StartXR());
        }
        IEnumerator StartXR()
        {
            if(vrEnabled) yield break;
            var manager=XRGeneralSettings.Instance?.Manager;
            if(manager==null||Assets.xrRig==null) {Toast("Проверьте OpenXR runtime и подключение контроллеров");yield break;}
            Toast("Подключение OpenXR…");yield return manager.InitializeLoader();
            if(manager.activeLoader==null) {Toast("OpenXR runtime недоступен. Продолжайте в режиме ПК.");yield break;}
            manager.StartSubsystems();vrEnabled=true;mainCamera.gameObject.SetActive(false);
            if(xrRig==null) xrRig=Instantiate(Assets.xrRig);
            xrRig.SetActive(true);xrRig.transform.position=new Vector3(0,0,-2.4f);
            var camera=xrRig.GetComponentInChildren<Camera>();if(camera!=null) mainCamera=camera;
            mainCamera.cullingMask&=~(1<<29);ScaleAssembly(.55f);
            UI.canvas.renderMode=RenderMode.WorldSpace;UI.canvas.worldCamera=mainCamera;
            UI.root.sizeDelta=new Vector2(1920,1080);UI.root.position=new Vector3(-1.44f,2.8f,.5f);UI.root.localScale=Vector3.one*.0015f;
            var raycaster=UI.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            var eventSystem=FindAnyObjectByType<EventSystem>();
            var old=eventSystem.GetComponent<InputSystemUIInputModule>();if(old!=null) Destroy(old);
            eventSystem.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            Toast("VR включён. Используйте луч для панелей и захват для деталей.");
        }

        void Update()
        {
            if(Time.unscaledTime>toastUntil&&UI.toast.text.Length>0) UI.toast.text="";
            if(!UI.ModalOpen) timer+=Time.unscaledDeltaTime;
            if(IsFlying) UpdateFlight();else UpdateWorkshop();
            if(CurrentMission!=null&&!missionDone) CheckMission();
            if(TimeTrial&&!missionDone&&OnStand&&Flight!=null&&Flight.poweredTime>=3&&HasTrialParts())
            {
                missionDone=true;Progress.bestTimes.Add(timer);Progress.bestTimes=Progress.bestTimes.OrderBy(t=>t).Take(10).ToList();SaveStore.Save(Progress);
                Sound.Play(SoundCue.Success);UI.ShowResult("ПОПЫТКА ЗАСЧИТАНА",$"Время сборки: {timer:F2} с\nКонструкция прошла проверку на стенде.");
            }
        }
        void UpdateWorkshop()
        {
            if(vrEnabled) return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;if(mouse==null||keyboard==null) return;
            if(keyboard.escapeKey.wasPressedThisFrame) {if(UI.ModalOpen) UI.CloseDialog();else UI.ShowHome();}
            if(UI.ModalOpen) return;
            if(mouse.rightButton.isPressed) {var delta=mouse.delta.ReadValue();yaw+=delta.x*.2f;pitch=Mathf.Clamp(pitch-delta.y*.15f,-10,55);SetCamera();}
            if(!EventSystem.current.IsPointerOverGameObject()&&Mathf.Abs(mouse.scroll.ReadValue().y)>0) {distance=Mathf.Clamp(distance-mouse.scroll.ReadValue().y*.003f,2.5f,10);SetCamera();}
            var ray=mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            if(dragging!=null)
            {
                if(dragPlane.Raycast(ray,out float enter))
                {
                    Vector3 location=ray.GetPoint(enter);
                    foreach(var item in dragGroup) item.transform.position=location+dragOffsets[item];
                }
                if(keyboard.rKey.wasPressedThisFrame)
                {
                    var rotation=Quaternion.Euler(0,45,0);
                    foreach(var item in dragGroup)
                    {
                        dragOffsets[item]=rotation*dragOffsets[item];item.transform.rotation=rotation*item.transform.rotation;
                    }
                }
                if(Assembly.TryTarget(dragging,out var target)) DrawSnap(benchRoot.TransformPoint(target.position));else snapMarker.positionCount=0;
                if(mouse.leftButton.wasReleasedThisFrame&&!mouse.leftButton.isPressed) EndDrag();
            }
            else if(mouse.leftButton.wasPressedThisFrame&&!EventSystem.current.IsPointerOverGameObject())
            {
                if(Physics.Raycast(ray,out var hit,30))
                {
                    var view=hit.collider.GetComponentInParent<PartView>();
                    if(view!=null) {Select(view);if(!view.Attached) BeginDrag(view,false);}
                    else Select(null);
                }
            }
            if(keyboard.deleteKey.wasPressedThisFrame) DeleteSelected();
            if(keyboard.ctrlKey.isPressed&&keyboard.zKey.wasPressedThisFrame) Assembly.Undo();
            if(keyboard.ctrlKey.isPressed&&keyboard.yKey.wasPressedThisFrame) Assembly.Redo();
            if(TimeTrial) UI.missionGoal.text=$"Время: {timer:F2} с. Соберите пять основных типов и четыре стабилизатора; проверьте на стенде.";
        }
        void DrawSnap(Vector3 center)
        {
            snapMarker.positionCount=49;
            for(int i=0;i<49;i++) {float a=i*Mathf.PI*2/48;snapMarker.SetPosition(i,center+new Vector3(Mathf.Cos(a)*.16f,0,Mathf.Sin(a)*.16f));}
        }
        void UpdateFlight()
        {
            var k=Keyboard.current;
            if(!vrEnabled&&k!=null&&!UI.ModalOpen)
            {
                Flight.manualInput=new DVector3((k.sKey.isPressed?1:0)-(k.wKey.isPressed?1:0),(k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0),(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0));
                Flight.translationInput=new DVector3((k.lKey.isPressed?1:0)-(k.jKey.isPressed?1:0),(k.iKey.isPressed?1:0)-(k.kKey.isPressed?1:0),(k.oKey.isPressed?1:0)-(k.uKey.isPressed?1:0));
                if(k.shiftKey.isPressed) Flight.throttle=Math.Min(1,Flight.throttle+Time.unscaledDeltaTime*.5);
                if(k.ctrlKey.isPressed) Flight.throttle=Math.Max(0,Flight.throttle-Time.unscaledDeltaTime*.5);
                if(k.xKey.wasPressedThisFrame) Flight.throttle=0;
                if(k.spaceKey.wasPressedThisFrame) NextStage();if(k.pKey.wasPressedThisFrame) DeployParachute();
                if(k.mKey.wasPressedThisFrame) ToggleMap();if(k.fKey.wasPressedThisFrame) UI.ShowFlightToolbar();
                if(k.bKey.wasPressedThisFrame) Flight.brakesDeployed=!Flight.brakesDeployed;
                if(k.escapeKey.wasPressedThisFrame) UI.ShowFlightToolbar();
                if(k.backspaceKey.wasPressedThisFrame) Abort();
                if(k.tKey.wasPressedThisFrame) CycleTimeWarp();
            }
            if(UI.ModalOpen) {Sound.SetEngine(0);return;}
            int limit=Target!=null||Flight.Altitude<5000?5:Flight.Altitude<80000?10:100;
            if(TimeWarp>limit) TimeWarp=limit;
            var mouse=Mouse.current;
            if(!vrEnabled&&mouse!=null&&!EventSystem.current.IsPointerOverGameObject()) presentation.OrbitCamera(mouse.rightButton.isPressed?mouse.delta.ReadValue():Vector2.zero,mouse.scroll.ReadValue().y);
            flightAccumulator+=Mathf.Min(Time.unscaledDeltaTime,.1f)*TimeWarp;
            double step=.02;
            int iterations=0;
            while(flightAccumulator>=step&&iterations++<300)
            {
                UpdatePrograms(step);
                Flight.Step(step);
                foreach(var piece in debris) piece.Step(step);
                Target?.Step(step);
                if(OnStand) {Flight.position=new DVector3(0,OrbitalMechanics.Radius+2,0);Flight.velocity=OrbitalMechanics.AtmosphereVelocity(Flight.position);Flight.angularVelocity=new DVector3();Flight.orientation=DQuaternion.Identity;}
                if(Flight.lastThrust<1&&Flight.Powered) orbitPowerTime+=step;
                if(Flight.Orbit.periapsis>=120000&&Flight.Orbit.apoapsis<=250000) orbitReached=true;
                flightAccumulator-=(float)step;
            }
            presentation.Refresh(Flight,Target);Sound.SetEngine((float)(Flight.lastThrust/900000));
            presentation.RefreshDebris(Flight,debris);
            uiTimer+=Time.unscaledDeltaTime;
            if(uiTimer>.15f)
            {
                uiTimer=0;double separation=Target==null?0:(Flight.DockingPoint-Target.DockingPoint).Magnitude;
                double relative=Target==null?0:(Flight.DockingVelocity-Target.DockingVelocity).Magnitude;
                UI.UpdateFlight(Flight,separation,relative);
                if(Flight.Powered&&Flight.Has(PartKind.FlightRecorder)) samples.Add(new FlightSample(Flight));
            }
            if((Flight.crashed||Flight.landed)&&!resultShown)
            {
                if(Flight.crashed) {Sound.Play(SoundCue.Explosion);presentation.Burst(Flight.centerOfMass.Float);}
                resultShown=true;ExportTelemetry();
                UI.ShowResult(Flight.landed?"ПОСАДКА ВЫПОЛНЕНА":"ИСПЫТАНИЕ ЗАВЕРШЕНО",$"{Flight.lastEvent}\n\nВысота: {Flight.maxAltitude/1000:F2} км\nСкорость: {Flight.maxSpeed:F0} м/с\nТопливо: {Flight.totalFuelUsed:F0} кг\nМаксимальная перегрузка: {Flight.maxG:F1} g\n\nТелеметрия сохранена в папке данных игры.");
            }
        }
        public void Abort()
        {
            if(Flight==null||!Flight.Has(PartKind.EscapeTower)) {Toast("Система аварийного спасения не установлена");return;}
            if(Flight.Has(PartKind.Satellite)) ReleasePayload();
            var carrier=Flight.SeparateCapsule(true);if(carrier!=null) debris.Add(carrier);
            Ascent.active=false;recovery.active=true;Flight.throttle=0;Sound.Play(SoundCue.Abort);Toast("Аварийное спасение включено");
        }
        public void CycleTimeWarp()
        {
            if(Flight==null||OnStand) return;
            int limit=Target!=null||Flight.Altitude<5000?5:Flight.Altitude<80000?10:100;
            int[] speeds={1,2,5,10,50,100};int next=speeds[(Array.IndexOf(speeds,TimeWarp)+1)%speeds.Length];
            TimeWarp=next>limit?1:next;Toast("Скорость времени ×"+TimeWarp);
        }
        public void AdjustThrottle(int direction) {if(Flight!=null) Flight.throttle=direction==0?0:Math.Clamp(Flight.throttle+direction*.1,0,1);}
        bool HasTrialParts()
        {
            var kinds=Assembly.Blueprint.parts.Select(p=>Catalog.Find(p.partId).kind).ToArray();
            return new[]{PartKind.FirstStage,PartKind.BoosterEngine,PartKind.InstrumentBay,PartKind.Capsule,PartKind.Fairing}.All(kinds.Contains)&&kinds.Count(k=>k==PartKind.Fin)>=4;
        }
        void CheckMission()
        {
            var craft=Assembly.Blueprint;
            bool Has(PartKind kind)=>craft.parts.Any(p=>Catalog.Find(p.partId).kind==kind);
            bool pass=false;int id=CurrentMission.id;
            if(id==1) pass=Has(PartKind.FirstStage)&&Has(PartKind.BoosterEngine)&&Has(PartKind.Fairing)&&Assembly.detachCount>0&&Assembly.snapCount>=4;
            if(id==2) pass=Enumerable.Range(0,10).All(i=>Has((PartKind)i))&&craft.parts.Count(p=>Catalog.Find(p.partId).kind==PartKind.Fin)>=4;
            if(id==3&&craft.parts.Count>0) {var body=new FlightBody(craft,Catalog);pass=body.TWR>=1.3&&body.TWR<=2&&Math.Sqrt(body.centerOfMass.x*body.centerOfMass.x+body.centerOfMass.z*body.centerOfMass.z)<.045;}
            if(Flight!=null)
            {
                var f=Flight;
                if(id==4) pass=OnStand&&f.poweredTime>=10&&f.throttle<.01&&!f.crashed;
                if(id==5) pass=f.poweredTime>=10&&Has(PartKind.FuelLine)&&Has(PartKind.FuelPump)&&Has(PartKind.FuelValve)&&Has(PartKind.Pressurizer);
                if(id==6) pass=f.maxAltitude>=1000&&f.recordedTime>0;
                if(id==7) pass=f.Altitude>=5000&&f.Tilt<10&&f.stableTime>10;
                if(id==8) pass=f.maxAltitude>=3000&&!f.parts.Any(p=>p.destroyed);
                if(id==9) pass=f.stageEvent&&f.maxAltitude>=20000;
                if(id==10) pass=f.payloadReleased&&f.Altitude>=50000;
                if(id==11) pass=f.maxAltitude>=100000&&f.Telemetry;
                if(id==12) pass=orbitPowerTime>=180&&f.Powered;
                if(id==13) pass=f.Altitude>=80000&&f.Has(PartKind.Radiator)&&f.Has(PartKind.Insulation)&&f.Has(PartKind.Thermometer)&&!f.parts.Any(p=>p.destroyed);
                if(id==14) pass=f.aborted&&f.chuteDeployed;
                if(id==15) pass=f.landed&&f.Has(PartKind.Capsule)&&f.Has(PartKind.FlightRecorder);
                if(id==16) pass=orbitReached;
                if(id==17) pass=orbitReached&&Payload!=null&&Payload.telemetryTime>=180&&Payload.Has(PartKind.Satellite);
                if(id==18&&Target!=null) pass=(f.position-Target.position).Magnitude<50&&(f.velocity-Target.velocity).Magnitude<1;
                if(id==19) pass=f.docked;
                if(id==20) pass=orbitReached&&f.payloadReleased&&f.landed&&f.Has(PartKind.Capsule)&&f.Has(PartKind.FlightRecorder);
            }
            if(!pass) return;
            missionDone=true;
            if(!Progress.completed.Contains(id)) Progress.completed.Add(id);
            SaveStore.Save(Progress);Sound.Play(SoundCue.Success);
            UI.ShowResult("МИССИЯ ВЫПОЛНЕНА",$"{id:00} / {CurrentMission.title}\n\n{CurrentMission.goal}\n\nВремя: {timer:F1} с\nПрогресс сохранён.");
        }
        void ExportTelemetry()
        {
            if(samples.Count==0) return;
            string folder=System.IO.Path.Combine(Application.persistentDataPath,"Telemetry");System.IO.Directory.CreateDirectory(folder);
            string text="time_s,altitude_m,speed_ms,mass_kg,fuel_kg,thrust_N,temperature_K\n"+string.Join("\n",samples.Select(s=>s.Csv()));
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"flight-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"),text);
        }
        void OnApplicationQuit() {ExportTelemetry();if(Progress!=null) SaveStore.Save(Progress);}
    }
}
