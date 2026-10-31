var g=BRP.LaboratoryGame.Instance;
var report=new BRP.Editor.VerificationSuite.Report {utc=System.DateTime.UtcNow.ToString("O")};
void Require(bool pass,string message){if(!pass)throw new System.InvalidOperationException(message);}
void Check(string name,System.Action test){try{test();report.checks.Add(new BRP.Editor.VerificationSuite.CheckResult{name=name,passed=true});report.passed++;}catch(System.Exception error){report.checks.Add(new BRP.Editor.VerificationSuite.CheckResult{name=name,details=error.GetBaseException().Message});report.failed++;}}
UnityEngine.UI.Button Button(string text)=>g.UI.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text==text);
g.StartSandbox();g.UI.CloseDialog();g.LoadPreset(1);
var selected=g.Assembly.views.First(v=>v.Definition.kind==BRP.PartKind.FirstStage);
string id=selected.Placement.instanceId;
string oldDecal=selected.Placement.decalId;
Check("custom finish applies and survives undo redo",()=>{
    g.Select(selected);g.UI.ShowPaint();
    g.UI.GetComponentsInChildren<UnityEngine.UI.Slider>()[0].value=.2f;
    UnityEngine.Canvas.ForceUpdateCanvases();
    foreach(var slider in g.UI.GetComponentsInChildren<UnityEngine.UI.Slider>())
    {
        Require(slider.fillRect.rect.width<=((UnityEngine.RectTransform)slider.transform).rect.width+.1f,"Slider fill exceeds its bounds");
        Require(slider.handleRect.rect.height<=((UnityEngine.RectTransform)slider.transform).rect.height+.1f,"Slider handle exceeds its bounds");
    }
    Button("БРП").onClick.Invoke();Button("Металлик").onClick.Invoke();
    g.UI.GetComponentInChildren<TMPro.TMP_InputField>().text="БРП ЛУНА";
    Button("Сохранить облик").onClick.Invoke();
    var part=g.Assembly.Blueprint.parts.Single(p=>p.instanceId==id);
    Require(part.decalId=="brp"&&part.decalText=="БРП ЛУНА"&&System.Math.Abs(part.color.r-.2f)<.001,"Finish not applied through UI");
    g.Assembly.Undo();Require(g.Assembly.Blueprint.parts.Single(p=>p.instanceId==id).decalId==oldDecal,"Undo did not restore finish");
    g.Assembly.Redo();Require(g.Assembly.Blueprint.parts.Single(p=>p.instanceId==id).decalId=="brp","Redo lost finish");
});
Check("finish preview cancel preserves blueprint",()=>{
    var view=g.Assembly.views.Single(v=>v.Placement.instanceId==id);g.Select(view);g.UI.ShowPaint();
    g.UI.GetComponentsInChildren<UnityEngine.UI.Slider>()[0].value=.85f;g.UI.CloseDialog();
    Require(System.Math.Abs(g.Assembly.Blueprint.parts.Single(p=>p.instanceId==id).color.r-.2f)<.001,"Cancel changed stored color");
});
g.UI.CloseDialog();
Check("automatic VR has no manual menu button",()=>Require(!g.UI.GetComponentsInChildren<BRP.LabButton>(true).Any(b=>b.command=="vr"),"Manual VR button remains"));
g.StartFlight(false);g.Flight.throttle=0;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var presentation=(BRP.FlightPresentation)typeof(BRP.LaboratoryGame).GetField("presentation",flags).GetValue(g);
Check("flight hides workshop navigation",()=>Require(!g.UI.transform.Find("Navigation").gameObject.activeSelf,"Workshop header remains active"));
Check("smoke uses a collision plane and floating origin anchor",()=>{
    var exhaust=presentation.GetComponentsInChildren<BRP.EngineVfx>().First();
    Require(exhaust.smoke.collision.enabled&&exhaust.smoke.collision.GetPlane(0)!=null,"No smoke collision plane");
    Require(exhaust.smoke.main.simulationSpace==UnityEngine.ParticleSystemSimulationSpace.Custom&&exhaust.smoke.main.customSimulationSpace!=null,"Smoke anchor missing");
});
g.Flight.position=new BRP.DVector3(0,BRP.OrbitalMechanics.Radius+180000,0);g.Flight.velocity=new BRP.DVector3(System.Math.Sqrt(BRP.OrbitalMechanics.Mu/g.Flight.position.Magnitude),0,0);g.Flight.maxAltitude=180000;
Check("Earth survives system map round trip",()=>{
    g.FocusMapBody(2);presentation.Refresh(g.Flight,null);g.ToggleMap();presentation.Refresh(g.Flight,null);
    var planet=(UnityEngine.GameObject)typeof(BRP.FlightPresentation).GetField("planet",flags).GetValue(presentation);
    Require(planet.activeSelf&&presentation.viewCamera.farClipPlane>300000,"Earth hidden or clipped after map");
});
Check("space hides atmospheric controls",()=>{
    g.UI.UpdateFlight(g.Flight,0,0);
    var panel=g.UI.GetComponentsInChildren<UnityEngine.RectTransform>(true).Single(t=>t.name=="Atmospheric Flight");
    Require(!panel.gameObject.activeSelf,"Atmosphere panel remains active in space");
});
Check("immersive presentation preserves tracked camera pose",()=>{
    var position=presentation.viewCamera.transform.position;var rotation=presentation.viewCamera.transform.rotation;
    presentation.immersiveVR=true;g.Flight.orientation=new BRP.DQuaternion(UnityEngine.Quaternion.Euler(45,70,20));presentation.Refresh(g.Flight,null);
    Require(UnityEngine.Vector3.Distance(position,presentation.viewCamera.transform.position)<.0001f&&UnityEngine.Quaternion.Angle(rotation,presentation.viewCamera.transform.rotation)<.001,"Flight rotates or moves observer camera");
    presentation.immersiveVR=false;
});
g.ReturnToWorkshop();g.LoadPreset(3);
var lander=g.Assembly.views.First(v=>v.Definition.kind==BRP.PartKind.SecondStage);g.Assembly.Paint(lander,UnityEngine.Color.red);lander.Placement.decalId="brp";
Check("lunar scenario preserves custom lander finish",()=>{
    g.StartLunarPractice();Require(g.Flight.IsLunar&&g.Flight.parts.Any(p=>p.definition.kind==BRP.PartKind.SecondStage&&p.placement.color==UnityEngine.Color.red&&p.placement.decalId=="brp"),"Lunar scenario replaced customized craft");
});
Check("lunar landing and manual relaunch",()=>{
    for(int i=0;i<60000&&!g.Flight.landed&&!g.Flight.crashed;i++)g.StepSimulation(.02);
    Require(g.Flight.landed&&!g.Flight.crashed,"Lander did not land");
    for(int i=0;i<4;i++)g.AdjustThrottle(1);
    for(int i=0;i<1000&&!g.Flight.crashed;i++)g.StepSimulation(.02);
    Require(!g.Flight.landed&&!g.Flight.crashed&&g.Flight.GroundAltitude>100,"Lander cannot lift off after touchdown");
});
g.ReturnToWorkshop();g.LoadPreset(1);g.UI.ShowHome();
System.IO.File.WriteAllText("Artifacts/presentation-verification.json",UnityEngine.JsonUtility.ToJson(report,true));
return report;
