using System;
using System.Collections.Generic;
namespace NuclearOptionTargetingPod
{
    internal interface IWheelLeaseElement
    {
        object Identity { get; }
        string Binding { get; }
        bool Attached { get; }
        bool Enabled { get; set; }
    }
    // No Unity/Rewired dependency: only exact element ownership and update boundaries.
    internal sealed class WheelLeasePolicy
    {
        private sealed class Saved
        {
            internal readonly IWheelLeaseElement Element;
            internal readonly string Binding;
            internal readonly bool Enabled;
            internal Saved(IWheelLeaseElement e){Element=e;Binding=e.Binding;Enabled=e.Enabled;}
        }
        private readonly List<Saved> saved=new List<Saved>();
        private bool requested,active,draining;
        private int boundary=-1,consumed=-1;
        internal bool Active=>active;
        internal bool Draining=>draining;
        internal string Fault {get;private set;}="";
        internal void Request(bool value)
        {
            if(value){if(draining)return;if(!requested)Fault="";requested=true;}
            else {requested=false;if(active)draining=true;}
        }
        internal bool Validate(IReadOnlyList<IWheelLeaseElement> current)
        {
            if(!active||current.Count!=saved.Count)return false;
            foreach(var s in saved)
            {
                if(!s.Element.Attached||s.Element.Binding!=s.Binding||s.Element.Enabled)return false;
                bool found=false;
                foreach(var e in current)if(ReferenceEquals(e.Identity,s.Element.Identity)&&e.Binding==s.Binding){found=true;break;}
                if(!found)return false;
            }
            return true;
        }
        internal void BeforeSource(IReadOnlyList<IWheelLeaseElement> elements,bool valid,bool neutral,int frame)
        {
            if(!valid){Clear();return;}
            if(active&&!Validate(elements)){Clear();Fault="Wheel mappings changed while leased.";return;}
            if(draining){if(neutral)Clear();return;}
            if(!requested)return;
            if(!active)
            {
                if(!neutral)return;
                try
                {
                    foreach(var e in elements)
                    {
                        if(!e.Attached)throw new InvalidOperationException("Wheel mapping detached.");
                        foreach(var s in saved)if(ReferenceEquals(s.Element.Identity,e.Identity))throw new InvalidOperationException("Duplicate wheel mapping.");
                        var item=new Saved(e);saved.Add(item);e.Enabled=false;
                    }
                    active=true;
                }
                catch {Clear();Fault="Could not acquire wheel mappings.";return;}
            }
            boundary=frame;
        }
        internal bool Consume(int frame,float raw,out float delta)
        {
            delta=0;
            if(!active||draining||!requested||boundary!=frame||consumed==frame||float.IsNaN(raw)||float.IsInfinity(raw))return false;
            consumed=frame;delta=raw;return true;
        }
        internal void Clear()
        {
            foreach(var s in saved)
            {
                try {if(s.Element.Attached&&s.Element.Binding==s.Binding&&!s.Element.Enabled)s.Element.Enabled=s.Enabled;}
                catch {Fault="A wheel mapping could not be restored.";}
            }
            saved.Clear();requested=active=draining=false;boundary=consumed=-1;
        }
    }
}
