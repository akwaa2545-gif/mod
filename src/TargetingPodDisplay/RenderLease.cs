using System;
namespace NuclearOptionTargetingPod
{
    // Camera.enabled only. No controller, GameObject, simulation or input suppression.
    internal sealed class RenderLease<T> where T:class
    {
        private readonly T main,cockpit;
        private readonly Func<T,bool> read;
        private readonly Action<T,bool> write;
        private readonly bool priorMain,priorCockpit;
        private bool active;
        internal RenderLease(T main,T cockpit,Func<T,bool> read,Action<T,bool> write)
        {
            this.main=main;this.cockpit=cockpit;this.read=read;this.write=write;
            priorMain=read(main);priorCockpit=read(cockpit);active=true;
            try {write(main,false);write(cockpit,false);} catch {Release(true);throw;}
        }
        internal bool Owns(T candidate)=>active&&ReferenceEquals(candidate,main)&&!read(main)&&!read(cockpit);
        internal void Release(bool sameCameraState)
        {
            if(!active)return;active=false;
            try {if(!read(main))write(main,priorMain);}
            finally {if(sameCameraState&&!read(cockpit))write(cockpit,priorCockpit);}
        }
    }
}
