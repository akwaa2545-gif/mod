using NuclearOptionObserver;
static class FullscreenTests
{
 static void Check(bool value){if(!value)throw new Exception("fullscreen assertion failed");}
 public static (string Name,Action Body)[] Cases=>new (string,Action)[]{
  ("fullscreen defaults to PiP and requires an eligible selected context",()=>{
   var s=new FullscreenState();Check(!s.Active&&!s.Toggle(false));Check(!s.Active&&s.Toggle(true)&&s.Active);Check(s.Toggle(false)&&!s.Active);
  }),
  ("fullscreen context or target loss exits and clears transient orbit",()=>{
   var s=new FullscreenState();s.Toggle(true);Check(s.Orbit(35,20));s.Ensure(false);Check(!s.Active&&s.Yaw==0&&s.Pitch==0);s.Ensure(false);Check(!s.Active);
  }),
  ("orbit applies only in fullscreen wraps yaw and clamps pitch",()=>{
   var s=new FullscreenState();Check(!s.Orbit(10,20)&&s.Yaw==0);s.Toggle(true);Check(s.Orbit(450,120)&&Math.Abs(s.Yaw-90)<.001&&s.Pitch==80);s.Orbit(-900,-300);Check(Math.Abs(s.Yaw+90)<.001&&s.Pitch==-80);s.ResetOrbit();Check(s.Yaw==0&&s.Pitch==0&&s.Active);
  }),
  ("nonfinite orbit input is rejected without changing pose state",()=>{
   var s=new FullscreenState();s.Toggle(true);s.Orbit(12,23);Check(!s.Orbit(float.NaN,0)&&s.Yaw==12&&s.Pitch==23);Check(!s.Orbit(0,float.PositiveInfinity));
  }),
  ("fullscreen render size is bounded and preserves standard screen aspect",()=>{
   var a=RenderSize.ForScreen(true,3840,2160);Check(a.Width==1280&&a.Height==720);var b=RenderSize.ForScreen(true,3440,1440);Check(b.Width<=1280&&b.Height<=720&&Math.Abs((double)b.Width/b.Height-3440.0/1440)<.005);
   var c=RenderSize.ForScreen(true,1080,1920);Check(c.Width==405&&c.Height==720);Check(RenderSize.ForScreen(true,640,480).Width==640);
  }),
  ("PiP dimensions remain unchanged and invalid screen dimensions stay finite",()=>{
   var a=RenderSize.ForScreen(false,8000,8000);Check(a.Width==480&&a.Height==270);var b=RenderSize.ForScreen(true,0,-1);Check(b.Width>=1&&b.Height>=1&&b.Width<=1280&&b.Height<=720);
  }),
  ("extra key defaults preserve existing custom keys and avoid every collision",()=>{
   var defaults=FullscreenKeys.Normalize(289,290,291,292,288);Check(defaults.Fullscreen==292&&defaults.Reset==288&&ShortcutPolicy.ValidFive(289,290,291,292,288));
   var custom=FullscreenKeys.Normalize(292,290,291,292,288);Check(custom.Fullscreen!=292&&custom.Reset==288&&ShortcutPolicy.ValidFive(292,290,291,custom.Fullscreen,custom.Reset));
   var invalid=FullscreenKeys.Normalize(289,290,291,32,32);Check(ShortcutPolicy.ValidFive(289,290,291,invalid.Fullscreen,invalid.Reset));Check(!ShortcutPolicy.ValidFive(289,290,291,289,288));
  }),
 };
}
