using NuclearOptionObserver;

static class MapGestureTests
{
    static void Check(bool value) { if (!value) throw new Exception("map gesture assertion failed"); }
    public static (string Name,Action Body)[] Cases => new (string,Action)[] {
        ("map capture rejects every invalid modifier context or other mouse button",()=>{
            for(int b=0;b<64;b++)Check(MapCapturePolicy.CanCapture((b&1)!=0,(b&2)!=0,(b&4)!=0,(b&8)!=0,(b&16)!=0,(b&32)!=0)==(b==3));
        }),
        ("lazy mouse controller and late phases capture and dispatch one gesture only",()=>{
            var map=new object();var icon=new object();var g=new MapGesture<object>();int captures=0,dispatches=0,native=0;
            MapHit<object>? Hit(){captures++;return new(map,icon,new object());}
            g.Observe(10,true,true,false,true,true,Hit);if(!g.SuppressMapSelect(map))native++;
            g.Observe(10,true,true,false,true,true,Hit);if(!g.SuppressIcon(map,icon,1))native++;
            g.Observe(10,true,true,false,true,true,Hit);if(g.TryTake(out _))dispatches++;if(g.TryTake(out _))dispatches++;
            Check(captures==1&&dispatches==1&&native==0&&g.Serial==1);
        }),
        ("early Ctrl release preserves paired mouse suppression until release frame ends",()=>{
            var map=new object();var icon=new object();var g=new MapGesture<object>();g.Observe(1,true,true,false,true,true,()=>new(map,icon,new()));
            Check(g.TryTake(out _));g.Observe(2,false,true,false,true,false,()=>null);Check(g.SuppressIcon(map,icon,0));
            g.Observe(3,false,false,true,true,false,()=>null);Check(g.SuppressIcon(map,icon,0)&&g.SuppressMapSelect(map));
            g.Observe(3,false,false,true,true,false,()=>null);Check(!g.TryTake(out _));
            g.Observe(4,false,false,false,true,false,()=>null);Check(!g.SuppressIcon(map,icon,0)&&!g.SuppressMapSelect(map)&&!g.SuppressIcon(map,icon,1));
        }),
        ("captured gesture consumes stale no-op without native fallback",()=>{
            var map=new object();var icon=new object();var g=new MapGesture<object>();g.Observe(1,true,true,false,true,true,()=>new(map,icon,new()));
            g.Cancel();Check(!g.TryTake(out _)&&g.SuppressMapSelect(map)&&g.SuppressIcon(map,icon,0));
        }),
        ("context loss cancels dispatch but retains only captured release consumption",()=>{
            var map=new object();var icon=new object();var g=new MapGesture<object>();g.Observe(1,true,true,false,true,true,()=>new(map,icon,new()));
            g.Observe(2,false,true,false,false,false,()=>null);Check(!g.TryTake(out _)&&g.SuppressMapSelect(map));
            Check(!g.SuppressMapSelect(new object())&&!g.SuppressIcon(map,new object(),0)&&!g.SuppressIcon(map,icon,2));
        }),
        ("replaced map cancels dispatch but retains the captured icon release only",()=>{
            var map=new object();var other=new object();var icon=new object();var g=new MapGesture<object>();g.Observe(1,true,true,false,true,true,()=>new(map,icon,new()));
            g.CancelIfMapChanged(other);Check(!g.TryTake(out _)&&g.SuppressIcon(other,icon,0)&&!g.SuppressMapSelect(other)&&!g.SuppressIcon(other,icon,1));
        }),
        ("top overlay nonmissile or absent native icon does not capture or suppress",()=>{
            var map=new object();var g=new MapGesture<object>();g.Observe(1,true,true,false,true,true,()=>null);Check(!g.TryTake(out _)&&!g.SuppressMapSelect(map));
        }),
        ("held click or late Ctrl cannot create a watch gesture",()=>{
            var g=new MapGesture<object>();int captures=0;MapHit<object>? Hit(){captures++;return new(new(),new(),new());}
            g.Observe(1,true,true,false,true,false,Hit);g.Observe(1,true,true,false,true,true,Hit);g.Observe(2,false,true,false,true,true,Hit);Check(captures==0&&!g.TryTake(out _));
        }),
        ("ordinary and Shift clicks remain native",()=>{
            var map=new object();var icon=new object();var g=new MapGesture<object>();g.Observe(1,true,true,false,true,false,()=>new(map,icon,new()));
            Check(!g.SuppressMapSelect(map)&&!g.SuppressIcon(map,icon,0)&&!g.SuppressIcon(map,icon,1));
        }),
        ("partial map hook failure rolls back without stopping list observation",()=>{
            using var list=new ObservationGate(()=>true,()=>{},()=>{},()=>{});Check(list.TryStart());int patches=0,published=0;
            using var map=new ObservationGate(()=>{patches++;throw new Exception("second map hook failed");},()=>published++,()=>published--,()=>patches=0);
            Check(!map.TryStart()&&!map.CanObserve&&map.Failure!=null&&patches==0&&published==0&&list.CanObserve);
        }),
        ("next unrelated pointer gesture receives a fresh serial",()=>{
            var map=new object();var icon=new object();var g=new MapGesture<object>();g.Observe(1,true,true,false,true,true,()=>new(map,icon,new()));Check(g.TryTake(out _));
            g.Observe(2,false,false,true,true,true,()=>null);g.Observe(3,true,true,false,true,true,()=>new(map,icon,new()));Check(g.TryTake(out _)&&g.Serial==2);
        }),
    };
}
