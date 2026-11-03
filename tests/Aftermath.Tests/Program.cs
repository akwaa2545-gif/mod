using NuclearOptionTargetingPod;
int failed=0,passed=0;
void Test(string name,Action test){try{test();passed++;}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Check(bool ok){if(!ok)throw new Exception("assertion failed");}
AftermathPolicy Watched(){var p=new AftermathPolicy();p.Observe(1,7,new AftermathPoint(100,200,300),12,10,100);return p;}
Test("only actual notification arms",()=>{var p=Watched();Check(!p.TryGet(10,100,out _,out _));Check(p.Notify(1,7,10,100,3));Check(p.TryGet(12.99,102,out var pt,out var f)&&pt.X==100&&pt.Y==200&&f==12);Check(!p.TryGet(13,103,out _,out _));});
Test("ordinary removal never lingers",()=>{var p=Watched();p.NoSelection(100);Check(!p.TryGet(10,100,out _,out _));Check(!p.TryGet(11,101,out _,out _));Check(!p.Notify(1,7,11,101,3));});
Test("same frame removal then kill",()=>{var p=Watched();p.NoSelection(101);Check(p.Notify(1,7,10.1,101,3));Check(p.WatchedToken==0);Check(p.TryGet(10.2,102,out _,out _));});
Test("wrong lifetime or scene ignored",()=>{var p=Watched();Check(!p.Notify(2,7,10,100,3));Check(!p.Notify(1,8,10,100,3));Check(p.Notify(1,7,10,100,3));});
Test("new designation cancels",()=>{var p=Watched();p.Notify(1,7,10,100,3);p.Observe(2,7,new AftermathPoint(1,2,3),20,10.1,101);Check(!p.TryGet(11,102,out _,out _));Check(!p.Notify(1,7,11,102,3));});
Test("duplicate does not extend",()=>{var p=Watched();p.Notify(1,7,10,100,3);Check(!p.Notify(1,7,12,102,3));Check(!p.TryGet(13,103,out _,out _));});
Test("clear owner loss drops everything",()=>{var p=Watched();p.Notify(1,7,10,100,3);p.Clear();Check(!p.TryGet(10.1,101,out _,out _));Check(p.WatchedToken==0);});
Test("nonfinite and invalid durations failclosed",()=>{foreach(var d in new[]{double.NaN,double.PositiveInfinity,-1,0,11,31}){var p=Watched();Check(!p.Notify(1,7,10,100,d));}var q=Watched();q.Observe(1,7,new AftermathPoint(double.NaN,0,0),12,10,100);Check(!q.Notify(1,7,10,100,3));});
Test("stale cached frames rejected",()=>{var p=Watched();Check(!p.Notify(1,7,10.1,102,3));});
Test("backward or nonfinite clock cancels",()=>{var p=Watched();p.Notify(1,7,10,100,3);Check(!p.TryGet(9,101,out _,out _));Check(!p.TryGet(10,102,out _,out _));var q=Watched();q.Notify(1,7,10,100,3);Check(!q.TryGet(double.NaN,101,out _,out _));});
Test("same killed target still listed preserves timer",()=>{var p=Watched();Check(p.Notify(1,7,10,100,3));p.Observe(1,7,new AftermathPoint(400,500,600),50,10.1,101);Check(p.TryGet(11,102,out var pt,out var f)&&pt.X==100&&f==12);Check(!p.TryGet(13,103,out _,out _));});
Console.WriteLine($"Aftermath policy: {passed} passed, {failed} failed");return failed==0?0:1;
