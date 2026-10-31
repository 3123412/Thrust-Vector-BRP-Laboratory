using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BRP
{
    public sealed class CelestialMapPresentation : MonoBehaviour
    {
        readonly List<Transform> bodies=new List<Transform>();
        readonly List<TextMeshPro> labels=new List<TextMeshPro>();
        readonly List<LineRenderer> orbits=new List<LineRenderer>();
        readonly string[] names={"Солнце","Меркурий","Венера","Земля","Марс","Юпитер","Сатурн","Уран","Нептун","Луна"};
        readonly string[] resourceNames={"Sun","Mercury","Venus","Earth","Mars","Jupiter","Saturn","Uranus","Neptune","Moon"};
        readonly List<Material> ownedMaterials=new List<Material>();
        readonly float[] distances={0,42,68,96,127,185,238,288,338,0};
        readonly float[] sizes={13,2.1f,4.8f,5,2.7f,11,9.5f,6.5f,6.3f,1.36f};
        readonly Color[] colors={new Color(1,.7f,.16f),new Color(.5f,.46f,.4f),new Color(.8f,.66f,.4f),Color.white,new Color(.65f,.27f,.12f),new Color(.69f,.52f,.37f),new Color(.75f,.64f,.4f),new Color(.3f,.75f,.8f),new Color(.15f,.36f,.85f),new Color(.6f,.6f,.58f)};
        LabSceneAssets assets;
        Transform rings;
        int focus=-1,previousView=-1;
        Vector3 cameraVelocity;
        float yaw,pitch,zoomScale=1;
        TextMeshPro legend;
        public void Orbit(Vector2 delta,float zoom)
        {
            yaw+=delta.x*.2f;pitch=Mathf.Clamp(pitch-delta.y*.15f,-70,70);
            zoomScale=Mathf.Clamp(zoomScale-zoom*.001f,.35f,2.2f);
        }
        public bool Select(Ray ray)
        {
            if(!Physics.Raycast(ray,out var hit,5000,1<<29)) return false;
            for(int i=0;i<bodies.Count;i++) if(hit.transform==bodies[i]) {focus=i;zoomScale=1;return true;}
            return false;
        }
        public void Initialize(LabSceneAssets source)
        {
            assets=source;
            for(int i=0;i<names.Length;i++)
            {
                var root=new GameObject(names[i]).transform;root.SetParent(transform,false);bodies.Add(root);
                root.gameObject.AddComponent<SphereCollider>().radius=1;
                var authored=Resources.Load<GameObject>("BRP/World/"+resourceNames[i]);
                var model=Instantiate(authored!=null?authored:i==9&&source.moon!=null?source.moon:source.planetSphere!=null?source.planetSphere:source.planet,root);
                Material material;
                if(i==3) material=source.planetMaterial;
                else if(i==9&&source.moonMaterial!=null) material=source.moonMaterial;
                else if(authored!=null) material=authored.GetComponentInChildren<Renderer>().sharedMaterial;
                else
                {
                    material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",colors[i]);material.SetFloat("_Smoothness",.15f);
                    ownedMaterials.Add(material);
                    if(i==0) {material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",colors[0]*1.5f);}
                }
                if(authored==null||i==3||i==9) foreach(var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=material;
                var label=new GameObject(names[i]+" label").AddComponent<TextMeshPro>();label.transform.SetParent(transform,false);
                label.font=source.font;label.text=names[i];label.fontSize=70;label.alignment=TextAlignmentOptions.Center;label.color=i==3?UiTheme.Cyan:i==9?UiTheme.Orange:UiTheme.TextColor;label.rectTransform.sizeDelta=new Vector2(140,28);labels.Add(label);
                var line=new GameObject(names[i]+" orbit").AddComponent<LineRenderer>();line.transform.SetParent(transform,false);
                line.sharedMaterial=source.orbitMaterial;line.useWorldSpace=false;line.startWidth=line.endWidth=.17f;line.startColor=line.endColor=new Color(.3f,.45f,.5f,.4f);orbits.Add(line);
            }
            if(source.saturnRings!=null&&Resources.Load<GameObject>("BRP/World/Saturn")==null) {rings=Instantiate(source.saturnRings,bodies[6]).transform;}
            legend=new GameObject("Map scale legend").AddComponent<TextMeshPro>();legend.transform.SetParent(transform,false);
            legend.font=source.font;legend.fontSize=70;legend.alignment=TextAlignmentOptions.Center;legend.color=UiTheme.Muted;legend.rectTransform.sizeDelta=new Vector2(600,40);
            foreach(var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer=29;
        }
        void OnDestroy() {foreach(var material in ownedMaterials) if(material!=null) Destroy(material);}
        public void Focus(int index) {focus=index==0?3:index==1?9:-1;previousView=-1;}
        public void NextBody() {focus=(focus+1)%bodies.Count;zoomScale=1;}
        public void Refresh(Camera camera,FlightBody flight,int view)
        {
            bool earthMoon=view==1;
            if(previousView!=view) {previousView=view;cameraVelocity=Vector3.zero;}
            for(int i=0;i<bodies.Count;i++)
            {
                bool visible=!earthMoon||i==3||i==9;
                bodies[i].gameObject.SetActive(visible);labels[i].gameObject.SetActive(visible);orbits[i].gameObject.SetActive(visible);
                if(!visible) continue;
                float radius=earthMoon?i==3?12:3.27f:sizes[i]*(focus<0?2.4f:1);bodies[i].localScale=Vector3.one*radius;
                float orbit=distances[i];
                double angle=.6+i*.82+flight.elapsed*.00001/Math.Max(1,i);
                Vector3 position=new Vector3(Mathf.Cos((float)angle)*orbit,Mathf.Sin((float)angle)*orbit,Mathf.Sin((float)angle)*orbit*.025f);
                if(i==0||earthMoon&&i==3) position=Vector3.zero;
                if(i==9) position=earthMoon?new Vector3(115,12,0):bodies[3].localPosition+new Vector3(13,6,0);
                bodies[i].localPosition=position;labels[i].transform.localPosition=position+Vector3.up*(radius+(earthMoon?12:focus<0?28:9));
                if(!earthMoon&&focus<0)
                {
                    Vector2 offset=i switch {0=>new Vector2(65,-70),1=>new Vector2(110,75),2=>new Vector2(-95,90),3=>new Vector2(-100,25),9=>new Vector2(-100,-45),_=>Vector2.zero};
                    if(offset!=Vector2.zero) labels[i].transform.localPosition=position+new Vector3(offset.x,offset.y,0);
                }
                labels[i].fontSize=earthMoon?i==9?35:50:focus>=0?40:220;
                labels[i].transform.rotation=camera.transform.rotation;
                if(i==9&&earthMoon)
                {
                    labels[i].text="ЛУНА\n384 400 км · 1,62 м/с²";
                    orbits[i].positionCount=2;orbits[i].SetPosition(0,Vector3.zero);orbits[i].SetPosition(1,position);
                }
                else
                {
                    labels[i].text=names[i];
                    if(i==0||i==9||earthMoon) {orbits[i].positionCount=0;continue;}
                    if(orbits[i].positionCount==129) continue;
                    orbits[i].positionCount=129;
                    for(int point=0;point<129;point++)
                    {
                        float a=point*Mathf.PI*2/128;
                        orbits[i].SetPosition(point,new Vector3(Mathf.Cos(a)*orbit,Mathf.Sin(a)*orbit,Mathf.Sin(a)*orbit*.025f));
                    }
                }
            }
            Vector3 center=earthMoon?new Vector3(55,4,0):focus>=0?bodies[focus].localPosition:new Vector3(0,-110,0);
            float distance=earthMoon?235:focus>=0?140:1550;
            camera.farClipPlane=5000;
            Vector3 desired=center+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,earthMoon?15:120,-distance*zoomScale);
            camera.transform.position=Vector3.SmoothDamp(camera.transform.position,desired,ref cameraVelocity,.35f);
            camera.transform.LookAt(center);
            legend.text=earthMoon?"ЗЕМЛЯ — ЛУНА\n384 400 км · схема расстояний":"СОЛНЕЧНАЯ СИСТЕМА\nРасстояния и размеры сокращены для обзора";
            legend.fontSize=earthMoon?30:focus>=0?35:190;
            legend.transform.position=center+camera.transform.up*(earthMoon?60:focus>=0?50:540);legend.transform.rotation=camera.transform.rotation;
        }
    }
}
