using NuclearOptionObserver;

static class LaunchTests
{
    static void Check(bool value) { if (!value) throw new Exception("launch behavior assertion failed"); }
    public static (string Name, Action Body)[] Cases => new (string,Action)[] {
        ("auto launch default off ignores events without changing epoch", () => {
            var e=new LifetimeEpochs<object>(); var target=new object(); using var q=new LaunchObservation<object>(e,()=>true,1);
            q.Observe(target,true); Check(!q.Enabled && q.TakeReady().Length==0 && e.Read(target)==0);
        }),
        ("enabling auto launch never invents an existing launch", () => {
            var e=new LifetimeEpochs<object>(); var target=new object(); using var q=new LaunchObservation<object>(e,()=>true,1);
            q.Observe(target,true); q.SetEnabled(true); Check(q.Enabled && q.TakeReady().Length==0 && e.Read(target)==0);
        }),
        ("enabled actual registration is deferred and epoch validated", () => {
            var e=new LifetimeEpochs<object>(); var target=new object(); using var q=new LaunchObservation<object>(e,()=>true,1);
            q.SetEnabled(true); q.Observe(target,true); var rows=q.TakeReady(); Check(rows.Length==1 && q.IsCurrent(rows[0]) && e.Read(target)>0);
            e.ObserveEnable(target); Check(!q.IsCurrent(rows[0]));
        }),
        ("auto toggling discards queued events and does not revive them", () => {
            using var q=new LaunchObservation<object>(new(),()=>true,1); q.SetEnabled(true); q.Observe(new(),true);
            q.SetEnabled(false); q.SetEnabled(true); Check(q.TakeReady().Length==0);
        }),
        ("deregistration invalidates pending own launch", () => {
            using var q=new LaunchObservation<object>(new(),()=>true,1); var target=new object(); q.SetEnabled(true); q.Observe(target,true); q.Observe(target,false); Check(q.TakeReady().Length==0);
        }),
        ("guard loss and dispose discard pending automatic launches", () => {
            bool ready=true; using var q=new LaunchObservation<object>(new(),()=>ready,1); q.SetEnabled(true); q.Observe(new(),true);
            ready=false; Check(q.TakeReady().Length==0); ready=true; Check(q.TakeReady().Length==0);
            q.Observe(new(),true); q.Dispose(); q.SetEnabled(true); q.Observe(new(),true); Check(q.TakeReady().Length==0 && !q.Enabled);
        }),
        ("launch event retains captured persistent identity rather than a later ID", () => {
            using var q=new LaunchObservation<object>(new(),()=>true,1); q.SetEnabled(true); q.Observe(new(),true,7); Check(q.TakeReady().Single().EntityId==7);
        }),
        ("turning auto off preserves the accepted target epoch", () => {
            var e=new LifetimeEpochs<object>();var target=new object();using var q=new LaunchObservation<object>(e,()=>true,1);q.SetEnabled(true);q.Observe(target,true);Check(q.TakeReady().Length==1);
            long accepted=e.Read(target);q.SetEnabled(false);q.Observe(target,true);Check(e.Read(target)==accepted);
        }),
        ("automatic acceptance requires current eligible own owner and captured ID",()=>{
            for(int b=0;b<8;b++)Check(OwnLaunchPolicy.Accept((b&1)!=0,(b&2)!=0,(b&4)!=0,9,9,7,7)==(b==7));
            Check(!OwnLaunchPolicy.Accept(true,true,true,8,9,7,7));Check(!OwnLaunchPolicy.Accept(true,true,true,9,9,7,8));
            Check(!OwnLaunchPolicy.Accept(true,true,true,9,9,0,0));Check(!OwnLaunchPolicy.Accept(true,true,true,0,0,7,7));
        }),
        ("native registration callback failures are captured not thrown", () => {
            using var q=new LaunchObservation<object>(new(),()=>throw new Exception("readiness fault"),1); q.SetEnabled(true); q.Observe(new(),true); Check(q.Failure!=null && !q.Enabled);
        }),
    };
}
