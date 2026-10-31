using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BRP
{
    public sealed class LabButton : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler
    {
        public string command;
        public int argument;
        public Image accent;
        float target;
        void OnEnable()
        {
            var button=GetComponent<UnityEngine.UI.Button>();
            button.onClick.RemoveListener(Execute);button.onClick.AddListener(Execute);
        }
        public void Execute()
        {
            var g=LaboratoryGame.Instance;if(g==null) return;
            switch(command)
            {
                case "missions":g.UI.ShowMissions();break;
                case "blueprints":g.UI.ShowBlueprints();break;
                case "records":g.UI.ShowRecords();break;
                case "help":g.UI.ShowHelp();break;
                case "settings":g.UI.ShowSettings();break;
                case "home":g.UI.ShowHome();break;
                case "lunar_menu":g.StartSandbox();g.UI.ShowLunarExpedition();break;
                case "flight_panels":g.UI.ToggleFlightNavigation();break;
                case "map_body":g.FocusMapBody(argument);break;
                case "map_cycle":g.CycleMapView();break;
                case "lunar_expedition":g.UI.ShowLunarExpedition();break;
                case "lunar_practice":g.StartLunarPractice();break;
                case "quit":SaveStore.Save(g.Progress);Application.Quit();break;
                case "category":g.UI.PopulateCatalog((PartCategory)argument);break;
                case "detach":g.DetachSelected();break;
                case "delete":g.DeleteSelected();break;
                case "symmetry":g.SymmetrySelected();break;
                case "stage":g.SetSelectedStage(argument);break;
                case "undo":g.Assembly.Undo();break;
                case "redo":g.Assembly.Redo();break;
                case "clear":g.ClearCraft();break;
                case "presets":g.UI.ShowPresets();break;
                case "save":g.SaveBlueprint();break;
                case "paint":g.UI.ShowPaint();break;
                case "passport":g.UI.ShowSelectedPassport();break;
                case "stand":g.StartFlight(true);break;
                case "launch":g.StartFlight(false);break;
                case "next_stage":g.NextStage();break;
                case "payload":g.ReleasePayload();break;
                case "parachute":g.DeployParachute();break;
                case "workshop":g.ReturnToWorkshop();break;
                case "flight_controls":g.UI.ShowFlightToolbar();break;
                case "map":g.ToggleMap();break;
                case "ascent":if(g.Flight!=null&&g.Flight.IsLunar) g.ToggleLandingPilot();else g.ToggleAscent();break;
                case "recover":g.PlanDeorbit();break;
                case "warp":g.CycleTimeWarp();break;
                case "throttle":g.AdjustThrottle(argument);break;
                case "scale":g.ScaleAssembly(argument>0?1.15f:1/1.15f);break;
                case "campaign":if(!g.IsFlying) g.StartMission(System.Math.Min(20,g.Progress.completed.Count+1));g.UI.CloseDialog();break;
                case "sandbox":g.StartSandbox();g.UI.CloseDialog();break;
                case "trial":g.StartTimeTrial();g.UI.CloseDialog();break;
                case "vr":g.UI.CloseDialog();g.EnableVR();break;
            }
        }
        public void OnPointerEnter(PointerEventData eventData) {target=1;LaboratoryGame.Instance?.Sound.Hover();}
        public void OnPointerExit(PointerEventData eventData) {target=0;}
        public void OnPointerDown(PointerEventData eventData) {LaboratoryGame.Instance?.Sound.Play(SoundCue.Click);}
        void Update()
        {
            if(accent==null||Mathf.Approximately(accent.color.a,target)) return;
            var color=accent.color;color.a=Mathf.MoveTowards(color.a,target,Time.unscaledDeltaTime*5);accent.color=color;
        }
    }
}
