using NuclearOptionTargetingPod;
class Element : IWheelLeaseElement { public object Identity=>this; public string Binding {get;set;}="wheel/action1"; public bool Attached {get;set;}=true; public bool Enabled {get;set;}=true; }
static class Program {
 static int count; static void Check(bool value,string name){count++;if(!value)throw new Exception(name);}
 static void Main(){
 var e=new Element();var p=new WheelLeasePolicy();p.Request(true);Check(!p.Active,"request does not arm");
 p.BeforeSource(new[]{e},true,false,1);Check(e.Enabled&&!p.Active,"cached input nonneutral blocks entry");
 p.BeforeSource(new[]{e},true,true,2);Check(!e.Enabled&&p.Active,"boundary disables only element");
 Check(p.Consume(2,1,out var d)&&d==1,"fresh sample");Check(!p.Consume(2,1,out _),"once per frame");
 Check(!p.Consume(3,1,out _),"no stale boundary");
 p.Request(false);Check(p.Draining&&!e.Enabled,"ordinary exit drains");p.BeforeSource(new[]{e},true,false,3);Check(!e.Enabled,"nonneutral drain stays muted");p.BeforeSource(new[]{e},true,true,4);Check(e.Enabled&&!p.Active,"neutral drain restores");
 p.Request(true);p.BeforeSource(new[]{e},true,true,5);p.Clear();Check(e.Enabled,"hard clear restores");
 p.Request(true);p.BeforeSource(new[]{e},true,true,6);e.Binding="rebound";p.Clear();Check(!e.Enabled,"never writes rebound binding");
 e.Enabled=true;p.Request(true);p.BeforeSource(new[]{e},true,true,7);e.Attached=false;p.Clear();Check(!e.Enabled,"never writes detached element");
 e.Attached=true;e.Enabled=false;p.Request(true);p.BeforeSource(new[]{e},true,true,8);p.Clear();Check(!e.Enabled,"original disabled retained");
 e.Enabled=true;p.Request(true);p.BeforeSource(new[]{e},true,true,9);var n=new Element();p.BeforeSource(new[]{e,n},true,true,10);Check(!p.Active&&e.Enabled&&n.Enabled,"new mapping aborts with restoration");
 p.Request(true);p.BeforeSource(new[]{e},true,true,11);p.BeforeSource(new[]{e},false,true,12);Check(e.Enabled&&!p.Active,"invalid owner clears");
 p.Request(true);p.BeforeSource(new[]{e},true,true,13);e.Enabled=true;Check(!p.Validate(new[]{e}),"external enabled conflict");p.Clear();Check(e.Enabled,"external value preserved");
 Console.WriteLine($"{count} wheel lease assertions passed");
 }
}
