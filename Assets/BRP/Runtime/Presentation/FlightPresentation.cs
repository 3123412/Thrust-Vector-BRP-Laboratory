using System;
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
        readonly Dictionary<string,EngineVfx> exhausts=new Dictionary<string,EngineVfx>();
        LineRenderer orbitLine,trail,targetLine;
        readonly List<Vector3> trailPoints=new List<Vector3>();
        GameObject planet,pad,canopy,targetVisual,terrain;
        LabSceneAssets assets;
        float trailTime,orbitRefresh;
        Vector3 cameraVelocity;
        bool previousMap;
        const float ViewScale=1.5f;

        public void Initialize(FlightBody flight,LabSceneAssets source,Camera camera)
        {
            assets=source;viewCamera=camera;lineMaterial=source.orbitMaterial;
            RenderSettings.fog=false;RenderSettings.skybox=source.skyMaterial;
            viewCamera.farClipPlane=40000000;
            rocketRoot=new GameObject("FlightCraft").transform;rocketRoot.SetParent(transform,false);
            foreach(var part in flight.parts)
            {
                var model=Instantiate(part.definition.model,rocketRoot);
                model.transform.localPosition=part.placement.position;
                model.transform.localRotation=part.placement.rotation*(part.definition.surface?Quaternion.Euler(0,180,0):Quaternion.identity);
                visuals[part.placement.instanceId]=model;
                if(part.definition.IsEngine && source.engineEffect!=null)
                {
                    var effect=Instantiate(source.engineEffect,model.transform).GetComponent<EngineVfx>();
                    effect.transform.localPosition=Vector3.zero;effect.transform.localRotation=Quaternion.identity;
                    exhausts[part.placement.instanceId]=effect;
                }
            }
            pad=Instantiate(source.launchpad,transform);pad.transform.localScale=Vector3.one*ViewScale;
            if(source.terrain!=null) {terrain=Instantiate(source.terrain,transform);terrain.transform.localScale=Vector3.one*ViewScale;}
            planet=Instantiate(source.planet,transform);planet.name="Earth";
            foreach(var renderer in planet.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=source.planetMaterial;
            if(source.canopy!=null)
            {
                canopy=Instantiate(source.canopy,rocketRoot);canopy.transform.localPosition=Vector3.up*8;canopy.SetActive(false);
            }
            orbitLine=CreateLine("PredictedOrbit",UiTheme.Cyan,.065f);
            trail=CreateLine("FlightPath",new Color(.75f,.8f,.8f,.4f),.035f);
            targetLine=CreateLine("TargetVector",UiTheme.Orange,.025f);
            viewCamera.transform.position=new Vector3(12,6,-18);viewCamera.transform.LookAt(Vector3.zero);
            Refresh(flight,null);
        }

        LineRenderer CreateLine(string name,Color color,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(transform,false);
            line.material=lineMaterial;line.startColor=line.endColor=color;line.startWidth=line.endWidth=width;line.positionCount=0;return line;
        }

        public void Refresh(FlightBody flight,FlightBody target)
        {
            foreach(var entry in visuals) entry.Value.SetActive(false);
            float atmosphere=(float)Math.Clamp(OrbitalMechanics.Density(flight.Altitude)/1.225,0,1);
            foreach(var part in flight.parts)
            {
                if(visuals.TryGetValue(part.placement.instanceId,out var model)) model.SetActive(!part.destroyed);
                if(exhausts.TryGetValue(part.placement.instanceId,out var effect))
                {
                    bool active=!part.destroyed&&part.active&&part.placement.stage==flight.stage&&flight.lastThrust>0;
                    effect.SetDrive(active?(float)flight.throttle:0,atmosphere);
                }
            }
            foreach(var entry in exhausts)
                if(!flight.parts.Exists(p=>p.placement.instanceId==entry.Key)) entry.Value.SetDrive(0,0);
            if(canopy!=null) canopy.SetActive(flight.chuteDeployed);
            if(map) UpdateMap(flight,target);else UpdateChase(flight,target);
            previousMap=map;
        }

        void UpdateMap(FlightBody flight,FlightBody target)
        {
            float scale=50f/(float)OrbitalMechanics.Radius;
            planet.transform.localPosition=Vector3.zero;planet.transform.localScale=Vector3.one*50;
            planet.SetActive(true);if(terrain!=null) terrain.SetActive(false);
            rocketRoot.localPosition=flight.position.Float*scale;
            rocketRoot.localScale=Vector3.one*.14f;rocketRoot.rotation=flight.orientation.Float;
            pad.SetActive(false);trail.positionCount=0;
            var desired=new Vector3(90,60,-105);
            if(!previousMap) viewCamera.transform.position=desired;
            viewCamera.transform.position=Vector3.SmoothDamp(viewCamera.transform.position,desired,ref cameraVelocity,.4f);
            viewCamera.transform.LookAt(Vector3.zero);
            orbitRefresh-=Time.unscaledDeltaTime;
            if(orbitRefresh<=0)
            {
                orbitRefresh=.35f;
                var r=flight.position;var v=flight.velocity;var points=new Vector3[300];
                double step=double.IsInfinity(flight.Orbit.period)?10:flight.Orbit.period/299;
                step=Math.Clamp(step,1,45);
                for(int i=0;i<points.Length;i++)
                {
                    points[i]=r.Float*scale;
                    OrbitalMechanics.Integrate(ref r,ref v,step,(p,s)=>OrbitalMechanics.Gravity(p));
                }
                orbitLine.positionCount=points.Length;orbitLine.SetPositions(points);
            }
            UpdateTarget(target,target==null?Vector3.zero:target.position.Float*scale,.08f);
        }

        void UpdateChase(FlightBody flight,FlightBody target)
        {
            rocketRoot.localScale=Vector3.one*ViewScale;
            rocketRoot.rotation=flight.orientation.Float;
            rocketRoot.localPosition=-(rocketRoot.rotation*flight.centerOfMass.Float)*ViewScale;
            planet.transform.localScale=Vector3.one*(float)OrbitalMechanics.Radius*ViewScale;
            planet.transform.localPosition=-flight.position.Float*ViewScale;
            double a=flight.elapsed*OrbitalMechanics.RotationRate;
            var padPosition=new DVector3(-Math.Sin(a)*OrbitalMechanics.Radius,Math.Cos(a)*OrbitalMechanics.Radius,0);
            pad.transform.localPosition=(padPosition-flight.position).Float*ViewScale;
            pad.transform.localRotation=Quaternion.Euler(0,0,(float)(a*180/Math.PI));
            pad.SetActive(flight.Altitude<2500);
            planet.SetActive(flight.Altitude>8000);
            if(terrain!=null) {terrain.SetActive(flight.Altitude<12000);terrain.transform.localPosition=pad.transform.localPosition;terrain.transform.localRotation=pad.transform.localRotation;}
            orbitLine.positionCount=0;
            var desired=new Vector3(12,6,-18);
            if(previousMap) viewCamera.transform.position=desired;
            viewCamera.transform.position=Vector3.SmoothDamp(viewCamera.transform.position,desired,ref cameraVelocity,.55f);
            viewCamera.transform.LookAt(Vector3.zero);
            trailTime+=Time.unscaledDeltaTime;
            if(trailTime>.3f)
            {
                trailTime=0;trailPoints.Add(-flight.orientation.Rotate(new DVector3(0,1,0)).Float*6);
                if(trailPoints.Count>40) trailPoints.RemoveAt(0);
            }
            trail.positionCount=0;
            UpdateTarget(target,target==null?Vector3.zero:(target.position-flight.position).Float*ViewScale,ViewScale);
        }

        void UpdateTarget(FlightBody target,Vector3 position,float scale)
        {
            if(target==null)
            {
                if(targetVisual!=null) targetVisual.SetActive(false);targetLine.positionCount=0;return;
            }
            if(targetVisual==null)
            {
                targetVisual=new GameObject("RendezvousTarget");targetVisual.transform.SetParent(transform,false);
                foreach(var part in target.parts)
                {
                    var model=Instantiate(part.definition.model,targetVisual.transform);
                    model.transform.localPosition=part.placement.position-target.centerOfMass.Float;model.transform.localRotation=part.placement.rotation;
                }
            }
            targetVisual.SetActive(true);targetVisual.transform.localPosition=position;targetVisual.transform.rotation=target.orientation.Float;targetVisual.transform.localScale=Vector3.one*scale;
            targetLine.positionCount=2;targetLine.SetPosition(0,rocketRoot.position);targetLine.SetPosition(1,targetVisual.transform.position);
        }

        public void Burst(Vector3 localPosition)
        {
            if(assets.engineEffect==null) return;
            var effect=Instantiate(assets.engineEffect,transform).GetComponent<EngineVfx>();effect.transform.position=rocketRoot.TransformPoint(localPosition);
            var emission=effect.smoke.emission;emission.rateOverTime=0;effect.smoke.Emit(70);effect.embers.Emit(50);
            effect.core.Emit(30);effect.plume.Emit(30);Destroy(effect.gameObject,5);
        }
    }
}
