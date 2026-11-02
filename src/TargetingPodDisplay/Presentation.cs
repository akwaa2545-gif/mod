#nullable disable
using System;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NuclearOptionTargetingPod
{
    // One owned output. Native feed is read-only and never released/destroyed here.
    internal sealed class Presentation : IDisposable
    {
        internal RenderTexture Output {get; private set;}
        private readonly BitmapText font = new BitmapText();
        private readonly NativeHint nativeHint=new NativeHint();
        private string hint="WPN HINT N/A";
        private string page="TGP",weapon="",designation="TARGET NONE",sensor="COLOR";
        private bool missile, manual, stabilized;
        private string selectedInfo="", selectedMotion="", selectedRelative="", pilotInfo="";
        private float manualFov;
        internal void SetManual(bool value,bool directionHold,float fov=0){manual=value;stabilized=directionHold;manualFov=fov;}
        internal Presentation(int width,int height)
        {
            Output=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){name="TargetingPodDisplay.Output",useMipMap=false,autoGenerateMips=false};
            if(!Output.Create()){Dispose();throw new InvalidOperationException("Presentation texture allocation failed");}
        }
        internal void UpdateTruth(PodContext context)
        {
            var station=context.Aircraft.weaponManager.currentWeaponStation;
            var info=station==null?null:station.WeaponInfo;
            missile=info!=null&&info.missile;
            page=DisplayPolicy.Page(missile,info!=null&&(info.bomb||info.glideBomb));
            weapon=info==null?"WPN NONE":Clean(info.weaponName);
            hint=nativeHint.Read(context);
            designation=DisplayPolicy.Designation(context.Aircraft.weaponManager.GetTargetList().Count);
            sensor=manual?"COLOR (OWN CAMERA)":DisplayPolicy.Sensor(context.TargetCam.UsingIR());
            selectedInfo=""; selectedMotion=""; selectedRelative=""; pilotInfo="";
            var targets=context.Aircraft.weaponManager.GetTargetList();
            if(targets.Count>0)
            {
                selectedInfo="SEL RNG "+UnitConverter.DistanceReading(context.TargetCam.GetDist())+" GRID "+context.TargetCam.GetGrid();
                if(targets.Count==1&&targets[0]!=null)
                {
                    var unit=targets[0];
                    selectedInfo=(unit is Aircraft&&unit.definition!=null?unit.definition.unitName:unit.unitName)+" | "+selectedInfo;
                    if(unit is Aircraft targetAircraft&&targetAircraft.pilots!=null&&targetAircraft.pilots.Length>0&&targetAircraft.pilots[0]!=null&&targetAircraft.pilots[0].player!=null)
                        pilotInfo="PILOT "+targetAircraft.pilots[0].player.GetDisplayName(NuclearOption.Networking.PlayerNameContext.Other);
                    if((unit is Aircraft||unit is Missile)&&context.Aircraft.NetworkHQ!=null&&context.Aircraft.NetworkHQ.IsTargetPositionAccurate(unit,20f))
                    {
                        selectedMotion="SEL HDG "+unit.transform.eulerAngles.y.ToString("0")+" ALT "+UnitConverter.AltitudeReading(unit.GlobalPosition().y)+" SPD "+UnitConverter.SpeedReading(unit.speed);
                        var relative=unit.GlobalPosition()-context.Aircraft.GlobalPosition();
                        selectedRelative="REL ALT "+UnitConverter.AltitudeReading(relative.y);
                        if(unit.rb!=null&&context.Aircraft.rb!=null)selectedRelative+=" CLOSING "+UnitConverter.SpeedReading(Vector3.Dot(context.Aircraft.rb.velocity,relative.normalized)-Vector3.Dot(unit.rb.velocity,relative.normalized));
                    }
                }
            }
        }
        private static string Clean(string value) => (value??"WPN N/A").Replace('\n',' ').Replace('\r',' ');
        internal void Compose(RenderTexture source)
        {
            if(source==null||Output==null||ReferenceEquals(source,Output))return;
            RenderTargetScope.Run(()=>RenderTexture.active,previous=>RenderTexture.active=previous,()=>
            {
                Graphics.Blit(source,Output);
                RenderTexture.active=Output; GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0,Output.width,Output.height,0);
                    float w=Output.width,h=Output.height,s=Mathf.Min(w,h),margin=s*.035f;
                    Color green=new Color(.5f,.94f,.64f,1);
                    Line(margin,margin,w-margin,margin,green);Line(margin,h-margin,w-margin,h-margin,green);
                    Line(margin,margin,margin,h-margin,green);Line(w-margin,margin,w-margin,h-margin,green);
                    float length=s*(missile?.30f:.065f),gap=s*.025f;
                    Line(w/2-length,h/2,w/2-gap,h/2,Color.white);Line(w/2+gap,h/2,w/2+length,h/2,Color.white);
                    Line(w/2,h/2-length,w/2,h/2-gap,Color.white);Line(w/2,h/2+gap,w/2,h/2+length,Color.white);
                    int size=Mathf.Clamp((int)(s*.022f),12,32);
                    Text(page+"  "+weapon,margin+5,margin+5,size,green);
                    Text(sensor+" | THERM N/A",margin+5,margin+size+10,size,green);
                    Text(hint,margin+5,h-margin-size*4-16,size,green);
                    Text(designation+" | WPN LOCK N/A",margin+5,h-margin-size*3-12,size,green);
                    Text(manual?(stabilized?"CAM INERTIAL | FLIGHT LIVE":"CAM FREE | FLIGHT LIVE"):"NATIVE CAM | INPUT OFF",margin+5,h-margin-size*2-8,size,green);
                    if(manual){Text("SENSOR FOV "+manualFov.ToString("0.0"),margin+5,margin+size*2+15,size,green);Text(selectedInfo,margin+5,margin+size*3+20,size,green);Text(selectedMotion,margin+5,margin+size*4+25,size,green);Text(selectedRelative,margin+5,margin+size*5+30,size,green);Text(pilotInfo,margin+5,margin+size*6+35,size,green);}
                    Text(manual?"DEDICATED KEYS | NIGHT N/A":"MANUAL OFF | NIGHT N/A",margin+5,h-margin-size-4,size,green);
                }
                finally { GL.PopMatrix(); }
            });
        }
        private static void Line(float x1,float y1,float x2,float y2,Color color)
        {
            Graphics.DrawTexture(new Rect(x1,y1,Mathf.Max(1,x2-x1),Mathf.Max(1,y2-y1)),Texture2D.whiteTexture,new Rect(0,0,1,1),0,0,0,0,color);
        }
        private void Text(string value,float x,float y,int size,Color color)
        {
            // Original text only. Native type/pilot/range/grid/velocity remain in native source image.
            font.Draw(value.ToUpperInvariant(),x,y,size,color,Output.width-10);
        }

        public void Dispose(){var texture=Output;Output=null;if(texture!=null){texture.Release();Object.Destroy(texture);}font.Dispose();}
    }
}
