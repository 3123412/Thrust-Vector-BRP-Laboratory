using UnityEngine;
using UnityEngine.UI;

namespace BRP
{
    public sealed class FinishPreview : MonoBehaviour
    {
        GameObject model,stage;
        Camera camera;
        RenderTexture target;
        PartDefinition definition;
        PlacedPart placement;

        public void Initialize(PartDefinition part,PlacedPart finish,RawImage image)
        {
            definition=part;placement=finish;
            stage=new GameObject("Finish preview studio");stage.transform.position=new Vector3(12000,12000,12000);
            model=Instantiate(part.model,stage.transform);
            foreach(var child in model.GetComponentsInChildren<Transform>()) child.gameObject.layer=30;
            var renderers=model.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            camera=new GameObject("Finish preview camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(stage.transform,false);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.055f);
            camera.cullingMask=1<<30;camera.fieldOfView=32;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().allowXRRendering=false;
            camera.transform.position=bounds.center+new Vector3(.35f,.18f,-1).normalized*bounds.size.magnitude*2;
            camera.transform.LookAt(bounds.center);
            target=new RenderTexture(640,640,24) {antiAliasing=4};target.Create();camera.targetTexture=target;image.texture=target;
            Refresh();
        }

        public void Refresh() {if(model!=null) RocketFinish.Apply(model,placement,definition);}

        void Update()
        {
            if(model!=null) model.transform.Rotate(Vector3.up,Time.unscaledDeltaTime*12,Space.World);
        }

        void OnDestroy()
        {
            if(camera!=null) camera.targetTexture=null;
            if(stage!=null) Destroy(stage);
            if(target!=null) {target.Release();Destroy(target);}
        }
    }
}
