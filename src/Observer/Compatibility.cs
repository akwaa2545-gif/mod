using System;
using System.IO;
using System.Reflection;
using BepInEx;

namespace NuclearOptionObserver
{
    internal static class Compatibility
    {
        // Original files stay hash-pinned. Only CoreModule's *live* MVID/Location can reflect the normal loader rewrite.
        internal static bool Check(out string reason)
        {
            try
            {
                var loader = Observe(typeof(BaseUnityPlugin).Assembly);
                var harmony = Observe(typeof(HarmonyLib.Harmony).Assembly);
                string loaderHash = FingerprintPolicy.HashFile(Path.Combine(Paths.BepInExAssemblyDirectory, "BepInEx.dll"));
                string harmonyHash = FingerprintPolicy.HashFile(Path.Combine(Paths.BepInExAssemblyDirectory, "0Harmony.dll"));
                if (!FingerprintPolicy.IsSupportedLoader(loader, loaderHash, harmony, harmonyHash))
                {
                    reason = "Unsupported loader/Harmony identity (only official BepInEx 5.4.23.5 or 5.4.23.6 + bundled Harmony 2.9.0.0 supported): BepInEx=" + loader.Version +
                        "; MVID=" + loader.Mvid + "; disk SHA-256=" + loaderHash + "; Harmony=" + harmony.Version + "; MVID=" + harmony.Mvid + "; disk SHA-256=" + harmonyHash;
                    return false;
                }
                if (!CheckGame(typeof(GameManager).Assembly, "Assembly-CSharp.dll", "babdd626bbab5f01e6c244150c1e77c57c131ee86967214e2e5ced6d813c2bce", "9e552383-bd56-40dc-8fe7-51f1744515e4", out reason)) return false;
                if (!CheckGame(typeof(Mirage.NetworkManager).Assembly, "Mirage.dll", "cd355251ef8cddf4d1ff42e70719974ff4c493075e49dd7d27d5c180436aaac0", "7cd9168e-2e77-447c-a91c-7a96ffce56eb", out reason)) return false;
                if (!CheckGame(typeof(UnityEngine.Component).Assembly, "UnityEngine.CoreModule.dll", "c6eab21b88e3ab42342957d55d264426f86dcda8d8db0b45ac1e513dc504bef3", "58f37dbd-41b6-4d47-a3d6-9ea4d7efae27", out reason)) return false;
                string coreObservation = reason;
                if (!CheckGame(typeof(UnityEngine.GUI).Assembly, "UnityEngine.IMGUIModule.dll", "4a41ecfc47a9e7a05dbca49c8512247786cbd1fe9debc1527bffbcf5106a6e23", "ed26b156-d900-441a-9572-9bae0b0fe919", out reason)) return false;
                if (!CheckGame(typeof(UnityEngine.UI.Image).Assembly, "UnityEngine.UI.dll", "73b13688ff1e87652a6111f5a11896542b54b4571617b52f52c8befb05bf8ca7", "c0b31eab-54e5-463f-979f-814c9f4db353", out reason)) return false;
                if (!CheckGame(typeof(UnityEngine.Canvas).Assembly, "UnityEngine.UIModule.dll", "c1d8ba535fca52d62964217f37ff1b174a0961109e54ca1c86cb7190b67e48ed", "c2365778-407b-43e6-8eb0-ccc483459e27", out reason)) return false;
                if (!CheckGame(typeof(UnityEngine.Input).Assembly, "UnityEngine.InputLegacyModule.dll", "74ef5ddfdbf9e65b8f8bad574c4e03dfc19d3c1c8535f859417f42b19d845f84", "316d27ec-0671-4d02-ac4d-4c561358518a", out reason)) return false;
                if (!CheckGame(typeof(UnityEngine.Rendering.Volume).Assembly, "Unity.RenderPipelines.Core.Runtime.dll", "029760dec74e58a8a22215991b79545782a65e6f42c7b46a04813efc4a34dc97", "5604da0d-385e-47dd-938a-8d2a706614c7", out reason)) return false;
                if (!CheckGame(typeof(UnityEngine.Rendering.Universal.UniversalRenderPipeline).Assembly, "Unity.RenderPipelines.Universal.Runtime.dll", "5113af9eb22fb849ec80ac250b548b21c18f84e3d6b3635bdf0bd35327d8ea36", "61aec7ce-08c5-40a1-b85d-1e4822746361", out reason)) return false;
                reason = "Exact original Managed inputs and supported loader " + loader.Version + " verified. " + coreObservation + ". Runtime rendering still requires retest.";
                return true;
            }
            catch (Exception ex) { reason = "Cannot validate original-file/runtime identities: " + ex.GetType().Name + ": " + ex.Message; return false; }
        }
        internal static bool CheckFullscreen(out string reason)
        {
            try { return CheckGame(typeof(Rewired.Player).Assembly, "Rewired_Core.dll", "3ae8897b0a184775ddcf6af6cf55413c3fe015a7c752f8df1bc0e05a088640d4", "ec2e4f47-e80e-4a29-8862-b856198ed94b", out reason); }
            catch (Exception ex) { reason = "Fullscreen input identity unavailable: " + ex.Message; return false; }
        }
        internal static bool CheckFlightHold(out string reason)
        {
            if (!CheckFullscreen(out reason)) return false;
            try { return CheckGame(typeof(UnityEngine.Physics).Assembly, "UnityEngine.PhysicsModule.dll", "e7c7467a81c3fd9952c694400dbbc17708fc365f7a444bdc00253f02a39b577b", "4ff5618a-f386-4224-bc09-4bea44b37732", out reason); }
            catch (Exception ex) { reason = "Hold physics identity unavailable: " + ex.Message; return false; }
        }
        private static bool CheckGame(Assembly assembly, string filename, string hash, string mvid, out string reason) =>
            FingerprintPolicy.CheckGame(Paths.ManagedPath, filename, hash, new Guid(mvid), Observe(assembly), true, out reason);
        private static RuntimeAssemblyInfo Observe(Assembly assembly)
        {
            string location;
            try { location = assembly.Location; }
            catch (NotSupportedException) { location = ""; } // Location is diagnostic only, never original game input identity.
            var name = assembly.GetName();
            bool loaderReference = false, preloaderReference = false;
            if (name.Name == "UnityEngine.CoreModule")
            {
                var expectedLoaderVersion = typeof(BaseUnityPlugin).Assembly.GetName().Version;
                foreach (var reference in assembly.GetReferencedAssemblies())
                {
                    if (reference.Version != expectedLoaderVersion) continue;
                    if (reference.Name == "BepInEx") loaderReference = true;
                    if (reference.Name == "BepInEx.Preloader") preloaderReference = true;
                }
            }
            return new RuntimeAssemblyInfo(name.Name ?? "", name.Version ?? new Version(0, 0, 0, 0), assembly.ManifestModule.ModuleVersionId, location, loaderReference && preloaderReference);
        }
    }
}
