using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BRP
{
    public readonly struct SnapTarget
    {
        public readonly string parentId;
        public readonly int port;
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public SnapTarget(string parent,int port,Vector3 position,Quaternion rotation)
        { parentId=parent;this.port=port;this.position=position;this.rotation=rotation; }
    }

    public sealed class AssemblyController : MonoBehaviour
    {
        public const float Scale = .34f;
        public const float VRGridStep = .1f;
        public const float VRGridRadius = .7f;
        public const float VRGridFloor = 1.06f;
        public const float VRGridCeiling = 3.46f;
        const float VRNodeDistance = .18f;
        const float VRGridReach = .15f;
        const float SnapTolerance = .56f;
        [field: SerializeField] public CraftBlueprint Blueprint { get; private set; } = new CraftBlueprint();
        [field: SerializeField] public PartCatalog Catalog { get; private set; }
        public List<PartView> views = new List<PartView>();
        public Transform assemblyRoot;
        public event Action Changed;
        public int snapCount, detachCount, paintCount;
        readonly List<CraftBlueprint> undo = new List<CraftBlueprint>();
        readonly List<CraftBlueprint> redo = new List<CraftBlueprint>();
        readonly HashSet<string> detachedIds = new HashSet<string>();
        readonly List<PartView> gridGroup = new List<PartView>();
        readonly HashSet<(string parent,int port)> occupiedPorts = new HashSet<(string,int)>();
        public void Initialize(PartCatalog catalog,Transform root)
        {
            Catalog=catalog;assemblyRoot=root;
            foreach(var view in views)
            {
                if(view==null) continue;
                var placement=Blueprint.parts.Find(p=>p.instanceId==view.Placement.instanceId);
                if(placement!=null) view.Initialize(this,catalog.Find(placement.partId),placement);
            }
        }

        public void Remember()
        {
            undo.Add(Blueprint.Clone());
            if(undo.Count>50) undo.RemoveAt(0);
            redo.Clear();
        }

        public void Load(CraftBlueprint craft,bool remember=true)
        {
            if(remember) Remember();
            foreach(var view in views.ToArray()) if(view!=null)
            {
                if(Application.isPlaying) Destroy(view.gameObject);else DestroyImmediate(view.gameObject);
            }
            views.Clear();detachedIds.Clear();Blueprint=craft.Clone();
            foreach(var placement in Blueprint.parts)
            {
                var definition=Catalog.Find(placement.partId);
                if(definition==null) continue;
                var view=CreateView(definition,placement);
                view.Attached=true;view.SetPlacement();
            }
            Changed?.Invoke();
        }

        PartView CreateView(PartDefinition definition,PlacedPart placement)
        {
            var go=new GameObject(definition.title);
            go.transform.SetParent(assemblyRoot,false);
            var model=Instantiate(definition.model,go.transform);
            model.name="Model";
            if(definition.surface) model.transform.localRotation=Quaternion.Euler(0,180,0);
            var view=go.AddComponent<PartView>();
            view.Initialize(this,definition,placement);
            views.Add(view);
            return view;
        }

        public PartView Spawn(PartDefinition definition)
        {
            if(views.Count>=128) return null;
            var placement=new PlacedPart {partId=definition.id};
            var view=CreateView(definition,placement);
            view.transform.position=assemblyRoot.position+new Vector3(-.85f,.2f,-.12f);
            return view;
        }

        public List<PartView> Detach(PartView view)
        {
            if(!view.Attached) return new List<PartView>{view};
            Remember();detachCount++;
            var group=Blueprint.Subtree(view.Placement.instanceId);
            foreach(var part in group) detachedIds.Add(part.instanceId);
            foreach(var p in group) Blueprint.parts.Remove(p);
            var result=views.Where(v=>group.Any(p=>p.instanceId==v.Placement.instanceId)).ToList();
            foreach(var v in result) v.Attached=false;
            view.Placement.parentId=null;
            Changed?.Invoke();
            return result;
        }
        public List<PartView> DetachedGroup(PartView root)
        {
            var ids=new HashSet<string>{root.Placement.instanceId};bool changed;
            do {changed=false;foreach(var view in views) if(!view.Attached&&ids.Contains(view.Placement.parentId??"")) changed|=ids.Add(view.Placement.instanceId);} while(changed);
            return views.Where(view=>!view.Attached&&ids.Contains(view.Placement.instanceId)).ToList();
        }

        public bool TryTarget(PartView moving,out SnapTarget target)
            =>FindTarget(moving,new Pose(moving.transform.position,moving.transform.rotation),out target,SnapTolerance,35);

        public bool TryVRTarget(PartView moving,out SnapTarget target)
            =>TryVRTarget(moving,new Pose(moving.transform.position,moving.transform.rotation),out target);

        public bool TryVRTarget(PartView moving,Pose pose,out SnapTarget target)
            =>FindTarget(moving,pose,out target,VRNodeDistance/Mathf.Max(.001f,assemblyRoot.lossyScale.x),180);

        bool FindTarget(PartView moving,Pose pose,out SnapTarget target,float tolerance,float maxAngle)
        {
            float best=tolerance;
            target=default;
            bool found=false;
            var d=moving.Definition;
            occupiedPorts.Clear();
            foreach(var part in Blueprint.parts) if(part.parentId!=null) occupiedPorts.Add((part.parentId,part.port));
            void Consider(SnapTarget next)
            {
                Vector3 movingConnector=pose.position;
                Vector3 targetConnector=next.position;
                if(next.port==1)
                {
                    movingConnector=pose.position+pose.rotation*Vector3.Scale(Vector3.up*d.height,moving.transform.lossyScale);
                    targetConnector+=next.rotation*Vector3.up*d.height;
                }
                float distance=Vector3.Distance(assemblyRoot.InverseTransformPoint(movingConnector),targetConnector);
                if(distance>=best) return;
                if(Quaternion.Angle(Quaternion.Inverse(assemblyRoot.rotation)*pose.rotation,next.rotation)>maxAngle) return;
                best=distance;targetLocal=next;found=true;
            }
            targetLocal=default;
            if(Blueprint.parts.Count==0 && d.IsStructural)
                Consider(new SnapTarget(null,0,Vector3.zero,Quaternion.identity));
            foreach(var existing in Blueprint.parts)
            {
                var parent=Catalog.Find(existing.partId);
                if(parent==null||parent.surface) continue;
                if(d.surface)
                {
                    for(int i=0;i<8;i++)
                    {
                        int port=2+i;
                        if(occupiedPorts.Contains((existing.instanceId,port))) continue;
                        float angle=i*45;
                        var q=Quaternion.Euler(0,angle,0);
                        var position=existing.position+q*new Vector3(parent.radius,.15f+parent.height*.25f,0);
                        Consider(new SnapTarget(existing.instanceId,port,position,q));
                    }
                }
                else
                {
                    int port=d.mount==MountType.Engine&&parent.kind!=PartKind.StackDecoupler&&parent.kind!=PartKind.Adapter?1:0;
                    if(occupiedPorts.Contains((existing.instanceId,port))) continue;
                    var position=existing.position+Vector3.up*(port==0?parent.height:-d.height);
                    Consider(new SnapTarget(existing.instanceId,port,position,Quaternion.identity));
                }
            }
            target=targetLocal;
            return found;
        }
        SnapTarget targetLocal;

        public bool TryVRGridPosition(PartView moving,Pose pose,out Vector3 position)
        {
            position=pose.position;
            moving.GetComponentsInChildren(false,gridGroup);
            if(gridGroup.Count==0) return false;
            var pivot=moving.transform.position;
            var delta=pose.rotation*Quaternion.Inverse(moving.transform.rotation);
            float horizontalExtent=0,bottomExtent=0,topExtent=0;
            foreach(var item in gridGroup)
            {
                var box=item.GetComponent<BoxCollider>();
                if(box==null||!box.enabled) continue;
                for(int i=0;i<8;i++)
                {
                    var corner=box.center+Vector3.Scale(box.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var cornerOffset=delta*(box.transform.TransformPoint(corner)-pivot);
                    horizontalExtent=Mathf.Max(horizontalExtent,new Vector2(cornerOffset.x,cornerOffset.z).magnitude);
                    bottomExtent=Mathf.Max(bottomExtent,-cornerOffset.y);topExtent=Mathf.Max(topExtent,cornerOffset.y);
                }
            }
            float radius=VRGridRadius-horizontalExtent;
            float minY=VRGridFloor+bottomExtent;
            float maxY=VRGridCeiling-topExtent;
            if(radius<0||maxY<minY) return false;
            var origin=assemblyRoot.position;
            var desired=pose.position;
            var offset=new Vector2(desired.x-origin.x,desired.z-origin.z);
            if(offset.magnitude>VRGridRadius+VRGridReach||desired.y<VRGridFloor-VRGridReach||desired.y>VRGridCeiling+VRGridReach) return false;
            var grid=new Vector2(Mathf.Round(offset.x/VRGridStep),Mathf.Round(offset.y/VRGridStep));
            while(grid.sqrMagnitude*VRGridStep*VRGridStep>radius*radius)
            {
                if(Mathf.Abs(grid.x)>=Mathf.Abs(grid.y)) grid.x-=Mathf.Sign(grid.x);
                else grid.y-=Mathf.Sign(grid.y);
            }
            float gridY=Mathf.Round((desired.y-VRGridFloor)/VRGridStep);
            float minGridY=Mathf.Ceil((minY-VRGridFloor)/VRGridStep);
            float maxGridY=Mathf.Floor((maxY-VRGridFloor)/VRGridStep);
            if(minGridY>maxGridY) return false;
            position=new Vector3(origin.x+grid.x*VRGridStep,VRGridFloor+Mathf.Clamp(gridY,minGridY,maxGridY)*VRGridStep,origin.z+grid.y*VRGridStep);
            if(Vector3.Distance(position,desired)>VRGridReach) return false;
            foreach(var item in gridGroup)
            {
                var box=item.GetComponent<BoxCollider>();
                if(box==null||!box.enabled) continue;
                var itemPosition=position+delta*(item.transform.position-pivot);
                var itemRotation=delta*item.transform.rotation;
                var itemCenter=itemPosition+itemRotation*Vector3.Scale(box.center,box.transform.lossyScale);
                float itemRadius=Vector3.Scale(box.size*.5f,box.transform.lossyScale).magnitude;
                foreach(var other in views)
                {
                    if(other==null||other==moving||other.transform.IsChildOf(moving.transform)) continue;
                    var otherBox=other.GetComponent<BoxCollider>();
                    if(otherBox==null||!otherBox.enabled||!other.gameObject.activeInHierarchy) continue;
                    var otherCenter=otherBox.transform.TransformPoint(otherBox.center);
                    float otherRadius=Vector3.Scale(otherBox.size*.5f,otherBox.transform.lossyScale).magnitude;
                    float combinedRadius=itemRadius+otherRadius;
                    if((itemCenter-otherCenter).sqrMagnitude>combinedRadius*combinedRadius) continue;
                    if(Physics.ComputePenetration(box,itemPosition,itemRotation,otherBox,other.transform.position,other.transform.rotation,out _,out float distance)&&distance>.005f)
                        return false;
                }
            }
            return true;
        }

        public void Attach(PartView view,SnapTarget target,IReadOnlyList<PartView> group=null)
        {
            if(group==null||!group.Any(p=>detachedIds.Contains(p.Placement.instanceId))) Remember();
            var oldPosition=view.Placement.position;
            var oldRotation=view.Placement.rotation;
            var delta=target.rotation*Quaternion.Inverse(oldRotation);
            var items=group??new[]{view};
            foreach(var item in items)
            {
                item.Placement.position=target.position+delta*(item.Placement.position-oldPosition);
                item.Placement.rotation=delta*item.Placement.rotation;
                if(item==view)
                {
                    item.Placement.parentId=target.parentId;item.Placement.port=target.port;
                    if(target.parentId!=null)
                    {
                        var parent=Blueprint.parts.Find(p=>p.instanceId==target.parentId);
                        item.Placement.stage=parent?.stage??0;
                    }
                }
                if(!Blueprint.parts.Contains(item.Placement)) Blueprint.parts.Add(item.Placement);
                detachedIds.Remove(item.Placement.instanceId);
                item.Attached=true;item.SnapToPlacement();
            }
            snapCount++;Changed?.Invoke();
        }

        public void Delete(PartView view)
        {
            Remember();
            var ids=Blueprint.Subtree(view.Placement.instanceId).Select(p=>p.instanceId).ToHashSet();
            ids.Add(view.Placement.instanceId);
            Blueprint.parts.RemoveAll(p=>ids.Contains(p.instanceId));
            foreach(var item in views.Where(v=>ids.Contains(v.Placement.instanceId)).ToArray()) { views.Remove(item);Destroy(item.gameObject); }
            Changed?.Invoke();
        }

        public void Paint(PartView view,Color color)
        {
            Remember();
            foreach(var item in views.Where(v=>v.Attached&&(view==null||v==view))) {item.Placement.color=color;item.ApplyColor();}
            paintCount++;Changed?.Invoke();
        }

        public void SetStage(PartView view,int stage)
        {
            Remember();view.Placement.stage=Mathf.Clamp(stage,0,5);Changed?.Invoke();
        }

        public void Symmetry(PartView view)
        {
            if(!view.Attached||!view.Definition.surface||Blueprint.parts.Count>125) return;
            Remember();
            for(int i=1;i<4;i++)
            {
                var source=view.Placement;
                int port=2+(source.port-2+i*2)%8;
                if(Blueprint.parts.Any(p=>p.parentId==source.parentId&&p.port==port)) continue;
                var q=Quaternion.Euler(0,i*90,0);
                var parent=Blueprint.parts.Find(p=>p.instanceId==source.parentId);
                var center=parent?.position??Vector3.zero;
                var p=new PlacedPart {partId=source.partId,parentId=source.parentId,port=port,stage=source.stage,color=source.color,position=center+q*(source.position-center),rotation=q*source.rotation};
                RocketFinish.Copy(source,p);
                Blueprint.parts.Add(p);var clone=CreateView(view.Definition,p);clone.Attached=true;clone.SetPlacement();
            }
            Changed?.Invoke();
        }

        public void Undo()
        {
            if(undo.Count==0) return;
            redo.Add(Blueprint.Clone());var last=undo[undo.Count-1];undo.RemoveAt(undo.Count-1);Load(last,false);
        }
        public void Redo()
        {
            if(redo.Count==0) return;
            undo.Add(Blueprint.Clone());var last=redo[redo.Count-1];redo.RemoveAt(redo.Count-1);Load(last,false);
        }
    }
}
