using System;
namespace NuclearOptionTargetingPod
{
    internal static class WheelZoom
    {
        // Mod viewing limits, not sensor hardware claims. Does not write native camera FOV.
        internal static float Adjust(float fov,float notches)
        {
            if(!SensorPolicy.Finite(fov)||fov<.1f||fov>60)return fov;
            if(!SensorPolicy.Finite(notches))return fov;
            return Math.Max(.1f,Math.Min(60,(float)(fov*Math.Pow(.85,Math.Max(-4,Math.Min(4,notches))))));
        }
    }
}
