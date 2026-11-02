using NuclearOptionObserver;
static class ScopedInputTests
{
 static void Check(bool v){if(!v)throw new Exception("scoped input assertion failed");}
 public static (string Name,Action Body)[] Cases=>new (string,Action)[]{
  ("camera masks only exact player named view queries inside a scope",()=>{
   var player=new object();var mask=new ScopedCameraMask(player,true);Check(!mask.Axis(player,"Pan View",out _));using(var lease=mask.Enter()){Check(mask.Axis(player,"Pan View",out var x)&&x==0);Check(!mask.Axis(new(),"Pan View",out _));Check(!mask.Axis(player,"Pitch",out _));Check(mask.Button(player,"Free Look",out var f)&&f);Check(mask.Button(player,"Switch View",out var b)&&!b);Check(!mask.Button(player,"Pause",out _));}Check(!mask.Axis(player,"Pan View",out _));
  }),
  ("nested camera scopes and exception cleanup are idempotent",()=>{
   var player=new object();var mask=new ScopedCameraMask(player,false);var outer=mask.Enter();try{using var inner=mask.Enter();Check(mask.Button(player,"Free Look",out var f)&&!f);throw new Exception("original camera fault");}catch{}Check(mask.Axis(player,"FOV",out _));outer!.Dispose();outer.Dispose();Check(!mask.Axis(player,"Pan View",out _));
  }),
  ("view query masks never apply from another thread",()=>{
   var player=new object();var mask=new ScopedCameraMask(player,true);using var lease=mask.Enter();Check(!Task.Run(()=>mask.Axis(player,"Pan View",out _)).Result);
  }),
  ("manual input command neutralizes axes and brake while preserving throttle and custom axis",()=>{
   Check(HeldManualInput.TryCapture(.63f,-.2f,out var c)&&c.Pitch==0&&c.Roll==0&&c.Yaw==0&&c.Brake==0&&c.Throttle==.63f&&c.CustomAxis==-.2f);
   Check(!HeldManualInput.TryCapture(float.NaN,0,out _)&&!HeldManualInput.TryCapture(0,float.PositiveInfinity,out _));
  }),
  ("partial fullscreen guard failure does not stop independent PiP observation",()=>{
   using var pip=new ObservationGate(()=>true,()=>{},()=>{},()=>{});Check(pip.TryStart());int patches=0;using var full=new ObservationGate(()=>{patches=5;throw new Exception("later required guard failed");},()=>throw new Exception("must not publish"),()=>{},()=>patches=0);Check(!full.TryStart()&&patches==0&&!full.CanObserve&&pip.CanObserve);
  }),
  ("target death returns image while held RMB/fire is drained before native input resumes",()=>{
   var mode=new FullscreenState();var drain=new ReleaseBoundary();mode.Toggle(true);mode.Ensure(false);drain.Begin();int nativeCalls=0;
   for(int frame=1;frame<=3;frame++)if(!drain.Poll(frame,true,true))nativeCalls++;
   Check(!mode.Active&&nativeCalls==0);if(!drain.Poll(4,false,true))nativeCalls++;Check(nativeCalls==0);if(!drain.Poll(5,false,true))nativeCalls++;Check(nativeCalls==1);
  }),
  ("axis input replacement precedes filtering and button or render phases cannot erase filtered output",()=>{
   var state=new object();float output=.9f;int writes=0;Action raw=()=>{output=0;writes++;};
   Check(!PilotInputBoundary.AllowNative(true,state,state,PilotInputRoot.Axes,raw)&&writes==1&&output==0);
   output=.27f; // Native FilterInputs correction after the axis root.
   Check(!PilotInputBoundary.AllowNative(true,state,state,PilotInputRoot.Buttons,raw)&&writes==1&&output==.27f);
   Check(PilotInputBoundary.AllowNative(false,state,state,PilotInputRoot.Axes,raw)&&writes==1&&output==.27f);
   Check(PilotInputBoundary.AllowNative(true,new(),state,PilotInputRoot.Axes,raw)&&writes==1);
  }),
  ("release drain checks every inspected suppressed held or release driven action",()=>{
   foreach(string action in new[]{"Brake","Next Weapon","Previous Weapon","Turret Control","Gear","Eject","Fire","Countermeasures","Next Countermeasure","Flight Assist","Radar","Nav Lights","Toggle Engine","Link Guns","Axis Modifier","Center","Switch View","Change Spectate Faction","Spectate Next Aircraft","Cycle Look At","Free Look"})Check(ExitActions.AnyHeld(x=>x==action));
   Check(!ExitActions.AnyHeld(_=>false));
  }),
  ("keyboard countermeasures and Switch View release do not resume native action during drain",()=>{
   var d=new ReleaseBoundary();d.Begin();int native=0;bool counter=true,switchView=true;Func<string,bool> held=x=>x=="Countermeasures"&&counter||x=="Switch View"&&switchView;
   if(!d.Poll(1,ExitActions.AnyHeld(held),true))native++;counter=false;if(!d.Poll(2,ExitActions.AnyHeld(held),true))native++;
   switchView=false;if(!d.Poll(3,ExitActions.AnyHeld(held),true))native++;Check(native==0);if(!d.Poll(4,ExitActions.AnyHeld(held),true))native++;Check(native==1);
  }),
  ("return drain consumes release frame then ends and cancels on identity loss",()=>{
   var d=new ReleaseBoundary();d.Begin();Check(d.Poll(1,true,true));Check(d.Poll(2,false,true));Check(d.Poll(2,false,true));Check(!d.Poll(3,false,true));d.Begin();Check(!d.Poll(4,true,false));
  }),
 };
}
