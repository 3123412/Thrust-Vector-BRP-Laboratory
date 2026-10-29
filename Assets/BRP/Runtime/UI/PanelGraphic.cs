using UnityEngine;
using UnityEngine.UI;

namespace BRP
{
    public sealed class PanelGraphic : UnityEngine.UI.Image
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var r=GetPixelAdjustedRect();float cut=Mathf.Min(5,Mathf.Min(r.width,r.height)*.1f);
            Vector2[] points={new Vector2(r.xMin+cut,r.yMin),new Vector2(r.xMax-cut,r.yMin),new Vector2(r.xMax,r.yMin+cut),new Vector2(r.xMax,r.yMax-cut),new Vector2(r.xMax-cut,r.yMax),new Vector2(r.xMin+cut,r.yMax),new Vector2(r.xMin,r.yMax-cut),new Vector2(r.xMin,r.yMin+cut)};
            helper.AddVert(r.center,color,new Vector2(.5f,.5f));
            for(int i=0;i<points.Length;i++)
            {
                var c=color;float height=Mathf.InverseLerp(r.yMin,r.yMax,points[i].y);c.r*=.95f+height*.08f;c.g*=.95f+height*.08f;c.b*=.95f+height*.08f;
                helper.AddVert(points[i],c,new Vector2(Mathf.InverseLerp(r.xMin,r.xMax,points[i].x),height));
            }
            for(int i=0;i<points.Length;i++) helper.AddTriangle(0,i+1,(i+1)%points.Length+1);
        }
    }
}
