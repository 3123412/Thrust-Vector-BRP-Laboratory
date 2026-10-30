using UnityEngine;

namespace BRP
{
    public static class CraftPresets
    {
        sealed class Builder
        {
            readonly PartCatalog catalog;
            public readonly CraftBlueprint craft;
            public Builder(PartCatalog catalog,string name) {this.catalog=catalog;craft=new CraftBlueprint {name=name};}
            public PlacedPart Add(PartKind kind,Vector3 position,PlacedPart parent=null,int port=0,int stage=0,Quaternion? rotation=null)
            {
                var part=new PlacedPart {partId=catalog.Find(kind).id,position=position,parentId=parent?.instanceId,port=port,stage=stage,rotation=rotation??Quaternion.identity};
                craft.parts.Add(part);return part;
            }
            public PlacedPart Above(PartKind kind,PlacedPart parent,int stage=-1)
                =>Add(kind,parent.position+Vector3.up*catalog.Find(parent.partId).height,parent,0,stage<0?parent.stage:stage);
            public void Surface(PartKind kind,PlacedPart parent,int port,float height=.3f)
            {
                var rotation=Quaternion.Euler(0,(port-2)*45,0);
                Add(kind,parent.position+rotation*new Vector3(catalog.Find(parent.partId).radius,height,0),parent,port,parent.stage,rotation);
            }
            public void CapsuleSystems(PlacedPart capsule)
            {
                PartKind[] kinds={PartKind.Window,PartKind.MainParachute,PartKind.DrogueParachute,PartKind.FlightComputer,PartKind.Gyroscope,PartKind.ReactionWheel,PartKind.Battery,PartKind.FlightRecorder};
                for(int i=0;i<kinds.Length;i++) Surface(kinds[i],capsule,2+i);
            }
            public void SatelliteSystems(PlacedPart satellite)
            {
                PartKind[] kinds={PartKind.SolarPanel,PartKind.Antenna,PartKind.Transmitter,PartKind.Battery,PartKind.PowerDistributor,PartKind.FlightComputer,PartKind.Gyroscope,PartKind.ReactionWheel};
                for(int i=0;i<kinds.Length;i++) Surface(kinds[i],satellite,2+i,.2f);
            }
        }

        public static CraftBlueprint Create(PartCatalog catalog,int level=0)
        {
            var b=new Builder(catalog,level==0?"БРП-01 / Учебная":level==1?"БРП-02 / Орбитальная":"БРП-03 / Возвращаемая");
            var first=b.Add(PartKind.FirstStage,Vector3.zero);
            b.Add(PartKind.BoosterEngine,Vector3.down*catalog.Find(PartKind.BoosterEngine).height,first,1);
            for(int i=0;i<4;i++) b.Surface(PartKind.Fin,first,2+i*2,.05f);
            PartKind[] fuelSystem={PartKind.FuelLine,PartKind.FuelPump,PartKind.FuelValve,PartKind.Pressurizer};
            for(int i=0;i<4;i++) b.Surface(fuelSystem[i],first,3+i*2,.9f);
            var top=b.Above(PartKind.Adapter,first);
            if(level>0)
            {
                top=b.Above(PartKind.StackDecoupler,top);
                top=b.Above(PartKind.VacuumEngine,top,1);
                top=b.Above(PartKind.SecondStage,top);
                b.Surface(PartKind.RcsBlock,top,2,.5f);b.Surface(PartKind.ServiceTank,top,4,.5f);
                b.Surface(PartKind.SolarPanel,top,6,.7f);b.Surface(PartKind.PowerDistributor,top,8,.3f);
                b.Surface(PartKind.FuelCell,top,3,.5f);b.Surface(PartKind.HorizonSensor,top,5,.5f);
            }
            top=b.Above(PartKind.Gimbal,top);
            var instruments=b.Above(PartKind.InstrumentBay,top);
            PartKind[] systems={PartKind.FlightComputer,PartKind.Gyroscope,PartKind.Battery,PartKind.Transmitter,PartKind.Antenna,PartKind.Altimeter,PartKind.Thermometer,PartKind.FlightRecorder};
            for(int i=0;i<systems.Length;i++) b.Surface(systems[i],instruments,2+i,.1f);
            top=instruments;
            if(level==1)
            {
                top=b.Above(PartKind.PayloadSeparator,top);
                var satellite=b.Above(PartKind.Satellite,top);b.SatelliteSystems(satellite);
                b.Above(PartKind.Fairing,satellite);
            }
            else
            {
                if(level>1) top=b.Above(PartKind.StackDecoupler,top);
                top=b.Above(PartKind.HeatShield,top);
                var capsule=b.Above(PartKind.Capsule,top);b.CapsuleSystems(capsule);
                if(level>1)
                {
                    b.Surface(PartKind.Radiator,top,2,.1f);b.Surface(PartKind.Insulation,top,3,.1f);
                    b.Surface(PartKind.ServiceTank,top,4,.1f);b.Surface(PartKind.FuelCell,top,5,.1f);
                    b.Surface(PartKind.PowerDistributor,top,6,.1f);b.Surface(PartKind.RcsBlock,top,7,.1f);
                }
                top=b.Above(PartKind.DockingPort,capsule);
                if(level>1)
                {
                    top=b.Above(PartKind.EscapeTower,top);
                    top=b.Above(PartKind.PayloadSeparator,top);
                    var satellite=b.Above(PartKind.Satellite,top);b.SatelliteSystems(satellite);
                    b.Above(PartKind.Fairing,satellite);
                }
                else b.Above(PartKind.Fairing,top);
            }
            return b.craft;
        }

        public static CraftBlueprint OrbitalVehicle(PartCatalog catalog)
        {
            var b=new Builder(catalog,"БРП / Орбитальный буксир");
            var shield=b.Add(PartKind.HeatShield,Vector3.zero);
            var capsule=b.Above(PartKind.Capsule,shield);b.CapsuleSystems(capsule);
            b.Above(PartKind.DockingPort,capsule);
            b.Surface(PartKind.RcsBlock,shield,2,.1f);b.Surface(PartKind.ServiceTank,shield,4,.1f);
            b.Surface(PartKind.SolarPanel,shield,6,.1f);b.Surface(PartKind.PowerDistributor,shield,8,.1f);
            return b.craft;
        }
    }
}
