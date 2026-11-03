namespace NuclearOptionTargetingPod
{
    // Global coordinates only. This policy retains no Unity object references.
    internal readonly struct AftermathPoint
    {
        internal readonly double X, Y, Z;
        internal AftermathPoint(double x, double y, double z) { X=x; Y=y; Z=z; }
    }
    internal sealed class AftermathPolicy
    {
        private long watched, consumed;
        private int scene, lastFrame, lostFrame=-1;
        private double observedAt, started, expires;
        private AftermathPoint cached;
        private float cachedFov;
        private bool active;
        internal long WatchedToken => watched;
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        internal void Observe(long token,int ownerScene,AftermathPoint point,float fov,double now,int frame)
        {
            if(token<=0 || !Finite(now) || !Finite(point.X)||!Finite(point.Y)||!Finite(point.Z)||!Finite(fov)||fov<=0||fov>=180||frame<0){Clear();return;}
            if(active&&consumed==token&&scene==ownerScene)return;
            if(watched!=token||scene!=ownerScene) active=false;
            watched=token;scene=ownerScene;cached=point;cachedFov=fov;observedAt=now;lastFrame=frame;lostFrame=-1;
        }
        internal void NoSelection(int frame)
        {
            if(active)return;
            if(watched==0)return;
            if(frame<lastFrame||frame-lastFrame>1||lostFrame>=0&&frame!=lostFrame){Clear();return;}
            lostFrame=frame;
        }
        internal bool Notify(long token,int ownerScene,double now,int frame,double duration)
        {
            if(active||watched==0||token!=watched||scene!=ownerScene)return false;
            if(!Finite(now)||now<observedAt||frame<lastFrame||frame-lastFrame>1||lostFrame>=0&&frame!=lostFrame||!Finite(duration)||duration<=0||duration>10){Clear();return false;}
            started=now;expires=now+duration;active=true;consumed=watched;watched=0;lostFrame=-1;return true;
        }
        internal bool TryGet(double now,int frame,out AftermathPoint point,out float fov)
        {
            point=default;fov=0;
            if(!active){if(watched!=0&&(frame<lastFrame||frame-lastFrame>1||lostFrame>=0&&frame!=lostFrame))Clear();return false;}
            if(!Finite(now)||now<started||now>=expires){Clear();return false;}
            point=cached;fov=cachedFov;return true;
        }
        internal void Clear(){watched=consumed=0;scene=0;lastFrame=0;lostFrame=-1;observedAt=started=expires=0;cached=default;cachedFov=0;active=false;}
    }
}
