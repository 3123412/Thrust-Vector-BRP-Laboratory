using UnityEngine;

namespace BRP
{
    public sealed class AerodynamicVfx : MonoBehaviour
    {
        ParticleSystem vapor;
        float strength;

        public void Initialize(Material material,float radius,float height)
        {
            var source=new GameObject("Transonic condensation");source.transform.SetParent(transform,false);
            source.transform.localPosition=Vector3.up*height*.62f;
            source.transform.localRotation=Quaternion.Euler(90,0,0);
            vapor=source.AddComponent<ParticleSystem>();vapor.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=vapor.main;main.loop=true;main.startLifetime=.22f;main.startSpeed=3;
            main.startSize=radius*.9f;main.startColor=new Color(.85f,.92f,1,.12f);
            main.maxParticles=96;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=vapor.emission;emission.rateOverTime=0;
            var shape=vapor.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=radius*1.1f;shape.angle=48;
            var size=vapor.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.4f,1,1.8f));
            var color=vapor.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=source.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            source.layer=29;vapor.Play();
        }

        public void Refresh(FlightBody body,bool visible)
        {
            float band=Mathf.Clamp01(1-Mathf.Abs((float)body.Mach-1.02f)/.22f);
            float air=Mathf.Clamp01((float)OrbitalMechanics.Density(body.Altitude)/.7f);
            float target=visible&&body.Altitude<12000?band*air:0;
            strength=Mathf.MoveTowards(strength,target,Time.unscaledDeltaTime*3);
            var emission=vapor.emission;emission.rateOverTime=strength*260;
        }
    }
}
