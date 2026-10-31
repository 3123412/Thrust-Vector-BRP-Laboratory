using System.Collections.Generic;

namespace BRP
{
    public enum TutorialGate
    {
        Continue,EmptyBench,HasParts,Detached,Reattached,Balanced,InFlight,StandRunning,ThrustStopped,
        Altitude,Stable,StageSeparated,PayloadReleased,PoweredCoast,Aborted,Parachute,Landed,Orbit,SatelliteRadio,Rendezvous,Docked,Completed
    }

    public sealed class TutorialStep
    {
        public string title,text,voice,command;
        public TutorialGate gate;
        public PartKind[] parts;
        public double threshold;
        public TutorialStep(string title,string text,string voice,TutorialGate gate,string command=null,double threshold=0,params PartKind[] parts)
        {this.title=title;this.text=text;this.voice=voice;this.gate=gate;this.command=command;this.threshold=threshold;this.parts=parts;}
    }

    public static class TutorialLesson
    {
        public static List<TutorialStep> Create(MissionDefinition mission,bool vr,bool explainControls)
        {
            string mode=vr?"vr":"pc";
            var steps=new List<TutorialStep>();
            steps.Add(new TutorialStep(mission.id==1?"ДОБРО ПОЖАЛОВАТЬ В БЮРО":"ЗАДАНИЕ КОНСТРУКТОРУ",mission.id==1?"Привет, салага. Вижу, хочешь научиться конструировать ракеты. Проходи к верстаку, я покажу.":mission.brief,mission.id==1?"intro":$"mission_{mission.id:00}",TutorialGate.Continue));
            if(explainControls) steps.Add(new TutorialStep("РУКИ НА ОРГАНЫ УПРАВЛЕНИЯ",vr?"Луч выбирает кнопки. Хват переносит свободную деталь. Поднеси её к узлу и отпусти. Установленную деталь сначала разблокируй.":"Перетаскивай карточки мышью. ПКМ вращает камеру, колесо приближает, R поворачивает деталь. Установленный узел сначала разблокируй.","controls_"+mode,TutorialGate.Continue));
            if(mission.id==1)
            {
                steps.Add(new TutorialStep("ПОДГОТОВЬ РАБОЧЕЕ МЕСТО","Сохрани нужную конструкцию, затем нажми «Очистить». Начнём с пустого верстака.","clear",TutorialGate.EmptyBench,"clear"));
                steps.Add(new TutorialStep("01 / ОСНОВА РАКЕТЫ","Возьми корпус первой ступени и установи его в подсвеченный центр верстака.","first_stage",TutorialGate.HasParts,null,0,PartKind.FirstStage));
                steps.Add(new TutorialStep("02 / ДВИГАТЕЛЬ СНИЗУ","В категории «Двигатели» возьми стартовый двигатель. Совмести его верх с нижним узлом корпуса. Сопло смотрит вниз.","engine",TutorialGate.HasParts,null,0,PartKind.BoosterEngine));
                steps.Add(new TutorialStep("03 / ОБТЕКАТЕЛЬ СВЕРХУ","Возьми головной обтекатель и установи на верхний узел корпуса. Он уменьшает сопротивление воздуха.","fairing",TutorialGate.HasParts,null,0,PartKind.Fairing));
                steps.Add(new TutorialStep("04 / РАЗБЛОКИРУЙ СОЕДИНЕНИЕ","Выбери двигатель и нажми «Разблокировать». Сними его с корпуса.","detach",TutorialGate.Detached,"detach",0,PartKind.BoosterEngine));
                steps.Add(new TutorialStep("05 / ВЕРНИ УЗЕЛ НА МЕСТО","Поднеси двигатель к нижнему креплению. Отпусти, когда узел совпадёт с подсветкой.","reattach",TutorialGate.Reattached,null,0,PartKind.BoosterEngine));
                return steps;
            }
            if(mission.id==2)
            {
                for(int i=0;i<10;i++)
                {
                    var kind=(PartKind)i;
                    steps.Add(new TutorialStep($"БАЗОВЫЙ МОДУЛЬ {i+1:00} / 10","Установи подсвеченный тип детали. Стабилизаторы закрепляются сбоку, остальные узлы соединяются по оси.",$"mission_02",TutorialGate.HasParts,null,0,kind));
                }
                steps.Add(new TutorialStep("ЧЕТЫРЕ СТАБИЛИЗАТОРА","Выбери один стабилизатор и нажми «Симметрия ×4». Проверь, что они смотрят в четыре стороны.","mission_02",TutorialGate.HasParts,"symmetry",4,PartKind.Fin));
                return steps;
            }
            if(mission.id==3)
            {
                steps.Add(new TutorialStep("ТЯГА, ВЕС И ЦЕНТР МАСС","Меняй массу и состав ракеты. Нужна тяговооружённость 1,3–2,0 и центр масс не дальше 4,5 см от оси.","mission_03",TutorialGate.Balanced));
                return steps;
            }
            var requirements=RequiredParts(mission.id);
            if(requirements.Length>0) steps.Add(new TutorialStep("ПРОВЕРЬ ОБОРУДОВАНИЕ","Добавь нужные модули. Подсветка показывает следующий отсутствующий узел; его назначение указано в паспорте.",$"mission_{mission.id:00}",TutorialGate.HasParts,null,0,requirements));
            if(mission.id==18||mission.id==19)
            {
                steps.Add(new TutorialStep("ОРБИТАЛЬНЫЙ СТЕНД","Открой «Управление», затем «Орбитальный стенд». Здесь два аппарата уже находятся на орбите.",$"mission_{mission.id:00}",TutorialGate.InFlight,"flight_controls"));
                steps.Add(new TutorialStep("ДВИГАТЕЛИ СБЛИЖЕНИЯ",vr?"Левый триггер + стик задают перемещение. Левый хват переключает вертикальную ось. Можно включить «Помощник сближения».":"I/K, J/L, U/O управляют перемещением. В командах доступен «Помощник сближения». Следи за расстоянием и относительной скоростью.","rendezvous_"+mode,TutorialGate.Rendezvous,"flight_controls",50));
                if(mission.id==19) steps.Add(new TutorialStep("СОВМЕСТИ СТЫКОВОЧНЫЕ УЗЛЫ","Порты смотрят навстречу друг другу. Скорость ≤ 0,2 м/с, угол ≤ 5°. Подойди ближе 0,6 м и нажми «Стыковать».","docking",TutorialGate.Docked,"flight_controls"));
                return steps;
            }
            bool stand=mission.id==4||mission.id==5;
            steps.Add(new TutorialStep(stand?"НА ОГНЕВОЙ СТЕНД":"НА КОСМОДРОМ",stand?"Нажми «Испытать». Ракета останется закреплена на стенде.":"Нажми «На космодром». Перед запуском проверь массу, тягу и запас топлива.",stand?"stand":"launch",TutorialGate.InFlight,stand?"stand":"launch"));
            if(stand)
            {
                steps.Add(new TutorialStep("ДЕСЯТЬ СЕКУНД ТЯГИ",vr?"Правый хват + стик меняют тягу. Дождись десяти секунд работы двигателя.":"Shift/Ctrl меняют тягу. Дождись десяти секунд работы двигателя.","throttle_"+mode,TutorialGate.StandRunning,null,10));
                steps.Add(new TutorialStep("ОТКЛЮЧИ ДВИГАТЕЛЬ",vr?"Нажми «Стоп» на панели тяги.":"Нажми X или «Стоп» на панели тяги.","throttle_"+mode,TutorialGate.ThrustStopped,"throttle"));
                return steps;
            }
            if(mission.id<=8)
            {
                steps.Add(new TutorialStep("УДЕРЖИВАЙ НАПРАВЛЕНИЕ",vr?"Правый триггер + стик управляют наклоном. Делай небольшие поправки.":"W/S, A/D меняют наклон, Q/E управляют креном. Делай небольшие поправки.","orientation_"+mode,TutorialGate.Continue));
                steps.Add(new TutorialStep("НАБИРАЙ ВЫСОТУ",mission.goal,$"mission_{mission.id:00}",mission.id==7?TutorialGate.Stable:TutorialGate.Altitude,null,mission.altitude));
            }
            else
            {
                bool orbitalLesson=mission.id==12||mission.id>=16;
                string guidanceText=orbitalLesson?"Включи «Программу выведения» или управляй вручную. Первая ступень отделяется после выгорания топлива.":vr?"Правый триггер + стик управляют наклоном. Поднимай ракету без резких поправок и следи за тягой.":"W/S, A/D меняют наклон, Q/E управляют креном. Следи за тягой и высотой.";
                steps.Add(new TutorialStep(orbitalLesson?"ВЫВЕДЕНИЕ И СТУПЕНИ":"КОНТРОЛИРУЙ ПОДЪЁМ",guidanceText,orbitalLesson?"orbit":"orientation_"+mode,TutorialGate.Continue,orbitalLesson?"ascent":null));
                if(mission.id==9) steps.Add(new TutorialStep("ОТДЕЛИ ПЕРВУЮ СТУПЕНЬ",vr?"Основная кнопка правого контроллера или кнопка «Ступень». Продолжай подъём выше 20 км.":"Space или кнопка «Ступень». Продолжай подъём выше 20 км.","staging_"+mode,TutorialGate.StageSeparated,"next_stage",20000));
                if(mission.id==10) steps.Add(new TutorialStep("ОСВОБОДИ ПОЛЕЗНУЮ НАГРУЗКУ","Набери 50 км и нажми «Отделить груз».","payload",TutorialGate.PayloadReleased,"payload",50000));
                if(mission.id==11||mission.id==13) steps.Add(new TutorialStep("КОНТРОЛИРУЙ БОРТОВЫЕ СИСТЕМЫ",mission.goal,$"mission_{mission.id:00}",TutorialGate.Altitude,null,mission.altitude));
                if(mission.id==12||mission.id>=16) steps.Add(new TutorialStep("ЗАМКНИ ОРБИТУ","Перигей ≥ 120 км, апогей ≤ 250 км. Посмотри траекторию на карте и дождись отключения основной тяги.","orbit",TutorialGate.Orbit,"map"));
                if(mission.id==12) steps.Add(new TutorialStep("180 СЕКУНД БЕЗ ОСНОВНОЙ ТЯГИ","Держи питание включённым. Солнечные панели работают через распределитель, топливный элемент расходует служебное горючее.","power",TutorialGate.PoweredCoast,null,180));
                if(mission.id==14) steps.Add(new TutorialStep("АВАРИЙНОЕ СПАСЕНИЕ","В командах полёта выбери «Аварийное спасение». Башня отделит капсулу от носителя.","abort",TutorialGate.Aborted,"flight_controls"));
                if(mission.id==14) steps.Add(new TutorialStep("РАСКРОЙ ПАРАШЮТ",vr?"Дополнительная кнопка правого контроллера раскрывает парашют. На большой скорости купол не выпускать.":"P раскрывает парашют. На большой скорости купол не выпускать.","parachute_"+mode,TutorialGate.Parachute,"parachute"));
                if(mission.id==17||mission.id==20)
                {
                    steps.Add(new TutorialStep("ОТДЕЛИ СПУТНИК","Нажми «Отделить груз». Спутник продолжит полёт самостоятельно.","payload",TutorialGate.PayloadReleased,"payload"));
                    steps.Add(new TutorialStep("ПРОВЕРЬ СВЯЗЬ СО СПУТНИКОМ","У спутника должны работать питание, передатчик и антенна. Дождись 180 секунд автономной телеметрии.","power",TutorialGate.SatelliteRadio,null,180));
                }
                if(mission.id==15||mission.id==20) steps.Add(new TutorialStep("ВОЗВРАЩЕНИЕ КАПСУЛЫ","Нажми «Возвращение капсулы». Следи за нагревом, парашютом и скоростью перед касанием.","recovery",TutorialGate.Landed,"recover"));
            }
            return steps;
        }

