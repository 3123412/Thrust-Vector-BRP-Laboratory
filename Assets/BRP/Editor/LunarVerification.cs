using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BRP.Editor
{
    public static class LunarVerification
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
            FlightBody Lander(double altitude,bool orbital)
            {
                var body=new FlightBody(CraftPresets.LunarLander(catalog),catalog) {referenceBody=CelestialBody.Moon,launched=true,throttle=0};
                body.position=new DVector3(0,CelestialBody.Moon.radius+altitude,0);
                body.velocity=orbital?new DVector3(Math.Sqrt(CelestialBody.Moon.mu/body.position.Magnitude),0,0):CelestialBody.Moon.SurfaceVelocity(body.position)+new DVector3(0,-20,0);
                body.maxAltitude=altitude;return body;
            }
            Check("lunar gravity and vacuum",()=>
            {
                Require(Math.Abs(CelestialBody.Moon.SurfaceGravity-1.6249)<.001,"Lunar gravity incorrect");
                var body=Lander(5000,false);body.Step(.02);
                Require(body.dynamicPressure==0&&body.heatFlux==0&&body.Mach==0,"Moon has atmospheric forces");
                Require(!body.DeployParachutes(),"Parachute deploys in vacuum");
                return $"g={CelestialBody.Moon.SurfaceGravity:F5}; no drag, heating or parachute";
            });
            Check("lunar lander graph and feet clearance",()=>
            {
                var craft=CraftPresets.LunarLander(catalog);
                Require(craft.Validate(catalog).Count==0,"Invalid lander graph");
                var engine=craft.parts.Single(p=>catalog.Find(p.partId).kind==PartKind.VacuumEngine);
                var legs=craft.parts.Where(p=>catalog.Find(p.partId).kind==PartKind.LandingLeg).ToArray();
                Require(legs.Length==4&&legs.All(p=>p.position.y<engine.position.y),"Engine touches before landing feet");
                return "Four feet extend below engine nozzle";
            });
            Check("lunar powered landing from 5 km",()=>
            {
                var body=Lander(5000,false);var pilot=new LunarLandingProgram();pilot.Begin(false);
                for(int i=0;i<60000&&!body.landed&&!body.crashed;i++) {pilot.Update(body);body.Step(.02);}
                Require(body.landed&&!body.crashed,"Landing failed: "+body.lastEvent+" / "+body.GroundAltitude);
                Require(body.parts.All(p=>!p.destroyed)&&body.Fuel>0,"Landing damaged craft or exhausted fuel");
                Require(body.GroundAltitude<.001,"Feet do not contact surface");
                return $"{body.lastEvent}; t={body.elapsed:F2}s; fuel={body.Fuel:F1}kg";
            });
            Check("lunar orbit to surface",()=>
            {
                var body=Lander(50000,true);var pilot=new LunarLandingProgram();pilot.Begin(true);
                for(int i=0;i<400000&&!body.landed&&!body.crashed;i++) {pilot.Update(body);body.Step(.02);}
                Require(body.landed&&!body.crashed,"Orbital descent failed: "+body.lastEvent+" / altitude="+body.GroundAltitude+" / fuel="+body.Fuel);
                Require(body.parts.All(p=>!p.destroyed)&&body.Fuel>0,"Orbital landing damaged craft or exhausted fuel");
                return $"{body.lastEvent}; t={body.elapsed:F2}s; fuel={body.Fuel:F1}kg";
            });
            Check("lunar orbital integration",()=>
            {
                double radius=CelestialBody.Moon.radius+50000;
                var position=new DVector3(0,radius,0);var velocity=new DVector3(Math.Sqrt(CelestialBody.Moon.mu/radius),0,0);
                double period=OrbitalMechanics.Elements(position,velocity,CelestialBody.Moon.mu,CelestialBody.Moon.radius).period;
                for(double time=0;time<period*3;time+=2) OrbitalMechanics.Integrate(ref position,ref velocity,2,(p,v)=>CelestialBody.Moon.Gravity(p));
                double drift=Math.Abs(position.Magnitude-radius)/radius;
                Require(drift<.0001,"Lunar orbit drift");return $"Three orbits, relative radial drift={drift:E3}";
            });
            Check("rocket finish persists in blueprint",()=>
            {
                var craft=CraftPresets.LunarLander(catalog);var part=craft.parts[0];
                part.color=new Color(.2f,.4f,.7f);part.accentColor=Color.red;part.decalId="brp";part.decalText="БРП ЛУНА";part.decalAngle=67;part.decalScale=.8f;
                var copy=craft.Clone().parts[0];Require(copy.color==part.color&&copy.accentColor==part.accentColor&&copy.decalId==part.decalId&&copy.decalText==part.decalText&&copy.decalAngle==67&&Math.Abs(copy.decalScale-.8f)<.001,"Finish not preserved");
                return "Colors, emblem, text, angle and scale survive JSON clone";
            });
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/lunar-verification.json",JsonUtility.ToJson(report,true));
            Debug.Log($"BRP lunar checks: {report.passed} passed, {report.failed} failed");return report;
        }
    }
}
