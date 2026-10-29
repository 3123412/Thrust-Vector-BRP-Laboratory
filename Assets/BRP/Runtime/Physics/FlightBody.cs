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
        public double fuel, oxidizer, energy, temperature = 288;
        public bool active = true;
        public bool destroyed;
        public double Mass => destroyed ? 0 : definition.dryMass + fuel + oxidizer;
    }

    public enum GuidanceMode { Manual, Hold, Prograde, Retrograde, Radial, Normal, Target }

    public sealed class FlightBody
    {
        public readonly List<PartState> parts = new List<PartState>();
        public DVector3 position, velocity, angularVelocity, centerOfMass;
        public DQuaternion orientation = DQuaternion.Identity;
        public InertiaTensor inertia;
        public double mass, elapsed, maxAltitude, maxSpeed, lastThrust, dynamicPressure, heatFlux, windSpeed;
        public double maxG, totalImpulse, totalFuelUsed, poweredTime, telemetryTime, stableTime;
        public double throttle = 1;
        public int stage;
        public bool launched, landed, crashed, chuteDeployed, brakesDeployed, valveOpen = true, stageEvent, payloadReleased, aborted, docked;
        public GuidanceMode guidance = GuidanceMode.Hold;
        public DVector3 manualInput;
        public DVector3 translationInput;
        public DVector3 targetDirection;
        public string lastEvent = "Ожидание запуска";
        public event Action<PartState> PartFailed;
        public double Altitude => position.Magnitude - OrbitalMechanics.Radius;
        public DVector3 SurfaceVelocity => velocity - OrbitalMechanics.AtmosphereVelocity(position);
        public double VerticalSpeed => DVector3.Dot(SurfaceVelocity,position.Normalized);
        public double Energy => parts.Sum(p=>p.destroyed ? 0 : p.energy);
        public double EnergyCapacity => parts.Where(p=>!p.destroyed).Sum(p=>(double)p.definition.electricCapacity);
        public double Fuel => parts.Where(p=>!p.destroyed).Sum(p=>p.fuel+p.oxidizer);
        public double Temperature => parts.Count>0 ? parts.Max(p=>p.temperature) : 288;
        public OrbitElements Orbit => OrbitalMechanics.Elements(position,velocity);
        public bool Has(PartKind kind) => parts.Any(p=>!p.destroyed && p.definition.kind==kind);
        public bool Powered => Energy > 0;
        public bool Telemetry => Has(PartKind.Antenna) && Has(PartKind.Transmitter) && Powered;
        public double Tilt => Math.Acos(Math.Clamp(DVector3.Dot(orientation.Rotate(new DVector3(0,1,0)),position.Normalized),-1,1))*180/Math.PI;

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
            velocity=OrbitalMechanics.AtmosphereVelocity(position);
        }

        FlightBody(List<PartState> detached, FlightBody source)
        {
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
                inertia.Add(part.Mass,new DVector3(center)-centerOfMass,part.definition.radius,part.definition.height);
            }
        }

        public double AvailableThrust => parts.Where(p=>!p.destroyed && p.placement.stage==stage).Sum(p=>(double)p.definition.thrust);
        public double TWR => AvailableThrust / Math.Max(1,mass*OrbitalMechanics.StandardGravity);
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
            bool sunlight=position.x>0 || position.y*position.y+position.z*position.z>OrbitalMechanics.Radius*OrbitalMechanics.Radius;
            bool bus=Has(PartKind.PowerDistributor);
            foreach(var p in parts.Where(p=>!p.destroyed))
            {
                consumption+=p.definition.electricConsumption;
                if(p.definition.kind==PartKind.SolarPanel && sunlight && bus) generation+=p.definition.electricGeneration;
                if(p.definition.kind==PartKind.FuelCell && bus && Fuel>0)
                {
                    double used=Drain(dt*.01,q=>q.fuel,(q,v)=>q.fuel=v,q=>q.definition.kind==PartKind.ServiceTank);
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
            if(!launched || landed || crashed || mass<=0) return;
            elapsed+=dt;
            UpdatePower(dt);
            var localForce=new DVector3();
            var localTorque=new DVector3();
            lastThrust=0;
            foreach(var p in parts.Where(p=>!p.destroyed&&p.active&&p.definition.thrust>0&&p.placement.stage==stage))
            {
                var d=p.definition;
                bool solid=d.kind==PartKind.SolidBooster || d.kind==PartKind.EscapeTower;
                double setting=solid ? 1 : Math.Clamp(throttle,0,1);
                if(!solid&&!valveOpen) setting=0;
                if(d.kind==PartKind.EscapeTower&&!aborted) setting=0;
                if(d.kind==PartKind.VernierEngine && manualInput.Magnitude<.01 && guidance==GuidanceMode.Manual) setting=0;
                double seaLevelFactor=1-.15*Math.Exp(-Math.Max(0,Altitude)/7000);
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
                    bool Eligible(PartState q)=>q.definition.kind!=PartKind.SolidBooster&&q.definition.kind!=PartKind.EscapeTower&&(q.placement.stage==stage||crossfeed);
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
                var force=new DVector3(p.placement.rotation*Vector3.up)*thrust;
                localForce+=force;
                localTorque+=DVector3.Cross(new DVector3(p.placement.position)-centerOfMass,force);
            }
            if(lastThrust>0) poweredTime+=dt;
            totalImpulse+=lastThrust*dt;
            if(Telemetry) telemetryTime+=dt;
            if(Tilt<10) stableTime+=dt;
            RecalculateMass();
            double altitude=Math.Max(0,Altitude);
            double density=OrbitalMechanics.Density(altitude);
            var wind=new DVector3(windSpeed,0,0);
            var airVelocity=SurfaceVelocity-wind;
            double speed=airVelocity.Magnitude;
            dynamicPressure=.5*density*speed*speed;
            var airLocal=orientation.Inverse.Rotate(airVelocity);
            double maxRadius=parts.Where(p=>!p.destroyed).Select(p=>(double)p.definition.radius).DefaultIfEmpty(.3).Max();
            double frontalArea=Math.PI*maxRadius*maxRadius;
            bool faired=Has(PartKind.Fairing);
            double dragArea=frontalArea*(faired?.22:.55);
            if(brakesDeployed) dragArea+=parts.Where(p=>!p.destroyed&&p.definition.kind==PartKind.AirBrake).Count()*3;
            if(chuteDeployed) dragArea+=parts.Where(p=>!p.destroyed).Sum(p=>(double)p.definition.parachuteArea)*1.5;
            var drag=airVelocity.Normalized*(-dynamicPressure*dragArea);
            foreach(var p in parts.Where(p=>!p.destroyed&&p.definition.liftArea>0))
            {
                var lateral=new DVector3(-airLocal.x,0,-airLocal.z);
                var aero=lateral*(density*speed*p.definition.liftArea*.7);
                localForce+=aero;
                localTorque+=DVector3.Cross(new DVector3(p.placement.position)-centerOfMass,aero);
            }
            var command=manualInput;
            if(guidance!=GuidanceMode.Manual && Has(PartKind.FlightComputer)&&Powered)
            {
                var up=position.Normalized;
                var desired=guidance==GuidanceMode.Prograde?velocity.Normalized:guidance==GuidanceMode.Retrograde?-velocity.Normalized:guidance==GuidanceMode.Normal?DVector3.Cross(position,velocity).Normalized:guidance==GuidanceMode.Target?targetDirection.Normalized:up;
                var desiredLocal=orientation.Inverse.Rotate(desired);
                command=DVector3.Cross(new DVector3(0,1,0),desiredLocal)*4-angularVelocity*2;
                command=new DVector3(Math.Clamp(command.x,-1,1),Math.Clamp(command.y,-1,1),Math.Clamp(command.z,-1,1));
            }
            double authority=0;
            foreach(var p in parts.Where(p=>!p.destroyed))
            {
                var k=p.definition.kind;
                if(k==PartKind.Gimbal && lastThrust>0&&Powered) authority+=p.definition.controlTorque;
                if(k==PartKind.ReactionWheel&&Powered) authority+=p.definition.controlTorque;
                if(k==PartKind.ControlFin) authority+=p.definition.controlTorque*Math.Min(1,dynamicPressure/5000);
                if(k==PartKind.RcsBlock&&Has(PartKind.ServiceTank)&&Fuel>0)
                {
                    double used=Drain(dt*.03*command.Magnitude,q=>q.fuel,(q,v)=>q.fuel=v,q=>q.definition.kind==PartKind.ServiceTank);
                    if(used>0) authority+=p.definition.controlTorque;
                }
            }
            localTorque+=command*authority;
            if(Has(PartKind.Gyroscope)&&Powered) localTorque-=angularVelocity*(authority*.4);
            angularVelocity+=inertia.Solve(localTorque-DVector3.Cross(angularVelocity,inertia.Multiply(angularVelocity)))*dt;
            if(angularVelocity.Magnitude>15) angularVelocity=angularVelocity.Normalized*15;
            orientation.Integrate(angularVelocity,dt);
            var external=orientation.Rotate(localForce)+drag;
            if(translationInput.Magnitude>0&&Has(PartKind.RcsBlock)&&Has(PartKind.ServiceTank))
            {
                double used=Drain(dt*.1*translationInput.Magnitude,q=>q.fuel,(q,v)=>q.fuel=v,q=>q.definition.kind==PartKind.ServiceTank);
                if(used>0) external+=orientation.Rotate(translationInput)*1200;
            }
            maxG=Math.Max(maxG,external.Magnitude/Math.Max(1,mass)/OrbitalMechanics.StandardGravity);
            var constantAcceleration=external/Math.Max(1,mass);
            OrbitalMechanics.Integrate(ref position,ref velocity,dt,(r,v)=>OrbitalMechanics.Gravity(r)+constantAcceleration);
            heatFlux=1.83e-4*Math.Sqrt(Math.Max(0,density)/Math.Max(.1,maxRadius))*speed*speed*speed;
            double cooling=parts.Where(p=>!p.destroyed).Sum(p=>(double)p.definition.coolingArea);
            double protection=Has(PartKind.HeatShield)?.025:Has(PartKind.Insulation)?.3:1;
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
            maxAltitude=Math.Max(maxAltitude,Altitude);maxSpeed=Math.Max(maxSpeed,speed);
            if(Altitude<0)
            {
                double landingSpeed=SurfaceVelocity.Magnitude;
                double tolerance=Has(PartKind.LandingLeg)?12:8;
                crashed=landingSpeed>tolerance;landed=!crashed;
                position=position.Normalized*OrbitalMechanics.Radius;
                velocity=OrbitalMechanics.AtmosphereVelocity(position);
                lastEvent=crashed?$"Удар о поверхность: {landingSpeed:F1} м/с":$"Посадка: {landingSpeed:F1} м/с";
            }
            if(parts.All(p=>p.destroyed)) { crashed=true;lastEvent="Конструкция разрушена"; }
        }

        public FlightBody Separate(bool payload=false)
        {
            var cut=parts.FirstOrDefault(p=>!p.destroyed&&p.active&&(payload?p.definition.kind==PartKind.PayloadSeparator:p.definition.kind==PartKind.StackDecoupler||p.definition.kind==PartKind.RadialDecoupler)&&p.placement.stage<=stage);
            if(cut==null) { if(!payload) stage=Math.Min(5,stage+1);lastEvent="Следующая ступень";stageEvent=true;return null; }
            var ids=new HashSet<string>{cut.placement.instanceId};
            bool change;
            do { change=false;foreach(var p in parts) if(!string.IsNullOrEmpty(p.placement.parentId)&&ids.Contains(p.placement.parentId)) change|=ids.Add(p.placement.instanceId); } while(change);
            var upper=parts.Where(p=>ids.Contains(p.placement.instanceId)).ToList();
            var lower=parts.Except(upper).ToList();
            if(upper.Count==0||lower.Count==0) return null;
            cut.active=false;
            var retained=payload?lower:upper;
            var released=payload?upper:lower;
            var detached=new FlightBody(released,this);
            var previousCenter=centerOfMass;
            parts.Clear();parts.AddRange(retained);RecalculateMass();
            var shift=orientation.Rotate(centerOfMass-previousCenter);
            position+=shift;velocity+=orientation.Rotate(DVector3.Cross(angularVelocity,centerOfMass-previousCenter));
            var impulse=orientation.Rotate(new DVector3(0,1,0))*1500;
            velocity+=impulse/mass;detached.velocity-=impulse/detached.mass;
            if(!payload) stage=Math.Min(5,stage+1);
            else detached.throttle=0;
            stageEvent=true;payloadReleased|=payload;
            lastEvent=payload?"Полезная нагрузка отделена":"Ступень отделена";
            return detached;
        }

        public bool DeployParachutes()
        {
            if(!Has(PartKind.MainParachute)&&!Has(PartKind.DrogueParachute)) {lastEvent="Нет парашютной системы";return false;}
            double safeSpeed=Has(PartKind.DrogueParachute)?350:180;
            if(Altitude>5000||SurfaceVelocity.Magnitude>safeSpeed) {lastEvent="Рано раскрывать: ниже 5 км и медленнее "+safeSpeed+" м/с";return false;}
            chuteDeployed=true;lastEvent="Парашюты раскрыты";return true;
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
