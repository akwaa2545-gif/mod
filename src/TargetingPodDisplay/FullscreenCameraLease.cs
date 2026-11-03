#nullable disable
using System;
using UnityEngine;
namespace NuclearOptionTargetingPod
{
    internal sealed class FullscreenCameraLease:IDisposable
    {
        private readonly CameraStateManager view;
        private readonly Camera main,cockpit;
        private readonly object state;
        private readonly Action lost;
        private RenderLease<Camera> render;
        internal FullscreenCameraLease(PodContext context,Action lost)
        {
            view=context.View;main=view.mainCamera;cockpit=view.cockpitCamRender;state=view.currentState;this.lost=lost;
            if(main==null||cockpit==null||main==cockpit||!main.isActiveAndEnabled||!ReferenceEquals(state,view.cockpitState))throw new InvalidOperationException("Fullscreen native camera ownership unavailable");
            render=new RenderLease<Camera>(main,cockpit,c=>c==null||c.enabled,(c,v)=>{if(c!=null)c.enabled=v;});
            view.onSwitchCamera+=OnSwitch;
        }
        internal bool Allows(CameraStateManager candidate)=>render!=null&&view!=null&&ReferenceEquals(candidate,view)&&ReferenceEquals(view.mainCamera,main)&&ReferenceEquals(view.cockpitCamRender,cockpit)&&ReferenceEquals(view.currentState,state)&&main!=null&&main.gameObject.activeInHierarchy&&render.Owns(main);
        private void OnSwitch(){Dispose();lost();}
        public void Dispose()
        {
            if(view!=null)view.onSwitchCamera-=OnSwitch;
            var owned=render;render=null;
            owned?.Release(view!=null&&ReferenceEquals(view.currentState,state));
        }
    }
}
