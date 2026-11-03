#nullable disable
using System;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NuclearOptionTargetingPod
{
    internal sealed class Presentation:IDisposable
    {
        internal RenderTexture Output {get;private set;}
        private readonly NativeHint nativeHint=new NativeHint();
        private Texture2D overlay;
        private OverlayData truth=new OverlayData();
        private readonly OverlayRefreshPolicy refresh=new OverlayRefreshPolicy();
        private string semantic="";
        private WeaponStation previousStation;
        private long stationGeneration;
        private bool manual,stabilized,aftermath;
        private float fov;
        internal void SetManual(bool value,bool directionHold,float fieldOfView=0){manual=value;stabilized=directionHold;fov=fieldOfView;}
        internal Presentation(int width,int height)
        {
            Output=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){name="TargetingPodDisplay.Output",useMipMap=false,autoGenerateMips=false};
            if(!Output.Create()){Dispose();throw new InvalidOperationException("Presentation texture allocation failed");}
        }
        internal void UpdateTruth(PodContext context,bool isAftermath=false)
        {
            if(aftermath!=isAftermath){aftermath=isAftermath;if(aftermath)nativeHint.Clear();}
            var station=context.Aircraft.weaponManager.currentWeaponStation;
            var info=station==null?null:station.WeaponInfo;
            var frame=context.Aircraft.cockpit.transform;
            var targets=context.Aircraft.weaponManager.GetTargetList();
            bool selected=false;
            foreach(var target in targets)if(target!=null&&!target.disabled&&target.isActiveAndEnabled&&target.persistentID.IsValid){selected=true;break;}
            var data=new OverlayData {
                Page=aftermath?"AFTERMATH":DisplayPolicy.Page(info!=null&&info.missile,info!=null&&(info.bomb||info.glideBomb)),
                Weapon=aftermath?"":Clean(info==null?"WPN NONE":info.weaponName),
                Missile=!aftermath&&info!=null&&DisplayPolicy.SurfaceMissile(info.missile,info.effectiveness.antiSurface,info.effectiveness.antiAir,info.effectiveness.antiMissile),Tracking=false,TargetSelected=!aftermath&&selected,RangeNearReticle=true,
                Sensor=manual?"COLOR":DisplayPolicy.Sensor(context.TargetCam.UsingIR()),
                Magnification="FOV "+fov.ToString("0.0"),
                State=aftermath?"FLIGHT LIVE AFTER":manual?(stabilized?"FLIGHT LIVE HOLD":"FLIGHT LIVE MANUAL"):"FLIGHT LIVE NATIVE",
                Clock=DateTime.UtcNow.ToString("HH:mm:ss")+" UTC",Laser="LSR N/A",
                PitchDegrees=Mathf.Asin(Mathf.Clamp(frame.forward.y,-1,1))*Mathf.Rad2Deg,
                RollDegrees=Mathf.Atan2(-frame.right.y,frame.up.y)*Mathf.Rad2Deg
            };
            if(data.Missile&&data.TargetSelected)
            {
                data.Page="TARGET SELECTED";
            }
            if(!aftermath)
            {
                string hint=nativeHint.Read(context);data.Hint=!selected||hint=="WPN HINT N/A"?"":hint;
                data.Target=DisplayPolicy.Designation(selected?targets.Count:0);
                if(selected&&targets.Count>0)
                {
                    data.Range="RNG "+UnitConverter.DistanceReading(context.TargetCam.GetDist());
                    data.Grid="GRID "+context.TargetCam.GetGrid();
                    if(targets.Count==1&&targets[0]!=null)
                    {
                        var unit=targets[0];data.Target=Clean(unit is Aircraft&&unit.definition!=null?unit.definition.unitName:unit.unitName);
                        if(unit is Aircraft aircraft&&aircraft.pilots!=null&&aircraft.pilots.Length>0&&aircraft.pilots[0]!=null&&aircraft.pilots[0].player!=null)
                            data.Pilot="PILOT "+aircraft.pilots[0].player.GetDisplayName(NuclearOption.Networking.PlayerNameContext.Other);
                        if((unit is Aircraft||unit is Missile)&&context.Aircraft.NetworkHQ!=null&&context.Aircraft.NetworkHQ.IsTargetPositionAccurate(unit,20f))
                        {
                            data.Motion="HDG "+unit.transform.eulerAngles.y.ToString("0")+" ALT "+UnitConverter.AltitudeReading(unit.GlobalPosition().y);
                            data.Relative="SPD "+UnitConverter.SpeedReading(unit.speed);
                        }
                    }
                }
            }
            if(!ReferenceEquals(station,previousStation)){previousStation=station;stationGeneration++;}
            var identity=new System.Text.StringBuilder();
            if(!aftermath)foreach(var target in targets)identity.Append(target==null?"none":target.persistentID.ToString()).Append(';');
            semantic=stationGeneration+"|"+identity+"|"+data.Page+"|"+data.Weapon+"|"+data.Target+"|"+data.Sensor+"|"+data.Hint+"|"+data.State+"|"+data.Missile+"|"+data.TargetSelected;
            truth=data;
        }
        private static string Clean(string value)=>(value??"").Replace('\n',' ').Replace('\r',' ');
        private void RefreshOverlay(int width,int height)
        {
            bool resize=overlay==null||overlay.width!=width||overlay.height!=height;
            if(!refresh.ShouldRefresh(Time.unscaledTimeAsDouble,semantic,width,height))return;
            if(resize){if(overlay!=null)Object.Destroy(overlay);overlay=new Texture2D(width,height,TextureFormat.RGBA32,false){name="TargetingPodDisplay.Symbology",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};}
            var top=OverlayRaster.Render(width,height,truth);var bottom=new byte[top.Length];int stride=width*4;
            for(int y=0;y<height;y++)Buffer.BlockCopy(top,y*stride,bottom,(height-1-y)*stride,stride);
            overlay.LoadRawTextureData(bottom);overlay.Apply(false,false);
        }
        internal void Compose(RenderTexture source)
        {
            if(source==null||Output==null||ReferenceEquals(source,Output))return;
            RefreshOverlay(Output.width,Output.height);
            RenderTargetScope.Run(()=>RenderTexture.active,previous=>RenderTexture.active=previous,()=>
            {
                Graphics.Blit(source,Output);RenderTexture.active=Output;GL.PushMatrix();
                try {GL.LoadPixelMatrix(0,Output.width,Output.height,0);Graphics.DrawTexture(new Rect(0,0,Output.width,Output.height),overlay);}
                finally { GL.PopMatrix(); }
            });
        }
        internal void DrawOverlay(Rect area)
        {
            // Only transparent symbology is scaled here. The owned scene camera renders directly to the screen.
            int width,height;
            if(area.width>=area.height){width=1024;height=Mathf.Max(128,Mathf.RoundToInt(1024*area.height/area.width));}
            else {height=1024;width=Mathf.Max(128,Mathf.RoundToInt(1024*area.width/area.height));}
            RefreshOverlay(width,height);GUI.DrawTexture(area,overlay,ScaleMode.StretchToFill,true);
        }
        public void Dispose(){nativeHint.Clear();previousStation=null;var texture=Output;Output=null;if(texture!=null){texture.Release();Object.Destroy(texture);}if(overlay!=null)Object.Destroy(overlay);overlay=null;}
    }
}
