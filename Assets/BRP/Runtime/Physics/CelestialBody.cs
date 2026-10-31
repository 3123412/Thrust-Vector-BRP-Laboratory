using System;

namespace BRP
{
    public sealed class CelestialBody
    {
        public static readonly CelestialBody Earth=new CelestialBody("Земля",6371000,3.986004418e14,7.2921159e-5,true);
        public static readonly CelestialBody Moon=new CelestialBody("Луна",1737400,4.9048695e12,2.6617e-6,false);
        public readonly string name;
        public readonly double radius,mu,rotationRate;
        public readonly bool atmosphere;
        CelestialBody(string name,double radius,double mu,double rotationRate,bool atmosphere)
        {this.name=name;this.radius=radius;this.mu=mu;this.rotationRate=rotationRate;this.atmosphere=atmosphere;}
        public double SurfaceGravity=>mu/(radius*radius);
        public DVector3 Gravity(DVector3 position)=>OrbitalMechanics.Gravity(position,mu);
        public DVector3 SurfaceVelocity(DVector3 position)=>DVector3.Cross(new DVector3(0,0,rotationRate),position);
        public static DVector3 MoonPosition(double time)
        {
            double angle=.6+time*2*Math.PI/(27.321661*86400);
            return new DVector3(Math.Cos(angle)*384400000,Math.Sin(angle)*384400000,Math.Sin(angle)*34400000);
        }
    }
}
