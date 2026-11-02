using NuclearOptionObserver;
static class PilotEligibilityTests
{
    static void Check(bool value){if(!value)throw new Exception("pilot predicate assertion failed");}
    public static (string Name,Action Body)[] Cases => new (string,Action)[]{
        ("native pilot identity gate rejects remote/dead/mismatched contexts across64 combinations",()=>{
            for(int n=0;n<64;n++){
                bool present=(n&1)!=0,dead=(n&2)!=0,ejected=(n&4)!=0,localPlayer=(n&8)!=0,sameAircraft=(n&16)!=0,sameState=(n&32)!=0;
                bool expected=present&&!dead&&!ejected&&localPlayer&&sameAircraft&&sameState;
                Check((PilotEligibility.Evaluate(present,dead,ejected,localPlayer,sameAircraft,sameState)==PilotRejection.None)==expected);
            }
        }),
        ("null or remote native player cannot qualify even in an otherwise matching pilot state",()=>{
            Check(PilotEligibility.Evaluate(true,false,false,false,true,true)==PilotRejection.NotLocalPlayer);
            Check(PilotEligibility.Evaluate(true,false,false,true,true,true)==PilotRejection.None);
        }),
        ("pilot failure diagnostics distinguish local-player and current-state rejection",()=>{
            Check(PilotEligibility.Evaluate(true,false,false,false,true,true)==PilotRejection.NotLocalPlayer);
            Check(PilotEligibility.Evaluate(true,false,false,true,true,false)==PilotRejection.OtherState);
            Check(PilotEligibility.Evaluate(true,false,false,true,false,true)==PilotRejection.OtherAircraft);
            Check(PilotEligibility.Evaluate(true,true,false,true,true,true)==PilotRejection.Dead);
        }),
    };
}
