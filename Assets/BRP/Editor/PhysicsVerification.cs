using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BRP.Editor
{
    public static class PhysicsVerification
    {
        public static VerificationSuite.Report Run()
        {
            var report=new VerificationSuite.Report {utc=DateTime.UtcNow.ToString("O")};
            var catalog=Resources.Load<PartCatalog>("BRP/Catalog");
            void Require(bool pass,string message) {if(!pass) throw new InvalidOperationException(message);}
            void Check(string name,Func<string> action)
            {
                try {report.checks.Add(new VerificationSuite.CheckResult {name=name,passed=true,details=action()});report.passed++;}
                catch(Exception error) {report.checks.Add(new VerificationSuite.CheckResult {name=name,details=error.GetBaseException().Message});report.failed++;}
            }
            FlightBody Body(CraftBlueprint craft)
            {
                var body=new FlightBody(craft,catalog) {referenceBody=CelestialBody.Moon,launched=true,throttle=0,guidance=GuidanceMode.Manual};
                body.position=new DVector3(0,1e9,0);body.velocity=new DVector3();return body;
            }
            DVector3 AngularMomentum(FlightBody body,DVector3 origin)
                =>body.orientation.Rotate(body.inertia.Multiply(body.angularVelocity))+DVector3.Cross(body.position-origin,body.velocity*body.mass);
            Check("rotated cylinder inertia transforms as a tensor",()=>
            {
                var original=new InertiaTensor();original.Add(120,new DVector3(),.4,5);
                var rotation=new DQuaternion(Quaternion.Euler(32,19,67));
                var rotated=new InertiaTensor();rotated.Add(120,new DVector3(),.4,5,rotation.Rotate(new DVector3(0,1,0)));
                var velocity=new DVector3(.3,.7,-.2);
                var expected=rotation.Rotate(original.Multiply(velocity));
                double error=(rotated.Multiply(rotation.Rotate(velocity))-expected).Magnitude;
                Require(error<.001,"Inertia ignores module orientation: "+error);
                Require((rotated.Solve(rotated.Multiply(velocity))-velocity).Magnitude<1e-10,"Tensor inverse failed");
                return $"Rotated angular momentum error={error:E3}";
            });
            Check("fuel depletion preserves the structural coordinate frame",()=>
            {
                var body=Body(CraftPresets.LunarLander(catalog));
                var origin=body.position-body.orientation.Rotate(body.centerOfMass);
                body.parts.First(p=>p.fuel>100).fuel-=100;
                body.Step(.02);
                var after=body.position-body.orientation.Rotate(body.centerOfMass);
                double error=(after-origin).Magnitude;
                Require(error<.001,"Rocket model jumps when fuel changes: "+error);
                return $"Structural origin displacement={error:E3}m";
            });
            Check("RCS expenditure updates mass in the same step",()=>
            {
                var body=Body(CraftPresets.OrbitalVehicle(catalog));body.translationInput=new DVector3(1,0,0);
                double before=body.mass;body.Step(.02);
                Require(body.mass<before,"RCS did not expend propellant");
                double error=Math.Abs(body.mass-body.parts.Sum(p=>p.Mass));
                Require(error<1e-9,"Stale mass after RCS consumption");
                return $"Resource/mass disagreement={error:E3}kg";
            });
            Check("vacuum burn agrees with the rocket equation",()=>
            {
                var definition=catalog.Find(PartKind.SolidBooster);
                var craft=new CraftBlueprint();craft.parts.Add(new PlacedPart {partId=definition.id});
                var body=Body(craft);double initialMass=body.mass;
                for(int i=0;i<100;i++) body.Step(.02);
                double expected=definition.specificImpulse*OrbitalMechanics.StandardGravity*Math.Log(initialMass/body.mass);
                double error=Math.Abs(body.velocity.y-expected)/expected;
                Require(body.totalFuelUsed>0&&error<.00001,"Burn delta-v error: "+error);
                return $"Expected={expected:F5}m/s; actual={body.velocity.y:F5}m/s; relative error={error:E3}";
            });
            Check("RCS impulse scales with its last propellant remainder",()=>
            {
                var body=Body(CraftPresets.OrbitalVehicle(catalog));
                foreach(var part in body.parts.Where(p=>p.definition.kind==PartKind.ServiceTank)) part.fuel=1e-6;
                body.RecalculateMass();double initialMass=body.mass;
                body.translationInput=new DVector3(1,0,0);body.Step(.02);
                double impulse=body.velocity.x*(initialMass+body.mass)*.5;
                double expected=1200*1e-6/.1;
                Require(Math.Abs(impulse-expected)<1e-7,"RCS creates a full impulse with nearly empty tank: "+impulse);
                return $"Last 1mg propellant: impulse={impulse:F6}Ns; expected={expected:F6}Ns";
            });
            Check("offset radial separation conserves angular and linear momentum",()=>
            {
                var craft=new CraftBlueprint();
                var core=new PlacedPart {partId=catalog.Find(PartKind.FirstStage).id};craft.parts.Add(core);
                var cut=new PlacedPart {partId=catalog.Find(PartKind.RadialDecoupler).id,parentId=core.instanceId,position=new Vector3(1,4,0)};craft.parts.Add(cut);
                craft.parts.Add(new PlacedPart {partId=catalog.Find(PartKind.SolidBooster).id,parentId=cut.instanceId,position=new Vector3(2,4,0),rotation=Quaternion.Euler(0,0,15)});
                var body=Body(craft);body.velocity=new DVector3(800,500,25);body.angularVelocity=new DVector3(.2,.3,-.1);
                body.orientation=new DQuaternion(Quaternion.Euler(12,28,19));
                body.position=new DVector3(0,CelestialBody.Moon.radius+5000,0);
                var origin=body.position;var linear=body.velocity*body.mass;var angular=AngularMomentum(body,origin);
                var other=body.Separate();Require(other!=null,"No radial separation");
                double linearError=(body.velocity*body.mass+other.velocity*other.mass-linear).Magnitude;
                double angularError=(AngularMomentum(body,origin)+AngularMomentum(other,origin)-angular).Magnitude;
                Require(linearError<.01,"Separation linear momentum error: "+linearError);
                Require(angularError<.01,"Separation angular momentum error: "+angularError);
                return $"Linear error={linearError:E3}; angular error={angularError:E3}";
            });
            Check("horizontal hull contacts ground at its radius",()=>
            {
                var definition=catalog.Find(PartKind.FirstStage);
                var craft=new CraftBlueprint();craft.parts.Add(new PlacedPart {partId=definition.id});
                var body=Body(craft);body.orientation=new DQuaternion(Quaternion.Euler(0,0,90));
                double error=Math.Abs(body.GroundClearance-definition.radius);
                Require(error<.00001,"Tilted hull ground clearance error: "+error);
                return $"Clearance={body.GroundClearance:F5}m; radius={definition.radius:F5}m";
            });
            Check("guidance can leave an exactly reversed attitude",()=>
            {
                var body=Body(CraftPresets.OrbitalVehicle(catalog));body.guidance=GuidanceMode.Target;
                body.targetDirection=new DVector3(0,-1,0);body.Step(.02);
                Require(body.angularVelocity.Magnitude>1e-6,"Antiparallel guidance has zero control torque");
                return $"Angular speed after reversal command={body.angularVelocity.Magnitude:F6}rad/s";
            });
            Check("tail fins restore attitude and damp angular motion",()=>
            {
                FlightBody Finned(float height,DVector3 air,DVector3 spin)
                {
                    var craft=new CraftBlueprint();var root=new PlacedPart {partId=catalog.Find(PartKind.FirstStage).id};craft.parts.Add(root);
                    craft.parts.Add(new PlacedPart {partId=catalog.Find(PartKind.Fin).id,parentId=root.instanceId,position=new Vector3(0,height,0)});
                    var body=Body(craft);body.referenceBody=CelestialBody.Earth;
                    body.position=new DVector3(0,OrbitalMechanics.Radius+10000,0);
                    body.velocity=body.referenceBody.SurfaceVelocity(body.position)+air;body.angularVelocity=spin;
                    return body;
                }
                var tail=Finned(-2,new DVector3(10,100,0),new DVector3());
                var nose=Finned(3,new DVector3(10,100,0),new DVector3());
                var damping=Finned(-2,new DVector3(0,100,0),new DVector3(0,0,.2));
                tail.Step(.02);nose.Step(.02);damping.Step(.02);
                Require(tail.angularVelocity.z<0&&nose.angularVelocity.z>0,"Fin location has incorrect stability sign");
                Require(damping.angularVelocity.z>0&&damping.angularVelocity.z<.2,"Airflow does not damp pitch rate");
                return $"Tail={tail.angularVelocity.z:E3}; nose={nose.angularVelocity.z:E3}; damped pitch={damping.angularVelocity.z:F6}rad/s";
            });
            Check("large simulation step retains bounded physics substeps",()=>
            {
                var craft=CraftPresets.LunarLander(catalog);var a=Body(craft.Clone());var b=Body(craft.Clone());a.throttle=b.throttle=.6;
                a.Step(.4);for(int i=0;i<20;i++) b.Step(.02);
                double error=(a.position-b.position).Magnitude+(a.velocity-b.velocity).Magnitude;
                Require(error<1e-6&&Math.Abs(a.Fuel-b.Fuel)<1e-8,"Large step bypasses force/resource integration: "+error);
                return $"0.4s vs 20x0.02s state difference={error:E3}";
            });
            foreach(var reference in new[]{CelestialBody.Earth,CelestialBody.Moon})
            {
                Check(reference.name+" eccentric orbit energy and angular momentum",()=>
                {
                    double radius=reference.radius+300000;
                    var position=new DVector3(radius,0,0);var velocity=new DVector3(0,Math.Sqrt(reference.mu/radius)*1.2,0);
                    double energy=velocity.SqrMagnitude*.5-reference.mu/position.Magnitude;
                    var angular=DVector3.Cross(position,velocity);
                    double duration=OrbitalMechanics.Elements(position,velocity,reference.mu,reference.radius).period*5;
                    int count=(int)Math.Ceiling(duration/4);double step=duration/count;
                    for(int i=0;i<count;i++) OrbitalMechanics.Integrate(ref position,ref velocity,step,(p,v)=>reference.Gravity(p));
                    double energyError=Math.Abs((velocity.SqrMagnitude*.5-reference.mu/position.Magnitude-energy)/energy);
                    double angularError=(DVector3.Cross(position,velocity)-angular).Magnitude/angular.Magnitude;
                    Require(energyError<1e-6&&angularError<1e-6,"Orbit drift exceeds tolerance");
                    return $"Five eccentric orbits; energy error={energyError:E3}; angular momentum error={angularError:E3}";
                });
            }
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/physics-verification.json",JsonUtility.ToJson(report,true));
            Debug.Log($"BRP physics checks: {report.passed} passed, {report.failed} failed");return report;
        }
    }
}
