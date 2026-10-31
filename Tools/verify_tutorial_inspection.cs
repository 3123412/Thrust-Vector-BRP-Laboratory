var g=BRP.LaboratoryGame.Instance;
var d=UnityEngine.Object.FindAnyObjectByType<BRP.TutorialDirector>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var report=new BRP.Editor.VerificationSuite.Report {utc=System.DateTime.UtcNow.ToString("O")};
void Require(bool pass,string message){if(!pass)throw new System.InvalidOperationException(message);}
void Check(string name,System.Action test){try{test();report.checks.Add(new BRP.Editor.VerificationSuite.CheckResult{name=name,passed=true});report.passed++;}catch(System.Exception error){report.checks.Add(new BRP.Editor.VerificationSuite.CheckResult{name=name,details=error.GetBaseException().Message});report.failed++;}}
void UpdateDirector()=>typeof(BRP.TutorialDirector).GetMethod("Update",flags).Invoke(d,null);
g.UI.CloseDialog();g.Progress.tutorialSeen=true;g.StartMission(1);g.ClearCraft();UpdateDirector();d.Advance();d.Advance();
var cameraPosition=g.mainCamera.transform.position;var cameraRotation=g.mainCamera.transform.rotation;
Check("first module inspection is optional and preserves the craft",()=>{
    Require(!d.FocusPreview.IsOpen,"Inspection opened without request");d.ShowFocusPreview();
    Require(d.FocusPreview.IsOpen&&d.FocusDefinition.kind==BRP.PartKind.FirstStage,"Wrong first module");
    Require(g.Assembly.Blueprint.parts.Count==0,"Preview modified craft");
    Require(UnityEngine.Vector3.Distance(cameraPosition,g.mainCamera.transform.position)<.0001f&&UnityEngine.Quaternion.Angle(cameraRotation,g.mainCamera.transform.rotation)<.001f,"Preview moved player camera");
});
Check("inspection supports bounded zoom and manual assembly",()=>{
    var preview=d.FocusPreview;preview.ChangeZoom(10);Require(System.Math.Abs(preview.Zoom-2.6)<.001,"Zoom maximum");
    preview.ChangeZoom(-20);Require(System.Math.Abs(preview.Zoom-.65)<.001,"Zoom minimum");
    preview.Rotate(new UnityEngine.Vector2(50,-30));preview.ResetView();Require(System.Math.Abs(preview.Zoom-1)<.001,"View reset");
    var slider=(UnityEngine.UI.Slider)typeof(BRP.TutorialFocusPreview).GetField("assemblySlider",flags).GetValue(preview);
    slider.value=.3f;Require(System.Math.Abs(preview.AssemblyFraction-.3)<.001,"Manual assembly slider");preview.Close();
});
var root=g.Assembly.Spawn(g.Catalog.Find(BRP.PartKind.FirstStage));root.transform.localPosition=UnityEngine.Vector3.zero;
Require(g.Assembly.TryTarget(root,out var rootTarget),"No root target");g.Assembly.Attach(root,rootTarget);d.Advance();d.ShowFocusPreview();
Check("engine lesson shows the real mating module and insertion guide",()=>{
    Require(d.FocusDefinition.kind==BRP.PartKind.BoosterEngine&&d.FocusPreview.HasConnection,"No engine connection context");
    var stage=(UnityEngine.GameObject)typeof(BRP.TutorialFocusPreview).GetField("studio",flags).GetValue(d.FocusPreview);
    Require(stage.GetComponentsInChildren<UnityEngine.LineRenderer>().Length>=2,"Missing ring or approach direction");
    Require(stage.GetComponentsInChildren<UnityEngine.MeshRenderer>().Length>=6,"Mating model absent");
    var previewCamera=stage.GetComponentInChildren<UnityEngine.Camera>();
    Require(!previewCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().allowXRRendering,"Preview renders stereo a second time");
    Require(g.Assembly.Blueprint.parts.Count==1,"Inspection changed real assembly");
});
Check("catalog reveal selects the required category",()=>{
    d.RevealFocusInCatalog();Require(!d.FocusPreview.IsOpen,"Catalog action left inspection over catalog");
    Require(g.UI.GetComponentsInChildren<BRP.CatalogCard>().Any(card=>card.definition.kind==BRP.PartKind.BoosterEngine),"Engine card not revealed");
});
Check("VR inspection remains fixed and does not write tracked pose",()=>{
    var inspectedPanel=(UnityEngine.RectTransform)typeof(BRP.TutorialFocusPreview).GetField("panel",flags).GetValue(d.FocusPreview);
    var originalPosition=inspectedPanel.position;var originalRotation=inspectedPanel.rotation;var originalScale=inspectedPanel.localScale;
    typeof(BRP.LaboratoryGame).GetField("vrEnabled",flags).SetValue(g,true);
    try
    {
        d.ShowFocusPreview();var panel=(UnityEngine.RectTransform)typeof(BRP.TutorialFocusPreview).GetField("panel",flags).GetValue(d.FocusPreview);
        var panelPosition=panel.position;var panelRotation=panel.rotation;
        g.mainCamera.transform.rotation=cameraRotation*UnityEngine.Quaternion.Euler(0,25,0);
        var trackedRotation=g.mainCamera.transform.rotation;
        typeof(BRP.TutorialFocusPreview).GetMethod("Update",flags).Invoke(d.FocusPreview,null);
        Require(UnityEngine.Vector3.Distance(panelPosition,panel.position)<.0001f&&UnityEngine.Quaternion.Angle(panelRotation,panel.rotation)<.001f,"Panel follows every head turn");
        Require(UnityEngine.Quaternion.Angle(trackedRotation,g.mainCamera.transform.rotation)<.001f,"Preview rotated HMD");
    }
    finally {typeof(BRP.LaboratoryGame).GetField("vrEnabled",flags).SetValue(g,false);g.mainCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);d.FocusPreview.Close();inspectedPanel.SetPositionAndRotation(originalPosition,originalRotation);inspectedPanel.localScale=originalScale;}
});
Check("hidden inspection suspends its camera and lights",()=>{
    var studio=(UnityEngine.GameObject)typeof(BRP.TutorialFocusPreview).GetField("studio",flags).GetValue(d.FocusPreview);
    Require(!studio.activeInHierarchy,"Closed studio still renders");
});
d.Hide();g.StartSandbox();g.LoadPreset(1);g.UI.ShowHome();
System.IO.File.WriteAllText("Artifacts/tutorial-inspection-verification.json",UnityEngine.JsonUtility.ToJson(report,true));
return report;
