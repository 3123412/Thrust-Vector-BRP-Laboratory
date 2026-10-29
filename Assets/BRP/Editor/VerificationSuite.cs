using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BRP.Editor
{
    public static class VerificationSuite
    {
        [Serializable] public sealed class CheckResult {public string name;public bool passed;public string details;}
        [Serializable] public sealed class Report {public string utc;public int passed,failed;public List<CheckResult> checks=new List<CheckResult>();}

        public static Report Run()
        {
            var report=new Report {utc=DateTime.UtcNow.ToString("O")};
            var catalog=Resources.Load<PartCatalog>("BRP/Catalog");
            void Check(string name,Action test)
            {
                try {test();report.checks.Add(new CheckResult{name=name,passed=true});report.passed++;}
                catch(Exception e) {report.checks.Add(new CheckResult{name=name,passed=false,details=e.Message});report.failed++;}
            }
            void Require(bool condition,string message) {if(!condition) throw new InvalidOperationException(message);}
            Check("catalog has 50 unique functional definitions",()=>Require(catalog.parts.Length==50&&catalog.parts.Select(p=>p.kind).Distinct().Count()==50,"Definition count"));
            foreach(var d in catalog.parts)
            {
                Check("model / "+d.id,()=>
                {
                    Require(d.model!=null,"Missing model");Require(d.dryMass>0&&d.height>0,"Invalid dimensions or mass");
                    Require(d.model.GetComponentsInChildren<MeshFilter>().Length>0,"Empty model");
                    Require(d.model.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.vertexCount>0),"Empty mesh");
                });
            }
            Check("Earth gravity at surface",()=>Require(Math.Abs(OrbitalMechanics.Gravity(new DVector3(0,OrbitalMechanics.Radius,0)).Magnitude-9.82)<.03,"Gravity"));
            Check("atmospheric profile decreases",()=>Require(OrbitalMechanics.Density(0)>OrbitalMechanics.Density(11000)&&OrbitalMechanics.Density(11000)>OrbitalMechanics.Density(25000)&&OrbitalMechanics.Density(170000)==0,"Density"));
            Check("circular orbit ten revolutions",()=>
            {
                double radius=OrbitalMechanics.Radius+180000;
                var r=new DVector3(radius,0,0);var v=new DVector3(0,Math.Sqrt(OrbitalMechanics.Mu/radius),0);
                var before=OrbitalMechanics.Elements(r,v);
                int steps=(int)Math.Ceiling(before.period*10/2);
                double dt=before.period*10/steps;
                for(int i=0;i<steps;i++) OrbitalMechanics.Integrate(ref r,ref v,dt,(p,s)=>OrbitalMechanics.Gravity(p));
                var after=OrbitalMechanics.Elements(r,v);
                Require(Math.Abs(after.semiMajorAxis-before.semiMajorAxis)/before.semiMajorAxis<.001,"Orbit drift");
            });
            Check("quaternion rotates and reverses",()=>
            {
                var q=new DQuaternion(Quaternion.Euler(37,23,71));var v=new DVector3(2,3,5);
                Require((q.Inverse.Rotate(q.Rotate(v))-v).Magnitude<1e-5,"Quaternion inverse");
                for(int i=0;i<10000;i++) q.Integrate(new DVector3(.1,.3,.2),.02);
                Require(Math.Abs(q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w-1)<1e-10,"Quaternion drift");
            });
            for(int i=0;i<3;i++)
            {
                int level=i;
                Check("preset graph / "+level,()=>Require(CraftPresets.Create(catalog,level).Validate(catalog).Count==0,"Invalid graph"));
            }
            Check("mass and resources are conserved",()=>
            {
                var body=new FlightBody(CraftPresets.Create(catalog),catalog) {launched=true};
                double initial=body.mass;
                for(int i=0;i<500;i++) body.Step(.02);
                Require(body.totalFuelUsed>0,"No fuel consumed");
                Require(Math.Abs(initial-body.mass-body.totalFuelUsed)<.1,"Mass accounting");
                Require(body.parts.All(p=>p.fuel>=0&&p.oxidizer>=0&&p.energy>=0),"Negative resource");
            });
            Check("two-stage separation conserves linear momentum",()=>
            {
                var body=new FlightBody(CraftPresets.Create(catalog,1),catalog);
                body.velocity=new DVector3(800,500,25);var momentum=body.velocity*body.mass;double mass=body.mass;
                var other=body.Separate();Require(other!=null,"No separation");
                Require(Math.Abs(body.mass+other.mass-mass)<.001,"Separation mass");
                Require((body.velocity*body.mass+other.velocity*other.mass-momentum).Magnitude<.01,"Separation momentum");
            });
            Check("docking conserves linear momentum",()=>
            {
                var a=new FlightBody(CraftPresets.Create(catalog,2),catalog);
                var b=new FlightBody(CraftPresets.Create(catalog,2),catalog);
                a.velocity=new DVector3(7600,2,0);b.velocity=new DVector3(7600,1.9,0);b.position+=new DVector3(1,0,0);
                var momentum=a.velocity*a.mass+b.velocity*b.mass;double mass=a.mass+b.mass;
                a.Dock(b);Require(Math.Abs(a.mass-mass)<.001,"Dock mass");Require((a.velocity*a.mass-momentum).Magnitude<.01,"Dock momentum");
            });
            Check("integration converges for powered ascent",()=>
            {
                FlightBody Run(double dt)
                {
                    var body=new FlightBody(CraftPresets.Create(catalog),catalog) {launched=true};
                    for(int i=0;i<(int)(20/dt);i++) body.Step(dt);return body;
                }
                var a=Run(.02);var b=Run(.01);
                Require(a.Altitude>100,"Rocket did not ascend");
                Require(Math.Abs(a.Altitude-b.Altitude)/Math.Max(1,b.Altitude)<.01,"Step convergence");
            });
            Check("blueprint JSON round trip",()=>
            {
                var craft=CraftPresets.Create(catalog,2);var copy=craft.Clone();
                Require(copy.parts.Count==craft.parts.Count,"Part loss");Require(copy.Validate(catalog).Count==0,"Broken graph");
            });
            Check("invalid graph is rejected",()=>
            {
                var craft=CraftPresets.Create(catalog);craft.parts[0].parentId=craft.parts[0].instanceId;
                Require(craft.Validate(catalog).Any(e=>e.Contains("Цикл")),"Cycle accepted");
            });
            Check("mission identifiers are complete",()=>Require(MissionDefinition.All.Select(m=>m.id).SequenceEqual(Enumerable.Range(1,20)),"Missing mission"));
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/verification.json",JsonUtility.ToJson(report,true));
            Debug.Log($"BRP verification: {report.passed} passed, {report.failed} failed");return report;
        }
    }
}
