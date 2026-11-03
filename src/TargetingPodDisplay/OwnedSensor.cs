#nullable disable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
namespace NuclearOptionTargetingPod
{
    internal sealed class OwnedSensor : IDisposable
    {
        private GameObject root;
        private Camera camera;
        private UniversalAdditionalCameraData data;
        internal RenderTexture Texture {get;private set;}
        internal readonly SensorPolicy View=new SensorPolicy();
        private readonly UniversalRenderPipeline.SingleCameraRequest request=new UniversalRenderPipeline.SingleCameraRequest();
        private Vector3? inertialDirection;
        private bool ir,fogHeld,previousFog,direct;
        private readonly Func<bool> eligible;
        private readonly Action lost;
        internal bool Stabilized=>inertialDirection.HasValue;
        internal float Fov=>camera.fieldOfView;
        internal OwnedSensor(PodContext context,Func<bool> eligible,Action lost)
        {
            this.eligible=eligible;this.lost=lost;
            try
            {
                root=new GameObject("TargetingPodDisplay.Sensor");camera=root.AddComponent<Camera>();camera.enabled=false;
                camera.nearClipPlane=2;camera.farClipPlane=Mathf.Min(context.Camera.farClipPlane,60000);
                camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=Color.black;
                camera.cullingMask=~(PhysicsLayers.UIMask|PhysicsLayers.HUDMask|PhysicsLayers.CockpitMask|PhysicsLayers.PreviewRenderMask|PhysicsLayers.EditorSelectOnlyMask);
                data=root.AddComponent<UniversalAdditionalCameraData>();data.renderType=CameraRenderType.Base;data.renderShadows=false;data.renderPostProcessing=false;data.requiresDepthTexture=false;data.requiresColorTexture=false;data.allowXRRendering=false;data.antialiasing=AntialiasingMode.None;
                camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);
                request.mipLevel=0;request.face=CubemapFace.Unknown;request.slice=0;
                RenderPipelineManager.beginCameraRendering+=BeginCamera;
                RenderPipelineManager.endCameraRendering+=EndCamera;
                RenderPipelineManager.endFrameRendering+=EndFrame;
            } catch {Dispose();throw;}
        }
        internal void Reset(){View.Reset();inertialDirection=null;}
        internal void Stabilize(){inertialDirection=inertialDirection.HasValue?(Vector3?)null:camera.transform.forward;}
        internal void Slew(float yaw,float pitch)
        {
            if(yaw!=0||pitch!=0)inertialDirection=null;
            if(!View.Slew(yaw,pitch))throw new InvalidOperationException("Invalid sensor movement");
        }
        internal void Prepare(PodContext context,bool manual,GlobalPosition? aftermath=null,float aftermathFov=0)
        {
            var native=context.Camera;
            if(manual||aftermath.HasValue)
            {
                var mount=context.TargetCam.GetCamMount();if(mount==null)throw new InvalidOperationException("Sensor mount unavailable");
                camera.transform.position=mount.position;
                if(aftermath.HasValue)camera.transform.rotation=Look(aftermath.Value.ToLocalPosition()-mount.position);
                else if(inertialDirection.HasValue)camera.transform.rotation=Look(inertialDirection.Value);
                else camera.transform.rotation=context.Aircraft.transform.rotation*Quaternion.Euler(View.Pitch,View.Yaw,0);
                camera.fieldOfView=aftermath.HasValue?aftermathFov:View.Fov;
            }
            else
            {
                camera.transform.SetPositionAndRotation(native.transform.position,native.transform.rotation);
                camera.fieldOfView=native.fieldOfView;
            }
            var position=camera.transform.position;
            if(!SensorPolicy.Finite(position.x)||!SensorPolicy.Finite(position.y)||!SensorPolicy.Finite(position.z)||!SensorPolicy.Finite(camera.fieldOfView)||camera.fieldOfView<.1f||camera.fieldOfView>179)throw new InvalidOperationException("Invalid sensor pose");
            // Own volume stack: copy only read-only settings, never native cameras, UI stacks or profiles.
            var nativeData=native.GetComponent<UniversalAdditionalCameraData>();
            if(nativeData==null)throw new InvalidOperationException("Native sensor volume settings unavailable");
            data.volumeLayerMask=nativeData.volumeLayerMask;
            data.volumeTrigger=nativeData.volumeTrigger!=null?nativeData.volumeTrigger:native.transform;
            data.renderPostProcessing=!manual&&nativeData.renderPostProcessing;
            ir=!manual&&context.TargetCam.UsingIR();
            camera.depth=context.View.mainCamera.depth+1;
            camera.targetDisplay=context.View.mainCamera.targetDisplay;
            camera.aspect=direct?(float)Screen.width/Mathf.Max(1,Screen.height):1;
        }
        private static Quaternion Look(Vector3 direction)
        {
            if(direction.sqrMagnitude<.0001f)throw new InvalidOperationException("Sensor direction unavailable");
            direction.Normalize();return Quaternion.LookRotation(direction,Mathf.Abs(Vector3.Dot(direction,Vector3.up))>.99f?Vector3.forward:Vector3.up);
        }
        internal void SetDirect(bool enabled)
        {
            direct=enabled;
            if(!enabled){camera.enabled=false;RestoreFog();return;}
            camera.targetTexture=null;camera.rect=new Rect(0,0,1,1);
            camera.aspect=(float)Screen.width/Mathf.Max(1,Screen.height);camera.enabled=true;
        }
        internal void Render(int width,int height)
        {
            if(direct)throw new InvalidOperationException("Offscreen pass forbidden during direct-screen rendering");
            if(Texture==null||Texture.width!=width||Texture.height!=height)
            {
                ReleaseTexture();Texture=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32){name="TargetingPodDisplay.SensorOutput",useMipMap=false,autoGenerateMips=false};
                if(!Texture.Create())throw new InvalidOperationException("Sensor output allocation failed");
                request.destination=Texture;
            }
            camera.aspect=(float)width/height;
            if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Sensor render requests unsupported");
            try {RenderPipeline.SubmitRenderRequest(camera,request);} finally {RestoreFog();}
        }
        private void BeginCamera(ScriptableRenderContext unused,Camera rendering)
        {
            if(!ReferenceEquals(rendering,camera))return;
            bool valid;try {valid=eligible();}catch {valid=false;}
            if(!valid){camera.enabled=false;RestoreFog();lost();return;}
            RestoreFog();previousFog=RenderSettings.fog;fogHeld=true;RenderSettings.fog=!ir;
        }
        private void EndCamera(ScriptableRenderContext unused,Camera rendering){if(ReferenceEquals(rendering,camera))RestoreFog();}
        private void EndFrame(ScriptableRenderContext unused,Camera[] cameras)=>RestoreFog();
        private void RestoreFog(){if(!fogHeld)return;fogHeld=false;RenderSettings.fog=previousFog;}
        private void ReleaseTexture(){request.destination=null;var texture=Texture;Texture=null;if(texture!=null){texture.Release();Object.Destroy(texture);}}
        public void Dispose()
        {
            if(camera!=null)camera.enabled=false;RestoreFog();
            RenderPipelineManager.beginCameraRendering-=BeginCamera;RenderPipelineManager.endCameraRendering-=EndCamera;RenderPipelineManager.endFrameRendering-=EndFrame;
            ReleaseTexture();if(root!=null)Object.Destroy(root);root=null;camera=null;data=null;
        }
    }
}
