#nullable disable
using System;
using System.Reflection;
using BepInEx.Bootstrap;
namespace NuclearOptionTargetingPod
{
    internal static class ObserverArbitration
    {
        private const string Id="io.github.nuclearoption.observer";
        private static Assembly verified;
        private static bool ReadActive(object owner,string field,string property)
        {
            var value=owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)?.GetValue(owner);
            // Null means that optional component has not been created, not an unknown property on a live component.
            if(value==null)return false;
            var prop=value.GetType().GetProperty(property,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            if(prop==null||prop.PropertyType!=typeof(bool))throw new MissingMemberException(property);
            return (bool)prop.GetValue(value);
        }
        internal static bool Available()
        {
            try
            {
                if(!Chainloader.PluginInfos.TryGetValue(Id,out var info))return !PodContext.ObserverLoaded();
                var plugin=info.Instance;if(plugin==null)return false;
                var assembly=plugin.GetType().Assembly;
                if(!ReferenceEquals(verified,assembly))
                {
                    if(plugin.GetType().FullName!="NuclearOptionObserver.Plugin"||info.Metadata.Version.ToString()!="0.5.1"||FingerprintPolicy.HashFile(assembly.Location)!="cb1a36f7a505375636468e58e4c806c29c42e3020394dc36e2327e517a06514e")return false;
                    foreach(var field in new[]{"fullscreen","fullscreenInput","flightHold","modal"})if(plugin.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)==null)return false;
                    verified=assembly;
                }
                return LeasePolicy.CanOwn(true,true,ReadActive(plugin,"fullscreen","Active"),ReadActive(plugin,"fullscreenInput","Draining"),false /* dedicated keys never write flight axes; Observer hold may coexist */,ReadActive(plugin,"modal","IsOpen"));
            }
            catch{return false;}
        }
    }
}
