#nullable disable
using System;
using NuclearOption.Networking;
using NuclearOption.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Player = NuclearOption.Networking.Player;
namespace NuclearOptionTargetingPod
{
    internal sealed class PodContext
    {
        internal Aircraft Aircraft;
        internal TargetCam TargetCam;
        internal Camera Camera;
        internal CameraStateManager View;
        internal Pilot Pilot;
        internal Player Player;
        internal int Scene;
        internal static bool ObserverLoaded()
        {
            foreach(var a in AppDomain.CurrentDomain.GetAssemblies()) if(a.GetName().Name=="Observer")return true;
            return false;
        }
        internal static bool TryGet(out PodContext value)
        {
            value=null;
            if(GameManager.gameState!=GameState.SinglePlayer || !NetworkManagerNuclearOption.IsLoaded || NetworkManagerNuclearOption.IsLoadingScene)return false;
            var manager=NetworkManagerNuclearOption.i;
            if(manager==null || manager.Server==null || manager.Client==null)return false;
            var server=manager.Server; var client=manager.Client;
            if(!server.Active||server.Listening||!client.IsConnected||!client.IsHost||!ReferenceEquals(server.LocalClient,client)||server.LocalPlayer==null||server.AuthenticatedPlayers==null||server.AuthenticatedPlayers.Count!=1||!ReferenceEquals(server.AuthenticatedPlayers[0],server.LocalPlayer))return false;
            if(!GameManager.GetLocalPlayer<Player>(out var player)||player==null||!player.isActiveAndEnabled)return false;
            if(!GameManager.GetLocalAircraft(out var aircraft)||aircraft==null||!aircraft.isActiveAndEnabled||aircraft.disabled||!aircraft.persistentID.IsValid||aircraft.HasEjected()||aircraft.cockpit==null||aircraft.cockpit.IsDetached()||aircraft.targetCam==null||!aircraft.Identity.HasAuthority)return false;
            Pilot local=null;
            if(aircraft.pilots==null)return false;
            foreach(var pilot in aircraft.pilots)
            {
                if(pilot==null||pilot.dead||pilot.ejected||pilot.player==null||!GameManager.IsLocalPlayer(pilot.player)||!ReferenceEquals(pilot.aircraft,aircraft)||pilot.playerState==null||!ReferenceEquals(pilot.currentState,pilot.playerState))continue;
                if(local!=null)return false; local=pilot;
            }
            var view=SceneSingleton<CameraStateManager>.i; var scene=SceneManager.GetActiveScene();
            if(local==null||view==null||!view.isActiveAndEnabled||view.mainCamera==null||!view.mainCamera.isActiveAndEnabled||!ReferenceEquals(view.followingUnit,aircraft)||!scene.IsValid()||!scene.isLoaded||!aircraft.gameObject.scene.isLoaded||!view.gameObject.scene.isLoaded)return false;
            if(!ReferenceEquals(view.currentState,view.cockpitState))return false;
            var camera=NativeContract.Read<Camera>(aircraft.targetCam,"cam");
            if(camera==null)return false;
            value=new PodContext{Aircraft=aircraft,TargetCam=aircraft.targetCam,Camera=camera,View=view,Pilot=local,Player=player,Scene=scene.handle}; return true;
        }
        internal bool Matches(PodContext other) => other!=null&&Scene==other.Scene&&ReferenceEquals(Aircraft,other.Aircraft)&&ReferenceEquals(TargetCam,other.TargetCam)&&ReferenceEquals(Camera,other.Camera)&&ReferenceEquals(View,other.View)&&ReferenceEquals(Pilot,other.Pilot)&&ReferenceEquals(Player,other.Player);
        private static bool TextFocus()
        {
            var selected=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if(selected==null)return false;
            foreach(var component in selected.GetComponents<Component>())if(component!=null&&(component is UnityEngine.UI.InputField||component.GetType().FullName=="TMPro.TMP_InputField"))return true;
            return false;
        }
        // Chat closes its cursor flag before its delayed native keyboard-map restoration.
        internal static bool UiClear() => GameplayUI.AllowPauseKeybind&&!TextFocus()&&(SceneSingleton<CameraControlUI>.i==null||!SceneSingleton<CameraControlUI>.i.isOpen)&& Application.isFocused&&!GameplayUI.GameIsPaused&&!DynamicMap.mapMaximized&&!LeaderboardMenu.IsOpen()&&!RadialMenuMain.IsInUse()&&CursorManager.GetFlags()==CursorFlags.None&&!NuclearOption.MissionEditorScripts.InputFieldChecker.InsideInputField&&GameManager.flightControlsEnabled;
    }
}
