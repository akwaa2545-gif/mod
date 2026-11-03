#nullable disable
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace NuclearOptionTargetingPod
{
    internal static class SensorRenderContract
    {
        internal static string Apply(Camera source,Camera target,UniversalAdditionalCameraData native,UniversalAdditionalCameraData owned,int excluded)
        {
            if(source==null||target==null||native==null||owned==null||ReferenceEquals(source,target)||ReferenceEquals(native,owned)||native.renderType!=CameraRenderType.Base)throw new InvalidOperationException("Native base sensor render contract unavailable");
            int index=NativeContract.Read<int>(native,"m_RendererIndex");
            if(index< -1||index>31)throw new InvalidOperationException("Unsupported native sensor renderer index");
            owned.SetRenderer(index);
            if(native.scriptableRenderer==null||!ReferenceEquals(owned.scriptableRenderer,native.scriptableRenderer))throw new InvalidOperationException("Owned sensor renderer does not match native renderer");
            target.cullingMask=source.cullingMask&~excluded;
            owned.requiresDepthTexture=native.requiresDepthTexture;
            owned.requiresColorTexture=native.requiresColorTexture;
            owned.renderShadows=native.renderShadows;
            target.allowHDR=source.allowHDR;target.allowMSAA=source.allowMSAA;
            target.useOcclusionCulling=source.useOcclusionCulling;
            target.nearClipPlane=source.nearClipPlane;target.farClipPlane=source.farClipPlane;
            target.clearFlags=source.clearFlags;target.backgroundColor=source.backgroundColor;
            var texture=source.targetTexture;
            string renderer=native.scriptableRenderer.GetType().Name+"#"+RuntimeHelpers.GetHashCode(native.scriptableRenderer);
            return "Sensor render parity: renderer="+index+" "+renderer+"; native/owned mask="+source.cullingMask.ToString("X8")+"/"+target.cullingMask.ToString("X8")+
                "; depth/color="+owned.requiresDepthTexture+"/"+owned.requiresColorTexture+"; HDR/MSAA="+target.allowHDR+"/"+target.allowMSAA+
                "; native RT="+(texture==null?"none":texture.format+" samples="+texture.antiAliasing)+". Partial-magenta correction requires runtime verification.";
        }
    }
}
