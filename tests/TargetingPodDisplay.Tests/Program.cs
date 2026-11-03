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
sealed class FakeDamagePart { }
sealed class FakeCockpitPresenter
{
    public object Owner {get;} public bool Initialized {get;}
    public FakeCockpitPresenter(object owner,bool initialized){Owner=owner;Initialized=initialized;}
}
