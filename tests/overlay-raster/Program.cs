using NuclearOptionTargetingPod;
static class Program {
 static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
 static bool Pixel(byte[] b,int w,int x,int y,int r,int g,int blue,int a=255){int i=(y*w+x)*4;return b[i]==r&&b[i+1]==g&&b[i+2]==blue&&b[i+3]==a;}
 static void Main(){
  foreach(var (w,h) in new[]{(256,256),(384,256),(512,512),(1024,1024),(1536,1024),(1920,1080)}){
   var d=new OverlayData{Page="TGP",Weapon=new string('W',500),Sensor="IR (GAME)",Magnification="MAG 34.5",Target="HT FUEL TANKER",Range="RNG 2.4 KM",Grid="GRID GH98",Hint="",State="NATIVE",Tracking=false};
   var b=OverlayRaster.Render(w,h,d);Check(b.Length==w*h*4,"RGBA size");int s=Math.Min(w,h),m=(int)Math.Round(s*.032);
   Check(Pixel(b,w,m,m,128,240,162),"green border must be baked, not white");
   Check(Pixel(b,w,w/2,h/2,0,0,0,0),"center gap transparent");
   Check(Pixel(b,w,w/2+(int)Math.Round(s*.04),h/2,235,244,235),"white TGP crosshair");
   int greens=0;for(int y=(int)(h*.15);y<h*.85;y++)for(int x=(int)(w*.15);x<w*.85;x++){int i=(y*w+x)*4;if(b[i+3]>0){Check(b[i]==235&&b[i+1]==244&&b[i+2]==235,"central 70% only white reticle, no label/background");} }
   for(int i=0;i<b.Length;i+=4)if(b[i]==128&&b[i+1]==240&&b[i+2]==162&&b[i+3]==255)greens++;
   Check(greens>2*w+2*h,"actual glyph pixels exist");
   var missile=OverlayRaster.Render(w,h,new OverlayData{Page="MISSILE",Missile=true,TargetSelected=true});Check(Pixel(missile,w,w/2+(int)Math.Round(s*.20),h/2,128,240,162),"selected AGM green longer arms");
   var unselected=OverlayRaster.Render(w,h,new OverlayData{Missile=true});Check(Pixel(unselected,w,w/2+(int)Math.Round(s*.04),h/2,235,244,235),"unselected AGM ordinary white reticle");Check(Pixel(unselected,w,w/2+(int)Math.Round(s*.20),h/2,0,0,0,0),"unselected AGM has no long arms");
   var otherSelected=OverlayRaster.Render(w,h,new OverlayData{TargetSelected=true});Check(Pixel(otherSelected,w,w/2+(int)Math.Round(s*.20),h/2,0,0,0,0),"non-AGM selection has no long arms");
   for(int tick=0;tick<3;tick++){int yy=h/2+(int)Math.Round(s*(.14+.05*tick));int half=(int)Math.Round(s*(.024+.016*tick));Check(Pixel(missile,w,w/2+half-1,yy,128,240,162),"three lower ticks grow downward");Check(Pixel(missile,w,w/2+half+2,yy,0,0,0,0),"tick width bounded");}
   int stroke=Math.Max(2,(int)Math.Round(s*.004));
   int whiteRows=0,greenRows=0;for(int yy=h/2-10;yy<=h/2+10;yy++){if(Pixel(b,w,w/2+(int)Math.Round(s*.04),yy,235,244,235))whiteRows++;if(Pixel(missile,w,w/2+(int)Math.Round(s*.20),yy,128,240,162))greenRows++;}
   Check(whiteRows==stroke && greenRows==stroke,"TGP and AGM have bold pixel stroke");
   for(int tick=0;tick<3;tick++){int yy=h/2+(int)Math.Round(s*(.14+.05*tick));int rows=0;for(int py=yy-10;py<=yy+10;py++)if(Pixel(missile,w,w/2+(int)Math.Round(s*.012),py,128,240,162))rows++;Check(rows==stroke,"each AGM tick has bold stroke");}
   Check(Pixel(b,w,w/2+(int)Math.Round(s*.075),h/2,235,244,235),"TGP arms extended");
   Check(Pixel(missile,w,w/2+(int)Math.Round(s*.31),h/2,128,240,162),"AGM arms extended");
   Check(Pixel(missile,w,w/2,h/2,0,0,0,0),"selected AGM center remains split");
   Check(Pixel(b,w,w/2+(int)Math.Round(s*.20),h/2,0,0,0,0),"TGP not missile reticle");
   if(w==1024||w==384||w==1536)Save(b,w,h,$"overlay-{w}x{h}.ppm");
   Console.WriteLine($"PASS {w}x{h}");
  }
  var empty=OverlayRaster.Render(256,256,new OverlayData());var invalid=OverlayRaster.Render(256,256,new OverlayData{Hint="WPN HINT N/A",Target="\n\r",State="N/A"});Check(empty.SequenceEqual(invalid),"unavailable and whitespace suppressed");
  var attitude=new OverlayData{PitchDegrees=12,RollDegrees=25,Clock="SIM 12:07:28",Laser="LSR N/A",Range="RNG 2.4 KM",RangeNearReticle=true};
  var withAttitude=OverlayRaster.Render(1024,1024,attitude);var level=OverlayRaster.Render(1024,1024,new OverlayData{PitchDegrees=0,RollDegrees=0,Clock="SIM 12:07:28",Laser="LSR N/A",Range="RNG 2.4 KM",RangeNearReticle=true});Check(!withAttitude.SequenceEqual(level),"attitude and data change pixels");
  int laserPixels=0;for(int y=440;y<490;y++)for(int x=850;x<980;x++)if(Pixel(withAttitude,1024,x,y,128,240,162))laserPixels++;Check(laserPixels>20,"explicit unsupported laser indicator visible");
  Save(withAttitude,1024,1024,"attitude-demo.ppm");
  var noAttitude=OverlayRaster.Render(256,256,new OverlayData());var nanAttitude=OverlayRaster.Render(256,256,new OverlayData{PitchDegrees=float.NaN,RollDegrees=0});Check(noAttitude.SequenceEqual(nanAttitude),"invalid attitude omitted, never fabricated level");
  var zero=OverlayRaster.Render(1024,1024,new OverlayData{PitchDegrees=0,RollDegrees=0});var roll=OverlayRaster.Render(1024,1024,new OverlayData{PitchDegrees=0,RollDegrees=25});var pitch=OverlayRaster.Render(1024,1024,new OverlayData{PitchDegrees=12,RollDegrees=0});Check(!zero.SequenceEqual(roll),"roll alone moves arc");Check(!zero.SequenceEqual(pitch),"pitch alone moves arc");
  Check(Pixel(zero,1024,92,921,128,240,162)&&Pixel(roll,1024,92,921,128,240,162),"aircraft symbol fixed while arc moves");
  Check(!OverlayRaster.Render(384,256,new OverlayData{Laser="LSR N/A"}).SequenceEqual(OverlayRaster.Render(384,256,new OverlayData{Laser="LSR N/B"})),"PiP laser status suffix must remain visible");
  Check(!OverlayRaster.Render(384,256,new OverlayData{Clock="SIM 12:07:28"}).SequenceEqual(OverlayRaster.Render(384,256,new OverlayData{Clock="SIM 12:07:29"})),"PiP clock seconds must remain visible");
  bool rejected=false;try{OverlayRaster.Render(0,256,new OverlayData());}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"invalid allocation rejected");Console.WriteLine("PASS missing fields and invalid dimensions");
  foreach(var (w,h) in new[]{(384,256),(1024,1024),(1536,1024)}){
   var sample=new OverlayData{Page="TGP",Weapon="20MM ROTARY CANNON",Sensor="COLOR",Magnification="MAG 34.5",Target="HT FUEL TANKER",Range="RNG 2.4 KM",Grid="GRID GH98",State="NATIVE",Tracking=true,PitchDegrees=12,RollDegrees=25,Clock="SIM 12:07:28",Laser="LSR N/A",RangeNearReticle=true};
   Save(OverlayRaster.Render(w,h,sample),w,h,$"demo-{w}x{h}.ppm");
   sample.Page="MISSILE";sample.Weapon="NATIVE MISSILE NAME";sample.Missile=true;sample.TargetSelected=true;sample.State="TARGET SELECTED";sample.Tracking=false;Save(OverlayRaster.Render(w,h,sample),w,h,$"missile-{w}x{h}.ppm");
  }
 }
 static void Save(byte[] b,int w,int h,string path){using var f=File.Create(path);var header=System.Text.Encoding.ASCII.GetBytes($"P6\n{w} {h}\n255\n");f.Write(header);for(int i=0;i<b.Length;i+=4){float a=b[i+3]/255f;for(int c=0;c<3;c++)f.WriteByte((byte)(b[i+c]*a+58*(1-a)));}}
}
