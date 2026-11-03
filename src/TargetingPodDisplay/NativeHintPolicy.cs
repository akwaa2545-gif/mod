namespace NuclearOptionTargetingPod
{
    internal static class NativeHintPolicy
    {
        internal static bool Allowed(string text,bool graphicEnabled)
        {
            switch(text)
            {
                case "OUT OF RANGE":case "TOO CLOSE":case "OUT OF ARC":case "TOO SLOW":return true;
                // Native intentionally withholds this inside some envelope ranges; retain that intent.
                case "SHOOT":return graphicEnabled;
                default:return false;
            }
        }
    }
}
