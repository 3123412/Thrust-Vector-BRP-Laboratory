using System;
using UnityEngine;

namespace BRP
{
    [Serializable]
    public struct DQuaternion
    {
        public double x, y, z, w;
        public DQuaternion(double x, double y, double z, double w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public DQuaternion(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
        public static DQuaternion Identity => new DQuaternion(0, 0, 0, 1);
        public Quaternion Float => new Quaternion((float)x, (float)y, (float)z, (float)w);
        public DQuaternion Inverse => new DQuaternion(-x, -y, -z, w);
        public DQuaternion Normalized
        {
            get { double n = Math.Sqrt(x*x+y*y+z*z+w*w); return new DQuaternion(x/n,y/n,z/n,w/n); }
        }
        public static DQuaternion operator *(DQuaternion a, DQuaternion b) => new DQuaternion(
            a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y,
            a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x,
            a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w,
            a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z);
        public DVector3 Rotate(DVector3 v)
        {
            var q = new DVector3(x,y,z);
            var t = DVector3.Cross(q,v)*2;
            return v + t*w + DVector3.Cross(q,t);
        }
        public void Integrate(DVector3 localAngularVelocity, double dt)
        {
            double angle = localAngularVelocity.Magnitude * dt;
            if (angle < 1e-14) return;
            var axis = localAngularVelocity.Normalized * Math.Sin(angle/2);
            this = (this * new DQuaternion(axis.x,axis.y,axis.z,Math.Cos(angle/2))).Normalized;
        }
    }
}
