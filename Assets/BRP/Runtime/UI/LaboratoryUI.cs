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
        [SerializeField] TextMeshProUGUI[] flightLabels;
        [SerializeField] RectTransform flightActions;
        readonly List<GameObject> workshopControls=new List<GameObject>();
        [SerializeField] RectTransform flightPanel;
        [SerializeField] RectTransform flightSystems,fuelBar,energyBar,throttleBar;
        [SerializeField] TextMeshProUGUI systemData,orientationData;
        [SerializeField] AttitudeIndicator attitude;
        [SerializeField] GameObject scaleDown,scaleUp,warpControl;
        [SerializeField] TextMeshProUGUI aeroReadout;
        [SerializeField] TextMeshProUGUI controlHint;
        [SerializeField] RectTransform navigation,mapTools;
        [SerializeField] GameObject flightMenu,atmosphericPanel,ascentButton,recoveryButton;
        [SerializeField] GameObject homeContinue;
        [SerializeField] TextMeshProUGUI mapHeading;
        LabButton[] flightBindings;
        Action dialogClosed;
        bool navigationExpanded,mapView;
        bool wasFlying;
        Transform vrCamera;
        PartCategory category;
        public bool ModalOpen=>modal!=null||(homePanel!=null&&homePanel.gameObject.activeSelf);
        public bool MapView=>mapView;

        public void ConfigureVR(Camera camera)
        {
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1.15f;
            vrCamera=camera.transform;
            root.SetParent(null,false);root.localPosition=Vector3.zero;root.localRotation=Quaternion.identity;
            root.sizeDelta=new Vector2(1920,1080);root.localScale=Vector3.one;
            PlaceVRPanel(homePanel,44,154);
            var homeBackground=homePanel.GetComponent<Image>();
            homeBackground.color=new Color(.025f,.035f,.04f,1f);
            PlaceVRPanel(left,24,156);
            PlaceVRPanel(right,1572,156);
            PlaceVRPanel(flightPanel,24,156);
            PlaceVRPanel(flightSystems,1572,156);
            PlaceVRPanel(bottom,373,840);
            PlaceVRPanel(flightActions,373,840);
            var navigation=root.Find("Navigation") as RectTransform;
            if(navigation!=null) navigation.gameObject.SetActive(false);
            if(mapTools!=null) PlaceVRPanel(mapTools,373,771);
            if(flightMenu!=null) PlaceVRPanel(flightMenu.GetComponent<RectTransform>(),1720,20);
            PlaceVRPanel(toast.rectTransform,430,220);
            if(controlHint!=null)
            {
                PlaceVRPanel(controlHint.rectTransform,1070,1045);
                controlHint.alignment=TextAlignmentOptions.Center;
            }
            phase.gameObject.SetActive(false);status.gameObject.SetActive(false);
            missionTitle.gameObject.SetActive(false);missionGoal.gameObject.SetActive(false);
            foreach(var label in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if(label.text.Contains("ОПЫТНОЕ КОНСТРУКТОРСКОЕ БЮРО")||label.text.Contains("01    /    СБОРКА")||label.text.Contains("ВНИМАНИЕ\nИСПЫТАТЕЛЬНАЯ ЗОНА"))
                    label.gameObject.SetActive(false);
            }
            GetComponent<GraphicRaycaster>().enabled=false;
        }

        public void SetVRObserver()
        {
            if(canvas.renderMode!=RenderMode.WorldSpace||vrCamera==null) return;
            root.SetPositionAndRotation(vrCamera.position,vrCamera.rotation);
        }

        void PlaceVRPanel(RectTransform panel,float x,float y)
        {
            panel.localRotation=Quaternion.identity;panel.localScale=Vector3.one;
            panel.anchoredPosition=new Vector2(x,-y);
        }

        public void SetAssemblyPanelsVisible(bool visible)
        {
            if(!visible)
            {
                foreach(var panel in new[]{left,right,bottom}) if(panel!=null) panel.gameObject.SetActive(false);
                return;
            }
            if(!ModalOpen) SetFlight(game.IsFlying);
        }

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
            var header=UiTheme.Panel("Navigation",root,24,20,1872,74,UiTheme.Ink);navigation=header;
            UiTheme.Text(header,"БРП",24,10,88,52,34,UiTheme.TextColor);
            UiTheme.Panel("BrandAccent",header,116,20,3,32,UiTheme.Orange);
            UiTheme.Text(header,"ВЕКТОР ТЯГИ",138,15,350,30,19);
            UiTheme.Text(header,"ЭКСПЕРИМЕНТАЛЬНАЯ ЛАБОРАТОРИЯ",138,43,400,22,14,UiTheme.Muted);
            string[] links={"Программа","Чертежи","Журнал","Справка","Настройки"};
            string[] commands={"missions","blueprints","records","help","settings"};
            for(int i=0;i<links.Length;i++) UiTheme.Command(header,links[i],960+i*155,17,145,40,commands[i]);
            UiTheme.Command(header,"Меню",1755,17,96,40,"home");
            flightMenu=UiTheme.Command(root,"Панели",1720,20,176,56,"flight_panels").gameObject;
            phase=UiTheme.Text(root,"АНГАР 01     /     СБОРКА",42,112,880,32,18,UiTheme.Cyan);
            scaleDown=UiTheme.Command(root,"−",922,107,40,38,"scale",-1).gameObject;
            scaleUp=UiTheme.Command(root,"+",972,107,40,38,"scale",1).gameObject;
            status=UiTheme.Text(root,"ГОТОВ К РАБОТЕ",1250,112,620,30,16,UiTheme.Muted);status.alignment=TextAlignmentOptions.Right;
            left=UiTheme.Panel("PartsLibrary",root,24,156,324,764);
            UiTheme.Text(left,"КОМПОНЕНТЫ",20,18,280,32,20);
            UiTheme.Text(left,"50 модулей • выберите деталь",20,55,280,28,16,UiTheme.Muted);
            string[] tabs={"Корпус","Двигатели","Управление","Электрика","Груз"};
            for(int i=0;i<5;i++) UiTheme.Command(left,tabs[i],14+(i%2)*150,92+(i/2)*43,144,38,"category",i);
            UiTheme.Divider(left,18,228,288);
            catalogContent=UiTheme.Scroll(left,"Parts",12,244,300,498,out var scroll);
            right=UiTheme.Panel("CraftInspector",root,1572,156,324,764);
            UiTheme.Text(right,"ИНЖЕНЕРНЫЙ РАСЧЁТ",22,20,280,29,18);
            craftValues=new TextMeshProUGUI[4];
            string[] labels={"МАССА / Т","ТЯГА / КН","ТЯГА К ВЕСУ","ЗАПАС ΔV / М/С"};
            for(int i=0;i<4;i++)
            {
                float x=22+(i%2)*148,y=82+(i/2)*104;
                UiTheme.Text(right,labels[i],x,y,138,25,14,UiTheme.Muted);
                craftValues[i]=UiTheme.Text(right,"0",x,y+26,138,52,34);
            }
            stats=UiTheme.Text(right,"0 деталей",22,303,280,28,17,UiTheme.Cyan);
            UiTheme.Divider(right,22,343,280);
            detailTitle=UiTheme.Text(right,"Выберите компонент",22,370,280,75,23);
            detailTitle.raycastTarget=true;
            var passportButton=detailTitle.gameObject.AddComponent<UnityEngine.UI.Button>();passportButton.targetGraphic=detailTitle;
            detailTitle.gameObject.AddComponent<LabButton>().command="passport";
            detailText=UiTheme.Text(right,"Паспорт детали, масса и назначение появятся здесь.",22,451,280,144,18,UiTheme.Muted);
            UiTheme.Command(right,"Разблокировать",20,610,190,42,"detach");
            UiTheme.Command(right,"Удалить",220,610,86,42,"delete");
            UiTheme.Command(right,"Симметрия ×4",20,665,286,38,"symmetry");
            UiTheme.Text(right,"СТУПЕНЬ",20,718,79,24,14,UiTheme.Muted);
            for(int i=0;i<6;i++) UiTheme.Command(right,(i+1).ToString(),101+i*34,711,29,31,"stage",i);
            bottom=UiTheme.Panel("AssemblyTools",root,373,947,1174,76,UiTheme.Ink);
            UiTheme.Command(bottom,"Отмена",14,15,90,45,"undo");
            UiTheme.Command(bottom,"Повтор",111,15,90,45,"redo");
            UiTheme.Command(bottom,"Чертёж",208,15,109,45,"presets");
            UiTheme.Command(bottom,"Очистить",324,15,108,45,"clear");
            UiTheme.Command(bottom,"Сохранить",439,15,128,45,"save");
            UiTheme.Command(bottom,"Облик",574,15,92,45,"paint");
            UiTheme.Command(bottom,"Испытать",673,15,151,45,"stand");
            UiTheme.Command(bottom,"НА КОСМОДРОМ  →",838,11,321,53,"launch",0,true);
            missionTitle=UiTheme.Text(root,"СВОБОДНЫЙ КОНСТРУКТОР",380,161,430,82,22);
            missionGoal=UiTheme.Text(root,"Соберите и испытайте свою конструкцию.",381,254,420,120,19,UiTheme.Muted);
            toast=UiTheme.Text(root,"",430,884,1060,44,21,UiTheme.Cyan);toast.alignment=TextAlignmentOptions.Center;
            UiTheme.Text(root,"BRP    /    65 ЛЕТ ПЕРВОМУ ПОЛЁТУ",28,1045,540,25,14,UiTheme.Muted);
            controlHint=UiTheme.Text(root,"ЛКМ  Выбор     ПКМ  Обзор     R  Поворот     Ctrl+Z  Отмена",1070,1045,820,25,15,UiTheme.Muted);
            controlHint.alignment=TextAlignmentOptions.Right;
            CreateFlightControls();BuildHome();SetFlight(false);
        }

        void BuildHome()
        {
            homePanel=UiTheme.Panel("MainMenu",root,44,154,650,800,new Color(.025f,.035f,.04f,.98f));
            UiTheme.Text(homePanel,"КОНСТРУКТОРСКОЕ БЮРО / 01",36,34,570,28,16,UiTheme.Cyan);
            UiTheme.Text(homePanel,"ВЕКТОР\nТЯГИ",30,108,590,210,82);
            UiTheme.Text(homePanel,"ЛАБОРАТОРИЯ BRP",36,334,570,40,23,UiTheme.Muted);
            UiTheme.Divider(homePanel,36,402,570);
            UiTheme.Text(homePanel,"От первого двигателя до орбиты.\nСборка. Выведение. Возвращение.",36,430,570,70,21);
            homeContinue=UiTheme.Command(homePanel,"Продолжить программу  →",36,535,570,58,"campaign",0,true).gameObject;
            UiTheme.Command(homePanel,"Свободный режим",36,608,276,44,"sandbox");
            UiTheme.Command(homePanel,"Сборка на время",330,608,276,44,"trial");
            UiTheme.Command(homePanel,"Лунная экспедиция",36,710,276,42,"lunar_menu");
            UiTheme.Command(homePanel,"Завершить игру",330,710,276,42,"quit");
        }

        void CreateFlightControls()
        {
            flightSystems=UiTheme.Panel("FlightSystems",root,1572,156,324,764);
            UiTheme.Text(flightSystems,"ОРИЕНТАЦИЯ И СИСТЕМЫ",22,20,280,29,17);
            var ball=UiTheme.Rect("Attitude",flightSystems,56,70,212,212);
            attitude=ball.gameObject.AddComponent<AttitudeIndicator>();attitude.raycastTarget=false;
            orientationData=UiTheme.Text(flightSystems,"ТАНГАЖ 90°",22,295,280,28,17,UiTheme.Cyan);orientationData.alignment=TextAlignmentOptions.Center;
            string[] gauges={"ТЯГА","ТОПЛИВО","ПИТАНИЕ"};
            RectTransform[] bars=new RectTransform[3];
            for(int i=0;i<3;i++)
            {
                UiTheme.Text(flightSystems,gauges[i],22,346+i*45,110,25,15,UiTheme.Muted);
                var track=UiTheme.Panel(gauges[i],flightSystems,134,351+i*45,166,5,new Color(.13f,.17f,.19f));
                bars[i]=UiTheme.Panel("Fill",track,0,0,166,5,i==0?UiTheme.Orange:UiTheme.Cyan);
            }
            throttleBar=bars[0];fuelBar=bars[1];energyBar=bars[2];
            systemData=UiTheme.Text(flightSystems,"",22,500,280,116,18,UiTheme.Muted);
            ascentButton=UiTheme.Command(flightSystems,"Программа выведения",20,635,284,43,"ascent",0,true).gameObject;
            recoveryButton=UiTheme.Command(flightSystems,"Возвращение капсулы",20,691,284,43,"recover").gameObject;
            warpControl=UiTheme.Command(root,"Время ×  /  T",365,107,170,38,"warp").gameObject;
            flightPanel=UiTheme.Panel("FlightReadout",root,24,156,324,574);
            UiTheme.Text(flightPanel,"ПОЛЁТНЫЕ ДАННЫЕ",22,20,280,28,18);
            flightValues=new TextMeshProUGUI[4];
            flightLabels=new TextMeshProUGUI[4];
            string[] labels={"ВЫСОТА / КМ","СКОРОСТЬ / М/С","АПОГЕЙ / КМ","ПЕРИГЕЙ / КМ"};
            for(int i=0;i<4;i++)
            {
                float y=78+i*95;
                flightLabels[i]=UiTheme.Text(flightPanel,labels[i],22,y,280,25,16,UiTheme.Muted);
                flightValues[i]=UiTheme.Text(flightPanel,"0.0",22,y+27,280,57,42,i<2?UiTheme.TextColor:UiTheme.Cyan);
            }
            flightData=UiTheme.Text(flightPanel,"",22,458,280,35,16,UiTheme.Muted);
            UiTheme.Command(flightPanel,"Тяга −",20,508,85,42,"throttle",-1);
            UiTheme.Command(flightPanel,"Стоп",117,508,85,42,"throttle",0);
            UiTheme.Command(flightPanel,"Тяга +",214,508,90,42,"throttle",1);
            var aeroPanel=UiTheme.Panel("Atmospheric Flight",root,24,750,324,170);
            atmosphericPanel=aeroPanel.gameObject;
            aeroPanel.SetParent(flightPanel,true);
            UiTheme.Text(aeroPanel,"АТМОСФЕРНЫЙ РЕЖИМ",22,16,280,25,16,UiTheme.Cyan);
            aeroReadout=UiTheme.Text(aeroPanel,"M 0.00",22,51,280,105,19);
            flightActions=UiTheme.Panel("FlightCommands",root,373,947,1174,76,UiTheme.Ink);
            UiTheme.Command(flightActions,"СТУПЕНЬ  /  Space",14,12,240,52,"next_stage",0,true);
            UiTheme.Command(flightActions,"Отделить груз",266,15,172,45,"payload");
            UiTheme.Command(flightActions,"Парашют",450,15,144,45,"parachute");
            UiTheme.Command(flightActions,"Карта",606,15,125,45,"map");
            UiTheme.Command(flightActions,"Управление",743,15,202,45,"flight_controls");
            UiTheme.Command(flightActions,"В ангар",957,15,201,45,"workshop");
            BuildMapTools();
        }

        void BuildMapTools()
        {
            mapTools=UiTheme.Panel("Celestial Navigation",root,373,771,1174,150,UiTheme.Ink);
            mapHeading=UiTheme.Text(mapTools,"НАВИГАЦИЯ / ЗЕМЛЯ — ЛУНА",16,8,1100,24,18,UiTheme.Cyan);
            UiTheme.Command(mapTools,"Своя орбита",14,38,276,44,"map_body",0);
            UiTheme.Command(mapTools,"Земля — Луна",304,38,276,44,"map_body",1);
            UiTheme.Command(mapTools,"Солнечная система",594,38,276,44,"map_body",2);
            UiTheme.Command(mapTools,"Команды",884,38,276,44,"flight_controls");
            UiTheme.Command(mapTools,"К полёту",14,94,276,44,"map",0,true);
            UiTheme.Command(mapTools,"Лунная экспедиция",304,94,414,44,"lunar_expedition");
            UiTheme.Command(mapTools,"Тренировка посадки / 5 км",732,94,428,44,"lunar_practice");
        }

        public void ToggleFlightNavigation()
        {
            navigationExpanded=!navigationExpanded;SetFlight(game.IsFlying);
        }

        public void SetMapView(bool active)
        {
            mapView=active;
            if(flightPanel!=null) flightPanel.gameObject.SetActive(game.IsFlying&&(!active||game.MapMode==0));
            if(mapHeading!=null) mapHeading.text=game.MapMode==2?"НАВИГАЦИЯ / СОЛНЕЧНАЯ СИСТЕМА":game.MapMode==1?"НАВИГАЦИЯ / ЗЕМЛЯ — ЛУНА":"НАВИГАЦИЯ / ОРБИТА АППАРАТА";
            if(mapTools!=null) mapTools.gameObject.SetActive(game.IsFlying&&active);
            if(flightSystems!=null) flightSystems.gameObject.SetActive(game.IsFlying&&!active);
            if(flightActions!=null) flightActions.gameObject.SetActive(game.IsFlying&&!active);
            if(atmosphericPanel!=null) atmosphericPanel.SetActive(game.IsFlying&&!active&&game.Flight!=null&&!game.Flight.IsLunar&&game.Flight.Altitude<80000);
        }

        public void ShowLunarExpedition()
        {
            var r=Dialog("ЛУННАЯ ЭКСПЕДИЦИЯ",1040,600);
            UiTheme.Text(r,"01 / ОРБИТА ЛУНЫ",35,110,960,36,25,UiTheme.Cyan);
            UiTheme.Text(r,"Сценарий начнётся с лунной орбиты. Подготовьте двигатель и запас топлива для торможения, спуска и обратного взлёта.",35,169,960,110,27);
            UiTheme.Text(r,"На Луне нет атмосферы: парашюты и аэродинамические рули не помогут. Уменьшайте вертикальную скорость двигателем. Контролируйте топливо, высоту и наклон.",35,303,960,110,23,UiTheme.Muted);
            UiTheme.Button(r,"Тренировка с высоты 5 км",35,467,455,65,()=>{CloseDialog();game.StartLunarPractice();});
            UiTheme.Button(r,"Начать с орбиты Луны",510,467,495,65,()=>{CloseDialog();game.BeginLunarTransfer();},true);
        }

        public void SetFlight(bool active)
        {
            if(active&&!wasFlying) navigationExpanded=false;
            wasFlying=active;
            left.gameObject.SetActive(!active);right.gameObject.SetActive(!active);bottom.gameObject.SetActive(!active);
            flightPanel.gameObject.SetActive(active);flightActions.gameObject.SetActive(active);
            flightSystems.gameObject.SetActive(active);
            if(navigation!=null) navigation.gameObject.SetActive(!game.VREnabled&&(!active||navigationExpanded));
            if(flightMenu!=null)
            {
                flightMenu.SetActive(active&&!game.VREnabled);flightMenu.GetComponentInChildren<TextMeshProUGUI>().text=navigationExpanded?"Скрыть панели":"Панели";
            }
            if(!game.VREnabled)
            {
                phase.rectTransform.anchoredPosition=active?new Vector2(24,-24):new Vector2(42,-112);
                status.rectTransform.anchoredPosition=active?new Vector2(24,-68):new Vector2(1250,-112);
                status.alignment=active?TextAlignmentOptions.Left:TextAlignmentOptions.Right;
                if(flightMenu!=null) flightMenu.GetComponent<RectTransform>().anchoredPosition=new Vector2(1720,navigationExpanded?-107:-20);
            }
            if(scaleDown!=null) scaleDown.SetActive(!active);
            if(scaleUp!=null) scaleUp.SetActive(!active);
            if(warpControl!=null) warpControl.SetActive(active&&!navigationExpanded);
            if(controlHint!=null) controlHint.text=game.VREnabled?"ЛУЧ  Кнопки     ХВАТ  Детали":active?"Shift / Ctrl  Тяга     Space  Ступень     M  Карта     T  Время":"ЛКМ  Выбор     ПКМ  Обзор     R  Поворот     Ctrl+Z  Отмена";
            missionTitle.gameObject.SetActive(!active&&!game.VREnabled);missionGoal.gameObject.SetActive(!active&&!game.VREnabled);
            phase.text=active?"КОСМОДРОМ     /     ПОЛЁТНЫЕ ИСПЫТАНИЯ":"АНГАР 01     /     ИНЖЕНЕРНАЯ СБОРКА";
            SetMapView(active&&mapView);
        }

        public void ShowFlightToolbar()
        {
            var r=Dialog("УПРАВЛЕНИЕ ПОЛЁТОМ",1000,850);
            string[] modes={"Вручную","Удерживать","По скорости","Против скорости","Радиально","Нормаль","На цель"};
            for(int i=0;i<modes.Length;i++) {int mode=i;UiTheme.Button(r,modes[i],35+(i%3)*312,96+(i/3)*54,300,44,()=>{game.SetGuidance((GuidanceMode)mode);CloseDialog();});}
            string controls=game.VREnabled?"Правый триггер + стик: наклон. Правый хват + стик: тяга.\nЛевый триггер + стик: двигатели сближения. A: ступень. B: парашют.\nX: карта. Y: команды. Панели выбираются лучом контроллера.":"Shift / Ctrl: тяга. X: отключить. W/S, A/D: наклон. Q/E: крен.\nI/K, J/L, U/O: двигатели сближения. Space: ступень. P: парашют.\nM: карта. T: время. F: команды. Backspace: спасение.";
            UiTheme.Text(r,controls,35,280,930,110,20,UiTheme.Muted);
            UiTheme.Button(r,"Орбитальный стенд",35,410,453,46,()=>{CloseDialog();game.StartOrbitalPractice();});
            UiTheme.Button(r,"Стыковать",502,410,463,46,()=>{CloseDialog();game.TryDock();});
            UiTheme.Button(r,"Тормозной манёвр",35,468,453,46,()=>{game.PlanDeorbit();CloseDialog();});
            UiTheme.Button(r,"Карта / траектория",502,468,463,46,()=>{game.ToggleMap();CloseDialog();});
            UiTheme.Button(r,"Помощник сближения",35,526,453,46,()=>{game.ToggleRendezvous();CloseDialog();});
            UiTheme.Button(r,"Топливный клапан",502,526,463,46,()=>{game.ToggleValve();CloseDialog();});
            UiTheme.Button(r,"Отделить капсулу",35,584,453,46,()=>{game.SeparateCapsule();CloseDialog();});
            UiTheme.Button(r,"Аварийное спасение",502,584,463,46,()=>{game.Abort();CloseDialog();});
            UiTheme.Button(r,"Лунная экспедиция",35,656,453,52,()=>{CloseDialog();game.BeginLunarTransfer();});
            UiTheme.Button(r,"Тренировка посадки / 5 км",502,656,463,52,()=>{CloseDialog();game.StartLunarPractice();});
            UiTheme.Text(r,"Экспедиция начинается с орбиты Луны. Посадка и взлёт рассчитываются в её гравитации.",35,727,930,60,20,UiTheme.Muted);
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

                UiTheme.Text(card,d.title,82,10,190,48,19);
                UiTheme.Text(card,$"{d.dryMass:N0} КГ / {(d.surface?"БОКОВОЙ":"ОСЕВОЙ")}",82,64,190,24,14,UiTheme.Muted);
                card.gameObject.AddComponent<CatalogCard>().definition=d;
            }
            catalogContent.sizeDelta=new Vector2(286,parts.Length*100);
            catalogContent.anchoredPosition=Vector2.zero;
        }

        public void ShowDefinition(PartDefinition d)
        {
            detailTitle.text=d.title+"  ›";
            detailText.text=d.description+$"\n\nМасса: {d.dryMass:N0} кг"+(d.thrust>0?$"\nТяга: {d.thrust/1000:N0} кН · Isp {d.specificImpulse:N0} с":"")+(d.fuelCapacity+d.oxidizerCapacity>0?$"\nЗапас: {d.fuelCapacity+d.oxidizerCapacity:N0} кг":"");
        }

        public void ShowSelectedPassport()
        {
            var selected=game.Selected;
            if(selected==null) {game.Toast("Выберите модуль на верстаке");return;}
            var definition=selected.Definition;var r=Dialog(definition.title.ToUpperInvariant(),1080,800);
            if(definition.thumbnail!=null)
            {
                var icon=UiTheme.Panel("Module photograph",r,35,118,235,235,Color.white);
                var image=icon.GetComponent<Image>();image.sprite=definition.thumbnail;image.preserveAspect=true;image.raycastTarget=false;
            }
            UiTheme.Text(r,"НАЗНАЧЕНИЕ",310,116,730,30,20,UiTheme.Cyan);
            UiTheme.Text(r,definition.description,310,163,730,170,26);
            UiTheme.Divider(r,35,382,1010);
            string basic=$"Сухая масса   {definition.dryMass:N0} кг\nДлина   {definition.height:F2} м\nДиаметр   {definition.radius*2:F2} м\nСтупень   {selected.Placement.stage+1}\nКрепление   {(definition.surface?"боковое":"осевое")}";
            string systems=$"Тяга   {definition.thrust/1000:N1} кН\nУдельный импульс   {definition.specificImpulse:N0} с\nТопливо + окислитель   {definition.fuelCapacity+definition.oxidizerCapacity:N0} кг\nЭлектрический запас   {definition.electricCapacity/1000:F1} кДж\nПредельная температура   {definition.temperatureLimit:N0} К";
            UiTheme.Text(r,basic,35,425,475,245,25);UiTheme.Text(r,systems,550,425,495,245,25);
            UiTheme.Button(r,"Настроить цвет и маркировку",35,703,1010,58,ShowPaint,true);
        }
        public void ClearSelection()
        {
            detailTitle.text="Выберите компонент";detailText.text="Паспорт детали, масса и назначение появятся здесь.";
        }
        public void UpdateStats()
        {
            var craft=game.Assembly.Blueprint;
            if(craft.parts.Count==0)
            {
                stats.text="0 деталей / 0 типов";foreach(var value in craftValues) value.text="0";
                status.text="ВЕРСТАК СВОБОДЕН • ДОБАВЬТЕ ПЕРВЫЙ МОДУЛЬ";
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
            if(flightBindings==null) flightBindings=flightActions.GetComponentsInChildren<LabButton>(true);
            foreach(var binding in flightBindings)
            {
                if(binding.command=="parachute") binding.GetComponent<Button>().interactable=!body.IsLunar&&(body.Has(PartKind.MainParachute)||body.Has(PartKind.DrogueParachute));
                if(binding.command=="payload") binding.GetComponent<Button>().interactable=body.Has(PartKind.PayloadSeparator);
                if(binding.command=="next_stage") binding.GetComponent<Button>().interactable=!body.IsLunar||body.Has(PartKind.StackDecoupler)||body.Has(PartKind.RadialDecoupler);
            }
            if(atmosphericPanel!=null) atmosphericPanel.SetActive(!mapView&&!body.IsLunar&&body.Altitude<80000);
            if(recoveryButton!=null) recoveryButton.SetActive(!body.IsLunar);
            if(ascentButton!=null) ascentButton.GetComponentInChildren<TextMeshProUGUI>().text=body.IsLunar?"Автопосадка на Луну":"Программа выведения";
            phase.text=mapView?game.MapMode==2?"НАВИГАЦИЯ / СОЛНЕЧНАЯ СИСТЕМА":game.MapMode==1?"НАВИГАЦИЯ / ЗЕМЛЯ — ЛУНА":"НАВИГАЦИЯ / ОРБИТА АППАРАТА":body.IsLunar?"ЛУНА / ПОСАДКА И ВЗЛЁТ":body.Altitude>=80000?"ЗЕМНАЯ ОРБИТА / УПРАВЛЕНИЕ ПОЛЁТОМ":"КОСМОДРОМ / ПОЛЁТНЫЕ ИСПЫТАНИЯ";
            var orbit=body.Orbit;
            string Format(double value)=>double.IsInfinity(value)?"∞":(value/1000).ToString("F1");
            bool meters=body.IsLunar||body.Altitude<2000;
            double altitude=body.IsLunar?body.GroundAltitude:body.Altitude;
            if(flightLabels!=null&&flightLabels.Length>0) flightLabels[0].text=meters?"ВЫСОТА / М":"ВЫСОТА / КМ";
            flightValues[0].text=meters?altitude.ToString(altitude<10?"F1":"F0"):(altitude/1000).ToString("F2");flightValues[1].text=body.SurfaceVelocity.Magnitude.ToString("F0");
            bool landing=body.IsLunar&&body.GroundAltitude<10000&&!mapView;
            if(flightLabels!=null&&flightLabels.Length>3)
            {
                flightLabels[2].text=landing?"СНИЖЕНИЕ / М/С":"АПОГЕЙ / КМ";
                flightLabels[3].text=landing?"БОКОВАЯ / М/С":"ПЕРИГЕЙ / КМ";
            }
            flightValues[2].text=landing?(-body.VerticalSpeed).ToString("F1"):Format(orbit.apoapsis);
            flightValues[3].text=landing?(body.SurfaceVelocity-body.position.Normalized*body.VerticalSpeed).Magnitude.ToString("F1"):Format(orbit.periapsis);
            flightData.text=body.IsLunar?$"T+{body.elapsed:0000}с  Vверт {body.VerticalSpeed:F1} м/с":$"T+{body.elapsed:0000}с   Тяга {body.throttle*100:F0}%   {body.Temperature:F0}К";
            if(aeroReadout!=null)
            {
                string regime=body.Altitude>=80000?"ВНЕ АТМОСФЕРЫ":body.Mach>=5?"ГИПЕРЗВУК":body.Mach>=1.2?"СВЕРХЗВУК":body.Mach>=.8?"ТРАНСЗВУК":"ДОЗВУК";
                string mach=body.Altitude>=80000?"—":body.Mach.ToString("F2");
                aeroReadout.text=$"{regime}\nM {mach}    q {body.dynamicPressure/1000:F1} кПа\nНагрев {body.Temperature:F0} К";
                aeroReadout.color=body.Mach>=.8&&body.Mach<1.2?UiTheme.Orange:UiTheme.TextColor;
            }
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
            if(game.VREnabled) SetAssemblyPanelsVisible(false);
            var panel=UiTheme.Panel("Dialog",modal,(1920-width)/2,(942-height)/2,width,height,UiTheme.Ink);
            UiTheme.Panel("Accent",panel,0,0,4,60,UiTheme.Orange);
            UiTheme.Text(panel,title,30,24,width-100,40,26);
            UiTheme.Button(panel,"×",width-62,20,40,40,CloseDialog);
            UiTheme.Divider(panel,30,76,width-60);return panel;
        }
        public void CloseDialog()
        {
            var release=dialogClosed;dialogClosed=null;release?.Invoke();
            navigationExpanded=false;
            if(modal!=null) {Destroy(modal.gameObject);modal=null;}
            if(homePanel!=null) homePanel.gameObject.SetActive(false);
            if(left!=null) SetFlight(game.IsFlying);
        }
        public void ShowHome()
        {
            CloseDialog();
            foreach(var label in homePanel.GetComponentsInChildren<TextMeshProUGUI>(true))
                if(label.text=="VR определяется автоматически при запуске") Destroy(label.gameObject);
            homePanel.gameObject.SetActive(true);
            foreach(var panel in new[]{left,right,bottom,flightPanel,flightSystems,flightActions,mapTools})
                if(panel!=null) panel.gameObject.SetActive(false);
            if(homeContinue!=null) homeContinue.GetComponentInChildren<TextMeshProUGUI>().text=game.IsFlying?"Продолжить полёт  →":"Продолжить программу  →";
            if(!game.VREnabled)
            {
                phase.rectTransform.anchoredPosition=new Vector2(42,-112);
                status.rectTransform.anchoredPosition=new Vector2(1250,-112);status.alignment=TextAlignmentOptions.Right;
            }
            phase.text=game.IsFlying?"ПРОГРАММА BRP / ПАУЗА":"АНГАР 01 / ГОТОВ К РАБОТЕ";
            left.gameObject.SetActive(false);right.gameObject.SetActive(false);bottom.gameObject.SetActive(false);
            missionTitle.gameObject.SetActive(false);missionGoal.gameObject.SetActive(false);
            flightPanel.gameObject.SetActive(false);flightActions.gameObject.SetActive(false);
            flightSystems.gameObject.SetActive(false);
            if(scaleDown!=null) scaleDown.SetActive(false);
            if(scaleUp!=null) scaleUp.SetActive(false);
            if(warpControl!=null) warpControl.SetActive(false);
            if(navigation!=null) navigation.gameObject.SetActive(true);
            if(flightMenu!=null) flightMenu.SetActive(false);
            if(mapTools!=null) mapTools.gameObject.SetActive(false);
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
            var r=Dialog("ЭТАЛОННЫЕ КОНСТРУКЦИИ",780,520);
            string[] titles={"БРП-01 / Учебная","БРП-02 / Орбитальная","БРП-03 / Возвращаемая","БРП-Л / Лунный посадочный модуль"};
            for(int i=0;i<titles.Length;i++) {int level=i;UiTheme.Button(r,titles[i],35,110+i*78,710,60,()=>{game.LoadPreset(level);CloseDialog();});}
        }
        public void ShowPaint()
        {
            var target=game.Selected;
            if(target==null) target=game.Assembly.views.FirstOrDefault(v=>v.Attached);
            if(target==null) {game.Toast("Сначала установите хотя бы один модуль");return;}
            var r=Dialog("МАСТЕРСКАЯ ОБЛИКА",1140,900);
            var draft=JsonUtility.FromJson<PlacedPart>(JsonUtility.ToJson(target.Placement));
            bool all=game.Selected==null,accent=false;
            var previewRect=UiTheme.Rect("Finish Preview",r,30,155,460,460);
            var image=previewRect.gameObject.AddComponent<RawImage>();image.raycastTarget=false;
            var preview=previewRect.gameObject.AddComponent<FinishPreview>();preview.Initialize(target.Definition,draft,image);
            UiTheme.Text(r,target.Definition.title,30,105,460,40,25);
            var scope=UiTheme.Button(r,all?"Цвет: весь корпус":"Цвет: выбранный модуль",30,632,460,46,null);
            var channels=new Slider[3];var values=new TextMeshProUGUI[3];
            void Refresh()
            {
                preview.Refresh();
                foreach(var view in game.Assembly.views)
                {
                    if(!view.Attached||(!all&&view!=target)) continue;
                    var candidate=JsonUtility.FromJson<PlacedPart>(JsonUtility.ToJson(view.Placement));
                    candidate.color=draft.color;candidate.accentColor=draft.accentColor;candidate.paintSmoothness=draft.paintSmoothness;
                    if(view==target) {candidate.decalId=draft.decalId;candidate.decalText=draft.decalText;candidate.decalAngle=draft.decalAngle;candidate.decalScale=draft.decalScale;}
                    RocketFinish.Apply(view.gameObject,candidate,view.Definition);
                }
                var color=accent?draft.accentColor:draft.color;
                for(int i=0;i<3;i++) if(channels[i]!=null) {channels[i].SetValueWithoutNotify(color[i]);values[i].text=Mathf.RoundToInt(color[i]*255).ToString();}
            }
            void Restore()
            {
                foreach(var view in game.Assembly.views) if(view!=null) view.ApplyColor();
            }
            scope.onClick.AddListener(()=>{Restore();all=!all;scope.GetComponentInChildren<TextMeshProUGUI>().text=all?"Цвет: весь корпус":"Цвет: выбранный модуль";Refresh();});
            UiTheme.Button(r,"Основной цвет",520,105,284,42,()=>{accent=false;Refresh();},true);
            UiTheme.Button(r,"Цвет акцентов",816,105,294,42,()=>{accent=true;Refresh();});
            Color[] palette={new Color(.85f,.89f,.9f),new Color(.06f,.18f,.28f),new Color(.93f,.29f,.07f),new Color(.88f,.82f,.66f),new Color(.12f,.4f,.36f),new Color(.08f,.09f,.11f),new Color(.6f,.12f,.15f),new Color(.3f,.42f,.6f),new Color(.5f,.31f,.58f),new Color(.86f,.67f,.12f),new Color(.4f,.44f,.46f),new Color(.22f,.62f,.65f)};
            for(int i=0;i<palette.Length;i++)
            {
                var color=palette[i];var button=UiTheme.Button(r,"",520+(i%6)*100,163+(i/6)*66,88,56,()=>{if(accent) draft.accentColor=color;else draft.color=color;Refresh();});
                button.GetComponent<Image>().color=color;
            }
            string[] labels={"R","G","B"};
            for(int i=0;i<3;i++)
            {
                int index=i;float y=310+i*44;UiTheme.Text(r,labels[i],520,y,35,30,21,UiTheme.Muted);
                channels[i]=UiTheme.Slider(r,563,y,452,30,draft.color[i]);values[i]=UiTheme.Text(r,"0",1035,y,75,30,21);
                channels[i].onValueChanged.AddListener(value=>{var color=accent?draft.accentColor:draft.color;color[index]=value;if(accent) draft.accentColor=color;else draft.color=color;Refresh();});
            }
            UiTheme.Text(r,"ПОКРЫТИЕ",520,445,590,24,18,UiTheme.Cyan);
            string[] finishes={"Матовый","Сатин","Металлик"};float[] smoothness={.12f,.38f,.72f};
            for(int i=0;i<3;i++) {float value=smoothness[i];UiTheme.Button(r,finishes[i],520+i*200,478,190,44,()=>{draft.paintSmoothness=value;Refresh();});}
            UiTheme.Text(r,"НАКЛЕЙКА НА МОДУЛЕ",520,546,590,26,18,UiTheme.Cyan);
            string[] decals={"","brp","flag","01","hazard","orbit"};string[] names={"Без наклейки","БРП","Флаг","01","Предупреждение","Орбита"};
            for(int i=0;i<decals.Length;i++) {string id=decals[i];UiTheme.Button(r,names[i],520+(i%3)*200,582+(i/3)*48,190,42,()=>{draft.decalId=id;Refresh();});}
            var inputRect=UiTheme.Panel("Module inscription",r,520,697,444,50,new Color(.09f,.13f,.16f));
            var input=inputRect.gameObject.AddComponent<TMP_InputField>();var text=UiTheme.Text(inputRect,"",14,4,416,42,24);text.richText=false;
            input.textViewport=inputRect;input.textComponent=text;input.targetGraphic=inputRect.GetComponent<Image>();input.characterLimit=24;input.lineType=TMP_InputField.LineType.SingleLine;
            var placeholder=UiTheme.Text(inputRect,"Позывной модуля",14,4,416,42,22,UiTheme.Muted);input.placeholder=placeholder;
            input.SetTextWithoutNotify(draft.decalText??"");input.onValueChanged.AddListener(value=>{draft.decalText=value;Refresh();});
            UiTheme.Button(r,"Клавиши",976,697,134,50,()=>ShowTextKeyboard(r,input));
            UiTheme.Text(r,"Надпись и эмблема сохраняются в чертеже. Стекло, приборы и солнечные элементы сохраняют свой материал.",520,765,590,55,18,UiTheme.Muted);
            UiTheme.Text(r,"Поворот",30,700,125,30,20,UiTheme.Muted);var angle=UiTheme.Slider(r,170,700,320,30,draft.decalAngle/360);angle.onValueChanged.AddListener(value=>{draft.decalAngle=value*360;Refresh();});
            UiTheme.Text(r,"Размер",30,752,125,30,20,UiTheme.Muted);var scale=UiTheme.Slider(r,170,752,320,30,(draft.decalScale-.3f)/.9f);scale.onValueChanged.AddListener(value=>{draft.decalScale=.3f+value*.9f;Refresh();});
            dialogClosed=Restore;
            UiTheme.Button(r,"Отменить изменения",30,833,320,48,CloseDialog);
            UiTheme.Button(r,"Сохранить облик",370,833,740,48,()=>
            {
                if(target==null) {CloseDialog();return;}
                Restore();game.Assembly.Paint(all?null:target,draft.color);
                foreach(var view in game.Assembly.views)
                {
                    if(!view.Attached||(!all&&view!=target)) continue;
                    view.Placement.accentColor=draft.accentColor;view.Placement.paintSmoothness=draft.paintSmoothness;
                    if(view==target) {view.Placement.decalId=draft.decalId;view.Placement.decalText=draft.decalText;view.Placement.decalAngle=draft.decalAngle;view.Placement.decalScale=draft.decalScale;}
                    view.ApplyColor();
                }
                dialogClosed=null;game.Sound.Play(SoundCue.Click);CloseDialog();
            },true);
            Refresh();
        }

        void ShowTextKeyboard(RectTransform parent,TMP_InputField input)
        {
            var previous=parent.Find("Marking keyboard");if(previous!=null) Destroy(previous.gameObject);
            var keyboard=UiTheme.Panel("Marking keyboard",parent,30,92,1080,716,UiTheme.Ink);
            UiTheme.Text(keyboard,"ПОЗЫВНОЙ МОДУЛЯ",30,20,1020,36,27,UiTheme.Cyan);
            var entry=UiTheme.Text(keyboard,input.text,30,76,1020,54,32);entry.richText=false;
            var keys=UiTheme.Rect("Keys",keyboard,30,201,1020,340);
            void RefreshEntry() {entry.text=input.text;}
            void BuildKeys(string alphabet)
            {
                for(int i=keys.childCount-1;i>=0;i--) Destroy(keys.GetChild(i).gameObject);
                for(int i=0;i<alphabet.Length;i++)
                {
                    string letter=alphabet[i].ToString();
                    UiTheme.Button(keys,letter,(i%10)*102,(i/10)*65,94,56,()=>{if(input.text.Length<input.characterLimit) input.text+=letter;RefreshEntry();});
                }
            }
            UiTheme.Button(keyboard,"Русский",30,141,240,44,()=>BuildKeys("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ0123456789-"));
            UiTheme.Button(keyboard,"English",286,141,240,44,()=>BuildKeys("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-"));
            UiTheme.Text(keyboard,"До 24 символов • выбор лучом контроллера",550,148,500,32,20,UiTheme.Muted);
            UiTheme.Button(keyboard,"Пробел",30,577,248,56,()=>{if(input.text.Length<input.characterLimit) input.text+=" ";RefreshEntry();});
            UiTheme.Button(keyboard,"Стереть",294,577,248,56,()=>{if(input.text.Length>0) input.text=input.text.Substring(0,input.text.Length-1);RefreshEntry();});
            UiTheme.Button(keyboard,"Очистить",558,577,248,56,()=>{input.text="";RefreshEntry();});
            UiTheme.Button(keyboard,"Готово",822,577,228,56,()=>Destroy(keyboard.gameObject),true);
            UiTheme.Text(keyboard,"Надпись применяется после сохранения облика модуля.",30,659,1020,30,21,UiTheme.Muted);
            BuildKeys("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ0123456789-");
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
            var r=Dialog("НАСТРОЙКИ",820,game.VREnabled?970:850);
            UiTheme.Text(r,"Громкость",35,110,740,30,22);
            UiTheme.Button(r,"Тише",35,160,230,48,()=>game.SetVolume(-.1f));
            UiTheme.Button(r,"Громче",285,160,230,48,()=>game.SetVolume(.1f));
            UiTheme.Button(r,"Без звука",535,160,245,48,()=>game.SetVolume(-1));
            string[] channels={"Интерфейс и механика","Двигатели","Атмосфера ангара"};
            float[] levels={game.Progress.interfaceVolume,game.Progress.engineVolume,game.Progress.environmentVolume};
            for(int i=0;i<3;i++)
            {
                int channel=i;float y=245+i*55;
                UiTheme.Text(r,channels[i],35,y,310,30,20,UiTheme.Muted);
                var slider=UiTheme.Slider(r,350,y,425,30,levels[i]);slider.onValueChanged.AddListener(value=>game.SetAudioChannel(channel,value));
            }
            var instructionAudio=UnityEngine.Object.FindAnyObjectByType<TutorialAudio>();
            if(instructionAudio!=null)
            {
                UiTheme.Text(r,"Голос инструктора",35,417,310,30,20,UiTheme.Muted);
                var voice=UiTheme.Slider(r,350,417,425,30,instructionAudio.VoiceVolume);voice.onValueChanged.AddListener(value=>instructionAudio.VoiceVolume=value);
                UiTheme.Text(r,"Музыка",35,472,310,30,20,UiTheme.Muted);
                var music=UiTheme.Slider(r,350,472,425,30,instructionAudio.MusicVolume);music.onValueChanged.AddListener(value=>instructionAudio.MusicVolume=value);
            }
            UiTheme.Text(r,"Графика",35,540,740,30,22);
            string[] quality={"Производительность","Баланс","Качество"};
            for(int i=0;i<3;i++) {int value=i;UiTheme.Button(r,quality[i],35+i*250,590,240,50,()=>game.SetQuality(value));}
            if(game.VREnabled&&!game.IsFlying)
            {
                UiTheme.Button(r,"Подстроить рабочее место",35,657,745,45,game.CenterVRWorkspace);
                UiTheme.Text(r,"Сядьте или встаньте удобно и посмотрите прямо перед собой.",35,708,745,35,18,UiTheme.Muted);
                var controls=UnityEngine.Object.FindAnyObjectByType<VROperatorControls>();
                if(controls!=null)
                {
                    UiTheme.Text(r,"ПЕРЕМЕЩЕНИЕ В АНГАРЕ",35,748,745,25,18,UiTheme.Cyan);
                    UiTheme.Button(r,controls.SmoothMovement?"Плавный ход: включён":"Плавный ход: выключен",35,783,235,46,controls.ToggleSmoothMovement);
                    UiTheme.Button(r,$"Скорость: {controls.MoveSpeed:F1} м/с",287,783,235,46,controls.CycleMoveSpeed);
                    UiTheme.Button(r,controls.SmoothTurn?"Поворот: плавный":"Поворот: шаговый",535,783,245,46,controls.ToggleTurnMode);
                    UiTheme.Text(r,"Телепортация доступна лучом контроллера. Шаговый поворот снижает укачивание.",35,833,745,25,16,UiTheme.Muted);
                }
            }
            UiTheme.Button(r,"Выйти из игры",35,game.VREnabled?876:740,745,50,()=>Application.Quit());
        }
        public void ShowHelp()
        {
            var r=Dialog("УПРАВЛЕНИЕ И ФИЗИКА",1050,820);
            UiTheme.Text(r,"СБОРКА",35,110,950,30,20,UiTheme.Cyan);
            UiTheme.Text(r,"Перетащите деталь из каталога или нажмите на её карточку.\nПереносите деталь мышью к узлу. Голубое кольцо показывает место фиксации.\nR поворачивает деталь. «Разблокировать» снимает узел. «Симметрия ×4» добавляет боковые копии.\nНомер ступени определяет, когда двигатель включится. Сохраняйте чертежи перед испытанием.",35,155,970,150,18);
            UiTheme.Text(r,"ПОЛЁТ",35,325,950,30,20,UiTheme.Cyan);
            UiTheme.Text(r,"Shift / Ctrl: тяга. X: выключить. Space: следующая ступень.\nW/S, A/D: наклон. Q/E: крен. P: парашют. B: воздушный тормоз.\nM: карта. T: ускорение времени. F: команды автопилота.\nI/K, J/L, U/O: перемещение двигателями ориентации. Backspace: аварийное спасение.",35,370,970,135,18);
            UiTheme.Text(r,"ИНЖЕНЕРНЫЙ РАСЧЁТ",35,530,950,30,20,UiTheme.Cyan);
            UiTheme.Text(r,"Тяга должна превышать вес. Центр масс и центр давления определяют устойчивость.\nТопливо меняет массу в полёте. В вакууме рули не работают, нужны двигатели ориентации.\nДля орбиты нужна скорость вдоль поверхности. Тепловой щит и парашют нужны для возвращения.\nВ VR: хват переносит деталь, луч выбирает панели, левый стик перемещает по ангару, правый поворачивает. Телепортируйтесь лучом.",35,575,970,150,18);
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
