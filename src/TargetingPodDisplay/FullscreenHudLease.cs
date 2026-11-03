#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;
namespace NuclearOptionTargetingPod
{
    // Rendering only: native HUD scripts, graphic enabled flags and weapon advice keep updating.
    internal sealed class FullscreenHudLease:IDisposable
    {
        private readonly FlightHud hud;
        private readonly CombatHUD combat;
        private readonly Aircraft aircraft;
        private readonly Canvas root;
        private readonly List<Canvas> canvases=new List<Canvas>();
        private readonly List<bool> prior=new List<bool>();
        private bool disposed;
        private FullscreenHudLease(FlightHud hud,CombatHUD combat,Aircraft aircraft,Canvas root)
        {
            this.hud=hud;this.combat=combat;this.aircraft=aircraft;this.root=root;
            var candidates=root.GetComponentsInChildren<Canvas>(true);
            if(candidates.Length==0||candidates.Length>32)throw new InvalidOperationException("HUD canvas count unsupported");
            try {foreach(var canvas in candidates){canvases.Add(canvas);prior.Add(canvas.enabled);canvas.enabled=false;}}
            catch {Dispose();throw;}
        }
        internal static FullscreenHudLease TryAcquire(PodContext context,out string reason)
        {
            reason="Fullscreen HUD rendering unavailable";
            var hud=SceneSingleton<FlightHud>.i;var combat=SceneSingleton<CombatHUD>.i;
            if(hud==null||combat==null||combat.aircraft!=context.Aircraft||NativeContract.Read<Aircraft>(hud,"aircraft")!=context.Aircraft){reason+=" (local aircraft owner)";return null;}
            var root=NativeContract.Read<Canvas>(hud,"canvas");
            if(!Proof(hud,combat,root,out reason))return null;
            return new FullscreenHudLease(hud,combat,context.Aircraft,root);
        }
        private static bool Below(Transform item,Transform parent)=>item!=null&&parent!=null&&(item==parent||item.IsChildOf(parent));
        private static bool Proof(FlightHud hud,CombatHUD combat,Canvas canvas,out string reason)
        {
            reason="Fullscreen HUD rendering unavailable: ";
            if(canvas==null||!canvas.gameObject.scene.isLoaded){reason+="canvas missing";return false;}
            var top=NativeContract.Read<GameObject>(combat,"topRightPanel");
            var map=SceneSingleton<DynamicMap>.i;
            if(!Below(hud.GetHUDCenter(),canvas.transform)||!Below(hud.HMDCenter,canvas.transform)||!Below(combat.iconLayer,canvas.transform)||top==null||!Below(top.transform,canvas.transform))
            {reason+="native canvas does not uniquely contain flight/weapon/target graphics";return false;}
            if(map==null||DynamicMap.mapMaximized||!Below(map.hudMapAnchor,canvas.transform)||!Below(map.transform,map.hudMapAnchor)||map.maximizedMapCanvas==null||Below(map.maximizedMapCanvas.transform,canvas.transform))
            {reason+="minimized/maximized map ownership not separated";return false;}
            var scripts=canvas.GetComponentsInChildren<MonoBehaviour>(true);
            if(scripts.Length>2048){reason+="HUD subtree exceeds bound";return false;}
            foreach(var script in scripts)
            {
                if(script==null)continue;
                string name=script.GetType().Name;
                if(name=="ChatBox"||name.Contains("PauseMenu")||name.Contains("MainMenu")||name.Contains("OptionsMenu")||name.Contains("SettingsMenu"))
                {reason+="shared menu/chat canvas";return false;}
            }
            reason="Fullscreen aircraft HUD canvas rendering suppressed; native weapon logic remains live.";return true;
        }
        internal bool IsCurrent(PodContext context)
        {
            if(disposed||hud==null||combat==null||context==null||context.Aircraft!=aircraft||SceneSingleton<FlightHud>.i!=hud||SceneSingleton<CombatHUD>.i!=combat||combat.aircraft!=aircraft||NativeContract.Read<Aircraft>(hud,"aircraft")!=aircraft||NativeContract.Read<Canvas>(hud,"canvas")!=root||!Proof(hud,combat,root,out _))return false;
            var current=root.GetComponentsInChildren<Canvas>(true);
            if(current.Length!=canvases.Count)return false;
            foreach(var canvas in current)if(canvas==null||!canvases.Contains(canvas)||canvas.enabled)return false;
            return true;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            // Restore surviving captured Canvas flags even on pilot/scene loss; never reactivate GameObjects.
            for(int i=canvases.Count-1;i>=0;i--){var canvas=canvases[i];if(canvas!=null&&!canvas.enabled)canvas.enabled=prior[i];}
            canvases.Clear();prior.Clear();
        }
    }
}
