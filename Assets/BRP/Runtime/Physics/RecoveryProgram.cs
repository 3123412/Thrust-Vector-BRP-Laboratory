using System;
using System.Linq;

namespace BRP
{
    public sealed class RecoveryProgram
    {
        public bool active;
        public string phase="Отключена";
        public FlightBody Update(FlightBody body)
        {
            if(!active||body.crashed||body.landed) return null;
            body.guidance=GuidanceMode.Retrograde;
            if(body.aborted&&body.parts.Any(p=>p.definition.kind==PartKind.EscapeTower&&p.fuel>0)) body.guidance=GuidanceMode.Hold;
            bool carrier=body.parts.Any(p=>!p.destroyed&&(p.definition.kind==PartKind.VacuumEngine||p.definition.kind==PartKind.BoosterEngine));
            if(carrier&&body.Orbit.periapsis>20000)
            {
                phase="Торможение для схода с орбиты";
                var retro=-body.velocity.Normalized;
                double alignment=DVector3.Dot(body.orientation.Rotate(new DVector3(0,1,0)),retro);
                body.throttle=alignment>.98?.4:0;
                body.lastEvent=phase;return null;
            }
            body.throttle=0;
            FlightBody detached=carrier?body.SeparateCapsule():null;
            phase=body.Altitude>4500?"Спуск капсулы":"Парашютная посадка";
            if(body.Altitude<4500&&body.VerticalSpeed<0&&body.SurfaceVelocity.Magnitude<330&&!body.mainChuteDeployed) body.DeployParachutes();
            body.lastEvent=phase;return detached;
        }
    }
}
