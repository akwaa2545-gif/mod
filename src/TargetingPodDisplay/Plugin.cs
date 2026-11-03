#nullable disable
using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
namespace NuclearOptionTargetingPod
{
    [BepInPlugin(Id,"Targeting Pod Display","0.1.2")]
    [DefaultExecutionOrder(10000)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id="local.nuclearoption.targetingpoddisplay";
        private ConfigEntry<bool> enabledConfig,cockpitConfig,confirmedKeys,pipConfig;
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
        private FullscreenCameraLease screenLease;
        private AftermathTracker aftermath;
        private ConfigEntry<float> aftermathSeconds;
        private bool aftermathUnavailable,pendingFullscreen;
        private bool compatible,fullscreen,faulted,manual;
        private string status="Disabled";
        private string keyStatus="";
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
            aftermathSeconds=Config.Bind("Presentation","AftermathSeconds",3f,new ConfigDescription("Live last-known-position view after a verified native kill report. Zero disables; never triggers from ordinary deselection.",new AcceptableValueRange<float>(0,10)));
            pipConfig=Config.Bind("Presentation","PiPEnabled",false,"Passive right-center targeting view. Shares cockpit output; hidden during direct fullscreen.");
        }
        private void SetStatus(string value){if(status==value)return;status=value;Logger.LogInfo(value);}
        private void SetKeyStatus(string value){if(keyStatus==value)return;keyStatus=value;Logger.LogInfo(value);}
        private bool KeysReady()=>DedicatedKeys.Available(fullKey.Value,manualKey.Value,SensorKeys(),out _);
        private bool ContextCurrent(out PodContext live)=>PodContext.TryGet(out live,screenLease)&&context!=null&&context.Matches(live)&&PodContext.UiClear();
        private bool RenderEligible()=>configuration.Matches(CurrentConfiguration())&&ContextCurrent(out _)&&(!(manual||fullscreen)||(ObserverArbitration.Available()&&KeysReady()));
        private void LateUpdate()
        {
            try
            {
                bool rearmed=configuration.Poll(CurrentConfiguration(),Time.frameCount,key=>Input.GetKey((KeyCode)key),out bool changed);
                if(changed){Release();return;}
                if(!compatible||faulted||!enabledConfig.Value||!PodContext.TryGet(out var live,screenLease)||!PodContext.UiClear()){Release();return;}
                if(context!=null&&!context.Matches(live)){Release();return;}
                context=live;
                bool arbitration=ObserverArbitration.Available();
                bool keys=DedicatedKeys.Available(fullKey.Value,manualKey.Value,SensorKeys(),out var keyReason);
                if(!keys)SetKeyStatus(keyReason);else keyStatus="";
                if(!arbitration||!keys||manual&&!confirmedKeys.Value){if(manual||fullscreen){Release();return;}}
                if(!NativeContract.IsTargetMode(context.TargetCam)||context.Aircraft.weaponManager==null){Release();return;}
                if(rearmed&&arbitration&&keys&&confirmedKeys.Value&&manualKey.Value!=KeyCode.None&&Input.GetKeyDown(manualKey.Value)&&(!manual&&!SensorKeyHeld()||manual))
                {
                    if(sensor==null)sensor=new OwnedSensor(context,RenderEligible,Release);
                    manual=!manual;sensor.Reset();aftermath?.Clear();pendingFullscreen=false;
                }
                var cam=context.Camera;var next=cam.targetTexture;
                var targets=context.Aircraft.weaponManager.GetTargetList();
                if(aftermath==null&&!aftermathUnavailable&&!manual&&targets.Count==1&&cam.isActiveAndEnabled&&aftermathSeconds.Value>0)
                {
                    try {aftermath=new AftermathTracker(()=>ContextCurrent(out _),()=>Mathf.Clamp(aftermathSeconds.Value,0,10));}
                    catch(Exception ex){aftermathUnavailable=true;SetStatus("Aftermath unavailable: "+ex.GetType().Name);}
                }
                if(manual||targets.Count>1)aftermath?.Clear();
                else if(targets.Count==1&&targets[0]!=null&&cam.isActiveAndEnabled)aftermath?.Observe(targets[0],NativeContract.Read<GlobalPosition>(context.TargetCam,"targetPosition"),cam.fieldOfView,context.Scene);
                else aftermath?.Observe(null,default,0,context.Scene);
                GlobalPosition aftermathPoint=default;float aftermathFov=0;
                bool showingAftermath=aftermath!=null&&aftermath.TryGet(Time.unscaledTimeAsDouble,out aftermathPoint,out aftermathFov);
                if(aftermath?.Fault!=null)SetStatus(aftermath.Fault);
                bool available=manual||showingAftermath||DisplayPolicy.CanPresent(true,cam.isActiveAndEnabled,true,context.Aircraft.weaponManager.GetTargetList().Count>0);
                if(!available)
                {
                    if(Input.GetKeyDown(KeyCode.Escape)||((fullscreen||pendingFullscreen)&&keys&&fullKey.Value!=KeyCode.None&&Input.GetKeyDown(fullKey.Value))){Release();return;}
                    // Retain only a same-frame kill-notification opportunity. No frozen image is shown.
                    if(aftermath!=null&&aftermath.Pending)
                    {
                        pendingFullscreen|=fullscreen;HidePresentation(false);return;
                    }
                    pendingFullscreen=false;HidePresentation(true);return;
                }
                if(next==null||!next.IsCreated()||next.width<64||next.height<64||next.width>4096||next.height>4096){Release();return;}
                if(source!=null&&!ReferenceEquals(source,next)){Release();return;}
                var dimensions=DisplayPolicy.OutputSize(next.width,next.height);
                if(dimensions.Width==0){SetStatus("Unsupported targeting texture aspect; native display retained.");HidePresentation(true);return;}
                source=next;
                if(presentation!=null&&(presentation.Output.width!=(int)dimensions.Width||presentation.Output.height!=(int)dimensions.Height))
                {binding?.Dispose();binding=null;presentation.Dispose();presentation=null;}
                if(sensor==null)sensor=new OwnedSensor(context,RenderEligible,Release);
                if(presentation==null)presentation=new Presentation((int)dimensions.Width,(int)dimensions.Height);
                if(manual)
                {
                    float dt=Mathf.Min(Time.unscaledDeltaTime,.05f),rate=sensor.View.Fov*1.5f*dt;
                    sensor.Slew((Input.GetKey(right.Value)?rate:0)-(Input.GetKey(left.Value)?rate:0),(Input.GetKey(down.Value)?rate:0)-(Input.GetKey(up.Value)?rate:0));
                    sensor.View.Zoom((Input.GetKey(zoomOut.Value)?rate:0)-(Input.GetKey(zoomIn.Value)?rate:0));
                    if(Input.GetKeyDown(recenter.Value))sensor.Reset();
                    if(Input.GetKeyDown(stabilize.Value))sensor.Stabilize();
                }
                if(showingAftermath&&pendingFullscreen&&arbitration&&keys)
                {screenLease=new FullscreenCameraLease(context,Release);fullscreen=true;pendingFullscreen=false;}
                if(!showingAftermath)pendingFullscreen=false;
                if(rearmed&&arbitration&&keys&&fullKey.Value!=KeyCode.None&&Input.GetKeyDown(fullKey.Value))
                {
                    if(fullscreen)EndFullscreen();
                    else {screenLease=new FullscreenCameraLease(context,Release);fullscreen=true;}
                }
                if(Input.GetKeyDown(KeyCode.Escape)){Release();return;}
                if(!configuration.Matches(CurrentConfiguration())||!ContextCurrent(out _)){Release();return;}
                if((manual||fullscreen)&&(!ObserverArbitration.Available()||!KeysReady())){Release();return;}
                sensor.Prepare(context,manual,showingAftermath?(GlobalPosition?)aftermathPoint:null,aftermathFov);sensor.SetDirect(fullscreen);
                presentation.SetManual(manual,sensor.Stabilized,sensor.Fov);presentation.UpdateTruth(context,showingAftermath);
                if(binding!=null&&(!cockpitConfig.Value||!binding.IsCurrent(context))){binding.Dispose();binding=null;}
                if(!fullscreen)
                {
                    sensor.Render((int)dimensions.Width,(int)dimensions.Height);
                    if(!configuration.Matches(CurrentConfiguration())||!ContextCurrent(out _)||((manual||fullscreen)&&!ObserverArbitration.Available())){Release();return;}
                    presentation.Compose(sensor.Texture);renderedFrame=Time.frameCount;
                    if(cockpitConfig.Value&&binding==null)
                    {
                        var image=NativeContract.FindConsumer(context,source,out var reason);SetStatus(reason);
                        if(image!=null)binding=new CockpitBinding(context,image,presentation.Output);
                    }
                    binding?.MaintainActivation(context,manual||showingAftermath);
                }
                else
                {
                    // Fullscreen is a real direct-screen camera. Cockpit/PiP consumers resume on exit.
                    binding?.Dispose();binding=null;renderedFrame=Time.frameCount;
                }
            }
            catch(Exception ex){faulted=true;Release();SetStatus("Fail closed until restart: "+ex.GetType().Name+": "+ex.Message);}
        }
        private void EndFullscreen()
        {
            if(sensor!=null)sensor.SetDirect(false);
            fullscreen=false;var lease=screenLease;screenLease=null;lease?.Dispose();
        }
        private void OnGUI()
        {
            if(presentation==null||Event.current.type!=EventType.Repaint||renderedFrame!=Time.frameCount)return;
            if(!configuration.Matches(CurrentConfiguration())||!ContextCurrent(out _)||((manual||fullscreen)&&(!ObserverArbitration.Available()||!KeysReady()))){Release();return;}
            var previousColor=GUI.color;GUI.color=Color.white;
            try
            {
                if(fullscreen)presentation.DrawOverlay(new Rect(0,0,Screen.width,Screen.height));
                else if(pipConfig.Value)
                {
                    var output=presentation.Output;if(output==null)return;
                    var area=DisplayPolicy.Fit(Screen.width*.28f,Screen.height*.40f,output.width,output.height);
                    GUI.DrawTexture(new Rect(Screen.width-area.Width-16,(Screen.height-area.Height)*.5f,area.Width,area.Height),output,ScaleMode.ScaleToFit,false);
                }
            }
            catch(Exception ex){faulted=true;Release();SetStatus("Presentation failed closed: "+ex.GetType().Name);}
            finally {GUI.color=previousColor;}
            // AIRCRAFT CONTROLS LIVE / NOT AUTOPILOT. This passive overlay consumes no mouse/flight input.
        }
        private void OnApplicationFocus(bool focused){if(!focused)Release();}
        private void HidePresentation(bool blockInput)
        {
            if(blockInput&&(manual||fullscreen))configuration.Block();
            EndFullscreen();manual=false;binding?.Dispose();binding=null;presentation?.Dispose();presentation=null;sensor?.Dispose();sensor=null;source=null;renderedFrame=-1;
        }
        private void Release()
        {
            if(manual||fullscreen)configuration.Block();
            HidePresentation(false);aftermath?.Dispose();aftermath=null;aftermathUnavailable=false;pendingFullscreen=false;context=null;
        }
        private void OnDisable()=>Release();
        private void OnDestroy()=>Release();
    }
}
