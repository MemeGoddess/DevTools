using System.Reflection;
using HarmonyLib;
using Verse;

namespace DevTools.Testing.Utils;

public static class Tweaker
{
    private static FieldInfo UltraSpeedBoost = AccessTools.Field(typeof(TickManager), "UltraSpeedBoost");
    private static FieldInfo CurTimeSpeed = AccessTools.Field(typeof(TickManager), "curTimeSpeed");
    public static bool SuperFast
    {
        get => UltraSpeedBoost.GetValue(null) is true;
        set
        {
            UltraSpeedBoost.SetValue(null, value); 
            CurTimeSpeed.SetValue(Find.TickManager, value ? TimeSpeed.Ultrafast : TimeSpeed.Fast);
            DebugViewSettings.neverForceNormalSpeed = value;
        }
    }
}