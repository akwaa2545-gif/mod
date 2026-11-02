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
        internal RenderTexture Texture {get;private set;}
        internal readonly SensorPolicy View=new SensorPolicy();
        private readonly UniversalRenderPipeline.SingleCameraRequest request=new UniversalRenderPipeline.SingleCameraRequest();
        private Vector3? inertialDirection;
        internal bool Stabilized => inertialDirection.HasValue;
        internal OwnedSensor(PodContext context)
        {
            try
            {
            root=new GameObject("TargetingPodDisplay.Sensor");camera=root.AddComponent<Camera>();camera.enabled=false;
            camera.nearClipPlane=2;camera.farClipPlane=Mathf.Min(context.Camera.farClipPlane,60000);
            camera.cullingMask=~(PhysicsLayers.UIMask|PhysicsLayers.HUDMask|PhysicsLayers.CockpitMask|PhysicsLayers.PreviewRenderMask|PhysicsLayers.EditorSelectOnlyMask);
            var data=root.AddComponent<UniversalAdditionalCameraData>();data.renderType=CameraRenderType.Base;data.renderShadows=false;data.renderPostProcessing=false;data.requiresDepthTexture=false;data.requiresColorTexture=false;data.allowXRRendering=false;data.antialiasing=AntialiasingMode.None;
            Texture=new RenderTexture(1024,1024,16,RenderTextureFormat.ARGB32){name="TargetingPodDisplay.SensorOutput",useMipMap=false,autoGenerateMips=false};
            if(!Texture.Create())throw new InvalidOperationException("Sensor output allocation failed");
            request.destination=Texture;request.mipLevel=0;request.face=CubemapFace.Unknown;request.slice=0;
            } catch { Dispose();throw; }
        }
        internal void Reset(){View.Reset();inertialDirection=null;}
        internal void Stabilize(){inertialDirection=inertialDirection.HasValue?(Vector3?)null:camera.transform.forward;}
        internal void Slew(float yaw,float pitch)
        {
            // Inertial stabilization is direction hold, NOT ground designation or weapon tracking.
            if(yaw!=0||pitch!=0)inertialDirection=null;
            if(!View.Slew(yaw,pitch))throw new InvalidOperationException("Invalid sensor movement");
        }
        internal void Render(PodContext context)
        {
            var mount=context.TargetCam.GetCamMount();if(mount==null)throw new InvalidOperationException("Sensor mount unavailable");
            var position=mount.position;if(!SensorPolicy.Finite(position.x)||!SensorPolicy.Finite(position.y)||!SensorPolicy.Finite(position.z))throw new InvalidOperationException("Invalid sensor position");
            camera.transform.position=position;
            if(inertialDirection.HasValue){var direction=inertialDirection.Value;camera.transform.rotation=Quaternion.LookRotation(direction,Mathf.Abs(Vector3.Dot(direction,Vector3.up))>.99f?Vector3.forward:Vector3.up);}
            else camera.transform.rotation=context.Aircraft.transform.rotation*Quaternion.Euler(View.Pitch,View.Yaw,0);
            camera.fieldOfView=View.Fov;camera.aspect=1;
            if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Sensor render requests unsupported");
            RenderPipeline.SubmitRenderRequest(camera,request);
        }
        public void Dispose(){request.destination=null;if(root!=null)Object.Destroy(root);root=null;camera=null;var texture=Texture;Texture=null;if(texture!=null){texture.Release();Object.Destroy(texture);}}
    }
}
