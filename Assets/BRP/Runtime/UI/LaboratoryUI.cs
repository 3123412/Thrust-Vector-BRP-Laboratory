using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BRP
{
    public sealed class LaboratoryUI : MonoBehaviour
    {
        public Canvas canvas;
        public RectTransform root;
        public TextMeshProUGUI stats,detailTitle,detailText,status,missionTitle,missionGoal,flightData,toast,phase;
        LaboratoryGame game;
        RectTransform left,right,bottom,modal,catalogContent;
        readonly List<GameObject> workshopControls=new List<GameObject>();
        RectTransform flightPanel;
        PartCategory category;
        public bool ModalOpen=>modal!=null;

        public void Initialize(LaboratoryGame owner,TMP_FontAsset font)
        {
            game=owner;UiTheme.Font=font;
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();root=GetComponent<RectTransform>();
            var header=UiTheme.Panel("Header",root,0,0,1920,98,UiTheme.Ink);
            UiTheme.Panel("BrandMark",header,32,26,5,42,UiTheme.Orange);
            UiTheme.Text(header,"ВЕКТОР ТЯГИ",52,17,420,36,30);
            UiTheme.Text(header,"ЛАБОРАТОРИЯ BRP     /     ИНЖЕНЕРНАЯ ПРОГРАММА",53,58,600,22,12,UiTheme.Muted);
            UiTheme.Button(header,"Миссии",1040,28,128,42,ShowMissions);
            UiTheme.Button(header,"Чертежи",1180,28,128,42,ShowBlueprints);
            UiTheme.Button(header,"Рекорды",1320,28,128,42,ShowRecords);
            UiTheme.Button(header,"Помощь",1460,28,128,42,ShowHelp);
            UiTheme.Button(header,"Настройки",1600,28,140,42,ShowSettings);
            UiTheme.Button(header,"Меню",1752,28,128,42,ShowHome);
            UiTheme.Divider(root,0,98,1920);
            phase=UiTheme.Text(root,"01 / СБОРОЧНЫЙ АНГАР",390,119,650,26,15,UiTheme.Cyan);
            status=UiTheme.Text(root,"СИСТЕМЫ В ОЖИДАНИИ",1400,113,480,24,12,UiTheme.Muted);status.alignment=TextAlignmentOptions.Right;
            left=UiTheme.Panel("Catalog",root,28,144,330,762);
            UiTheme.Text(left,"КАТАЛОГ ДЕТАЛЕЙ",22,20,280,28,19);
            UiTheme.Text(left,"50 МОДУЛЕЙ  /  ПЕРЕТАЩИТЕ НА ВЕРСТАК",22,55,290,20,11,UiTheme.Muted);
            string[] names={"Корпус","Тяга","Рули","Питание","Груз"};
            for(int i=0;i<5;i++)
            {
                int index=i;
                UiTheme.Button(left,names[i],14+i*61,88,58,32,()=>PopulateCatalog((PartCategory)index));
            }
            UiTheme.Divider(left,16,132,298);
            catalogContent=UiTheme.Scroll(left,"Parts",14,145,304,600,out var scroll);
            right=UiTheme.Panel("Inspector",root,1562,144,330,762);
            UiTheme.Text(right,"ПАСПОРТ КОНСТРУКЦИИ",22,20,290,28,18);
            stats=UiTheme.Text(right,"",22,65,286,215,17);
            UiTheme.Divider(right,22,285,286);
            detailTitle=UiTheme.Text(right,"Выберите деталь",22,307,286,62,22);
            detailText=UiTheme.Text(right,"Описание и параметры появятся здесь.",22,380,286,142,15,UiTheme.Muted);
            UiTheme.Button(right,"Снять",20,545,138,40,()=>game.DetachSelected());
            UiTheme.Button(right,"Удалить",170,545,138,40,()=>game.DeleteSelected());
            UiTheme.Button(right,"Симметрия ×4",20,597,288,40,()=>game.SymmetrySelected());
            UiTheme.Text(right,"СТУПЕНЬ",20,653,120,23,12,UiTheme.Muted);
            for(int i=0;i<6;i++) {int s=i;UiTheme.Button(right,(i+1).ToString(),20+i*49,683,43,36,()=>game.SetSelectedStage(s));}
            bottom=UiTheme.Panel("BuildTools",root,390,934,1140,94,UiTheme.Ink);
            UiTheme.Button(bottom,"Отмена",16,21,100,48,()=>game.Assembly.Undo());
            UiTheme.Button(bottom,"Повтор",124,21,100,48,()=>game.Assembly.Redo());
            UiTheme.Button(bottom,"Очистить",232,21,108,48,()=>game.ClearCraft());
            UiTheme.Button(bottom,"Эталон",348,21,100,48,ShowPresets);
            UiTheme.Button(bottom,"Сохранить",456,21,118,48,()=>game.SaveBlueprint());
            UiTheme.Button(bottom,"Покраска",582,21,110,48,ShowPaint);
            UiTheme.Button(bottom,"На стенд",700,21,130,48,()=>game.StartFlight(true));
            UiTheme.Button(bottom,"ЗАПУСК",842,16,282,58,()=>game.StartFlight(false),true);
            missionTitle=UiTheme.Text(root,"СВОБОДНЫЙ КОНСТРУКТОР",390,160,1125,42,28);
            missionGoal=UiTheme.Text(root,"Соберите свою ракету. Или начните с готового чертежа.",391,210,1100,64,16,UiTheme.Muted);
            toast=UiTheme.Text(root,"",400,875,1120,40,18,UiTheme.Cyan);toast.alignment=TextAlignmentOptions.Center;
            UiTheme.Text(root,"BRP / 1961–2026",30,1042,350,24,12,UiTheme.Muted);
            UiTheme.Text(root,"ЛКМ: выбрать / переносить     ПКМ: обзор     R: повернуть     Delete: удалить     Ctrl+Z: отмена",450,1042,1400,24,12,UiTheme.Muted);
            PopulateCatalog(PartCategory.Structure);
            CreateFlightControls();SetFlight(false);ShowHome();
        }

        void CreateFlightControls()
        {
            flightPanel=UiTheme.Panel("FlightPanel",root,28,144,330,762);
            UiTheme.Text(flightPanel,"ТЕЛЕМЕТРИЯ ПОЛЁТА",22,20,290,30,19);
            flightData=UiTheme.Text(flightPanel,"",22,72,286,460,18);
            UiTheme.Button(flightPanel,"Ступень / Space",20,550,290,42,()=>game.NextStage());
            UiTheme.Button(flightPanel,"Отделить груз",20,603,290,42,()=>game.ReleasePayload());
            UiTheme.Button(flightPanel,"Парашют / P",20,656,290,42,()=>game.DeployParachute());
            UiTheme.Button(flightPanel,"В ангар",20,709,290,36,()=>game.ReturnToWorkshop());
        }

        public void SetFlight(bool active)
        {
            left.gameObject.SetActive(!active);right.gameObject.SetActive(!active);bottom.gameObject.SetActive(!active);
            flightPanel.gameObject.SetActive(active);
            phase.text=active?"02 / ИСПЫТАНИЯ И ПОЛЁТ":"01 / СБОРОЧНЫЙ АНГАР";
        }

        public void ShowFlightToolbar()
        {
            var r=Dialog("УПРАВЛЕНИЕ ПОЛЁТОМ",790,620);
            string[] modes={"Вручную","Удерживать","По скорости","Против скорости","Радиально","Нормаль","На цель"};
            for(int i=0;i<modes.Length;i++) {int mode=i;UiTheme.Button(r,modes[i],35+(i%3)*238,90+(i/3)*54,225,42,()=>{game.SetGuidance((GuidanceMode)mode);CloseDialog();});}
            UiTheme.Text(r,"Тяга: Shift / Ctrl. Отключить: X. W/S и A/D: наклон. Q/E: крен.\nI/K, J/L, U/O: двигатели сближения.\nM: карта. T: ускорение времени. B: тормоз. Backspace: спасение.",35,270,720,100,17,UiTheme.Muted);
            UiTheme.Button(r,"Орбитальный стенд",35,400,342,46,()=>{CloseDialog();game.StartOrbitalPractice();});
            UiTheme.Button(r,"Стыковать",393,400,342,46,()=>{CloseDialog();game.TryDock();});
            UiTheme.Button(r,"Тормозной манёвр",35,460,342,46,()=>{game.PlanDeorbit();CloseDialog();});
            UiTheme.Button(r,"Карта / траектория",393,460,342,46,()=>{game.ToggleMap();CloseDialog();});
        }

        public void PopulateCatalog(PartCategory next)
        {
            category=next;
            foreach(Transform child in catalogContent) Destroy(child.gameObject);
            var parts=game.Catalog.parts.Where(p=>p.category==category).ToArray();
            for(int i=0;i<parts.Length;i++)
            {
                var d=parts[i];var card=UiTheme.Panel(d.id,catalogContent,0,i*86,290,77,new Color(.065f,.104f,.133f));
                UiTheme.Text(card,Array.IndexOf(game.Catalog.parts,d).ToString("D2"),12,13,30,24,13,UiTheme.Cyan);
                UiTheme.Text(card,d.title,50,10,225,40,16);
                UiTheme.Text(card,$"{d.dryMass:N0} КГ    /    {(d.surface?"БОКОВОЙ УЗЕЛ":"ОСЕВОЙ УЗЕЛ")}",50,53,225,20,10,UiTheme.Muted);
                card.gameObject.AddComponent<CatalogCard>().definition=d;
            }
            catalogContent.sizeDelta=new Vector2(294,parts.Length*86);
            catalogContent.anchoredPosition=Vector2.zero;
        }

        public void ShowDefinition(PartDefinition d)
        {
            detailTitle.text=d.title;
            detailText.text=d.description+$"\n\nМасса: {d.dryMass:N0} кг"+(d.thrust>0?$"\nТяга: {d.thrust/1000:N0} кН · Isp {d.specificImpulse:N0} с":"")+(d.fuelCapacity+d.oxidizerCapacity>0?$"\nЗапас: {d.fuelCapacity+d.oxidizerCapacity:N0} кг":"");
        }
        public void UpdateStats()
        {
            var craft=game.Assembly.Blueprint;
            if(craft.parts.Count==0) {stats.text="ДЕТАЛИ                  0 / 128\n\nМАССА                         0 КГ\nТЯГА                           0 КН\n\nУстановите первый корпус.";return;}
            var body=new FlightBody(craft,game.Catalog);
            stats.text=$"ДЕТАЛИ                {craft.parts.Count} / 128\nТИПЫ                       {craft.parts.Select(p=>p.partId).Distinct().Count()} / 50\n\nМАССА                   {body.mass/1000:F1} Т\nТЯГА                      {body.AvailableThrust/1000:F0} КН\nТЯГА / ВЕС             {body.TWR:F2}\nЗАПАС ΔV              {body.DeltaV:F0} М/С";
            status.text=body.TWR>1?"КОНСТРУКЦИЯ ГОТОВА К ПРОВЕРКЕ":"ПРОВЕРЬТЕ ТЯГУ И МАССУ";
        }
        public void UpdateFlight(FlightBody body,double distance,double relativeSpeed)
        {
            var orbit=body.Orbit;
            string Format(double v)=>double.IsInfinity(v)?"∞":(v/1000).ToString("F1");
            flightData.text=$"T + {body.elapsed:0000.0} С\n\nВЫСОТА          {body.Altitude/1000:F2} КМ\nСКОРОСТЬ       {body.SurfaceVelocity.Magnitude:F0} М/С\nПО ВЕРТИКАЛИ {body.VerticalSpeed:F0} М/С\n\nМАССА             {body.mass/1000:F2} Т\nТОПЛИВО         {body.Fuel/1000:F2} Т\nТЯГА                 {body.lastThrust/1000:F0} КН\nДРОССЕЛЬ        {body.throttle*100:F0}%\nСТУПЕНЬ          {body.stage+1} / 6\n\nАПОГЕЙ            {Format(orbit.apoapsis)} КМ\nПЕРИГЕЙ          {Format(orbit.periapsis)} КМ\nНАГРЕВ             {body.Temperature:F0} К\nЭНЕРГИЯ          {body.Energy:F0} ДЖ\n\nЦЕЛЬ                 {distance:F1} М\nОТН. СКОРОСТЬ {relativeSpeed:F2} М/С";
            status.text=$"{body.lastEvent.ToUpperInvariant()}   /   ×{game.TimeWarp}";
        }

        RectTransform Dialog(string title,float width=900,float height=760)
        {
            CloseDialog();
            modal=UiTheme.Panel("ModalShade",root,0,98,1920,942,new Color(.01f,.02f,.03f,.82f));
            var panel=UiTheme.Panel("Dialog",modal,(1920-width)/2,(942-height)/2,width,height,UiTheme.Ink);
            UiTheme.Panel("Accent",panel,0,0,4,60,UiTheme.Orange);
            UiTheme.Text(panel,title,30,24,width-100,40,26);
            UiTheme.Button(panel,"×",width-62,20,40,40,CloseDialog);
            UiTheme.Divider(panel,30,76,width-60);return panel;
        }
        public void CloseDialog() {if(modal!=null) {Destroy(modal.gameObject);modal=null;}}
        public void ShowHome()
        {
            var panel=Dialog("ВЕКТОР ТЯГИ",820,690);
            UiTheme.Text(panel,"ЛАБОРАТОРИЯ BRP",35,98,750,35,18,UiTheme.Cyan);
            UiTheme.Text(panel,"Соберите ракету.\nПроверьте расчёт полётом.",35,152,730,110,38);
            UiTheme.Text(panel,"50 типов деталей · 20 миссий · орбитальная программа",35,286,740,35,17,UiTheme.Muted);
            UiTheme.Button(panel,"Начать кампанию",35,356,750,60,()=>{game.StartMission(Math.Min(20,game.Progress.completed.Count+1));CloseDialog();},true);
            UiTheme.Button(panel,"Свободный конструктор",35,431,365,52,()=>{game.StartSandbox();CloseDialog();});
            UiTheme.Button(panel,"Сборка на время",420,431,365,52,()=>{game.StartTimeTrial();CloseDialog();});
            UiTheme.Button(panel,"Виртуальная реальность",35,499,750,48,()=>{CloseDialog();game.EnableVR();});
            UiTheme.Text(panel,"Управление мышью и клавиатурой доступно сразу.\nИстория космонавтики начинается с работы инженера.",35,582,750,60,17,UiTheme.Muted);
        }
        public void ShowMissions()
        {
            var r=Dialog("ПРОГРАММА ИСПЫТАНИЙ",1120,825);
            var content=UiTheme.Scroll(r,"Missions",30,95,1060,690,out var scroll);
            var missions=game.Missions;
            for(int i=0;i<missions.Length;i++)
            {
                var m=missions[i];bool done=game.Progress.completed.Contains(m.id);
                bool unlocked=m.id==1||game.Progress.completed.Contains(m.id-1);
                var row=UiTheme.Panel(m.title,content,0,i*94,1030,85,UiTheme.PanelColor);
                UiTheme.Text(row,m.id.ToString("D2"),18,16,60,40,30,done?UiTheme.Cyan:UiTheme.Muted);
                UiTheme.Text(row,m.title,90,12,700,30,21);
                UiTheme.Text(row,m.goal,90,48,700,28,14,UiTheme.Muted);
                var button=UiTheme.Button(row,done?"Повторить":unlocked?"Начать":"Закрыто",855,21,155,42,()=>{game.StartMission(m.id);CloseDialog();},unlocked&&!done);
                button.interactable=unlocked||done;
            }
            content.sizeDelta=new Vector2(1030,missions.Length*94);
        }
        public void ShowBlueprints()
        {
            var r=Dialog("СОХРАНЁННЫЕ ЧЕРТЕЖИ");
            var content=UiTheme.Scroll(r,"Blueprints",30,100,840,590,out var scroll);
            for(int i=0;i<game.Progress.blueprints.Count;i++)
            {
                var blueprint=game.Progress.blueprints[i];
                UiTheme.Text(content,blueprint.name+"  /  "+blueprint.parts.Count+" деталей",0,i*66,600,50,20);
                UiTheme.Button(content,"Открыть",640,i*66,165,46,()=>{game.ReturnToWorkshop();game.Assembly.Load(blueprint);CloseDialog();});
            }
            if(game.Progress.blueprints.Count==0) UiTheme.Text(content,"Сохраните конструкцию кнопкой на верстаке.",0,0,800,100,21,UiTheme.Muted);
            content.sizeDelta=new Vector2(820,Math.Max(100,game.Progress.blueprints.Count*66));
        }
        public void ShowPresets()
        {
            var r=Dialog("ЭТАЛОННЫЕ КОНСТРУКЦИИ",780,430);
            string[] titles={"БРП-01 / Учебная","БРП-02 / Орбитальная","БРП-03 / Возвращаемая"};
            for(int i=0;i<3;i++) {int level=i;UiTheme.Button(r,titles[i],35,110+i*78,710,60,()=>{game.LoadPreset(level);CloseDialog();});}
        }
        public void ShowPaint()
        {
            var r=Dialog("ЦВЕТ КОРПУСА",690,350);
            Color[] colors={new Color(.82f,.86f,.88f),new Color(.06f,.18f,.28f),new Color(.92f,.29f,.07f),new Color(.88f,.83f,.68f),new Color(.12f,.4f,.36f)};
            for(int i=0;i<colors.Length;i++)
            {
                var color=colors[i];var b=UiTheme.Button(r,"",35+i*125,120,110,90,()=>{game.Assembly.Paint(game.Selected,color);game.Sound.Play(SoundCue.Click);CloseDialog();});
                b.GetComponent<UnityEngine.UI.Image>().color=color;
            }
            UiTheme.Text(r,"Выбранная деталь или весь корпус, если ничего не выбрано.",35,247,620,60,17,UiTheme.Muted);
        }
        public void ShowRecords()
        {
            var r=Dialog("ЖУРНАЛ ЛАБОРАТОРИИ",820,600);
            UiTheme.Text(r,$"Пройдено миссий: {game.Progress.completed.Count} / 20\nСохранено конструкций: {game.Progress.blueprints.Count}\n\nСборка на время",35,110,750,150,24);
            string records=game.Progress.bestTimes.Count==0?"Пока нет завершённых попыток.":string.Join("\n",game.Progress.bestTimes.OrderBy(t=>t).Take(5).Select((t,i)=>$"{i+1:00}       {TimeSpan.FromSeconds(t):mm\\:ss\\.ff}"));
            UiTheme.Text(r,records,35,290,750,210,23,UiTheme.Cyan);
        }
        public void ShowSettings()
        {
            var r=Dialog("НАСТРОЙКИ",820,600);
            UiTheme.Text(r,"Громкость",35,110,740,30,22);
            UiTheme.Button(r,"Тише",35,160,230,48,()=>game.SetVolume(-.1f));
            UiTheme.Button(r,"Громче",285,160,230,48,()=>game.SetVolume(.1f));
            UiTheme.Button(r,"Без звука",535,160,245,48,()=>game.SetVolume(-1));
            UiTheme.Text(r,"Графика",35,250,740,30,22);
            string[] quality={"Производительность","Баланс","Качество"};
            for(int i=0;i<3;i++) {int value=i;UiTheme.Button(r,quality[i],35+i*250,300,240,50,()=>game.SetQuality(value));}
            UiTheme.Button(r,"Выйти из игры",35,440,745,50,()=>Application.Quit());
        }
        public void ShowHelp()
        {
            var r=Dialog("УПРАВЛЕНИЕ И ФИЗИКА",1050,820);
            UiTheme.Text(r,"СБОРКА",35,110,950,30,20,UiTheme.Cyan);
            UiTheme.Text(r,"Перетащите деталь из каталога или нажмите на её карточку.\nПереносите деталь мышью к узлу. Голубое кольцо показывает место фиксации.\nR поворачивает деталь. «Снять» разблокирует узел. «Симметрия ×4» добавляет боковые копии.\nНомер ступени определяет, когда двигатель включится. Сохраняйте чертежи перед испытанием.",35,155,970,150,18);
            UiTheme.Text(r,"ПОЛЁТ",35,325,950,30,20,UiTheme.Cyan);
            UiTheme.Text(r,"Shift / Ctrl: тяга. X: выключить. Space: следующая ступень.\nW/S, A/D: наклон. Q/E: крен. P: парашют. B: воздушный тормоз.\nM: карта. T: ускорение времени. F: команды автопилота.\nI/K, J/L, U/O: перемещение двигателями ориентации. Backspace: аварийное спасение.",35,370,970,135,18);
            UiTheme.Text(r,"ИНЖЕНЕРНЫЙ РАСЧЁТ",35,530,950,30,20,UiTheme.Cyan);
            UiTheme.Text(r,"Тяга должна превышать вес. Центр масс и центр давления определяют устойчивость.\nТопливо меняет массу в полёте. В вакууме рули не работают, нужны двигатели ориентации.\nДля орбиты нужна скорость вдоль поверхности. Тепловой щит и парашют нужны для возвращения.\nВ VR: захват контроллером, луч для панелей, разблокировка установленного узла через меню.",35,575,970,150,18);
        }
        public void ShowResult(string title,string message)
        {
            var r=Dialog(title,870,600);
            UiTheme.Text(r,message,35,115,800,310,23);
            UiTheme.Button(r,"Доработать конструкцию",35,465,385,60,()=>{game.ReturnToWorkshop();CloseDialog();});
            UiTheme.Button(r,"Программа испытаний",445,465,385,60,ShowMissions,true);
        }
    }
}
