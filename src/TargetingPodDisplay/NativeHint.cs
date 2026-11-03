#nullable disable
using System;
using System.Reflection;
using UnityEngine;
namespace NuclearOptionTargetingPod
{
    internal sealed class NativeHint
    {
        private HUDWeaponState previousState;
        private WeaponStation previousStation;
        private Unit[] previousTargets=Array.Empty<Unit>();
        private float changedAt;
        internal void Clear(){previousState=null;previousStation=null;previousTargets=Array.Empty<Unit>();changedAt=0;}
        internal string Read(PodContext context)
        {
            var hud=SceneSingleton<CombatHUD>.i;
            var station=context.Aircraft.weaponManager.currentWeaponStation;
            var targets=context.Aircraft.weaponManager.GetTargetList();
            if(hud==null||!ReferenceEquals(hud.aircraft,context.Aircraft))return "WPN HINT N/A";
            var state=NativeContract.Read<HUDWeaponState>(hud,"weaponState");
            bool changed=!ReferenceEquals(state,previousState)||!ReferenceEquals(station,previousStation)||targets.Count!=previousTargets.Length;
            if(!changed)for(int i=0;i<targets.Count;i++)if(!ReferenceEquals(targets[i],previousTargets[i])){changed=true;break;}
            if(changed){previousState=state;previousStation=station;previousTargets=targets.ToArray();changedAt=Time.timeSinceLevelLoad;return "WPN HINT N/A";}
            if(!(state is HUDMissileState missile)||!state.isActiveAndEnabled||station==null||targets.Count==0||station.Ammo<=0||!ReferenceEquals(NativeContract.Read<WeaponStation>(missile,"weaponStation"),station)||!ReferenceEquals(NativeContract.Read<Aircraft>(missile,"aircraft"),context.Aircraft)||NativeContract.Read<bool>(missile,"hidden"))return "WPN HINT N/A";
            float stamp=NativeContract.Read<float>(missile,"lastTextDisplay");
            if(!SensorPolicy.Finite(stamp)||stamp<=changedAt||Time.timeSinceLevelLoad-stamp<0||Time.timeSinceLevelLoad-stamp>.25f)return "WPN HINT N/A";
            var field=typeof(HUDMissileState).GetField("hint",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            if(field==null||field.FieldType.FullName!="TMPro.TextMeshProUGUI")return "WPN HINT N/A";
            var hint=field.GetValue(missile) as Behaviour;
            if(hint==null||!hint.gameObject.activeInHierarchy)return "WPN HINT N/A";
            var property=hint.GetType().GetProperty("text",BindingFlags.Instance|BindingFlags.Public);
            if(property==null||property.PropertyType!=typeof(string))return "WPN HINT N/A";
            var text=(string)property.GetValue(hint);
            // Preserve only the inspected native envelope strings, never convert them to a lock claim.
            return NativeHintPolicy.Allowed(text,hint.enabled)?"WPN HINT: "+text:"WPN HINT N/A";
        }
    }
}
