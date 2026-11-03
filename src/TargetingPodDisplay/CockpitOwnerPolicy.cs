using System;
using System.Collections.Generic;
namespace NuclearOptionTargetingPod
{
    public static class CockpitOwnerPolicy
    {
        public static bool CanUseCandidate(bool hasContainingPart,bool containingPartValid,bool inAircraftRoot) => hasContainingPart?containingPartValid:inAircraftRoot;
        public static bool CanUseRegisteredPart(bool sameOwner,bool detached,bool sceneLoaded,bool sameScene) => sameOwner&&!detached&&sceneLoaded&&sameScene;
        public static bool AttachedToRegistry<TCandidate,TPart>(TCandidate candidate,ICollection<TPart> registry,Func<TCandidate,TPart?> containingPart,Func<TPart,bool> validPart,Func<TCandidate,bool> inMainRoot) where TPart:class
        {
            var part=containingPart(candidate);
            return part!=null?registry.Contains(part)&&validPart(part):inMainRoot(candidate);
        }
        // Aircraft damage markers and cockpit presentation components are different objects.
        public static T? Unique<T>(IEnumerable<T> candidates,object aircraft,Func<T,object?> owner,Func<T,bool> initialized,int limit=64,Func<object?,object?,bool>? sameOwner=null) where T:class
        {
            T? result=null;int count=0;var seen=new HashSet<T>();
            sameOwner??=ReferenceEquals;
            foreach(var candidate in candidates)
            {
                if(++count>limit)return null;
                if(candidate==null||!seen.Add(candidate)||!initialized(candidate)||!sameOwner(owner(candidate),aircraft))continue;
                if(result!=null)return null;
                result=candidate;
            }
            return result;
        }
    }
}
