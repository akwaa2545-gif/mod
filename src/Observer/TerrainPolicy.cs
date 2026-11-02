using System;
namespace NuclearOptionObserver
{
 internal readonly struct TerrainPoint {internal TerrainPoint(float distance,float height){Distance=distance;Height=height;}internal float Distance{get;}internal float Height{get;} }
 internal readonly struct TerrainObservation {internal TerrainObservation(bool valid,double time,TerrainPoint[] points,int count){Valid=valid;Time=time;Points=points;Count=count;}internal bool Valid{get;}internal double Time{get;}internal TerrainPoint[] Points{get;}internal int Count{get;} }
 internal sealed class TerrainScanSchedule
 {
  private double next=double.NegativeInfinity;
  internal bool Due(double now){if(double.IsNaN(now)||double.IsInfinity(now)||now<next)return false;next=now+.2;return true;}
  internal void Reset(){next=double.NegativeInfinity;}
 }
 internal sealed class TerrainAdvisor
 {
  private int clearSamples;private double lastSample=double.NegativeInfinity;
  internal bool Avoiding{get;private set;}
  internal bool Assess(bool enabled,FlightTelemetry f,TerrainObservation data,double now,out TerrainDemand demand,out string reason)
  {
   demand=default;reason="Terrain data unavailable, invalid, truncated or stale.";
   if(!enabled){Reset();demand=TerrainDemand.Clear;reason="Terrain avoidance OFF";return true;}
   if(!data.Valid||data.Points==null||data.Count<1||data.Count>8||data.Count>data.Points.Length||double.IsNaN(now)||double.IsInfinity(now)||double.IsNaN(data.Time)||double.IsInfinity(data.Time)||now<data.Time||now-data.Time>.6)return false;
   float worst=float.PositiveInfinity,required=float.NegativeInfinity;bool expiredHazard=false;
   const float response=1.5f;float age=(float)(now-data.Time);
   for(int i=0;i<data.Count;i++)
   {
    var p=data.Points[i];if(!FlightEnvelope.Finite(p.Distance)||!FlightEnvelope.Finite(p.Height)||p.Distance<0)return false;
    float time=Math.Max(0,p.Distance/Math.Max(20,f.GroundSpeed)-age);
    float projectedClearance=f.Altitude+f.VerticalSpeed*time-p.Height;
    worst=Math.Min(worst,projectedClearance);
    float remaining=time-response;
    if(remaining<=0){if(projectedClearance<150)expiredHazard=true;}
    else
    {
     // Preserve the present descent through the response delay; do not invent time once its window has expired.
     float responseAltitude=f.Altitude+f.VerticalSpeed*response;
     required=Math.Max(required,(p.Height+150-responseAltitude)/remaining);
    }
   }
   if(worst<150){Avoiding=true;clearSamples=0;}
   else if(Avoiding&&data.Time>lastSample){if(worst>200)++clearSamples;else clearSamples=0;if(clearSamples>=3)Avoiding=false;}
   lastSample=Math.Max(lastSample,data.Time);
   if(Avoiding)
   {
    reason="URGENT: predicted terrain demand exceeds recovery/airspeed envelope.";
    if(expiredHazard||f.AirSpeed<f.MinAirspeed*1.1f||required>Math.Min(30,f.AirSpeed*.2f))return false;
    demand=new TerrainDemand(true,true,Math.Max(3,required));reason="Avoidance climb (bounded prediction, not a guarantee).";return true;
   }
   demand=TerrainDemand.Clear;reason="Terrain samples clear (bounded sensing).";return true;
  }
  internal void Reset(){Avoiding=false;clearSamples=0;lastSample=double.NegativeInfinity;}
 }
}
