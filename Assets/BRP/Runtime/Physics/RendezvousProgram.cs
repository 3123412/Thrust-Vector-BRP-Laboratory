using System;

namespace BRP
{
    public sealed class RendezvousProgram
    {
        public bool active;
        public bool Update(FlightBody body,FlightBody target)
        {
            var offset=target.DockingPoint-body.DockingPoint;
            body.targetDirection=offset;
            if(!active) return false;
            body.guidance=GuidanceMode.Target;
            var desired=offset.Normalized*Math.Min(.65,offset.Magnitude*.08);
            var correction=desired-(body.DockingVelocity-target.DockingVelocity);
            body.translationInput=body.orientation.Inverse.Rotate(correction)*1.8;
            if(body.translationInput.Magnitude>1) body.translationInput=body.translationInput.Normalized;
            bool ready=body.CanDock(target,out _,out _,out _);
            if(ready) {active=false;body.translationInput=new DVector3();}
            return ready;
        }
    }
}
