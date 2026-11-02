using NuclearOptionObserver;

static class ManualTests
{
    static void Check(bool value) { if (!value) throw new Exception("manual behavior assertion failed"); }
    static MissileCandidate<object> Item(object target, ulong id=1, long epoch=0, string label="Missile owner") => new(target,id,epoch,label);
    public static (string Name, Action Body)[] Cases => new (string,Action)[] {
        ("toggle PiP and Next share safe gameplay or panel scope without editing or modifiers",()=>{
            Check(InputPolicy.MissileShortcutAllowed(false,false,false,true));
            Check(InputPolicy.MissileShortcutAllowed(false,false,true,false));
            Check(!InputPolicy.MissileShortcutAllowed(false,false,false,false));
            Check(!InputPolicy.MissileShortcutAllowed(true,false,true,true));
            Check(!InputPolicy.MissileShortcutAllowed(false,true,true,true));
        }),

        ("global refresh accepts all candidate owners without selecting or opening", () => {
            var c=new ManualCatalog<object>(1); c.Refresh(new[]{Item(new(),1),Item(new(),2),Item(new(),3)});
            Check(c.Rows.Count==3 && c.Selected==null && !c.Visible);
        }),
        ("refresh preserves stable lifetime token despite list order and labels", () => {
            var a=new object(); var b=new object(); var c=new ManualCatalog<object>(1); c.Refresh(new[]{Item(a),Item(b,2)}); var token=c.Rows[0].Token;
            c.Refresh(new[]{Item(b,2),Item(a,1,0,"changed distance")}); Check(Phase1Policy.IsCurrent(token,c.Rows[0].Token));
        }),
        ("manual selection does not open and launch refresh cannot switch or open", () => {
            var a=new object(); var c=new ManualCatalog<object>(1); c.Refresh(new[]{Item(a)}); Check(c.Select(c.Rows[0].Token)); Check(!c.Visible);
            c.Refresh(new[]{Item(a),Item(new(),2)}); Check(ReferenceEquals(a,c.Selected!.Candidate.Target) && !c.Visible);
            c.ToggleVisible(); Check(c.Visible); c.Refresh(new[]{Item(a),Item(new(),3)}); Check(c.Visible && ReferenceEquals(a,c.Selected!.Candidate.Target));
        }),
        ("epoch change rejects stale row and ends without switching", () => {
            var a=new object(); var c=new ManualCatalog<object>(1); c.Refresh(new[]{Item(a)}); var token=c.Rows[0].Token; c.Select(token);
            c.Refresh(new[]{Item(a,1,1),Item(new(),2)}); Check(c.Selected==null && !c.Select(token)); Check(!Phase1Policy.IsCurrent(token,c.Rows[0].Token));
        }),
        ("removed and replacement managed object never inherit selection", () => {
            var c=new ManualCatalog<object>(1); c.Refresh(new[]{Item(new())}); var token=c.Rows[0].Token; c.Select(token);
            c.Refresh(new[]{Item(new())}); Check(c.Selected==null && !c.Select(token)); c.Clear(); Check(c.Selected==null);
        }),
        ("different scene context rejects previous lifetime token", () => {
            var a=new object(); var c=new ManualCatalog<object>(1); c.Refresh(new[]{Item(a)}); var old=c.Rows[0].Token;
            var d=new ManualCatalog<object>(2); d.Refresh(new[]{Item(a)}); Check(!d.Select(old));
        }),
        ("snapshot identity accepts the same reference persistent ID and epoch", () => {
            var target=new object(); Check(Item(target,7,4).MatchesIdentity(target,7,4));
        }),
        ("stale row rejects changed persistent ID without an OnEnable epoch", () => {
            var target=new object(); Check(!Item(target,7,4).MatchesIdentity(target,8,4));
        }),
        ("snapshot identity rejects replaced reference even with equal ID and epoch", () => {
            Check(!Item(new object(),7,4).MatchesIdentity(new object(),7,4));
        }),
        ("snapshot identity rejects changed epoch even with equal reference and ID", () => {
            var target=new object(); Check(!Item(target,7,4).MatchesIdentity(target,7,5));
        }),
        ("observing a known missile never selects or shows by itself",()=>{
            var c=new ManualCatalog<object>(1);var target=new object();var row=c.Observe(Item(target,7,4));Check(row!=null&&c.Rows.Count==1&&c.Selected==null&&!c.Visible);
            var again=c.Observe(Item(target,7,4));Check(again!=null&&Phase1Policy.IsCurrent(row!.Token,again.Token));
        }),
        ("explicit select and show is idempotent and independent of auto setting",()=>{
            var c=new ManualCatalog<object>(1);var row=c.Observe(Item(new(),7,4));Check(row!=null&&c.Select(row.Token,true)&&c.Visible);Check(c.Select(row!.Token,true)&&c.Visible);
        }),
        ("manual next previous wrap without selecting or opening automatically", () => {
            var c=new ManualCatalog<object>(1); c.Refresh(new[]{Item(new(),1),Item(new(),2),Item(new(),3)});
            Check(c.Adjacent(1)!.Candidate.Id==1 && c.Adjacent(-1)!.Candidate.Id==3 && c.Selected==null);
            c.Select(c.Adjacent(1)!.Token); Check(c.Selected!.Candidate.Id==1); c.Select(c.Adjacent(-1)!.Token); Check(c.Selected!.Candidate.Id==3);
            c.Select(c.Adjacent(1)!.Token); Check(c.Selected!.Candidate.Id==1 && !c.Visible);
            c.Refresh(Array.Empty<MissileCandidate<object>>()); Check(c.Adjacent(1)==null && c.Adjacent(-1)==null && c.Selected==null);
        }),
        ("registry refresh cadence is bounded without catchup", () => {
            var rate=new RefreshCadence(); Check(rate.Due(0)); Check(!rate.Due(.1)); Check(rate.Due(.5)); Check(rate.Due(100)); Check(!rate.Due(100)); Check(!rate.Due(double.NaN));
        }),
        ("layout clamps resolution scale and invalid numeric settings", () => {
            var r=ViewLayout.Clamp(5000,-20,5000,4,800,600); Check(r.X>=0 && r.Y>=0 && r.X+r.Width<=800 && r.Y+r.Height+40<=600 && r.Scale<=1.5f);
            var tiny=ViewLayout.Clamp(float.NaN,float.PositiveInfinity,-1,float.NaN,200,120); Check(float.IsFinite(tiny.X) && tiny.Width>0 && tiny.Height+40<=120);
        }),
        ("visible PiP blocks observer map capture over image and status using screen Y conversion",()=>{
            var l=ViewLayout.Clamp(100,80,480,1,1920,1080);
            Check(l.BlocksPointer(true,110,1080-90,1080));
            Check(l.BlocksPointer(true,110,1080-(80+l.Height+20),1080));
            Check(!l.BlocksPointer(true,99,1080-90,1080)&&!l.BlocksPointer(true,110,1080-(80+l.Height+40),1080));
        }),
        ("hidden PiP does not block map watch and clamped scaled layout matches hit geometry",()=>{
            var l=ViewLayout.Clamp(5000,5000,1280,1.5f,640,360);float sx=l.X+l.Width/2,sy=360-(l.Y+l.Height+20);
            Check(l.BlocksPointer(true,sx,sy,360)&&!l.BlocksPointer(false,sx,sy,360));
            Check(!l.BlocksPointer(true,float.NaN,sy,360)&&!l.BlocksPointer(true,sx,float.PositiveInfinity,360));
        }),
        ("settings persistence round trip and migration preserve enabled not auto open", () => {
            var v=ObserverPreferences.Normalize(true,0,100,80,600,1.25f,289,290,291); Check(v.Enabled && v.Schema==2 && !v.StartVisible);
            var r=ObserverPreferences.Normalize(v.Enabled,v.Schema,v.X,v.Y,v.Width,v.Scale,v.PanelKey,v.ViewKey,v.CycleKey);
            Check(r.X==100 && r.Y==80 && r.Width==600 && r.Scale==1.25f && r.PanelKey==289 && r.ViewKey==290 && r.CycleKey==291 && !r.StartVisible);
        }),
        ("hotkey collisions unsafe keys and invalid config fall back safely", () => {
            Check(!ShortcutPolicy.Valid(32,290,291)); Check(!ShortcutPolicy.Valid(289,289,291)); Check(ShortcutPolicy.Valid(289,290,291));
            var s=ObserverPreferences.Normalize(true,1,0,0,480,1,32,32,32); Check(s.PanelKey==289 && s.ViewKey==290 && s.CycleKey==291);
        }),
        ("function key event flags are not modifier chords", () => {
            Check(ShortcutPolicy.NoChordModifiers(0) && ShortcutPolicy.NoChordModifiers(64) && ShortcutPolicy.NoChordModifiers(32));
            foreach(int chord in new[]{1,2,4,8,65,66,68,72}) Check(!ShortcutPolicy.NoChordModifiers(chord));
        }),
        ("panel requires paused native menu cursor focus and no selected text", () => {
            Check(InputPolicy.PanelAllowed(true,true,true,true,false));
            for(int bits=0;bits<16;bits++) Check(!InputPolicy.PanelAllowed((bits&1)!=0,(bits&2)!=0,(bits&4)!=0,(bits&8)!=0,true));
            Check(!InputPolicy.PanelAllowed(true,false,true,true,false)); Check(!InputPolicy.PanelAllowed(true,true,false,true,false));
            Check(!InputPolicy.GameplayShortcutAllowed(true,true,false,false)); Check(InputPolicy.GameplayShortcutAllowed(true,false,false,false));
            Check(!InputPolicy.GameplayShortcutAllowed(true,false,true,false)); Check(!InputPolicy.GameplayShortcutAllowed(true,false,false,true));
        }),
        ("paused panel requires every readiness and weapon safety fact", () => {
            for(int bits=0;bits<64;bits++) Check(InputPolicy.PanelAllowed((bits&1)!=0,(bits&2)!=0,(bits&4)!=0,(bits&8)!=0,(bits&16)!=0,(bits&32)!=0)==(bits==47));
        }),
        ("modal failed creation rolls back and repeated teardown is idempotent", () => {
            int owned=0,closes=0; var modal=new ModalSession(()=>{owned++;throw new Exception("partial");},()=>{owned--;closes++;});
            Check(!modal.TryOpen() && !modal.IsOpen && owned==0 && closes==1); modal.Dispose(); modal.Dispose(); Check(closes==1);
        }),
        ("modal guard loss closes once and refuses further interaction", () => {
            int owned=0; var modal=new ModalSession(()=>{owned++;return true;},()=>owned--); Check(modal.TryOpen());
            Check(!modal.Maintain(false) && !modal.IsOpen && owned==0); Check(!modal.Maintain(true) && owned==0); modal.Dispose();
        }),
        ("declined modal creation unwinds partial ownership", () => {
            int owned=0; var modal=new ModalSession(()=>{owned++;return false;},()=>owned--); Check(!modal.TryOpen() && owned==0); modal.Dispose(); Check(owned==0);
        }),
        ("OnGUI guard close persists once before later update and teardown", () => {
            int owned=0,saves=0; bool capturing=true; var modal=new ModalSession(()=>{owned++;return true;},()=>owned--,()=>{Check(owned==0);capturing=false;saves++;});
            Check(modal.TryOpen()); Check(!modal.Maintain(false)); modal.Close(); modal.Maintain(false); modal.Dispose();
            Check(saves==1 && owned==0 && !capturing);
        }),
        ("failed modal opening never writes settings as if an opened panel closed", () => {
            int saves=0; var modal=new ModalSession(()=>false,()=>{},()=>saves++); Check(!modal.TryOpen()); modal.Close(); modal.Dispose(); Check(saves==0);
        }),
        ("save failure cannot prevent modal resource cleanup or repeated teardown", () => {
            int owned=0,saves=0; var modal=new ModalSession(()=>{owned++;return true;},()=>owned--,()=>{saves++;throw new IOException("save failed");});
            Check(modal.TryOpen()); modal.Close(); modal.Dispose(); Check(owned==0 && saves==1 && modal.Failure!=null);
        }),
        ("modal open close owns only its resources and can reopen", () => {
            int owned=0; var modal=new ModalSession(()=>{owned++;return true;},()=>owned--); Check(modal.TryOpen()); Check(modal.TryOpen() && owned==1);
            modal.Close(); modal.Close(); Check(owned==0); Check(modal.TryOpen() && owned==1); modal.Dispose(); Check(owned==0 && !modal.TryOpen());
        }),
    };
}
