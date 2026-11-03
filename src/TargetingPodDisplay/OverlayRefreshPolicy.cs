namespace NuclearOptionTargetingPod
{
    internal sealed class OverlayRefreshPolicy
    {
        private string semantic="";
        private int width,height;
        private double next;
        internal bool ShouldRefresh(double now,string current,int w,int h)
        {
            if(current==semantic&&w==width&&h==height&&now<next)return false;
            semantic=current;width=w;height=h;next=now+.2;return true;
        }
    }
}
