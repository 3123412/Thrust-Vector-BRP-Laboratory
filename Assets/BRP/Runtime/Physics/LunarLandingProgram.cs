using System;

namespace BRP
{
    public sealed class LunarLandingProgram
    {
        public bool active;
        public string phase="Отключена";
        bool descent;
        public void Begin(bool fromOrbit) {active=true;descent=!fromOrbit;}
        public void Update(FlightBody body)
        {
            if(!active||!body.IsLunar) return;
            if(body.landed||body.crashed)
            {
                active=false;body.throttle=0;body.lastThrust=0;
                foreach(var part in body.parts) part.thrust=0;
                return;
            }
            double altitude=body.GroundAltitude;
            var up=body.position.Normalized;
            var surface=body.SurfaceVelocity;
            double radial=DVector3.Dot(surface,up);
            var lateral=surface-up*radial;
            if(!descent&&body.Orbit.periapsis>250)
            {
                phase="Луна / торможение с орбиты";body.guidance=GuidanceMode.Retrograde;
                body.throttle=DVector3.Dot(body.orientation.Rotate(new DVector3(0,1,0)),-body.velocity.Normalized)>.985?.18:0;
            }
            else if(!descent&&altitude>25000)
            {
                phase="Луна / ожидание посадочного торможения";body.throttle=0;body.guidance=GuidanceMode.Retrograde;
            }
            else
            {
                descent=true;phase=altitude>100?"Луна / двигательный спуск":"Луна / касание опор";
                double gravity=body.referenceBody.mu/body.position.SqrMagnitude;
                double available=body.AvailableThrust/Math.Max(1,body.mass);
                double descentSpeed=Math.Min(.32*Math.Sqrt(2*Math.Max(.5,available-gravity)*Math.Max(0,altitude)),.22*altitude+.6);
                double target=lateral.Magnitude>100?-45:-Math.Clamp(descentSpeed,.6,90);
                double vertical=Math.Max(0,gravity+(target-radial)*.9);
                var horizontal=-lateral*.12;
                double horizontalLimit=Math.Max(0,available*.75);
                if(horizontal.Magnitude>horizontalLimit) horizontal=horizontal.Normalized*horizontalLimit;
                var acceleration=up*vertical+horizontal;
                body.guidance=GuidanceMode.Target;
                body.targetDirection=acceleration.Magnitude>.01?acceleration.Normalized:up;
                double alignment=DVector3.Dot(body.orientation.Rotate(new DVector3(0,1,0)),body.targetDirection);
                body.throttle=alignment>.94?Math.Clamp(acceleration.Magnitude/Math.Max(.1,available),0,1):0;
            }
            body.lastEvent=phase;
        }
    }
}
