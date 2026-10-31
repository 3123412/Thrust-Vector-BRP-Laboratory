using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BRP
{
    [Serializable]
    public sealed class PartState
    {
        public PlacedPart placement;
        public PartDefinition definition;
        public double fuel, oxidizer, energy, thrust, temperature = 288;
        public bool active = true;
        public bool destroyed;
        public double Mass => destroyed ? 0 : definition.dryMass + fuel + oxidizer;
    }

    public enum GuidanceMode { Manual, Hold, Prograde, Retrograde, Radial, Normal, Target }

    public sealed class FlightBody
    {
        public readonly List<PartState> parts = new List<PartState>();
        public CelestialBody referenceBody=CelestialBody.Earth;
        public bool IsLunar=>referenceBody==CelestialBody.Moon;
        public DVector3 position, velocity, angularVelocity, centerOfMass;
        public DQuaternion orientation = DQuaternion.Identity;
        public InertiaTensor inertia;
        public double mass, elapsed, maxAltitude, maxSpeed, lastThrust, dynamicPressure, heatFlux, windSpeed;
        public double maxG, totalImpulse, totalFuelUsed, poweredTime, telemetryTime, stableTime, recordedTime;
        public double throttle = 1;
        public double launchSupportAltitude=2;
        public int stage;
        public bool launched, landed, crashed, chuteDeployed, mainChuteDeployed, brakesDeployed, valveOpen = true, stageEvent, payloadReleased, aborted, docked;
        public GuidanceMode guidance = GuidanceMode.Hold;
        public DVector3 manualInput;
        public DVector3 translationInput;
        public DVector3 targetDirection;
        public string lastEvent = "Ожидание запуска";
        public event Action<PartState> PartFailed;
        public double Altitude => position.Magnitude - referenceBody.radius;
        public DVector3 SurfaceVelocity => velocity - referenceBody.SurfaceVelocity(position);
        public double VerticalSpeed => DVector3.Dot(SurfaceVelocity,position.Normalized);
        public double Mach => referenceBody.atmosphere&&Altitude<80000?(SurfaceVelocity-new DVector3(windSpeed,0,0)).Magnitude/OrbitalMechanics.SpeedOfSound(Altitude):0;
        public double Energy => parts.Sum(p=>p.destroyed ? 0 : p.energy);
        public double EnergyCapacity => parts.Where(p=>!p.destroyed).Sum(p=>(double)p.definition.electricCapacity);
        public double Fuel => parts.Where(p=>!p.destroyed).Sum(p=>p.fuel+p.oxidizer);
        public double Temperature => parts.Count>0 ? parts.Max(p=>p.temperature) : 288;
        public OrbitElements Orbit => OrbitalMechanics.Elements(position,velocity,referenceBody.mu,referenceBody.radius);
        public bool Has(PartKind kind) => parts.Any(p=>!p.destroyed && p.definition.kind==kind);
        public bool Powered => Energy > 0;
        public bool Telemetry => Has(PartKind.Antenna) && Has(PartKind.Transmitter) && Powered;
        public double Tilt => Math.Acos(Math.Clamp(DVector3.Dot(orientation.Rotate(new DVector3(0,1,0)),position.Normalized),-1,1))*180/Math.PI;
        public PartState DockingPort => parts.FirstOrDefault(p=>!p.destroyed&&p.definition.kind==PartKind.DockingPort);
        public DVector3 DockingAxis => DockingPort==null?orientation.Rotate(new DVector3(0,1,0)):orientation.Rotate(new DVector3(DockingPort.placement.rotation*Vector3.up));
        public DVector3 DockingPoint => DockingPort==null?position:position+orientation.Rotate(new DVector3(DockingPort.placement.position+DockingPort.placement.rotation*Vector3.up*DockingPort.definition.height)-centerOfMass);
        public DVector3 DockingVelocity => velocity+orientation.Rotate(DVector3.Cross(angularVelocity,orientation.Inverse.Rotate(DockingPoint-position)));

        public FlightBody(CraftBlueprint craft, PartCatalog catalog)
        {
            foreach(var placement in craft.parts)
            {
                var d=catalog.Find(placement.partId);
                if(d==null) throw new ArgumentException("Unknown part: "+placement.partId);
                parts.Add(new PartState { placement=placement,definition=d,fuel=d.fuelCapacity,oxidizer=d.oxidizerCapacity,energy=d.electricCapacity });
            }
            RecalculateMass();
            position=new DVector3(0,OrbitalMechanics.Radius+2,0);
            launchSupportAltitude=2+GroundClearance;
            position=new DVector3(0,OrbitalMechanics.Radius+launchSupportAltitude,0);
            velocity=OrbitalMechanics.AtmosphereVelocity(position);
        }

        FlightBody(List<PartState> detached, FlightBody source)
        {
            referenceBody=source.referenceBody;
            parts.AddRange(detached);
            stage=source.stage; launched=true; guidance=GuidanceMode.Manual;
            orientation=source.orientation; angularVelocity=source.angularVelocity;
            RecalculateMass();
            var localOffset=centerOfMass-source.centerOfMass;
            position=source.position+orientation.Rotate(localOffset);
            velocity=source.velocity+orientation.Rotate(DVector3.Cross(source.angularVelocity,localOffset));
        }

        public void RecalculateMass()
        {
            mass=parts.Sum(p=>p.Mass);
            centerOfMass=new DVector3();
            foreach(var part in parts)
            {
                var center=part.placement.position+part.placement.rotation*Vector3.up*(part.definition.height*.5f);
                centerOfMass+=new DVector3(center)*part.Mass;
            }
            if(mass>0) centerOfMass/=mass;
            inertia=new InertiaTensor();
            foreach(var part in parts)
            {
                var center=part.placement.position+part.placement.rotation*Vector3.up*(part.definition.height*.5f);
                inertia.Add(part.Mass,new DVector3(center)-centerOfMass,part.definition.radius,part.definition.height,new DVector3(part.placement.rotation*Vector3.up));
            }
        }

        void RefreshMassFrame()
        {
            var previousCenter=centerOfMass;
            RecalculateMass();
            var offset=centerOfMass-previousCenter;
            position+=orientation.Rotate(offset);
            velocity+=orientation.Rotate(DVector3.Cross(angularVelocity,offset));
        }

        void ApplyImpulse(DVector3 impulse,DVector3 point)
        {
            velocity+=impulse/mass;
            angularVelocity+=inertia.Solve(orientation.Inverse.Rotate(DVector3.Cross(point-position,impulse)));
        }

        public double AvailableThrust => parts.Where(p=>!p.destroyed&&p.active&&p.placement.stage==stage&&(p.definition.kind!=PartKind.EscapeTower||aborted)).Sum(p=>(double)p.definition.thrust);
        public double TWR => AvailableThrust / Math.Max(1,mass*referenceBody.SurfaceGravity);
        public double DeltaV
        {
            get
            {
                double remaining=mass, result=0;
                foreach(int s in parts.Select(p=>p.placement.stage).Distinct().OrderBy(s=>s))
                {
                    var stageParts=parts.Where(p=>!p.destroyed&&p.placement.stage==s).ToArray();
                    double propellant=stageParts.Sum(p=>p.fuel+p.oxidizer);
                    double thrust=stageParts.Sum(p=>(double)p.definition.thrust);
                    double isp=thrust>0 ? stageParts.Sum(p=>p.definition.thrust*p.definition.specificImpulse)/thrust : 0;
                    if(remaining>propellant && isp>0) result+=isp*OrbitalMechanics.StandardGravity*Math.Log(remaining/(remaining-propellant));
                    remaining-=stageParts.Sum(p=>p.Mass);
                }
                return result;
            }
        }

        double Drain(double amount, Func<PartState,double> read, Action<PartState,double> write, Func<PartState,bool> eligible)
        {
            double available=parts.Where(p=>!p.destroyed&&eligible(p)).Sum(read);
            double used=Math.Min(amount,available);
            if(available<=0) return 0;
            foreach(var p in parts.Where(p=>!p.destroyed&&eligible(p))) write(p,Math.Max(0,read(p)-used*read(p)/available));
            return used;
        }

        void UpdatePower(double dt)
        {
            double generation=0, consumption=0;
            double r=position.Magnitude;
            bool sunlight=position.x>0 || position.y*position.y+position.z*position.z>referenceBody.radius*referenceBody.radius;
            bool bus=Has(PartKind.PowerDistributor);
            foreach(var p in parts.Where(p=>!p.destroyed))
            {
                consumption+=p.definition.electricConsumption;
                if(p.definition.kind==PartKind.SolarPanel && sunlight && bus) generation+=p.definition.electricGeneration;
                if(p.definition.kind==PartKind.FuelCell && bus && Fuel>0)
                {
                    double used=Drain(dt*.01,q=>q.fuel,(q,v)=>q.fuel=v,q=>q.definition.kind==PartKind.ServiceTank);
                    totalFuelUsed+=used;
                    if(used>0) generation+=p.definition.electricGeneration;
                }
            }
            double change=(generation-consumption)*dt;
            if(change<0) Drain(-change,p=>p.energy,(p,v)=>p.energy=v,p=>true);
            else
            {
                double room=parts.Where(p=>!p.destroyed).Sum(p=>p.definition.electricCapacity-p.energy);
                if(room>0) foreach(var p in parts.Where(p=>!p.destroyed)) p.energy+=Math.Min(change,room)*(p.definition.electricCapacity-p.energy)/room;
            }
        }

        public void Step(double dt)
        {
            if(!double.IsFinite(dt)||dt<=0) return;
            if(dt>.020000001)
            {
                int count=(int)Math.Ceiling(dt/.02);
                for(int i=0;i<count;i++) Step(dt/count);
                return;
            }
            if(!launched || landed || crashed || mass<=0) return;
            elapsed+=dt;
            double initialMass=mass;
            var initialCenter=centerOfMass;
            UpdatePower(dt);
            var localForce=new DVector3();
            var localTorque=new DVector3();
            lastThrust=0;
            foreach(var part in parts) part.thrust=0;
            foreach(var p in parts.Where(p=>!p.destroyed&&p.active&&p.definition.thrust>0&&p.placement.stage==stage))
            {
                var d=p.definition;
                bool solid=d.kind==PartKind.SolidBooster || d.kind==PartKind.EscapeTower;
                double setting=solid ? 1 : Math.Clamp(throttle,0,1);
                if(!solid&&!valveOpen) setting=0;
                if(d.kind==PartKind.EscapeTower&&!aborted) setting=0;
                if(d.kind==PartKind.VernierEngine && manualInput.Magnitude<.01 && guidance==GuidanceMode.Manual) setting=0;
                double seaLevelFactor=referenceBody.atmosphere?1-.15*Math.Exp(-Math.Max(0,Altitude)/7000):1;
                double thrust=d.thrust*setting*seaLevelFactor;
                double needed=thrust/(d.specificImpulse*OrbitalMechanics.StandardGravity)*dt;
                if(needed<=0) continue;
                if(solid)
                {
                    double used=Math.Min(p.fuel,needed);p.fuel-=used;thrust*=used/needed;totalFuelUsed+=used;
                }
                else
                {
                    bool crossfeed=Has(PartKind.FuelLine)&&Has(PartKind.FuelPump)&&Powered;
                    bool Eligible(PartState q)=>q.definition.kind!=PartKind.SolidBooster&&q.definition.kind!=PartKind.EscapeTower&&q.definition.kind!=PartKind.ServiceTank&&(q.placement.stage==stage||crossfeed&&q.placement.stage<stage);
                    double f=parts.Where(q=>!q.destroyed&&Eligible(q)).Sum(q=>q.fuel);
                    double o=parts.Where(q=>!q.destroyed&&Eligible(q)).Sum(q=>q.oxidizer);
                    double fraction=Math.Min(1,Math.Min(f/(needed/3.44),o/(needed*2.44/3.44)));
                    if(Has(PartKind.FuelPump)&&!Has(PartKind.Pressurizer)) fraction*=.85;
                    Drain(needed*fraction/3.44,q=>q.fuel,(q,v)=>q.fuel=v,Eligible);
                    Drain(needed*fraction*2.44/3.44,q=>q.oxidizer,(q,v)=>q.oxidizer=v,Eligible);
                    totalFuelUsed+=needed*fraction;
                    thrust*=fraction;
                }
                lastThrust+=thrust;
                p.thrust=thrust;
                var force=new DVector3(p.placement.rotation*Vector3.up)*thrust;
                localForce+=force;
                localTorque+=DVector3.Cross(new DVector3(p.placement.position)-centerOfMass,force);
            }
            if(lastThrust>0) poweredTime+=dt;
            totalImpulse+=lastThrust*dt;
            if(Telemetry) telemetryTime+=dt;
            if(Has(PartKind.FlightRecorder)&&Powered) recordedTime+=dt;
            if(Tilt<10) stableTime+=dt;
            RefreshMassFrame();
            localTorque+=DVector3.Cross(initialCenter-centerOfMass,localForce);
            double altitude=Math.Max(0,Altitude);
            double density=referenceBody.atmosphere?OrbitalMechanics.Density(altitude):0;
            var wind=new DVector3(windSpeed,0,0);
            var airVelocity=SurfaceVelocity-wind;
            double speed=airVelocity.Magnitude;
            dynamicPressure=.5*density*speed*speed;
            var airLocal=orientation.Inverse.Rotate(airVelocity);
            double maxRadius=parts.Where(p=>!p.destroyed).Select(p=>(double)p.definition.radius).DefaultIfEmpty(.3).Max();
            double frontalArea=Math.PI*maxRadius*maxRadius;
            bool faired=Has(PartKind.Fairing);
            double dragArea=frontalArea*(faired?.22:Has(PartKind.Capsule)?1.1:.55);
            dragArea*=OrbitalMechanics.WaveDragMultiplier(speed/OrbitalMechanics.SpeedOfSound(altitude));
            if(Has(PartKind.CargoBay)) dragArea*=.85;
            if(brakesDeployed) dragArea+=parts.Where(p=>!p.destroyed&&p.definition.kind==PartKind.AirBrake).Count()*3;
            if(chuteDeployed) dragArea+=parts.Where(p=>!p.destroyed&&(mainChuteDeployed||p.definition.kind==PartKind.DrogueParachute)).Sum(p=>(double)p.definition.parachuteArea)*1.5;
            var drag=airVelocity.Normalized*(-dynamicPressure*dragArea);
            foreach(var p in parts.Where(p=>!p.destroyed&&p.definition.liftArea>0))
            {
                var aerodynamicCenter=p.placement.position+p.placement.rotation*Vector3.up*(p.definition.height*.5f);
                var offset=new DVector3(aerodynamicCenter)-centerOfMass;
                var surfaceVelocity=airLocal+DVector3.Cross(angularVelocity,offset);
                var lateral=new DVector3(-surfaceVelocity.x,0,-surfaceVelocity.z);
                var aero=lateral*(density*surfaceVelocity.Magnitude*p.definition.liftArea*.7);
                localForce+=aero;
                localTorque+=DVector3.Cross(offset,aero);
            }
            var command=manualInput;
            bool automatic=guidance!=GuidanceMode.Manual&&Has(PartKind.FlightComputer)&&Powered;
            if(automatic)
            {
                var up=position.Normalized;
                var desired=guidance==GuidanceMode.Prograde?velocity.Normalized:guidance==GuidanceMode.Retrograde?-velocity.Normalized:guidance==GuidanceMode.Normal?DVector3.Cross(position,velocity).Normalized:guidance==GuidanceMode.Target&&targetDirection.Magnitude>0?targetDirection.Normalized:up;
                var desiredLocal=orientation.Inverse.Rotate(desired);
                var turnAxis=DVector3.Cross(new DVector3(0,1,0),desiredLocal);
                double alignment=Math.Clamp(desiredLocal.y,-1,1);
                double turnAngle=Math.Atan2(turnAxis.Magnitude,alignment);
                if(turnAxis.Magnitude<1e-10&&alignment<0) turnAxis=new DVector3(1,0,0);
                command=turnAxis.Normalized*(turnAngle*6)-angularVelocity*4;
            }
            double authority=0;
            foreach(var p in parts.Where(p=>!p.destroyed))
            {
                var k=p.definition.kind;
                if(k==PartKind.Gimbal && lastThrust>0&&Powered) authority+=p.definition.controlTorque;
                if(k==PartKind.VernierEngine&&p.thrust>0) authority+=p.definition.controlTorque;
                if(k==PartKind.ReactionWheel&&Powered) authority+=p.definition.controlTorque;
                if(k==PartKind.ControlFin) authority+=p.definition.controlTorque*Math.Min(1,dynamicPressure/5000);
                if(k==PartKind.RcsBlock&&Has(PartKind.ServiceTank)&&Fuel>0)
                {
                    double needed=dt*.03*Math.Min(1,command.Magnitude);
                    double used=Drain(needed,q=>q.fuel,(q,v)=>q.fuel=v,q=>q.definition.kind==PartKind.ServiceTank);
                    totalFuelUsed+=used;
                    if(used>0) authority+=p.definition.controlTorque*used/needed;
                }
            }
            var steering=automatic?inertia.Multiply(command)-localTorque+DVector3.Cross(angularVelocity,inertia.Multiply(angularVelocity)):command*authority;
            if(!automatic&&Has(PartKind.Gyroscope)&&Powered) steering-=inertia.Multiply(angularVelocity)*3;
            if(steering.Magnitude>authority) steering=steering.Normalized*authority;
            localTorque+=steering;
            angularVelocity+=inertia.Solve(localTorque-DVector3.Cross(angularVelocity,inertia.Multiply(angularVelocity)))*dt;
            if(angularVelocity.Magnitude>15) angularVelocity=angularVelocity.Normalized*15;
            orientation.Integrate(angularVelocity,dt);
            var external=orientation.Rotate(localForce)+drag;
            if(translationInput.Magnitude>0&&Has(PartKind.RcsBlock)&&Has(PartKind.ServiceTank))
            {
                double needed=dt*.1*translationInput.Magnitude;
                double used=Drain(needed,q=>q.fuel,(q,v)=>q.fuel=v,q=>q.definition.kind==PartKind.ServiceTank);
                totalFuelUsed+=used;
                if(used>0) external+=orientation.Rotate(translationInput)*(1200*used/needed);
            }
            maxG=Math.Max(maxG,external.Magnitude/Math.Max(1,mass)/OrbitalMechanics.StandardGravity);
            var constantAcceleration=external/Math.Max(1,(initialMass+mass)*.5);
            OrbitalMechanics.Integrate(ref position,ref velocity,dt,(r,v)=>referenceBody.Gravity(r)+constantAcceleration);
            heatFlux=1.83e-4*Math.Sqrt(Math.Max(0,density)/Math.Max(.1,maxRadius))*speed*speed*speed;
            double cooling=parts.Where(p=>!p.destroyed).Sum(p=>(double)p.definition.coolingArea);
            double protection=Has(PartKind.HeatShield)?.025:Has(PartKind.Insulation)?.3:Has(PartKind.CargoBay)?.45:1;
            foreach(var p in parts.Where(p=>!p.destroyed).ToArray())
            {
                double absorbed=heatFlux*Math.PI*p.definition.radius*p.definition.radius*(p.definition.kind==PartKind.HeatShield?1:protection)*.08;
                double radiation=(p.temperature-270)*(2+cooling)*12;
                p.temperature=Math.Max(100,p.temperature+(absorbed-radiation)*dt/(p.definition.dryMass*p.definition.heatCapacity));
                double load=dynamicPressure*Math.Max(.05,p.definition.liftArea);
                if(p.temperature>p.definition.temperatureLimit || load>p.definition.strength)
                {
                    p.destroyed=true;lastEvent="Разрушение: "+p.definition.title;PartFailed?.Invoke(p);
                }
            }
            if(chuteDeployed&&dynamicPressure>35000)
            {
                foreach(var p in parts.Where(p=>p.definition.parachuteArea>0)) p.destroyed=true;
                chuteDeployed=false;lastEvent="Парашют сорван потоком";
            }
            RefreshMassFrame();
            maxAltitude=Math.Max(maxAltitude,Altitude);maxSpeed=Math.Max(maxSpeed,speed);
            if(!IsLunar&&maxAltitude<launchSupportAltitude+3&&Altitude<launchSupportAltitude&&!aborted)
            {
                var up=position.Normalized;position=up*(referenceBody.radius+launchSupportAltitude);
                double vertical=DVector3.Dot(SurfaceVelocity,up);if(vertical<0) velocity-=up*vertical;
            }
            double contactAltitude=GroundClearance;
            if(Altitude<contactAltitude)
            {
                double landingSpeed=SurfaceVelocity.Magnitude;
                double tolerance=IsLunar?4.5:parts.Count(p=>!p.destroyed&&p.definition.kind==PartKind.LandingLeg)>=3?12:8;
                crashed=landingSpeed>tolerance||IsLunar&&Tilt>15;landed=!crashed;
                position=position.Normalized*(referenceBody.radius+contactAltitude);
                velocity=referenceBody.SurfaceVelocity(position);
                lastEvent=crashed?$"Удар о поверхность: {landingSpeed:F1} м/с":$"Посадка: {landingSpeed:F1} м/с";
            }
            if(parts.All(p=>p.destroyed)) { crashed=true;lastEvent="Конструкция разрушена"; }
        }

        public double GroundClearance
        {
            get
            {
                double clearance=.1;
                var up=position.Normalized;
                foreach(var part in parts)
                {
                    if(part.destroyed) continue;
                    var axis=orientation.Rotate(new DVector3(part.placement.rotation*Vector3.up));
                    var center=new DVector3(part.placement.position)-centerOfMass+new DVector3(part.placement.rotation*Vector3.up)*(part.definition.height*.5);
                    double projection=Math.Clamp(DVector3.Dot(axis,up),-1,1);
                    double extent=Math.Abs(projection)*part.definition.height*.5+part.definition.radius*Math.Sqrt(Math.Max(0,1-projection*projection));
                    clearance=Math.Max(clearance,extent-DVector3.Dot(orientation.Rotate(center),up));
                }
                return clearance;
            }
        }
        public double GroundAltitude=>Math.Max(0,Altitude-GroundClearance);

        public FlightBody Separate(bool payload=false)
        {
            var cut=parts.FirstOrDefault(p=>!p.destroyed&&p.active&&(payload?p.definition.kind==PartKind.PayloadSeparator:p.definition.kind==PartKind.StackDecoupler||p.definition.kind==PartKind.RadialDecoupler)&&(payload||p.placement.stage<=stage));
            if(cut==null) { if(!payload) stage=Math.Min(5,stage+1);lastEvent="Следующая ступень";stageEvent=true;return null; }
            var ids=new HashSet<string>{cut.placement.instanceId};
            bool change;
            do { change=false;foreach(var p in parts) if(!string.IsNullOrEmpty(p.placement.parentId)&&ids.Contains(p.placement.parentId)) change|=ids.Add(p.placement.instanceId); } while(change);
            var upper=parts.Where(p=>ids.Contains(p.placement.instanceId)).ToList();
            var lower=parts.Except(upper).ToList();
            if(upper.Count==0||lower.Count==0) return null;
            cut.active=false;
            bool radial=cut.definition.kind==PartKind.RadialDecoupler;
            var retained=payload||radial?lower:upper;
            var released=payload||radial?upper:lower;
            var detached=new FlightBody(released,this);
            var previousCenter=centerOfMass;
            parts.Clear();parts.AddRange(retained);RecalculateMass();
            var shift=orientation.Rotate(centerOfMass-previousCenter);
            position+=shift;velocity+=orientation.Rotate(DVector3.Cross(angularVelocity,centerOfMass-previousCenter));
            var direction=radial?new DVector3(-cut.placement.position.x,0,-cut.placement.position.z).Normalized:new DVector3(0,payload?-1:1,0);
            var impulse=orientation.Rotate(direction)*1500;
            var releasePoint=position+orientation.Rotate(new DVector3(cut.placement.position)-centerOfMass);
            ApplyImpulse(impulse,releasePoint);detached.ApplyImpulse(-impulse,releasePoint);
            if(!payload&&!radial) stage=Math.Min(5,stage+1);
            else detached.throttle=0;
            stageEvent=true;payloadReleased|=payload;
            lastEvent=payload?"Полезная нагрузка отделена":"Ступень отделена";
            return detached;
        }

        public bool DeployParachutes()
        {
            if(!referenceBody.atmosphere) {lastEvent="На Луне нет атмосферы. Используйте посадочный двигатель.";return false;}
            if(!Has(PartKind.MainParachute)&&!Has(PartKind.DrogueParachute)) {lastEvent="Нет парашютной системы";return false;}
            double safeSpeed=Has(PartKind.DrogueParachute)?350:180;
            if(Altitude>5000||SurfaceVelocity.Magnitude>safeSpeed) {lastEvent="Рано раскрывать: ниже 5 км и медленнее "+safeSpeed+" м/с";return false;}
            chuteDeployed=true;
            mainChuteDeployed=SurfaceVelocity.Magnitude<=180&&Has(PartKind.MainParachute);
            lastEvent=mainChuteDeployed?"Основной парашют раскрыт":"Тормозной парашют раскрыт";return true;
        }

        public FlightBody SeparateCapsule(bool emergency=false)
        {
            var capsule=parts.FirstOrDefault(p=>!p.destroyed&&p.definition.kind==PartKind.Capsule);
            if(capsule==null) {lastEvent="Возвращаемая капсула не установлена";return null;}
            var shield=parts.FirstOrDefault(p=>p.placement.instanceId==capsule.placement.parentId&&p.definition.kind==PartKind.HeatShield);
            var root=shield??capsule;
            var ids=new HashSet<string>{root.placement.instanceId};
            bool changed;
            do {changed=false;foreach(var part in parts) if(ids.Contains(part.placement.parentId??"")) changed|=ids.Add(part.placement.instanceId);} while(changed);
            var released=parts.Where(p=>!ids.Contains(p.placement.instanceId)).ToList();
            if(released.Count==0) {lastEvent="Капсула уже отделена";return null;}
            var carrier=new FlightBody(released,this) {throttle=0};
            var previousCenter=centerOfMass;
            parts.RemoveAll(p=>!ids.Contains(p.placement.instanceId));RecalculateMass();
            var offset=centerOfMass-previousCenter;
            position+=orientation.Rotate(offset);velocity+=orientation.Rotate(DVector3.Cross(angularVelocity,offset));
            var impulse=orientation.Rotate(new DVector3(0,1,0))*500;
            var releasePoint=position+orientation.Rotate(new DVector3(root.placement.position)-centerOfMass);
            ApplyImpulse(impulse,releasePoint);carrier.ApplyImpulse(-impulse,releasePoint);
            root.placement.parentId=null;stage=capsule.placement.stage;throttle=0;aborted|=emergency;stageEvent=true;
            guidance=GuidanceMode.Retrograde;
            lastEvent=emergency?"Аварийное отделение капсулы":"Капсула отделена для возвращения";
            return carrier;
        }

        public bool CanDock(FlightBody other,out double distance,out double speed,out double angle)
        {
            distance=(DockingPoint-other.DockingPoint).Magnitude;
            speed=(DockingVelocity-other.DockingVelocity).Magnitude;
            angle=Math.Acos(Math.Clamp(DVector3.Dot(DockingAxis,-other.DockingAxis),-1,1))*180/Math.PI;
            return DockingPort!=null&&other.DockingPort!=null&&distance<=.6&&speed<=.2&&angle<=5;
        }

        public void Dock(FlightBody other)
        {
            double combined=mass+other.mass;
            var combinedPosition=(position*mass+other.position*other.mass)/combined;
            var combinedVelocity=(velocity*mass+other.velocity*other.mass)/combined;
            var angularMomentum=orientation.Rotate(inertia.Multiply(angularVelocity))+other.orientation.Rotate(other.inertia.Multiply(other.angularVelocity));
            angularMomentum+=DVector3.Cross(position-combinedPosition,velocity*mass)+DVector3.Cross(other.position-combinedPosition,other.velocity*other.mass);
            var offset=orientation.Inverse.Rotate(other.position-position);
            var relative=orientation.Inverse*other.orientation;
            foreach(var part in other.parts)
            {
                part.placement.position=(offset+relative.Rotate(new DVector3(part.placement.position)-other.centerOfMass)+centerOfMass).Float;
                part.placement.rotation=(relative*new DQuaternion(part.placement.rotation)).Float;
                parts.Add(part);
            }
            position=combinedPosition;velocity=combinedVelocity;RecalculateMass();
            angularVelocity=inertia.Solve(orientation.Inverse.Rotate(angularMomentum));docked=true;
            other.parts.Clear();other.landed=true;lastEvent="Стыковка выполнена";
        }
    }
}
