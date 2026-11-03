using System;
using System.Collections.Generic;
namespace NuclearOptionTargetingPod
{
    // Pure RGBA overlay, top-down rows. Does not own or inspect game cameras/targets.
    internal sealed class OverlayData
    {
        internal string Page="", Weapon="", Sensor="", Magnification="", Target="", Range="", Grid="", Hint="", State="", Pilot="", Motion="", Relative="";
        internal bool Missile, Tracking, RangeNearReticle, TargetSelected;
        internal string Clock="", Laser="";
        internal float? PitchDegrees, RollDegrees;
    }
    internal static class OverlayRaster
    {
        internal static byte[] Render(int width, int height, OverlayData data)
        {
            if(width<128 || height<128 || width>4096 || height>4096) throw new ArgumentOutOfRangeException(nameof(width), "Overlay dimensions must be 128..4096.");
            if(data==null) throw new ArgumentNullException(nameof(data));
            var c=new Canvas(width,height);
            int s=Math.Min(width,height), margin=(int)Math.Round(s*.032), inset=margin+Math.Max(3,(int)Math.Round(s*.012));
            int scale=Math.Max(1,(int)Math.Round(s*.020/7)), glyph=7*scale, pitch=glyph+Math.Max(1,(int)Math.Round(s*.004));
            int thickness=Math.Max(1,(int)Math.Round(s*.0015));
            c.Rect(margin,margin,width-2*margin,thickness,128,240,162,255);
            c.Rect(margin,height-margin-thickness,width-2*margin,thickness,128,240,162,255);
            c.Rect(margin,margin,thickness,height-2*margin,128,240,162,255);
            c.Rect(width-margin-thickness,margin,thickness,height-2*margin,128,240,162,255);
            // Missile means caller-confirmed AGM role. TargetSelected is native selection,
            // deliberately not a claim of prelaunch seeker lock.
            bool selectedAgm=data.Missile && data.TargetSelected;
            int cx=width/2,cy=height/2,gap=(int)Math.Round(s*(selectedAgm?.020:.018)), arm=(int)Math.Round(s*(selectedAgm?.29:.060));
            byte rr=selectedAgm?(byte)128:(byte)235, gg=selectedAgm?(byte)240:(byte)244, bb=selectedAgm?(byte)162:(byte)235;
            c.Rect(cx-arm,cy,arm-gap,thickness,rr,gg,bb,255); c.Rect(cx+gap,cy,arm-gap,thickness,rr,gg,bb,255);
            c.Rect(cx,cy-arm,thickness,arm-gap,rr,gg,bb,255); c.Rect(cx,cy+gap,thickness,arm-gap,rr,gg,bb,255);
            if(selectedAgm)
            {
                for(int tick=0;tick<3;tick++)
                {
                    int yy=cy+(int)Math.Round(s*(.14+.05*tick));
                    int half=(int)Math.Round(s*(.018+.012*tick));
                    c.Rect(cx-half,yy,2*half,thickness,128,240,162,255);
                }
            }
            if(data.Tracking && !data.Missile)
            {
                int radius=(int)Math.Round(s*.09), leg=(int)Math.Round(s*.023);
                foreach(int dx in new[]{-1,1}) foreach(int dy in new[]{-1,1})
                {
                    int x=cx+dx*radius,y=cy+dy*radius;
                    c.Rect(dx<0?x:x-leg,y,leg,thickness,235,244,235,255);
                    c.Rect(x,dy<0?y:y-leg,thickness,leg,235,244,235,255);
                }
            }
            int rows=Math.Max(1,(int)Math.Floor((height*.15-inset-3)/pitch));
            int leftWidth=(int)(width*.55)-inset, rightWidth=(int)(width*.35)-inset;
            string[] top={data.Page,data.Weapon,data.Target,data.Pilot};
            for(int i=0;i<Math.Min(rows,top.Length);i++)c.Text(top[i],inset,inset+i*pitch,scale,leftWidth,false);
            c.Text(data.Sensor,width-inset,inset,scale,rightWidth,true);
            if(rows>1)c.Text(data.Magnification,width-inset,inset+pitch,scale,rightWidth,true);
            bool attitude=data.PitchDegrees.HasValue && data.RollDegrees.HasValue &&
                !float.IsNaN(data.PitchDegrees.Value) && !float.IsInfinity(data.PitchDegrees.Value) &&
                !float.IsNaN(data.RollDegrees.Value) && !float.IsInfinity(data.RollDegrees.Value);
            int leftX=attitude?(int)Math.Round(s*.20):inset;
            string[] left={data.RangeNearReticle?"":data.Range,data.Grid,data.State};
            string[] right={data.Hint,data.Relative,data.Motion};
            for(int i=0;i<Math.Min(rows,3);i++)
            {
                int y=height-inset-glyph-i*pitch;
                c.Text(left[i],leftX,y,scale,Math.Max(0,leftWidth-leftX+inset),false);
                c.Text(right[i],width-inset,y,scale,rightWidth,true);
            }
            if(data.RangeNearReticle)c.Text(data.Range,cx+(int)Math.Round(s*.12),cy+(int)Math.Round(s*.07),scale,(int)(s*.27),false);
            c.Text(data.Laser,width-inset,(int)(height*.45),scale,Math.Max(7*6*scale,(int)(s*.14)),true,true);
            if(attitude)
            {
                // Fixed aircraft reference; ground arc rotates opposite positive right bank.
                // Positive nose-up pitch moves ground below aircraft. Display travel is clamped.
                int ax=(int)Math.Round(s*.09),ay=(int)Math.Round(height*.90),radius=(int)Math.Round(s*.030);
                double roll=-data.RollDegrees.GetValueOrDefault()*Math.PI/180.0;
                double offset=Math.Max(-45,Math.Min(45,data.PitchDegrees.GetValueOrDefault()))/45.0*radius;
                int lastX=0,lastY=0;
                for(int n=0;n<=48;n++)
                {
                    double angle=n*Math.PI/48,xx=Math.Cos(angle)*radius,yy=Math.Sin(angle)*radius+offset;
                    int px=ax+(int)Math.Round(xx*Math.Cos(roll)-yy*Math.Sin(roll));
                    int py=ay+(int)Math.Round(xx*Math.Sin(roll)+yy*Math.Cos(roll));
                    if(n>0)c.Line(lastX,lastY,px,py,thickness);lastX=px;lastY=py;
                }
                c.Rect(ax-radius,ay,2*radius,thickness,128,240,162,255);
                c.Rect(ax,ay-radius/3,thickness,radius/3,128,240,162,255);
                c.Rect(ax-2*thickness,ay-2*thickness,5*thickness,5*thickness,128,240,162,255);
                c.Text(data.Clock,inset,ay-2*radius-glyph,scale,Math.Max(13*6*scale,(int)(s*.28)),false);
            }
            else c.Text(data.Clock,inset,height-inset-glyph-3*pitch,scale,Math.Max(13*6*scale,(int)(s*.28)),false);
            return c.Bytes;
        }
        private sealed class Canvas
        {
            internal readonly byte[] Bytes;
            private readonly int width,height;
            internal Canvas(int w,int h){width=w;height=h;Bytes=new byte[checked(w*h*4)];}
            internal void Rect(int x,int y,int w,int h,byte r,byte g,byte b,byte a)
            {
                int x0=Math.Max(0,x),y0=Math.Max(0,y),x1=Math.Min(width,x+w),y1=Math.Min(height,y+h);
                for(int py=y0;py<y1;py++)for(int px=x0;px<x1;px++){int i=(py*width+px)*4;Bytes[i]=r;Bytes[i+1]=g;Bytes[i+2]=b;Bytes[i+3]=a;}
            }
            internal void Line(int x0,int y0,int x1,int y1,int thickness)
            {
                int dx=Math.Abs(x1-x0),dy=-Math.Abs(y1-y0),sx=x0<x1?1:-1,sy=y0<y1?1:-1,error=dx+dy;
                while(true){Rect(x0,y0,thickness,thickness,128,240,162,255);if(x0==x1&&y0==y1)break;int e=2*error;if(e>=dy){error+=dy;x0+=sx;}if(e<=dx){error+=dx;y0+=sy;}}
            }
            internal void Text(string value,int x,int y,int scale,int maxWidth,bool right,bool allowUnavailable=false)
            {
                string text=(value??"").Replace('\r',' ').Replace('\n',' ').Trim().ToUpperInvariant();
                if(text.Length==0 || text=="--" || (!allowUnavailable && text.Contains("N/A")))return;
                int advance=scale*6,capacity=Math.Max(0,maxWidth/advance);
                if(capacity<1)return;
                if(text.Length>capacity)text=capacity>3?text.Substring(0,capacity-3)+"...":text.Substring(0,capacity);
                int length=text.Length*advance-scale;
                if(right)x-=length;
                Rect(x-2,y-2,length+4,7*scale+4,0,0,0,153);
                foreach(char ch in text)
                {
                    if(ch!=' ')
                    {
                        if(!Rows.TryGetValue(ch,out var pattern))pattern=Rows['?'];
                        for(int row=0;row<7;row++)for(int col=0;col<5;col++)if(pattern[row*6+col]=='1')Rect(x+col*scale,y+row*scale,scale,scale,128,240,162,255);
                    }
                    x+=advance;
                }
            }
        }
        private static readonly Dictionary<char,string> Rows=new Dictionary<char,string>{
            ['A']="01110/10001/10001/11111/10001/10001/10001",['B']="11110/10001/10001/11110/10001/10001/11110",['C']="01111/10000/10000/10000/10000/10000/01111",
            ['D']="11110/10001/10001/10001/10001/10001/11110",['E']="11111/10000/10000/11110/10000/10000/11111",['F']="11111/10000/10000/11110/10000/10000/10000",
            ['G']="01111/10000/10000/10111/10001/10001/01111",['H']="10001/10001/10001/11111/10001/10001/10001",['I']="11111/00100/00100/00100/00100/00100/11111",
            ['J']="00111/00010/00010/00010/10010/10010/01100",['K']="10001/10010/10100/11000/10100/10010/10001",['L']="10000/10000/10000/10000/10000/10000/11111",
            ['M']="10001/11011/10101/10101/10001/10001/10001",['N']="10001/11001/11001/10101/10011/10011/10001",['O']="01110/10001/10001/10001/10001/10001/01110",
            ['P']="11110/10001/10001/11110/10000/10000/10000",['Q']="01110/10001/10001/10001/10101/10010/01101",['R']="11110/10001/10001/11110/10100/10010/10001",
            ['S']="01111/10000/10000/01110/00001/00001/11110",['T']="11111/00100/00100/00100/00100/00100/00100",['U']="10001/10001/10001/10001/10001/10001/01110",
            ['V']="10001/10001/10001/10001/10001/01010/00100",['W']="10001/10001/10001/10101/10101/11011/10001",['X']="10001/10001/01010/00100/01010/10001/10001",
            ['Y']="10001/10001/01010/00100/00100/00100/00100",['Z']="11111/00001/00010/00100/01000/10000/11111",
            ['0']="01110/10001/10011/10101/11001/10001/01110",['1']="00100/01100/00100/00100/00100/00100/01110",['2']="01110/10001/00001/00010/00100/01000/11111",
            ['3']="11110/00001/00001/01110/00001/00001/11110",['4']="00010/00110/01010/10010/11111/00010/00010",['5']="11111/10000/10000/11110/00001/00001/11110",
            ['6']="01110/10000/10000/11110/10001/10001/01110",['7']="11111/00001/00010/00100/01000/01000/01000",['8']="01110/10001/10001/01110/10001/10001/01110",
            ['9']="01110/10001/10001/01111/00001/00001/01110",['/']="00001/00001/00010/00100/01000/10000/10000",['-']="00000/00000/00000/11111/00000/00000/00000",
            ['(']="00010/00100/01000/01000/01000/00100/00010",[')']="01000/00100/00010/00010/00010/00100/01000",['|']="00100/00100/00100/00100/00100/00100/00100",
            [':']="00000/00100/00100/00000/00100/00100/00000",['.']="00000/00000/00000/00000/00000/00110/00110",['?']="01110/10001/00001/00010/00100/00000/00100"
        };
    }
}
