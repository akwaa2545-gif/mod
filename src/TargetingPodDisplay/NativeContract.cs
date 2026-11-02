#nullable disable
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace NuclearOptionTargetingPod
{
    internal static class NativeContract
    {
        internal static T Read<T>(object owner,string field)
        {
            if(owner==null) return default;
            var info=owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            if(info==null||info.IsStatic||info.FieldType!=typeof(T))throw new MissingFieldException(owner.GetType().Name,field);
            return (T)info.GetValue(owner);
        }
        internal static bool IsTargetMode(TargetCam cam) => Read<TargetCam.CamMode>(cam,"currentMode")!=TargetCam.CamMode.landingMode;
        internal static RawImage FindConsumer(PodContext context,RenderTexture source,out string reason,RawImage ownedImage=null,Texture ownedTexture=null)
        {
            reason="No proven unique target RawImage; native cockpit retained.";
            var cockpit=context.Aircraft.cockpit;
            var tac=Read<TacScreen>(cockpit,"tacScreen");
            if(tac==null)return null;
            var target=Read<GameObject>(tac,"targetCamDisplay"); var landing=Read<GameObject>(tac,"landingCamDisplay");
            if(target==null||landing==null)return null;
            if(!DisplayPolicy.CanBindHierarchy(tac.transform.IsChildOf(cockpit.transform),target.transform.IsChildOf(tac.transform),landing.transform.IsChildOf(tac.transform),!ReferenceEquals(target,landing),target.transform.IsChildOf(landing.transform),landing.transform.IsChildOf(target.transform),target.GetComponentsInChildren<Renderer>(true).Length))return null;
            // No material guessing: support only an exact output identity on a dedicated RawImage.
            var images=target.GetComponentsInChildren<RawImage>(true);
            if(images.Length>64)return null;
            var matches=new bool[images.Length];
            for(int i=0;i<images.Length;i++)matches[i]=ReferenceEquals(images[i].texture,source)||(ownedTexture!=null&&ReferenceEquals(images[i].texture,ownedTexture));
            int index=DisplayPolicy.Unique(matches);
            if(index<0)return null;
            if(ownedImage!=null&&!ReferenceEquals(images[index],ownedImage))return null;
            // Separate landing/radar consumers may share the immutable native texture.
            // Only the unique target descendant's texture property is replaced.
            // Matching renderer textures cannot be ruled out cheaply without enumerating shared assets: fail closed if target has renderers.

            reason="Unique cockpit target RawImage verified."; return images[index];
        }
    }
    internal sealed class CockpitBinding : IDisposable
    {
        private readonly RawImage image;
        private readonly TacScreen tac;
        private readonly GameObject targetRoot,landingRoot;
        private readonly Texture original,owned;
        private GameObject display;
        private TargetCam targetCam;
        private bool nativeActive;
        private bool activationOwned;
        internal CockpitBinding(PodContext context,RawImage image,Texture owned)
        {
            this.image=image;original=image.texture;this.owned=owned;
            tac=NativeContract.Read<TacScreen>(context.Aircraft.cockpit,"tacScreen");targetRoot=NativeContract.Read<GameObject>(tac,"targetCamDisplay");landingRoot=NativeContract.Read<GameObject>(tac,"landingCamDisplay");
            image.texture=owned;
        }
        internal bool IsCurrent(PodContext context) => Held&&tac!=null&&targetRoot!=null&&landingRoot!=null&&
            ReferenceEquals(NativeContract.Read<TacScreen>(context.Aircraft.cockpit,"tacScreen"),tac)&&
            ReferenceEquals(NativeContract.Read<GameObject>(tac,"targetCamDisplay"),targetRoot)&&
            ReferenceEquals(NativeContract.Read<GameObject>(tac,"landingCamDisplay"),landingRoot)&&
            ReferenceEquals(context.Camera.targetTexture,original)&&
            ReferenceEquals(NativeContract.FindConsumer(context,(RenderTexture)original,out _,image,owned),image);

        internal void MaintainActivation(PodContext context,bool manual)
        {
            if(!manual){ReleaseActivation();return;}
            if(display==null)
            {
                var tac=NativeContract.Read<TacScreen>(context.Aircraft.cockpit,"tacScreen");
                display=NativeContract.Read<GameObject>(tac,"targetCamDisplay");
                if(display==null||image==null||!image.transform.IsChildOf(display.transform))throw new InvalidOperationException("Target activation proof lost");
                nativeActive=display.activeSelf;targetCam=context.TargetCam;targetCam.onCamToggle+=NativeToggle;
            }
            if(!NativeContract.IsTargetMode(context.TargetCam)){ReleaseActivation();return;}
            if(!display.activeSelf){display.SetActive(true);activationOwned=true;}
        }
        private void NativeToggle(TargetCam.OnCamToggle change)
        {
            if(change.camMode==TargetCam.CamMode.landingMode){ReleaseActivation();return;}
            nativeActive=change.enabled;
        }
        private void ReleaseActivation()
        {
            if(targetCam!=null)targetCam.onCamToggle-=NativeToggle;
            // Restore latest native event intent, not stale entry state. Never touch landing/radar/time.
            if(display!=null&&activationOwned&&display.activeSelf)display.SetActive(nativeActive);
            display=null;targetCam=null;activationOwned=false;
        }
        internal bool Held => image!=null&&DisplayPolicy.MayRestore(image.texture,owned);
        public void Dispose(){ReleaseActivation();if(Held)image.texture=original;}
    }
}
