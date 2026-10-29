using System.Collections.Generic;
using UnityEngine;

namespace BRP
{
    public sealed class FlightPresentation : MonoBehaviour
    {
        public Transform rocketRoot;
        public bool map;
        public Camera viewCamera;
        public Material lineMaterial;
        readonly Dictionary<string,GameObject> visuals=new Dictionary<string,GameObject>();
        readonly List<ParticleSystem> exhausts=new List<ParticleSystem>();
        LineRenderer orbitLine,trail;
        readonly List<Vector3> trailPoints=new List<Vector3>();
        GameObject planet,pad,canopy;
        LabSceneAssets assets;
        FlightBody body;
        float trailTime;
        Transform targetMarker;
        public void Initialize(FlightBody flight,LabSceneAssets source,Camera camera)
        {
            body=flight;assets=source;viewCamera=camera;lineMaterial=source.orbitMaterial;
            rocketRoot=new GameObject("FlightCraft").transform;rocketRoot.SetParent(transform,false);
            foreach(var part in flight.parts)
            {
                var model=Instantiate(part.definition.model,rocketRoot);
                model.transform.localPosition=part.placement.position;
                model.transform.localRotation=part.placement.rotation;
                visuals[part.placement.instanceId]=model;
                if(part.definition.IsEngine)
                {
                    var exhaust=new GameObject("EngineExhaust").AddComponent<ParticleSystem>();
                    exhaust.transform.SetParent(model.transform,false);exhaust.transform.localRotation=Quaternion.Euler(90,0,0);
                    var main=exhaust.main;main.startLifetime=.35f;main.startSpeed=7;main.startSize=.28f;
                    main.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.35f,.05f,.9f),new Color(.3f,.65f,1,.5f));
                    main.maxParticles=300;main.simulationSpace=ParticleSystemSimulationSpace.World;
                    var emission=exhaust.emission;emission.rateOverTime=0;
                    var shape=exhaust.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=7;shape.radius=.13f;
                    var renderer=exhaust.GetComponent<ParticleSystemRenderer>();renderer.material=lineMaterial;
                    exhausts.Add(exhaust);
                }
            }
            pad=Instantiate(source.launchpad,transform);
            pad.transform.localScale=Vector3.one*1.4f;
            if(source.planet!=null)
            {
                planet=Instantiate(source.planet,transform);planet.name="Earth";
                planet.transform.localScale=Vector3.one*50;
                planet.transform.localPosition=new Vector3(0,-50,0);
                foreach(var renderer in planet.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=source.planetMaterial;
            }
            if(source.canopy!=null)
            {
                canopy=Instantiate(source.canopy,rocketRoot);canopy.transform.localPosition=Vector3.up*10;
                canopy.SetActive(false);
            }
            orbitLine=CreateLine("Orbit",UiTheme.Cyan,.08f);
            trail=CreateLine("Trajectory",UiTheme.Orange,.06f);
            targetMarker=new GameObject("TargetMarker").transform;targetMarker.SetParent(transform,false);
        }
        LineRenderer CreateLine(string name,Color color,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(transform,false);
            line.material=lineMaterial;line.startColor=line.endColor=color;line.startWidth=line.endWidth=width;line.positionCount=0;return line;
        }
        public void Refresh(FlightBody flight,FlightBody target)
        {
            body=flight;
            foreach(var part in flight.parts)
                if(visuals.TryGetValue(part.placement.instanceId,out var visual)) visual.SetActive(!part.destroyed);
            float intensity=(float)Mathf.Clamp01((float)flight.lastThrust/900000);
            foreach(var exhaust in exhausts)
            {
                if(exhaust==null) continue;
                var emission=exhaust.emission;emission.rateOverTime=intensity*180;
                var main=exhaust.main;main.startSpeed=4+intensity*8;
            }
            if(canopy!=null) canopy.SetActive(flight.chuteDeployed);
            if(map)
            {
                float scale=50f/(float)OrbitalMechanics.Radius;
                rocketRoot.localPosition=flight.position.Float*scale;
                rocketRoot.localScale=Vector3.one*.18f;
                rocketRoot.rotation=flight.orientation.Float;
                if(planet!=null) {planet.transform.localPosition=Vector3.zero;planet.SetActive(true);}
                pad.SetActive(false);
                viewCamera.transform.position=new Vector3(90,60,-105);viewCamera.transform.LookAt(Vector3.zero);
                var r=flight.position;var v=flight.velocity;var points=new Vector3[240];
                double step=double.IsInfinity(flight.Orbit.period)?10:flight.Orbit.period/239;
                step=System.Math.Clamp(step,1,60);
                for(int i=0;i<points.Length;i++)
                {
                    points[i]=r.Float*scale;
                    OrbitalMechanics.Integrate(ref r,ref v,step,(p,s)=>OrbitalMechanics.Gravity(p));
                }
                orbitLine.positionCount=points.Length;orbitLine.SetPositions(points);trail.positionCount=0;
            }
            else
            {
                float altitude=(float)flight.Altitude;
                var location=new Vector3((float)(flight.position.x/8000),Mathf.Min(90,altitude/2000),0);
                rocketRoot.localPosition=location+new Vector3(0,2,0);
                rocketRoot.localScale=Vector3.one*.7f;rocketRoot.rotation=flight.orientation.Float;
                var cameraOffset=new Vector3(7,4,-11);
                viewCamera.transform.position=Vector3.Lerp(viewCamera.transform.position,rocketRoot.position+cameraOffset,Time.unscaledDeltaTime*2);
                viewCamera.transform.LookAt(rocketRoot.position+Vector3.up*1.4f);
                pad.SetActive(altitude<4000);
                if(planet!=null) {planet.transform.localPosition=new Vector3(0,-50,0);planet.SetActive(true);}
                orbitLine.positionCount=0;
                trailTime+=Time.unscaledDeltaTime;
                if(trailTime>.2f) {trailTime=0;trailPoints.Add(rocketRoot.position);if(trailPoints.Count>300) trailPoints.RemoveAt(0);}
                trail.positionCount=trailPoints.Count;trail.SetPositions(trailPoints.ToArray());
            }
        }
    }
}
