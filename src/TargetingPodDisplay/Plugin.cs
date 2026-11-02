#nullable disable
using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
namespace NuclearOptionTargetingPod
{
    [BepInPlugin(Id,"Targeting Pod Display","0.1.0")]
    [DefaultExecutionOrder(10000)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id="local.nuclearoption.targetingpoddisplay";
        private ConfigEntry<bool> enabledConfig,cockpitConfig,confirmedKeys;
        private ConfigEntry<KeyCode> up,down,left,right,zoomIn,zoomOut,recenter,stabilize;
        private bool SensorKeyHeld(){foreach(var key in SensorKeys())if(Input.GetKey(key))return true;return false;}
        private KeyCode[] SensorKeys()=>new[]{up.Value,down.Value,left.Value,right.Value,zoomIn.Value,zoomOut.Value,recenter.Value,stabilize.Value};
        private ConfigEntry<KeyCode> fullKey,manualKey;
        private readonly ConfigurationGate configuration=new ConfigurationGate();
        private InputConfiguration CurrentConfiguration()
        {
            var sensorKeys=SensorKeys();var keys=new int[sensorKeys.Length+2];keys[0]=(int)fullKey.Value;keys[1]=(int)manualKey.Value;
            for(int i=0;i<sensorKeys.Length;i++)keys[i+2]=(int)sensorKeys[i];
            return new InputConfiguration(keys,confirmedKeys.Value,enabledConfig.Value,cockpitConfig.Value);
        }
        private PodContext context;
        private Presentation presentation;
        private CockpitBinding binding;
        private OwnedSensor sensor;
        private RenderTexture source;
        private bool compatible,fullscreen,faulted,manual,renderingOwned;
        private string status="Disabled";
        private int renderedFrame=-1;
        private void Awake()
        {
            enabledConfig=Config.Bind("General","Enabled",false,"Experimental / runtime untested. Explicit opt-in. Offline local cockpit only.");
            cockpitConfig=Config.Bind("General","ReplaceCockpit",false,"Opt-in: replace only a proven unique native targeting RawImage. Unsupported surfaces retain native display.");
            fullKey=Config.Bind("Keys","Fullscreen",KeyCode.None,"Unbound by default. Requires unused native keyboard key. Aircraft controls remain LIVE. F7-F11 reserved for Observer.");
            manualKey=Config.Bind("Keys","ManualSensor",KeyCode.None,"Unbound by default. Enable independent COLOR sensor and dedicated unmapped keys; no input capture or autopilot. Arrows slew, PageUp/PageDown zoom, Home recenter, End inertial direction hold. All must be native-unmapped.");
            confirmedKeys=Config.Bind("Keys","ConfirmNoOtherModUsesSensorKeys",false,"Manual sensor requires true after checking other mods for raw-key shortcuts. Native Rewired conflicts are checked automatically; unknown third-party Unity key handlers cannot be proved absent. This mod never disables their input.");
            up=Config.Bind("SensorKeys","Up",KeyCode.UpArrow,"Letters except A/C/F/H/V/Y, or arrows/PageUp/PageDown/Home/End. Keypad keys excluded due native raw handlers. All eight must be distinct and native-unmapped; also check other mods.");
            down=Config.Bind("SensorKeys","Down",KeyCode.DownArrow,"");left=Config.Bind("SensorKeys","Left",KeyCode.LeftArrow,"");right=Config.Bind("SensorKeys","Right",KeyCode.RightArrow,"");
            zoomIn=Config.Bind("SensorKeys","ZoomIn",KeyCode.PageUp,"");zoomOut=Config.Bind("SensorKeys","ZoomOut",KeyCode.PageDown,"");recenter=Config.Bind("SensorKeys","Recenter",KeyCode.Home,"");stabilize=Config.Bind("SensorKeys","InertialHold",KeyCode.End,"");
            compatible=Compatibility.Check(out var reason)&&Compatibility.CheckFullscreen(out reason);SetStatus(reason);
            RenderPipelineManager.endFrameRendering+=AfterFrame;
        }
        private void SetStatus(string value){if(status==value)return;status=value;Logger.LogInfo(value);}
        private bool KeysReady()=>DedicatedKeys.Available(fullKey.Value,manualKey.Value,SensorKeys(),out _);
        private void LateUpdate()
        {
            try
            {
                bool rearmed=configuration.Poll(CurrentConfiguration(),Time.frameCount,key=>Input.GetKey((KeyCode)key),out bool changed);
                if(changed){Release();return;}
                if(!compatible||faulted||!enabledConfig.Value||!PodContext.TryGet(out var live)||!PodContext.UiClear()){Release();return;}
                if(context!=null&&!context.Matches(live)){Release();return;}
                context=live;
                bool arbitration=ObserverArbitration.Available();
                bool keys=DedicatedKeys.Available(fullKey.Value,manualKey.Value,SensorKeys(),out var keyReason);
                if(!arbitration||!keys||manual&&!confirmedKeys.Value){if(manual||fullscreen){Release();return;}if(!keys)SetStatus(keyReason);}
                if(rearmed&&arbitration&&keys&&confirmedKeys.Value&&manualKey.Value!=KeyCode.None&&Input.GetKeyDown(manualKey.Value)&&(!manual&&!SensorKeyHeld()||manual))
                {
                    manual=!manual;binding?.Dispose();binding=null;presentation?.Dispose();presentation=null;
                    sensor?.Dispose();sensor=null;
                    if(manual)sensor=new OwnedSensor(context);
                }
                if(!NativeContract.IsTargetMode(context.TargetCam)){Release();return;}
                var cam=context.Camera;var next=cam.targetTexture;
                bool available=manual||DisplayPolicy.CanPresent(true,cam.isActiveAndEnabled,true,context.Aircraft.weaponManager.GetTargetList().Count>0);
                if(!available||next==null||!next.IsCreated()||next.width<64||next.height<64||next.width>4096||next.height>4096||context.Aircraft.weaponManager==null){Release();return;}
                if(source!=null&&!ReferenceEquals(source,next)){Release();return;}
                source=next;
                int width=manual?1024:source.width,height=manual?1024:source.height;
                if(presentation!=null&&(presentation.Output.width!=width||presentation.Output.height!=height)){binding?.Dispose();binding=null;presentation.Dispose();presentation=null;renderedFrame=-1;}
                if(presentation==null)presentation=new Presentation(width,height);
                if(manual)
                {
                    float dt=Mathf.Min(Time.unscaledDeltaTime,.05f),rate=sensor.View.Fov*1.5f*dt;
                    sensor.Slew((Input.GetKey(right.Value)?rate:0)-(Input.GetKey(left.Value)?rate:0),(Input.GetKey(down.Value)?rate:0)-(Input.GetKey(up.Value)?rate:0));
                    sensor.View.Zoom((Input.GetKey(zoomOut.Value)?rate:0)-(Input.GetKey(zoomIn.Value)?rate:0));
                    if(Input.GetKeyDown(recenter.Value))sensor.Reset();
                    if(Input.GetKeyDown(stabilize.Value))sensor.Stabilize();
                }
                presentation.SetManual(manual,sensor?.Stabilized==true,sensor?.View.Fov??0);presentation.UpdateTruth(context);
                if(!manual&&renderedFrame<0)presentation.Compose(source);
                if(binding!=null&&(!cockpitConfig.Value||!binding.IsCurrent(context))){binding.Dispose();binding=null;}
                if(cockpitConfig.Value&&binding==null)
                {
                    var image=NativeContract.FindConsumer(context,source,out var reason);SetStatus(reason);
                    if(image!=null)binding=new CockpitBinding(context,image,presentation.Output);
                }
                binding?.MaintainActivation(context,manual);
                if(rearmed&&arbitration&&keys&&fullKey.Value!=KeyCode.None&&Input.GetKeyDown(fullKey.Value))fullscreen=!fullscreen;
                if(Input.GetKeyDown(KeyCode.Escape)){Release();return;}
                if(manual)
                {
                    // Recheck immediately before rendering. No native camera/weapon/flight state is written.
                    if(!configuration.Matches(CurrentConfiguration())||!ObserverArbitration.Available()||!KeysReady()){Release();return;}
                    if(binding!=null&&!binding.IsCurrent(context)){binding.Dispose();binding=null;}
                    renderingOwned=true;try{sensor.Render(context);}finally{renderingOwned=false;}
                    if(!configuration.Matches(CurrentConfiguration())||!PodContext.TryGet(out var afterRender)||!context.Matches(afterRender)||!PodContext.UiClear()||!ObserverArbitration.Available()){Release();return;}
                    if(binding!=null&&!binding.IsCurrent(context)){binding.Dispose();binding=null;}
                    presentation.Compose(sensor.Texture);renderedFrame=Time.frameCount;
                }
            }
            catch(Exception ex){faulted=true;Release();SetStatus("Fail closed until restart: "+ex.GetType().Name+": "+ex.Message);}
        }
        private void AfterFrame(ScriptableRenderContext renderContext,Camera[] cameras)
        {
            if(renderingOwned||manual||presentation==null||source==null||context==null||renderedFrame==Time.frameCount)return;
            try
            {
                if(!configuration.Matches(CurrentConfiguration())||!PodContext.TryGet(out var live)||!context.Matches(live)||!PodContext.UiClear()||!context.Camera.isActiveAndEnabled||!NativeContract.IsTargetMode(context.TargetCam)){Release();return;}
                bool rendered=false;foreach(var camera in cameras)if(ReferenceEquals(camera,context.Camera))rendered=true;
                if(!rendered){Release();return;}
                if(binding!=null&&!binding.IsCurrent(context)){binding.Dispose();binding=null;}
                presentation.Compose(source);renderedFrame=Time.frameCount;
            }
            catch(Exception ex){faulted=true;Release();SetStatus("Render failed closed: "+ex.GetType().Name);}
        }
        private void OnGUI()
        {
            if(!fullscreen||presentation==null||Event.current.type!=EventType.Repaint)return;
            if(!configuration.Matches(CurrentConfiguration())||!PodContext.UiClear()||!ObserverArbitration.Available()||!KeysReady()){Release();return;}
            var output=presentation.Output;if(output==null)return;
            // Flight/safety HUD perimeter stays visible; never claim full input capture.
            var area=DisplayPolicy.Fit(Screen.width*.9f,Screen.height*.85f,output.width,output.height);
            GUI.DrawTexture(new Rect(Screen.width*.05f+area.X,Screen.height*.075f+area.Y,area.Width,area.Height),output,ScaleMode.ScaleToFit,false);
            GUI.Label(new Rect(16,16,Screen.width-32,48),"Targeting Pod Display — AIRCRAFT CONTROLS LIVE / NOT AUTOPILOT | "+fullKey.Value+" return | "+(manual?"dedicated sensor keys":"native sensor")+" | experimental");
        }
        private void OnApplicationFocus(bool focused){if(!focused)Release();}
        private void Release()
        {
            if(manual||fullscreen)configuration.Block();fullscreen=false;manual=false;binding?.Dispose();binding=null;presentation?.Dispose();presentation=null;sensor?.Dispose();sensor=null;source=null;context=null;renderedFrame=-1;
        }
        private void OnDisable()=>Release();
        private void OnDestroy(){RenderPipelineManager.endFrameRendering-=AfterFrame;Release();}
    }
}
