using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BRP.Editor
{
    public static class CampaignAcceptance
    {
        static VerificationSuite.Report report;
        static string progress;
        static readonly Dictionary<string,byte[]> files=new Dictionary<string,byte[]>();
        public static void Begin()
        {
            var game=LaboratoryGame.Instance;if(game==null) throw new InvalidOperationException("Enter Play Mode");
            progress=JsonUtility.ToJson(game.Progress);report=new VerificationSuite.Report {utc=DateTime.UtcNow.ToString("O")};files.Clear();
            foreach(string name in new[]{"progress.json","progress.json.bak"})
            {
                string path=Path.Combine(SaveStore.Folder,name);files[path]=File.Exists(path)?File.ReadAllBytes(path):null;
            }
        }
        public static VerificationSuite.CheckResult RunOne(int id)
        {
            var game=LaboratoryGame.Instance;
            var result=new VerificationSuite.CheckResult {name=$"mission {id:00} / {MissionDefinition.All[id-1].title}"};
            void Require(bool pass,string message) {if(!pass) throw new InvalidOperationException(message);}
            void Until(Func<bool> condition,double limit=1200)
            {
                for(int i=0;i<(int)(limit/.02)&&!condition();i++)
                {
                    game.StepSimulation(.02);
                    if(game.Flight.crashed) throw new InvalidOperationException(game.Flight.lastEvent);
                }
                Require(condition(),"Scenario deadline reached");
            }
            PartView Place(PartKind kind,Vector3 position)
            {
                var view=game.Assembly.Spawn(game.Catalog.Find(kind));view.transform.localPosition=position;view.transform.localRotation=Quaternion.identity;
                Require(game.Assembly.TryTarget(view,out var target),"No mount for "+kind);game.Assembly.Attach(view,target);return view;
            }
            try
            {
                game.Progress.completed.Remove(id);game.UI.CloseDialog();game.StartMission(id);
                if(id==1)
                {
                    game.ClearCraft();Place(PartKind.FirstStage,Vector3.zero);
                    var engine=Place(PartKind.BoosterEngine,new Vector3(0,-1.1f,0));Place(PartKind.Fairing,new Vector3(0,1.8f,0));
                    var group=game.Assembly.Detach(engine);engine.transform.localPosition=new Vector3(0,-1.1f,0);
                    Require(game.Assembly.TryTarget(engine,out var target),"Cannot return detached engine");game.Assembly.Attach(engine,target,group);
                }
                else if(id==2)
                {
                    game.LoadPreset(1);var adapter=game.Assembly.Blueprint.parts.First(p=>game.Catalog.Find(p.partId).kind==PartKind.Adapter);
                    var definition=game.Catalog.Find(adapter.partId);Place(PartKind.Window,adapter.position+new Vector3(definition.radius,.15f+definition.height*.25f,0));
                }
                else if(id==3) game.LoadPreset(0);
                else if(id==18||id==19)
                {
                    game.StartOrbitalPractice();game.ToggleRendezvous();
                    if(id==18) Until(()=>(game.Flight.position-game.Target.position).Magnitude<50&&(game.Flight.velocity-game.Target.velocity).Magnitude<1,400);
                    else Until(()=>game.Flight.docked,400);
                }
                else
                {
                    int preset=id<=8?0:id==13||id==14||id==15||id==20?2:1;
                    game.LoadPreset(preset);game.StartFlight(id==4||id==5);
                    Require(game.IsFlying,"Launch rejected");
                    if(id==4||id==5)
                    {
                        Until(()=>game.Flight.poweredTime>=10,30);game.Flight.throttle=0;game.StepSimulation(.02);
                    }
                    else if(id==14||id==15)
                    {
                        Until(()=>game.Flight.elapsed>=40,50);
                        if(id==14) game.Abort();else {game.ReleasePayload();game.SeparateCapsule();game.PlanDeorbit();}
                        Until(()=>id==14?game.Flight.chuteDeployed:game.Flight.landed,1800);
                    }
                    else if(id<=8)
                    {
                        double height=id==6?1000:id==7?5000:3000;Until(()=>game.Flight.Altitude>=height,600);
                    }
                    else
                    {
                        game.ToggleAscent();
                        if(id==9) Until(()=>game.Flight.stageEvent&&game.Flight.Altitude>=20000);
                        if(id==10) {Until(()=>game.Flight.stage>=1&&game.Flight.Altitude>=50000);game.ReleasePayload();}
                        if(id==11) Until(()=>game.Flight.Altitude>=100000);
                        if(id==13) Until(()=>game.Flight.Altitude>=80000);
                        if(id==12||id==16||id==17||id==20)
                        {
                            Until(()=>!game.Ascent.active);
                            if(id==12) {double end=game.Flight.elapsed+180.1;Until(()=>game.Flight.elapsed>=end,200);}
                            if(id==17||id==20)
                            {
                                game.ReleasePayload();Require(game.Payload!=null,"Payload release failed");
                                Until(()=>game.Payload.telemetryTime>=180,200);
                            }
                            if(id==20) {game.PlanDeorbit();Until(()=>game.Flight.landed,4800);}
                        }
                    }
                }
                typeof(LaboratoryGame).GetMethod("CheckMission",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
                Require(game.Progress.completed.Contains(id),"Mission criteria did not accept the scenario");
                result.passed=true;result.details=game.Flight==null?"Assembly accepted":$"Flight {game.Flight.elapsed:F2}s; {game.Flight.lastEvent}";report.passed++;
            }
            catch(Exception error) {result.details=error.GetBaseException().Message;report.failed++;}
            report.checks.Add(result);File.WriteAllText("Artifacts/campaign-verification.json",JsonUtility.ToJson(report,true));return result;
        }
        public static VerificationSuite.Report Finish()
        {
            var game=LaboratoryGame.Instance;game.ReturnToWorkshop();game.UI.CloseDialog();game.StartSandbox();game.LoadPreset(1);
            JsonUtility.FromJsonOverwrite(progress,game.Progress);
            foreach(var file in files) {if(file.Value==null) {if(File.Exists(file.Key)) File.Delete(file.Key);}else File.WriteAllBytes(file.Key,file.Value);}
            game.UI.ShowHome();return report;
        }
    }
}