        static PartKind[] RequiredParts(int mission)
        {
            switch(mission)
            {
                case 5:return new[]{PartKind.FuelLine,PartKind.FuelPump,PartKind.FuelValve,PartKind.Pressurizer,PartKind.Battery};
                case 6:return new[]{PartKind.FlightRecorder,PartKind.Battery};
                case 7:return new[]{PartKind.FlightComputer,PartKind.Gyroscope,PartKind.Gimbal};
                case 8:return new[]{PartKind.Fin,PartKind.ControlFin};
                case 9:return new[]{PartKind.SecondStage,PartKind.VacuumEngine,PartKind.StackDecoupler};
                case 10:return new[]{PartKind.PayloadSeparator,PartKind.Satellite};
                case 11:return new[]{PartKind.Antenna,PartKind.Transmitter,PartKind.Battery};
                case 12:return new[]{PartKind.Battery,PartKind.PowerDistributor,PartKind.SolarPanel};
                case 13:return new[]{PartKind.Radiator,PartKind.Insulation,PartKind.Thermometer};
                case 14:return new[]{PartKind.Capsule,PartKind.EscapeTower,PartKind.MainParachute};
                case 15:return new[]{PartKind.Capsule,PartKind.HeatShield,PartKind.FlightRecorder,PartKind.DrogueParachute,PartKind.MainParachute};
                case 16:return new[]{PartKind.SecondStage,PartKind.VacuumEngine,PartKind.FlightComputer};
                case 17:return new[]{PartKind.Satellite,PartKind.PayloadSeparator,PartKind.Antenna,PartKind.Transmitter};
                case 20:return new[]{PartKind.Satellite,PartKind.PayloadSeparator,PartKind.Capsule,PartKind.HeatShield,PartKind.FlightRecorder,PartKind.MainParachute};
                default:return new PartKind[0];
            }
        }
    }
}
