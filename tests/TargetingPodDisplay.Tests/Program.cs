using NuclearOptionTargetingPod;
int count=0;
void Check(bool condition,string label) { if(!condition) throw new Exception(label); count++; }
Check(DisplayPolicy.Unique(new[]{false,true,false})==1,"unique");
Check(DisplayPolicy.Unique(new[]{true,true})==-1,"ambiguous");
Check(DisplayPolicy.Unique(Array.Empty<bool>())==-1,"empty");
Check(DisplayPolicy.Unique(new[]{false,false})==-1,"none");
object native=new(), owned=new(), other=new();
Check(DisplayPolicy.MayRestore(owned,owned),"restore own");
Check(!DisplayPolicy.MayRestore(other,owned),"respect successor");
Check(!DisplayPolicy.MayRestore(null,owned),"destroyed");
Check(DisplayPolicy.Page(true,true)=="MISSILE","missile priority");
Check(DisplayPolicy.Page(false,true)=="TGP / BOMB","bomb");
Check(DisplayPolicy.Page(false,false)=="TGP","other");
Check(DisplayPolicy.Designation(0)=="TARGET NONE","empty not no lock");
Check(DisplayPolicy.Designation(1)=="TARGET SELECTED","selection not lock");
Check(DisplayPolicy.Designation(3)=="TARGETS 3","multi");
Check(DisplayPolicy.Sensor(true)=="IR (GAME)","IR honesty");
Check(DisplayPolicy.Sensor(false)=="COLOR","color");
Check(DisplayPolicy.Fit(1920,1080,1024,1024).Width==1080,"square fit");
Check(DisplayPolicy.Fit(1000,1000,1920,1080).Height==562.5f,"wide fit");
Check(DisplayPolicy.Fit(0,100,10,10).Width==0,"invalid fit");
Check(!DisplayPolicy.CanPresent(true,true,false,true),"landing");
Check(!DisplayPolicy.CanPresent(true,false,true,true),"disabled source");
Check(!DisplayPolicy.CanPresent(false,true,true,true),"offline");
Check(!DisplayPolicy.CanPresent(true,true,true,false),"empty selection");
Check(DisplayPolicy.CanPresent(true,true,true,true),"valid");
Console.WriteLine($"{count} targeting display policy assertions passed; no game assemblies loaded.");
Check(LeasePolicy.CanOwn(true,true,false,false,false,false),"idle competitor");
Check(!LeasePolicy.CanOwn(true,false,false,false,false,false),"unknown competitor");
Check(!LeasePolicy.CanOwn(true,true,true,false,false,false),"observer fullscreen");
Check(!LeasePolicy.CanOwn(true,true,false,true,false,false),"observer drain");
Check(!LeasePolicy.CanOwn(true,true,false,false,true,false),"observer hold");
Check(!LeasePolicy.CanOwn(true,true,false,false,false,true),"observer modal");
Check(!LeasePolicy.CanOwn(false,true,false,false,false,false),"invalid context");
var angles=new SensorPolicy();angles.Slew(400,100);Check(angles.Yaw==135&&angles.Pitch==85,"mod limits");
angles.Zoom(-1000);Check(angles.Fov==1,"min fov");angles.Zoom(1000);Check(angles.Fov==60,"max fov");
angles.Reset();Check(angles.Yaw==0&&angles.Pitch==0&&angles.Fov==20,"reset");
Check(!angles.Slew(float.NaN,0),"reject nan");
Console.WriteLine($"{count} total targeting display assertions passed.");
var settings=new InputConfiguration(new[]{1,2,3,4,5,6,7,8,9,10},true,true,true);
var configGate=new ConfigurationGate();
Check(!configGate.Poll(settings,1,_=>false,out _),"initial release boundary");
Check(configGate.Poll(settings,2,_=>false,out _),"released configuration ready");
for(int i=0;i<10;i++)
{
    var keys=new[]{1,2,3,4,5,6,7,8,9,10};keys[i]=100+i;
    Check(!configGate.Poll(new InputConfiguration(keys,true,true,true),3+i*3,_=>false,out var changed)&&changed,"each key change invalidates "+i);
    Check(!configGate.Poll(settings,4+i*3,_=>false,out _),"returning old settings still invalidates");
    Check(configGate.Poll(settings,5+i*3,_=>false,out _),"release rearm "+i);
}
Check(!configGate.Poll(new InputConfiguration(new[]{1,0,3,4,5,6,7,8,9,10},true,true,true),40,k=>k==2,out _),"manual None invalidates old held toggle");
Check(!configGate.Poll(settings,41,k=>k==2,out _),"old held key survives config restore");
Check(!configGate.Poll(settings,42,_=>false,out _),"keyup frame blocked");
Check(configGate.Poll(settings,43,_=>false,out _),"next released frame rearmed");
Check(!configGate.Matches(new InputConfiguration(new[]{1,2,3,4,5,6,7,8,9,10},false,true,true)),"confirmation change invalid");
Check(!configGate.Matches(new InputConfiguration(new[]{1,2,3,4,5,6,7,8,9,10},true,false,true)),"enabled change invalid");
Check(!configGate.Matches(new InputConfiguration(new[]{1,2,3,4,5,6,7,8,9,10},true,true,false)),"cockpit change invalid");
var immutableKeys=new[]{1,2};var immutableSettings=new InputConfiguration(immutableKeys,true,true,true);immutableKeys[0]=99;
Check(immutableSettings.Equals(new InputConfiguration(new[]{1,2},true,true,true)),"snapshot copies input array");
Console.WriteLine($"{count} total targeting display assertions passed after configuration lease regressions.");
object callerTarget=new(),outputTarget=new();object activeTarget=callerTarget;bool sawCaller=false;
RenderTargetScope.Run(()=>{sawCaller=ReferenceEquals(activeTarget,callerTarget);return activeTarget;},v=>activeTarget=v,()=>activeTarget=outputTarget);
Check(sawCaller&&ReferenceEquals(activeTarget,callerTarget),"capture before blit mutation and restore on success");
try{RenderTargetScope.Run(()=>activeTarget,v=>activeTarget=v,()=>{activeTarget=outputTarget;throw new InvalidOperationException("blit failure");});}catch(InvalidOperationException){}
Check(ReferenceEquals(activeTarget,callerTarget),"restore after first blit failure");
try{RenderTargetScope.Run(()=>activeTarget,v=>activeTarget=v,()=>{activeTarget=outputTarget;throw new ArgumentException("overlay failure");});}catch(ArgumentException){}
Check(ReferenceEquals(activeTarget,callerTarget),"restore after overlay failure");
Check(DisplayPolicy.Unique(new[]{true,false})==0,"one owned target consumer");
Check(DisplayPolicy.Unique(new[]{true,true})==-1,"new duplicate original or owned output consumer invalidates");
Console.WriteLine($"{count} total targeting display assertions passed after render regressions.");
Check(DisplayPolicy.CanBindHierarchy(true,true,true,true,false,false,0),"distinct landing sharing immutable source permitted");
Check(!DisplayPolicy.CanBindHierarchy(true,true,true,true,true,false,0),"new target under landing invalidates");
Check(!DisplayPolicy.CanBindHierarchy(true,true,true,true,false,true,0),"new landing under target invalidates");
Check(!DisplayPolicy.CanBindHierarchy(false,true,true,true,false,false,0),"tac reparented out of cockpit invalidates");
Check(!DisplayPolicy.CanBindHierarchy(true,false,true,true,false,false,0),"target reparented out of tac invalidates");
Check(!DisplayPolicy.CanBindHierarchy(true,true,false,true,false,false,0),"landing reparented out of tac invalidates");
Check(!DisplayPolicy.CanBindHierarchy(true,true,true,false,false,false,0),"aliased landing invalidates");
Check(!DisplayPolicy.CanBindHierarchy(true,true,true,true,false,false,1),"added renderer invalidates");
Console.WriteLine($"{count} total targeting display assertions passed.");
var localAircraft=new object();var remoteAircraft=new object();
var cockpitPresenter=new FakeCockpitPresenter(localAircraft,true);var damagePart=new FakeDamagePart();
Check(CockpitOwnerPolicy.Unique(new object[]{damagePart,cockpitPresenter},localAircraft,c=>c is FakeCockpitPresenter p?p.Owner:null,c=>c is FakeCockpitPresenter p&&p.Initialized)==cockpitPresenter,"damage marker is not presentation component");
Check(CockpitOwnerPolicy.Unique(new object[]{damagePart},localAircraft,c=>c is FakeCockpitPresenter p?p.Owner:null,c=>c is FakeCockpitPresenter p&&p.Initialized)==null,"missing presenter preserves standalone feed");
Check(CockpitOwnerPolicy.Unique(new[]{new FakeCockpitPresenter(remoteAircraft,true)},localAircraft,c=>c.Owner,c=>c.Initialized)==null,"remote owned presenter rejected");
Check(CockpitOwnerPolicy.Unique(new[]{new FakeCockpitPresenter(localAircraft,false)},localAircraft,c=>c.Owner,c=>c.Initialized)==null,"uninitialized presenter rejected");
Check(CockpitOwnerPolicy.Unique(new[]{cockpitPresenter,new FakeCockpitPresenter(localAircraft,true)},localAircraft,c=>c.Owner,c=>c.Initialized)==null,"ambiguous local presenters rejected");
Check(CockpitOwnerPolicy.Unique(new[]{cockpitPresenter},localAircraft,c=>c.Owner,c=>c.Initialized,0)==null,"bounded search refuses overflow");
Console.WriteLine($"{count} total targeting display assertions passed after cockpit owner regression.");
// Normal local complex physics reparents intact parts out of the Aircraft transform.
Check(CockpitOwnerPolicy.CanUseRegisteredPart(true,false,true,true),"registered intact root may be outside aircraft transform");
Check(!CockpitOwnerPolicy.CanUseRegisteredPart(false,false,true,true),"remote registered part rejected");
Check(!CockpitOwnerPolicy.CanUseRegisteredPart(true,true,true,true),"damaged detached root rejected");
Check(!CockpitOwnerPolicy.CanUseRegisteredPart(true,false,false,true),"unloaded part rejected");
Check(!CockpitOwnerPolicy.CanUseRegisteredPart(true,false,true,false),"foreign scene part rejected");
Check(CockpitOwnerPolicy.Unique(new[]{cockpitPresenter,cockpitPresenter},localAircraft,c=>c.Owner,c=>c.Initialized)==cockpitPresenter,"overlapping registered roots deduplicate same component");
var ownerHandleA=new FakeNativeHandle(7);var ownerHandleB=new FakeNativeHandle(7);
Check(CockpitOwnerPolicy.Unique(new[]{new FakeCockpitPresenter(ownerHandleA,true)},ownerHandleB,c=>c.Owner,c=>c.Initialized,sameOwner:(a,b)=>a is FakeNativeHandle x&&b is FakeNativeHandle y&&x.Id==y.Id)!=null,"native owner identity can match separate managed wrappers");
Check(CockpitOwnerPolicy.Unique(new[]{new FakeCockpitPresenter(ownerHandleA,true)},new FakeNativeHandle(8),c=>c.Owner,c=>c.Initialized,sameOwner:(a,b)=>a is FakeNativeHandle x&&b is FakeNativeHandle y&&x.Id==y.Id)==null,"different native owner identity rejected");
Console.WriteLine($"{count} total targeting display assertions passed after registered-root regression.");
Check(CockpitOwnerPolicy.CanUseCandidate(true,true,false),"intact registered containing part outside aircraft root accepted");
Check(!CockpitOwnerPolicy.CanUseCandidate(true,false,true),"nested detached or deregistered part cannot inherit approved ancestor");
Check(CockpitOwnerPolicy.CanUseCandidate(false,false,true),"presenter directly on main aircraft hierarchy accepted");
Check(!CockpitOwnerPolicy.CanUseCandidate(false,false,false),"unregistered unrelated root rejected");
Console.WriteLine($"{count} total targeting display assertions passed.");
var registeredParts=new List<RegisteredPart>();
var normalPart=new RegisteredPart(localAircraft);registeredParts.Add(normalPart);
var registeredPresenter=new RegisteredPresenter(localAircraft,normalPart);
RegisteredPresenter? ResolveRegistered(params RegisteredPresenter[] candidates)=>CockpitOwnerPolicy.Unique(candidates,localAircraft,c=>c.Owner,
 c=>c.Initialized&&CockpitOwnerPolicy.AttachedToRegistry(c,registeredParts,x=>x.Part,p=>ReferenceEquals(p.Owner,localAircraft)&&!p.Detached,x=>x.InMainRoot));
