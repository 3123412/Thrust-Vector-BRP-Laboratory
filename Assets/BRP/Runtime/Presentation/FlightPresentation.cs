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
        readonly Dictionary<FlightBody,Transform> detachedVisuals=new Dictionary<FlightBody,Transform>();
        readonly Dictionary<string,PartState> currentParts=new Dictionary<string,PartState>();
        readonly Vector3[] orbitPoints=new Vector3[300];
        LineRenderer orbitLine,trail,targetLine;
        readonly List<Vector3> trailPoints=new List<Vector3>();
        GameObject planet,pad,canopy,targetVisual,terrain;
        LabSceneAssets assets;
        float trailTime,orbitRefresh;
        Vector3 cameraVelocity;
        bool previousMap;
        const float ViewScale=1.5f;
        float cameraYaw=35,cameraPitch=18,cameraDistance=35;
        float mapYaw=180,mapPitch=20,mapDistance=210;
        TMPro.TextMeshPro apoLabel,periLabel,craftLabel;
        bool cameraManual;
        AerodynamicVfx aerodynamicVfx;
        public void OrbitCamera(Vector2 delta,float zoom)
        {
            if(delta.sqrMagnitude>.01f||Mathf.Abs(zoom)>.01f) cameraManual=true;
            if(map)
            {
                mapYaw+=delta.x*.2f;mapPitch=Mathf.Clamp(mapPitch-delta.y*.15f,-80,80);mapDistance=Mathf.Clamp(mapDistance-zoom*.1f,68,350);
            }
            else
            {
                cameraYaw+=delta.x*.2f;cameraPitch=Mathf.Clamp(cameraPitch-delta.y*.15f,-60,80);
                cameraDistance=Mathf.Clamp(cameraDistance-zoom*.015f,5,120);
            }
        }

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
            float height=1,radius=.3f;
            foreach(var part in flight.parts) {height=Mathf.Max(height,part.placement.position.y+part.definition.height);radius=Mathf.Max(radius,part.definition.radius);}
            aerodynamicVfx=rocketRoot.gameObject.AddComponent<AerodynamicVfx>();aerodynamicVfx.Initialize(source.smokeMaterial,radius,height);
            if(source.terrain!=null)
            {
                terrain=new GameObject("Launch terrain");terrain.transform.SetParent(transform,false);
                Instantiate(source.terrain,terrain.transform);terrain.transform.localScale=Vector3.one*ViewScale*4;
            }
            planet=new GameObject("Earth");planet.transform.SetParent(transform,false);Instantiate(source.planet,planet.transform);
            foreach(var renderer in planet.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=source.planetMaterial;
            if(source.canopy!=null)
            {
                canopy=Instantiate(source.canopy,rocketRoot);canopy.transform.localPosition=Vector3.up*8;canopy.SetActive(false);
            }
            orbitLine=CreateLine("PredictedOrbit",UiTheme.Cyan,.065f);
            trail=CreateLine("FlightPath",new Color(.75f,.8f,.8f,.4f),.035f);
            targetLine=CreateLine("TargetVector",UiTheme.Orange,.025f);
            apoLabel=MapLabel("Ap",UiTheme.Cyan);periLabel=MapLabel("Pe",UiTheme.Cyan);craftLabel=MapLabel("БРП",UiTheme.Orange);
            SetFlightLayer(transform);
            viewCamera.transform.position=new Vector3(12,6,-18);viewCamera.transform.LookAt(Vector3.zero);
            Refresh(flight,null);
        }

        LineRenderer CreateLine(string name,Color color,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(transform,false);
            line.material=lineMaterial;line.startColor=line.endColor=color;line.startWidth=line.endWidth=width;line.positionCount=0;return line;
        }
        TMPro.TextMeshPro MapLabel(string text,Color color)
        {
            var label=new GameObject(text).AddComponent<TMPro.TextMeshPro>();label.transform.SetParent(transform,false);
            label.font=assets.font;label.text=text;label.fontSize=3;label.color=color;label.alignment=TMPro.TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta=new Vector2(18,5);return label;
        }

        public void Refresh(FlightBody flight,FlightBody target)
        {
            currentParts.Clear();
            foreach(var part in flight.parts)
            {
                currentParts[part.placement.instanceId]=part;
                if(visuals.ContainsKey(part.placement.instanceId)) continue;
                var model=Instantiate(part.definition.model,rocketRoot);visuals[part.placement.instanceId]=model;
                model.transform.localPosition=part.placement.position;
                model.transform.localRotation=part.placement.rotation*(part.definition.surface?Quaternion.Euler(0,180,0):Quaternion.identity);
                SetFlightLayer(model.transform);
            }
            foreach(var entry in visuals)
            {
                currentParts.TryGetValue(entry.Key,out var state);
                bool visible=state!=null&&!state.destroyed;
                if(entry.Value.activeSelf!=visible) entry.Value.SetActive(visible);
            }
            float atmosphere=(float)Math.Clamp(OrbitalMechanics.Density(flight.Altitude)/1.225,0,1);
            aerodynamicVfx.Refresh(flight,!map&&!flight.landed&&!flight.crashed);
            assets.skyMaterial.SetFloat("_Atmosphere",map?0:Mathf.Exp(-(float)Math.Max(0,flight.Altitude)/26000));
            foreach(var part in flight.parts)
            {
                if(exhausts.TryGetValue(part.placement.instanceId,out var effect))
                {
                    effect.SetDrive((float)(part.thrust/Math.Max(1,part.definition.thrust)),atmosphere);
                }
            }
            foreach(var entry in exhausts)
                if(!flight.parts.Exists(p=>p.placement.instanceId==entry.Key)) entry.Value.SetDrive(0,0);
            if(canopy!=null) canopy.SetActive(flight.chuteDeployed);
            if(canopy!=null)
            {
                var capsule=flight.parts.Find(p=>p.definition.kind==PartKind.Capsule);
                if(capsule!=null) canopy.transform.localPosition=capsule.placement.position+Vector3.up*(capsule.definition.height+3);
            }
            apoLabel.gameObject.SetActive(map);periLabel.gameObject.SetActive(map);craftLabel.gameObject.SetActive(map);
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
            var desired=Quaternion.Euler(-mapPitch,mapYaw,0)*Vector3.forward*mapDistance;
            if(!previousMap) viewCamera.transform.position=desired;
            viewCamera.transform.position=Vector3.SmoothDamp(viewCamera.transform.position,desired,ref cameraVelocity,.4f);
            viewCamera.transform.LookAt(Vector3.zero);
            craftLabel.transform.position=rocketRoot.position+Vector3.up*2;
            craftLabel.transform.rotation=viewCamera.transform.rotation;apoLabel.transform.rotation=viewCamera.transform.rotation;periLabel.transform.rotation=viewCamera.transform.rotation;
            orbitRefresh-=Time.unscaledDeltaTime;
            if(orbitRefresh<=0)
            {
                orbitRefresh=.35f;
                var r=flight.position;var v=flight.velocity;var points=orbitPoints;
                double step=double.IsInfinity(flight.Orbit.period)?10:flight.Orbit.period/299;
                step=Math.Clamp(step,1,45);
                double highest=0,lowest=double.PositiveInfinity;Vector3 apo=Vector3.zero,peri=Vector3.zero;
                for(int i=0;i<points.Length;i++)
                {
                    points[i]=r.Float*scale;
                    if(r.Magnitude>highest) {highest=r.Magnitude;apo=points[i];}
                    if(r.Magnitude<lowest) {lowest=r.Magnitude;peri=points[i];}
                    OrbitalMechanics.Integrate(ref r,ref v,step,(p,s)=>OrbitalMechanics.Gravity(p));
                }
                orbitLine.positionCount=points.Length;orbitLine.SetPositions(points);
                apoLabel.transform.position=apo+Vector3.up*2;periLabel.transform.position=peri+Vector3.up*2;
                apoLabel.text=$"Ap {flight.Orbit.apoapsis/1000:F0} км";periLabel.text=$"Pe {flight.Orbit.periapsis/1000:F0} км";
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
            float lower=float.PositiveInfinity,upper=float.NegativeInfinity;
            foreach(var part in flight.parts) if(!part.destroyed) {lower=Mathf.Min(lower,part.placement.position.y);upper=Mathf.Max(upper,part.placement.position.y+part.definition.height);}
            var center=rocketRoot.TransformPoint(new Vector3(0,(lower+upper)*.5f,0));
            if(!cameraManual) cameraDistance=Mathf.Clamp((upper-lower)*ViewScale*2.4f,9,45);
            var desired=center+Quaternion.Euler(-cameraPitch,cameraYaw,0)*Vector3.forward*cameraDistance;
            if(previousMap) viewCamera.transform.position=desired;
            viewCamera.transform.position=Vector3.SmoothDamp(viewCamera.transform.position,desired,ref cameraVelocity,.55f);
            viewCamera.transform.LookAt(center);
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
                SetFlightLayer(targetVisual.transform);
            }
            targetVisual.SetActive(true);targetVisual.transform.localPosition=position;targetVisual.transform.rotation=target.orientation.Float;targetVisual.transform.localScale=Vector3.one*scale;
            targetLine.positionCount=2;targetLine.SetPosition(0,rocketRoot.position);targetLine.SetPosition(1,targetVisual.transform.position);
        }

        public void Burst(Vector3 localPosition)
        {
            if(assets.engineEffect==null) return;
            var effect=Instantiate(assets.engineEffect,transform).GetComponent<EngineVfx>();effect.transform.position=rocketRoot.TransformPoint(localPosition);
            SetFlightLayer(effect.transform);
            var emission=effect.smoke.emission;emission.rateOverTime=0;effect.smoke.Emit(70);effect.embers.Emit(50);
            effect.core.Emit(30);effect.plume.Emit(30);Destroy(effect.gameObject,5);
        }
        public void RefreshDebris(FlightBody flight,IReadOnlyList<FlightBody> bodies)
        {
            foreach(var body in bodies)
            {
                if(!detachedVisuals.TryGetValue(body,out var root))
                {
                    root=new GameObject("Separated Craft").transform;root.SetParent(transform,false);detachedVisuals[body]=root;
                    foreach(var part in body.parts)
                    {
                        var model=Instantiate(part.definition.model,root);
                        model.transform.localPosition=part.placement.position-body.centerOfMass.Float;
                        model.transform.localRotation=part.placement.rotation*(part.definition.surface?Quaternion.Euler(0,180,0):Quaternion.identity);
                    }
                    SetFlightLayer(root);
                }
                root.gameObject.SetActive(!body.crashed);
                root.rotation=body.orientation.Float;
                root.localScale=Vector3.one*(map?.08f:ViewScale);
                root.localPosition=map?body.position.Float*(50f/(float)OrbitalMechanics.Radius):(body.position-flight.position).Float*ViewScale;
            }
        }
        static void SetFlightLayer(Transform root)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=29;
        }
    }
}
