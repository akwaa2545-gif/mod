using System;
using System.Collections.Generic;
using UnityEngine;
namespace NuclearOptionTargetingPod
{
    // Original tiny geometric 5x7 alphabet. No font/screenshot asset dependency.
    internal sealed class BitmapText : IDisposable
    {
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
            ['.']="00000/00000/00000/00000/00000/00110/00110",['?']="01110/10001/00001/00010/00100/00000/00100"
        };
        private readonly Dictionary<char,Texture2D> glyphs=new Dictionary<char,Texture2D>();
        private Texture2D Glyph(char c)
        {
            if(glyphs.TryGetValue(c,out var value))return value;
            if(!Rows.TryGetValue(c,out var pattern))pattern=Rows['?'];
            var rows=pattern.Split('/');var pixels=new Color[35];
            for(int y=0;y<7;y++)for(int x=0;x<5;x++)pixels[(6-y)*5+x]=rows[y][x]=='1'?Color.white:Color.clear;
            value=new Texture2D(5,7,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};value.SetPixels(pixels);value.Apply(false,true);glyphs[c]=value;return value;
        }
        internal void Draw(string text,float x,float y,int size,Color color,float right)
        {
            float scale=size/7f,width=Mathf.Min(right-x,text.Length*scale*6);
            Graphics.DrawTexture(new Rect(x-2,y-2,width+4,size+4),Texture2D.whiteTexture,new Rect(0,0,1,1),0,0,0,0,new Color(0,0,0,.85f));
            foreach(char c in text){if(x+5*scale>right)break;if(c!=' ')Graphics.DrawTexture(new Rect(x,y,5*scale,size),Glyph(c),new Rect(0,0,1,1),0,0,0,0,color);x+=6*scale;}
        }
        public void Dispose(){foreach(var glyph in glyphs.Values)UnityEngine.Object.Destroy(glyph);glyphs.Clear();}
    }
}