Check(ResolveRegistered(registeredPresenter)==registeredPresenter,"registry fixture nested presenter resolved");
registeredPresenter.InMainRoot=false;
Check(ResolveRegistered(registeredPresenter)==registeredPresenter,"registry fixture normal unparent retains presentation");
registeredParts.Remove(normalPart);
Check(ResolveRegistered(registeredPresenter)==null,"registry removal rejects stale owner and transform");
registeredParts.Add(normalPart);
Check(ResolveRegistered(registeredPresenter)==registeredPresenter,"registry re-add permits valid owner again");
normalPart.Detached=true;registeredPresenter.InMainRoot=true;
Check(ResolveRegistered(registeredPresenter)==null,"nested damaged part cannot inherit aircraft root proof");
normalPart.Detached=false;normalPart.Owner=remoteAircraft;
Check(ResolveRegistered(registeredPresenter)==null,"registered remote part rejects stale presenter owner");
normalPart.Owner=localAircraft;
Check(ResolveRegistered(registeredPresenter,registeredPresenter)==registeredPresenter,"overlapping root enumerations still one component");
Console.WriteLine($"{count} total targeting display assertions passed after mutable registry fixture.");
var renderMain=new RenderFlag(true);var renderCockpit=new RenderFlag(true);
var renderLease=new RenderLease<RenderFlag>(renderMain,renderCockpit,x=>x.Enabled,(x,v)=>x.Enabled=v);
Check(!renderMain.Enabled&&!renderCockpit.Enabled,"lease suppresses only captured render flags");
Check(renderLease.Owns(renderMain),"exact disabled main accepted by context");
Check(!renderLease.Owns(new RenderFlag(false)),"unrelated disabled camera rejected");
renderLease.Release(false);
Check(renderMain.Enabled&&!renderCockpit.Enabled,"state switch restores main but leaves new cockpit choice");
renderMain.Enabled=true;renderCockpit.Enabled=true;
renderLease=new RenderLease<RenderFlag>(renderMain,renderCockpit,x=>x.Enabled,(x,v)=>x.Enabled=v);
renderLease.Release(true);
Check(renderMain.Enabled&&renderCockpit.Enabled,"same owner restores both captured values");
renderLease.Release(true);Check(renderMain.Enabled,"release idempotent");
renderMain.Enabled=true;renderCockpit.Enabled=false;
renderLease=new RenderLease<RenderFlag>(renderMain,renderCockpit,x=>x.Enabled,(x,v)=>x.Enabled=v);
renderMain.Enabled=true;
Check(!renderLease.Owns(renderMain),"external render reactivation loses ownership");
renderLease.Release(true);Check(renderMain.Enabled&&!renderCockpit.Enabled,"restore respects successor and original disabled cockpit");
Console.WriteLine($"{count} total targeting display assertions including direct render lease.");
Check(DisplayPolicy.SurfaceMissile(true,1,0,0),"native surface-role missile gets long reticle");
Check(!DisplayPolicy.SurfaceMissile(true,1,1,0),"ambiguous multirole missile remains generic");
Check(!DisplayPolicy.SurfaceMissile(true,0,1,0),"air missile remains generic");
Check(!DisplayPolicy.SurfaceMissile(false,1,0,0),"bomb never gets missile reticle");
Check(!DisplayPolicy.SurfaceMissile(true,float.NaN,0,0),"invalid surface role remains generic");
Console.WriteLine($"{count} total targeting display assertions including surface-role symbology.");
var rasterCadence=new OverlayRefreshPolicy();
Check(rasterCadence.ShouldRefresh(1,"target1|SHOOT",1024,1024),"first raster rendered");
Check(!rasterCadence.ShouldRefresh(1.1,"target1|SHOOT",1024,1024),"unchanged numeric telemetry throttled");
Check(rasterCadence.ShouldRefresh(1.11,"target2|",1024,1024),"target and expired hint invalidate immediately");
Check(rasterCadence.ShouldRefresh(1.12,"target2|MANUAL",1024,1024),"view mode invalidates immediately");
Check(rasterCadence.ShouldRefresh(1.13,"target2|MANUAL",1024,576),"fullscreen dimensions invalidate immediately");
Check(!rasterCadence.ShouldRefresh(1.2,"target2|MANUAL",1024,576),"unchanged dimensions remain throttled");
Check(rasterCadence.ShouldRefresh(1.4,"target2|MANUAL",1024,576),"numeric telemetry eventually refreshes");
Console.WriteLine($"{count} total targeting display assertions including semantic raster invalidation.");
Check(DisplayPolicy.OutputSize(256,128).Width==1024&&DisplayPolicy.OutputSize(256,128).Height==512,"wide native feed aspect preserved");
Check(DisplayPolicy.OutputSize(128,256).Width==512&&DisplayPolicy.OutputSize(128,256).Height==1024,"portrait native feed aspect preserved");
Check(DisplayPolicy.OutputSize(256,256).Width==1024,"small square feed gets readable raster");
Check(DisplayPolicy.OutputSize(4096,64).Width==0,"extreme unsupported aspect fails closed instead of distorting");
Console.WriteLine($"{count} total targeting display assertions including native aspect preservation.");
Check(Math.Abs(WheelZoom.Adjust(10,1)-8.5f)<.001,"wheel forward narrows own field of view");
Check(WheelZoom.Adjust(10,-1)>10,"wheel backward widens own field of view");
Check(WheelZoom.Adjust(.1f,4)==.1f,"wheel min FOV bounded");
Check(WheelZoom.Adjust(60,-4)==60,"wheel max FOV bounded");
Check(WheelZoom.Adjust(10,float.NaN)==10,"nonfinite wheel ignored");
Check(WheelZoom.Adjust(10,100)==WheelZoom.Adjust(10,4),"wheel burst bounded");
Console.WriteLine($"{count} total targeting display assertions including wheel zoom.");
Check(NativeHintPolicy.Allowed("OUT OF ARC",false),"fresh native arc warning survives disabled graphic");
Check(NativeHintPolicy.Allowed("TOO SLOW",false),"fresh native speed warning survives disabled graphic");
Check(!NativeHintPolicy.Allowed("SHOOT",false),"intentionally hidden SHOOT is not manufactured");
Check(NativeHintPolicy.Allowed("SHOOT",true),"native visible SHOOT retained");
Check(!NativeHintPolicy.Allowed("FIRE",true),"unknown invented fire text rejected");
Console.WriteLine($"{count} total targeting display assertions including native hint visibility.");
Check(!new InputConfiguration(new[]{1},true,true,true,true).Equals(new InputConfiguration(new[]{1},true,true,true,false)),"wheel isolation config invalidates active input generation");
Console.WriteLine($"{count} total targeting display assertions including wheel configuration generation.");
sealed class RenderFlag { public bool Enabled;public RenderFlag(bool value){Enabled=value;} }
sealed class RegisteredPart { public object Owner; public bool Detached; public RegisteredPart(object owner){Owner=owner;} }
sealed class RegisteredPresenter { public object Owner;public RegisteredPart? Part;public bool Initialized=true,InMainRoot=true;public RegisteredPresenter(object owner,RegisteredPart? part){Owner=owner;Part=part;} }
sealed class FakeNativeHandle { public int Id {get;} public FakeNativeHandle(int id){Id=id;} }
sealed class FakeDamagePart { }
sealed class FakeCockpitPresenter
{
    public object Owner {get;} public bool Initialized {get;}
    public FakeCockpitPresenter(object owner,bool initialized){Owner=owner;Initialized=initialized;}
}
