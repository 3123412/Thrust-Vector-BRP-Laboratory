using System;
using System.Linq;

namespace BRP
{
    public sealed class AscentProgram
    {
        public bool active;
        public string phase="Отключена";
        public double targetAltitude=180000;

        public FlightBody Update(FlightBody body,double dt)
        {
            if(!active||body.crashed||body.landed) return null;
            var up=body.position.Normalized;
            var east=DVector3.Cross(new DVector3(0,0,1),up).Normalized;
            var orbit=body.Orbit;
            if(orbit.periapsis>=120000&&orbit.apoapsis<=250000)
            {
                body.throttle=0;active=false;phase="Орбита достигнута";body.lastEvent=phase;return null;
            }
            bool fuel=body.parts.Any(p=>!p.destroyed&&p.placement.stage==body.stage&&p.definition.kind!=PartKind.ServiceTank&&p.fuel>1&&p.oxidizer>1);
            FlightBody detached=null;
            if(!fuel&&body.parts.Any(p=>p.active&&p.definition.kind==PartKind.StackDecoupler&&p.placement.stage<=body.stage)) detached=body.Separate();
            body.guidance=GuidanceMode.Target;
            phase=body.stage==0?"Подъём первой ступени":"Формирование орбиты";
            double vertical=DVector3.Dot(body.velocity,up);
            double horizontal=(body.velocity-up*vertical).Magnitude;
            double desiredVertical=Math.Clamp((targetAltitude-body.Altitude)/90,-200,850);
            double acceleration=body.AvailableThrust/Math.Max(1,body.mass);
            double gravity=OrbitalMechanics.Mu/(body.position.Magnitude*body.position.Magnitude);
            double radialFraction=Math.Clamp((gravity-horizontal*horizontal/body.position.Magnitude+(desiredVertical-vertical)*.035)/Math.Max(.1,acceleration*.98),-.4,.9999);
            body.targetDirection=up*radialFraction+east*Math.Sqrt(1-radialFraction*radialFraction);
            body.throttle=1;
            body.lastEvent=phase;
            return detached;
        }
    }
}
