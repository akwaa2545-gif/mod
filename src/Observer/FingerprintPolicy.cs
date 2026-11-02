using System;
using System.IO;
using System.Security.Cryptography;

namespace NuclearOptionObserver
{
    public readonly struct RuntimeAssemblyInfo
    {
        public RuntimeAssemblyInfo(string name, Version version, Guid mvid, string location, bool hasLoaderEntrypointReferences = false)
        { Name = name; Version = version; Mvid = mvid; Location = location; HasLoaderEntrypointReferences = hasLoaderEntrypointReferences; }
        public string Name { get; }
        public Version Version { get; }
        public Guid Mvid { get; }
        public string Location { get; }
        public bool HasLoaderEntrypointReferences { get; }
    }
    // Pure file/metadata policy. Original game files are never located through a patched Assembly.Location.
    public static class FingerprintPolicy
    {
        public static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static bool IsSupportedLoader(RuntimeAssemblyInfo loader, string loaderHash, RuntimeAssemblyInfo harmony, string harmonyHash)
        {
            if (loader.Name != "BepInEx" || harmony.Name != "0Harmony" || harmony.Version != new Version(2, 9, 0, 0) ||
                harmony.Mvid != new Guid("474744d6-5d8e-460f-a08c-d5fd82b5d65f") ||
                harmonyHash != "1a21cc03424fc82c3dd1346905d16494536b9595ae4162228d99fb7c285c1031") return false;
            return (loader.Version == new Version(5, 4, 23, 5) && loader.Mvid == new Guid("d1b92069-86c2-41dc-ad96-bb21eee55a97") &&
                    loaderHash == "8255b28902886085c578b9e427d3073c97002db85176d2090cdeda90ef14ce70") ||
                   (loader.Version == new Version(5, 4, 23, 6) && loader.Mvid == new Guid("83313e36-e388-498e-a597-9931cf0d08bb") &&
                    loaderHash == "780f175821781c14fdc349bf214603b7bab594ac556aecb1f6679b80248133f0");
        }
        public static bool CheckGame(string managedDirectory, string filename, string expectedHash, Guid expectedMvid, RuntimeAssemblyInfo runtime, bool supportedLoader, out string reason)
        {
            reason = "unsupported loader identity";
            if (!supportedLoader) return false;
            if (Path.GetFileName(filename) != filename || runtime.Name != Path.GetFileNameWithoutExtension(filename) || runtime.Mvid == Guid.Empty)
            { reason = "invalid live assembly identity for " + filename + "; name=" + runtime.Name + "; MVID=" + runtime.Mvid; return false; }
            try
            {
                string actualHash = HashFile(Path.Combine(managedDirectory, filename));
                if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                {
                    reason = "Original Managed file SHA-256 mismatch: " + filename + "; expected=" + expectedHash + "; actual=" + actualHash + "; live MVID=" + runtime.Mvid;
                    return false;
                }
                // Official BepInEx injects Chainloader into this one Unity assembly and loads rewritten bytes.
                // Its live MVID/Location are not the identity of the original disk input. No other DLL is exempt.
                bool loaderPatchedCore = filename == "UnityEngine.CoreModule.dll";
                if (runtime.Mvid != expectedMvid && (!loaderPatchedCore || !runtime.HasLoaderEntrypointReferences))
                { reason = "Live MVID mismatch: " + filename + "; expected=" + expectedMvid + "; actual=" + runtime.Mvid + "; original disk hash matches"; return false; }
                reason = filename + ": original Managed SHA-256 verified; live MVID=" + runtime.Mvid +
                    (loaderPatchedCore ? "; CoreModule loader entrypoint references=" + runtime.HasLoaderEntrypointReferences + "; Location=" + (string.IsNullOrEmpty(runtime.Location) ? "memory" : "file/dump/remapped") + " (not used for original-file lookup)" : "; live MVID matches");
                return true;
            }
            catch (Exception ex) { reason = "Cannot verify original Managed file " + filename + ": " + ex.GetType().Name + ": " + ex.Message; return false; }
        }
    }
    // At most one rejection message per ten seconds, including when the reason changes each frame.
    public sealed class ReasonThrottle
    {
        private double next = double.NegativeInfinity;
        public bool ShouldLog(string reason, double now)
        {
            if (string.IsNullOrEmpty(reason) || double.IsNaN(now) || double.IsInfinity(now) || now < next) return false;
            next = now + 10;
            return true;
        }
    }
}
