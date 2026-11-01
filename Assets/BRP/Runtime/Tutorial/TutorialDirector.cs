using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BRP
{
    public sealed class TutorialDirector : MonoBehaviour
    {
        LaboratoryGame game;
        TutorialAudio audioGuide;
        RectTransform panel;
        TextMeshProUGUI instruction;
        TutorialFocusPreview focusPreview;
        TutorialFocusContext focusContext;
        LineRenderer marker;
        readonly Vector3[] ring=new Vector3[49];
        List<TutorialStep> steps;
        MissionDefinition mission;
        FlightBody previousFlight;
        Outline highlighted;
        Graphic highlightedGraphic;
        float stepSince,nextRefresh,nextResultCheck,stalledSince,captionUntil;
        double coastSince=-1;
        int index,detachAtStart,observedMissionRun=-1,hintLevel;
        bool hidden,vrLayout,successAnnounced,explainLesson,vrPanelPositioned;
        PartKind? spokenFocus;
        Vector3 guideLocal;
        PartView guideView;
        bool validGuide;
        string lastProgress;
        string lastCaption;
        public int ActiveMissionId=>mission?.id??0;
        public int StepIndex=>index;
        public int StepCount=>steps?.Count??0;
        public string ActiveStepTitle=>steps!=null&&index<steps.Count?steps[index].title:"";
        public TutorialAudio AudioGuide=>audioGuide;
        public TutorialFocusPreview FocusPreview=>focusPreview;
        public PartDefinition FocusDefinition=>focusContext.definition;
        public string CurrentDiagnostic { get; private set; }

        void Start()
        {
            game=LaboratoryGame.Instance;
            if(game==null) {enabled=false;return;}
            audioGuide=gameObject.AddComponent<TutorialAudio>();audioGuide.Initialize(game);
            BuildPanel();
            focusPreview=gameObject.AddComponent<TutorialFocusPreview>();focusPreview.Initialize(game,RevealFocusInCatalog);
            var target=new GameObject("Instructor mount guide");target.transform.SetParent(transform,false);
            marker=target.AddComponent<LineRenderer>();marker.sharedMaterial=game.Assets.orbitMaterial;
            marker.startColor=marker.endColor=UiTheme.Cyan;marker.startWidth=marker.endWidth=.007f;
            marker.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;marker.receiveShadows=false;
            marker.positionCount=0;
        }

        void BuildPanel()
        {
            instruction=UiTheme.Text(game.UI.root,"",180,82,1560,76,25);
            instruction.alignment=TextAlignmentOptions.Center;
            instruction.enableAutoSizing=true;instruction.fontSizeMin=21;instruction.fontSizeMax=26;
            instruction.raycastTarget=false;instruction.outlineWidth=.22f;instruction.outlineColor=new Color(0,0,0,.92f);
            var outline=instruction.gameObject.AddComponent<Outline>();outline.effectColor=new Color(0,0,0,.8f);outline.effectDistance=new Vector2(2,-2);
            panel=instruction.rectTransform;
            panel.gameObject.SetActive(false);
        }

        public void Replay()
        {
            if(steps==null||index>=steps.Count) return;
            audioGuide.Speak(VoiceFor(steps[index]));
        }
        public void Advance()
        {
            if(steps==null||index>=steps.Count||!Satisfied(steps[index])) return;
            if(steps[index].voice.StartsWith("controls_",StringComparison.Ordinal))
            {game.Progress.tutorialSeen=true;SaveStore.Save(game.Progress);}
            index++;coastSince=-1;
            if(index>=steps.Count) {audioGuide.Speak("complete");ClearHighlight();return;}
            ShowStep();
        }

        void ShowStep()
        {
            vrPanelPositioned=false;nextRefresh=0;var step=steps[index];instruction.text="";lastCaption="";captionUntil=0;
            if(focusPreview!=null) focusPreview.Close();
            stepSince=stalledSince=Time.unscaledTime;hintLevel=0;lastProgress=null;ClearHighlight();
            spokenFocus=MissingPart(step);
            if(!hidden&&!audioGuide.Speak(VoiceFor(step))) instruction.text=step.text;
        }

        void Update()
        {
            if(game==null||panel==null) return;
            if(game.CurrentMission!=mission||observedMissionRun!=game.MissionRun)
            {
                observedMissionRun=game.MissionRun;mission=game.CurrentMission;index=0;hidden=false;successAnnounced=false;coastSince=-1;ClearHighlight();
                if(mission!=null)
                {
                    detachAtStart=game.Assembly.detachCount;
                    explainLesson=!game.Progress.tutorialSeen;
                    steps=TutorialLesson.Create(mission,game.VREnabled,explainLesson);
                    ShowStep();
                }
                else {steps=null;audioGuide.StopSpeech();}
            }
            if(game.Flight!=previousFlight)
            {
                bool launched=game.Flight!=null&&!game.OnStand&&game.Flight.Altitude<100;
                previousFlight=game.Flight;vrPanelPositioned=false;
                if(launched) audioGuide.Launch();
            }
            if(vrLayout!=game.VREnabled)
            {
                vrLayout=game.VREnabled;vrPanelPositioned=false;
                if(mission!=null)
                {
                    bool finished=index>=steps.Count;
                    steps=TutorialLesson.Create(mission,game.VREnabled,explainLesson);
                    if(finished) index=steps.Count;else {index=Mathf.Min(index,steps.Count-1);ShowStep();}
                }
            }
            bool active=mission!=null&&steps!=null&&index<steps.Count&&!game.UI.ModalOpen&&!game.UI.MapView;
            if(mission!=null&&!successAnnounced&&game.UI.ModalOpen&&Time.unscaledTime>=nextResultCheck)
            {
                nextResultCheck=Time.unscaledTime+.5f;
                if(game.UI.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="МИССИЯ ВЫПОЛНЕНА"))
                {successAnnounced=true;index=steps.Count;audioGuide.Speak("complete");ClearHighlight();active=false;}
            }
            panel.gameObject.SetActive(active&&!hidden&&!focusPreview.IsOpen&&!string.IsNullOrEmpty(instruction.text));
            audioGuide.Suspend(game.UI.ModalOpen&&!successAnnounced);
            if(!active||hidden) {marker.positionCount=0;focusPreview.Close();ClearHighlight();return;}
            if(game.VREnabled&&!vrPanelPositioned) PositionVR();
            var current=steps[index];bool ready=Satisfied(current);
            if(Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+.15f;
                UpdateFocusCopy(current);
                Highlight(current);
                RefreshGuide(current);
                RefreshFocus(current);
            }
            string subtitle=audioGuide.CurrentSubtitle;
            if(!string.IsNullOrEmpty(subtitle))
            {
                lastCaption=subtitle;captionUntil=Time.unscaledTime+1.25f;
                if(instruction.text!=subtitle) instruction.text=subtitle;
            }
            else if(!audioGuide.IsNarrating&&Time.unscaledTime>captionUntil&&instruction.text.Length>0)
            {lastCaption="";instruction.text="";panel.gameObject.SetActive(false);}
            else if(!audioGuide.IsNarrating&&!string.IsNullOrEmpty(lastCaption)&&instruction.text!=lastCaption) instruction.text=lastCaption;
            focusPreview.SetCopy(Purpose(current),ActionText(current),CurrentDiagnostic,subtitle);
            UpdateMarker(current);
            string progress=ProgressText(current)+"|"+CurrentDiagnostic;
            if(progress!=lastProgress) {lastProgress=progress;stalledSince=Time.unscaledTime;}
            if(current.gate==TutorialGate.Continue&&Time.unscaledTime>stepSince+1.2f&&!audioGuide.IsNarrating&&!focusPreview.IsOpen) Advance();
            else if(current.gate!=TutorialGate.Continue&&ready&&Time.unscaledTime>stepSince+1.2f&&!audioGuide.IsNarrating&&!focusPreview.IsOpen) Advance();
            else if(current.gate!=TutorialGate.Continue&&!ready&&!audioGuide.IsNarrating&&!focusPreview.IsOpen&&Time.unscaledTime-stalledSince>(hintLevel==0?14:22))
            {hintLevel=Mathf.Min(2,hintLevel+1);stalledSince=Time.unscaledTime;Replay();}
        }

        void PositionVR()
        {
            vrPanelPositioned=true;panel.anchorMin=panel.anchorMax=new Vector2(0,1);panel.pivot=new Vector2(0,1);
            panel.anchoredPosition=new Vector2(340,-140);panel.sizeDelta=new Vector2(1240,76);
            panel.localRotation=Quaternion.identity;panel.localScale=Vector3.one;
            instruction.fontSize=22;instruction.enableAutoSizing=true;instruction.fontSizeMin=19;instruction.fontSizeMax=22;
            instruction.alignment=TextAlignmentOptions.Center;
        }

        bool Has(PartKind kind)=>game.Assembly.Blueprint.parts.Any(p=>game.Catalog.Find(p.partId).kind==kind);
        int Count(PartKind kind)=>game.Assembly.Blueprint.parts.Count(p=>game.Catalog.Find(p.partId).kind==kind);
        bool Satisfied(TutorialStep step)
        {
            var flight=game.Flight;
            switch(step.gate)
            {
                case TutorialGate.Continue:return true;
                case TutorialGate.EmptyBench:return game.Assembly.Blueprint.parts.Count==0;
                case TutorialGate.HasParts:return step.parts.All(Has)&&(step.threshold<=0||Count(step.parts[0])>=step.threshold);
                case TutorialGate.Detached:return game.Assembly.detachCount>detachAtStart;
                case TutorialGate.Reattached:return Has(PartKind.BoosterEngine)&&game.Assembly.snapCount>=4;
                case TutorialGate.Balanced:
                    if(game.Assembly.Blueprint.parts.Count==0) return false;
                    var body=new FlightBody(game.Assembly.Blueprint,game.Catalog);
                    return body.TWR>=1.3&&body.TWR<=2&&Math.Sqrt(body.centerOfMass.x*body.centerOfMass.x+body.centerOfMass.z*body.centerOfMass.z)<.045;
                case TutorialGate.InFlight:return flight!=null;
                case TutorialGate.StandRunning:return flight!=null&&flight.poweredTime>=step.threshold;
                case TutorialGate.ThrustStopped:return flight!=null&&flight.throttle<.01;
                case TutorialGate.Altitude:return flight!=null&&flight.maxAltitude>=step.threshold;
                case TutorialGate.Stable:return flight!=null&&flight.Altitude>=step.threshold&&flight.Tilt<10&&flight.stableTime>10;
                case TutorialGate.StageSeparated:return flight!=null&&flight.stageEvent&&flight.maxAltitude>=step.threshold;
                case TutorialGate.PayloadReleased:return flight!=null&&flight.payloadReleased&&flight.maxAltitude>=step.threshold;
                case TutorialGate.PoweredCoast:
                    if(flight==null||!flight.Powered||flight.lastThrust>1) {coastSince=-1;return false;}
                    if(coastSince<0) coastSince=flight.elapsed;
                    return flight.elapsed-coastSince>=step.threshold;
                case TutorialGate.Aborted:return flight!=null&&flight.aborted;
                case TutorialGate.Parachute:return flight!=null&&flight.chuteDeployed;
                case TutorialGate.Landed:return flight!=null&&flight.landed&&!flight.crashed;
                case TutorialGate.Orbit:return flight!=null&&flight.Orbit.periapsis>=120000&&flight.Orbit.apoapsis<=250000;
                case TutorialGate.SatelliteRadio:return game.Payload!=null&&game.Payload.telemetryTime>=step.threshold;
                case TutorialGate.Rendezvous:return flight!=null&&game.Target!=null&&(flight.position-game.Target.position).Magnitude<step.threshold&&(flight.velocity-game.Target.velocity).Magnitude<1;
                case TutorialGate.Docked:return flight!=null&&flight.docked;
                case TutorialGate.Completed:return game.Progress.completed.Contains(mission.id)&&game.UI.ModalOpen;
                default:return false;
            }
        }

        string ProgressText(TutorialStep step)
        {
            var flight=game.Flight;
            if(step.gate==TutorialGate.HasParts)
            {
                if(step.threshold>0) return $"Установлено {Count(step.parts[0])} / {step.threshold:F0}";
                return $"Установлено {step.parts.Count(Has)} / {step.parts.Length}";
            }
            if(step.gate==TutorialGate.StandRunning&&flight!=null) return $"Тяга: {flight.poweredTime:F1} / {step.threshold:F0} с";
            if(step.gate==TutorialGate.PoweredCoast&&flight!=null) return $"Автономно: {(coastSince<0?0:Math.Max(0,flight.elapsed-coastSince)):F0} / 180 с";
            if(step.gate==TutorialGate.SatelliteRadio) return $"Связь: {game.Payload?.telemetryTime??0:F0} / 180 с";
            if(step.gate==TutorialGate.Altitude&&flight!=null) return $"Высота: {flight.Altitude/1000:F1} / {step.threshold/1000:F0} км";
            if(step.gate==TutorialGate.Orbit&&flight!=null) return $"Pe {flight.Orbit.periapsis/1000:F0} / Ap {flight.Orbit.apoapsis/1000:F0} км";
            if((step.gate==TutorialGate.Rendezvous||step.gate==TutorialGate.Docked)&&flight!=null&&game.Target!=null) return $"До цели {(flight.position-game.Target.position).Magnitude:F1} м";
            return Satisfied(step)?"Шаг принят":"Следуй подсветке";
        }

        PartKind? MissingPart(TutorialStep step)
        {
            if(step.parts==null||step.parts.Length==0) return null;
            foreach(var kind in step.parts) if(!Has(kind)) return kind;
            if(step.gate==TutorialGate.HasParts&&(step.threshold<=0||Count(step.parts[0])>=step.threshold)) return null;
            return step.parts[0];
        }

        string VoiceFor(TutorialStep step)
        {
            var focus=MissingPart(step);
            if(mission.id!=1&&step.gate==TutorialGate.HasParts&&focus.HasValue) return "part_"+game.Catalog.Find(focus.Value).id;
            return step.voice;
        }

        void UpdateFocusCopy(TutorialStep step)
        {
            if(mission.id==1||step.gate!=TutorialGate.HasParts) return;
            var focus=MissingPart(step);
            if(focus==spokenFocus) return;
            spokenFocus=focus;
            if(focus.HasValue&&!hidden) audioGuide.Speak(VoiceFor(step));
        }

        void Highlight(TutorialStep step)
        {
            Graphic graphic=null;
            if(!string.IsNullOrEmpty(step.command))
            {
                var button=game.UI.GetComponentsInChildren<LabButton>().FirstOrDefault(b=>b.command==step.command);
                if(button!=null) graphic=button.GetComponent<Graphic>();
            }
            else if(!game.IsFlying)
            {
                var kind=MissingPart(step);
                if(kind.HasValue)
                {
                    var card=game.UI.GetComponentsInChildren<CatalogCard>().FirstOrDefault(c=>c.definition.kind==kind.Value);
                    if(card!=null) graphic=card.GetComponent<Graphic>();
                    else
                    {
                        var definition=game.Catalog.Find(kind.Value);
                        var button=game.UI.GetComponentsInChildren<LabButton>().FirstOrDefault(b=>b.command=="category"&&b.argument==(int)definition.category);
                        if(button!=null) graphic=button.GetComponent<Graphic>();
                    }
                }
            }
            if(graphic!=highlightedGraphic)
            {
                ClearHighlight();highlightedGraphic=graphic;
                if(graphic!=null) {highlighted=graphic.gameObject.AddComponent<Outline>();highlighted.effectColor=hintLevel>0?UiTheme.Orange:UiTheme.Cyan;highlighted.effectDistance=new Vector2(3,-3);}
            }
            if(highlighted!=null)
            {
                float pulse=hintLevel>0?Mathf.Sin(Time.unscaledTime*7)*1.5f:0;
                highlighted.effectDistance=new Vector2(3+pulse,-3-pulse);
                highlighted.effectColor=hintLevel>0?UiTheme.Orange:UiTheme.Cyan;
            }
        }

        void ClearHighlight()
        {
            if(highlighted!=null) Destroy(highlighted);
            highlighted=null;highlightedGraphic=null;
        }

        void UpdateMarker(TutorialStep step)
        {
            marker.positionCount=0;if(game.IsFlying||!validGuide) return;
            Vector3 point=guideView==null?game.Assembly.assemblyRoot.TransformPoint(guideLocal):guideView.transform.TransformPoint(guideLocal);
            float radius=.09f+Mathf.Sin(Time.unscaledTime*(hintLevel>0?7:3))*(hintLevel>0?.025f:.012f);
            marker.startColor=marker.endColor=hintLevel>0?UiTheme.Orange:UiTheme.Cyan;
            for(int i=0;i<ring.Length;i++)
            {
                float angle=i*Mathf.PI*2/(ring.Length-1);ring[i]=point+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            }
            marker.positionCount=ring.Length;marker.SetPositions(ring);
        }

        void RefreshGuide(TutorialStep step)
        {
            validGuide=false;guideView=null;if(game.IsFlying) return;
            var kind=MissingPart(step);if(!kind.HasValue) return;
            var definition=game.Catalog.Find(kind.Value);
            if(step.gate==TutorialGate.Detached)
            {
                guideView=game.Assembly.views.FirstOrDefault(v=>v.Attached&&v.Definition.kind==kind.Value);
                if(guideView==null) return;guideLocal=Vector3.up*definition.height*.5f;validGuide=true;
            }
            else
            {
                validGuide=TryGuideTarget(definition,out var target);guideLocal=target.position;
                var parent=game.Assembly.Blueprint.parts.FirstOrDefault(p=>p.instanceId==target.parentId);
                if(parent!=null&&!definition.surface) guideLocal=parent.position+Vector3.up*(target.port==0?game.Catalog.Find(parent.partId).height:0);
            }
        }

        bool TryGuideTarget(PartDefinition definition,out SnapTarget target)
        {
            var craft=game.Assembly.Blueprint;target=default;
            if(craft.parts.Count==0)
            {
                target=new SnapTarget(null,0,Vector3.zero,Quaternion.identity);return definition.IsStructural;
            }
            foreach(var placement in craft.parts.OrderByDescending(p=>p.position.y))
            {
                var parent=game.Catalog.Find(placement.partId);if(parent==null||parent.surface) continue;
                if(definition.surface)
                {
                    for(int i=0;i<8;i++)
                    {
                        int port=2+i;if(craft.parts.Any(p=>p.parentId==placement.instanceId&&p.port==port)) continue;
                        var rotation=Quaternion.Euler(0,i*45,0);
                        target=new SnapTarget(placement.instanceId,port,placement.position+rotation*new Vector3(parent.radius,.15f+parent.height*.25f,0),rotation);return true;
                    }
                }
                else
                {
                    int port=definition.mount==MountType.Engine&&parent.kind!=PartKind.StackDecoupler&&parent.kind!=PartKind.Adapter?1:0;
                    if(craft.parts.Any(p=>p.parentId==placement.instanceId&&p.port==port)) continue;
                    target=new SnapTarget(placement.instanceId,port,placement.position+Vector3.up*(port==0?parent.height:-definition.height),Quaternion.identity);return true;
                }
            }
            return false;
        }

        void RefreshFocus(TutorialStep step)
        {
            focusContext=default;
            var kind=MissingPart(step);
            if(!kind.HasValue&&step.parts!=null&&step.parts.Length>0) kind=step.parts[0];
            if(kind.HasValue&&!game.IsFlying)
            {
                var definition=game.Catalog.Find(kind.Value);
                var view=game.Selected!=null&&game.Selected.Definition==definition?game.Selected:game.Assembly.views.FirstOrDefault(v=>v.Definition==definition&&!v.Attached);
                if(view==null) view=game.Assembly.views.FirstOrDefault(v=>v.Definition==definition);
                bool attached=view!=null&&view.Attached;
                SnapTarget target;
                bool mounted;
                if(attached)
                {
                    var placement=view.Placement;
                    target=new SnapTarget(placement.parentId,placement.port,placement.position,placement.rotation);mounted=true;
                }
                else mounted=TryGuideTarget(definition,out target);
                var parent=game.Assembly.Blueprint.parts.FirstOrDefault(p=>p.instanceId==target.parentId);
                var parentDefinition=parent==null?null:game.Catalog.Find(parent.partId);
                Vector3 connector=target.position;
                if(parent!=null&&!definition.surface) connector=parent.position+Vector3.up*(target.port==0?parentDefinition.height:0);
                focusContext=new TutorialFocusContext
                {
                    definition=definition,placement=view?.Placement,parentPlacement=parent,parentDefinition=parentDefinition,
                    target=target,hasMount=mounted,connector=connector,removal=step.gate==TutorialGate.Detached,
                    approach=definition.surface?target.rotation*Vector3.right:target.port==1?Vector3.down:Vector3.up,
                    location=parent==null?"центр верстака":definition.surface?"боковой узел":target.port==1?"нижний осевой узел":"верхний осевой узел"
                };
            }
            focusPreview.SetContext(focusContext);
            CurrentDiagnostic=Diagnose(step);
        }

        string Purpose(TutorialStep step)
        {
            if(focusContext.definition!=null) return focusContext.definition.description;
            switch(step.gate)
            {
                case TutorialGate.Balanced:return "Симметрия удерживает тягу вдоль оси. Тяговооружённость определяет запас ускорения.";
                case TutorialGate.Orbit:return "Орбиту держит горизонтальная скорость. Высоты самой по себе недостаточно.";
                case TutorialGate.Parachute:case TutorialGate.Landed:return "Снижение должно закончиться мягкой посадкой, а не ударом о поверхность.";
                case TutorialGate.Rendezvous:case TutorialGate.Docked:return "Для встречи нужно сблизить орбиты и погасить скорость относительно цели.";
                case TutorialGate.PoweredCoast:return "После выключения двигателя аппарату всё ещё нужны питание и управление.";
                case TutorialGate.EmptyBench:return "Чистый верстак позволяет собрать учебную ракету по шагам.";
                case TutorialGate.InFlight:return "Перед стартом проверь соединения и последовательность ступеней.";
                case TutorialGate.Stable:return "Небольшой наклон сохраняет управляемость в плотном воздухе.";
                case TutorialGate.StageSeparated:return "Отработанную ступень сбрасывают, чтобы не разгонять пустую массу.";
                case TutorialGate.ThrustStopped:return "Выключение тяги позволяет проверить аппаратуру без дальнейшего разгона.";
                default:return "Каждый шаг проверяется по настоящей сборке и состоянию полёта.";
            }
        }

        string ActionText(TutorialStep step)
        {
            if(focusContext.definition==null) return step.text.Length>145?step.text.Substring(0,142)+"…":step.text;
            if(step.gate==TutorialGate.Detached) return "Выбери двигатель. Нажми «Разблокировать», затем отведи его от корпуса.";
            if(step.threshold>1) return $"Установи {step.threshold:F0} одинаковых модуля равномерно вокруг корпуса. Поможет симметрия.";
            if(focusContext.parentPlacement==null) return "Перенеси модуль в подсвеченный центр верстака и отпусти.";
            return "Совмести модуль с узлом: "+focusContext.location+". Отпусти после появления фиксации.";
        }

        string Diagnose(TutorialStep step)
        {
            if(Satisfied(step)) return "Шаг принят. Можно продолжать.";
            if(focusContext.definition==null)
            {
                if(game.Flight!=null&&game.Flight.crashed) return "Аппарат разрушен. Вернись в ангар и проверь конструкцию.";
                if(step.gate==TutorialGate.Orbit) return "Поднимай перицентр тягой вдоль движения. Следи за Pe и Ap на карте.";
                if(step.gate==TutorialGate.Balanced) return "Поставь боковые детали симметрично. Нужна тяговооружённость 1,3–2,0.";
                if(step.gate==TutorialGate.Landed) return "Следи за вертикальной скоростью. Перед касанием погаси снижение.";
                return game.VREnabled?"Нужная кнопка отмечена. Наведи луч и нажми триггер.":"Нужная кнопка отмечена. Нажми её мышью.";
            }
            if(!focusContext.hasMount) return game.Assembly.Blueprint.parts.Count==0?"Сначала установи корпус. Двигателю нужна опора.":"Свободного узла нет. Освободи крепление или добавь корпус / переходник.";
            var selected=game.Selected;
            if(selected==null||selected.Definition!=focusContext.definition) return "Выбери отмеченную деталь в каталоге. «Показать ближе» объяснит соединение.";
            if(selected.Attached) return step.gate==TutorialGate.Detached?"Установленная деталь закреплена. Сначала нажми «Разблокировать».":"Этот модуль уже установлен. Для следующей копии используй каталог.";
            if(game.Assembly.TryTarget(selected,out _)) return game.VREnabled?"Узел совпал. Отпусти хват — деталь закрепится.":"Узел совпал. Отпусти кнопку мыши — деталь закрепится.";
            if(Quaternion.Angle(selected.transform.localRotation,focusContext.target.rotation)>35) return game.VREnabled?"Поверни модуль кистью. Его ось должна совпасть с узлом.":"Поверни деталь клавишей R. Её ось должна совпасть с узлом.";
            return "Подведи деталь ближе к светящемуся узлу. В окне осмотра показано направление.";
        }

        public void ShowFocusPreview()
        {
            if(steps==null||index>=steps.Count||game.IsFlying) return;
            RefreshFocus(steps[index]);focusPreview.Open();
        }

        public void RevealFocusInCatalog()
        {
            var definition=focusContext.definition;if(definition==null||game.IsFlying) return;
            focusPreview.Close();game.UI.PopulateCatalog(definition.category);Canvas.ForceUpdateCanvases();
            var card=game.UI.GetComponentsInChildren<CatalogCard>().FirstOrDefault(c=>c.definition==definition);
            if(card==null) return;
            var scroll=card.GetComponentInParent<ScrollRect>();
            if(scroll!=null)
            {
                float range=scroll.content.rect.height-scroll.viewport.rect.height;
                if(range>0) scroll.verticalNormalizedPosition=1-Mathf.Clamp01(-((RectTransform)card.transform).anchoredPosition.y/range);
                scroll.velocity=Vector2.zero;
            }
            Highlight(steps[index]);
        }

        void OnDestroy() {ClearHighlight();if(panel!=null) Destroy(panel.gameObject);if(focusPreview!=null) Destroy(focusPreview);}
    }
}
