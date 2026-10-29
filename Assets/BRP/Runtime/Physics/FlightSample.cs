using System.Globalization;

namespace BRP
{
    public readonly struct FlightSample
    {
        readonly double time,altitude,speed,mass,fuel,thrust,temperature;
        public FlightSample(FlightBody body)
        {time=body.elapsed;altitude=body.Altitude;speed=body.SurfaceVelocity.Magnitude;mass=body.mass;fuel=body.Fuel;thrust=body.lastThrust;temperature=body.Temperature;}
        public string Csv()=>string.Join(",",new[]{time,altitude,speed,mass,fuel,thrust,temperature}.SelectValue());
    }
    static class FlightValues
    {
        public static string[] SelectValue(this double[] values)
        {
            var result=new string[values.Length];for(int i=0;i<values.Length;i++) result[i]=values[i].ToString("F4",CultureInfo.InvariantCulture);return result;
        }
    }
}
