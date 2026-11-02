using System;
namespace NuclearOptionTargetingPod
{
    public static class LeasePolicy
    {
        public static bool CanOwn(bool context,bool known,bool fullscreen,bool drain,bool hold,bool modal) => context&&known&&!fullscreen&&!drain&&!hold&&!modal;
    }
    public sealed class SensorPolicy
    {
        // Deliberate mod viewing limits; not a claim about real sensor hardware.
        public float Yaw {get;private set;}
        public float Pitch {get;private set;}
        public float Fov {get;private set;}=20;
        public static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public bool Slew(float yaw,float pitch){if(!Finite(yaw)||!Finite(pitch))return false;Yaw=Math.Max(-135,Math.Min(135,Yaw+yaw));Pitch=Math.Max(-85,Math.Min(85,Pitch+pitch));return true;}
        public void Zoom(float delta){if(Finite(delta))Fov=Math.Max(1,Math.Min(60,Fov+delta));}
        public void Reset(){Yaw=Pitch=0;Fov=20;}
    }
}
