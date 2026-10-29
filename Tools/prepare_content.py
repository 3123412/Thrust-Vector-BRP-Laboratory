import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/BRP/Resources/BRP'
DEST.mkdir(parents=True, exist_ok=True)

rows = [
('booster_engine','Двигатель первой ступени','BoosterEngine',1200,1.1,'Propulsion','Engine','Тяга у поверхности. Требует горючее и окислитель.',{'thrust':900000,'specificImpulse':310}),
('first_stage','Корпус первой ступени','FirstStage',900,1.8,'Structure','Stack','Несущая ступень с интегрированными баками.',{'fuelCapacity':18000,'oxidizerCapacity':44000}),
('adapter','Переходной отсек','Adapter',160,.42,'Structure','Stack','Соединяет корпуса разных диаметров.',{}),
('vacuum_engine','Двигатель второй ступени','VacuumEngine',240,.8,'Propulsion','Engine','Экономичный двигатель с расширенным соплом.',{'thrust':180000,'specificImpulse':365}),
('second_stage','Корпус второй ступени','SecondStage',300,1.3,'Structure','Stack','Лёгкая ступень для высоты и орбитальных манёвров.',{'fuelCapacity':3500,'oxidizerCapacity':8500,'radius':.34}),
('fin','Хвостовой стабилизатор','Fin',35,.55,'Control','Surface','Смещает центр давления к хвосту. Ставьте симметрично.',{'liftArea':1.4,'surface':True}),
('instrument_bay','Приборный отсек','InstrumentBay',85,.36,'Control','Stack','Собирает показания датчиков и управляет ступенями.',{'electricConsumption':8,'electricCapacity':1500}),
('fairing','Головной обтекатель','Fairing',90,.95,'Structure','Stack','Защищает нагрузку и уменьшает сопротивление.',{'dragCoefficient':.12}),
('window','Иллюминатор','Window',12,.24,'Structure','Surface','Герметичное смотровое окно капсулы.',{'surface':True}),
('antenna','Антенна связи','Antenna',8,.5,'Electrical','Surface','Усиливает радиоканал телеметрического передатчика.',{'surface':True,'electricConsumption':2}),
('fuel_tank','Бак горючего','FuelTank',280,.8,'Propulsion','Stack','Дополнительный запас горючего.',{'fuelCapacity':6000}),
('oxidizer_tank','Бак окислителя','OxidizerTank',300,.8,'Propulsion','Stack','Запас окислителя для жидкостных двигателей.',{'oxidizerCapacity':14500}),
('service_tank','Бак служебного топлива','ServiceTank',45,.4,'Propulsion','Surface','Топливо двигателей ориентации и топливного элемента.',{'fuelCapacity':220,'surface':True}),
('solid_booster','Твердотопливный ускоритель','SolidBooster',160,.95,'Propulsion','Surface','Работает до выгорания заряда. После зажигания не выключается.',{'thrust':240000,'fuelCapacity':1800,'specificImpulse':250,'surface':True}),
('radial_decoupler','Радиальный разделитель','RadialDecoupler',30,.28,'Structure','Surface','Отделяет боковую подсборку по команде ступени.',{'surface':True}),
('stack_decoupler','Межступенчатый разделитель','StackDecoupler',65,.18,'Structure','Stack','Разделяет ступени с небольшим импульсом.',{}),
('fuel_line','Топливопровод','FuelLine',8,.45,'Propulsion','Surface','Открывает подачу из дополнительных баков в двигатель.',{'surface':True}),
('fuel_pump','Топливный насос','FuelPump',30,.35,'Propulsion','Surface','Поддерживает подачу топлива при высоком расходе.',{'surface':True,'electricConsumption':15}),
('fuel_valve','Запорный клапан','FuelValve',12,.25,'Propulsion','Surface','Разрешает или перекрывает подачу топлива.',{'surface':True}),
('pressurizer','Агрегат наддува','Pressurizer',22,.42,'Propulsion','Surface','Стабилизирует давление топливной системы.',{'surface':True}),
('gimbal','Карданный подвес','Gimbal',50,.25,'Control','Stack','Отклоняет вектор тяги работающего двигателя.',{'controlTorque':90000,'electricConsumption':3}),
('vernier_engine','Рулевой двигатель','VernierEngine',18,.35,'Control','Surface','Создаёт управляющий момент при работе основной ступени.',{'thrust':6500,'specificImpulse':280,'controlTorque':20000,'surface':True}),
('rcs','Блок двигателей ориентации','RcsBlock',12,.22,'Control','Surface','Поворот и поступательные манёвры в вакууме.',{'controlTorque':18000,'surface':True}),
('reaction_wheel','Реакционное колесо','ReactionWheel',45,.24,'Control','Stack','Поворачивает аппарат без расхода топлива.',{'controlTorque':8000,'electricConsumption':18}),
('gyroscope','Гироскопический датчик','Gyroscope',8,.2,'Control','Surface','Измеряет вращение и включает демпфирование автопилота.',{'surface':True,'electricConsumption':2}),
('horizon_sensor','Датчик ориентации','HorizonSensor',5,.18,'Control','Surface','Находит местный горизонт и направление к Земле.',{'surface':True,'electricConsumption':2}),
('control_fin','Аэродинамический руль','ControlFin',30,.5,'Control','Surface','Активно управляет ракетой в плотной атмосфере.',{'liftArea':1.2,'controlTorque':10000,'surface':True,'electricConsumption':2}),
('airbrake','Воздушный тормоз','AirBrake',28,.55,'Control','Surface','Раскрывается по команде и увеличивает сопротивление.',{'surface':True,'dragCoefficient':1.4}),
('ballast','Балласт','Ballast',180,.3,'Structure','Surface','Сдвигает центр масс без изменения тяги.',{'surface':True}),
('computer','Бортовой компьютер','FlightComputer',16,.28,'Control','Surface','Удерживает ориентацию и выполняет орбитальные манёвры.',{'surface':True,'electricConsumption':5}),
('battery','Аккумулятор','Battery',35,.28,'Electrical','Surface','Питание приборов при выключенных генераторах.',{'surface':True,'electricCapacity':18000}),
('solar_panel','Солнечная панель','SolarPanel',18,.9,'Electrical','Surface','Вырабатывает энергию на освещённой стороне орбиты.',{'surface':True,'electricGeneration':90}),
('fuel_cell','Топливный элемент','FuelCell',35,.35,'Electrical','Surface','Преобразует служебное топливо в электричество.',{'surface':True,'electricGeneration':150}),
('power_bus','Распределитель питания','PowerDistributor',12,.25,'Electrical','Surface','Соединяет дополнительные источники питания с приборами.',{'surface':True}),
('radiator','Радиатор','Radiator',20,.75,'Electrical','Surface','Отводит тепло от корпуса и приборов.',{'surface':True,'coolingArea':8}),
('insulation','Теплоизоляция','Insulation',15,.6,'Structure','Surface','Снижает тепловой поток к внутренним приборам.',{'surface':True,'temperatureLimit':1400}),
('heat_shield','Тепловой щит','HeatShield',140,.2,'Structure','Stack','Абрационный щит для возвращения капсулы.',{'temperatureLimit':2800,'heatCapacity':1600,'dragCoefficient':1.1}),
('thermometer','Температурный датчик','Thermometer',3,.15,'Electrical','Surface','Показывает температуру и предупреждает о перегреве.',{'surface':True,'electricConsumption':1}),
('altimeter','Высотомер','Altimeter',4,.18,'Electrical','Surface','Измеряет высоту над поверхностью и скорость снижения.',{'surface':True,'electricConsumption':1}),
('transmitter','Передатчик телеметрии','Transmitter',10,.25,'Electrical','Surface','Передаёт данные при наличии антенны и питания.',{'surface':True,'electricConsumption':12}),
('capsule','Герметичная капсула','Capsule',320,.9,'Payload','Stack','Возвращаемый модуль с базовыми приборами и запасом энергии.',{'electricCapacity':3000,'dragCoefficient':.5,'radius':.38}),
('satellite','Спутниковый блок','Satellite',110,.65,'Payload','Payload','Автономная полезная нагрузка для вывода на орбиту.',{'electricConsumption':10,'electricCapacity':1000,'radius':.3}),
('docking_port','Стыковочный узел','DockingPort',45,.22,'Payload','Stack','Соединяет аппараты при малой относительной скорости.',{'radius':.28}),
('cargo_bay','Грузовой отсек','CargoBay',150,.9,'Payload','Stack','Укрывает полезную нагрузку во время подъёма.',{}),
('payload_separator','Механизм отделения нагрузки','PayloadSeparator',25,.15,'Payload','Stack','Отделяет нагрузку от носителя на заданной высоте.',{}),
('parachute','Основной парашют','MainParachute',35,.3,'Payload','Surface','Безопасное раскрытие ниже 5 км и медленнее 180 м/с.',{'surface':True,'parachuteArea':400}),
('drogue','Тормозной парашют','DrogueParachute',15,.24,'Payload','Surface','Предварительное торможение до раскрытия основного купола.',{'surface':True,'parachuteArea':35}),
('landing_leg','Посадочная опора','LandingLeg',35,.65,'Payload','Surface','Смягчает посадку. Установите не менее трёх опор.',{'surface':True}),
('escape_tower','Система аварийного спасения','EscapeTower',95,.8,'Payload','Stack','Отводит капсулу от аварийного носителя.',{'thrust':120000,'fuelCapacity':60,'specificImpulse':180}),
('recorder','Регистратор миссии','FlightRecorder',8,.2,'Payload','Surface','Сохраняет историю полёта при наличии питания.',{'surface':True,'electricConsumption':1})
]

parts = []
for ident,title,kind,mass,height,category,mount,description,extra in rows:
    p = dict(id=ident,title=title,kind=kind,dryMass=mass,height=height,category=category,mount=mount,description=description,radius=.45,dragCoefficient=.3,specificImpulse=300,temperatureLimit=900,strength=350000,heatCapacity=800,price=round(mass*3+100))
    p.update(extra)
    parts.append(p)
(DEST / 'parts.json').write_text(json.dumps({'parts':parts},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(parts)} definitions written')
