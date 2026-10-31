var g=BRP.LaboratoryGame.Instance;
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var report=new BRP.Editor.VerificationSuite.Report {utc=System.DateTime.UtcNow.ToString("O")};
void Require(bool pass,string message){if(!pass)throw new System.InvalidOperationException(message);}
void Check(string name,System.Action test){try{test();report.checks.Add(new BRP.Editor.VerificationSuite.CheckResult{name=name,passed=true});report.passed++;}catch(System.Exception error){report.checks.Add(new BRP.Editor.VerificationSuite.CheckResult{name=name,details=error.GetBaseException().Message});report.failed++;}}
var rig=(UnityEngine.GameObject)typeof(BRP.LaboratoryGame).GetField("xrRig",flags).GetValue(g);
var camera=rig.GetComponentInChildren<UnityEngine.Camera>(true);
var desktop=g.mainCamera;
var position=rig.transform.position;var rotation=rig.transform.rotation;
var headPosition=camera.transform.localPosition;var headRotation=camera.transform.localRotation;
var rootPosition=g.UI.root.position;var rootRotation=g.UI.root.rotation;var rootScale=g.UI.root.localScale;
var renderMode=g.UI.canvas.renderMode;
g.StartSandbox();g.UI.CloseDialog();g.LoadPreset(1);
var assemblyPosition=g.Assembly.assemblyRoot.position;
try
{
    typeof(BRP.LaboratoryGame).GetField("vrEnabled",flags).SetValue(g,true);g.mainCamera=camera;
    Check("seated workspace uses explicit recenter without moving craft",()=>{
        camera.transform.SetPositionAndRotation(new UnityEngine.Vector3(2,1.1f,-4),UnityEngine.Quaternion.Euler(12,63,4));
        var localRotation=camera.transform.localRotation;
        g.CenterVRWorkspace();
        Require(UnityEngine.Vector3.Distance(camera.transform.position,new UnityEngine.Vector3(0,1.65f,-2.4f))<.001f,"Eye position not centered");
        Require(UnityEngine.Quaternion.Angle(localRotation,camera.transform.localRotation)<.001f,"Tracked head local rotation changed");
        Require(UnityEngine.Vector3.Distance(assemblyPosition,g.Assembly.assemblyRoot.position)<.001f,"Recenter moved rocket through bench");
    });
    Check("worldspace panels remain positioned through observer round trip",()=>{
        g.UI.ConfigureVR(camera);var before=g.UI.root.position;
        g.UI.SetVRObserver(new UnityEngine.Vector3(6,1.2f,-10),UnityEngine.Quaternion.Euler(0,-31,0));
        g.UI.SetVRObserver(new UnityEngine.Vector3(0,0,-2.4f),UnityEngine.Quaternion.identity);
        Require(UnityEngine.Vector3.Distance(before,g.UI.root.position)<.001f,"Observer return displaced panels");
        Require(g.UI.canvas.renderMode==UnityEngine.RenderMode.WorldSpace&&g.UI.canvas.worldCamera==camera,"VR canvas lost camera");
    });
    g.StartFlight(false);
    var controls=UnityEngine.Object.FindAnyObjectByType<BRP.VROperatorControls>();
    Check("focus loss clears orientation and translation input",()=>{
        g.Flight.manualInput=new BRP.DVector3(1,1,1);g.Flight.translationInput=new BRP.DVector3(1,1,1);
        typeof(BRP.VROperatorControls).GetMethod("OnApplicationFocus",flags).Invoke(controls,new object[]{false});
        Require(g.Flight.manualInput.Magnitude==0&&g.Flight.translationInput.Magnitude==0,"Stale controls after focus loss");
        typeof(BRP.VROperatorControls).GetMethod("OnApplicationFocus",flags).Invoke(controls,new object[]{true});
    });
    Check("modal panels clear stale flight input",()=>{
        g.UI.ShowSettings();g.Flight.manualInput=new BRP.DVector3(1,0,0);g.Flight.translationInput=new BRP.DVector3(0,1,0);
        typeof(BRP.VROperatorControls).GetMethod("Update",flags).Invoke(controls,null);
        Require(g.Flight.manualInput.Magnitude==0&&g.Flight.translationInput.Magnitude==0,"Modal retains manual input");g.UI.CloseDialog();
    });
    Check("return to workshop keeps chosen recenter",()=>{
        var chosen=(UnityEngine.Vector3)typeof(BRP.LaboratoryGame).GetField("workshopRigPosition",flags).GetValue(g);
        g.ReturnToWorkshop();Require(UnityEngine.Vector3.Distance(rig.transform.position,chosen)<.001f,"Lost seated recenter");
    });
}
finally
{
    g.ReturnToWorkshop();typeof(BRP.LaboratoryGame).GetField("vrEnabled",flags).SetValue(g,false);g.mainCamera=desktop;
    rig.transform.SetPositionAndRotation(position,rotation);camera.transform.localPosition=headPosition;camera.transform.localRotation=headRotation;
    g.UI.canvas.renderMode=renderMode;g.UI.canvas.worldCamera=desktop;g.UI.root.SetPositionAndRotation(rootPosition,rootRotation);g.UI.root.localScale=rootScale;
    g.UI.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled=true;
}
System.IO.File.WriteAllText("Artifacts/vr-workspace-verification.json",UnityEngine.JsonUtility.ToJson(report,true));
return report;
