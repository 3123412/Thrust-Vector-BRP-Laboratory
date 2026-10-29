using System;
using UnityEngine;

namespace BRP
{
    public enum PartKind
    {
        BoosterEngine, FirstStage, Adapter, VacuumEngine, SecondStage, Fin, InstrumentBay, Fairing, Window, Antenna,
        FuelTank, OxidizerTank, ServiceTank, SolidBooster, RadialDecoupler, StackDecoupler, FuelLine, FuelPump, FuelValve, Pressurizer,
        Gimbal, VernierEngine, RcsBlock, ReactionWheel, Gyroscope, HorizonSensor, ControlFin, AirBrake, Ballast, FlightComputer,
        Battery, SolarPanel, FuelCell, PowerDistributor, Radiator, Insulation, HeatShield, Thermometer, Altimeter, Transmitter,
        Capsule, Satellite, DockingPort, CargoBay, PayloadSeparator, MainParachute, DrogueParachute, LandingLeg, EscapeTower, FlightRecorder
    }

    public enum PartCategory { Structure, Propulsion, Control, Electrical, Payload }
    public enum MountType { Stack, Surface, Engine, Payload }

    [CreateAssetMenu(menuName = "BRP/Part")]
    public sealed class PartDefinition : ScriptableObject
    {
        public string id;
        public string title;
        [TextArea] public string description;
        public PartKind kind;
        public PartCategory category;
        public MountType mount;
        public GameObject model;
        public Sprite thumbnail;
        public float dryMass;
        public float height;
        public float radius = 0.45f;
        public float fuelCapacity;
        public float oxidizerCapacity;
        public float thrust;
        public float specificImpulse = 300;
        public float dragCoefficient = 0.3f;
        public float liftArea;
        public float electricCapacity;
        public float electricGeneration;
        public float electricConsumption;
        public float controlTorque;
        public float temperatureLimit = 900;
        public float strength = 350000;
        public float heatCapacity = 800;
        public float coolingArea;
        public float parachuteArea;
        public float price = 100;
        public bool surface;

        public bool IsEngine => thrust > 0;
        public bool IsStructural => !surface && mount != MountType.Engine;
    }
}
