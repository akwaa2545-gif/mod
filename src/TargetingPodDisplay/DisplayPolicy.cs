using System;
namespace NuclearOptionTargetingPod
{
    public static class DisplayPolicy
    {
        public static bool CanBindHierarchy(bool tacInCockpit,bool targetInTac,bool landingInTac,bool distinct,bool targetInLanding,bool landingInTarget,int targetRenderers) =>
            tacInCockpit&&targetInTac&&landingInTac&&distinct&&!targetInLanding&&!landingInTarget&&targetRenderers==0;
        public static int Unique(bool[] matches) { int found=-1; for(int i=0;i<matches.Length;i++) if(matches[i]) { if(found>=0)return -1; found=i; } return found; }
        public static bool MayRestore(object? current,object owned) => current!=null && ReferenceEquals(current,owned);
        public static string Page(bool missile,bool bomb) => missile ? "MISSILE" : bomb ? "TGP / BOMB" : "TGP";
        public static string Designation(int count) => count<=0 ? "TARGET NONE" : count==1 ? "TARGET SELECTED" : "TARGETS "+count;
        public static string Sensor(bool ir) => ir ? "IR (GAME)" : "COLOR";
        public static bool CanPresent(bool context,bool sourceEnabled,bool notLanding,bool hasSelection) => context&&sourceEnabled&&notLanding&&hasSelection;
        public readonly struct Area { public readonly float X,Y,Width,Height; public Area(float x,float y,float width,float height){X=x;Y=y;Width=width;Height=height;} }
        public static Area Fit(float width,float height,float sourceWidth,float sourceHeight)
        {
            if(width<=0||height<=0||sourceWidth<=0||sourceHeight<=0)return new Area();
            float scale=Math.Min(width/sourceWidth,height/sourceHeight), w=sourceWidth*scale,h=sourceHeight*scale;
            return new Area((width-w)/2,(height-h)/2,w,h);
        }
    }
}
