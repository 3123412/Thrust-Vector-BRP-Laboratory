$ErrorActionPreference='Stop'
Set-Location 'C:/Developing/My project'
function Invoke-BrpShot([string]$Code,[int]$Seconds,[string]$Label) {
    $codePath=Join-Path (Get-Location) 'Temp/showcase-command.cs'
    [System.IO.File]::WriteAllText($codePath,$Code,[System.Text.UTF8Encoding]::new($false))
    $response=unity command script eval_file --caller plugin --skill unity-cli --project-path 'C:/Developing/My project' --format json --timeout 120 -- $codePath | ConvertFrom-Json
    if(-not $response.success -or -not $response.data.result.success) { throw ($response | ConvertTo-Json -Depth 8) }
    Write-Output $Label
    if($Seconds -gt 0) { Start-Sleep -Seconds $Seconds }
}
try {
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;BRP.Editor.CampaignAcceptance.Begin();g.StartSandbox();g.LoadPreset(1);g.UI.ShowHome();BRP.Editor.ShowcaseRecorder.Start();return "home";' 4 'главное меню'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.Progress.tutorialSeen=false;g.UI.CloseDialog();g.StartMission(1);return "instructor";' 17 'инструктор и мужская озвучка'
    Invoke-BrpShot 'UnityEngine.Object.FindAnyObjectByType<BRP.TutorialDirector>().Advance();return "controls";' 20 'обучение управлению'
    Invoke-BrpShot 'UnityEngine.Object.FindAnyObjectByType<BRP.TutorialDirector>().Advance();return "bench";' 8 'подготовка верстака'
    Invoke-BrpShot 'BRP.LaboratoryGame.Instance.ClearCraft();return "mount guidance";' 12 'подсветка детали и крепления'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;var v=g.Assembly.Spawn(g.Catalog.Find(BRP.PartKind.FirstStage));v.transform.localPosition=UnityEngine.Vector3.zero;if(g.Assembly.TryTarget(v,out var target))g.Assembly.Attach(v,target);return "first module";' 5 'установка первого модуля'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.StartSandbox();g.LoadPreset(1);g.UI.CloseDialog();g.Select(g.Assembly.views.First(v=>v.Definition.kind==BRP.PartKind.FirstStage));g.UI.ShowPaint();return "customization";' 4 'мастерская облика'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.UI.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text=="БРП").onClick.Invoke();g.UI.GetComponentInChildren<TMPro.TMP_InputField>().text="ВЕКТОР-01";return "markings";' 5 'наклейки и маркировки'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.UI.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text=="Сохранить облик").onClick.Invoke();g.StartFlight(false);g.ToggleAscent();return "launch";' 12 'запуск, орошение и выхлоп'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.CycleTimeWarp();g.CycleTimeWarp();return "ascent x5";' 28 'выведение, ускорение времени x5'
    Invoke-BrpShot 'BRP.LaboratoryGame.Instance.CycleTimeWarp();return "ascent x10";' 30 'вторая ступень и орбита'
    Invoke-BrpShot 'BRP.LaboratoryGame.Instance.ToggleMap();return "map";' 5 'карта орбиты'
    Invoke-BrpShot 'BRP.LaboratoryGame.Instance.FocusMapBody(1);return "earth moon";' 5 'система Земля — Луна'
    Invoke-BrpShot 'BRP.LaboratoryGame.Instance.FocusMapBody(2);return "solar system";' 7 'карта планет'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.ToggleMap();g.ReleasePayload();return "payload";' 5 'отделение спутника'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.StartLunarPractice();g.CycleTimeWarp();g.CycleTimeWarp();return "lunar descent";' 27 'посадка на Луну'
    Invoke-BrpShot 'BRP.LaboratoryGame.Instance.UI.CloseDialog();return "lunar surface";' 5 'лунная поверхность'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;for(int i=0;i<4;i++)g.AdjustThrottle(1);return "lunar liftoff";' 5 'повторный взлёт'
    Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.ReturnToWorkshop();g.LoadPreset(1);g.UI.ShowHome();return "home";' 3 'лаборатория'
}
finally {
    Invoke-BrpShot 'if(BRP.Editor.ShowcaseRecorder.Recording)BRP.Editor.ShowcaseRecorder.Stop();BRP.Editor.CampaignAcceptance.Finish();return "video saved";' 0 'запись завершена'
}
