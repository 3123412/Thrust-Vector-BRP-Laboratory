using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BRP.Editor
{
    public static class FlightScenarioVerification
    {
        public static VerificationSuite.Report Run()
        {
            var report=new VerificationSuite.Report {utc=DateTime.UtcNow.ToString("O")};
            var catalog=Resources.Load<PartCatalog>("BRP/Catalog");
            void Require(bool pass,string message) {if(!pass) throw new InvalidOperationException(message);}
            void Check(string name,Func<string> action)
            {
                try {report.checks.Add(new VerificationSuite.CheckResult {name=name,passed=true,details=action()});report.passed++;}
                catch(Exception e) {report.checks.Add(new VerificationSuite.CheckResult {name=name,details=e.Message});report.failed++;}
            }
            FlightBody Ascend(int level)
            {
                var body=new FlightBody(CraftPresets.Create(catalog,level),catalog) {launched=true};
                var program=new AscentProgram {active=true};
                for(int i=0;i<50000&&program.active&&!body.crashed&&!body.landed;i++) {program.Update(body,.02);body.Step(.02);}
                Require(!program.active&&!body.crashed&&!body.landed,"Ascent did not finish");
                Require(body.Orbit.periapsis>=120000&&body.Orbit.apoapsis<=250000,"Orbit outside mission limits");
                Require(body.parts.All(p=>!p.destroyed),"Ascent damaged parts");
                return body;
            }
            Check("orbital launcher reaches orbit",()=>
            {
                var body=Ascend(1);return $"{body.elapsed:F2}s / {body.Orbit.periapsis:F0}–{body.Orbit.apoapsis:F0}m";
            });
            Check("complete launch, satellite and capsule recovery",()=>
            {
                var body=Ascend(2);var satellite=body.Separate(true);
                Require(satellite!=null&&satellite.Has(PartKind.Satellite),"Satellite separation failed");
                for(int i=0;i<9000;i++) {body.Step(.02);satellite.Step(.02);}
                Require(satellite.telemetryTime>=179.99,"Satellite lost power or radio");
                var recovery=new RecoveryProgram {active=true};
                for(int i=0;i<240000&&!body.landed&&!body.crashed;i++) {recovery.Update(body);body.Step(.02);}
                Require(body.landed&&!body.crashed,"Capsule did not land");
                Require(body.Has(PartKind.Capsule)&&body.Has(PartKind.FlightRecorder)&&body.Has(PartKind.HeatShield),"Recovery equipment lost");
                Require(body.parts.All(p=>!p.destroyed),"Recovery damaged parts");
                return $"{body.lastEvent}; satellite telemetry {satellite.telemetryTime:F1}s; flight {body.elapsed:F1}s";
            });
            Check("emergency tower recovers a capsule",()=>
            {
                var body=new FlightBody(CraftPresets.Create(catalog,2),catalog) {launched=true};
                for(int i=0;i<2000;i++) body.Step(.02);
                body.Separate(true);var carrier=body.SeparateCapsule(true);
                Require(carrier!=null&&body.Has(PartKind.EscapeTower)&&body.Has(PartKind.FlightRecorder),"Wrong escape subtree");
                var recovery=new RecoveryProgram {active=true};
                for(int i=0;i<60000&&!body.landed&&!body.crashed;i++) {recovery.Update(body);body.Step(.02);}
                Require(body.aborted&&body.landed&&body.mainChuteDeployed,"Escape landing failed");
                return body.lastEvent;
            });
            Check("RCS rendezvous and port docking",()=>
            {
                double radius=OrbitalMechanics.Radius+180000;
                var a=new FlightBody(CraftPresets.OrbitalVehicle(catalog),catalog) {launched=true,throttle=0,position=new DVector3(0,radius,0),velocity=new DVector3(Math.Sqrt(OrbitalMechanics.Mu/radius),0,0),guidance=GuidanceMode.Manual};
                var b=new FlightBody(CraftPresets.OrbitalVehicle(catalog),catalog) {launched=true,throttle=0,position=a.position+new DVector3(0,80,0),velocity=a.velocity,orientation=new DQuaternion(Quaternion.Euler(0,0,180)),guidance=GuidanceMode.Manual};
                var program=new RendezvousProgram {active=true};
                for(int i=0;i<20000;i++)
                {
                    if(program.Update(a,b)) {a.Dock(b);break;}
                    a.Step(.02);b.Step(.02);
                }
                Require(a.docked,"Ports did not meet");return $"Docked at {a.elapsed:F2}s";
            });
            Check("closed valve stops liquid fuel",()=>
            {
                var body=new FlightBody(CraftPresets.Create(catalog),catalog) {launched=true,valveOpen=false};
                body.Step(.02);Require(body.lastThrust==0&&body.totalFuelUsed==0,"Valve did not isolate liquid engine");return "No thrust or liquid fuel consumption";
            });
            Check("solid charge continues after throttle cutoff",()=>
            {
                var craft=CraftPresets.Create(catalog);var root=craft.parts[0];
                craft.parts.Add(new PlacedPart {partId=catalog.Find(PartKind.SolidBooster).id,parentId=root.instanceId,port=10,position=new Vector3(1,0,0)});
                var body=new FlightBody(craft,catalog) {launched=true,throttle=0};body.Step(.02);
                Require(body.lastThrust>0&&body.totalFuelUsed>0,"Solid charge incorrectly obeys throttle");return "Solid remains lit at zero throttle";
            });
            Check("solar generation needs a distribution bus",()=>
            {
                var a=new FlightBody(CraftPresets.OrbitalVehicle(catalog),catalog) {launched=true,throttle=0};
                var b=new FlightBody(CraftPresets.OrbitalVehicle(catalog),catalog) {launched=true,throttle=0};
                b.parts.RemoveAll(p=>p.definition.kind==PartKind.PowerDistributor);
                foreach(var p in a.parts.Concat(b.parts)) p.energy*=.5;
                double beforeA=a.Energy,beforeB=b.Energy;a.Step(.1);b.Step(.1);
                Require(a.Energy>beforeA&&b.Energy<beforeB,"Power distribution has no effect");return "Bus connects solar power to battery";
            });
            Check("occupied mount and non-finite data rejected",()=>
            {
                var craft=CraftPresets.Create(catalog);craft.parts[2].parentId=craft.parts[1].parentId;craft.parts[2].port=craft.parts[1].port;
                Require(craft.Validate(catalog).Any(e=>e.Contains("занят")),"Duplicate mount accepted");
                craft=CraftPresets.Create(catalog);craft.parts[0].position=new Vector3(float.NaN,0,0);
                Require(craft.Validate(catalog).Any(e=>e.Contains("положение")),"NaN accepted");return "Both invalid inputs rejected";
            });
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/flight-verification.json",JsonUtility.ToJson(report,true));
            Debug.Log($"BRP flight scenarios: {report.passed} passed, {report.failed} failed");return report;
        }
    }
}
