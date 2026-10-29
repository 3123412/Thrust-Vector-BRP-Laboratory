using UnityEngine;

namespace BRP
{
    public sealed class EngineVfx : MonoBehaviour
    {
        public ParticleSystem core,plume,smoke,embers;
        public Light glow;
        float intensity;
        public void SetDrive(float power,float atmosphere)
        {
            intensity=Mathf.MoveTowards(intensity,power,Time.unscaledDeltaTime*2.5f);
            SetEmission(core,intensity*260);
            SetEmission(plume,intensity*180);
            SetEmission(smoke,intensity*65*Mathf.Sqrt(atmosphere));
            SetEmission(embers,intensity*12*atmosphere);
            var main=core.main;main.startSpeed=8+intensity*8;main.startSize=.18f+intensity*.16f;
            main=plume.main;main.startSpeed=5+intensity*7;
            var shape=plume.shape;shape.angle=Mathf.Lerp(24,7,atmosphere);
            if(glow!=null) glow.intensity=intensity*(2.3f+Mathf.PerlinNoise(Time.unscaledTime*17,0)*.5f);
        }
        static void SetEmission(ParticleSystem system,float rate)
        {
            if(system==null) return;var emission=system.emission;emission.rateOverTime=rate;
        }
    }
}
