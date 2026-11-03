using System;
namespace NuclearOptionTargetingPod
{
    public static class DisplayPolicy
    {
        // Native effectiveness scores are presentation hints, not an AGM type or lock report.
        public static bool SurfaceMissile(bool missile,float surface,float air,float antimissile)=>missile&&SensorPolicy.Finite(surface)&&SensorPolicy.Finite(air)&&SensorPolicy.Finite(antimissile)&&surface>0&&surface>air&&surface>antimissile;
        public static bool CanBindHierarchy(bool tacInCockpit,bool targetInTac,bool landingInTac,bool distinct,bool targetInLanding,bool landingInTarget,int targetRenderers) =>
            tacInCockpit&&targetInTac&&landingInTac&&distinct&&!targetInLanding&&!landingInTarget&&targetRenderers==0;
        public static int Unique(bool[] matches) { int found=-1; for(int i=0;i<matches.Length;i++) if(matches[i]) { if(found>=0)return -1; found=i; } return found; }
        public static bool MayRestore(object? current,object owned) => current!=null && ReferenceEquals(current,owned);
        public static string Page(bool missile,bool bomb) => missile ? "MISSILE" : bomb ? "TGP / BOMB" : "TGP";
        public static string Designation(int count) => count<=0 ? "TARGET NONE" : count==1 ? "TARGET SELECTED" : "TARGETS "+count;
        public static string Sensor(bool ir) => ir ? "IR (GAME)" : "COLOR";
        public static bool CanPresent(bool context,bool sourceEnabled,bool notLanding,bool hasSelection) => context&&sourceEnabled&&notLanding&&hasSelection;
        public readonly struct Area { public readonly float X,Y,Width,Height; public Area(float x,float y,float width,float height){X=x;Y=y;Width=width;Height=height;} }
        public static Area OutputSize(int width,int height)
        {
            if(width<64||height<64||width>4096||height>4096)return new Area();
            double scale=1024.0/Math.Max(width,height);int w=(int)Math.Round(width*scale),h=(int)Math.Round(height*scale);
            return w<128||h<128?new Area():new Area(0,0,w,h);
        }
        public static Area Fit(float width,float height,float sourceWidth,float sourceHeight)
        {
            if(width<=0||height<=0||sourceWidth<=0||sourceHeight<=0)return new Area();
            float scale=Math.Min(width/sourceWidth,height/sourceHeight), w=sourceWidth*scale,h=sourceHeight*scale;
            return new Area((width-w)/2,(height-h)/2,w,h);
        }
    }
}
