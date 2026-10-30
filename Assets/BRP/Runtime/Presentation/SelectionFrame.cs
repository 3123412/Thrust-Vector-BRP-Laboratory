using UnityEngine;

namespace BRP
{
    public sealed class SelectionFrame : MonoBehaviour
    {
        static readonly int[] Path={0,1,3,2,0,4,5,1,5,7,3,7,6,2,6,4};
        readonly Vector3[] points=new Vector3[16];
        LineRenderer line;
        PartView selected;
        BoxCollider bounds;
        LaboratoryGame game;

        void Start()
        {
            game=GetComponent<LaboratoryGame>();
            line=new GameObject("Selected component bounds").AddComponent<LineRenderer>();
            line.transform.SetParent(transform,false);line.sharedMaterial=game.Assets.orbitMaterial;
            line.startColor=line.endColor=UiTheme.Cyan;line.startWidth=line.endWidth=.003f;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        }

        void LateUpdate()
        {
            if(game.Selected!=selected)
            {
                selected=game.Selected;bounds=selected==null?null:selected.GetComponent<BoxCollider>();
            }
            bool visible=selected!=null&&bounds!=null&&!game.IsFlying&&!game.UI.ModalOpen;
            line.enabled=visible;if(!visible) return;
            Vector3 extent=bounds.size*.5f+Vector3.one*.035f;
            for(int i=0;i<Path.Length;i++)
            {
                int corner=Path[i];
                points[i]=selected.transform.TransformPoint(bounds.center+Vector3.Scale(extent,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
            }
            line.positionCount=points.Length;line.SetPositions(points);
        }
    }
}
