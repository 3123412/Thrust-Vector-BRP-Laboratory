using UnityEngine;
using UnityEngine.UI;

namespace BRP
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AttitudeIndicator : MaskableGraphic
    {
        float pitch=90,roll;
        public void SetState(FlightBody body)
        {
            var up=body.orientation.Inverse.Rotate(body.position.Normalized);
            pitch=(float)System.Math.Asin(System.Math.Clamp(up.y,-1,1))*Mathf.Rad2Deg;
            if(up.x*up.x+up.z*up.z>.0001) roll=Mathf.Atan2((float)up.x,(float)up.z);
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.48f;
            float horizon=-pitch/90*radius;
            for(int i=0;i<64;i++)
            {
                float a=-radius+2*radius*i/64,b=-radius+2*radius*(i+1)/64;
                float x0=Mathf.Sqrt(Mathf.Max(0,radius*radius-a*a)),x1=Mathf.Sqrt(Mathf.Max(0,radius*radius-b*b));
                Color tint=(a+b)*.5f>horizon?new Color(.1f,.27f,.36f):new Color(.32f,.2f,.12f);
                Quad(mesh,new Vector2(-x0,a),new Vector2(x0,a),new Vector2(x1,b),new Vector2(-x1,b),tint,true);
            }
            for(int n=-80;n<=80;n+=10)
            {
                float y=(n-pitch)/90*radius;if(Mathf.Abs(y)>radius*.9f) continue;
                float width=n==0?Mathf.Sqrt(radius*radius-y*y):n%30==0?radius*.42f:radius*.22f;
                Quad(mesh,new Vector2(-width,y-.6f),new Vector2(width,y-.6f),new Vector2(width,y+.6f),new Vector2(-width,y+.6f),new Color(.8f,.85f,.83f,.65f),true);
            }
            Quad(mesh,new Vector2(-37,-1.4f),new Vector2(-10,-1.4f),new Vector2(-10,1.4f),new Vector2(-37,1.4f),UiTheme.Orange,false);
            Quad(mesh,new Vector2(10,-1.4f),new Vector2(37,-1.4f),new Vector2(37,1.4f),new Vector2(10,1.4f),UiTheme.Orange,false);
            Quad(mesh,new Vector2(-1.4f,-8),new Vector2(1.4f,-8),new Vector2(1.4f,8),new Vector2(-1.4f,8),UiTheme.Orange,false);
        }
        void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint,bool rotate)
        {
            int start=mesh.currentVertCount;
            Vector2 Transform(Vector2 p)=>rotate?new Vector2(p.x*Mathf.Cos(roll)-p.y*Mathf.Sin(roll),p.x*Mathf.Sin(roll)+p.y*Mathf.Cos(roll)):p;
            foreach(var point in new[]{a,b,c,d}) mesh.AddVert(Transform(point)+rectTransform.rect.center,tint*color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
