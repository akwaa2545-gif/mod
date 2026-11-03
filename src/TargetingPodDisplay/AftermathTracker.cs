#nullable disable
using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NuclearOptionTargetingPod
{
    // Read-only notification hook. The plugin must retain one instance per live owner context,
    // call Clear on manual/selection/context changes, and Dispose when that context ends.
    internal sealed class AftermathTracker : IDisposable
    {
        private static AftermathTracker current;
        private readonly Func<bool> ownerValid;
        private readonly Func<double> duration;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly AftermathPolicy policy = new AftermathPolicy();
        private readonly Harmony harmony;
        private Unit watched;
        private PersistentID watchedId, consumedId;
        private int watchedInstance, consumedInstance, consumedScene;
        private bool haveConsumed;
        private int watchedScene;
        private long generation;
        private bool disposed;
        internal string Fault { get; private set; }
        // A pending notification permits keeping this tracker, never displaying an aftermath image.
        internal bool Pending
        {
            get
            {
                if(!Eligible())return false;
                policy.TryGet(Time.unscaledTimeAsDouble,Time.frameCount,out _,out _);
                if(policy.WatchedToken==0)DropWatched();
                return policy.WatchedToken!=0;
            }
        }
        internal AftermathTracker(Func<bool> ownerValid, Func<double> duration)
        {
            this.ownerValid=ownerValid??throw new ArgumentNullException(nameof(ownerValid));
            this.duration=duration??throw new ArgumentNullException(nameof(duration));
            if(current!=null)throw new InvalidOperationException("Aftermath notification owner already exists.");
            if(typeof(MessageManager).Module.ModuleVersionId!=new Guid("9e552383-bd56-40dc-8fe7-51f1744515e4"))throw new NotSupportedException("Aftermath game contract mismatch.");
            var method=typeof(MessageManager).GetMethod("UserCode_RpcKillMessage_635947223",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly,null,new[]{typeof(PersistentID),typeof(PersistentID),typeof(KillType)},null);
            if(method==null||method.IsStatic||method.ReturnType!=typeof(void))throw new MissingMethodException("Native kill notification contract missing.");
            harmony=new Harmony("local.nuclearoption.targetingpoddisplay.aftermath");
            try{harmony.Patch(method,postfix:new HarmonyMethod(typeof(AftermathTracker),nameof(AfterKillNotification)));current=this;}
            catch{harmony.UnpatchSelf();throw;}
        }
        private bool Eligible()
        {
            if(disposed||Fault!=null||Thread.CurrentThread.ManagedThreadId!=thread)return false;
            try{if(ownerValid())return true;Clear();return false;}
            catch(Exception ex){Fail(ex);return false;}
        }
        internal void Observe(Unit target,GlobalPosition knownPosition,float fov,int scene)
        {
            if(!Eligible())return;
            try
            {
                if(scene!=SceneManager.GetActiveScene().handle){Clear();return;}
                if(target==null||target.disabled||!target.isActiveAndEnabled)
                {
                    policy.NoSelection(Time.frameCount);
                    if(!policy.TryGet(Time.unscaledTimeAsDouble,Time.frameCount,out _,out _)&&policy.WatchedToken==0)haveConsumed=false;
                    if(policy.WatchedToken==0)DropWatched();
                    return;
                }
                if(haveConsumed&&consumedId==target.persistentID&&consumedScene==scene&&consumedInstance==target.GetInstanceID())return;
                haveConsumed=false;
                if(!target.persistentID.IsValid||target.gameObject.scene.handle!=scene){Clear();return;}
                if(!ReferenceEquals(watched,target)||watchedId!=target.persistentID||watchedScene!=scene)
                {
                    if(generation==long.MaxValue)throw new InvalidOperationException("Aftermath lifetime exhausted.");
                    generation++;watchedInstance=target.GetInstanceID();watched=target;watchedId=target.persistentID;watchedScene=scene;
                }
                policy.Observe(generation,scene,new AftermathPoint(knownPosition.x,knownPosition.y,knownPosition.z),fov,Time.unscaledTimeAsDouble,Time.frameCount);
                if(policy.WatchedToken==0)DropWatched();
            }
            catch(Exception ex){Fail(ex);}
        }
        internal bool TryGet(double now,out GlobalPosition point,out float fov)
        {
            point=default;fov=0;if(!Eligible())return false;
            if(!policy.TryGet(now,Time.frameCount,out var value,out fov)){if(policy.WatchedToken==0)DropWatched();return false;}
            point=new GlobalPosition((float)value.X,(float)value.Y,(float)value.Z);return true;
        }
        private static void AfterKillNotification(MessageManager __instance,PersistentID killedID)
        {
            var tracker=current;
            if(tracker==null||!tracker.Eligible())return;
            try
            {
                if(!ReferenceEquals(__instance,NetworkSceneSingleton<MessageManager>.i)||ReferenceEquals(tracker.watched,null)||tracker.watchedId!=killedID||tracker.watchedScene!=SceneManager.GetActiveScene().handle)return;
                // A registered replacement with the same persistent ID is a different lifetime.
                if(UnitRegistry.TryGetUnit(killedID,out var registered)&&!ReferenceEquals(registered,tracker.watched))return;
                if(tracker.policy.Notify(tracker.generation,tracker.watchedScene,Time.unscaledTimeAsDouble,Time.frameCount,tracker.duration()))
                {
                    tracker.haveConsumed=true;tracker.consumedId=tracker.watchedId;tracker.consumedScene=tracker.watchedScene;tracker.consumedInstance=tracker.watchedInstance;
                    tracker.DropWatched();
                }
                else if(tracker.policy.WatchedToken==0)tracker.DropWatched();
            }
            catch(Exception ex){tracker.Fail(ex);}
        }
        private void DropWatched(){watched=null;watchedId=default;watchedScene=watchedInstance=0;}
        internal void Clear(){policy.Clear();DropWatched();haveConsumed=false;consumedId=default;consumedInstance=consumedScene=0;}
        private void Fail(Exception ex){Fault="Aftermath unavailable: "+ex.GetType().Name;Clear();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;if(ReferenceEquals(current,this))current=null;
            Clear();harmony.UnpatchSelf();
        }
    }
}
