$ErrorActionPreference='Stop'
Set-Location 'C:/Developing/My project'
function Invoke-BrpShot([string]$Code,[int]$Seconds,[string]$Label) {
    $response=unity command script eval --caller plugin --skill unity-cli --project-path 'C:/Developing/My project' --format json -- $Code --timeout 120 | ConvertFrom-Json
    if(-not $response.success -or -not $response.data.result.success) { throw ($response | ConvertTo-Json -Depth 8) }
    Write-Output $Label
    if($Seconds -gt 0) { Start-Sleep -Seconds $Seconds }
}
Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.ReturnToWorkshop();g.StartSandbox();g.LoadPreset(1);g.UI.ShowHome();BRP.Editor.ShowcaseRecorder.Start();return "home";' 5 'главное меню'
Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.UI.CloseDialog();g.UI.PopulateCatalog(BRP.PartCategory.Propulsion);g.Select(g.Assembly.views.First(v=>v.Definition.kind==BRP.PartKind.BoosterEngine));return "constructor";' 5 'каталог и паспорт двигателя'
Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.UI.ShowMissions();return "missions";' 4 'программа испытаний'
Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.UI.CloseDialog();g.StartFlight(false);g.ToggleAscent();return "launch";' 12 'запуск с космодрома'
Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.CycleTimeWarp();g.CycleTimeWarp();return "ascent x5";' 28 'выведение, ускорение времени x5'
Invoke-BrpShot 'BRP.LaboratoryGame.Instance.CycleTimeWarp();return "ascent x10";' 30 'вторая ступень и орбита'
Invoke-BrpShot 'BRP.LaboratoryGame.Instance.ToggleMap();return "map";' 7 'карта орбиты'
Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.ReleasePayload();g.ToggleMap();return "payload";' 6 'отделение спутника'
Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.StartOrbitalPractice();g.ToggleRendezvous();g.CycleTimeWarp();g.CycleTimeWarp();return "rendezvous";' 30 'сближение и стыковка'
Invoke-BrpShot 'var g=BRP.LaboratoryGame.Instance;g.ReturnToWorkshop();g.LoadPreset(2);g.StartFlight(false);g.CycleTimeWarp();g.CycleTimeWarp();return "rescue launch";' 5 'испытание системы спасения'
Invoke-BrpShot 'BRP.LaboratoryGame.Instance.Abort();return "abort";' 22 'отделение капсулы и парашютная посадка'
Invoke-BrpShot 'BRP.Editor.ShowcaseRecorder.Stop();return "video saved";' 0 'запись завершена'
