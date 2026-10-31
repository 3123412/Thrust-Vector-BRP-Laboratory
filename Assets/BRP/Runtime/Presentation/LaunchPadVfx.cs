using UnityEngine;

namespace BRP
{
    public sealed class LaunchPadVfx : MonoBehaviour
    {
        ParticleSystem steam;
        float amount;
        bool lunar;
        Transform surface;
        Material steamMaterial;
        public void Initialize(Material material,Transform surface,bool moon=false)
        {
            lunar=moon;
            this.surface=surface;
            var source=new GameObject("Water deluge steam");source.layer=29;source.transform.SetParent(transform,false);
            source.transform.localPosition=Vector3.up*(lunar?.2f:1.7f);source.transform.localRotation=Quaternion.Euler(-90,0,0);
            steam=source.AddComponent<ParticleSystem>();steam.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=steam.main;main.startLifetime=new ParticleSystem.MinMaxCurve(2.5f,4);main.startSpeed=new ParticleSystem.MinMaxCurve(3,7);
            main.startSize=new ParticleSystem.MinMaxCurve(1.2f,2.4f);main.startColor=new Color(.8f,.86f,.9f,.32f);
            main.simulationSpace=ParticleSystemSimulationSpace.Custom;main.customSimulationSpace=surface;main.maxParticles=700;main.gravityModifier=-.06f;
            if(lunar) {main.startColor=new Color(.53f,.5f,.43f,.2f);main.gravityModifier=0;main.startSize=new ParticleSystem.MinMaxCurve(.2f,.6f);}
            var shape=steam.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=82;shape.radius=1.1f;
            var emission=steam.emission;emission.rateOverTime=0;
            var size=steam.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.4f),new Keyframe(1,3.4f)));
            var color=steam.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.6f,.6f),new GradientAlphaKey(0,1)});color.color=gradient;
            var collision=steam.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.Planes;collision.SetPlane(0,surface);
            collision.dampen=.7f;collision.bounce=.02f;collision.lifetimeLoss=.05f;collision.radiusScale=1;
            steamMaterial=new Material(material);steamMaterial.SetColor("_BaseColor",new Color(.9f,.92f,.95f,.7f));
            var renderer=steam.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=steamMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            steam.Play();
        }
        void OnDestroy() {if(steamMaterial!=null) Destroy(steamMaterial);}
        public void Refresh(FlightBody body,bool visible)
        {
            float range=lunar?60:180;
            float target=visible&&body.Altitude<range?(float)(body.lastThrust/(lunar?180000:900000))*Mathf.Clamp01(1-(float)body.Altitude/range):0;
            amount=Mathf.MoveTowards(amount,target,Time.unscaledDeltaTime*1.8f);
            if(lunar)
            {
                var force=steam.forceOverLifetime;force.enabled=true;force.space=ParticleSystemSimulationSpace.World;
                Vector3 gravity=-surface.up*2.43f;force.x=gravity.x;force.y=gravity.y;force.z=gravity.z;
            }
            var emission=steam.emission;emission.rateOverTime=amount*130;
        }
    }
}
