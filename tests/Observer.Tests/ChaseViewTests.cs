using NuclearOptionObserver;
using System.Numerics;

static class ChaseViewTests
{
    static ChaseVector V(Vector3 v)=>new(v.X,v.Y,v.Z);
    static Vector3 V(ChaseVector v)=>new(v.X,v.Y,v.Z);
    static void Check(bool ok){if(!ok)throw new Exception("chase pose assertion failed");}
    static void Near(ChaseVector a,ChaseVector b,float tolerance=0.0001f)=>Check(Vector3.Distance(V(a),V(b))<tolerance);
    static ChasePose Pose(ChaseVector p,ChaseVector f){Check(ChaseView.TryCreate(p,f,out var pose,out var reason)&&reason.Length==0);return pose;}
    static void Basis(ChasePose p){Check(float.IsFinite(p.Position.X)&&float.IsFinite(p.Position.Y)&&float.IsFinite(p.Position.Z));Check(Math.Abs(V(p.Forward).Length()-1)<.00001f&&Math.Abs(V(p.Up).Length()-1)<.00001f&&Math.Abs(Vector3.Dot(V(p.Forward),V(p.Up)))<.00001f);}
    public static (string Name,Action Body)[] Cases=>new (string,Action)[]{
        ("missile rolls zero 45 90 180 produce the same chase pose",()=>{
            var baseline=Pose(new(10,20,30),V(Vector3.Transform(Vector3.UnitZ,Quaternion.CreateFromYawPitchRoll(.6f,.25f,0))));
            foreach(float roll in new[]{0,45,90,180}){var f=Vector3.Transform(Vector3.UnitZ,Quaternion.CreateFromYawPitchRoll(.6f,.25f,roll*MathF.PI/180));var p=Pose(new(10,20,30),V(f));Near(baseline.Position,p.Position);Near(baseline.Forward,p.Forward);Near(baseline.Up,p.Up);}
        }),
        ("chase looks exactly at missile center instead of sharing its offset parallel aim",()=>{
            var target=new ChaseVector(23,17,-29);foreach(var f in new[]{new ChaseVector(0,0,1),new(1,.3f,.4f),new(.1f,-.9f,.2f)}){var p=Pose(target,f);Basis(p);Check(Vector3.Dot(Vector3.Normalize(V(target)-V(p.Position)),V(p.Forward))>.999999f);}
        }),
        ("ordinary chase horizon derives from world up not missile or cockpit roll",()=>{
            foreach(var f in new[]{new ChaseVector(0,0,1),new(1,.2f,-1),new(-.3f,-.8f,.2f)}){var p=Pose(new(0,0,0),f);Basis(p);Check(p.Up.Y>0&&Math.Abs(Vector3.Cross(V(p.Forward),V(p.Up)).Y)<.00001f);}
        }),
        ("vertical and nearly vertical views have finite deterministic stable fallback",()=>{
            foreach(float sign in new[]{-1f,1f}){var baseline=Pose(new(1,2,3),new(0,sign,0));Basis(baseline);foreach(float epsilon in new[]{-1e-8f,0,1e-8f}){var p=Pose(new(1,2,3),new(epsilon,sign,-epsilon));Basis(p);Check(Vector3.Dot(V(baseline.Up),V(p.Up))>.99999f);}}
        }),
        ("invalid nonfinite target or direction fails closed with reason",()=>{
            foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){Check(!ChaseView.TryCreate(new(bad,0,0),new(0,0,1),out _,out var a)&&a.Length>0);Check(!ChaseView.TryCreate(new(0,0,0),new(0,bad,1),out _,out var b)&&b.Length>0);}
            Check(!ChaseView.TryCreate(new(0,0,0),new(0,0,0),out _,out var reason)&&reason.Length>0);
        }),
        ("unrepresentable chase offset fails closed instead of supplying invalid Unity rotation",()=>{
            Check(!ChaseView.TryCreate(new(float.MaxValue,float.MaxValue,float.MaxValue),new(0,0,1),out _,out var reason)&&reason.Length>0);
        }),
        ("chase pose is translation invariant within float precision",()=>{
            var target=new Vector3(4,5,6);var shift=new Vector3(123,-456,789);var a=Pose(V(target),new(.3f,.2f,1));var b=Pose(V(target+shift),new(.3f,.2f,1));Near(V(V(a.Position)+shift),b.Position);Near(a.Forward,b.Forward);Near(a.Up,b.Up);
        }),
        ("previous or cockpit pose cannot seed output orientation or position",()=>{
            var target=new ChaseVector(1,2,3);var f=new ChaseVector(.4f,.2f,1);var expected=Pose(target,f);
            foreach(var q in new[]{Quaternion.Identity,Quaternion.CreateFromYawPitchRoll(1,2,3),Quaternion.CreateFromYawPitchRoll(-2,.5f,1)}){var previous=new ChasePose(new(900,-400,80),V(Vector3.Transform(Vector3.UnitZ,q)),V(Vector3.Transform(Vector3.UnitY,q)));Check(ChaseView.TryCreate(target,f,out previous,out _));Near(expected.Position,previous.Position);Near(expected.Forward,previous.Forward);Near(expected.Up,previous.Up);}
        }),
        ("zero orbit exactly preserves stabilized chase and orbit stays target centered",()=>{
            var target=new ChaseVector(1,2,3);var expected=Pose(target,new(.4f,.2f,1));Check(ChaseView.TryOrbit(target,new(.4f,.2f,1),0,0,out var zero,out _));Near(expected.Position,zero.Position);Near(expected.Up,zero.Up);
            foreach(float yaw in new[]{-180f,-90,0,90,180})foreach(float pitch in new[]{-80f,0,80}){Check(ChaseView.TryOrbit(target,new(.4f,.2f,1),yaw,pitch,out var p,out _));Basis(p);Check(Vector3.Dot(Vector3.Normalize(V(target)-V(p.Position)),V(p.Forward))>.999999f);}
        }),
        ("orbit remains roll invariant and finite for vertical missile directions",()=>{
            Check(ChaseView.TryOrbit(new(0,0,0),new(0,1,0),70,20,out var p,out _));Basis(p);Check(ChaseView.TryOrbit(new(0,0,0),new(0,-1,0),-90,-30,out p,out _));Basis(p);
            var f=Vector3.Transform(Vector3.UnitZ,Quaternion.CreateFromYawPitchRoll(.6f,.25f,0));Check(ChaseView.TryOrbit(new(1,2,3),V(f),50,25,out var a,out _));foreach(float roll in new[]{45f,90,180}){f=Vector3.Transform(Vector3.UnitZ,Quaternion.CreateFromYawPitchRoll(.6f,.25f,roll*MathF.PI/180));Check(ChaseView.TryOrbit(new(1,2,3),V(f),50,25,out var b,out _));Near(a.Position,b.Position);Near(a.Up,b.Up);}
        }),
        ("orbit rejects nonfinite input with a reason",()=>{
            Check(!ChaseView.TryOrbit(new(0,0,0),new(0,0,1),float.NaN,0,out _,out var reason)&&reason.Length>0);
        }),
        ("nonunit forward normalizes without changing the chase pose",()=>{
            var a=Pose(new(1,2,3),new(.4f,.2f,1));var b=Pose(new(1,2,3),new(400,200,1000));Near(a.Position,b.Position);Near(a.Forward,b.Forward);Near(a.Up,b.Up);
        }),
    };
}
