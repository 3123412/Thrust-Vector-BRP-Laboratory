using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using System.Collections;

namespace BRP
{
    public sealed class PartView : MonoBehaviour
    {
        [field: SerializeField] public PartDefinition Definition { get; private set; }
        [field: SerializeField] public PlacedPart Placement { get; private set; }
        [field: SerializeField] public bool Attached { get; set; }
        [field: SerializeField] public AssemblyController Owner { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public Rigidbody Body { get; private set; }
        System.Collections.Generic.List<PartView> heldGroup;
        void Awake()
        {
            if(Definition!=null) Initialize(Owner,Definition,Placement);
        }
        public void Initialize(AssemblyController owner,PartDefinition definition,PlacedPart placement)
        {
            Owner=owner;Definition=definition;Placement=placement;
            var box=GetComponent<BoxCollider>();
            if(box==null) box=gameObject.AddComponent<BoxCollider>();
            box.center=Vector3.up*definition.height*.5f;
            box.size=new Vector3(definition.radius*2,definition.height,definition.radius*2);
            if(definition.surface) {box.center=new Vector3(.1f,definition.height*.5f,0);box.size=new Vector3(.5f,definition.height,.3f);}
            bool hasBounds=false;
            var bounds=new Bounds();
            foreach(var filter in GetComponentsInChildren<MeshFilter>())
            {
                if(filter.sharedMesh==null) continue;
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var local=transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if(!hasBounds) {bounds=new Bounds(local,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(local);
                }
            }
            if(hasBounds) {box.center=bounds.center;box.size=bounds.size+Vector3.one*.02f;}
            Body=GetComponent<Rigidbody>();
            if(Body==null) Body=gameObject.AddComponent<Rigidbody>();
            Body.isKinematic=true;Body.useGravity=false;
            Body.mass=Mathf.Clamp(definition.dryMass/100, .1f,20);
            Body.interpolation=RigidbodyInterpolation.Interpolate;
            Grab=GetComponent<XRGrabInteractable>();
            if(Grab==null) Grab=gameObject.AddComponent<XRGrabInteractable>();
            Grab.movementType=XRBaseInteractable.MovementType.Kinematic;
            Grab.throwOnDetach=false;Grab.useDynamicAttach=true;
            Grab.selectEntered.RemoveListener(OnGrab);Grab.selectExited.RemoveListener(OnRelease);
            Grab.selectEntered.AddListener(OnGrab);Grab.selectExited.AddListener(OnRelease);
            ApplyColor();
        }
        void OnGrab(SelectEnterEventArgs args)
        {
            if(Attached)
            {
                Grab.interactionManager.SelectExit(args.interactorObject,Grab);
                LaboratoryGame.Instance?.Toast("Сначала нажмите «Разблокировать» в паспорте детали");
                return;
            }
            LaboratoryGame.Instance?.Select(this);
            heldGroup=Owner.DetachedGroup(this);
            foreach(var child in heldGroup) if(child!=this) child.transform.SetParent(transform,true);
        }
        void OnRelease(SelectExitEventArgs args)
        {
            if(Attached) return;
            if(heldGroup!=null) foreach(var child in heldGroup) if(child!=this) child.transform.SetParent(Owner.assemblyRoot,true);
            if(Owner.TryTarget(this,out var target))
            {
                Owner.Attach(this,target,heldGroup);
                var haptic=args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
                if(haptic!=null) haptic.SendHapticImpulse(.45f,.065f);
                LaboratoryGame.Instance?.Sound.Play(SoundCue.Snap);
            }
            else {Body.isKinematic=false;Body.useGravity=true;}
            heldGroup=null;
        }
        public void SetPlacement()
        {
            Body.isKinematic=true;Body.useGravity=false;
            transform.localPosition=Placement.position;transform.localRotation=Placement.rotation;
            ApplyColor();
        }
        public void SnapToPlacement()
        {
            if(!Application.isPlaying) {SetPlacement();return;}
            Body.isKinematic=true;Body.useGravity=false;
            StopAllCoroutines();StartCoroutine(SnapAnimation());
        }
        IEnumerator SnapAnimation()
        {
            Vector3 start=transform.localPosition;Quaternion rotation=transform.localRotation;
            float elapsed=0;
            while(elapsed<.14f)
            {
                elapsed+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,elapsed/.14f);
                transform.localPosition=Vector3.Lerp(start,Placement.position,t);transform.localRotation=Quaternion.Slerp(rotation,Placement.rotation,t);
                yield return null;
            }
            SetPlacement();
        }
        public void ApplyColor()
        {
            if(Placement==null||Definition==null) return;
            RocketFinish.Apply(gameObject,Placement,Definition);
        }
        void Update()
        {
            if(!Attached&&transform.position.y<-.5f)
            {
                Body.isKinematic=true;Body.useGravity=false;
                transform.position=Owner.assemblyRoot.position+new Vector3(-.85f,.15f,0);
            }
        }
    }
}
