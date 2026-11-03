#nullable disable
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using Rewired;
using UnityEngine;
namespace NuclearOptionTargetingPod
{
    internal static class DedicatedKeys
    {
        internal static bool Available(KeyCode fullscreen,KeyCode manual,KeyCode[] sensorKeys,out string reason)
        {
            reason="Dedicated keys unavailable: select unused F1-F6/F12 toggles and choose distinct native-unmapped sensor keys.";
            if(!AllowedToggle(fullscreen)){reason="Keys.Fullscreen="+fullscreen+" is unsupported. Choose None, F1-F6 or F12. Native bindings are checked; check other mods too.";return false;}
            if(!AllowedToggle(manual)){reason="Keys.ManualSensor="+manual+" is unsupported. Choose None, F1-F6 or F12. Native bindings are checked; check other mods too.";return false;}
            if(fullscreen!=KeyCode.None&&fullscreen==manual){reason="Keys.Fullscreen and Keys.ManualSensor both use "+fullscreen+". Choose distinct toggle keys.";return false;}
            if(!ReInput.isReady||GameManager.playerInput==null){reason="Native input is not ready; dedicated keys remain unavailable.";return false;}
            var keys=new HashSet<KeyCode>();
            if(manual!=KeyCode.None)foreach(var key in sensorKeys)
            {
                if(!AllowedSensor(key)){reason="SensorKeys contains unsupported key "+key+". Use permitted letters/navigation keys listed in the guide.";return false;}
                if(!keys.Add(key)){reason="SensorKeys repeats "+key+". Each sensor action needs a distinct key.";return false;}
            }
            if(fullscreen!=KeyCode.None)keys.Add(fullscreen);if(manual!=KeyCode.None)keys.Add(manual);
            int maps=0,elements=0;
            // All native players, including UI/system mappings. Never remap or silently disable native input.
            foreach(var player in ReInput.players.AllPlayers)
            {
                foreach(var map in player.controllers.maps.GetAllMaps(ControllerType.Keyboard))
                {
                    if(++maps>128)return false;if(map==null||!map.enabled)continue;
                    foreach(var element in map.AllMaps)
                    {
                        if(++elements>4096)return false;
                        if(element==null)return false;
                        if(keys.Contains(element.keyCode)){reason="Dedicated sensor key conflicts with enabled native input: "+element.keyCode+". Native input retained.";return false;}
                    }
                }
            }
            reason="Dedicated sensor keys verified unmapped; flight controls remain live.";return true;
        }
        // Conservative native raw-key inventory exclusion (including inactive editor/chase handlers).
        internal static bool AllowedSensor(KeyCode key)=>
            key>=KeyCode.A&&key<=KeyCode.Z&&key!=KeyCode.A&&key!=KeyCode.C&&key!=KeyCode.F&&key!=KeyCode.H&&key!=KeyCode.V&&key!=KeyCode.Y||
            key==KeyCode.UpArrow||key==KeyCode.DownArrow||key==KeyCode.LeftArrow||key==KeyCode.RightArrow||key==KeyCode.PageUp||key==KeyCode.PageDown||key==KeyCode.Home||key==KeyCode.End;

        private static bool AllowedToggle(KeyCode key)=>key==KeyCode.None||key>=KeyCode.F1&&key<=KeyCode.F6||key==KeyCode.F12;
    }
}
