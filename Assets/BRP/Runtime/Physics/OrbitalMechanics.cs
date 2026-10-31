using System;

namespace BRP
{
    public readonly struct OrbitElements
    {
        public readonly double apoapsis, periapsis, period, inclination, eccentricity, semiMajorAxis;
        public OrbitElements(double ap, double pe, double period, double inc, double e, double a)
        {
            apoapsis = ap; periapsis = pe; this.period = period; inclination = inc; eccentricity = e; semiMajorAxis = a;
        }
        public bool Bound => eccentricity < 1 && periapsis > 0;
    }

    public static class OrbitalMechanics
    {
        public const double Radius = 6371000;
        public const double Mu = 3.986004418e14;
        public const double RotationRate = 7.2921159e-5;
        public const double StandardGravity = 9.80665;

        public static DVector3 Gravity(DVector3 position,double mu=Mu)
        {
            double r = position.Magnitude;
            return r > 1 ? position * (-mu / (r * r * r)) : new DVector3();
        }

        public static double Density(double altitude)
        {
            if (altitude > 160000) return 0;
            altitude = Math.Max(0, altitude);
            double scale = altitude < 11000 ? 8500 : altitude < 25000 ? 6800 : 7400;
            double baseDensity = altitude < 11000 ? 1.225 : altitude < 25000 ? 0.336 : 0.0428;
            double baseHeight = altitude < 11000 ? 0 : altitude < 25000 ? 11000 : 25000;
            return baseDensity * Math.Exp(-(altitude - baseHeight) / scale);
        }

        public static DVector3 AtmosphereVelocity(DVector3 position) => DVector3.Cross(new DVector3(0, 0, RotationRate), position);

        public static double AirTemperature(double altitude)
        {
            altitude=Math.Max(0,altitude);
            if(altitude<11000) return 288.15-.0065*altitude;
            if(altitude<20000) return 216.65;
            if(altitude<32000) return 216.65+.001*(altitude-20000);
            if(altitude<47000) return 228.65+.0028*(altitude-32000);
            if(altitude<51000) return 270.65;
            return Math.Max(186.95,270.65-.0028*(altitude-51000));
        }

        public static double SpeedOfSound(double altitude) => Math.Sqrt(1.4*287.05*AirTemperature(altitude));

        public static double WaveDragMultiplier(double mach)
        {
            mach=Math.Max(0,mach);
            double rise=Math.Exp(-Math.Pow((mach-1.05)/.24,2));
            double supersonic=mach>1?.24*(1-Math.Exp(-(mach-1)*4))/Math.Sqrt(mach):0;
            return 1+.48*rise+supersonic;
        }

        public static OrbitElements Elements(DVector3 position, DVector3 velocity,double mu=Mu,double radius=Radius)
        {
            double r = position.Magnitude;
            var h = DVector3.Cross(position, velocity);
            var e = DVector3.Cross(velocity, h) / mu - position / r;
            double eccentricity = e.Magnitude;
            double energy = velocity.SqrMagnitude / 2 - mu / r;
            double a = Math.Abs(energy) > 1e-12 ? -mu / (2 * energy) : double.PositiveInfinity;
            double pe = h.SqrMagnitude / mu / (1 + eccentricity) - radius;
            double ap = energy < 0 ? a * (1 + eccentricity) - radius : double.PositiveInfinity;
            double period = energy < 0 ? 2 * Math.PI * Math.Sqrt(a * a * a / mu) : double.PositiveInfinity;
            double inc = h.Magnitude > 1e-9 ? Math.Acos(Math.Clamp(h.z / h.Magnitude, -1, 1)) * 180 / Math.PI : 0;
            return new OrbitElements(ap, pe, period, inc, eccentricity, a);
        }

        public static void Integrate(ref DVector3 position, ref DVector3 velocity, double dt, Func<DVector3, DVector3, DVector3> acceleration)
        {
            var v1 = velocity;
            var a1 = acceleration(position, velocity);
            var v2 = velocity + a1 * (dt / 2);
            var a2 = acceleration(position + v1 * (dt / 2), v2);
            var v3 = velocity + a2 * (dt / 2);
            var a3 = acceleration(position + v2 * (dt / 2), v3);
            var v4 = velocity + a3 * dt;
            var a4 = acceleration(position + v3 * dt, v4);
            position += (v1 + 2 * v2 + 2 * v3 + v4) * (dt / 6);
            velocity += (a1 + 2 * a2 + 2 * a3 + a4) * (dt / 6);
        }
    }
}
