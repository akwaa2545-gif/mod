using NuclearOptionTargetingPod;using Rewired;using UnityEngine;
static class Program {
 static int count;static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 static void Main(){
 ReInput.Reset();var p=new Player();ReInput.players.AllPlayers.Add(p);var map=new ControllerMap();p.controllers.maps.Maps.Add(map);
 var wheel=new ActionElementMap{controllerMap=map};var steering=new ActionElementMap{controllerMap=map,elementIdentifierId=0};var fire=new ActionElementMap{controllerMap=map,elementType=ControllerElementType.Button,elementIdentifierId=3};map.AllMaps.AddRange(new[]{wheel,steering,fire});
 bool eligible=true,context=true;using var lease=new WheelInputLease(()=>eligible,()=>context);
 lease.Request(true);Check(wheel.enabled&&!lease.TryConsume(out _),"request no premature mutation/sample");
 p.Cached=.1f;Time.frameCount=1;ReInput.Boundary();Check(wheel.enabled&&!lease.Active,"cached action tail blocks acquisition");
 p.Cached=0;Time.frameCount=2;ReInput.Boundary();Check(!wheel.enabled&&lease.Active&&steering.enabled&&fire.enabled,"only wheel disabled");
 ReInput.controllers.Mouse.Raw=1;Check(lease.TryConsume(out var d)&&d==1,"post-boundary fresh wheel");Check(!lease.TryConsume(out _),"no duplicate sample");
 lease.Request(false);eligible=false;Time.frameCount=3;ReInput.Boundary();Check(lease.Draining&&!wheel.enabled,"exit while moving retains only wheel lease");
 ReInput.controllers.Mouse.Raw=0;Time.frameCount=4;ReInput.Boundary();Check(!lease.Active&&wheel.enabled,"neutral restores");
 eligible=true;lease.Request(true);Time.frameCount=5;ReInput.Boundary();context=false;Time.frameCount=6;ReInput.Boundary();Check(wheel.enabled&&!lease.Active,"hard context loss clears before source");
 context=true;ReInput.mapping.Behavior.mouseOtherAxisMode=MouseOtherAxisMode.DigitalAxis;lease.Request(true);Time.frameCount=7;ReInput.Boundary();Check(wheel.enabled&&!lease.Active&&lease.Fault.Contains("digital"),"digital smoothing fails closed");
 ReInput.mapping.Behavior.mouseOtherAxisMode=MouseOtherAxisMode.MouseAxis;lease.Request(true);Time.frameCount=8;ReInput.Boundary();wheel.actionId=2;Check(!lease.TryConsume(out _)&&!lease.Active&&!wheel.enabled,"rebind does not restore unrelated assignment");
 wheel.enabled=true;lease.Request(true);Time.frameCount=9;ReInput.Boundary();map.AllMaps.Remove(wheel);lease.Clear();Check(!wheel.enabled,"detached original not mutated");
 map.AllMaps.Add(wheel);wheel.enabled=true;lease.Request(true);Time.frameCount=10;ReInput.Boundary();lease.Dispose();Check(wheel.enabled,"dispose restores");wheel.enabled=true;Time.frameCount=11;ReInput.Boundary();Check(wheel.enabled,"dispose unhooks boundary");
 int lost=0;bool sourceSawReleased=false;WheelInputLease callbackLease=null;
 callbackLease=new WheelInputLease(()=>eligible,()=>context,()=>{Check(wheel.enabled&&!callbackLease.Active,"callback after restoration");lost++;callbackLease.Clear();});
 callbackLease.Request(true);Time.frameCount=12;ReInput.Boundary();context=false;
 Action sourceObserver=()=>{sourceSawReleased=lost==1&&wheel.enabled;};ReInput.InputSourceUpdateEvent+=sourceObserver;
 Time.frameCount=13;ReInput.Boundary();Check(lost==1&&sourceSawReleased,"loss before next source observer");
 Time.frameCount=14;ReInput.Boundary();Check(lost==1,"callback once");ReInput.InputSourceUpdateEvent-=sourceObserver;
 context=true;callbackLease.Request(true);Time.frameCount=15;ReInput.Boundary();callbackLease.Clear();Check(lost==1,"explicit clear silent");
 callbackLease.Request(true);Time.frameCount=16;ReInput.Boundary();callbackLease.Request(false);Time.frameCount=17;ReInput.Boundary();Check(lost==1,"ordinary drain silent");
 callbackLease.Request(true);Time.frameCount=18;ReInput.Boundary();callbackLease.Dispose();Check(lost==1,"dispose silent");
 Console.WriteLine($"{count} wheel adapter assertions passed");
 }
}
