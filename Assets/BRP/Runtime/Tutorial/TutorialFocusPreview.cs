using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BRP
{
    public struct TutorialFocusContext
    {
        public PartDefinition definition,parentDefinition;
        public PlacedPart placement,parentPlacement;
        public SnapTarget target;
        public Vector3 connector,approach;
        public bool hasMount,removal;
        public string location;
    }

    public sealed class TutorialFocusPreview : MonoBehaviour
    {
        LaboratoryGame game;
        RectTransform panel;
        RawImage viewport;
        TextMeshProUGUI partTitle,purpose,action,diagnosis,subtitle,assemblyLabel,viewLabel;
        UnityEngine.UI.Button connectionButton,catalogButton,animationButton;
        UnityEngine.UI.Slider assemblySlider;
        GameObject studio,geometry,moving,parentModel;
        Camera previewCamera;
        RenderTexture texture;
        LineRenderer mountRing,approachArrow;
        TutorialFocusContext context;
        Action revealCatalog;
        Vector3 pivot,mountedPosition,explodedPosition;
        float yaw=-28,pitch=16,zoom=1,fitDistance=4,animationClock;
        bool connectionMode=true,animate=true,positionedVR,applyingSlider;
        string diagnosticCopy,purposeCopy,actionCopy,subtitleCopy;
        public bool IsOpen=>panel!=null&&panel.gameObject.activeSelf;
        public PartDefinition Definition=>context.definition;
        public float Zoom=>zoom;
        public float AssemblyFraction=>assemblySlider==null?0:assemblySlider.value;
        public bool HasConnection=>context.hasMount;

        public void Initialize(LaboratoryGame owner,Action showCatalog)
        {
            game=owner;revealCatalog=showCatalog;
            panel=UiTheme.Panel("Instructor inspection",game.UI.root,380,184,1160,724,new Color(.027f,.037f,.043f,1));
            UiTheme.Panel("Inspection accent",panel,0,0,5,724,UiTheme.Cyan);
            UiTheme.Text(panel,"УЧЕБНЫЙ СТЕНД / МОДЕЛЬ МОЖНО ВРАЩАТЬ",24,14,1060,27,18,UiTheme.Cyan);
            partTitle=UiTheme.Text(panel,"",24,53,1060,44,30);
            UiTheme.Button(panel,"×",1094,12,46,46,Close);
            var frame=UiTheme.Panel("Inspection viewport",panel,24,114,664,442,new Color(.025f,.04f,.055f));
            viewport=UiTheme.Rect("Interactive model",frame,1,1,662,440).gameObject.AddComponent<RawImage>();viewport.color=Color.white;
            viewport.gameObject.AddComponent<TutorialPreviewInput>().preview=this;
            viewLabel=UiTheme.Text(frame,"",16,12,628,26,17,UiTheme.Cyan);
            UiTheme.Text(frame,"Осмотр не меняет настоящую сборку",16,402,628,25,17,UiTheme.Muted);
            UiTheme.Text(panel,"ЗАЧЕМ ЭТОТ МОДУЛЬ",714,115,416,27,18,UiTheme.Cyan);
            purpose=UiTheme.Text(panel,"",714,153,416,106,23);
            UiTheme.Text(panel,"ЧТО СДЕЛАТЬ",714,275,416,27,18,UiTheme.Cyan);
            action=UiTheme.Text(panel,"",714,313,416,100,23);
            UiTheme.Text(panel,"ЕСЛИ НЕ ПОЛУЧАЕТСЯ",714,430,416,27,18,UiTheme.Orange);
            diagnosis=UiTheme.Text(panel,"",714,468,416,91,22,UiTheme.Muted);
            UiTheme.Button(panel,"←",24,573,57,44,()=>Rotate(new Vector2(-45,0)));
            UiTheme.Button(panel,"→",89,573,57,44,()=>Rotate(new Vector2(45,0)));
            UiTheme.Button(panel,"−",160,573,54,44,()=>ChangeZoom(-.2f));
            UiTheme.Button(panel,"+",222,573,54,44,()=>ChangeZoom(.2f));
            UiTheme.Button(panel,"Сброс",290,573,112,44,ResetView);
            UiTheme.Button(panel,"Деталь",414,573,123,44,()=>SetConnectionMode(false));
            connectionButton=UiTheme.Button(panel,"Соединение",549,573,139,44,()=>SetConnectionMode(true));
            assemblyLabel=UiTheme.Text(panel,"ДЕМОНСТРАЦИЯ МОНТАЖА",24,634,477,25,18,UiTheme.Cyan);
            animationButton=UiTheme.Button(panel,"Пауза",571,629,117,37,ToggleAnimation);
            assemblySlider=UiTheme.Slider(panel,24,671,664,25,1);
            assemblySlider.onValueChanged.AddListener(SetAssemblyFraction);
            subtitle=UiTheme.Text(panel,"",714,573,416,57,20,UiTheme.Muted);
            catalogButton=UiTheme.Button(panel,"В каталоге",714,651,191,48,()=>revealCatalog?.Invoke());
            UiTheme.Button(panel,"К сборке →",919,651,211,48,Close,true);
            panel.gameObject.SetActive(false);
        }

        public void SetContext(TutorialFocusContext value)
        {
            bool changed=context.definition!=value.definition||context.parentPlacement?.instanceId!=value.parentPlacement?.instanceId||context.target.position!=value.target.position||context.target.rotation!=value.target.rotation||context.hasMount!=value.hasMount||context.removal!=value.removal;
            context=value;
            if(context.definition==null) {Close();return;}
            if(IsOpen&&changed) BuildModels();
        }

        public void SetCopy(string why,string what,string problem,string spoken)
        {
            purposeCopy=why;actionCopy=what;diagnosticCopy=problem;subtitleCopy=spoken;
            if(!IsOpen) return;
            SetText(purpose,purposeCopy);SetText(action,actionCopy);SetText(diagnosis,diagnosticCopy);SetText(subtitle,subtitleCopy??"");
        }

        public void Open()
        {
            if(context.definition==null||game.IsFlying) return;
            panel.gameObject.SetActive(true);panel.SetAsLastSibling();positionedVR=false;
            BuildModels();SetCopy(purposeCopy,actionCopy,diagnosticCopy,subtitleCopy);
            if(game.VREnabled) PositionVR();
        }

        public void Close()
        {
            if(panel!=null) panel.gameObject.SetActive(false);
            if(studio!=null) studio.SetActive(false);
        }

        public void Rotate(Vector2 delta)
        {
            yaw+=delta.x*.65f;pitch=Mathf.Clamp(pitch-delta.y*.45f,-70,75);UpdateCamera();
        }
        public void ChangeZoom(float amount) {zoom=Mathf.Clamp(zoom+amount,.65f,2.6f);UpdateCamera();}
        public void ResetView() {yaw=-28;pitch=16;zoom=1;UpdateCamera();}

        public void SetConnectionMode(bool value)
        {
            bool next=value&&context.hasMount;
            if(connectionMode==next) return;
            connectionMode=next;BuildModels();
        }

        public void SetAssemblyFraction(float value)
        {
            if(!applyingSlider) {animate=false;SetText(animationButton.GetComponentInChildren<TextMeshProUGUI>(),"Показать");}
            if(moving!=null) moving.transform.localPosition=Vector3.Lerp(explodedPosition,mountedPosition,Mathf.SmoothStep(0,1,value));
            if(assemblyLabel!=null) assemblyLabel.text=(context.removal?"РАЗБОРКА":"МОНТАЖ")+$" / {(value>=.98f?"СОЕДИНЕНО":value<=.02f?"СНЯТО":$"{value*100:F0}%")}";
        }

        void ToggleAnimation()
        {
            animate=!animate;animationClock=0;
            SetText(animationButton.GetComponentInChildren<TextMeshProUGUI>(),animate?"Пауза":"Показать");
        }

        void CreateStudio()
        {
            if(studio!=null) {studio.SetActive(true);return;}
            studio=new GameObject("Instructor isolated preview studio");studio.transform.SetParent(transform,false);studio.transform.position=new Vector3(16000,16000,-16000);
            var cameraObject=new GameObject("Instructor model camera");cameraObject.transform.SetParent(studio.transform,false);
            previewCamera=cameraObject.AddComponent<Camera>();previewCamera.cullingMask=1<<30;
            previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.025f,.04f,.055f);
            previewCamera.fieldOfView=34;previewCamera.nearClipPlane=.025f;previewCamera.farClipPlane=150;previewCamera.allowHDR=false;
            var data=cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.allowXRRendering=false;data.renderPostProcessing=false;
            texture=new RenderTexture(800,532,24,RenderTextureFormat.ARGB32) {antiAliasing=4,name="BRP instructor inspection"};texture.Create();
            previewCamera.targetTexture=texture;viewport.texture=texture;
            PreviewLight("Preview key",new Vector3(-3,4,-4),new Color(.8f,.9f,1),8);
            PreviewLight("Preview fill",new Vector3(3,1,3),new Color(1,.75f,.5f),5);
        }

        void PreviewLight(string label,Vector3 position,Color color,float intensity)
        {
            var lightObject=new GameObject(label);lightObject.transform.SetParent(studio.transform,false);lightObject.transform.localPosition=position;
            var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=14;light.cullingMask=1<<30;light.shadows=LightShadows.None;
        }

        GameObject Model(PartDefinition definition,PlacedPart finish,string label)
        {
            var actor=new GameObject(label);actor.transform.SetParent(geometry.transform,false);
            var model=Instantiate(definition.model,actor.transform);
            if(definition.surface) model.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var child in actor.GetComponentsInChildren<Transform>()) child.gameObject.layer=30;
            foreach(var collider in actor.GetComponentsInChildren<Collider>()) collider.enabled=false;
            foreach(var body in actor.GetComponentsInChildren<Rigidbody>()) {body.isKinematic=true;body.useGravity=false;}
            RocketFinish.Apply(actor,finish??new PlacedPart {partId=definition.id},definition,game.Assets.font);
            return actor;
        }

        void BuildModels()
        {
            if(!IsOpen||context.definition==null) return;
            CreateStudio();
            if(geometry!=null) {geometry.SetActive(false);Destroy(geometry);}
            geometry=new GameObject("Instruction geometry");geometry.transform.SetParent(studio.transform,false);geometry.layer=30;
            bool mountedView=connectionMode&&context.hasMount;
            moving=Model(context.definition,context.placement,"Required module");
            moving.transform.localRotation=mountedView?context.target.rotation:Quaternion.identity;
            mountedPosition=mountedView?context.target.position-context.connector:Vector3.zero;
            float separation=Mathf.Max(.55f,context.definition.height*.55f);
            explodedPosition=mountedView?mountedPosition+context.approach.normalized*separation:mountedPosition;
            moving.transform.localPosition=explodedPosition;
            parentModel=null;
            if(mountedView&&context.parentDefinition!=null&&context.parentPlacement!=null)
            {
                parentModel=Model(context.parentDefinition,context.parentPlacement,"Mating module");
                parentModel.transform.localPosition=context.parentPlacement.position-context.connector;parentModel.transform.localRotation=context.parentPlacement.rotation;
            }
            var renderers=geometry.GetComponentsInChildren<MeshRenderer>();
            var bounds=new Bounds(moving.transform.position,Vector3.one*.1f);
            if(renderers.Length>0) {bounds=renderers[0].bounds;foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);}
            pivot=studio.transform.InverseTransformPoint(bounds.center);
            fitDistance=Mathf.Max(1,bounds.extents.magnitude/Mathf.Tan(previewCamera.fieldOfView*.5f*Mathf.Deg2Rad)*1.15f);
            if(mountedView) BuildMountMarks();
            connectionButton.interactable=context.hasMount;catalogButton.interactable=!game.IsFlying;
            assemblySlider.gameObject.SetActive(mountedView);animationButton.gameObject.SetActive(mountedView);assemblyLabel.gameObject.SetActive(mountedView);
            partTitle.text=context.definition.title;
            viewLabel.text=mountedView?"СОЕДИНЕНИЕ / "+context.location.ToUpperInvariant():"ОТДЕЛЬНЫЙ МОДУЛЬ / ВРАЩАЙ И ПРИБЛИЖАЙ";
            animate=mountedView;animationClock=0;SetText(animationButton.GetComponentInChildren<TextMeshProUGUI>(),"Пауза");
            applyingSlider=true;assemblySlider.SetValueWithoutNotify(context.removal?1:0);SetAssemblyFraction(assemblySlider.value);applyingSlider=false;animate=mountedView;
            ResetView();
        }

        LineRenderer Line(string label,Color color,float width)
        {
            var lineObject=new GameObject(label);lineObject.transform.SetParent(geometry.transform,false);lineObject.layer=30;
            var line=lineObject.AddComponent<LineRenderer>();line.sharedMaterial=game.Assets.orbitMaterial;line.useWorldSpace=false;
            line.startColor=line.endColor=color;line.startWidth=line.endWidth=width;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            return line;
        }

        void BuildMountMarks()
        {
            var normal=context.approach.sqrMagnitude>.1f?context.approach.normalized:Vector3.up;
            var tangent=Vector3.Cross(normal,Mathf.Abs(Vector3.Dot(normal,Vector3.up))>.9f?Vector3.right:Vector3.up).normalized;
            var bitangent=Vector3.Cross(normal,tangent);
            float radius=context.definition.surface?.17f:Mathf.Max(.16f,context.definition.radius*1.12f);
            var points=new Vector3[49];for(int i=0;i<points.Length;i++) {float angle=i*Mathf.PI*2/(points.Length-1);points[i]=(tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle))*radius;}
            mountRing=Line("Mating ring",UiTheme.Cyan,.014f);mountRing.positionCount=points.Length;mountRing.SetPositions(points);
            approachArrow=Line("Insertion direction",UiTheme.Orange,.018f);
            var tip=normal*.07f;var tail=normal*Mathf.Max(.65f,context.definition.height*.5f);
            approachArrow.positionCount=5;approachArrow.SetPositions(new[]{tail,tip,tip+normal*.16f+tangent*.09f,tip,tip+normal*.16f-tangent*.09f});
        }

        void UpdateCamera()
        {
            if(previewCamera==null||studio==null) return;
            var center=studio.transform.TransformPoint(pivot);
            previewCamera.transform.position=center+Quaternion.Euler(-pitch,yaw,0)*Vector3.back*(fitDistance/zoom);
            previewCamera.transform.LookAt(center);
        }

        void PositionVR()
        {
            var view=game.mainCamera.transform;
            var forward=Vector3.ProjectOnPlane(view.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.1f) forward=Vector3.forward;
            var rotation=Quaternion.LookRotation(forward,Vector3.up);
            const float scale=.00155f;
            panel.rotation=rotation;panel.localScale=Vector3.one*(scale/game.UI.root.lossyScale.x);
            Vector3 center=view.position+forward*1.7f+Vector3.up*.05f;
            panel.position=center+rotation*new Vector3(-panel.rect.width*scale*.5f,panel.rect.height*scale*.5f,0);
            positionedVR=true;
        }

        void Update()
        {
            if(!IsOpen) return;
            if(game.IsFlying||game.UI.ModalOpen||game.UI.MapView) {Close();return;}
            if(game.VREnabled&&!positionedVR) PositionVR();
            if(animate&&connectionMode&&context.hasMount)
            {
                animationClock+=Time.unscaledDeltaTime;
                float phase=Mathf.PingPong(animationClock/3.2f,1);
                float fraction=context.removal?1-phase:phase;
                applyingSlider=true;assemblySlider.SetValueWithoutNotify(fraction);SetAssemblyFraction(fraction);applyingSlider=false;
            }
        }

        static void SetText(TextMeshProUGUI text,string value) {value=value??"";if(text.text!=value) text.text=value;}

        void OnDestroy()
        {
            if(previewCamera!=null) previewCamera.targetTexture=null;
            if(studio!=null) Destroy(studio);
            if(texture!=null) {texture.Release();Destroy(texture);}
            if(panel!=null) Destroy(panel.gameObject);
        }
    }
}
