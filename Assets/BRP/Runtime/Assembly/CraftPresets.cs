using UnityEngine;

namespace BRP
{
    public static class CraftPresets
    {
        public static CraftBlueprint Create(PartCatalog catalog,int level=0)
        {
            var craft=new CraftBlueprint {name=level==0?"БРП-01 / Учебная":level==1?"БРП-02 / Орбитальная":"БРП-03 / Возвращаемая"};
            PlacedPart Add(PartKind kind,Vector3 position,PlacedPart parent=null,int port=0,int stage=0,Quaternion? rotation=null)
            {
                var p=new PlacedPart {partId=catalog.Find(kind).id,position=position,parentId=parent?.instanceId,port=port,stage=stage,rotation=rotation??Quaternion.identity};
                craft.parts.Add(p);return p;
            }
            void Surface(PartKind kind,PlacedPart parent,int port,float y,float angle=0)
            {
                var q=Quaternion.Euler(0,angle,0);
                Add(kind,parent.position+q*new Vector3(catalog.Find(parent.partId).radius,y,0),parent,port,parent.stage,q);
            }
            var first=Add(PartKind.FirstStage,Vector3.zero);
            Add(PartKind.BoosterEngine,new Vector3(0,-1.1f,0),first,1);
            for(int i=0;i<4;i++) Surface(PartKind.Fin,first,2+i*2,.05f,i*90);
            PlacedPart top=first;float height=1.8f;int upperStage=0;
            if(level>0)
            {
                top=Add(PartKind.StackDecoupler,new Vector3(0,height,0),top);height+=.18f;
                top=Add(PartKind.VacuumEngine,new Vector3(0,height,0),top,0,1);height+=.8f;
                top=Add(PartKind.SecondStage,new Vector3(0,height,0),top,0,1);height+=1.3f;upperStage=1;
                Surface(PartKind.RcsBlock,top,2,.5f);
                Surface(PartKind.ServiceTank,top,4,.5f,90);
                Surface(PartKind.SolarPanel,top,6,.7f,180);
                Surface(PartKind.PowerDistributor,top,8,.3f,270);
            }
            top=Add(PartKind.Gimbal,new Vector3(0,height,0),top,0,upperStage);height+=.25f;
            var instruments=Add(PartKind.InstrumentBay,new Vector3(0,height,0),top,0,upperStage);height+=.36f;
            Surface(PartKind.FlightComputer,instruments,2,.05f);
            Surface(PartKind.Gyroscope,instruments,4,.05f,90);
            Surface(PartKind.Battery,instruments,6,.05f,180);
            Surface(PartKind.Transmitter,instruments,8,.05f,270);
            Surface(PartKind.Antenna,instruments,3,.1f,45);
            Surface(PartKind.Altimeter,instruments,5,.1f,135);
            Surface(PartKind.Thermometer,instruments,7,.1f,225);
            Surface(PartKind.FlightRecorder,instruments,9,.1f,315);
            top=instruments;
            if(level>0) {top=Add(PartKind.PayloadSeparator,new Vector3(0,height,0),top,0,upperStage);height+=.15f;}
            if(level>1) {top=Add(PartKind.HeatShield,new Vector3(0,height,0),top,0,upperStage);height+=.2f;}
            var capsule=Add(level==1?PartKind.Satellite:PartKind.Capsule,new Vector3(0,height,0),top,0,upperStage);
            height+=catalog.Find(capsule.partId).height;
            Surface(PartKind.Window,capsule,2,.3f);
            Surface(PartKind.MainParachute,capsule,4,.4f,90);
            Surface(PartKind.DrogueParachute,capsule,6,.3f,180);
            if(level>1) {Add(PartKind.DockingPort,new Vector3(0,height,0),capsule,0,upperStage);height+=.22f;}
            else Add(PartKind.Fairing,new Vector3(0,height,0),capsule,0,upperStage);
            return craft;
        }
    }
}
