using System;
using UnityEngine;

namespace BRP
{
    [Serializable]
    public struct DVector3
    {
        public double x, y, z;
        public DVector3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public DVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public double SqrMagnitude => x * x + y * y + z * z;
        public double Magnitude => Math.Sqrt(SqrMagnitude);
        public DVector3 Normalized => Magnitude > 1e-12 ? this / Magnitude : new DVector3();
        public Vector3 Float => new Vector3((float)x, (float)y, (float)z);
        public static DVector3 operator +(DVector3 a, DVector3 b) => new DVector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static DVector3 operator -(DVector3 a, DVector3 b) => new DVector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static DVector3 operator -(DVector3 a) => a * -1;
        public static DVector3 operator *(DVector3 a, double b) => new DVector3(a.x * b, a.y * b, a.z * b);
        public static DVector3 operator *(double b, DVector3 a) => a * b;
        public static DVector3 operator /(DVector3 a, double b) => a * (1 / b);
        public static double Dot(DVector3 a, DVector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static DVector3 Cross(DVector3 a, DVector3 b) => new DVector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }
}
