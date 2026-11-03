using System;
using System.Collections.Generic;
namespace NuclearOptionTargetingPod
{
    public static class CockpitOwnerPolicy
    {
        // Aircraft damage markers and cockpit presentation components are different objects.
        public static T? Unique<T>(IEnumerable<T> candidates,object aircraft,Func<T,object?> owner,Func<T,bool> initialized,int limit=64) where T:class
        {
            T? result=null;int count=0;
            foreach(var candidate in candidates)
            {
                if(++count>limit)return null;
                if(candidate==null||!initialized(candidate)||!ReferenceEquals(owner(candidate),aircraft))continue;
                if(result!=null)return null;
                result=candidate;
            }
            return result;
        }
    }
}
