using UnityEngine;

namespace BRP
{
    public sealed class EngineVfx : MonoBehaviour
    {
        public ParticleSystem core,plume,smoke,embers;
        public Light glow;
        public Renderer continuousCore;
        MaterialPropertyBlock properties;
        MaterialPropertyBlock plumeProperties;
        ParticleSystemRenderer plumeRenderer;
        float intensity;
        public void ConfigureGround(Transform surface)
        {
            var main=smoke.main;main.simulationSpace=ParticleSystemSimulationSpace.Custom;main.customSimulationSpace=surface;
            var collision=smoke.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.Planes;
            collision.SetPlane(0,surface);collision.bounce=.02f;collision.dampen=.65f;collision.lifetimeLoss=.1f;collision.radiusScale=1;
            main=core.main;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main=plume.main;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        }
        public void SetDrive(float power,float atmosphere)
        {
            intensity=Mathf.MoveTowards(intensity,power,Time.unscaledDeltaTime*2.5f);
            if(continuousCore!=null)
            {
                properties??=new MaterialPropertyBlock();properties.SetFloat("_Power",intensity);
                properties.SetColor("_BaseColor",Color.Lerp(new Color(.35f,.7f,1.8f),new Color(2.3f,1.1f,.2f),atmosphere));
                continuousCore.SetPropertyBlock(properties);
            }
            SetEmission(core,intensity*80);
            SetEmission(plume,intensity*Mathf.Lerp(24,90,atmosphere));
            SetEmission(smoke,intensity*65*Mathf.Sqrt(atmosphere));
            SetEmission(embers,intensity*12*atmosphere);
            var main=core.main;main.startSpeed=8+intensity*8;main.startSize=.12f+intensity*.12f;
            main=plume.main;main.startSpeed=5+intensity*7;main.startColor=new Color(1,1,1,Mathf.Lerp(.06f,.2f,atmosphere));
            if(plumeRenderer==null) plumeRenderer=plume.GetComponent<ParticleSystemRenderer>();
            plumeProperties??=new MaterialPropertyBlock();
            plumeProperties.SetColor("_BaseColor",Color.Lerp(new Color(.25f,.55f,1.5f),new Color(2.1f,.9f,.25f),atmosphere));
            plumeRenderer.SetPropertyBlock(plumeProperties);
            var shape=plume.shape;shape.angle=Mathf.Lerp(24,7,atmosphere);
            if(glow!=null) glow.intensity=intensity*(2.3f+Mathf.PerlinNoise(Time.unscaledTime*17,0)*.5f);
        }
        static void SetEmission(ParticleSystem system,float rate)
        {
            if(system==null) return;var emission=system.emission;emission.rateOverTime=rate;
        }
    }
}
