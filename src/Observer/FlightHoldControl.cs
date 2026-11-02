using System;
namespace NuclearOptionObserver
{
 internal enum AirframeKind { Unsupported, FixedWing, SwivelVtol }
 internal struct FlightTelemetry {
  internal float Altitude{get;set;}internal float Speed{get;set;}internal float GroundSpeed{get;set;}internal float AirSpeed{get;set;}internal float VerticalSpeed{get;set;}
  internal float Pitch{get;set;}internal float Bank{get;set;}internal float Alpha{get;set;}internal float OmegaX{get;set;}internal float OmegaY{get;set;}internal float OmegaZ{get;set;}internal float Slip{get;set;}
  internal float CourseX{get;set;}internal float CourseZ{get;set;}internal float Throttle{get;set;}internal float RadarAltitude{get;set;}internal float MinAirspeed{get;set;}internal float MaxSpeed{get;set;}internal float NozzleAngle{get;set;}internal float CustomAxis{get;set;}
  internal bool Ready{get;set;}internal bool GearRetracted{get;set;}internal bool AutoHover{get;set;}internal bool Damaged{get;set;}internal bool EnginesRunning{get;set;}internal bool HasSwivel{get;set;}internal AirframeKind Kind{get;set;}
 }
 internal readonly struct FlightCommand {internal FlightCommand(float pitch,float roll,float yaw,float throttle){Pitch=pitch;Roll=roll;Yaw=yaw;Throttle=throttle;}internal float Pitch{get;}internal float Roll{get;}internal float Yaw{get;}internal float Throttle{get;} }
 internal readonly struct HoldTargets {
  internal HoldTargets(float altitude,float speed,float x,float z){Altitude=altitude;Speed=speed;CourseX=x;CourseZ=z;}
  internal float Altitude{get;}internal float Speed{get;}internal float CourseX{get;}internal float CourseZ{get;}
 }
 internal readonly struct TerrainDemand {internal TerrainDemand(bool valid,bool avoiding,float verticalSpeed){Valid=valid;Avoiding=avoiding;VerticalSpeed=verticalSpeed;}internal bool Valid{get;}internal bool Avoiding{get;}internal float VerticalSpeed{get;}internal static TerrainDemand Clear=>new(true,false,0);}
 internal static class FlightEnvelope
 {
  internal static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
  internal static bool Check(FlightTelemetry f,bool entering,out string reason)
  {
   reason="Invalid or nonfinite flight telemetry/airframe parameters.";
   if(!Finite(f.Altitude)||!Finite(f.Speed)||!Finite(f.GroundSpeed)||f.GroundSpeed<20||!Finite(f.AirSpeed)||!Finite(f.VerticalSpeed)||!Finite(f.Pitch)||!Finite(f.Bank)||!Finite(f.Alpha)||!Finite(f.OmegaX)||!Finite(f.OmegaY)||!Finite(f.OmegaZ)||!Finite(f.Slip)||!Finite(f.CourseX)||!Finite(f.CourseZ)||!Finite(f.Throttle)||!Finite(f.RadarAltitude)||!Finite(f.MinAirspeed)||!Finite(f.MaxSpeed)||!Finite(f.NozzleAngle)||!Finite(f.CustomAxis)||f.MinAirspeed<35||f.MaxSpeed<=f.MinAirspeed||!Finite(f.CourseX*f.CourseX+f.CourseZ*f.CourseZ)||f.CourseX*f.CourseX+f.CourseZ*f.CourseZ<.5f||f.CourseX*f.CourseX+f.CourseZ*f.CourseZ>2.1f)return false;
   reason="Local pilot/aircraft state is not ready.";if(!f.Ready)return false;
   reason="Unsupported helicopter/tilt/unknown airframe.";if(f.Kind==AirframeKind.Unsupported)return false;
   reason="VTOL needs an actual forward swivel nozzle and forward demand.";if(f.Kind==AirframeKind.SwivelVtol&&(!f.HasSwivel||f.NozzleAngle< -1||f.NozzleAngle>5||f.CustomAxis<.94f))return false;
   reason="Gear must be retracted; hover/vector transition is unsupported.";if(!f.GearRetracted||f.AutoHover)return false;
   reason="Damage or engine-out/ignition/fuel/RPM state is unsupported.";if(f.Damaged||!f.EnginesRunning)return false;
   reason="Insufficient airspeed/stall margin or overspeed.";if(f.AirSpeed<f.MinAirspeed*(entering?1:.9f)||f.Speed<40||f.Speed>f.MaxSpeed*1.1f)return false;
   reason="Insufficient initial/recovery ground clearance or altitude envelope.";if(f.RadarAltitude<(entering?300:35)||f.Altitude<(entering?300:35)||f.Altitude>8000)return false;
   reason="Large attitude/alpha/angular rate is outside this experimental envelope.";
   if(Math.Abs(f.Bank)>(entering?25:60)||Math.Abs(f.Pitch)>(entering?20:35)||Math.Abs(f.Alpha)>(entering?12:20)||Math.Abs(f.OmegaX)>(entering?.5f:2)||Math.Abs(f.OmegaY)>(entering?.5f:2)||Math.Abs(f.OmegaZ)>(entering?.5f:2))return false;
   reason="";return true;
  }
 }
 internal sealed class FlightHoldCore
 {
  private float alpha,throttleFeed,speedIntegral,altIntegral;
  private FlightCommand last;
  internal HoldTargets Targets{get;private set;}
  internal bool HasTargets{get;private set;}
  internal bool Capture(FlightTelemetry f,out string reason)
  {
   if(!FlightEnvelope.Check(f,true,out reason))return false;
   float length=(float)Math.Sqrt(f.CourseX*f.CourseX+f.CourseZ*f.CourseZ);
   Targets=new HoldTargets(f.Altitude,f.Speed,f.CourseX/length,f.CourseZ/length);
   alpha=Clamp(f.Alpha,-8,12);throttleFeed=Clamp(f.Throttle,0,1);speedIntegral=altIntegral=0;
   last=new FlightCommand(0,0,0,throttleFeed);HasTargets=true;return true;
  }
  internal bool Step(FlightTelemetry f,float dt,TerrainDemand terrain,out FlightCommand command,out string reason)
  {
   command=default;reason="Hold has no captured targets.";if(!HasTargets)return false;
   reason="Invalid physics dt.";if(!FlightEnvelope.Finite(dt)||dt<=0||dt>.1f)return false;
   if(!FlightEnvelope.Check(f,false,out reason))return false;
   reason="Invalid terrain demand.";if(!terrain.Valid||!FlightEnvelope.Finite(terrain.VerticalSpeed))return false;
   float courseError=(float)(Math.Atan2(f.CourseZ*Targets.CourseX-f.CourseX*Targets.CourseZ,f.CourseX*Targets.CourseX+f.CourseZ*Targets.CourseZ)*180/Math.PI);
   float bankTarget=terrain.Avoiding?0:Clamp(courseError*.45f,-15,15);
   float altError=Targets.Altitude-f.Altitude;
   float rawVs=altError*.04f+altIntegral;
   if(!terrain.Avoiding&&CanIntegrate(rawVs,altError,-3,8))altIntegral=Clamp(altIntegral+altError*.002f*dt,-3,3);
   float vs=terrain.Avoiding?Clamp(terrain.VerticalSpeed,3,30):Clamp(altError*.04f+altIntegral,-3,8);
   float pitchTarget=Clamp(alpha+(float)(Math.Atan2(vs,Math.Max(20,f.AirSpeed))*180/Math.PI),-8,terrain.Avoiding?18:12);
   // Angles in degrees; angular damping uses native local rad/s signs.
   float pitch=Clamp(-.045f*(pitchTarget-f.Pitch)-.25f*f.OmegaX,-.6f,.6f);
   float roll=Clamp(.035f*(bankTarget-f.Bank)+.25f*f.OmegaZ,-.75f,.75f);
   float yaw=Clamp(.01f*f.Slip-.2f*f.OmegaY,-.3f,.3f);
   float speedError=Targets.Speed-f.Speed;
   float rawThrottle=throttleFeed+.008f*speedError+speedIntegral;
   float throttleFloor=terrain.Avoiding?.85f:0;
   if(CanIntegrate(rawThrottle,speedError,throttleFloor,1))speedIntegral=Clamp(speedIntegral+speedError*.0008f*dt,-.25f,.25f);
   float throttle=Clamp(throttleFeed+.008f*speedError+speedIntegral,throttleFloor,1);
   command=new FlightCommand(Slew(last.Pitch,pitch,dt),Slew(last.Roll,roll,dt*1.5f),Slew(last.Yaw,yaw,dt),Slew(last.Throttle,throttle,dt*(terrain.Avoiding?.35f:.2f)));
   last=command;reason="";return true;
  }
  internal void Release(){HasTargets=false;speedIntegral=altIntegral=0;last=default;}
  private static bool CanIntegrate(float output,float error,float low,float high)=>output>low&&output<high||output>=high&&error<0||output<=low&&error>0;
  private static float Clamp(float x,float low,float high)=>Math.Max(low,Math.Min(high,x));
  private static float Slew(float previous,float next,float amount)=>previous+Clamp(next-previous,-amount,amount);
 }
}
