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
        [SerializeField] LaboratoryGame game;
        [SerializeField] RectTransform left,right,bottom,modal,catalogContent,homePanel;
        [SerializeField] TextMeshProUGUI[] craftValues;
        [SerializeField] TextMeshProUGUI[] flightValues;
        [SerializeField] RectTransform flightActions;
        readonly List<GameObject> workshopControls=new List<GameObject>();
        [SerializeField] RectTransform flightPanel;
        [SerializeField] RectTransform flightSystems,fuelBar,energyBar,throttleBar;
        [SerializeField] TextMeshProUGUI systemData,orientationData;
        [SerializeField] AttitudeIndicator attitude;
        PartCategory category;
        public bool ModalOpen=>modal!=null||(homePanel!=null&&homePanel.gameObject.activeSelf);

        public void Initialize(LaboratoryGame owner,TMP_FontAsset font)
        {
            game=owner;UiTheme.Font=font;
            if(root==null) BuildLayout(owner,font);
            PopulateCatalog(PartCategory.Structure);SetFlight(false);UpdateStats();ShowHome();
        }

        public void BuildLayout(LaboratoryGame owner,TMP_FontAsset font)
        {
            game=owner;UiTheme.Font=font;
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera=owner.mainCamera;canvas.planeDistance=.5f;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();root=GetComponent<RectTransform>();
            var header=UiTheme.Panel("Navigation",root,24,20,1872,74,UiTheme.Ink);
            UiTheme.Text(header,"БРП",24,10,88,52,34,UiTheme.TextColor);
            UiTheme.Panel("BrandAccent",header,116,20,3,32,UiTheme.Orange);
            UiTheme.Text(header,"ВЕКТОР ТЯГИ",138,15,350,30,19);
            UiTheme.Text(header,"ЭКСПЕРИМЕНТАЛЬНАЯ ЛАБОРАТОРИЯ",138,43,400,20,10,UiTheme.Muted);
            string[] links={"Программа","Чертежи","Журнал","Справка","Настройки"};
            string[] commands={"missions","blueprints","records","help","settings"};
            for(int i=0;i<links.Length;i++) UiTheme.Command(header,links[i],960+i*155,17,145,40,commands[i]);
            UiTheme.Command(header,"Выход",1755,17,96,40,"home");
            phase=UiTheme.Text(root,"АНГАР 01     /     СБОРКА",42,112,650,30,12,UiTheme.Cyan);
            UiTheme.Command(root,"−",922,107,34,30,"scale",-1);
            UiTheme.Command(root,"+",966,107,34,30,"scale",1);
            status=UiTheme.Text(root,"ГОТОВ К РАБОТЕ",1400,112,470,24,11,UiTheme.Muted);status.alignment=TextAlignmentOptions.Right;
            left=UiTheme.Panel("PartsLibrary",root,24,156,324,764);
            UiTheme.Text(left,"КОМПОНЕНТЫ",20,18,280,32,20);
            UiTheme.Text(left,"50 типов / выбор и перенос",20,55,280,24,12,UiTheme.Muted);
            string[] tabs={"Корпус","Тяга","Рули","Ток","Груз"};
            for(int i=0;i<5;i++) UiTheme.Command(left,tabs[i],14+i*59,96,56,36,"category",i);
            UiTheme.Divider(left,18,146,288);
            catalogContent=UiTheme.Scroll(left,"Parts",12,163,300,579,out var scroll);
            right=UiTheme.Panel("CraftInspector",root,1572,156,324,764);
            UiTheme.Text(right,"ИНЖЕНЕРНЫЙ РАСЧЁТ",22,20,280,29,18);
            craftValues=new TextMeshProUGUI[4];
            string[] labels={"МАССА / Т","ТЯГА / КН","ТЯГА К ВЕСУ","ЗАПАС ΔV / М/С"};
            for(int i=0;i<4;i++)
            {
                float x=22+(i%2)*148,y=82+(i/2)*104;
                UiTheme.Text(right,labels[i],x,y,138,22,10,UiTheme.Muted);
                craftValues[i]=UiTheme.Text(right,"0",x,y+26,138,52,34);
            }
            stats=UiTheme.Text(right,"0 деталей",22,303,280,25,13,UiTheme.Cyan);
            UiTheme.Divider(right,22,343,280);
            detailTitle=UiTheme.Text(right,"Выберите компонент",22,370,280,75,23);
            detailText=UiTheme.Text(right,"Паспорт детали, масса и назначение появятся здесь.",22,463,280,129,15,UiTheme.Muted);
            UiTheme.Command(right,"Разблокировать",20,610,190,42,"detach");
            UiTheme.Command(right,"Удалить",220,610,86,42,"delete");
            UiTheme.Command(right,"Симметрия ×4",20,665,286,38,"symmetry");
            UiTheme.Text(right,"СТУПЕНЬ",20,718,75,20,10,UiTheme.Muted);
            for(int i=0;i<6;i++) UiTheme.Command(right,(i+1).ToString(),101+i*34,711,29,31,"stage",i);
            bottom=UiTheme.Panel("AssemblyTools",root,373,947,1174,76,UiTheme.Ink);
            UiTheme.Command(bottom,"Отмена",14,15,90,45,"undo");
            UiTheme.Command(bottom,"Повтор",111,15,90,45,"redo");
            UiTheme.Command(bottom,"Чертёж",208,15,109,45,"presets");
            UiTheme.Command(bottom,"Очистить",324,15,108,45,"clear");
            UiTheme.Command(bottom,"Сохранить",439,15,128,45,"save");
            UiTheme.Command(bottom,"Цвет",574,15,92,45,"paint");
            UiTheme.Command(bottom,"Испытать",673,15,151,45,"stand");
            UiTheme.Command(bottom,"НА КОСМОДРОМ  →",838,11,321,53,"launch",0,true);
            missionTitle=UiTheme.Text(root,"СВОБОДНЫЙ КОНСТРУКТОР",380,161,430,82,22);
            missionGoal=UiTheme.Text(root,"Соберите и испытайте свою конструкцию.",381,254,340,120,14,UiTheme.Muted);
            toast=UiTheme.Text(root,"",430,884,1060,44,17,UiTheme.Cyan);toast.alignment=TextAlignmentOptions.Center;
            UiTheme.Text(root,"BRP    /    65 ЛЕТ ПЕРВОМУ ПОЛЁТУ",28,1045,540,23,10,UiTheme.Muted);
            UiTheme.Text(root,"ЛКМ  Выбор     ПКМ  Обзор     R  Поворот     Ctrl+Z  Отмена",1070,1045,820,23,11,UiTheme.Muted).alignment=TextAlignmentOptions.Right;
            CreateFlightControls();BuildHome();SetFlight(false);
        }

        void BuildHome()
        {
            homePanel=UiTheme.Panel("MainMenu",root,44,154,650,750,new Color(.025f,.035f,.04f,.94f));
            UiTheme.Text(homePanel,"ПРОГРАММА BRP     /     1961–2026",36,34,570,28,12,UiTheme.Cyan);
            UiTheme.Text(homePanel,"ВЕКТОР\nТЯГИ",30,108,590,210,82);
            UiTheme.Text(homePanel,"ЛАБОРАТОРИЯ BRP",36,334,570,40,23,UiTheme.Muted);
            UiTheme.Divider(homePanel,36,402,570);
            UiTheme.Text(homePanel,"Ракета начинается с расчёта.\nСоберите свою и проверьте её в полёте.",36,430,570,70,19);
            UiTheme.Command(homePanel,"Продолжить программу  →",36,535,570,58,"campaign",0,true);
            UiTheme.Command(homePanel,"Свободный режим",36,608,276,44,"sandbox");
            UiTheme.Command(homePanel,"Сборка на время",330,608,276,44,"trial");
            UiTheme.Command(homePanel,"Подключить VR",36,672,570,40,"vr");
        }

        void CreateFlightControls()
        {
            flightSystems=UiTheme.Panel("FlightSystems",root,1572,156,324,764);
            UiTheme.Text(flightSystems,"ОРИЕНТАЦИЯ И СИСТЕМЫ",22,20,280,29,17);
            var ball=UiTheme.Rect("Attitude",flightSystems,56,70,212,212);
            attitude=ball.gameObject.AddComponent<AttitudeIndicator>();attitude.raycastTarget=false;
            orientationData=UiTheme.Text(flightSystems,"ТАНГАЖ 90°",22,295,280,24,12,UiTheme.Cyan);orientationData.alignment=TextAlignmentOptions.Center;
            string[] gauges={"ТЯГА","ТОПЛИВО","ПИТАНИЕ"};
            RectTransform[] bars=new RectTransform[3];
            for(int i=0;i<3;i++)
            {
                UiTheme.Text(flightSystems,gauges[i],22,346+i*45,110,21,11,UiTheme.Muted);
                var track=UiTheme.Panel(gauges[i],flightSystems,134,351+i*45,166,5,new Color(.13f,.17f,.19f));
                bars[i]=UiTheme.Panel("Fill",track,0,0,166,5,i==0?UiTheme.Orange:UiTheme.Cyan);
            }
            throttleBar=bars[0];fuelBar=bars[1];energyBar=bars[2];
            systemData=UiTheme.Text(flightSystems,"",22,500,280,116,14,UiTheme.Muted);
            UiTheme.Command(flightSystems,"Программа выведения",20,635,284,43,"ascent",0,true);
            UiTheme.Command(flightSystems,"Возвращение капсулы",20,691,284,43,"recover");
            UiTheme.Command(root,"Время ×  /  T",365,107,170,32,"warp");
            flightPanel=UiTheme.Panel("FlightReadout",root,24,156,324,574);
            UiTheme.Text(flightPanel,"ПОЛЁТНЫЕ ДАННЫЕ",22,20,280,28,18);
            flightValues=new TextMeshProUGUI[4];
            string[] labels={"ВЫСОТА / КМ","СКОРОСТЬ / М/С","АПОГЕЙ / КМ","ПЕРИГЕЙ / КМ"};
            for(int i=0;i<4;i++)
            {
                float y=78+i*95;
                UiTheme.Text(flightPanel,labels[i],22,y,280,23,11,UiTheme.Muted);
                flightValues[i]=UiTheme.Text(flightPanel,"0.0",22,y+27,280,54,38,i<2?UiTheme.TextColor:UiTheme.Cyan);
            }
            flightData=UiTheme.Text(flightPanel,"",22,458,280,26,12,UiTheme.Muted);
            UiTheme.Command(flightPanel,"Тяга −",20,508,85,42,"throttle",-1);
            UiTheme.Command(flightPanel,"Стоп",117,508,85,42,"throttle",0);
            UiTheme.Command(flightPanel,"Тяга +",214,508,90,42,"throttle",1);
            flightActions=UiTheme.Panel("FlightCommands",root,373,947,1174,76,UiTheme.Ink);
            UiTheme.Command(flightActions,"СТУПЕНЬ  /  Space",14,12,240,52,"next_stage",0,true);
            UiTheme.Command(flightActions,"Отделить груз",266,15,172,45,"payload");
            UiTheme.Command(flightActions,"Парашют",450,15,144,45,"parachute");
            UiTheme.Command(flightActions,"Карта",606,15,125,45,"map");
            UiTheme.Command(flightActions,"Управление",743,15,202,45,"flight_controls");
            UiTheme.Command(flightActions,"В ангар",957,15,201,45,"workshop");
        }

        public void SetFlight(bool active)
        {
            left.gameObject.SetActive(!active);right.gameObject.SetActive(!active);bottom.gameObject.SetActive(!active);
            flightPanel.gameObject.SetActive(active);flightActions.gameObject.SetActive(active);
            flightSystems.gameObject.SetActive(active);
            missionTitle.gameObject.SetActive(!active);missionGoal.gameObject.SetActive(!active);
            phase.text=active?"КОСМОДРОМ     /     ПОЛЁТНЫЕ ИСПЫТАНИЯ":"АНГАР 01     /     ИНЖЕНЕРНАЯ СБОРКА";
        }

        public void ShowFlightToolbar()
        {
            var r=Dialog("УПРАВЛЕНИЕ ПОЛЁТОМ",790,730);
            string[] modes={"Вручную","Удерживать","По скорости","Против скорости","Радиально","Нормаль","На цель"};
            for(int i=0;i<modes.Length;i++) {int mode=i;UiTheme.Button(r,modes[i],35+(i%3)*238,90+(i/3)*54,225,42,()=>{game.SetGuidance((GuidanceMode)mode);CloseDialog();});}
            UiTheme.Text(r,"Тяга: Shift / Ctrl. Отключить: X. W/S и A/D: наклон. Q/E: крен.\nI/K, J/L, U/O: двигатели сближения.\nM: карта. T: ускорение времени. B: тормоз. Backspace: спасение.",35,270,720,100,17,UiTheme.Muted);
            UiTheme.Button(r,"Орбитальный стенд",35,400,342,46,()=>{CloseDialog();game.StartOrbitalPractice();});
            UiTheme.Button(r,"Стыковать",393,400,342,46,()=>{CloseDialog();game.TryDock();});
            UiTheme.Button(r,"Тормозной манёвр",35,460,342,46,()=>{game.PlanDeorbit();CloseDialog();});
            UiTheme.Button(r,"Карта / траектория",393,460,342,46,()=>{game.ToggleMap();CloseDialog();});
            UiTheme.Button(r,"Помощник сближения",35,520,342,46,()=>{game.ToggleRendezvous();CloseDialog();});
            UiTheme.Button(r,"Топливный клапан",393,520,342,46,()=>{game.ToggleValve();CloseDialog();});
            UiTheme.Button(r,"Отделить капсулу",35,580,342,46,()=>{game.SeparateCapsule();CloseDialog();});
            UiTheme.Button(r,"Аварийное спасение",393,580,342,46,()=>{game.Abort();CloseDialog();});
        }

        public void PopulateCatalog(PartCategory next)
        {
            category=next;
            for(int i=catalogContent.childCount-1;i>=0;i--)
            {
                var child=catalogContent.GetChild(i);
                if(Application.isPlaying) Destroy(child.gameObject);else DestroyImmediate(child.gameObject);
            }
            var parts=game.Catalog.parts.Where(p=>p.category==category).ToArray();
            for(int i=0;i<parts.Length;i++)
            {
                var d=parts[i];var card=UiTheme.Panel(d.id,catalogContent,0,i*100,286,92,new Color(.067f,.081f,.087f));
                if(d.thumbnail!=null) {var icon=UiTheme.Panel("Preview",card,6,10,67,67,Color.white);var image=icon.GetComponent<UnityEngine.UI.Image>();image.sprite=d.thumbnail;image.preserveAspect=true;image.raycastTarget=false;}
                UiTheme.Text(card,(Array.IndexOf(game.Catalog.parts,d)+1).ToString("D2"),82,64,28,18,10,UiTheme.Cyan);
                UiTheme.Text(card,d.title,82,13,190,43,16);
                UiTheme.Text(card,$"{d.dryMass:N0} КГ    /    {(d.surface?"БОКОВОЙ УЗЕЛ":"ОСЕВОЙ УЗЕЛ")}",118,64,155,20,9,UiTheme.Muted);
                card.gameObject.AddComponent<CatalogCard>().definition=d;
            }
            catalogContent.sizeDelta=new Vector2(286,parts.Length*100);
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
            if(craft.parts.Count==0)
            {
                stats.text="0 деталей / 0 типов";foreach(var value in craftValues) value.text="0";
                return;
            }
            var body=new FlightBody(craft,game.Catalog);
            craftValues[0].text=(body.mass/1000).ToString("F1");craftValues[1].text=(body.AvailableThrust/1000).ToString("F0");
            craftValues[2].text=body.TWR.ToString("F2");craftValues[3].text=body.DeltaV.ToString("F0");
            craftValues[2].color=body.TWR>1?UiTheme.Cyan:UiTheme.Orange;
            stats.text=$"{craft.parts.Count} деталей / {craft.parts.Select(p=>p.partId).Distinct().Count()} типов";
            status.text=body.TWR>1?"СИСТЕМЫ ГОТОВЫ К ИСПЫТАНИЮ":"ПРОВЕРЬТЕ ТЯГУ И МАССУ";
        }
        public void UpdateFlight(FlightBody body,double distance,double relativeSpeed)
        {
            var orbit=body.Orbit;
            string Format(double value)=>double.IsInfinity(value)?"∞":(value/1000).ToString("F1");
            flightValues[0].text=(body.Altitude/1000).ToString("F2");flightValues[1].text=body.SurfaceVelocity.Magnitude.ToString("F0");
            flightValues[2].text=Format(orbit.apoapsis);flightValues[3].text=Format(orbit.periapsis);
            flightData.text=$"T+{body.elapsed:0000}с   Тяга {body.throttle*100:F0}%   {body.Temperature:F0}К";
            attitude.SetState(body);
            bool horizon=body.Powered&&(body.Has(PartKind.HorizonSensor)||body.Has(PartKind.Capsule));
            attitude.color=horizon?Color.white:new Color(1,1,1,.25f);
            orientationData.text=horizon?$"НАКЛОН {body.Tilt:F1}°   /   СТУПЕНЬ {body.stage+1}":"ДАТЧИК ГОРИЗОНТА НЕДОСТУПЕН";
            double capacity=body.parts.Where(p=>!p.destroyed).Sum(p=>(double)p.definition.fuelCapacity+p.definition.oxidizerCapacity);
            fuelBar.sizeDelta=new Vector2(166*(float)(body.Fuel/System.Math.Max(1,capacity)),5);
            energyBar.sizeDelta=new Vector2(166*(float)(body.Energy/System.Math.Max(1,body.EnergyCapacity)),5);
            throttleBar.sizeDelta=new Vector2(166*(float)body.throttle,5);
            systemData.text=$"Топливо  {body.Fuel:N0} кг\nЗаряд  {body.Energy/1000:F1} кДж\nПерегрузка  {body.maxG:F1} g\n"+(game.Target!=null?$"До узла  {distance:F1} м\nСближение  {relativeSpeed:F2} м/с":body.Telemetry?"Телеметрия  связь установлена":"Телеметрия  нет связи");
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
        public void CloseDialog()
        {
            if(modal!=null) {Destroy(modal.gameObject);modal=null;}
            if(homePanel!=null) homePanel.gameObject.SetActive(false);
            if(left!=null) SetFlight(game.IsFlying);
        }
        public void ShowHome()
        {
            CloseDialog();homePanel.gameObject.SetActive(true);
            left.gameObject.SetActive(false);right.gameObject.SetActive(false);bottom.gameObject.SetActive(false);
            missionTitle.gameObject.SetActive(false);missionGoal.gameObject.SetActive(false);
            flightPanel.gameObject.SetActive(false);flightActions.gameObject.SetActive(false);
            flightSystems.gameObject.SetActive(false);
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
            var r=Dialog("НАСТРОЙКИ",820,740);
            UiTheme.Text(r,"Громкость",35,110,740,30,22);
            UiTheme.Button(r,"Тише",35,160,230,48,()=>game.SetVolume(-.1f));
            UiTheme.Button(r,"Громче",285,160,230,48,()=>game.SetVolume(.1f));
            UiTheme.Button(r,"Без звука",535,160,245,48,()=>game.SetVolume(-1));
            string[] channels={"Интерфейс и механика","Двигатели","Атмосфера ангара"};
            float[] levels={game.Progress.interfaceVolume,game.Progress.engineVolume,game.Progress.environmentVolume};
            for(int i=0;i<3;i++)
            {
                int channel=i;float y=245+i*55;
                UiTheme.Text(r,channels[i],35,y,310,30,17,UiTheme.Muted);
                var slider=UiTheme.Slider(r,350,y,425,30,levels[i]);slider.onValueChanged.AddListener(value=>game.SetAudioChannel(channel,value));
            }
            UiTheme.Text(r,"Графика",35,435,740,30,22);
            string[] quality={"Производительность","Баланс","Качество"};
            for(int i=0;i<3;i++) {int value=i;UiTheme.Button(r,quality[i],35+i*250,485,240,50,()=>game.SetQuality(value));}
            UiTheme.Button(r,"Выйти из игры",35,620,745,50,()=>Application.Quit());
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
