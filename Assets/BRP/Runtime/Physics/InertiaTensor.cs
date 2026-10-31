using System;

namespace BRP
{
    public struct InertiaTensor
    {
        public double xx, yy, zz, xy, xz, yz;
        public void Add(double mass, DVector3 offset, double radius, double height)
            => Add(mass,offset,radius,height,new DVector3(0,1,0));

        public void Add(double mass, DVector3 offset, double radius, double height, DVector3 axis)
        {
            double side = mass * (3 * radius * radius + height * height) / 12;
            double axialDifference=mass*radius*radius/2-side;
            axis=axis.Normalized;
            xx += side + axialDifference*axis.x*axis.x + mass*(offset.y*offset.y+offset.z*offset.z);
            yy += side + axialDifference*axis.y*axis.y + mass*(offset.x*offset.x+offset.z*offset.z);
            zz += side + axialDifference*axis.z*axis.z + mass*(offset.x*offset.x+offset.y*offset.y);
            xy += axialDifference*axis.x*axis.y - mass*offset.x*offset.y;
            xz += axialDifference*axis.x*axis.z - mass*offset.x*offset.z;
            yz += axialDifference*axis.y*axis.z - mass*offset.y*offset.z;
        }
        public DVector3 Multiply(DVector3 v) => new DVector3(xx*v.x+xy*v.y+xz*v.z,xy*v.x+yy*v.y+yz*v.z,xz*v.x+yz*v.y+zz*v.z);
        public DVector3 Solve(DVector3 v)
        {
            double a=yy*zz-yz*yz, b=xz*yz-xy*zz, c=xy*yz-xz*yy;
            double d=xx*zz-xz*xz, e=xy*xz-xx*yz, f=xx*yy-xy*xy;
            double det=xx*a+xy*b+xz*c;
            return Math.Abs(det)>1e-12 ? new DVector3(a*v.x+b*v.y+c*v.z,b*v.x+d*v.y+e*v.z,c*v.x+e*v.y+f*v.z)/det : new DVector3();
        }
    }
}
