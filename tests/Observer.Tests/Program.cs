using NuclearOptionObserver;

// No NuGet test runner or game/Unity/BepInEx assembly references. Failing assertion = nonzero exit.
var tests = new (string Name, Action Body)[] {
    ("original disk hash independent of empty loader-patched CoreModule location", () => {
        WithOriginal("UnityEngine.CoreModule.dll", (dir, hash) => {
            var info=new RuntimeAssemblyInfo("UnityEngine.CoreModule",new(0,0,0,0),Guid.NewGuid(),"",true);
            Check(FingerprintPolicy.CheckGame(dir,"UnityEngine.CoreModule.dll",hash,Guid.NewGuid(),info,true,out var reason));
            Check(reason.Contains("original",StringComparison.OrdinalIgnoreCase));
        });
    }),
    ("loader-patched dump location never replaces exact original game file", () => {
        WithOriginal("UnityEngine.CoreModule.dll", (dir, hash) => {
            string dump=Path.Combine(dir,"patched-copy.dll"); File.WriteAllText(dump,"different rewritten bytes");
            var info=new RuntimeAssemblyInfo("UnityEngine.CoreModule",new(0,0,0,0),Guid.NewGuid(),dump,true);
            Check(FingerprintPolicy.CheckGame(dir,"UnityEngine.CoreModule.dll",hash,Guid.NewGuid(),info,true,out _));
        });
    }),
    ("changed CoreModule MVID without loader entrypoint evidence rejects", () => {
        WithOriginal("UnityEngine.CoreModule.dll", (dir, hash) => Check(!FingerprintPolicy.CheckGame(dir,"UnityEngine.CoreModule.dll",hash,Guid.NewGuid(),new("UnityEngine.CoreModule",new(0,0,0,0),Guid.NewGuid(),"",false),true,out _)));
    }),
    ("unchanged CoreModule MVID tolerates memory location without claiming rewrite", () => {
        WithOriginal("UnityEngine.CoreModule.dll", (dir, hash) => { var id=Guid.NewGuid(); Check(FingerprintPolicy.CheckGame(dir,"UnityEngine.CoreModule.dll",hash,id,new("UnityEngine.CoreModule",new(0,0,0,0),id,"",false),true,out _)); });
    }),
    ("genuine changed CoreModule original remains rejected", () => {
        WithOriginal("UnityEngine.CoreModule.dll", (dir, hash) => {
            File.AppendAllText(Path.Combine(dir,"UnityEngine.CoreModule.dll"),"changed"); var id=Guid.NewGuid();
            Check(!FingerprintPolicy.CheckGame(dir,"UnityEngine.CoreModule.dll",hash,id,new("UnityEngine.CoreModule",new(0,0,0,0),id,""),true,out _));
        });
    }),
    ("genuine changed gameplay original remains rejected", () => {
        WithOriginal("Assembly-CSharp.dll", (dir, hash) => {
            File.AppendAllText(Path.Combine(dir,"Assembly-CSharp.dll"),"changed"); var id=Guid.NewGuid();
            Check(!FingerprintPolicy.CheckGame(dir,"Assembly-CSharp.dll",hash,id,new("Assembly-CSharp",new(0,0,0,0),id,"original"),true,out _));
        });
    }),
    ("non-CoreModule runtime MVID mismatch is never exempt", () => {
        WithOriginal("Assembly-CSharp.dll", (dir, hash) => Check(!FingerprintPolicy.CheckGame(dir,"Assembly-CSharp.dll",hash,Guid.NewGuid(),new("Assembly-CSharp",new(0,0,0,0),Guid.NewGuid(),""),true,out _)));
    }),
    ("matching gameplay disk and runtime fingerprint accepts", () => {
        WithOriginal("Assembly-CSharp.dll", (dir, hash) => { var id=Guid.NewGuid(); Check(FingerprintPolicy.CheckGame(dir,"Assembly-CSharp.dll",hash,id,new("Assembly-CSharp",new(0,0,0,0),id,"original"),true,out _)); });
    }),
    ("unsupported loader cannot claim CoreModule rewrite exemption", () => {
        WithOriginal("UnityEngine.CoreModule.dll", (dir, hash) => Check(!FingerprintPolicy.CheckGame(dir,"UnityEngine.CoreModule.dll",hash,Guid.NewGuid(),new("UnityEngine.CoreModule",new(0,0,0,0),Guid.NewGuid(),""),false,out _)));
    }),
    ("runtime assembly name must still match original filename", () => {
        WithOriginal("UnityEngine.CoreModule.dll", (dir, hash) => Check(!FingerprintPolicy.CheckGame(dir,"UnityEngine.CoreModule.dll",hash,Guid.NewGuid(),new("Unknown",new(0,0,0,0),Guid.NewGuid(),""),true,out _)));
    }),
    ("official supported BepInEx5.4.23.5 and5.4.23.6 identities accept", () => {
        var harmony=new RuntimeAssemblyInfo("0Harmony",new(2,9,0,0),new("474744d6-5d8e-460f-a08c-d5fd82b5d65f"),"core");
        const string hh="1a21cc03424fc82c3dd1346905d16494536b9595ae4162228d99fb7c285c1031";
        Check(FingerprintPolicy.IsSupportedLoader(new("BepInEx",new(5,4,23,5),new("d1b92069-86c2-41dc-ad96-bb21eee55a97"),"core"),"8255b28902886085c578b9e427d3073c97002db85176d2090cdeda90ef14ce70",harmony,hh));
        Check(FingerprintPolicy.IsSupportedLoader(new("BepInEx",new(5,4,23,6),new("83313e36-e388-498e-a597-9931cf0d08bb"),"core"),"780f175821781c14fdc349bf214603b7bab594ac556aecb1f6679b80248133f0",harmony,hh));
        Check(!FingerprintPolicy.IsSupportedLoader(new("BepInEx",new(5,4,23,4),new("d1b92069-86c2-41dc-ad96-bb21eee55a97"),"core"),"8255b28902886085c578b9e427d3073c97002db85176d2090cdeda90ef14ce70",harmony,hh));
        Check(!FingerprintPolicy.IsSupportedLoader(new("BepInEx",new(5,4,23,5),new("d1b92069-86c2-41dc-ad96-bb21eee55a97"),"core"),"changed",harmony,hh));
        Check(!FingerprintPolicy.IsSupportedLoader(new("BepInEx",new(5,4,23,5),new("d1b92069-86c2-41dc-ad96-bb21eee55a97"),"core"),"8255b28902886085c578b9e427d3073c97002db85176d2090cdeda90ef14ce70",harmony,"changed"));
    }),
    ("original file hashing uses its actual exact bytes", () => {
        WithOriginal("fixture.dll",(dir,hash)=>Equal(hash,FingerprintPolicy.HashFile(Path.Combine(dir,"fixture.dll"))));
    }),
    ("readiness reason logging is globally rate limited", () => {
        var throttle=new ReasonThrottle(); Check(throttle.ShouldLog("not loaded",0)); Check(!throttle.ShouldLog("not loaded",1));
        Check(!throttle.ShouldLog("loading",2)); Check(throttle.ShouldLog("loading",10));
        Check(!throttle.ShouldLog("loading",double.NaN)); Check(!throttle.ShouldLog("loading",double.PositiveInfinity));
    }),
    ("hook unavailable never subscribes or accepts a registration", () => {
        int begin = 0, end = 0, remove = 0;
        using var gate = new ObservationGate(() => false, () => ++begin, () => ++end, () => ++remove);
        var queue = new RegistrationQueue<object>(new(), () => gate.CanObserve, 1);
        Check(!gate.TryStart()); Check(gate.Failure != null); Check(!gate.CanObserve);
        queue.Enqueue(new object(), true); Equal(0, queue.TakeReady().Length); Equal(0, begin); Equal(0, end); Equal(1, remove);
    }),
    ("installer throws after partial hook setup and cleans only attempted hook", () => {
        int hooks = 0, begin = 0, remove = 0;
        using var gate = new ObservationGate(() => { ++hooks; throw new InvalidOperationException("patch failed"); }, () => ++begin, () => throw new Exception("must not begin"), () => { --hooks; ++remove; });
        Check(!gate.TryStart()); Check(gate.Failure != null); Check(!gate.CanObserve); Equal(0,hooks); Equal(0,begin); Equal(1,remove);
        gate.Dispose(); Equal(1,remove);
    }),
    ("hook success precedes subscription and queue acceptance", () => {
        var order = new List<string>(); ObservationGate? gate = null;
        var queue = new RegistrationQueue<object>(new(), () => gate?.CanObserve ?? false, 1);
        using (gate = new ObservationGate(() => { order.Add("hook"); return true; }, () => {
            Equal("hook", order.Single()); Check(!gate!.CanObserve); queue.Enqueue(new object(),true); order.Add("subscribe");
        }, () => order.Add("unsubscribe"), () => order.Add("unpatch"))) {
            Check(gate.TryStart()); Check(gate.CanObserve); Equal(0,queue.TakeReady().Length);
            queue.Enqueue(new object(),true); Equal(1,queue.TakeReady().Length);
        }
        Equal("hook,subscribe,unsubscribe,unpatch",string.Join(",",order));
    }),
    ("partial subscription setup failure releases subscriptions resources and hook", () => {
        int hooks=0, subscriptions=0, resources=0;
        using var gate = new ObservationGate(() => { ++hooks; return true; }, () => { ++subscriptions; ++resources; throw new InvalidOperationException("subscribe failed"); }, () => { --subscriptions; --resources; }, () => --hooks);
        Check(!gate.TryStart()); Check(!gate.CanObserve); Check(gate.Failure != null); Equal(0,hooks); Equal(0,subscriptions); Equal(0,resources);
    }),
    ("prefix throws during install prevents subscription", () => {
        int subscriptions=0, hooks=0; ObservationGate? gate=null;
        using (gate = new ObservationGate(() => { ++hooks; gate!.ObservePrefix(() => throw new InvalidOperationException("epoch failed")); return true; }, () => ++subscriptions, () => --subscriptions, () => --hooks)) {
            Check(!gate.TryStart()); Check(!gate.CanObserve); Check(gate.Failure != null); Equal(0,subscriptions); Equal(0,hooks);
        }
    }),
    ("prefix exception fails closed before queue acceptance or another render", () => {
        int subscriptions=0, hooks=0; var target=new object(); Registration<object>? selected=null;
        ObservationGate? gate=null; var queue=new RegistrationQueue<object>(new(),()=>gate?.CanObserve ?? false,1);
        var bridge=new FakeBridge { CheckValid=()=>selected != null && queue.IsCurrent(selected) };
        using var picture=new MissilePicture(bridge); picture.SetVisible(true);
        using (gate = new ObservationGate(() => { ++hooks; return true; }, () => ++subscriptions, () => { --subscriptions; queue.Clear(); picture.Dispose(); }, () => --hooks)) {
            Check(gate.TryStart()); queue.Enqueue(target,true); selected=queue.TakeReady().Single(); Check(picture.Track(selected.Token)); picture.Tick(0); Equal(1,bridge.Renders);
            queue.Enqueue(new object(),true);
            gate.ObservePrefix(() => throw new InvalidOperationException("prefix failed")); Check(!gate.CanObserve);
            Equal(0,queue.TakeReady().Length); picture.Tick(1); Equal(1,bridge.Renders); Check(!picture.HasSelection);
            Check(!gate.EnsureOperational()); Equal(0,subscriptions); Equal(0,hooks); Equal(1,bridge.Releases);
        }
    }),
    ("two queued same-object registrations reject first stamp", () => {
        using var gate=new ObservationGate(()=>true,()=>{},()=>{},()=>{}); Check(gate.TryStart());
        var epochs=new LifetimeEpochs<object>(); var queue=new RegistrationQueue<object>(epochs,()=>gate.CanObserve,1); var target=new object();
        queue.Enqueue(target,true); queue.Enqueue(target,true); var accepted=queue.TakeReady();
        Equal(1,accepted.Length); Equal(epochs.Read(target),accepted[0].Stamp.Epoch); Check(queue.IsCurrent(accepted[0]));
    }),
    ("repeat registration ends selected token before fresh acceptance no stale frame", () => {
        using var gate=new ObservationGate(()=>true,()=>{},()=>{},()=>{}); Check(gate.TryStart());
        var queue=new RegistrationQueue<object>(new(),()=>gate.CanObserve,1); var target=new object(); Registration<object>? selected=null;
        var bridge=new FakeBridge { CheckValid=()=>selected != null && queue.IsCurrent(selected) }; using var picture=new MissilePicture(bridge); picture.SetVisible(true);
        queue.Enqueue(target,true); selected=queue.TakeReady().Single(); var old=selected; Check(picture.Track(old.Token)); picture.Tick(0); Equal(1,bridge.Renders);
        queue.Enqueue(target,true); queue.Enqueue(target,true); Check(!queue.IsCurrent(old)); picture.Tick(1); Equal(1,bridge.Renders); Check(picture.TrackingEnded); Check(!picture.HasSelection);
        var ready=queue.TakeReady(); Equal(1,ready.Length); Check(!Phase1Policy.IsCurrent(old.Token,ready[0].Token));
        picture.Tick(2); Equal(1,bridge.Renders); // Queue acceptance alone never rebinds the old selection.
        selected=ready[0]; Check(picture.Track(selected.Token)); picture.Tick(3); Equal(2,bridge.Renders); Check(!queue.IsCurrent(old));
    }),
    ("initial OnEnable before registration unchanged epoch accepts", () => {
        var epochs=new LifetimeEpochs<object>(); var target=new object(); epochs.ObserveEnable(target);
        using var gate=new ObservationGate(()=>true,()=>{},()=>{},()=>{}); Check(gate.TryStart());
        var queue=new RegistrationQueue<object>(epochs,()=>gate.CanObserve,1); queue.Enqueue(target,true); var accepted=queue.TakeReady().Single(); Check(queue.IsCurrent(accepted));
    }),
    ("all offline facts required independently", () => {
        var good = OfflineSnapshot.Ready;
        Check(Phase1Policy.CanActivate(true, good));
        for (int bit = 0; bit < OfflineSnapshot.FactCount; ++bit)
            Check(!Phase1Policy.CanActivate(true, new OfflineSnapshot(good.Facts & ~(1u << bit))), $"fact {bit}");
        Check(!Phase1Policy.CanActivate(false, good));
    }),
    ("unknown and multiplayer never activate", () => {
        Check(!Phase1Policy.CanActivate(true, default));
        Check(!Phase1Policy.CanActivate(true, OfflineSnapshot.Ready.Without(OfflineSnapshot.SinglePlayer)));
    }),
    ("token compares scene and lifetime", () => {
        Check(Phase1Policy.IsCurrent(new(1, 2), new(1, 2)));
        Check(!Phase1Policy.IsCurrent(new(1, 2), new(2, 2)));
        Check(!Phase1Policy.IsCurrent(new(1, 2), new(1, 3)));
        Check(!Phase1Policy.IsCurrent(default, default));
    }),
    ("render gating requires every condition", () => {
        for (int bits = 0; bits < 8; ++bits)
            Equal(bits == 7, Phase1Policy.CanRender((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0));
    }),
    ("queued GameObject off/on rejects old epoch", () => EpochRejects()),
    ("queued component disable/enable rejects old epoch", () => EpochRejects()),
    ("selected GameObject off/on ends old lifetime", () => SelectionEpochEnds()),
    ("selected component disable/enable ends old lifetime", () => SelectionEpochEnds()),
    ("unrelated missile enable preserves selection epoch", () => {
        var epochs = new LifetimeEpochs<object>(); var a = new object(); var b = new object();
        long before = epochs.Read(a); epochs.ObserveEnable(b); Equal(before, epochs.Read(a));
    }),
    ("registration invalidation returns its exact epoch", () => {
        var e = new LifetimeEpochs<object>(); var target = new object(); long before = e.Read(target);
        long registration = e.Invalidate(target); Check(registration > before); Equal(registration, e.Read(target));
        e.ObserveEnable(target); Check(!new RegistrationStamp(1, registration, 0).IsAcceptable(1, e.Read(target), 1));
    }),
    ("registration waits next phase", () => {
        var c = new RegistrationStamp(3, 4, 5); Check(!c.IsAcceptable(3, 4, 5)); Check(c.IsAcceptable(3, 4, 6));
    }),
    ("scene and anchor context reject queued candidates", () => {
        Check(!new RegistrationStamp(3, 4, 5).IsAcceptable(4, 4, 6));
    }),
    ("fresh registration may select fresh pooled lifetime", () => {
        var e = new LifetimeEpochs<object>(); var target = new object(); long old = e.Read(target);
        e.ObserveEnable(target); Check(old != e.Read(target));
        Check(new RegistrationStamp(1, e.Read(target), 4).IsAcceptable(1, e.Read(target), 5));
        var b = new FakeBridge(); var p = new MissilePicture(b); Check(p.Track(new(1, 1))); b.Valid = false; p.Tick(0);
        Check(p.TrackingEnded); b.Valid = true; Check(p.Track(new(1, 2))); Check(!p.TrackingEnded);
    }),
    ("hidden never renders", () => { var b = new FakeBridge(); var p = new MissilePicture(b); p.Track(new(1,1)); p.Tick(0); Equal(0,b.Renders); }),
    ("invalid target ends without auto switch", () => {
        var b = new FakeBridge(); var p = new MissilePicture(b); p.SetVisible(true); p.Track(new(1,1)); b.Valid = false; p.Tick(0);
        Check(p.TrackingEnded); b.Valid = true; p.Tick(1); Equal(0,b.Renders); Check(!p.HasSelection);
    }),
    ("guard lost stops and releases resources once", () => {
        var b = new FakeBridge(); var p = new MissilePicture(b); p.SetVisible(true); p.Track(new(1,1)); p.Tick(0); b.Ready = false; p.Tick(1); p.Dispose(); p.Dispose(); Equal(1,b.Releases); Equal(1,b.Renders);
    }),
    ("20Hz cadence no catchup and one request per tick", () => {
        var b = new FakeBridge(); var p = new MissilePicture(b); p.SetVisible(true); p.Track(new(1,1));
        p.Tick(0); p.Tick(0.001); p.Tick(0.049); Equal(1,b.Renders); p.Tick(0.05); Equal(2,b.Renders);
        p.Tick(10); Equal(3,b.Renders); p.Tick(10); p.Tick(10.01); Equal(3,b.Renders); p.Tick(10.05); Equal(4,b.Renders);
    }),
    ("pause emits no bursts on resume", () => {
        var b = new FakeBridge(); var p = new MissilePicture(b); p.SetVisible(true); p.Track(new(1,1)); p.Tick(1); p.Tick(1); Equal(1,b.Renders); p.Tick(100); Equal(2,b.Renders);
    }),
    ("invalid timestamps never render", () => { var b = new FakeBridge(); var p = new MissilePicture(b); p.SetVisible(true); p.Track(new(1,1)); p.Tick(double.NaN); p.Tick(double.PositiveInfinity); Equal(0,b.Renders); }),
    ("hide releases owned resources and show can recreate", () => {
        var b = new FakeBridge(); var p = new MissilePicture(b); p.SetVisible(true); p.Track(new(1,1)); p.Tick(0); p.SetVisible(false); p.SetVisible(false); Equal(1,b.Releases); p.Tick(1); Equal(1,b.Renders); p.SetVisible(true); p.Tick(2); Equal(2,b.Renders);
    }),
    ("render failure releases and latches fault", () => {
        var b = new FakeBridge { FailRender = true }; var p = new MissilePicture(b); p.SetVisible(true); p.Track(new(1,1)); p.Tick(0); Check(p.Faulted); Equal(1,b.Releases); b.FailRender = false; p.Tick(1); Equal(1,b.Renders); p.Dispose(); Equal(1,b.Releases);
    }),
    ("dispose before first render is harmless and final", () => {
        var b = new FakeBridge(); var p = new MissilePicture(b); p.Dispose(); p.Dispose(); Check(!p.Track(new(1,1))); p.SetVisible(true); p.Tick(0); Equal(0,b.Renders); Equal(0,b.Releases);
    }),
    ("explicit end invalidates old selection before fresh track", () => {
        var b = new FakeBridge(); var p = new MissilePicture(b); p.Track(new(1,1)); p.EndTracking(); Check(!p.HasSelection); Check(p.TrackingEnded); p.Tick(0); Equal(0,b.Renders);
    }),
    ("picture defaults fixed dimensions and cadence", () => { Equal(480,MissilePicture.Width); Equal(270,MissilePicture.Height); Equal(0.05,MissilePicture.Interval); }),
};
int failures = 0;
tests = tests.Concat(ManualTests.Cases).Concat(LaunchTests.Cases).Concat(MapGestureTests.Cases).Concat(ChaseViewTests.Cases).Concat(FullscreenTests.Cases).Concat(ScopedInputTests.Cases).Concat(PilotEligibilityTests.Cases).Concat(FlightHoldTests.Cases).Concat(HoldControlTests.Cases).Concat(TerrainTests.Cases).ToArray();
foreach (var test in tests) { try { test.Body(); Console.WriteLine($"PASS {test.Name}"); } catch(Exception e) { ++failures; Console.WriteLine($"FAIL {test.Name}: {e.Message}"); } }
Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed; {failures} failed");
return failures == 0 ? 0 : 1;

static void Check(bool condition, string message = "assertion failed") { if (!condition) throw new Exception(message); }
static void Equal<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"expected {expected}, got {actual}"); }
static void WithOriginal(string filename, Action<string,string> body) {
    string dir=Path.Combine(Path.GetTempPath(),"observer-fingerprint-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
    try { var bytes=System.Text.Encoding.UTF8.GetBytes("our synthetic original DLL fixture; not a game assembly"); File.WriteAllBytes(Path.Combine(dir,filename),bytes); string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(); body(dir,hash); }
    finally { Directory.Delete(dir,true); }
}
static void EpochRejects() { var e = new LifetimeEpochs<object>(); var target = new object(); var queued = new RegistrationStamp(1,e.Read(target),0); e.ObserveEnable(target); Check(!queued.IsAcceptable(1,e.Read(target),1)); }
static void SelectionEpochEnds() { var e = new LifetimeEpochs<object>(); var target = new object(); long captured = e.Read(target); var b = new FakeBridge { CheckValid = () => e.Read(target) == captured }; var p = new MissilePicture(b); p.SetVisible(true); p.Track(new(1,1)); e.ObserveEnable(target); p.Tick(0); Check(p.TrackingEnded); Equal(0,b.Renders); }
sealed class FakeBridge : IPictureBridge {
    public bool Valid = true, Ready = true, FailRender; public Func<bool>? CheckValid; public int Renders,Releases;
    public bool IsReady => Ready;
    public bool IsCurrent(EntityToken token) => Valid && (CheckValid?.Invoke() ?? true);
    public void Render(EntityToken token) { ++Renders; if (FailRender) throw new InvalidOperationException("allocation/render failed"); }
    public void Release() => ++Releases;
}
