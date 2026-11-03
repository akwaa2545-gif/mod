// Mutable input fixture, not game code. No Unity/Rewired assembly references or execution.
namespace UnityEngine { public static class Time {public static int frameCount;} }
namespace Rewired {
 public enum ControllerType {Mouse} public enum ControllerElementType {Axis,Button} public enum MouseOtherAxisMode {MouseAxis,DigitalAxis}
 public class Identifier {public int id=2;public string key="wheel/vertical";public ControllerElementType elementType=ControllerElementType.Axis;}
 public class Mouse {public float Raw;public int Index=2;public List<Identifier> AxisElementIdentifiers=new(){new()};public int GetAxisIndexById(int id)=>Index;public float GetAxisRawById(int id)=>Raw;}
 public class ActionElementMap {public int id,actionId=1,elementIdentifierId=2,axisRange,axisContribution;public bool invert,enabled=true;public ControllerElementType elementType=ControllerElementType.Axis;public ControllerMap controllerMap;}
 public class ControllerMap {public int id;public List<ActionElementMap> AllMaps=new();}
 public class MapSet {public List<ControllerMap> Maps=new();public IEnumerable<ControllerMap> GetAllMaps(ControllerType type)=>Maps;}
 public class PlayerControllers {public MapSet maps=new();} public class Player {public int id;public float Cached;public bool Button;public PlayerControllers controllers=new();public float GetAxis(int id)=>Cached;public float GetAxisRaw(int id)=>Cached;public bool GetButton(int id)=>Button;}
 public class InputAction {public int behaviorId=1;} public class InputBehavior {public MouseOtherAxisMode mouseOtherAxisMode;}
 public class Mapping {public InputAction Action=new();public InputBehavior Behavior=new();public InputAction GetAction(int id)=>Action;public InputBehavior GetInputBehavior(int player,int behavior)=>Behavior;}
 public class Players {public List<Player> AllPlayers=new();} public class Controllers {public Mouse Mouse=new();}
 public static class ReInput {public static bool isReady=true;public static Players players=new();public static Controllers controllers=new();public static Mapping mapping=new();public static event Action InputSourceUpdateEvent;public static void Boundary()=>InputSourceUpdateEvent?.Invoke();public static void Reset(){players=new();controllers=new();mapping=new();isReady=true;}}
}
