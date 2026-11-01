using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BRP
{
    public sealed class VRAssemblyGrid : MonoBehaviour
    {
        const float Radius=AssemblyController.VRGridRadius,Step=AssemblyController.VRGridStep,Width=.003f;
        static readonly int[] OutlineOrder={0,1,3,2,0,4,5,1,5,7,3,7,6,2,6,4};
        static readonly Color GridColor=new Color(.25f,.7f,.72f,.75f);
        static readonly Color NodeColor=new Color(1f,.48f,.14f,1f);
        LaboratoryGame game;
        GameObject grid;
        Mesh mesh;
        LineRenderer marker,outline;
        bool hintShown;

        public void Initialize(LaboratoryGame owner)
        {
            game=owner;
            grid=new GameObject("VR assembly grid");grid.transform.SetParent(game.workshop,false);
            grid.layer=game.workshop.gameObject.layer;
            var filter=grid.AddComponent<MeshFilter>();var renderer=grid.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=game.Assets.orbitMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            mesh=CreateGrid();filter.sharedMesh=mesh;
            marker=CreateLine("VR placement marker",33,true,.006f);
            for(int i=0;i<33;i++)
            {
                float angle=i*Mathf.PI*2/32;
                marker.SetPosition(i,new Vector3(Mathf.Cos(angle)*.055f,.005f,Mathf.Sin(angle)*.055f));
            }
            outline=CreateLine("VR placement preview",OutlineOrder.Length,false,.004f);outline.useWorldSpace=true;
            grid.SetActive(false);marker.gameObject.SetActive(false);outline.gameObject.SetActive(false);
        }

        LineRenderer CreateLine(string name,int count,bool loop,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(game.workshop,false);
            line.gameObject.layer=game.workshop.gameObject.layer;line.sharedMaterial=game.Assets.orbitMaterial;
            line.positionCount=count;line.loop=loop;line.useWorldSpace=false;line.startWidth=line.endWidth=width;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }

        static Mesh CreateGrid()
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            var color=new Color(.18f,.48f,.5f,.65f);var axis=new Color(.28f,.72f,.72f,.8f);
            for(int i=-6;i<=6;i++)
            {
                float p=i*Step;float extent=Mathf.Sqrt(Radius*Radius-p*p);
                AddStrip(vertices,colors,triangles,new Vector3(-extent,0,p),new Vector3(extent,0,p),i==0?axis:color);
                AddStrip(vertices,colors,triangles,new Vector3(p,0,-extent),new Vector3(p,0,extent),i==0?axis:color);
            }
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
                AddStrip(vertices,colors,triangles,new Vector3(Mathf.Cos(a)*Radius,0,Mathf.Sin(a)*Radius),new Vector3(Mathf.Cos(b)*Radius,0,Mathf.Sin(b)*Radius),axis);
            }
            var result=new Mesh {name="VR assembly grid"};result.SetVertices(vertices);result.SetColors(colors);result.SetTriangles(triangles,0);result.RecalculateBounds();return result;
        }

        static void AddStrip(List<Vector3> vertices,List<Color> colors,List<int> triangles,Vector3 a,Vector3 b,Color color)
        {
            var side=Vector3.Cross(Vector3.up,(b-a).normalized)*Width*.5f;int first=vertices.Count;
            vertices.Add(a-side);vertices.Add(a+side);vertices.Add(b+side);vertices.Add(b-side);
            for(int i=0;i<4;i++) colors.Add(color);
            triangles.Add(first);triangles.Add(first+2);triangles.Add(first+1);triangles.Add(first);triangles.Add(first+3);triangles.Add(first+2);
        }

        void LateUpdate()
        {
            PartView active=null;
            if(game!=null&&game.VREnabled&&!game.IsFlying)
                foreach(var view in game.Assembly.views)
                    if(view!=null&&view.Grab!=null&&view.Grab.isSelected) {active=view;break;}
            bool visible=active!=null;
            bool preview=visible&&active.VRPreviewVisible;
            if(grid.activeSelf!=visible) grid.SetActive(visible);
            if(marker.gameObject.activeSelf!=preview) marker.gameObject.SetActive(preview);
            if(outline.gameObject.activeSelf!=preview) outline.gameObject.SetActive(preview);
            if(!visible) return;
            if(!hintShown) {game.Toast("Бирюзовый — сетка, оранжевый — узел ракеты.");hintShown=true;}
            var position=preview?active.VRPreviewPosition:active.transform.position;var origin=game.Assembly.assemblyRoot.position;
            grid.transform.position=new Vector3(origin.x,Mathf.Clamp(position.y,AssemblyController.VRGridFloor,AssemblyController.VRGridCeiling)+.002f,origin.z);
            if(!preview) return;
            marker.transform.position=active.VRPreviewMarkerPosition;var color=active.VRPreviewIsNode?NodeColor:GridColor;
            marker.startColor=marker.endColor=color;outline.startColor=outline.endColor=color;
            var box=active.GetComponent<BoxCollider>();
            if(box==null) {outline.gameObject.SetActive(false);return;}
            if(!outline.gameObject.activeSelf) outline.gameObject.SetActive(true);
            for(int i=0;i<OutlineOrder.Length;i++)
            {
                int corner=OutlineOrder[i];
                var offset=box.center+Vector3.Scale(box.size*.5f,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                outline.SetPosition(i,position+active.VRPreviewRotation*Vector3.Scale(offset,active.transform.lossyScale));
            }
        }

        void OnDestroy()
        {
            if(mesh!=null) Destroy(mesh);
            if(grid!=null) Destroy(grid);
            if(marker!=null) Destroy(marker.gameObject);
            if(outline!=null) Destroy(outline.gameObject);
        }
    }
}
