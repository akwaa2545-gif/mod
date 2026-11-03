#nullable disable
using System;
using System.Collections.Generic;
using Rewired;
using UnityEngine;
namespace NuclearOptionTargetingPod
{
    // Scope is physical vertical-wheel mappings only. Never suppress an entire action/map/player.
    internal sealed class WheelInputLease : IDisposable
    {
        private readonly Func<bool> eligible,contextValid;
        private readonly Action ownershipLost;
        private readonly WheelLeasePolicy policy=new WheelLeasePolicy();
        private bool disposed,requested;
        private string fault="";
        internal bool Active=>policy.Active;
        internal bool Draining=>policy.Draining;
        internal string Fault=>fault.Length!=0?fault:policy.Fault;
        internal WheelInputLease(Func<bool> eligible,Func<bool> contextValid,Action ownershipLost=null)
        {
            this.eligible=eligible??throw new ArgumentNullException(nameof(eligible));
            this.contextValid=contextValid??throw new ArgumentNullException(nameof(contextValid));
            this.ownershipLost=ownershipLost;
            ReInput.InputSourceUpdateEvent+=BeforeSource;
        }
        internal void Request(bool value)
        {
            if(disposed)return;
            if(value&&Draining)return;
            if(value&&!requested)fault="";
            requested=value;policy.Request(value);
        }
        internal void Clear(){requested=false;policy.Clear();}
        private void BeforeSource()
        {
            if(disposed||(!requested&&!Active))return;
            try
            {
                if(!ReInput.isReady||!contextValid()||(!Draining&&!eligible())){Fail("Wheel ownership context ended.");return;}
                var entries=Collect(out var mouse,out bool neutral);
                if(entries==null){Clear();return;}
                // Cached previous input must be neutral before entry/release. Digital-smoothed
                // wheel modes are rejected by Collect rather than resetting unrelated action state.
                policy.BeforeSource(entries,true,neutral,Time.frameCount);
                if(!policy.Active&&policy.Fault.Length!=0)Fail(policy.Fault);
            }
            catch(Exception ex){Fail("Wheel input boundary failed: "+ex.GetType().Name);}
        }
        internal bool TryConsume(out float delta)
        {
            delta=0;
            if(disposed||!requested||!Active||Draining)return false;
            try
            {
                if(!contextValid()||!eligible()){Fail("Wheel ownership context ended.");return false;}
                var entries=Collect(out var mouse,out _);
                if(entries==null)return false;
                if(!policy.Validate(entries)){Fail("Wheel mappings changed before consumption.");return false;}
                return policy.Consume(Time.frameCount,mouse.GetAxisRawById(2),out delta);
            }
            catch(Exception ex){Fail("Wheel sample failed: "+ex.GetType().Name);return false;}
        }
        private List<IWheelLeaseElement> Collect(out Mouse mouse,out bool neutral)
        {
            mouse=ReInput.controllers.Mouse;neutral=false;
            if(mouse==null||mouse.GetAxisIndexById(2)!=2){Fail("Vertical mouse wheel is unavailable.");return null;}
            bool verified=false;
            foreach(var id in mouse.AxisElementIdentifiers)
                if(id.id==2&&id.key=="wheel/vertical"&&id.elementType==ControllerElementType.Axis)verified=true;
            if(!verified){Fail("Vertical mouse wheel identity is unsupported.");return null;}
            float raw=mouse.GetAxisRawById(2);
            neutral=!float.IsNaN(raw)&&!float.IsInfinity(raw)&&raw==0;
            var result=new List<IWheelLeaseElement>();int maps=0,elements=0;
            foreach(var player in ReInput.players.AllPlayers)
            foreach(var map in player.controllers.maps.GetAllMaps(ControllerType.Mouse))
            {
                if(map==null||++maps>128)throw new InvalidOperationException("Mouse map bound exceeded.");
                foreach(var element in map.AllMaps)
                {
                    if(element==null||++elements>4096)throw new InvalidOperationException("Mouse element bound exceeded.");
                    if(element.elementType!=ControllerElementType.Axis||element.elementIdentifierId!=2)continue;
                    var action=ReInput.mapping.GetAction(element.actionId);
                    var behavior=action==null?null:ReInput.mapping.GetInputBehavior(player.id,action.behaviorId);
                    if(behavior==null||behavior.mouseOtherAxisMode!=MouseOtherAxisMode.MouseAxis)
                    {Fail("Fullscreen wheel unavailable for digital-smoothed or unknown wheel bindings.");return null;}
                    if(!Active&&(player.GetAxis(element.actionId)!=0||player.GetAxisRaw(element.actionId)!=0||player.GetButton(element.actionId)))neutral=false;
                    result.Add(new Element(player,map,element));
                }
            }
            return result;
        }
        private void Fail(string reason)
        {
            bool notify=requested||Active;
            fault=reason;
            Clear();
            // Release mappings first. The owner can then release its screen camera in this
            // same source-update callback before native input aggregation continues.
            if(notify&&ownershipLost!=null)
            {
                try {ownershipLost();}
                catch(Exception ex){fault=reason+" Ownership-loss callback failed: "+ex.GetType().Name;}
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            ReInput.InputSourceUpdateEvent-=BeforeSource;Clear();
        }
        private sealed class Element : IWheelLeaseElement
        {
            private readonly Player player;private readonly ControllerMap map;private readonly ActionElementMap element;
            internal Element(Player player,ControllerMap map,ActionElementMap element){this.player=player;this.map=map;this.element=element;}
            public object Identity=>element;
            public string Binding
            {
                get
                {
                    var action=ReInput.mapping.GetAction(element.actionId);
                    var behavior=action==null?null:ReInput.mapping.GetInputBehavior(player.id,action.behaviorId);
                    return player.id+":"+map.id+":"+element.id+":"+element.actionId+":"+element.elementIdentifierId+":"+(int)element.elementType+":"+(int)element.axisRange+":"+(int)element.axisContribution+":"+element.invert+":"+(action==null?-1:action.behaviorId)+":"+(behavior==null?-1:(int)behavior.mouseOtherAxisMode);
                }
            }
            public bool Attached
            {
                get
                {
                    int maps=0,elements=0;bool playerFound=false;
                    foreach(var p in ReInput.players.AllPlayers)if(ReferenceEquals(p,player)){playerFound=true;break;}
                    if(!playerFound)return false;
                    foreach(var candidate in player.controllers.maps.GetAllMaps(ControllerType.Mouse))
                    {
                        if(++maps>128)return false;if(!ReferenceEquals(candidate,map))continue;
                        foreach(var e in candidate.AllMaps){if(++elements>4096)return false;if(ReferenceEquals(e,element))return ReferenceEquals(element.controllerMap,map);}
                    }
                    return false;
                }
            }
            public bool Enabled {get=>element.enabled;set=>element.enabled=value;}
        }
    }
}
