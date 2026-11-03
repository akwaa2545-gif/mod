using System;
using System.Collections.Generic;
namespace NuclearOptionTargetingPod
{
    public sealed class InputConfiguration : IEquatable<InputConfiguration>
    {
        private readonly int[] keys;
        private readonly bool confirmed,enabled,cockpit,wheel;
        public InputConfiguration(int[] keys,bool confirmed,bool enabled,bool cockpit,bool wheel=true){this.keys=(int[])keys.Clone();this.confirmed=confirmed;this.enabled=enabled;this.cockpit=cockpit;this.wheel=wheel;}
        internal void AddKeys(HashSet<int> destination){foreach(int key in keys)if(key!=0)destination.Add(key);}
        public bool Equals(InputConfiguration? other)
        {
            if(other==null||confirmed!=other.confirmed||enabled!=other.enabled||cockpit!=other.cockpit||wheel!=other.wheel||keys.Length!=other.keys.Length)return false;
            for(int i=0;i<keys.Length;i++)if(keys[i]!=other.keys[i])return false;return true;
        }
    }
    // Exact immutable configuration generation plus previous/current key-up boundary.
    public sealed class ConfigurationGate
    {
        private InputConfiguration? snapshot;
        private readonly HashSet<int> releaseKeys=new HashSet<int>();
        private bool blocked=true;
        private int clearFrame=-1;
        public bool Matches(InputConfiguration current)=>snapshot!=null&&snapshot.Equals(current);
        public void Block(){blocked=true;clearFrame=-1;snapshot?.AddKeys(releaseKeys);}
        public bool Poll(InputConfiguration current,int frame,Func<int,bool> held,out bool changed)
        {
            changed=snapshot!=null&&!snapshot.Equals(current);
            if(snapshot==null||changed){snapshot?.AddKeys(releaseKeys);current.AddKeys(releaseKeys);snapshot=current;blocked=true;clearFrame=-1;}
            if(!blocked)return true;
            foreach(int key in releaseKeys)if(held(key)){clearFrame=-1;return false;}
            if(clearFrame<0){clearFrame=frame;return false;}
            if(frame<=clearFrame)return false;
            blocked=false;releaseKeys.Clear();return true;
        }
    }
    public static class RenderTargetScope
    {
        // Includes the first Blit in the guarded body: Unity can change active target there.
        public static void Run<T>(Func<T> read,Action<T> restore,Action render)
        {T previous=read();try{render();}finally{restore(previous);}}
    }
}
