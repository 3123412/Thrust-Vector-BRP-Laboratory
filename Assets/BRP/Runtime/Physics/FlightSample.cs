using System.Globalization;

namespace BRP
{
    public readonly struct FlightSample
    {
        readonly double time,altitude,speed,mass,fuel,thrust,temperature;
        public FlightSample(FlightBody body)
        {time=body.elapsed;altitude=body.Altitude;speed=body.SurfaceVelocity.Magnitude;mass=body.mass;fuel=body.Fuel;thrust=body.lastThrust;temperature=body.Temperature;}
        public string Csv()=>string.Join(",",System.Array.ConvertAll(new[]{time,altitude,speed,mass,fuel,thrust,temperature},value=>value.ToString("F4",CultureInfo.InvariantCulture)));
    }
}
